using System;
using System.Collections.Generic;
using System.Linq;

namespace QPAC.Sculptor
{
    /// <summary>
    /// A 2D profile carried along a 3D polyline path — wire harnesses, piping, handrails, bent rod and
    /// formed trim. Corners can be rounded with <see cref="BendRadius"/>; the profile keeps a twist-free
    /// orientation along the path.
    /// </summary>
    public class Sweep : StepShape
    {
        public string Name;
        public List<(double U, double V)> Profile;
        public List<(double X, double Y, double Z)> Path;
        public double BendRadius;
        public int BendSegments = 8;

        /// <summary>Which way the profile's U axis points at the start of the path (default: chosen automatically).</summary>
        public (double X, double Y, double Z) Up;

        public Sweep(string name, IEnumerable<(double U, double V)> profile, IEnumerable<(double X, double Y, double Z)> path, double bendRadius = 0, int bendSegments = 8)
        {
            Name = name;
            Profile = profile.ToList();
            Path = path.ToList();
            BendRadius = bendRadius;
            BendSegments = bendSegments;
        }

        /// <summary>A round rod or wire of <paramref name="radius"/> along the path.</summary>
        public static Sweep Rod(string name, IEnumerable<(double X, double Y, double Z)> path, double radius, double bendRadius = 0, int sides = 16)
            => new Sweep(name, Profiles.Circle(0, 0, radius, sides), path, bendRadius);

        public override string ShapeName => Name;

        /// <summary>A rounded corner of the path: points turn positively about <see cref="Axis"/> through <see cref="Center"/>.</summary>
        internal readonly struct Bend
        {
            public readonly (double X, double Y, double Z) Center;
            public readonly (double X, double Y, double Z) Axis;

            public Bend((double X, double Y, double Z) center, (double X, double Y, double Z) axis)
            {
                Center = center;
                Axis = axis;
            }
        }

        public override Polyhedron CreateSolid()
        {
            var solid = new Polyhedron();
            var profile = Polygon2D.WithWinding(Polygon2D.Clean(Profile ?? new List<(double U, double V)>()), true);
            var path = CleanPath(Path ?? new List<(double X, double Y, double Z)>());
            if (profile.Count < 3 || path.Count < 2) return solid;
            var bends = new List<Bend>();
            // bendOf[k] is the bend whose arc segment k (path[k] → path[k+1]) is a chord of, or −1 on a straight run.
            var bendOf = Enumerable.Repeat(-1, path.Count - 1).ToList();
            if (BendRadius > 0) (path, bendOf) = FilletPath(path, BendRadius, BendSegments, bends);

            var tangents = new List<(double X, double Y, double Z)>();
            for (int i = 0; i + 1 < path.Count; i++) tangents.Add(Vec.Normalize(Vec.Sub(path[i + 1], path[i])));

            var t0 = tangents[0];
            var n = Vec.Sub(Up, Vec.Scale(t0, Vec.Dot(Up, t0)));
            n = Vec.Normalize(n);
            if (n == (0, 0, 0)) n = Vec.AnyPerpendicular(t0);
            var b = Vec.Cross(t0, n);

            var rings = new List<int[]>();
            // The frame of each ring laid square to the path (null for a mitred joint), and of each segment.
            var ringFrames = new List<((double X, double Y, double Z) N, (double X, double Y, double Z) B, (double X, double Y, double Z) T)?>();
            var segmentFrames = new List<((double X, double Y, double Z) N, (double X, double Y, double Z) B)>();
            int[] Ring((double X, double Y, double Z) at, (double X, double Y, double Z) rn, (double X, double Y, double Z) rb)
                => profile.Select(p => solid.AddVertex(Vec.Add(at, Vec.Add(Vec.Scale(rn, p.U), Vec.Scale(rb, p.V))))).ToArray();

            rings.Add(Ring(path[0], n, b));
            ringFrames.Add((n, b, t0));
            segmentFrames.Add((n, b));

            for (int j = 1; j < path.Count - 1; j++)
            {
                var ta = tangents[j - 1]; var tb = tangents[j];
                var joint = path[j];
                int bend = bendOf[j - 1] >= 0 ? bendOf[j - 1] : bendOf[j];
                if (bend >= 0)
                {
                    // On a bend the section stays square to the true arc, so each ring is a turned copy of the last.
                    var t = Vec.Normalize(Vec.Cross(bends[bend].Axis, Vec.Sub(joint, bends[bend].Center)));
                    var (nt, bt) = Turn(n, ta, t);
                    rings.Add(Ring(joint, nt, bt));
                    ringFrames.Add((nt, bt, t));
                }
                else
                {
                    var m = Vec.Normalize(Vec.Add(ta, tb));
                    double tm = Vec.Dot(ta, m);
                    if (m == (0, 0, 0) || tm < 0.05)
                        throw new ArgumentException($"{Name}: path turns back on itself at point {j}; add a bend radius or an intermediate point.");
                    var na = n; var ba = b;
                    rings.Add(profile.Select(p =>
                    {
                        var off = Vec.Add(Vec.Scale(na, p.U), Vec.Scale(ba, p.V));
                        double lambda = -Vec.Dot(off, m) / tm;
                        return solid.AddVertex(Vec.Add(joint, Vec.Add(off, Vec.Scale(ta, lambda))));
                    }).ToArray());
                    ringFrames.Add(null);
                }

                // Parallel transport: turn the frame by the same rotation that takes ta onto tb.
                (n, b) = Turn(n, ta, tb);
                segmentFrames.Add((n, b));
            }

            rings.Add(Ring(path[path.Count - 1], n, b));
            ringFrames.Add((n, b, tangents[tangents.Count - 1]));

            for (int k = 0; k + 1 < rings.Count; k++)
            {
                var a = rings[k]; var c = rings[k + 1];
                for (int i = 0; i < a.Length; i++)
                {
                    int i2 = (i + 1) % a.Length;
                    solid.AddFace(new[] { a[i], a[i2], c[i2], c[i] });
                }
            }
            solid.AddFace(rings[0]);
            solid.AddFace(rings[rings.Count - 1]);
            TagSurfaces(solid, profile, path, tangents, bendOf, bends, rings, ringFrames, segmentFrames);
            return solid;
        }

        private static ((double X, double Y, double Z) N, (double X, double Y, double Z) B) Turn(
            (double X, double Y, double Z) n, (double X, double Y, double Z) from, (double X, double Y, double Z) to)
        {
            var axis = Vec.Cross(from, to);
            double s = Vec.Length(axis);
            if (s > 1e-12) n = Vec.Normalize(Vec.Rotate(n, Vec.Scale(axis, 1 / s), Math.Atan2(s, Vec.Dot(from, to))));
            return (n, Vec.Cross(to, n));
        }

        // Straight runs carry the profile's arcs as cylinders; a bend turns the profile about the bend axis,
        // so its arcs become tori and its straight edges cylinders, cones or flat rings, as in a Revolve.
        private static void TagSurfaces(
            Polyhedron solid, List<(double U, double V)> profile, List<(double X, double Y, double Z)> path,
            List<(double X, double Y, double Z)> tangents, List<int> bendOf, List<Bend> bends, List<int[]> rings,
            List<((double X, double Y, double Z) N, (double X, double Y, double Z) B, (double X, double Y, double Z) T)?> ringFrames,
            List<((double X, double Y, double Z) N, (double X, double Y, double Z) B)> segmentFrames)
        {
            int m = profile.Count;
            double size = Math.Max(1, Vec.Length(Vec.Sub(solid.Max, solid.Min)));
            double tol = 1e-9 * size;
            var arcs = Arcs.Find(profile, closed: true);
            var group = Enumerable.Range(0, m).ToArray();
            var arcOf = new Arc2D?[m];
            foreach (var arc in arcs)
                for (int e = 0; e < arc.Segments; e++)
                {
                    group[(arc.Start + e) % m] = arc.Start;
                    arcOf[(arc.Start + e) % m] = arc;
                }
            var leaders = Enumerable.Range(0, m).Where(i => group[i] == i).ToList();
            (double X, double Y, double Z) Place((double X, double Y, double Z) at, (double X, double Y, double Z) n, (double X, double Y, double Z) b, (double U, double V) p)
                => Vec.Add(at, Vec.Add(Vec.Scale(n, p.U), Vec.Scale(b, p.V)));

            int segments = rings.Count - 1;
            for (int k = 0; k < segments;)
            {
                int bend = bendOf[k];
                if (bend < 0)
                {
                    var (n, b) = segmentFrames[k];
                    foreach (var arc in arcs)
                    {
                        var surface = new CylinderSurface(Place(path[k], n, b, arc.Center), tangents[k], arc.Radius);
                        for (int e = 0; e < arc.Segments; e++) solid.Faces[k * m + (arc.Start + e) % m].Surface = surface;
                    }
                    k++;
                    continue;
                }

                int k1 = k;
                while (k1 + 1 < segments && bendOf[k1 + 1] == bend) k1++;
                var bd = bends[bend];
                if (ringFrames[k] is var (rn, rb, _))
                {
                    (double R, double Z) Meridian((double X, double Y, double Z) p) => Arcs.Meridian(p, bd.Center, bd.Axis);
                    foreach (int leader in leaders)
                    {
                        var surface = arcOf[leader] is Arc2D arc
                            ? Arcs.RevolvedArc(Meridian(Place(path[k], rn, rb, arc.Center)), arc.Radius, bd.Center, bd.Axis, tol)
                            : Arcs.Revolved(Meridian(solid.Vertices[rings[k][leader]]), Meridian(solid.Vertices[rings[k][(leader + 1) % m]]), bd.Center, bd.Axis, tol);
                        if (surface == null) continue;
                        for (int i = 0; i < m; i++)
                            if (group[i] == leader)
                                for (int s = k; s <= k1; s++) solid.Faces[s * m + i].Surface = surface;
                    }
                    // Where one surface meets the next, the profile vertex traces a circle about the bend axis.
                    for (int i = 0; i < m; i++)
                    {
                        if (group[(i - 1 + m) % m] == group[i]) continue;
                        var (r, z) = Meridian(solid.Vertices[rings[k][i]]);
                        if (r <= tol) continue;
                        var chain = Enumerable.Range(k, k1 - k + 2).Select(s => rings[s][i]).ToArray();
                        solid.Curves.Add(new CurveRun(chain, new Circle3D(Vec.Add(bd.Center, Vec.Scale(bd.Axis, z)), bd.Axis, r)));
                    }
                }
                k = k1 + 1;
            }

            for (int k = 0; k < rings.Count; k++)
            {
                if (!(ringFrames[k] is var (rn, rb, rt))) continue;
                foreach (var arc in arcs)
                {
                    var chain = Enumerable.Range(0, arc.Segments + 1).Select(e => rings[k][(arc.Start + e) % m]).ToArray();
                    solid.Curves.Add(new CurveRun(chain, new Circle3D(Place(path[k], rn, rb, arc.Center), rt, arc.Radius)));
                }
            }
        }

        private static List<(double X, double Y, double Z)> CleanPath(List<(double X, double Y, double Z)> path)
        {
            var r = new List<(double X, double Y, double Z)>();
            foreach (var p in path)
                if (r.Count == 0 || Vec.Length(Vec.Sub(p, r[r.Count - 1])) > 1e-9) r.Add(p);
            // Collinear interior points add nothing but rings.
            for (int i = r.Count - 2; i >= 1; i--)
            {
                var d0 = Vec.Normalize(Vec.Sub(r[i], r[i - 1]));
                var d1 = Vec.Normalize(Vec.Sub(r[i + 1], r[i]));
                if (Vec.Dot(d0, d1) > 1 - 1e-12) r.RemoveAt(i);
            }
            return r;
        }

        internal static (List<(double X, double Y, double Z)> Points, List<int> BendOf) FilletPath(
            List<(double X, double Y, double Z)> path, double radius, int segments, List<Bend> bends)
        {
            var r = new List<(double X, double Y, double Z)> { path[0] };
            var bendOf = new List<int>();
            void Add((double X, double Y, double Z) p, int bend)
            {
                if (Vec.Length(Vec.Sub(p, r[r.Count - 1])) <= 1e-9) return;
                r.Add(p);
                bendOf.Add(bend);
            }

            for (int i = 1; i < path.Count - 1; i++)
            {
                var a = path[i - 1]; var bp = path[i]; var c = path[i + 1];
                var e1 = Vec.Normalize(Vec.Sub(a, bp));
                var e2 = Vec.Normalize(Vec.Sub(c, bp));
                double theta = Math.Acos(Math.Max(-1, Math.Min(1, Vec.Dot(e1, e2))));
                if (theta < 1e-6 || Math.PI - theta < 1e-6) { Add(bp, -1); continue; }

                double tangent = radius / Math.Tan(theta / 2);
                double limit = Math.Min(
                    Vec.Length(Vec.Sub(a, bp)) * (i == 1 ? 1.0 : 0.5),
                    Vec.Length(Vec.Sub(c, bp)) * (i == path.Count - 2 ? 1.0 : 0.5));
                double rr = radius;
                if (tangent > limit) { tangent = limit; rr = tangent * Math.Tan(theta / 2); }

                var t1 = Vec.Add(bp, Vec.Scale(e1, tangent));
                var bis = Vec.Normalize(Vec.Add(e1, e2));
                var center = Vec.Add(bp, Vec.Scale(bis, rr / Math.Sin(theta / 2)));
                var start = Vec.Sub(t1, center);
                var axis = Vec.Normalize(Vec.Cross(start, Vec.Sub(Vec.Add(bp, Vec.Scale(e2, tangent)), center)));
                double sweep = Math.PI - theta;
                int k = Math.Max(1, segments);
                bends.Add(new Bend(center, axis));
                for (int s = 0; s <= k; s++)
                    Add(Vec.Add(center, Vec.Rotate(start, axis, sweep * s / k)), s == 0 ? -1 : bends.Count - 1);
            }
            Add(path[path.Count - 1], -1);

            // A bend that used up a whole edge leaves two straight runs in line; they are one segment.
            for (int i = r.Count - 2; i >= 1; i--)
            {
                if (bendOf[i - 1] >= 0 || bendOf[i] >= 0) continue;
                var d0 = Vec.Normalize(Vec.Sub(r[i], r[i - 1]));
                var d1 = Vec.Normalize(Vec.Sub(r[i + 1], r[i]));
                if (Vec.Dot(d0, d1) <= 1 - 1e-12) continue;
                r.RemoveAt(i);
                bendOf.RemoveAt(i);
            }
            return (r, bendOf);
        }
    }
}
