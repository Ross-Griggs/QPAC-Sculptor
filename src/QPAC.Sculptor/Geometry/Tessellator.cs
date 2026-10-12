using System;
using System.Collections.Generic;
using System.Linq;

namespace QPAC.Sculptor
{
    /// <summary>Triangulates polyhedron faces (concave outlines and holes included) for rendering.</summary>
    public static class Tessellator
    {
        /// <summary>
        /// Splits a face into triangles wound the same way as the face (counter-clockwise seen from
        /// outside). Returned indices refer to <see cref="Polyhedron.Vertices"/>.
        /// </summary>
        public static List<(int A, int B, int C)> Triangulate(Polyhedron solid, PolyFace face)
        {
            var n = solid.FaceNormal(face);
            var u = Vec.AnyPerpendicular(n);
            var v = Vec.Cross(n, u);
            (double U, double V) P(int i) { var p = solid.Vertices[i]; return (Vec.Dot(p, u), Vec.Dot(p, v)); }

            var ring = BridgeHoles(face, P);
            return EarClip(ring, P);
        }

        public static IEnumerable<(int A, int B, int C)> Triangulate(Polyhedron solid)
            => solid.Faces.SelectMany(f => Triangulate(solid, f));

        // Splices each hole into the outer loop through a zero-width bridge so ear clipping sees one simple polygon.
        private static List<int> BridgeHoles(PolyFace face, Func<int, (double U, double V)> P)
        {
            var ring = face.Outer.ToList();
            if (face.Holes.Count == 0) return ring;

            var holes = face.Holes
                .Select(h => h.ToList())
                .OrderByDescending(h => h.Max(i => P(i).U))
                .ToList();

            for (int hi = 0; hi < holes.Count; hi++)
            {
                var hole = holes[hi];
                int m = 0;
                for (int k = 1; k < hole.Count; k++) if (P(hole[k]).U > P(hole[m]).U) m = k;
                var pm = P(hole[m]);

                var others = holes.Skip(hi + 1).ToList();
                var candidates = Enumerable.Range(0, ring.Count)
                    .OrderBy(k => Dist2(P(ring[k]), pm))
                    .ToList();

                int best = -1;
                foreach (int k in candidates)
                {
                    var pk = P(ring[k]);
                    if (LocallyInside(ring, k, pm, P) && BridgeClear(pm, pk, ring, hole, others, P)) { best = k; break; }
                }
                if (best < 0) best = candidates[0];

                var spliced = new List<int>(ring.Count + hole.Count + 2);
                for (int k = 0; k <= best; k++) spliced.Add(ring[k]);
                for (int k = 0; k <= hole.Count; k++) spliced.Add(hole[(m + k) % hole.Count]);
                for (int k = best; k < ring.Count; k++) spliced.Add(ring[k]);
                ring = spliced;
            }
            return ring;
        }

        // Once holes are spliced in, one point can appear several times in the ring. The bridge must
        // attach at the occurrence whose interior wedge (ring is counter-clockwise) contains the
        // direction toward the hole, or the spliced polygon crosses itself.
        private static bool LocallyInside(List<int> ring, int k, (double U, double V) target, Func<int, (double U, double V)> P)
        {
            int n = ring.Count;
            var p = P(ring[k]);
            var a = P(ring[(k - 1 + n) % n]);
            var b = P(ring[(k + 1) % n]);
            double Cross((double U, double V) o, (double U, double V) x, (double U, double V) y)
                => (x.U - o.U) * (y.V - o.V) - (x.V - o.V) * (y.U - o.U);
            if (Cross(a, p, b) >= 0)
                return Cross(p, b, target) >= 0 && Cross(p, target, a) >= 0;
            return !(Cross(p, a, target) > 0 && Cross(p, target, b) > 0);
        }

        private static bool BridgeClear((double U, double V) a, (double U, double V) b, List<int> ring, List<int> hole, List<List<int>> others, Func<int, (double U, double V)> P)
        {
            double du = b.U - a.U, dv = b.V - a.V, len2 = du * du + dv * dv;
            double tol = 1e-9 * Math.Max(1.0, Math.Sqrt(len2));

            // Holes laid out on a grid share coordinates, so a bridge can run exactly through another
            // loop's corner without properly crossing an edge; that splices an invalid polygon.
            bool Grazes((double U, double V) q)
            {
                if (Dist2(q, a) <= tol * tol || Dist2(q, b) <= tol * tol) return false;
                double t = ((q.U - a.U) * du + (q.V - a.V) * dv) / len2;
                if (t <= 0 || t >= 1) return false;
                double ex = a.U + t * du - q.U, ey = a.V + t * dv - q.V;
                return ex * ex + ey * ey <= tol * tol;
            }

            bool Hits(IList<int> loop)
            {
                for (int i = 0; i < loop.Count; i++)
                {
                    var c = P(loop[i]); var d = P(loop[(i + 1) % loop.Count]);
                    if (Polygon2D.SegmentsCross(a, b, c, d) || Grazes(c)) return true;
                }
                return false;
            }
            if (Hits(ring) || Hits(hole)) return false;
            foreach (var o in others) if (Hits(o)) return false;
            return true;
        }

        private static double Dist2((double U, double V) a, (double U, double V) b)
        {
            double du = a.U - b.U, dv = a.V - b.V;
            return du * du + dv * dv;
        }

        private static List<(int A, int B, int C)> EarClip(List<int> ring, Func<int, (double U, double V)> P)
        {
            var tris = new List<(int A, int B, int C)>();
            var idx = new List<int>(ring);
            var pts = idx.Select(P).ToList();

            double area = 0;
            for (int i = 0; i < pts.Count; i++)
                area += pts[i].U * pts[(i + 1) % pts.Count].V - pts[(i + 1) % pts.Count].U * pts[i].V;
            if (area < 0) { idx.Reverse(); pts.Reverse(); }

            double Cross((double U, double V) o, (double U, double V) a, (double U, double V) b)
                => (a.U - o.U) * (b.V - o.V) - (a.V - o.V) * (b.U - o.U);

            int guard = 0;
            bool relaxed = false;
            while (idx.Count > 3 && guard++ < 100000)
            {
                bool clipped = false;
                int count = idx.Count;
                // First pass rejects ears with any vertex on their boundary. Grid-aligned holes put
                // vertices exactly on many diagonals, so if nothing clips, only block on strictly-inside points.
                for (int i = 0; i < count; i++)
                {
                    int ip = (i - 1 + count) % count, inx = (i + 1) % count;
                    var a = pts[ip]; var b = pts[i]; var c = pts[inx];
                    double cr = Cross(a, b, c);
                    if (cr <= 0)
                    {
                        // Collinear spur or bridge seam: drop the middle vertex without a triangle.
                        if (Math.Abs(cr) < 1e-14 && (a.U - b.U) * (c.U - b.U) + (a.V - b.V) * (c.V - b.V) <= 0 && idx[ip] != idx[inx])
                        {
                            idx.RemoveAt(i); pts.RemoveAt(i);
                            clipped = true;
                            break;
                        }
                        continue;
                    }

                    bool contains = false;
                    for (int k = 0; k < count && !contains; k++)
                    {
                        if (k == ip || k == i || k == inx) continue;
                        if (idx[k] == idx[ip] || idx[k] == idx[i] || idx[k] == idx[inx]) continue;
                        var p = pts[k];
                        double thr = relaxed ? 1e-9 * cr : 0;
                        bool In(double x) => relaxed ? x > thr : x >= 0;
                        if (In(Cross(a, b, p)) && In(Cross(b, c, p)) && In(Cross(c, a, p))) contains = true;
                    }
                    if (contains) continue;

                    tris.Add((idx[ip], idx[i], idx[inx]));
                    idx.RemoveAt(i); pts.RemoveAt(i);
                    clipped = true;
                    relaxed = false;
                    break;
                }
                if (!clipped && !relaxed)
                {
                    relaxed = true;
                    continue;
                }
                if (!clipped)
                {
                    // Numerically stuck (near-degenerate input): fan the rest rather than lose the face.
                    for (int i = 1; i + 1 < idx.Count; i++) tris.Add((idx[0], idx[i], idx[i + 1]));
                    idx.Clear();
                }
            }
            if (idx.Count == 3 && Cross(pts[0], pts[1], pts[2]) > 0) tris.Add((idx[0], idx[1], idx[2]));
            return tris;
        }
    }
}
