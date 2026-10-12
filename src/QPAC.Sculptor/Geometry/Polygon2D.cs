using System;
using System.Collections.Generic;
using System.Linq;

namespace QPAC.Sculptor
{
    internal static class Polygon2D
    {
        public static double SignedArea(IReadOnlyList<(double U, double V)> loop)
        {
            double a = 0;
            for (int i = 0; i < loop.Count; i++)
            {
                var p = loop[i];
                var q = loop[(i + 1) % loop.Count];
                a += p.U * q.V - q.U * p.V;
            }
            return 0.5 * a;
        }

        public static List<(double U, double V)> WithWinding(IEnumerable<(double U, double V)> loop, bool counterClockwise)
        {
            var l = loop.ToList();
            if ((SignedArea(l) > 0) != counterClockwise) l.Reverse();
            return l;
        }

        // Drops consecutive duplicates (and a closing duplicate) that would create zero-length edges.
        public static List<(double U, double V)> Clean(IEnumerable<(double U, double V)> loop, double tol = 1e-12)
        {
            var r = new List<(double U, double V)>();
            foreach (var p in loop)
                if (r.Count == 0 || Dist(r[r.Count - 1], p) > tol) r.Add(p);
            while (r.Count > 1 && Dist(r[0], r[r.Count - 1]) <= tol) r.RemoveAt(r.Count - 1);
            return r;
        }

        public static double Dist((double U, double V) a, (double U, double V) b)
        {
            double du = a.U - b.U, dv = a.V - b.V;
            return Math.Sqrt(du * du + dv * dv);
        }

        /// <summary>
        /// Moves every edge of the loop a distance <paramref name="d"/> to its left (miter join).
        /// For a counter-clockwise outline that shrinks it; for a clockwise hole it enlarges it — in
        /// both cases it removes material. Throws if an edge would turn inside out.
        /// </summary>
        public static List<(double U, double V)> OffsetLeft(IReadOnlyList<(double U, double V)> loop, double d, string context)
        {
            int n = loop.Count;
            if (Math.Abs(d) < 1e-15) return loop.ToList();
            var result = new List<(double U, double V)>(n);
            for (int i = 0; i < n; i++)
            {
                var prev = loop[(i - 1 + n) % n];
                var cur = loop[i];
                var next = loop[(i + 1) % n];
                var e0 = Unit((cur.U - prev.U, cur.V - prev.V));
                var e1 = Unit((next.U - cur.U, next.V - cur.V));
                var n0 = (-e0.V, e0.U);
                var n1 = (-e1.V, e1.U);
                var bis = (n0.Item1 + n1.Item1, n0.Item2 + n1.Item2);
                double bl2 = bis.Item1 * bis.Item1 + bis.Item2 * bis.Item2;
                if (bl2 < 1e-12)
                    throw new ArgumentException($"{context}: profile doubles back on itself at vertex {i}; cannot offset it.");
                // Scale so the offset along each adjacent edge normal is exactly d.
                double s = 2.0 * d / bl2;
                result.Add((cur.U + bis.Item1 * s, cur.V + bis.Item2 * s));
            }

            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                double ou = loop[j].U - loop[i].U, ov = loop[j].V - loop[i].V;
                double nu = result[j].U - result[i].U, nv = result[j].V - result[i].V;
                double olen = Math.Sqrt(ou * ou + ov * ov);
                if (ou * nu + ov * nv < -1e-9 * Math.Max(1.0, olen * olen))
                    throw new ArgumentException($"{context}: an offset of {Math.Abs(d):G4} is larger than the profile allows (edge {i} would turn inside out). Use a smaller radius or round the profile corners first.");
            }
            return result;
        }

        public static (double U, double V) Unit((double U, double V) a)
        {
            double l = Math.Sqrt(a.U * a.U + a.V * a.V);
            return l < 1e-15 ? (0, 0) : (a.U / l, a.V / l);
        }

        public static bool Contains(IReadOnlyList<(double U, double V)> loop, (double U, double V) p)
        {
            bool inside = false;
            for (int i = 0, j = loop.Count - 1; i < loop.Count; j = i++)
            {
                var a = loop[i]; var b = loop[j];
                if ((a.V > p.V) != (b.V > p.V) &&
                    p.U < (b.U - a.U) * (p.V - a.V) / (b.V - a.V) + a.U)
                    inside = !inside;
            }
            return inside;
        }

        public static bool SegmentsCross((double U, double V) a, (double U, double V) b, (double U, double V) c, (double U, double V) d)
        {
            double Cross((double U, double V) o, (double U, double V) p, (double U, double V) q)
                => (p.U - o.U) * (q.V - o.V) - (p.V - o.V) * (q.U - o.U);
            double d1 = Cross(c, d, a), d2 = Cross(c, d, b), d3 = Cross(a, b, c), d4 = Cross(a, b, d);
            return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
        }

        public static bool LoopsCross(IReadOnlyList<(double U, double V)> a, IReadOnlyList<(double U, double V)> b)
        {
            for (int i = 0; i < a.Count; i++)
                for (int j = 0; j < b.Count; j++)
                    if (SegmentsCross(a[i], a[(i + 1) % a.Count], b[j], b[(j + 1) % b.Count])) return true;
            return false;
        }

        public static bool SelfIntersects(IReadOnlyList<(double U, double V)> a)
        {
            int n = a.Count;
            for (int i = 0; i < n; i++)
                for (int j = i + 2; j < n; j++)
                {
                    if (i == 0 && j == n - 1) continue;
                    if (SegmentsCross(a[i], a[(i + 1) % n], a[j], a[(j + 1) % n])) return true;
                }
            return false;
        }

        /// <summary>True when <paramref name="inner"/> lies entirely inside <paramref name="outer"/>.</summary>
        public static bool Inside(IReadOnlyList<(double U, double V)> inner, IReadOnlyList<(double U, double V)> outer)
            => !LoopsCross(inner, outer) && inner.All(p => Contains(outer, p));

        /// <summary>True when the two loops neither cross nor nest.</summary>
        public static bool Disjoint(IReadOnlyList<(double U, double V)> a, IReadOnlyList<(double U, double V)> b)
            => !LoopsCross(a, b) && !Contains(b, a[0]) && !Contains(a, b[0]);

        public static bool IsConvex(IReadOnlyList<(double U, double V)> loop)
        {
            int n = loop.Count, sign = 0;
            for (int i = 0; i < n; i++)
            {
                var a = loop[i]; var b = loop[(i + 1) % n]; var c = loop[(i + 2) % n];
                double cr = (b.U - a.U) * (c.V - b.V) - (b.V - a.V) * (c.U - b.U);
                if (Math.Abs(cr) < 1e-12) continue;
                int s = cr > 0 ? 1 : -1;
                if (sign == 0) sign = s;
                else if (s != sign) return false;
            }
            return true;
        }

        // Sutherland–Hodgman: clip any polygon by a convex counter-clockwise polygon.
        public static List<(double U, double V)> ClipConvex(IReadOnlyList<(double U, double V)> subject, IReadOnlyList<(double U, double V)> convexCcw)
        {
            var output = subject.ToList();
            for (int i = 0; i < convexCcw.Count && output.Count > 0; i++)
            {
                var a = convexCcw[i]; var b = convexCcw[(i + 1) % convexCcw.Count];
                double Side((double U, double V) p) => (b.U - a.U) * (p.V - a.V) - (b.V - a.V) * (p.U - a.U);
                var input = output;
                output = new List<(double U, double V)>();
                for (int k = 0; k < input.Count; k++)
                {
                    var cur = input[k]; var prev = input[(k - 1 + input.Count) % input.Count];
                    double sc = Side(cur), sp = Side(prev);
                    if (sc >= 0)
                    {
                        if (sp < 0) output.Add(Intersect(prev, cur, sp, sc));
                        output.Add(cur);
                    }
                    else if (sp >= 0) output.Add(Intersect(prev, cur, sp, sc));
                }
            }
            return Clean(output, 1e-9);
        }

        private static (double U, double V) Intersect((double U, double V) p, (double U, double V) q, double sp, double sq)
        {
            double t = sp / (sp - sq);
            return (p.U + (q.U - p.U) * t, p.V + (q.V - p.V) * t);
        }

        /// <summary>
        /// Adds vertices along the longest edges until the loop has <paramref name="count"/> vertices,
        /// keeping every original corner.
        /// </summary>
        public static List<(double U, double V)> Densify(IReadOnlyList<(double U, double V)> loop, int count)
        {
            int n = loop.Count;
            if (count <= n) return loop.ToList();
            var splits = new int[n];
            var lens = new double[n];
            for (int i = 0; i < n; i++) lens[i] = Dist(loop[i], loop[(i + 1) % n]);
            for (int extra = count - n; extra > 0; extra--)
            {
                int best = 0;
                double bestLen = -1;
                for (int i = 0; i < n; i++)
                {
                    double seg = lens[i] / (splits[i] + 1);
                    if (seg > bestLen) { bestLen = seg; best = i; }
                }
                splits[best]++;
            }
            var r = new List<(double U, double V)>(count);
            for (int i = 0; i < n; i++)
            {
                var a = loop[i]; var b = loop[(i + 1) % n];
                int parts = splits[i] + 1;
                for (int k = 0; k < parts; k++)
                {
                    double t = (double)k / parts;
                    r.Add((a.U + (b.U - a.U) * t, a.V + (b.V - a.V) * t));
                }
            }
            return r;
        }
    }
}
