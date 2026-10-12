using System;
using System.Collections.Generic;
using System.Linq;

namespace QPAC.Sculptor
{
    /// <summary>
    /// One planar face of a <see cref="Polyhedron"/>: an outer vertex loop plus optional hole loops.
    /// After <see cref="Polyhedron.Orient"/> the outer loop winds counter-clockwise seen from outside
    /// the solid and every hole winds the opposite way.
    /// </summary>
    public sealed class PolyFace
    {
        public int[] Outer;
        public List<int[]> Holes = new List<int[]>();

        public PolyFace(int[] outer, IEnumerable<int[]> holes = null)
        {
            Outer = outer;
            if (holes != null) Holes.AddRange(holes);
        }

        public IEnumerable<int[]> Loops
        {
            get
            {
                yield return Outer;
                foreach (var h in Holes) yield return h;
            }
        }

        internal void Reverse()
        {
            Array.Reverse(Outer);
            foreach (var h in Holes) Array.Reverse(h);
        }
    }

    /// <summary>
    /// A closed solid bounded by planar polygon faces. Every shape tessellates to one of these, and
    /// both the STEP writer and the WPF renderer consume it, so the preview always matches the export.
    /// </summary>
    public sealed class Polyhedron
    {
        public List<(double X, double Y, double Z)> Vertices = new List<(double X, double Y, double Z)>();
        public List<PolyFace> Faces = new List<PolyFace>();

        public int AddVertex((double X, double Y, double Z) p)
        {
            Vertices.Add(p);
            return Vertices.Count - 1;
        }

        public PolyFace AddFace(int[] outer, params int[][] holes)
        {
            var f = new PolyFace(outer, holes);
            Faces.Add(f);
            return f;
        }

        public Polyhedron Clone()
        {
            var p = new Polyhedron { Vertices = new List<(double X, double Y, double Z)>(Vertices) };
            foreach (var f in Faces)
                p.Faces.Add(new PolyFace((int[])f.Outer.Clone(), f.Holes.Select(h => (int[])h.Clone())));
            return p;
        }

        public Polyhedron Transformed(Transform xf)
        {
            var p = Clone();
            if (xf.IsIdentity) return p;
            for (int i = 0; i < p.Vertices.Count; i++) p.Vertices[i] = xf.Apply(p.Vertices[i]);
            if (xf.Determinant < 0) foreach (var f in p.Faces) f.Reverse();
            return p;
        }

        public (double X, double Y, double Z) Min => Vertices.Count == 0 ? (0, 0, 0) : (Vertices.Min(v => v.X), Vertices.Min(v => v.Y), Vertices.Min(v => v.Z));
        public (double X, double Y, double Z) Max => Vertices.Count == 0 ? (0, 0, 0) : (Vertices.Max(v => v.X), Vertices.Max(v => v.Y), Vertices.Max(v => v.Z));

        /// <summary>Area-weighted outward normal of a loop (Newell's method); its length is twice the loop area.</summary>
        public (double X, double Y, double Z) LoopNormal(int[] loop)
        {
            double nx = 0, ny = 0, nz = 0;
            for (int k = 0; k < loop.Length; k++)
            {
                var p = Vertices[loop[k]];
                var q = Vertices[loop[(k + 1) % loop.Length]];
                nx += (p.Y - q.Y) * (p.Z + q.Z);
                ny += (p.Z - q.Z) * (p.X + q.X);
                nz += (p.X - q.X) * (p.Y + q.Y);
            }
            return (nx, ny, nz);
        }

        public (double X, double Y, double Z) FaceNormal(PolyFace f) => Vec.Normalize(LoopNormal(f.Outer));

        /// <summary>Signed volume; positive once faces are oriented outward.</summary>
        public double Volume
        {
            get
            {
                double v = 0;
                foreach (var f in Faces)
                    foreach (var loop in f.Loops)
                    {
                        var a = Vertices[loop[0]];
                        for (int i = 1; i + 1 < loop.Length; i++)
                            v += Vec.Dot(a, Vec.Cross(Vertices[loop[i]], Vertices[loop[i + 1]]));
                    }
                return v / 6.0;
            }
        }

        public double SurfaceArea
        {
            get
            {
                double a = 0;
                foreach (var f in Faces)
                {
                    var n = FaceNormal(f);
                    foreach (var loop in f.Loops) a += 0.5 * Vec.Dot(LoopNormal(loop), n);
                }
                return a;
            }
        }

        /// <summary>
        /// Merges coincident vertices, drops repeated vertices inside loops and removes faces that
        /// collapse to fewer than three distinct vertices. Shapes with poles (spheres, cones to a point)
        /// and fully rounded corners rely on this.
        /// </summary>
        public Polyhedron Weld(double tolerance = 1e-9)
        {
            double size = Math.Max(1.0, Vec.Length(Vec.Sub(Max, Min)));
            double tol = tolerance * size;
            var map = new int[Vertices.Count];
            var grid = new Dictionary<(long, long, long), List<int>>();
            var kept = new List<(double X, double Y, double Z)>();
            (long, long, long) Cell((double X, double Y, double Z) p) =>
                ((long)Math.Floor(p.X / (tol * 4)), (long)Math.Floor(p.Y / (tol * 4)), (long)Math.Floor(p.Z / (tol * 4)));

            for (int i = 0; i < Vertices.Count; i++)
            {
                var p = Vertices[i];
                var c = Cell(p);
                int found = -1;
                for (long dx = -1; dx <= 1 && found < 0; dx++)
                    for (long dy = -1; dy <= 1 && found < 0; dy++)
                        for (long dz = -1; dz <= 1 && found < 0; dz++)
                            if (grid.TryGetValue((c.Item1 + dx, c.Item2 + dy, c.Item3 + dz), out var list))
                                foreach (int k in list)
                                    if (Vec.Length(Vec.Sub(kept[k], p)) <= tol) { found = k; break; }
                if (found < 0)
                {
                    found = kept.Count;
                    kept.Add(p);
                    if (!grid.TryGetValue(c, out var bucket)) grid[c] = bucket = new List<int>();
                    bucket.Add(found);
                }
                map[i] = found;
            }

            int[] Clean(int[] loop)
            {
                var r = new List<int>(loop.Length);
                foreach (int v in loop)
                {
                    int m = map[v];
                    if (r.Count == 0 || r[r.Count - 1] != m) r.Add(m);
                }
                while (r.Count > 1 && r[0] == r[r.Count - 1]) r.RemoveAt(r.Count - 1);
                return r.ToArray();
            }

            var result = new Polyhedron { Vertices = kept };
            foreach (var f in Faces)
            {
                var outer = Clean(f.Outer);
                if (outer.Length < 3) continue;
                var holes = f.Holes.Select(Clean).Where(h => h.Length >= 3);
                result.Faces.Add(new PolyFace(outer, holes));
            }
            return result;
        }

        /// <summary>
        /// Makes every face wind consistently with its neighbours and flips the whole solid if it ends
        /// up inside out. Shape builders can therefore emit faces in either winding.
        /// </summary>
        public Polyhedron Orient()
        {
            FixHoleWinding();
            var edgeFaces = new Dictionary<(int, int), List<int>>();
            for (int fi = 0; fi < Faces.Count; fi++)
                foreach (var loop in Faces[fi].Loops)
                    for (int k = 0; k < loop.Length; k++)
                    {
                        int a = loop[k], b = loop[(k + 1) % loop.Length];
                        var key = a < b ? (a, b) : (b, a);
                        if (!edgeFaces.TryGetValue(key, out var l)) edgeFaces[key] = l = new List<int>();
                        l.Add(fi);
                    }

            bool Traverses(PolyFace f, int a, int b)
            {
                foreach (var loop in f.Loops)
                    for (int k = 0; k < loop.Length; k++)
                        if (loop[k] == a && loop[(k + 1) % loop.Length] == b) return true;
                return false;
            }

            var visited = new bool[Faces.Count];
            for (int seed = 0; seed < Faces.Count; seed++)
            {
                if (visited[seed]) continue;
                visited[seed] = true;
                var component = new List<int> { seed };
                var queue = new Queue<int>();
                queue.Enqueue(seed);
                while (queue.Count > 0)
                {
                    int fi = queue.Dequeue();
                    var f = Faces[fi];
                    foreach (var loop in f.Loops)
                        for (int k = 0; k < loop.Length; k++)
                        {
                            int a = loop[k], b = loop[(k + 1) % loop.Length];
                            var key = a < b ? (a, b) : (b, a);
                            var adj = edgeFaces[key];
                            if (adj.Count != 2) continue;
                            int gi = adj[0] == fi ? adj[1] : adj[0];
                            if (gi == fi || visited[gi]) continue;
                            if (Traverses(Faces[gi], a, b)) Faces[gi].Reverse();
                            visited[gi] = true;
                            component.Add(gi);
                            queue.Enqueue(gi);
                        }
                }

                // Each connected shell gets its own inside-out check.
                double vol = 0;
                foreach (int fi in component)
                    foreach (var loop in Faces[fi].Loops)
                    {
                        var a = Vertices[loop[0]];
                        for (int i = 1; i + 1 < loop.Length; i++)
                            vol += Vec.Dot(a, Vec.Cross(Vertices[loop[i]], Vertices[loop[i + 1]]));
                    }
                if (vol < 0) foreach (int fi in component) Faces[fi].Reverse();
            }

            return this;
        }

        // A face's holes must wind against its outer loop; done before propagation so hole edges
        // carry the same orientation information as outer edges.
        private void FixHoleWinding()
        {
            foreach (var f in Faces)
            {
                var n = LoopNormal(f.Outer);
                for (int h = 0; h < f.Holes.Count; h++)
                    if (Vec.Dot(LoopNormal(f.Holes[h]), n) > 0) Array.Reverse(f.Holes[h]);
            }
        }

        /// <summary>
        /// Joins neighbouring faces that lie in the same plane into one face, so a revolved end cap
        /// becomes a single disc instead of a fan of triangles. Faces must already be oriented.
        /// </summary>
        public Polyhedron MergeCoplanarFaces()
        {
            double size = Math.Max(1e-9, Vec.Length(Vec.Sub(Max, Min)));
            var normals = Faces.Select(FaceNormal).ToList();
            var edgeFace = new Dictionary<(int, int), int>();
            for (int fi = 0; fi < Faces.Count; fi++)
                foreach (var loop in Faces[fi].Loops)
                    for (int k = 0; k < loop.Length; k++)
                        edgeFace[(loop[k], loop[(k + 1) % loop.Length])] = fi;

            bool SamePlane(int a, int b)
            {
                if (Vec.Dot(normals[a], normals[b]) < 1 - 1e-12) return false;
                var p = Vertices[Faces[a].Outer[0]];
                return Faces[b].Outer.All(v => Math.Abs(Vec.Dot(Vec.Sub(Vertices[v], p), normals[a])) < 1e-9 * size);
            }

            var group = Enumerable.Repeat(-1, Faces.Count).ToArray();
            var groups = new List<List<int>>();
            for (int seed = 0; seed < Faces.Count; seed++)
            {
                if (group[seed] >= 0) continue;
                var members = new List<int> { seed };
                group[seed] = groups.Count;
                for (int q = 0; q < members.Count; q++)
                    foreach (var loop in Faces[members[q]].Loops)
                        for (int k = 0; k < loop.Length; k++)
                        {
                            if (!edgeFace.TryGetValue((loop[(k + 1) % loop.Length], loop[k]), out int nb)) continue;
                            if (group[nb] >= 0 || !SamePlane(seed, nb)) continue;
                            group[nb] = groups.Count;
                            members.Add(nb);
                        }
                groups.Add(members);
            }
            if (groups.Count == Faces.Count) return this;

            var merged = new List<PolyFace>();
            foreach (var members in groups)
            {
                if (members.Count == 1) { merged.Add(Faces[members[0]]); continue; }
                var face = MergeGroup(members, normals[members[0]]);
                if (face != null) merged.Add(face);
                else merged.AddRange(members.Select(m => Faces[m]));
            }
            Faces = merged;
            return this;
        }

        private PolyFace MergeGroup(List<int> members, (double X, double Y, double Z) normal)
        {
            var inGroup = new HashSet<(int, int)>();
            foreach (int m in members)
                foreach (var loop in Faces[m].Loops)
                    for (int k = 0; k < loop.Length; k++) inGroup.Add((loop[k], loop[(k + 1) % loop.Length]));

            var next = new Dictionary<int, int>();
            foreach (var e in inGroup)
            {
                if (inGroup.Contains((e.Item2, e.Item1))) continue;
                if (next.ContainsKey(e.Item1)) return null; // pinched boundary: leave the faces as they are
                next[e.Item1] = e.Item2;
            }

            var loops = new List<int[]>();
            var used = new HashSet<int>();
            foreach (int start in next.Keys)
            {
                if (used.Contains(start)) continue;
                var loop = new List<int>();
                int v = start;
                while (used.Add(v))
                {
                    loop.Add(v);
                    if (!next.TryGetValue(v, out v)) return null;
                }
                if (v != start || loop.Count < 3) return null;
                loops.Add(loop.ToArray());
            }

            var outers = loops.Where(l => Vec.Dot(LoopNormal(l), normal) > 0).ToList();
            if (outers.Count != 1) return null;
            return new PolyFace(outers[0], loops.Where(l => l != outers[0]));
        }

        /// <summary>Weld + orient + merge coplanar faces: the clean-up every shape runs before export.</summary>
        public Polyhedron Normalize() => Weld().Orient().MergeCoplanarFaces();

        /// <summary>
        /// Checks that the polyhedron is a closed, consistently oriented, planar 2-manifold with
        /// positive volume. Returns human-readable problems; an empty list means it is a valid solid.
        /// </summary>
        public List<string> Validate(double planarTolerance = 1e-6)
        {
            var problems = new List<string>();
            if (Faces.Count < 4) problems.Add($"only {Faces.Count} faces");

            var directed = new Dictionary<(int, int), int>();
            foreach (var f in Faces)
                foreach (var loop in f.Loops)
                    for (int k = 0; k < loop.Length; k++)
                    {
                        var e = (loop[k], loop[(k + 1) % loop.Length]);
                        directed.TryGetValue(e, out int c);
                        directed[e] = c + 1;
                    }
            int open = 0, duplicated = 0;
            foreach (var kv in directed)
            {
                if (kv.Value > 1) duplicated++;
                if (!directed.ContainsKey((kv.Key.Item2, kv.Key.Item1))) open++;
            }
            if (open > 0) problems.Add($"{open} edges have no opposite partner (open or inconsistently oriented shell)");
            if (duplicated > 0) problems.Add($"{duplicated} edges are used more than once in the same direction (non-manifold)");

            double size = Math.Max(1e-9, Vec.Length(Vec.Sub(Max, Min)));
            for (int fi = 0; fi < Faces.Count; fi++)
            {
                var f = Faces[fi];
                var n = FaceNormal(f);
                if (n == (0, 0, 0)) { problems.Add($"face {fi} is degenerate"); continue; }
                var p0 = Vertices[f.Outer[0]];
                double worst = f.Loops.SelectMany(l => l).Max(v => Math.Abs(Vec.Dot(Vec.Sub(Vertices[v], p0), n)));
                if (worst > planarTolerance * size) problems.Add($"face {fi} is not planar (deviation {worst:G3})");
            }

            double vol = Volume;
            if (!(vol > 0)) problems.Add($"volume is {vol:G6}; expected positive");
            return problems;
        }
    }
}
