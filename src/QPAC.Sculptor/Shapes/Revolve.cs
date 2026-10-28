using System;
using System.Collections.Generic;
using System.Linq;

namespace QPAC.Sculptor
{
    /// <summary>
    /// A closed profile of (radius, height) points spun about an axis — the lathe operation. Covers
    /// flanges, bells, inlet cones, rings, knobs and anything else with rotational symmetry. A partial
    /// <see cref="AngleDegrees"/> leaves two flat end faces.
    /// </summary>
    public class Revolve : StepShape
    {
        public string Name;
        public (double X, double Y, double Z) Origin;
        public (double X, double Y, double Z) Axis;

        /// <summary>Direction of angle 0 (perpendicular to the axis). Leave default to pick one automatically.</summary>
        public (double X, double Y, double Z) ReferenceDirection;

        /// <summary>Profile points as (R = distance from the axis, Z = position along the axis). R must be ≥ 0.</summary>
        public List<(double R, double Z)> Profile = new List<(double R, double Z)>();
        public double StartDegrees;
        public double AngleDegrees = 360;
        public int Segments = 64;

        public Revolve(string name, (double X, double Y, double Z) origin, (double X, double Y, double Z) axis,
            IEnumerable<(double R, double Z)> profile, double angleDegrees = 360, int segments = 64)
        {
            Name = name;
            Origin = origin;
            Axis = axis;
            Profile = profile.ToList();
            AngleDegrees = angleDegrees;
            Segments = segments;
        }

        public override string ShapeName => Name;

        /// <summary>Rounds the listed profile corners (indices into <see cref="Profile"/>), or all when none are given.</summary>
        public Revolve FilletProfile(double radius, int segmentsPerCorner = 6, params int[] corners)
        {
            var loop = Profile.Select(p => (U: p.R, V: p.Z)).ToList();
            var rounded = corners.Length == 0
                ? Profiles.Fillet(loop, radius, segmentsPerCorner)
                : Profiles.FilletCorners(loop, radius, segmentsPerCorner, corners);
            Profile = rounded.Select(p => (Math.Max(0, p.U), p.V)).ToList();
            return this;
        }

        public override Polyhedron CreateSolid() => Build(Origin, Axis, ReferenceDirection, Profile, StartDegrees, AngleDegrees, Segments, Name);

        internal static Polyhedron Build(
            (double X, double Y, double Z) origin,
            (double X, double Y, double Z) axis,
            (double X, double Y, double Z) refDir,
            IReadOnlyList<(double R, double Z)> profileIn,
            double startDegrees, double angleDegrees, int segments, string name)
        {
            var solid = new Polyhedron();
            var w = Vec.Normalize(axis);
            if (w == (0, 0, 0)) return solid;
            var profile = Polygon2D.Clean(profileIn.Select(p => (U: p.R, V: p.Z))).Select(p => (R: p.U, Z: p.V)).ToList();
            if (profile.Count < 3) return solid;
            if (profile.Any(p => p.R < -1e-9)) throw new ArgumentException($"{name}: revolve profile crosses the axis (negative radius).");
            if (Math.Abs(Polygon2D.SignedArea(profile.Select(p => (p.R, p.Z)).ToList())) < 1e-15) return solid;

            var u = Vec.Normalize(Vec.Sub(refDir, Vec.Scale(w, Vec.Dot(refDir, w))));
            if (u == (0, 0, 0)) u = Vec.AnyPerpendicular(w);
            var v = Vec.Cross(w, u);

            double sweep = Math.Max(-360, Math.Min(360, angleDegrees));
            bool full = Math.Abs(Math.Abs(sweep) - 360) < 1e-9;
            int n = Math.Max(full ? 8 : 1, (int)Math.Ceiling(Math.Max(3, segments) * Math.Abs(sweep) / 360.0));
            int columns = full ? n : n + 1;
            double a0 = startDegrees * Math.PI / 180, da = sweep * Math.PI / 180 / n;

            var idx = new int[profile.Count, columns];
            for (int j = 0; j < columns; j++)
            {
                double a = a0 + da * j, c = Math.Cos(a), s = Math.Sin(a);
                for (int i = 0; i < profile.Count; i++)
                {
                    double r = Math.Max(0, profile[i].R);
                    idx[i, j] = solid.AddVertex(Vec.Frame(origin, u, v, w, r * c, r * s, profile[i].Z));
                }
            }

            for (int i = 0; i < profile.Count; i++)
            {
                int i2 = (i + 1) % profile.Count;
                for (int j = 0; j < n; j++)
                {
                    int j2 = (j + 1) % columns;
                    solid.AddFace(new[] { idx[i, j], idx[i2, j], idx[i2, j2], idx[i, j2] });
                }
            }

            if (!full)
            {
                solid.AddFace(Enumerable.Range(0, profile.Count).Select(i => idx[i, 0]).ToArray());
                solid.AddFace(Enumerable.Range(0, profile.Count).Select(i => idx[i, n]).ToArray());
            }
            TagSurfaces(solid, idx, profile, origin, u, v, w, a0, da, n, full);
            return solid;
        }

        // Profile edge i swept around the axis made faces i*n … i*n+n−1. Each straight profile edge sweeps
        // a plane, cylinder or cone and each profile arc a sphere or torus. A full turn is split into two
        // halves so that no face or circle closes on itself.
        private static void TagSurfaces(
            Polyhedron solid, int[,] idx, List<(double R, double Z)> profile,
            (double X, double Y, double Z) origin, (double X, double Y, double Z) u, (double X, double Y, double Z) v, (double X, double Y, double Z) w,
            double a0, double da, int n, bool full)
        {
            int m = profile.Count;
            double size = Math.Max(1e-9, profile.Max(p => Math.Max(Math.Abs(p.R), Math.Abs(p.Z))));
            double tol = 1e-9 * Math.Max(1, size);
            (double X, double Y, double Z) OnAxis(double z) => Vec.Add(origin, Vec.Scale(w, z));
            (double X, double Y, double Z) Dir(int j) => Vec.Add(Vec.Scale(u, Math.Cos(a0 + da * j)), Vec.Scale(v, Math.Sin(a0 + da * j)));

            var group = Enumerable.Range(0, m).ToArray();
            var arcOf = new Arc2D?[m];
            foreach (var arc in Arcs.Find(profile.Select(p => (U: p.R, V: p.Z)).ToList(), closed: true))
                for (int e = 0; e < arc.Segments; e++)
                {
                    int i = (arc.Start + e) % m;
                    group[i] = arc.Start;
                    arcOf[i] = arc;
                }

            AnalyticSurface SurfaceFor(int i) => arcOf[i] is Arc2D arc
                ? Arcs.RevolvedArc((arc.Center.U, arc.Center.V), arc.Radius, origin, w, tol)
                : Arcs.Revolved(profile[i], profile[(i + 1) % m], origin, w, tol);

            int[] splits = full ? new[] { 0, n / 2, n } : new[] { 0, n };
            for (int i = 0; i < m; i++)
            {
                if (group[i] != i && arcOf[i] != null) continue;
                var members = Enumerable.Range(0, m).Where(k => group[k] == group[i]).ToList();
                for (int h = 0; h + 1 < splits.Length; h++)
                {
                    var surface = SurfaceFor(i);
                    if (surface == null) continue;
                    foreach (int k in members)
                        for (int j = splits[h]; j < splits[h + 1]; j++)
                            solid.Faces[k * n + j].Surface = surface;
                }
            }

            int columns = idx.GetLength(1);
            int[] Column(int i, int j0, int j1) => Enumerable.Range(j0, j1 - j0 + 1).Select(j => idx[i, j % columns]).ToArray();

            // Circles traced by profile vertices where one surface ends and the next begins.
            for (int i = 0; i < m; i++)
            {
                int before = (i - 1 + m) % m;
                if (group[before] == group[i] || profile[i].R <= tol) continue;
                var circle = new Circle3D(OnAxis(profile[i].Z), w, profile[i].R);
                for (int h = 0; h + 1 < splits.Length; h++)
                    solid.Curves.Add(new CurveRun(Column(i, splits[h], splits[h + 1]), circle));
            }

            // Profile arcs on the seams between halves and on the end faces of a partial revolve.
            foreach (int j in splits.Distinct().Where(j => full ? j < n : true))
                foreach (var arc in arcOf.Where(a => a.HasValue).Select(a => a.Value).Distinct())
                {
                    var dir = Dir(j);
                    var center = Vec.Add(OnAxis(arc.Center.V), Vec.Scale(dir, arc.Center.U));
                    var chain = Enumerable.Range(0, arc.Segments + 1).Select(e => idx[(arc.Start + e) % m, j]).ToArray();
                    solid.Curves.Add(new CurveRun(chain, new Circle3D(center, Vec.Cross(w, dir), arc.Radius)));
                }
        }
    }

    /// <summary>A UV sphere.</summary>
    public class Sphere : StepShape
    {
        public string Name;
        public double Cx, Cy, Cz;
        public double Radius;
        public int Segments, Rings;

        public Sphere(string name, double cx, double cy, double cz, double radius, int segments = 32, int rings = 16)
        {
            Name = name; Cx = cx; Cy = cy; Cz = cz; Radius = radius; Segments = segments; Rings = rings;
        }

        public override string ShapeName => Name;

        public override Polyhedron CreateSolid()
        {
            if (Radius <= 0) return new Polyhedron();
            int k = Math.Max(2, Rings);
            var profile = new List<(double R, double Z)>();
            for (int i = 0; i <= k; i++)
            {
                double phi = Math.PI * i / k;
                profile.Add((Radius * Math.Sin(phi), -Radius * Math.Cos(phi)));
            }
            return Revolve.Build((Cx, Cy, Cz), (0, 0, 1), (1, 0, 0), profile, 0, 360, Segments, Name);
        }
    }

    /// <summary>A ring doughnut around <see cref="Axis"/>; <see cref="MajorRadius"/> to the tube centre, <see cref="MinorRadius"/> of the tube.</summary>
    public class Torus : StepShape
    {
        public string Name;
        public (double X, double Y, double Z) Center;
        public (double X, double Y, double Z) Axis;
        public double MajorRadius, MinorRadius;
        public int Segments, TubeSegments;

        public Torus(string name, (double X, double Y, double Z) center, (double X, double Y, double Z) axis,
            double majorRadius, double minorRadius, int segments = 48, int tubeSegments = 16)
        {
            Name = name; Center = center; Axis = axis;
            MajorRadius = majorRadius; MinorRadius = minorRadius;
            Segments = segments; TubeSegments = tubeSegments;
        }

        public override string ShapeName => Name;

        public override Polyhedron CreateSolid()
        {
            if (MinorRadius <= 0 || MajorRadius < MinorRadius)
                throw new ArgumentException($"{Name}: torus needs 0 < MinorRadius ≤ MajorRadius.");
            var profile = Profiles.Circle(MajorRadius, 0, MinorRadius, TubeSegments).Select(p => (R: p.U, Z: p.V)).ToList();
            return Revolve.Build(Center, Axis, default, profile, 0, 360, Segments, Name);
        }
    }

    /// <summary>
    /// A hollow (or, with <see cref="InnerRadius"/> 0, solid) round tube between two points — pipe,
    /// conduit, sleeve, spacer, bushing.
    /// </summary>
    public class Tube : StepShape
    {
        public string Name;
        public (double X, double Y, double Z) Start, End;
        public double OuterRadius, InnerRadius;
        public int Sides;

        public Tube(string name, (double X, double Y, double Z) start, (double X, double Y, double Z) end,
            double outerRadius, double innerRadius, int sides = 32)
        {
            Name = name; Start = start; End = end;
            OuterRadius = outerRadius; InnerRadius = innerRadius; Sides = sides;
        }

        public override string ShapeName => Name;

        public override Polyhedron CreateSolid()
        {
            var axis = Vec.Sub(End, Start);
            double len = Vec.Length(axis);
            if (len < 1e-9 || OuterRadius <= 0) return new Polyhedron();
            if (InnerRadius >= OuterRadius) throw new ArgumentException($"{Name}: InnerRadius must be smaller than OuterRadius.");
            double ri = Math.Max(0, InnerRadius);
            var profile = new List<(double R, double Z)> { (ri, 0), (OuterRadius, 0), (OuterRadius, len), (ri, len) };
            return Revolve.Build(Start, axis, default, profile, 0, 360, Sides, Name);
        }
    }
}
