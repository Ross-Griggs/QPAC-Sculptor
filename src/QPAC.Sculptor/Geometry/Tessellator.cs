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
                    if (BridgeClear(pm, pk, ring, hole, others, P)) { best = k; break; }
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

        private static bool BridgeClear((double U, double V) a, (double U, double V) b, List<int> ring, List<int> hole, List<List<int>> others, Func<int, (double U, double V)> P)
        {
            bool Hits(IList<int> loop)
            {
                for (int i = 0; i < loop.Count; i++)
                {
                    var c = P(loop[i]); var d = P(loop[(i + 1) % loop.Count]);
                    if (Polygon2D.SegmentsCross(a, b, c, d)) return true;
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
            while (idx.Count > 3 && guard++ < 100000)
            {
                bool clipped = false;
                int count = idx.Count;
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
                        if (Cross(a, b, p) >= 0 && Cross(b, c, p) >= 0 && Cross(c, a, p) >= 0) contains = true;
                    }
                    if (contains) continue;

                    tris.Add((idx[ip], idx[i], idx[inx]));
                    idx.RemoveAt(i); pts.RemoveAt(i);
                    clipped = true;
                    break;
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
