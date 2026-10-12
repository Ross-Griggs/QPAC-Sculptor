using System;
using System.Collections.Generic;
using System.Linq;

namespace QPAC.Sculptor
{
    /// <summary>
    /// A solid blended between a bottom and a top profile — square-to-round transitions, reducers,
    /// hoppers and offset ducts. Profiles may have different vertex counts; corners are kept.
    /// </summary>
    public class Loft : StepShape
    {
        public string Name;
        public (double X, double Y, double Z) Origin;
        public (double X, double Y, double Z) U;
        public (double X, double Y, double Z) V;
        public double Height;
        public List<(double U, double V)> Bottom;
        public List<(double U, double V)> Top;

        /// <summary>Shifts the top profile within its plane (offset transitions).</summary>
        public (double U, double V) TopOffset;

        public Loft(string name, (double X, double Y, double Z) origin, (double X, double Y, double Z) u, (double X, double Y, double Z) v,
            double height, IEnumerable<(double U, double V)> bottom, IEnumerable<(double U, double V)> top)
        {
            Name = name;
            Origin = origin; U = u; V = v; Height = height;
            Bottom = bottom.ToList();
            Top = top.ToList();
        }

        /// <summary>Rectangular duct end blending into a round one.</summary>
        public static Loft SquareToRound(string name, (double X, double Y, double Z) origin, double width, double depth, double diameter, double height, int sides = 48)
            => new Loft(name, origin, (1, 0, 0), (0, 1, 0), height,
                Profiles.Rectangle(0, 0, width, depth), Profiles.Circle(0, 0, diameter / 2, sides));

        public override string ShapeName => Name;

        public override Polyhedron CreateSolid()
        {
            var solid = new Polyhedron();
            var a = Polygon2D.WithWinding(Polygon2D.Clean(Bottom ?? new List<(double U, double V)>()), true);
            var b = Polygon2D.WithWinding(Polygon2D.Clean(Top ?? new List<(double U, double V)>()), true);
            if (a.Count < 3 || b.Count < 3 || Math.Abs(Height) < 1e-9) return solid;

            int n = Math.Max(a.Count, b.Count);
            a = Polygon2D.Densify(a, n);
            b = Polygon2D.Densify(b, n);
            b = Align(a, b);

            var u = Vec.Normalize(U);
            var v = Vec.Normalize(Vec.Sub(V, Vec.Scale(u, Vec.Dot(V, u))));
            if (u == (0, 0, 0) || v == (0, 0, 0)) throw new ArgumentException($"{Name}: U and V must be non-zero and not parallel.");
            var w = Vec.Cross(u, v);

            var bottom = a.Select(p => solid.AddVertex(Vec.Frame(Origin, u, v, w, p.U, p.V, 0))).ToArray();
            var top = b.Select(p => solid.AddVertex(Vec.Frame(Origin, u, v, w, p.U + TopOffset.U, p.V + TopOffset.V, Height))).ToArray();

            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                var quad = new[] { bottom[i], bottom[j], top[j], top[i] };
                if (Planar(solid, quad)) solid.AddFace(quad);
                else
                {
                    solid.AddFace(new[] { bottom[i], bottom[j], top[j] });
                    solid.AddFace(new[] { bottom[i], top[j], top[i] });
                }
            }
            solid.AddFace(bottom);
            solid.AddFace(top);
            return solid;
        }

        // Rotate the top loop's start so each bottom vertex pairs with the nearest top vertex (minimum twist).
        private static List<(double U, double V)> Align(List<(double U, double V)> a, List<(double U, double V)> b)
        {
            int n = a.Count, best = 0;
            double ca = a.Average(p => p.U), cva = a.Average(p => p.V);
            double cb = b.Average(p => p.U), cvb = b.Average(p => p.V);
            double bestCost = double.MaxValue;
            for (int s = 0; s < n; s++)
            {
                double cost = 0;
                for (int i = 0; i < n; i++)
                {
                    var p = a[i]; var q = b[(i + s) % n];
                    double du = (p.U - ca) - (q.U - cb), dv = (p.V - cva) - (q.V - cvb);
                    cost += du * du + dv * dv;
                }
                if (cost < bestCost) { bestCost = cost; best = s; }
            }
            return Enumerable.Range(0, n).Select(i => b[(i + best) % n]).ToList();
        }

        private static bool Planar(Polyhedron s, int[] quad)
        {
            var p0 = s.Vertices[quad[0]];
            var nrm = Vec.Normalize(s.LoopNormal(quad));
            if (nrm == (0, 0, 0)) return false;
            double size = Vec.Length(Vec.Sub(s.Vertices[quad[2]], p0));
            return quad.All(i => Math.Abs(Vec.Dot(Vec.Sub(s.Vertices[i], p0), nrm)) <= 1e-9 * Math.Max(1, size));
        }
    }

    /// <summary>Any closed polyhedron you build yourself (for example from an imported mesh).</summary>
    public class MeshSolid : StepShape
    {
        public string Name;
        public Polyhedron Mesh;

        public MeshSolid(string name, Polyhedron mesh)
        {
            Name = name;
            Mesh = mesh;
        }

        public override string ShapeName => Name;

        public override Polyhedron CreateSolid() => Mesh?.Clone() ?? new Polyhedron();
    }
}
