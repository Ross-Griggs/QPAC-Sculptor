using System;
using System.Collections.Generic;

namespace QPAC.Sculptor
{
    /// <summary>A box centred on (<see cref="Cx"/>, <see cref="Cy"/>, <see cref="Cz"/>) with sizes along X, Y and Z.</summary>
    public class Rectangle : StepShape
    {
        public string Name;
        public double Cx, Cy, Cz;
        public double Sx, Sy, Sz;

        /// <summary>Rounds (or with <see cref="ChamferEdges"/> bevels) all twelve edges by this amount.</summary>
        public double EdgeRadius;
        public bool ChamferEdges;
        public int EdgeSegments = 6;

        public Rectangle(string name, double cx, double cy, double cz, double sx, double sy, double sz)
        {
            Name = name;
            Cx = cx;
            Cy = cy;
            Cz = cz;
            Sx = sx;
            Sy = sy;
            Sz = sz;
        }

        /// <summary>Box spanning two opposite corners.</summary>
        public static Rectangle FromCorners(string name, (double X, double Y, double Z) a, (double X, double Y, double Z) b)
            => new Rectangle(name, (a.X + b.X) / 2, (a.Y + b.Y) / 2, (a.Z + b.Z) / 2, Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y), Math.Abs(b.Z - a.Z));

        public override string ShapeName => Name;

        /// <summary>
        /// Orient the box so its local X/Y/Z axes align with the given orthonormal basis vectors,
        /// pivoting about its centre. Prefer <see cref="StepShapeTransformExtensions.RotateAbout"/>.
        /// </summary>
        public Rectangle Oriented(
            (double X, double Y, double Z) ax,
            (double X, double Y, double Z) ay,
            (double X, double Y, double Z) az)
        {
            Xf = Xf.Then(Transform.FromBasis((Cx, Cy, Cz), ax, ay, az));
            return this;
        }

        public Rectangle WithFillet(double radius, int segments = 6)
        {
            EdgeRadius = radius;
            EdgeSegments = segments;
            ChamferEdges = false;
            return this;
        }

        public Rectangle WithChamfer(double distance)
        {
            EdgeRadius = distance;
            ChamferEdges = true;
            return this;
        }

        public override Polyhedron CreateSolid()
        {
            var solid = new Polyhedron();
            double hx = Math.Abs(Sx) / 2, hy = Math.Abs(Sy) / 2, hz = Math.Abs(Sz) / 2;
            if (hx < 1e-12 || hy < 1e-12 || hz < 1e-12) return solid;

            double r = Math.Max(0, Math.Min(EdgeRadius, Math.Min(hx, Math.Min(hy, hz))));
            (double T, double Inset)[] samples = r > 0
                ? (ChamferEdges ? EdgeFinish.Chamfer(r) : EdgeFinish.Fillet(r, EdgeSegments)).Samples()
                : new[] { (0.0, 0.0) };

            // Rings from the bottom face up; each is the outline at that height with a fixed vertex count
            // so neighbouring rings stitch into quads (coincident points weld away at the caps).
            var rings = new List<(double Z, double Inset)>();
            foreach (var s in samples) rings.Add((s.T, s.Inset));
            for (int i = samples.Length - 1; i >= 0; i--)
            {
                var s = samples[i];
                if (2 * hz - s.T > rings[rings.Count - 1].Z + 1e-12) rings.Add((2 * hz - s.T, s.Inset));
            }

            var idx = new List<int[]>();
            foreach (var ring in rings)
            {
                var loop = Outline(hx - ring.Inset, hy - ring.Inset, r - ring.Inset, r > 0 ? (ChamferEdges ? 1 : Math.Max(1, EdgeSegments)) : 0);
                var ids = new int[loop.Count];
                for (int i = 0; i < loop.Count; i++)
                    ids[i] = solid.AddVertex((Cx + loop[i].U, Cy + loop[i].V, Cz - hz + ring.Z));
                idx.Add(ids);
            }

            for (int k = 0; k + 1 < idx.Count; k++)
            {
                var a = idx[k]; var b = idx[k + 1];
                for (int i = 0; i < a.Length; i++)
                {
                    int j = (i + 1) % a.Length;
                    solid.AddFace(new[] { a[i], a[j], b[j], b[i] });
                }
            }
            solid.AddFace(idx[0]);
            solid.AddFace(idx[idx.Count - 1]);
            return solid;
        }

        // Rectangle of half-extents (ex, ey) with corners rounded by radius cr (chamfered when segments == 1).
        private static List<(double U, double V)> Outline(double ex, double ey, double cr, int segments)
        {
            var loop = new List<(double U, double V)>();
            if (segments == 0)
            {
                loop.Add((-ex, -ey)); loop.Add((ex, -ey)); loop.Add((ex, ey)); loop.Add((-ex, ey));
                return loop;
            }
            cr = Math.Max(0, cr);
            var centers = new[] { (ex - cr, -ey + cr), (ex - cr, ey - cr), (-ex + cr, ey - cr), (-ex + cr, -ey + cr) };
            for (int c = 0; c < 4; c++)
            {
                double a0 = -Math.PI / 2 + c * Math.PI / 2;
                for (int i = 0; i <= segments; i++)
                {
                    double a = a0 + Math.PI / 2 * i / segments;
                    loop.Add((centers[c].Item1 + cr * Math.Cos(a), centers[c].Item2 + cr * Math.Sin(a)));
                }
            }
            return loop;
        }
    }
}
