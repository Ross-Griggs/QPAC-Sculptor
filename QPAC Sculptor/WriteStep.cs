using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QPAC.Sculptor
{
    public class Step
    {
        private static readonly (double X, double Y, double Z)[] Axes =
        {
            (1, 0, 0), (-1, 0, 0),
            (0, 1, 0), (0, -1, 0),
            (0, 0, 1), (0, 0, -1),
        }; 
        public static void WriteStep(string path, StepFile file, string productName)
        {
            var data = new StringBuilder();
            int id = 0;
            int Next() => ++id;

            string F(double v) => v.ToString("0.0###########", CultureInfo.InvariantCulture);
            void E(int n, string body) => data.Append('#').Append(n).Append('=').Append(body).Append(";\n");

            var dir = new Dictionary<(double, double, double), int>();
            foreach (var a in Axes)
            {
                int d = Next();
                dir[a] = d;
                E(d, $"DIRECTION('',({F(a.X)},{F(a.Y)},{F(a.Z)}))");
            }

            int mm = Next(); E(mm, "(LENGTH_UNIT()NAMED_UNIT(*)SI_UNIT(.MILLI.,.METRE.))");
            int dimExp = Next(); E(dimExp, "DIMENSIONAL_EXPONENTS(1.,0.,0.,0.,0.,0.,0.)");
            int inchMeas = Next(); E(inchMeas, $"LENGTH_MEASURE_WITH_UNIT(LENGTH_MEASURE(25.4),#{mm})");
            int inchUnit = Next(); E(inchUnit, $"(CONVERSION_BASED_UNIT('INCH',#{inchMeas})LENGTH_UNIT()NAMED_UNIT(#{dimExp}))");
            int radUnit = Next(); E(radUnit, "(NAMED_UNIT(*)PLANE_ANGLE_UNIT()SI_UNIT($,.RADIAN.))");
            int srUnit = Next(); E(srUnit, "(NAMED_UNIT(*)SI_UNIT($,.STERADIAN.)SOLID_ANGLE_UNIT())");
            int uncert = Next(); E(uncert, $"UNCERTAINTY_MEASURE_WITH_UNIT(LENGTH_MEASURE(1.E-05),#{inchUnit},'distance_accuracy_value','edge curve and vertex point accuracy')");
            int context = Next(); E(context,
                $"(GEOMETRIC_REPRESENTATION_CONTEXT(3)GLOBAL_UNCERTAINTY_ASSIGNED_CONTEXT((#{uncert}))GLOBAL_UNIT_ASSIGNED_CONTEXT((#{inchUnit},#{radUnit},#{srUnit}))REPRESENTATION_CONTEXT('',''))");

            int appCtx = Next(); E(appCtx, "APPLICATION_CONTEXT('automotive design')");
            int appProto = Next(); E(appProto, $"APPLICATION_PROTOCOL_DEFINITION('international standard','automotive_design',2000,#{appCtx})");
            int prodCtx = Next(); E(prodCtx, $"PRODUCT_CONTEXT('',#{appCtx},'mechanical')");
            int pdCtx = Next(); E(pdCtx, $"PRODUCT_DEFINITION_CONTEXT('part definition',#{appCtx},'design')");

            int origin = Next(); E(origin, "CARTESIAN_POINT('',(0.,0.,0.))");
            int axisPl = Next(); E(axisPl, $"AXIS2_PLACEMENT_3D('',#{origin},#{dir[(0, 0, 1)]},#{dir[(1, 0, 0)]})");

            (int Pd, int Rep) EmitProduct(string nm, IReadOnlyList<int> bodySolids, bool isAssembly)
            {
                int prod = Next(); E(prod, $"PRODUCT('{Escape(nm)}','{Escape(nm)}','',(#{prodCtx}))");
                int pdfWith = Next(); E(pdfWith, $"PRODUCT_DEFINITION_FORMATION_WITH_SPECIFIED_SOURCE('','',#{prod},.NOT_KNOWN.)");
                int pdId = Next(); E(pdId, $"PRODUCT_DEFINITION('','',#{pdfWith},#{pdCtx})");
                int pdsId = Next(); E(pdsId, $"PRODUCT_DEFINITION_SHAPE('','',#{pdId})");

                var items = new StringBuilder($"#{axisPl}");
                if (bodySolids != null)
                    foreach (int s in bodySolids) items.Append(",#").Append(s);

                int repId = Next();
                E(repId, isAssembly
                    ? $"SHAPE_REPRESENTATION('{Escape(nm)}',({items}),#{context})"
                    : $"ADVANCED_BREP_SHAPE_REPRESENTATION('{Escape(nm)}',({items}),#{context})");
                int sdrId = Next(); E(sdrId, $"SHAPE_DEFINITION_REPRESENTATION(#{pdsId},#{repId})");
                return (pdId, repId);
            }

            // ---- One PRODUCT per StepPart: each becomes its own node in the tree ----
            var leaves = new List<(int Pd, int Rep, string Name)>();
            foreach (var part in file.parts)
            {
                var partSolids = new List<int>();
                foreach (var shape in part.shapes)
                {
                    switch (shape)
                    {
                        case Rectangle r:
                            {
                                partSolids.Add(WriteBox(r, Next, E, F, dir));
                                break;
                            }
                        case Cone cn:
                            {
                                partSolids.Add(WriteFrustum(cn, Next, E, F));
                                break;
                            }
                        case Cylinder cy:
                            {
                                int cylId = WriteCylinder(cy, Next, E, F);
                                if (cylId >= 0) partSolids.Add(cylId);
                                break;
                            }
                        case Extrusion ex:
                            {
                                int exId = WriteExtrusion(ex, Next, E, F);
                                if (exId >= 0) partSolids.Add(exId);
                                break;
                            }
                    }
                }

                // Skip empty parts so the assembly tree shows no hollow nodes.
                if (partSolids.Count == 0) continue;

                var (leafPd, leafRep) = EmitProduct(part.Name, partSolids, isAssembly: false);
                leaves.Add((leafPd, leafRep, part.Name));
            }

            // ---- Root assembly product that owns every part ----
            string name = string.IsNullOrWhiteSpace(productName) ? "SPFA" : productName;
            var (asmPd, asmRep) = EmitProduct(name, null, isAssembly: true);

            // ---- Attach each leaf under the assembly: this is what builds the tree. ----
            // The NEXT_ASSEMBLY_USAGE_OCCURRENCE is the parent->child edge a CAD viewer
            // renders; the CONTEXT_DEPENDENT_SHAPE_REPRESENTATION places the child (here an
            // identity transform, both placements pointing at the shared assembly origin).
            int occNo = 0;
            foreach (var (leafPd, leafRep, leafName) in leaves)
            {
                int nauo = Next(); E(nauo, $"NEXT_ASSEMBLY_USAGE_OCCURRENCE('{++occNo}','{Escape(leafName)}','',#{asmPd},#{leafPd},$)");
                int pdsNauo = Next(); E(pdsNauo, $"PRODUCT_DEFINITION_SHAPE('','',#{nauo})");
                int itd = Next(); E(itd, $"ITEM_DEFINED_TRANSFORMATION('','',#{axisPl},#{axisPl})");
                int relRep = Next(); E(relRep, $"(REPRESENTATION_RELATIONSHIP('','',#{leafRep},#{asmRep})REPRESENTATION_RELATIONSHIP_WITH_TRANSFORMATION(#{itd})SHAPE_REPRESENTATION_RELATIONSHIP())");
                int cdsr = Next(); E(cdsr, $"CONTEXT_DEPENDENT_SHAPE_REPRESENTATION(#{relRep},#{pdsNauo})");
            }

            // ---- Assemble the file ----
            var stp = new StringBuilder();
            stp.Append("ISO-10303-21;\n");
            stp.Append("HEADER;\n");
            stp.Append("FILE_DESCRIPTION(('SPFA fan array - boundary representation'),'2;1');\n");
            stp.Append($"FILE_NAME('{Escape(name)}.stp','',(''),(''),'QPAC SPFA Configurator','QPACSPFA','');\n");
            stp.Append("FILE_SCHEMA(('AUTOMOTIVE_DESIGN { 1 0 10303 214 1 1 1 1 }'));\n");
            stp.Append("ENDSEC;\n");
            stp.Append("DATA;\n");
            stp.Append(data);
            stp.Append("ENDSEC;\n");
            stp.Append("END-ISO-10303-21;\n");

            System.IO.File.WriteAllText(path, stp.ToString());
        }
        private static string Escape(string s) => (s ?? "").Replace("'", "''");
        private static int WriteBox(
            Rectangle b,
            Func<int> Next,
            Action<int, string> E,
            Func<double, string> F,
            Dictionary<(double, double, double), int> dir)
        {
            if (!b.Xf.IsIdentity) return WriteTransformedBox(b, Next, E, F);

            double hx = b.Sx / 2.0, hy = b.Sy / 2.0, hz = b.Sz / 2.0;

            // 8 corners, indexed by (xSign, ySign, zSign)
            //   0:(-,-,-) 1:(+,-,-) 2:(+,+,-) 3:(-,+,-)
            //   4:(-,-,+) 5:(+,-,+) 6:(+,+,+) 7:(-,+,+)
            double[][] c =
            {
                new[] { b.Cx - hx, b.Cy - hy, b.Cz - hz },
                new[] { b.Cx + hx, b.Cy - hy, b.Cz - hz },
                new[] { b.Cx + hx, b.Cy + hy, b.Cz - hz },
                new[] { b.Cx - hx, b.Cy + hy, b.Cz - hz },
                new[] { b.Cx - hx, b.Cy - hy, b.Cz + hz },
                new[] { b.Cx + hx, b.Cy - hy, b.Cz + hz },
                new[] { b.Cx + hx, b.Cy + hy, b.Cz + hz },
                new[] { b.Cx - hx, b.Cy + hy, b.Cz + hz },
            };

            var pt = new int[8];
            var vtx = new int[8];
            for (int i = 0; i < 8; i++)
            {
                pt[i] = Next(); E(pt[i], $"CARTESIAN_POINT('',({F(c[i][0])},{F(c[i][1])},{F(c[i][2])}))");
                vtx[i] = Next(); E(vtx[i], $"VERTEX_POINT('',#{pt[i]})");
            }

            // 12 edges as (startVertex, endVertex, unitDirection, length).
            // Canonical direction is from the lower-numbered traversal to the higher.
            var edgeDefs = new (int A, int B, (double, double, double) D, double L)[]
            {
                (0, 1, (1, 0, 0), b.Sx),  // e0  bottom front
                (1, 2, (0, 1, 0), b.Sy),  // e1
                (2, 3, (-1, 0, 0), b.Sx), // e2
                (3, 0, (0, -1, 0), b.Sy), // e3
                (4, 5, (1, 0, 0), b.Sx),  // e4  top
                (5, 6, (0, 1, 0), b.Sy),  // e5
                (6, 7, (-1, 0, 0), b.Sx), // e6
                (7, 4, (0, -1, 0), b.Sy), // e7
                (0, 4, (0, 0, 1), b.Sz),  // e8  verticals
                (1, 5, (0, 0, 1), b.Sz),  // e9
                (2, 6, (0, 0, 1), b.Sz),  // e10
                (3, 7, (0, 0, 1), b.Sz),  // e11
            };

            var edge = new int[12];
            for (int i = 0; i < 12; i++)
            {
                var (a, bb, d, len) = edgeDefs[i];
                int vec = Next(); E(vec, $"VECTOR('',#{dir[d]},{F(len)})");
                int line = Next(); E(line, $"LINE('',#{pt[a]},#{vec})");
                edge[i] = Next(); E(edge[i], $"EDGE_CURVE('',#{vtx[a]},#{vtx[bb]},#{line},.T.)");
            }

            // 6 faces: each is (4 oriented edges as edgeIndex+sense, planeNormal, refDir, originVertex).
            // Loops are CCW as seen from outside; plane normal points outward; same_sense = .T.
            var faceDefs = new ((int E, bool S)[] Loop, (double, double, double) N, (double, double, double) R, int O)[]
            {
                // z- : 0,3,2,1
                (new[] { (3, false), (2, false), (1, false), (0, false) }, (0, 0, -1), (1, 0, 0), 0),
                // z+ : 4,5,6,7
                (new[] { (4, true), (5, true), (6, true), (7, true) }, (0, 0, 1), (1, 0, 0), 4),
                // y- : 0,1,5,4
                (new[] { (0, true), (9, true), (4, false), (8, false) }, (0, -1, 0), (1, 0, 0), 0),
                // y+ : 2,3,7,6
                (new[] { (2, true), (11, true), (6, false), (10, false) }, (0, 1, 0), (1, 0, 0), 2),
                // x- : 0,4,7,3
                (new[] { (8, true), (7, false), (11, false), (3, true) }, (-1, 0, 0), (0, 1, 0), 0),
                // x+ : 1,2,6,5
                (new[] { (1, true), (10, true), (5, false), (9, false) }, (1, 0, 0), (0, 1, 0), 1),
            };

            var faces = new int[6];
            for (int f = 0; f < 6; f++)
            {
                var (loop, n, r, o) = faceDefs[f];

                var oriented = new int[4];
                for (int k = 0; k < 4; k++)
                {
                    var (ei, sense) = loop[k];
                    oriented[k] = Next();
                    E(oriented[k], $"ORIENTED_EDGE('',*,*,#{edge[ei]},.{(sense ? "T" : "F")}.)");
                }

                int edgeLoop = Next();
                E(edgeLoop, $"EDGE_LOOP('',(#{oriented[0]},#{oriented[1]},#{oriented[2]},#{oriented[3]}))");

                int bound = Next(); E(bound, $"FACE_OUTER_BOUND('',#{edgeLoop},.T.)");

                int placement = Next();
                E(placement, $"AXIS2_PLACEMENT_3D('',#{pt[o]},#{dir[n]},#{dir[r]})");
                int plane = Next(); E(plane, $"PLANE('',#{placement})");

                faces[f] = Next();
                E(faces[f], $"ADVANCED_FACE('',(#{bound}),#{plane},.T.)");
            }

            int shell = Next();
            E(shell, $"CLOSED_SHELL('',(#{faces[0]},#{faces[1]},#{faces[2]},#{faces[3]},#{faces[4]},#{faces[5]}))");

            int solid = Next();
            E(solid, $"MANIFOLD_SOLID_BREP('{Escape(b.Name)}',#{shell})");
            return solid;
        }

        /// <summary>
        /// Emits a rotated box as a MANIFOLD_SOLID_BREP and returns its id. Unlike
        /// <see cref="WriteBox"/> (locked to the world axes), the axis-aligned corners are
        /// pushed through the shape's <see cref="StepShape.Xf"/>, so the box can tilt in any
        /// direction — used for centrifugal impeller blades and any part the caller rotates
        /// with <see cref="StepShapeTransformExtensions.RotateAbout"/>. Each face is
        /// auto-oriented (Newell normal flipped outward) exactly like the frustum writer, so
        /// it imports as a clean solid alongside the axis-aligned boxes.
        /// </summary>
        private static int WriteTransformedBox(
            Rectangle b,
            Func<int> Next,
            Action<int, string> E,
            Func<double, string> F)
        {
            double hx = b.Sx / 2.0, hy = b.Sy / 2.0, hz = b.Sz / 2.0;

            // 8 corners: built axis-aligned about the center, then rotated by Xf. Indexed
            // (xSign,ySign,zSign) with signs in {-1,+1} — same order as WriteBox.
            var signs = new (int X, int Y, int Z)[]
            {
                (-1, -1, -1), (+1, -1, -1), (+1, +1, -1), (-1, +1, -1),
                (-1, -1, +1), (+1, -1, +1), (+1, +1, +1), (-1, +1, +1),
            };
            var center = b.Xf.Apply((b.Cx, b.Cy, b.Cz));
            var pos = new (double X, double Y, double Z)[8];
            var cp = new int[8];
            var vtx = new int[8];
            for (int i = 0; i < 8; i++)
            {
                pos[i] = b.Xf.Apply((b.Cx + signs[i].X * hx, b.Cy + signs[i].Y * hy, b.Cz + signs[i].Z * hz));
                cp[i] = Next(); E(cp[i], $"CARTESIAN_POINT('',({F(pos[i].X)},{F(pos[i].Y)},{F(pos[i].Z)}))");
                vtx[i] = Next(); E(vtx[i], $"VERTEX_POINT('',#{cp[i]})");
            }

            // One EDGE_CURVE per undirected vertex pair, canonical direction low->high.
            var edgeId = new Dictionary<(int, int), int>();
            int Edge(int a2, int b2)
            {
                int lo = Math.Min(a2, b2), hi = Math.Max(a2, b2);
                var key = (lo, hi);
                if (edgeId.TryGetValue(key, out int existing)) return existing;
                var s = pos[lo]; var e = pos[hi];
                double dx = e.X - s.X, dy = e.Y - s.Y, dz = e.Z - s.Z;
                double len = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                int d = Next(); E(d, $"DIRECTION('',({F(dx / len)},{F(dy / len)},{F(dz / len)}))");
                int v = Next(); E(v, $"VECTOR('',#{d},{F(len)})");
                int ln = Next(); E(ln, $"LINE('',#{cp[lo]},#{v})");
                int ec = Next(); E(ec, $"EDGE_CURVE('',#{vtx[lo]},#{vtx[hi]},#{ln},.T.)");
                edgeId[key] = ec;
                return ec;
            }

            int Face(int[] loopArr)
            {
                var loop = new List<int>(loopArr);

                // Newell normal + centroid of the loop polygon.
                double nx = 0, ny = 0, nz = 0, fxc = 0, fyc = 0, fzc = 0;
                for (int k = 0; k < loop.Count; k++)
                {
                    var p = pos[loop[k]];
                    var q = pos[loop[(k + 1) % loop.Count]];
                    nx += (p.Y - q.Y) * (p.Z + q.Z);
                    ny += (p.Z - q.Z) * (p.X + q.X);
                    nz += (p.X - q.X) * (p.Y + q.Y);
                    fxc += p.X; fyc += p.Y; fzc += p.Z;
                }
                fxc /= loop.Count; fyc /= loop.Count; fzc /= loop.Count;

                // Flip loop + normal if the normal points inward (toward the box center).
                if (nx * (fxc - center.X) + ny * (fyc - center.Y) + nz * (fzc - center.Z) < 0)
                {
                    loop.Reverse();
                    nx = -nx; ny = -ny; nz = -nz;
                }
                double nl = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                nx /= nl; ny /= nl; nz /= nl;

                var oe = new List<int>();
                for (int k = 0; k < loop.Count; k++)
                {
                    int a2 = loop[k], b2 = loop[(k + 1) % loop.Count];
                    int ec = Edge(a2, b2);
                    bool sense = a2 < b2;
                    int o = Next(); E(o, $"ORIENTED_EDGE('',*,*,#{ec},.{(sense ? "T" : "F")}.)");
                    oe.Add(o);
                }
                int loopId = Next(); E(loopId, $"EDGE_LOOP('',({string.Join(",", oe.ConvertAll(x => "#" + x))}))");
                int bound = Next(); E(bound, $"FACE_OUTER_BOUND('',#{loopId},.T.)");

                var p0 = pos[loop[0]]; var p1 = pos[loop[1]];
                double rx = p1.X - p0.X, ry = p1.Y - p0.Y, rz = p1.Z - p0.Z;
                double rlen = Math.Sqrt(rx * rx + ry * ry + rz * rz);
                int nd = Next(); E(nd, $"DIRECTION('',({F(nx)},{F(ny)},{F(nz)}))");
                int rd = Next(); E(rd, $"DIRECTION('',({F(rx / rlen)},{F(ry / rlen)},{F(rz / rlen)}))");
                int axp = Next(); E(axp, $"AXIS2_PLACEMENT_3D('',#{cp[loop[0]]},#{nd},#{rd})");
                int pl = Next(); E(pl, $"PLANE('',#{axp})");
                int f = Next(); E(f, $"ADVANCED_FACE('',(#{bound}),#{pl},.T.)");
                return f;
            }

            var faces = new int[6];
            faces[0] = Face(new[] { 0, 1, 2, 3 }); // -local Z
            faces[1] = Face(new[] { 4, 5, 6, 7 }); // +local Z
            faces[2] = Face(new[] { 0, 1, 5, 4 }); // -local Y
            faces[3] = Face(new[] { 3, 2, 6, 7 }); // +local Y
            faces[4] = Face(new[] { 0, 3, 7, 4 }); // -local X
            faces[5] = Face(new[] { 1, 2, 6, 5 }); // +local X

            int shell = Next();
            E(shell, $"CLOSED_SHELL('',(#{faces[0]},#{faces[1]},#{faces[2]},#{faces[3]},#{faces[4]},#{faces[5]}))");
            int sld = Next();
            E(sld, $"MANIFOLD_SOLID_BREP('{Escape(b.Name)}',#{shell})");
            return sld;
        }

        private static int WriteFrustum(
            Cone c,
            Func<int> Next,
            Action<int, string> E,
            Func<double, string> F)
        {
            int N = Math.Max(8, c.Sides);
            double zb = c.Cz;
            double zt = c.Cz + c.Height;
            double rb = Math.Max(1e-4, c.BottomRadius);
            double rt = Math.Max(1e-4, c.TopRadius);

            var pos = new (double X, double Y, double Z)[2 * N];
            var cp = new int[2 * N];
            var vtx = new int[2 * N];
            for (int i = 0; i < N; i++)
            {
                double a = 2.0 * Math.PI * i / N;
                double cos = Math.Cos(a), sin = Math.Sin(a);
                pos[i] = c.Xf.Apply((c.Cx + rb * cos, c.Cy + rb * sin, zb));
                pos[N + i] = c.Xf.Apply((c.Cx + rt * cos, c.Cy + rt * sin, zt));
            }
            for (int i = 0; i < 2 * N; i++)
            {
                cp[i] = Next(); E(cp[i], $"CARTESIAN_POINT('',({F(pos[i].X)},{F(pos[i].Y)},{F(pos[i].Z)}))");
                vtx[i] = Next(); E(vtx[i], $"VERTEX_POINT('',#{cp[i]})");
            }

            var edgeId = new Dictionary<(int, int), int>();
            int Edge(int a, int b)
            {
                int lo = Math.Min(a, b), hi = Math.Max(a, b);
                var key = (lo, hi);
                if (edgeId.TryGetValue(key, out int existing)) return existing;
                var s = pos[lo]; var e = pos[hi];
                double dx = e.X - s.X, dy = e.Y - s.Y, dz = e.Z - s.Z;
                double L = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                int d = Next(); E(d, $"DIRECTION('',({F(dx / L)},{F(dy / L)},{F(dz / L)}))");
                int v = Next(); E(v, $"VECTOR('',#{d},{F(L)})");
                int ln = Next(); E(ln, $"LINE('',#{cp[lo]},#{v})");
                int ec = Next(); E(ec, $"EDGE_CURVE('',#{vtx[lo]},#{vtx[hi]},#{ln},.T.)");
                edgeId[key] = ec;
                return ec;
            }

            var frustumCenter = c.Xf.Apply((c.Cx, c.Cy, 0.5 * (zb + zt)));

            int Face(List<int> loop)
            {
                double nx = 0, ny = 0, nz = 0;
                double fxc = 0, fyc = 0, fzc = 0;
                for (int k = 0; k < loop.Count; k++)
                {
                    var p = pos[loop[k]];
                    var q = pos[loop[(k + 1) % loop.Count]];
                    nx += (p.Y - q.Y) * (p.Z + q.Z);
                    ny += (p.Z - q.Z) * (p.X + q.X);
                    nz += (p.X - q.X) * (p.Y + q.Y);
                    fxc += p.X; fyc += p.Y; fzc += p.Z;
                }
                fxc /= loop.Count; fyc /= loop.Count; fzc /= loop.Count;

                // Flip loop + normal if the normal points toward the axis instead of outward.
                if (nx * (fxc - frustumCenter.X) + ny * (fyc - frustumCenter.Y) + nz * (fzc - frustumCenter.Z) < 0)
                {
                    loop.Reverse();
                    nx = -nx; ny = -ny; nz = -nz;
                }
                double nl = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                nx /= nl; ny /= nl; nz /= nl;

                var oe = new List<int>();
                for (int k = 0; k < loop.Count; k++)
                {
                    int a = loop[k], b = loop[(k + 1) % loop.Count];
                    int ec = Edge(a, b);
                    bool sense = a < b; // .T. when traversal matches the canonical low->high direction
                    int o = Next(); E(o, $"ORIENTED_EDGE('',*,*,#{ec},.{(sense ? "T" : "F")}.)");
                    oe.Add(o);
                }
                int loopId = Next(); E(loopId, $"EDGE_LOOP('',({string.Join(",", oe.ConvertAll(x => "#" + x))}))");
                int bound = Next(); E(bound, $"FACE_OUTER_BOUND('',#{loopId},.T.)");

                // Plane: origin at first loop vertex, normal outward, ref dir along the first edge.
                var p0 = pos[loop[0]]; var p1 = pos[loop[1]];
                double rx = p1.X - p0.X, ry = p1.Y - p0.Y, rz = p1.Z - p0.Z;
                double rlen = Math.Sqrt(rx * rx + ry * ry + rz * rz);
                int nd = Next(); E(nd, $"DIRECTION('',({F(nx)},{F(ny)},{F(nz)}))");
                int rd = Next(); E(rd, $"DIRECTION('',({F(rx / rlen)},{F(ry / rlen)},{F(rz / rlen)}))");
                int ax = Next(); E(ax, $"AXIS2_PLACEMENT_3D('',#{cp[loop[0]]},#{nd},#{rd})");
                int pl = Next(); E(pl, $"PLANE('',#{ax})");
                int f = Next(); E(f, $"ADVANCED_FACE('',(#{bound}),#{pl},.T.)");
                return f;
            }

            var faces = new List<int>();
            for (int i = 0; i < N; i++)
            {
                int j = (i + 1) % N;
                faces.Add(Face(new List<int> { i, j, N + j, N + i })); // side facet
            }
            var bottom = new List<int>();
            for (int i = 0; i < N; i++) bottom.Add(i);
            faces.Add(Face(bottom));
            var top = new List<int>();
            for (int i = 0; i < N; i++) top.Add(N + i);
            faces.Add(Face(top));

            int shell = Next();
            E(shell, $"CLOSED_SHELL('',({string.Join(",", faces.ConvertAll(x => "#" + x))}))");
            int sld = Next();
            E(sld, $"MANIFOLD_SOLID_BREP('{Escape(c.Name)}',#{shell})");
            return sld;
        }

        /// <summary>
        /// Emits a faceted cylinder swept between two arbitrary 3D endpoints (unlike
        /// <see cref="WriteFrustum"/>, which is locked to the +Z axis) as a
        /// MANIFOLD_SOLID_BREP and returns its id — used for in-plane wire-harness runs
        /// that head off in any direction. Returns -1 for a degenerate (zero-length)
        /// axis so the caller can skip it. Built from <c>Sides</c> planar side facets
        /// plus two planar end caps, each auto-oriented so its normal points outward.
        /// </summary>
        private static int WriteCylinder(
            Cylinder c,
            Func<int> Next,
            Action<int, string> E,
            Func<double, string> F)
        {
            int N = Math.Max(8, c.Sides);

            // Axis from start to end.
            double axx = c.X2 - c.X1, axy = c.Y2 - c.Y1, axz = c.Z2 - c.Z1;
            double L = Math.Sqrt(axx * axx + axy * axy + axz * axz);
            if (L < 1e-9) return -1; // degenerate — nothing to sweep
            double ux = axx / L, uy = axy / L, uz = axz / L;

            // Orthonormal basis (u, v, w) around the axis. Pick a helper axis that is
            // not near-parallel to u so the cross product is well conditioned.
            double hx = 0, hy = 0, hz = 1;
            if (Math.Abs(uz) > 0.9) { hx = 1; hy = 0; hz = 0; }
            double vx = uy * hz - uz * hy;
            double vy = uz * hx - ux * hz;
            double vz = ux * hy - uy * hx;
            double vl = Math.Sqrt(vx * vx + vy * vy + vz * vz);
            vx /= vl; vy /= vl; vz /= vl;
            double wx = uy * vz - uz * vy;
            double wy = uz * vx - ux * vz;
            double wz = ux * vy - uy * vx;

            double R = Math.Max(1e-4, c.Radius);

            // Ring vertices: 0..N-1 = start ring, N..2N-1 = end ring.
            var pos = new (double X, double Y, double Z)[2 * N];
            var cp = new int[2 * N];
            var vtx = new int[2 * N];
            for (int i = 0; i < N; i++)
            {
                double a = 2.0 * Math.PI * i / N;
                double cs = Math.Cos(a), sn = Math.Sin(a);
                double rx = R * (cs * vx + sn * wx);
                double ry = R * (cs * vy + sn * wy);
                double rz = R * (cs * vz + sn * wz);
                pos[i] = c.Xf.Apply((c.X1 + rx, c.Y1 + ry, c.Z1 + rz));
                pos[N + i] = c.Xf.Apply((c.X2 + rx, c.Y2 + ry, c.Z2 + rz));
            }
            for (int i = 0; i < 2 * N; i++)
            {
                cp[i] = Next(); E(cp[i], $"CARTESIAN_POINT('',({F(pos[i].X)},{F(pos[i].Y)},{F(pos[i].Z)}))");
                vtx[i] = Next(); E(vtx[i], $"VERTEX_POINT('',#{cp[i]})");
            }

            // Solid centroid (axis midpoint, rotated) — used to flip each face outward.
            var (cX, cY, cZ) = c.Xf.Apply((0.5 * (c.X1 + c.X2), 0.5 * (c.Y1 + c.Y2), 0.5 * (c.Z1 + c.Z2)));

            // One EDGE_CURVE per undirected vertex pair, canonical direction low->high.
            var edgeId = new Dictionary<(int, int), int>();
            int Edge(int a, int b)
            {
                int lo = Math.Min(a, b), hi = Math.Max(a, b);
                var key = (lo, hi);
                if (edgeId.TryGetValue(key, out int existing)) return existing;
                var s = pos[lo]; var e = pos[hi];
                double dx = e.X - s.X, dy = e.Y - s.Y, dz = e.Z - s.Z;
                double len = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                int d = Next(); E(d, $"DIRECTION('',({F(dx / len)},{F(dy / len)},{F(dz / len)}))");
                int v = Next(); E(v, $"VECTOR('',#{d},{F(len)})");
                int ln = Next(); E(ln, $"LINE('',#{cp[lo]},#{v})");
                int ec = Next(); E(ec, $"EDGE_CURVE('',#{vtx[lo]},#{vtx[hi]},#{ln},.T.)");
                edgeId[key] = ec;
                return ec;
            }

            int Face(List<int> loop)
            {
                // Newell normal + centroid of the loop polygon.
                double nx = 0, ny = 0, nz = 0;
                double fxc = 0, fyc = 0, fzc = 0;
                for (int k = 0; k < loop.Count; k++)
                {
                    var p = pos[loop[k]];
                    var q = pos[loop[(k + 1) % loop.Count]];
                    nx += (p.Y - q.Y) * (p.Z + q.Z);
                    ny += (p.Z - q.Z) * (p.X + q.X);
                    nz += (p.X - q.X) * (p.Y + q.Y);
                    fxc += p.X; fyc += p.Y; fzc += p.Z;
                }
                fxc /= loop.Count; fyc /= loop.Count; fzc /= loop.Count;

                // Flip loop + normal if the normal points inward (toward the axis midpoint).
                if (nx * (fxc - cX) + ny * (fyc - cY) + nz * (fzc - cZ) < 0)
                {
                    loop.Reverse();
                    nx = -nx; ny = -ny; nz = -nz;
                }
                double nl = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                nx /= nl; ny /= nl; nz /= nl;

                var oe = new List<int>();
                for (int k = 0; k < loop.Count; k++)
                {
                    int a = loop[k], b = loop[(k + 1) % loop.Count];
                    int ec = Edge(a, b);
                    bool sense = a < b; // .T. when traversal matches canonical low->high
                    int o = Next(); E(o, $"ORIENTED_EDGE('',*,*,#{ec},.{(sense ? "T" : "F")}.)");
                    oe.Add(o);
                }
                int loopId = Next(); E(loopId, $"EDGE_LOOP('',({string.Join(",", oe.ConvertAll(x => "#" + x))}))");
                int bound = Next(); E(bound, $"FACE_OUTER_BOUND('',#{loopId},.T.)");

                // Plane: origin at first loop vertex, normal outward, ref dir along first edge.
                var p0 = pos[loop[0]]; var p1 = pos[loop[1]];
                double rx = p1.X - p0.X, ry = p1.Y - p0.Y, rz = p1.Z - p0.Z;
                double rlen = Math.Sqrt(rx * rx + ry * ry + rz * rz);
                int nd = Next(); E(nd, $"DIRECTION('',({F(nx)},{F(ny)},{F(nz)}))");
                int rd = Next(); E(rd, $"DIRECTION('',({F(rx / rlen)},{F(ry / rlen)},{F(rz / rlen)}))");
                int ax = Next(); E(ax, $"AXIS2_PLACEMENT_3D('',#{cp[loop[0]]},#{nd},#{rd})");
                int pl = Next(); E(pl, $"PLANE('',#{ax})");
                int f = Next(); E(f, $"ADVANCED_FACE('',(#{bound}),#{pl},.T.)");
                return f;
            }

            var faces = new List<int>();
            for (int i = 0; i < N; i++)
            {
                int j = (i + 1) % N;
                faces.Add(Face(new List<int> { i, j, N + j, N + i })); // side facet
            }
            var startCap = new List<int>();
            for (int i = 0; i < N; i++) startCap.Add(i);
            faces.Add(Face(startCap));
            var endCap = new List<int>();
            for (int i = 0; i < N; i++) endCap.Add(N + i);
            faces.Add(Face(endCap));

            int shell = Next();
            E(shell, $"CLOSED_SHELL('',({string.Join(",", faces.ConvertAll(x => "#" + x))}))");
            int sld = Next();
            E(sld, $"MANIFOLD_SOLID_BREP('{Escape(c.Name)}',#{shell})");
            return sld;
        }

        /// <summary>
        /// Emits an extruded plate with extruded cuts (<see cref="Extrusion"/>) as a
        /// MANIFOLD_SOLID_BREP and returns its id (-1 if degenerate). The two cap faces
        /// carry the outer loop as their FACE_OUTER_BOUND and each cut as an inner
        /// FACE_BOUND — STEP's native way to hold a hole in a face — and the cut walls are
        /// emitted as side quads so the removed material is a real through-pocket.
        /// </summary>
        private static int WriteExtrusion(
            Extrusion ex,
            Func<int> Next,
            Action<int, string> E,
            Func<double, string> F)
        {
            if (ex.Outer == null || ex.Outer.Count < 3 || Math.Abs(ex.Depth) < 1e-9) return -1;

            // Orthonormal frame; W is the sweep direction. Origin and basis are pushed
            // through Xf so the whole extrusion (and its cuts) rotate together. A rotation
            // is orthonormal, so Cross(R·u, R·v) == R·(u×v) and the frame stays right-handed.
            var origin = ex.Xf.Apply(ex.Origin);
            var u = ex.Xf.ApplyDirection(Normalize(ex.U));
            var v = ex.Xf.ApplyDirection(Normalize(ex.V));
            var w = Cross(u, v);

            // All loops in one flat list: index 0 = outer, 1.. = extruded cuts.
            var loops = new List<List<(double U, double V)>> { ex.Outer };
            loops.AddRange(ex.Cuts);
            int L = loops.Count;

            // Per loop, the front (t=0) and back (t=Depth) rings of vertex-point and
            // cartesian-point ids, plus world positions for direction/normal math.
            var fVtx = new int[L][]; var bVtx = new int[L][];
            var fPt = new int[L][]; var bPt = new int[L][];
            var pos = new Dictionary<int, (double X, double Y, double Z)>();

            (double X, double Y, double Z) World(double pu, double pv, double t) =>
                (origin.X + pu * u.X + pv * v.X + t * w.X,
                 origin.Y + pu * u.Y + pv * v.Y + t * w.Y,
                 origin.Z + pu * u.Z + pv * v.Z + t * w.Z);

            int Emit(string body) { int id = Next(); E(id, body); return id; }

            for (int l = 0; l < L; l++)
            {
                int n = loops[l].Count;
                fVtx[l] = new int[n]; bVtx[l] = new int[n];
                fPt[l] = new int[n]; bPt[l] = new int[n];
                for (int i = 0; i < n; i++)
                {
                    var pf = World(loops[l][i].U, loops[l][i].V, 0);
                    var pb = World(loops[l][i].U, loops[l][i].V, ex.Depth);
                    fPt[l][i] = Emit($"CARTESIAN_POINT('',({F(pf.X)},{F(pf.Y)},{F(pf.Z)}))");
                    bPt[l][i] = Emit($"CARTESIAN_POINT('',({F(pb.X)},{F(pb.Y)},{F(pb.Z)}))");
                    fVtx[l][i] = Emit($"VERTEX_POINT('',#{fPt[l][i]})");
                    bVtx[l][i] = Emit($"VERTEX_POINT('',#{bPt[l][i]})");
                    pos[fVtx[l][i]] = pf; pos[bVtx[l][i]] = pb;
                }
            }

            // One EDGE_CURVE per undirected vertex pair (canonical low->high id), cached
            // so a shared edge is emitted once and referenced by exactly two faces.
            var edgeId = new Dictionary<(int, int), int>();
            int Edge(int va, int ca, int vb, int cb)
            {
                bool flip = va > vb;
                int v1 = flip ? vb : va, c1 = flip ? cb : ca, v2 = flip ? va : vb;
                var key = (v1, v2);
                if (edgeId.TryGetValue(key, out int hit)) return hit;
                var s = pos[v1]; var e = pos[v2];
                double dx = e.X - s.X, dy = e.Y - s.Y, dz = e.Z - s.Z;
                double len = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                int d = Emit($"DIRECTION('',({F(dx / len)},{F(dy / len)},{F(dz / len)}))");
                int vc = Emit($"VECTOR('',#{d},{F(len)})");
                int ln = Emit($"LINE('',#{c1},#{vc})");
                int ec = Emit($"EDGE_CURVE('',#{v1},#{v2},#{ln},.T.)");
                edgeId[key] = ec;
                return ec;
            }

            // EDGE_LOOP over a vertex cycle, reversed so it winds CCW about targetNormal.
            int Loop(int[] vids, int[] cids, (double X, double Y, double Z) targetNormal)
            {
                var order = Enumerable.Range(0, vids.Length).ToList();
                double nx = 0, ny = 0, nz = 0;                 // Newell normal
                for (int k = 0; k < order.Count; k++)
                {
                    var p = pos[vids[order[k]]];
                    var q = pos[vids[order[(k + 1) % order.Count]]];
                    nx += (p.Y - q.Y) * (p.Z + q.Z);
                    ny += (p.Z - q.Z) * (p.X + q.X);
                    nz += (p.X - q.X) * (p.Y + q.Y);
                }
                if (nx * targetNormal.X + ny * targetNormal.Y + nz * targetNormal.Z < 0)
                    order.Reverse();

                var oe = new List<int>();
                for (int k = 0; k < order.Count; k++)
                {
                    int ia = order[k], ib = order[(k + 1) % order.Count];
                    int ec = Edge(vids[ia], cids[ia], vids[ib], cids[ib]);
                    oe.Add(Emit($"ORIENTED_EDGE('',*,*,#{ec},.{(vids[ia] < vids[ib] ? "T" : "F")}.)"));
                }
                return Emit($"EDGE_LOOP('',({string.Join(",", oe.ConvertAll(x => "#" + x))}))");
            }

            var faces = new List<int>();
            int refDir = Emit($"DIRECTION('',({F(u.X)},{F(u.Y)},{F(u.Z)}))");

            // --- Two cap faces: outer loop as FACE_OUTER_BOUND, each cut as an inner FACE_BOUND ---
            void Cap(bool back)
            {
                var vtx = back ? bVtx : fVtx;
                var pts = back ? bPt : fPt;
                var n = back ? w : (-w.X, -w.Y, -w.Z);   // outward face normal

                var bounds = new List<string>();
                int outer = Loop(vtx[0], pts[0], n);
                bounds.Add($"#{Emit($"FACE_OUTER_BOUND('',#{outer},.T.)")}");
                for (int l = 1; l < L; l++)
                {
                    // A cut's bound winds opposite the face normal.
                    int hole = Loop(vtx[l], pts[l], (-n.Item1, -n.Item2, -n.Item3));
                    bounds.Add($"#{Emit($"FACE_BOUND('',#{hole},.T.)")}");
                }
                int nd = Emit($"DIRECTION('',({F(n.Item1)},{F(n.Item2)},{F(n.Item3)}))");
                int ax = Emit($"AXIS2_PLACEMENT_3D('',#{pts[0][0]},#{nd},#{refDir})");
                int pl = Emit($"PLANE('',#{ax})");
                faces.Add(Emit($"ADVANCED_FACE('',({string.Join(",", bounds)}),#{pl},.T.)"));
            }
            Cap(back: false);
            Cap(back: true);

            // --- Side walls for every loop (outer + each cut) ---
            for (int l = 0; l < L; l++)
            {
                int n = loops[l].Count;
                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n;
                    // Quad front i -> front j -> back j -> back i. edgeDir × W is the in-plane
                    // outward normal for a CCW outer loop; a cut winds CW so the same formula
                    // points into the pocket — outward from the material in both cases.
                    var pi = pos[fVtx[l][i]]; var pj = pos[fVtx[l][j]];
                    var edgeDir = Normalize((pj.X - pi.X, pj.Y - pi.Y, pj.Z - pi.Z));
                    var nrm = Cross(edgeDir, w);
                    int loop = Loop(
                        new[] { fVtx[l][i], fVtx[l][j], bVtx[l][j], bVtx[l][i] },
                        new[] { fPt[l][i], fPt[l][j], bPt[l][j], bPt[l][i] },
                        nrm);
                    int ob = Emit($"FACE_OUTER_BOUND('',#{loop},.T.)");
                    int nd = Emit($"DIRECTION('',({F(nrm.X)},{F(nrm.Y)},{F(nrm.Z)}))");
                    int rd = Emit($"DIRECTION('',({F(edgeDir.X)},{F(edgeDir.Y)},{F(edgeDir.Z)}))");
                    int ax = Emit($"AXIS2_PLACEMENT_3D('',#{fPt[l][i]},#{nd},#{rd})");
                    int pl = Emit($"PLANE('',#{ax})");
                    faces.Add(Emit($"ADVANCED_FACE('',(#{ob}),#{pl},.T.)"));
                }
            }

            int shell = Emit($"CLOSED_SHELL('',({string.Join(",", faces.ConvertAll(x => "#" + x))}))");
            return Emit($"MANIFOLD_SOLID_BREP('{Escape(ex.Name)}',#{shell})");
        }
        private static (double X, double Y, double Z) Cross((double X, double Y, double Z) a, (double X, double Y, double Z) b)
            => (a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        private static (double X, double Y, double Z) Normalize((double X, double Y, double Z) a)
        {
            double l = Math.Sqrt(a.X * a.X + a.Y * a.Y + a.Z * a.Z);
            if (l < 1e-12) return (0, 0, 0);
            return (a.X / l, a.Y / l, a.Z / l);
        }
    }
}
