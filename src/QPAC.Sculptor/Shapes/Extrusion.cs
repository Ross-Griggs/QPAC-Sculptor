using System;
using System.Collections.Generic;
using System.Linq;

namespace QPAC.Sculptor
{
    /// <summary>A blind cut into one cap face of an <see cref="Extrusion"/>, leaving a floor behind.</summary>
    public sealed class Pocket
    {
        public List<(double U, double V)> Loop;
        public double Depth;
        public bool FromBack;

        public Pocket(IEnumerable<(double U, double V)> loop, double depth, bool fromBack = false)
        {
            Loop = loop.ToList();
            Depth = depth;
            FromBack = fromBack;
        }
    }

    /// <summary>
    /// A 2D profile drawn in the plane through <see cref="Origin"/> spanned by <see cref="U"/> and
    /// <see cref="V"/>, swept <see cref="Depth"/> along U×V. Supports through-cuts, blind pockets,
    /// shelling, rib grids, filleted or chamfered cap edges and draft.
    /// </summary>
    public class Extrusion : StepShape
    {
        public string Name;
        public (double X, double Y, double Z) Origin;
        public (double X, double Y, double Z) U;
        public (double X, double Y, double Z) V;
        public double Depth;
        public List<(double U, double V)> Outer = new List<(double U, double V)>();
        public List<List<(double U, double V)>> Cuts = new List<List<(double U, double V)>>();
        public List<Pocket> Pockets = new List<Pocket>();

        /// <summary>Finish on the edges around the face at the origin plane (null = sharp).</summary>
        public EdgeFinish FrontEdge;

        /// <summary>Finish on the edges around the face at <see cref="Depth"/> (null = sharp).</summary>
        public EdgeFinish BackEdge;

        /// <summary>Taper of the side walls in degrees; positive shrinks the profile toward the back face.</summary>
        public double DraftDegrees;

        public Extrusion(
            string name,
            (double X, double Y, double Z) origin,
            (double X, double Y, double Z) u,
            (double X, double Y, double Z) v,
            double depth)
        {
            Name = name;
            Origin = origin; U = u; V = v; Depth = depth;
        }

        /// <summary>Extrusion in the world XY plane, swept along +Z from <paramref name="z"/>.</summary>
        public static Extrusion AlongZ(string name, double z, double depth, IEnumerable<(double U, double V)> profile)
            => new Extrusion(name, (0, 0, z), (1, 0, 0), (0, 1, 0), depth).SetProfile(profile);

        public override string ShapeName => Name;

        public static List<(double U, double V)> Circle(double cu, double cv, double radius, int sides = 32)
            => Profiles.Circle(cu, cv, radius, sides);

        public Extrusion SetProfile(IEnumerable<(double U, double V)> profile)
        {
            Outer = profile.ToList();
            return this;
        }

        public Extrusion AddCircle(double cu, double cv, double radius, int sides = 256)
            => SetProfile(Profiles.Circle(cu, cv, radius, sides));

        public Extrusion AddRectangle(double cu, double cv, double width, double height, double cornerRadius = 0, int segmentsPerCorner = 8)
            => SetProfile(Profiles.RoundedRectangle(cu, cv, width, height, cornerRadius, segmentsPerCorner));

        public Extrusion AddPolygon(params (double U, double V)[] points) => SetProfile(Profiles.Polygon(points));

        public Extrusion AddCut(IEnumerable<(double U, double V)> loop)
        {
            Cuts.Add(Polygon2D.WithWinding(loop, false));
            return this;
        }

        public Extrusion AddCircularCut(double cu, double cv, double radius, int sides = 256)
            => AddCut(Profiles.Circle(cu, cv, radius, sides));

        public Extrusion AddRectangularCut(double cu, double cv, double width, double height, double cornerRadius = 0, int segmentsPerCorner = 8)
            => AddCut(Profiles.RoundedRectangle(cu, cv, width, height, cornerRadius, segmentsPerCorner));

        public Extrusion AddSlotCut(double u1, double v1, double u2, double v2, double width, int segmentsPerEnd = 16)
            => AddCut(Profiles.Slot(u1, v1, u2, v2, width, segmentsPerEnd));

        /// <summary><paramref name="count"/> round holes evenly spaced on a circle of <paramref name="patternRadius"/>.</summary>
        public Extrusion AddBoltCircle(double cu, double cv, double patternRadius, int count, double holeRadius, double startDegrees = 0, int sides = 32)
        {
            for (int i = 0; i < count; i++)
            {
                double a = (startDegrees + 360.0 * i / count) * Math.PI / 180;
                AddCircularCut(cu + patternRadius * Math.Cos(a), cv + patternRadius * Math.Sin(a), holeRadius, sides);
            }
            return this;
        }

        /// <summary>A grid of round holes starting at (<paramref name="u0"/>, <paramref name="v0"/>).</summary>
        public Extrusion AddHoleGrid(double u0, double v0, int columns, int rows, double pitchU, double pitchV, double holeRadius, int sides = 32)
        {
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < columns; c++)
                    AddCircularCut(u0 + c * pitchU, v0 + r * pitchV, holeRadius, sides);
            return this;
        }

        public Extrusion AddPocket(IEnumerable<(double U, double V)> loop, double depth, bool fromBack = false)
        {
            Pockets.Add(new Pocket(loop, depth, fromBack));
            return this;
        }

        public Extrusion AddCircularPocket(double cu, double cv, double radius, double depth, bool fromBack = false, int sides = 64)
            => AddPocket(Profiles.Circle(cu, cv, radius, sides), depth, fromBack);

        public Extrusion AddRectangularPocket(double cu, double cv, double width, double height, double depth, double cornerRadius = 0, bool fromBack = false)
            => AddPocket(Profiles.RoundedRectangle(cu, cv, width, height, cornerRadius), depth, fromBack);

        /// <summary>
        /// Hollows the solid into a tray: walls <paramref name="wallThickness"/> thick around the
        /// outline and a floor <paramref name="floorThickness"/> thick, open on the front face (or the
        /// back). A floor of 0 makes an open-ended tube instead.
        /// </summary>
        public Extrusion Shell(double wallThickness, double floorThickness, bool openFront = true)
        {
            var inner = Polygon2D.OffsetLeft(Polygon2D.WithWinding(Polygon2D.Clean(Outer), true), wallThickness, Name + " shell");
            if (floorThickness <= 0) return AddCut(inner);
            return AddPocket(inner, Math.Abs(Depth) - floorThickness, !openFront);
        }

        /// <summary>
        /// Like <see cref="Shell"/> but leaves a grid of stiffening ribs standing inside the pocket:
        /// <paramref name="columns"/> × <paramref name="rows"/> pockets separated by ribs
        /// <paramref name="ribThickness"/> wide. The shelled interior must be convex.
        /// </summary>
        public Extrusion AddPocketGrid(double wallThickness, double floorThickness, double ribThickness, int columns, int rows, bool openFront = true)
        {
            var inner = Polygon2D.OffsetLeft(Polygon2D.WithWinding(Polygon2D.Clean(Outer), true), wallThickness, Name + " pocket grid");
            if (!Polygon2D.IsConvex(inner))
                throw new ArgumentException($"{Name}: AddPocketGrid needs a convex outline; add individual pockets with AddPocket instead.");
            double u0 = inner.Min(p => p.U), u1 = inner.Max(p => p.U);
            double v0 = inner.Min(p => p.V), v1 = inner.Max(p => p.V);
            int cols = Math.Max(1, columns), rws = Math.Max(1, rows);
            double cw = (u1 - u0 - (cols - 1) * ribThickness) / cols;
            double ch = (v1 - v0 - (rws - 1) * ribThickness) / rws;
            if (cw <= 0 || ch <= 0) throw new ArgumentException($"{Name}: ribs leave no room for pockets.");
            double depth = Math.Abs(Depth) - floorThickness;
            for (int r = 0; r < rws; r++)
                for (int c = 0; c < cols; c++)
                {
                    double cu = u0 + c * (cw + ribThickness) + cw / 2;
                    double cv = v0 + r * (ch + ribThickness) + ch / 2;
                    var cell = Polygon2D.ClipConvex(Profiles.Rectangle(cu, cv, cw, ch), inner);
                    if (cell.Count >= 3 && Profiles.Area(cell) > 1e-6 * cw * ch) AddPocket(cell, depth, !openFront);
                }
            return this;
        }

        /// <summary>Rounds every corner of the outline (the edges running along the extrusion).</summary>
        public Extrusion FilletCorners(double radius, int segmentsPerCorner = 8)
            => SetProfile(Profiles.Fillet(Outer, radius, segmentsPerCorner));

        /// <summary>Rounds the edges around the cap faces.</summary>
        public Extrusion FilletEdges(double radius, int segments = 6, bool front = true, bool back = true)
        {
            if (front) FrontEdge = EdgeFinish.Fillet(radius, segments);
            if (back) BackEdge = EdgeFinish.Fillet(radius, segments);
            return this;
        }

        public Extrusion ChamferEdges(double distance, bool front = true, bool back = true)
        {
            if (front) FrontEdge = EdgeFinish.Chamfer(distance);
            if (back) BackEdge = EdgeFinish.Chamfer(distance);
            return this;
        }

        public Extrusion WithDraft(double degrees)
        {
            DraftDegrees = degrees;
            return this;
        }

        public override Polyhedron CreateSolid()
        {
            var solid = new Polyhedron();
            var outer = Polygon2D.Clean(Outer ?? new List<(double U, double V)>());
            if (outer.Count < 3 || Math.Abs(Depth) < 1e-9) return solid;
            outer = Polygon2D.WithWinding(outer, true);
            if (Polygon2D.SelfIntersects(outer)) throw new ArgumentException($"{Name}: the outline crosses itself.");

            double D = Math.Abs(Depth), sign = Math.Sign(Depth);
            var u = Vec.Normalize(U);
            var v = Vec.Normalize(Vec.Sub(V, Vec.Scale(u, Vec.Dot(V, u))));
            if (u == (0, 0, 0) || v == (0, 0, 0)) throw new ArgumentException($"{Name}: U and V must be non-zero and not parallel.");
            var w = Vec.Cross(u, v);
            int Vertex((double U, double V) p, double t) => solid.AddVertex(Vec.Frame(Origin, u, v, w, p.U, p.V, sign * t));

            var cuts = (Cuts ?? new List<List<(double U, double V)>>())
                .Select(c => Polygon2D.Clean(c)).Where(c => c.Count >= 3)
                .Select(c => Polygon2D.WithWinding(c, false)).ToList();
            var pockets = (Pockets ?? new List<Pocket>())
                .Select(p => (Loop: Polygon2D.WithWinding(Polygon2D.Clean(p.Loop), false), p.Depth, p.FromBack))
                .Where(p => p.Loop.Count >= 3).ToList();

            foreach (var p in pockets)
                if (p.Depth <= 0 || p.Depth >= D)
                    throw new ArgumentException($"{Name}: pocket depth {p.Depth:G4} must be between 0 and the extrusion depth {D:G4}; use a cut for a through-hole.");

            for (int i = 0; i < cuts.Count; i++)
                for (int j = i + 1; j < cuts.Count; j++)
                    if (!Polygon2D.Disjoint(cuts[i], cuts[j]))
                        throw new ArgumentException($"{Name}: cuts {i} and {j} overlap.");

            // Each cut is either free (runs cap to cap) or sits inside a front and/or back pocket.
            var frontOf = new int[cuts.Count];
            var backOf = new int[cuts.Count];
            for (int c = 0; c < cuts.Count; c++)
            {
                frontOf[c] = backOf[c] = -1;
                for (int p = 0; p < pockets.Count; p++)
                {
                    if (Polygon2D.Inside(cuts[c], pockets[p].Loop))
                    {
                        if (pockets[p].FromBack) backOf[c] = p; else frontOf[c] = p;
                    }
                    else if (!Polygon2D.Disjoint(cuts[c], pockets[p].Loop))
                        throw new ArgumentException($"{Name}: cut {c} straddles the edge of pocket {p}.");
                }
            }

            var rings = RingSamples(D);
            var free = new List<List<(double U, double V)>> { outer };
            var freeCutIndex = new List<int>();
            for (int c = 0; c < cuts.Count; c++)
                if (frontOf[c] < 0 && backOf[c] < 0) { free.Add(cuts[c]); freeCutIndex.Add(c); }

            var ringLoops = rings.Select(r => free.Select(l => Polygon2D.OffsetLeft(l, r.Inset, Name)).ToList()).ToList();
            int last = rings.Count - 1;

            foreach (int k in new[] { 0, last })
                for (int l = 1; l < free.Count; l++)
                    if (!Polygon2D.Inside(ringLoops[k][l], ringLoops[k][0]))
                        throw new ArgumentException($"{Name}: cut {freeCutIndex[l - 1]} runs outside the outline or too close to its edge.");

            for (int p = 0; p < pockets.Count; p++)
            {
                var pk = pockets[p];
                var cap = ringLoops[pk.FromBack ? last : 0];
                if (!Polygon2D.Inside(pk.Loop, cap[0]))
                    throw new ArgumentException($"{Name}: pocket {p} runs outside the outline or into a rounded edge.");
                for (int l = 1; l < cap.Count; l++)
                    if (!Polygon2D.Disjoint(pk.Loop, cap[l]))
                        throw new ArgumentException($"{Name}: pocket {p} overlaps cut {freeCutIndex[l - 1]}.");
                for (int q = p + 1; q < pockets.Count; q++)
                {
                    if (Polygon2D.Disjoint(pk.Loop, pockets[q].Loop)) continue;
                    if (pk.FromBack == pockets[q].FromBack)
                        throw new ArgumentException($"{Name}: pockets {p} and {q} overlap.");
                    if (pk.Depth + pockets[q].Depth >= D - 1e-9)
                        throw new ArgumentException($"{Name}: pockets {p} and {q} meet in the middle; use a cut instead.");
                }
            }

            var ringIdx = new int[rings.Count][][];
            for (int k = 0; k < rings.Count; k++)
            {
                ringIdx[k] = new int[free.Count][];
                for (int l = 0; l < free.Count; l++)
                    ringIdx[k][l] = ringLoops[k][l].Select(p => Vertex(p, rings[k].T)).ToArray();
            }

            void Walls(int[] a, int[] b)
            {
                for (int i = 0; i < a.Length; i++)
                {
                    int j = (i + 1) % a.Length;
                    solid.AddFace(new[] { a[i], a[j], b[j], b[i] });
                }
            }

            for (int k = 0; k < last; k++)
                for (int l = 0; l < free.Count; l++)
                    Walls(ringIdx[k][l], ringIdx[k + 1][l]);

            var frontHoles = new List<int[]>();
            var backHoles = new List<int[]>();
            for (int l = 1; l < free.Count; l++)
            {
                frontHoles.Add(ringIdx[0][l]);
                backHoles.Add(ringIdx[last][l]);
            }

            var floorHoles = pockets.Select(_ => new List<int[]>()).ToList();
            for (int c = 0; c < cuts.Count; c++)
            {
                if (frontOf[c] < 0 && backOf[c] < 0) continue;
                double t0 = frontOf[c] >= 0 ? pockets[frontOf[c]].Depth : 0;
                double t1 = backOf[c] >= 0 ? D - pockets[backOf[c]].Depth : D;
                var start = cuts[c].Select(p => Vertex(p, t0)).ToArray();
                var end = cuts[c].Select(p => Vertex(p, t1)).ToArray();
                Walls(start, end);
                if (frontOf[c] >= 0) floorHoles[frontOf[c]].Add(start); else frontHoles.Add(start);
                if (backOf[c] >= 0) floorHoles[backOf[c]].Add(end); else backHoles.Add(end);
            }

            for (int p = 0; p < pockets.Count; p++)
            {
                var pk = pockets[p];
                double tCap = pk.FromBack ? D : 0;
                double tFloor = pk.FromBack ? D - pk.Depth : pk.Depth;
                var top = pk.Loop.Select(q => Vertex(q, tCap)).ToArray();
                var floor = pk.Loop.Select(q => Vertex(q, tFloor)).ToArray();
                Walls(top, floor);
                solid.AddFace(floor, floorHoles[p].ToArray());
                (pk.FromBack ? backHoles : frontHoles).Add(top);
            }

            solid.AddFace(ringIdx[0][0], frontHoles.ToArray());
            solid.AddFace(ringIdx[last][0], backHoles.ToArray());
            return solid;
        }

        private List<(double T, double Inset)> RingSamples(double depth)
        {
            var front = FrontEdge?.Samples() ?? new[] { (0.0, 0.0) };
            var back = BackEdge?.Samples() ?? new[] { (0.0, 0.0) };
            double fs = front[front.Length - 1].T, bs = back[back.Length - 1].T;
            if (fs + bs > depth + 1e-12)
                throw new ArgumentException($"{Name}: edge finishes ({fs:G4} + {bs:G4}) are deeper than the extrusion ({depth:G4}).");

            var rings = new List<(double T, double Inset)>(front);
            foreach (var s in Enumerable.Reverse(back))
            {
                var r = (T: depth - s.T, s.Inset);
                if (Math.Abs(r.T - rings[rings.Count - 1].T) < 1e-12 && Math.Abs(r.Inset - rings[rings.Count - 1].Inset) < 1e-12) continue;
                rings.Add(r);
            }

            if (Math.Abs(DraftDegrees) > 1e-12)
            {
                double tan = Math.Tan(DraftDegrees * Math.PI / 180);
                for (int k = 0; k < rings.Count; k++) rings[k] = (rings[k].T, rings[k].Inset + rings[k].T * tan);
            }
            return rings;
        }
    }
}
