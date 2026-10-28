using System;
using System.Collections.Generic;
using System.Linq;

namespace QPAC.Sculptor
{
    internal sealed class BEdge
    {
        public int Start, End;

        /// <summary>Null for a straight edge.</summary>
        public Circle3D Circle;

        /// <summary>True when Start → End runs counter-clockwise about <see cref="Circle3D.Normal"/>.</summary>
        public bool CircleSense;
    }

    internal sealed class BFace
    {
        public readonly List<List<(BEdge Edge, bool Forward)>> Loops = new List<List<(BEdge Edge, bool Forward)>>();

        /// <summary>Null for a flat face, whose plane is <see cref="PlanePoint"/> / <see cref="PlaneNormal"/>.</summary>
        public AnalyticSurface Surface;
        public (double X, double Y, double Z) PlanePoint, PlaneNormal;

        /// <summary>True when the surface's own normal points out of the solid.</summary>
        public bool SameSense = true;

        /// <summary>The first loop encloses the rest (always true of a flat face).</summary>
        public bool HasOuterLoop = true;
    }

    /// <summary>
    /// Turns a polyhedron into boundary-representation faces and edges. Facets tagged with the same
    /// exact surface become one curved face and vertex chains on a circle become one arc edge. Any tag
    /// that does not fit its neighbours — a curved face bounded by a chord, an arc that is not on the
    /// faces beside it — falls back to flat facets and straight edges, so the result is always a valid solid.
    /// </summary>
    internal static class BRep
    {
        private sealed class Patch
        {
            public AnalyticSurface Surface;
            public List<int> Faces = new List<int>();
            public List<int[]> Boundary;
            public bool Alive;
        }

        public static List<BFace> Build(Polyhedron poly, bool exact)
        {
            var faces = poly.Faces;
            double size = Math.Max(1, Vec.Length(Vec.Sub(poly.Max, poly.Min)));
            double tol = 1e-9 * size;

            var edgeFace = new Dictionary<(int, int), int>();
            for (int fi = 0; fi < faces.Count; fi++)
                foreach (var loop in faces[fi].Loops)
                    for (int k = 0; k < loop.Length; k++)
                        edgeFace[(loop[k], loop[(k + 1) % loop.Length])] = fi;

            // Patches: connected facets sharing one surface; every untagged facet is its own flat patch.
            var patchOf = Enumerable.Repeat(-1, faces.Count).ToArray();
            var patches = new List<Patch>();
            for (int seed = 0; seed < faces.Count; seed++)
            {
                if (patchOf[seed] >= 0) continue;
                var surface = exact ? faces[seed].Surface : null;
                var patch = new Patch { Surface = surface, Alive = surface != null };
                patchOf[seed] = patches.Count;
                patch.Faces.Add(seed);
                for (int q = 0; surface != null && q < patch.Faces.Count; q++)
                    foreach (var loop in faces[patch.Faces[q]].Loops)
                        for (int k = 0; k < loop.Length; k++)
                        {
                            if (!edgeFace.TryGetValue((loop[(k + 1) % loop.Length], loop[k]), out int nb)) continue;
                            if (patchOf[nb] >= 0 || faces[nb].Surface != surface) continue;
                            patchOf[nb] = patches.Count;
                            patch.Faces.Add(nb);
                        }
                if (patch.Alive)
                {
                    patch.Boundary = BoundaryLoops(faces, patch.Faces, patchOf, patches.Count, edgeFace);
                    patch.Alive = patch.Boundary != null;
                }
                patches.Add(patch);
            }

            // Arc runs, each facet edge claimed by at most one. A run that closes on itself is split in two.
            var runs = new List<CurveRun>();
            if (exact)
                foreach (var run in poly.Curves)
                {
                    var c = run.Chain;
                    if (c.Length > 2 && c[0] == c[c.Length - 1])
                    {
                        int mid = c.Length / 2;
                        runs.Add(new CurveRun(c.Take(mid + 1).ToArray(), run.Curve));
                        runs.Add(new CurveRun(c.Skip(mid).ToArray(), run.Curve));
                    }
                    else runs.Add(run);
                }
            var runOf = new Dictionary<(int, int), int>();
            var runAlive = new bool[runs.Count];
            for (int r = 0; r < runs.Count; r++)
            {
                var c = runs[r].Chain;
                runAlive[r] = c.Length >= 2 && c.Distinct().Count() == c.Length;
                for (int k = 0; runAlive[r] && k + 1 < c.Length; k++)
                {
                    var key = Key(c[k], c[k + 1]);
                    if (runOf.ContainsKey(key) || !edgeFace.ContainsKey((c[k], c[k + 1])) || !edgeFace.ContainsKey((c[k + 1], c[k])))
                        runAlive[r] = false;
                }
                if (runAlive[r])
                    for (int k = 0; k + 1 < c.Length; k++) runOf[Key(c[k], c[k + 1])] = r;
            }
            int AliveRun(int a, int b) => runOf.TryGetValue(Key(a, b), out int r) && runAlive[r] ? r : -1;

            var onCache = new Dictionary<(int, AnalyticSurface), bool>();
            bool RunOn(int r, AnalyticSurface s)
            {
                if (!onCache.TryGetValue((r, s), out bool ok)) onCache[(r, s)] = ok = runs[r].Curve.LiesOn(s, tol);
                return ok;
            }
            bool SegmentOn(int a, int b, AnalyticSurface s)
            {
                for (int k = 0; k <= 4; k++)
                    if (s.Distance(Vec.Lerp(poly.Vertices[a], poly.Vertices[b], k / 4.0)) > tol) return false;
                return true;
            }
            var planeOf = new Dictionary<int, PlaneSurface>();
            PlaneSurface Plane(int fi)
            {
                if (!planeOf.TryGetValue(fi, out var p))
                    planeOf[fi] = p = new PlaneSurface(poly.Vertices[faces[fi].Outer[0]], poly.FaceNormal(faces[fi]));
                return p;
            }

            // Demote until consistent: a curved face needs every boundary edge on its surface, and an arc
            // needs to lie on the faces either side of it.
            for (bool changed = true; changed;)
            {
                changed = false;
                foreach (var patch in patches)
                {
                    if (!patch.Alive) continue;
                    foreach (var loop in patch.Boundary)
                    {
                        for (int k = 0; k < loop.Length && patch.Alive; k++)
                        {
                            int a = loop[k], b = loop[(k + 1) % loop.Length];
                            int r = AliveRun(a, b);
                            if (r >= 0 ? !RunOn(r, patch.Surface) : !SegmentOn(a, b, patch.Surface)) patch.Alive = false;
                        }
                        if (!patch.Alive) { changed = true; break; }
                    }
                }
                for (int r = 0; r < runs.Count; r++)
                {
                    if (!runAlive[r]) continue;
                    var c = runs[r].Chain;
                    for (int k = 0; k + 1 < c.Length && runAlive[r]; k++)
                    {
                        int f1 = edgeFace[(c[k], c[k + 1])], f2 = edgeFace[(c[k + 1], c[k])];
                        if (patchOf[f1] == patchOf[f2] && patches[patchOf[f1]].Alive) continue;
                        foreach (int f in new[] { f1, f2 })
                        {
                            var p = patches[patchOf[f]];
                            if (!(p.Alive ? RunOn(r, p.Surface) : RunOn(r, Plane(f)))) runAlive[r] = false;
                        }
                    }
                    if (!runAlive[r]) changed = true;
                }
            }

            // Output faces.
            var result = new List<BFace>();
            var outOf = new int[faces.Count];
            var loopsOf = new List<List<int[]>>();
            foreach (var patch in patches)
            {
                if (patch.Alive)
                {
                    foreach (int fi in patch.Faces) outOf[fi] = result.Count;
                    var f0 = faces[patch.Faces[0]];
                    var centroid = Centroid(poly, f0.Outer);
                    result.Add(new BFace
                    {
                        Surface = patch.Surface,
                        SameSense = Vec.Dot(patch.Surface.Normal(centroid), poly.FaceNormal(f0)) > 0,
                        HasOuterLoop = patch.Boundary.Count == 1,
                    });
                    loopsOf.Add(patch.Boundary);
                }
                else
                    foreach (int fi in patch.Faces)
                    {
                        outOf[fi] = result.Count;
                        var plane = Plane(fi);
                        result.Add(new BFace { PlanePoint = plane.Point, PlaneNormal = plane.Normal0 });
                        loopsOf.Add(faces[fi].Loops.ToList());
                    }
            }

            // Edges: consecutive facet edges on one arc run with the same face across them become one arc.
            var edges = new Dictionary<(int Run, int Lo, int Hi), BEdge>();
            for (int o = 0; o < result.Count; o++)
                foreach (var loop in loopsOf[o])
                {
                    int m = loop.Length;
                    var label = new (int Run, int Other)[m];
                    for (int k = 0; k < m; k++)
                    {
                        int a = loop[k], b = loop[(k + 1) % m];
                        label[k] = (AliveRun(a, b), outOf[edgeFace[(b, a)]]);
                    }
                    bool Joins(int k) => label[k].Run >= 0 && label[k] == label[(k - 1 + m) % m];
                    int start = Enumerable.Range(0, m).FirstOrDefault(k => !Joins(k));

                    var coedges = new List<(BEdge Edge, bool Forward)>();
                    for (int s = 0; s < m;)
                    {
                        int k0 = (start + s) % m;
                        int len = 1;
                        while (s + len < m && Joins((start + s + len) % m)) len++;
                        int va = loop[k0], vb = loop[(k0 + len) % m];
                        int run = label[k0].Run;
                        var key = (run, Math.Min(va, vb), Math.Max(va, vb));
                        if (!edges.TryGetValue(key, out var edge))
                        {
                            edge = new BEdge { Start = va, End = vb };
                            if (run >= 0)
                            {
                                edge.Circle = runs[run].Curve;
                                var p0 = poly.Vertices[va];
                                var p1 = poly.Vertices[loop[(k0 + 1) % m]];
                                edge.CircleSense = Vec.Dot(edge.Circle.Tangent(p0), Vec.Sub(p1, p0)) > 0;
                            }
                            edges[key] = edge;
                        }
                        coedges.Add((edge, edge.Start == va));
                        s += len;
                    }
                    result[o].Loops.Add(coedges);
                }
            return result;
        }

        private static (int, int) Key(int a, int b) => a < b ? (a, b) : (b, a);

        private static (double X, double Y, double Z) Centroid(Polyhedron poly, int[] loop)
        {
            (double X, double Y, double Z) c = (0, 0, 0);
            foreach (int v in loop) c = Vec.Add(c, poly.Vertices[v]);
            return Vec.Scale(c, 1.0 / loop.Length);
        }

        // The patch's outline as closed vertex loops, or null when the outline touches itself.
        private static List<int[]> BoundaryLoops(List<PolyFace> faces, List<int> members, int[] patchOf, int patch, Dictionary<(int, int), int> edgeFace)
        {
            var next = new Dictionary<int, int>();
            foreach (int fi in members)
                foreach (var loop in faces[fi].Loops)
                    for (int k = 0; k < loop.Length; k++)
                    {
                        int a = loop[k], b = loop[(k + 1) % loop.Length];
                        if (edgeFace.TryGetValue((b, a), out int nb) && patchOf[nb] == patch) continue;
                        if (next.ContainsKey(a)) return null;
                        next[a] = b;
                    }
            var loops = new List<int[]>();
            var used = new HashSet<int>();
            foreach (int s in next.Keys)
            {
                if (used.Contains(s)) continue;
                var l = new List<int>();
                int v = s;
                while (used.Add(v))
                {
                    l.Add(v);
                    if (!next.TryGetValue(v, out v)) return null;
                }
                if (v != s || l.Count < 2) return null;
                loops.Add(l.ToArray());
            }
            return loops.Count > 0 ? loops : null;
        }
    }
}
