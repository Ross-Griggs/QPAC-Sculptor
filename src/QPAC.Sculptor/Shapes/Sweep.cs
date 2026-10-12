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

        public override Polyhedron CreateSolid()
        {
            var solid = new Polyhedron();
            var profile = Polygon2D.WithWinding(Polygon2D.Clean(Profile ?? new List<(double U, double V)>()), true);
            var path = CleanPath(Path ?? new List<(double X, double Y, double Z)>());
            if (profile.Count < 3 || path.Count < 2) return solid;
            if (BendRadius > 0) path = FilletPath(path, BendRadius, BendSegments);

            var tangents = new List<(double X, double Y, double Z)>();
            for (int i = 0; i + 1 < path.Count; i++) tangents.Add(Vec.Normalize(Vec.Sub(path[i + 1], path[i])));

            var t0 = tangents[0];
            var n = Vec.Sub(Up, Vec.Scale(t0, Vec.Dot(Up, t0)));
            n = Vec.Normalize(n);
            if (n == (0, 0, 0)) n = Vec.AnyPerpendicular(t0);
            var b = Vec.Cross(t0, n);

            var rings = new List<int[]>();
            int[] Ring(Func<(double U, double V), (double X, double Y, double Z)> place)
                => profile.Select(p => solid.AddVertex(place(p))).ToArray();

            rings.Add(Ring(p => Vec.Add(path[0], Vec.Add(Vec.Scale(n, p.U), Vec.Scale(b, p.V)))));

            for (int j = 1; j < path.Count - 1; j++)
            {
                var ta = tangents[j - 1]; var tb = tangents[j];
                var m = Vec.Normalize(Vec.Add(ta, tb));
                double tm = Vec.Dot(ta, m);
                if (m == (0, 0, 0) || tm < 0.05)
                    throw new ArgumentException($"{Name}: path turns back on itself at point {j}; add a bend radius or an intermediate point.");
                var joint = path[j];
                var na = n; var ba = b;
                rings.Add(Ring(p =>
                {
                    var off = Vec.Add(Vec.Scale(na, p.U), Vec.Scale(ba, p.V));
                    double lambda = -Vec.Dot(off, m) / tm;
                    return Vec.Add(joint, Vec.Add(off, Vec.Scale(ta, lambda)));
                }));

                // Parallel transport: turn the frame by the same rotation that takes ta onto tb.
                var axis = Vec.Cross(ta, tb);
                double s = Vec.Length(axis);
                if (s > 1e-12)
                {
                    double ang = Math.Atan2(s, Vec.Dot(ta, tb));
                    axis = Vec.Scale(axis, 1 / s);
                    n = Vec.Normalize(Vec.Rotate(n, axis, ang));
                    b = Vec.Cross(tb, n);
                }
            }

            var end = path[path.Count - 1];
            rings.Add(Ring(p => Vec.Add(end, Vec.Add(Vec.Scale(n, p.U), Vec.Scale(b, p.V)))));

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
            return solid;
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

        internal static List<(double X, double Y, double Z)> FilletPath(List<(double X, double Y, double Z)> path, double radius, int segments)
        {
            var r = new List<(double X, double Y, double Z)> { path[0] };
            for (int i = 1; i < path.Count - 1; i++)
            {
                var a = path[i - 1]; var bp = path[i]; var c = path[i + 1];
                var e1 = Vec.Normalize(Vec.Sub(a, bp));
                var e2 = Vec.Normalize(Vec.Sub(c, bp));
                double theta = Math.Acos(Math.Max(-1, Math.Min(1, Vec.Dot(e1, e2))));
                if (theta < 1e-6 || Math.PI - theta < 1e-6) { r.Add(bp); continue; }

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
                for (int s = 0; s <= k; s++)
                    r.Add(Vec.Add(center, Vec.Rotate(start, axis, sweep * s / k)));
            }
            r.Add(path[path.Count - 1]);
            return CleanPath(r);
        }
    }
}
