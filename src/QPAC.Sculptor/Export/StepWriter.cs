using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace QPAC.Sculptor
{
    // Emits an AP214 exchange file: one PRODUCT per part, each holding faceted MANIFOLD_SOLID_BREPs,
    // under a root assembly product.
    internal sealed class StepWriter
    {
        private readonly StringBuilder data = new StringBuilder();
        private readonly Dictionary<string, int> directions = new Dictionary<string, int>();
        private readonly List<(int Solid, StepColor Color)> styled = new List<(int Solid, StepColor Color)>();
        private int id;
        private double scale = 1;

        private int Emit(string body)
        {
            int n = ++id;
            data.Append('#').Append(n).Append('=').Append(body).Append(";\n");
            return n;
        }

        private static string F(double v)
        {
            if (Math.Abs(v) < 1e-14) v = 0;
            return v.ToString("0.0###########", CultureInfo.InvariantCulture);
        }

        private string Len(double v) => F(v * scale);

        private string Pt((double X, double Y, double Z) p) => $"({Len(p.X)},{Len(p.Y)},{Len(p.Z)})";

        private int Direction((double X, double Y, double Z) d)
        {
            var n = Vec.Normalize(d);
            string key = $"({F(n.X)},{F(n.Y)},{F(n.Z)})";
            if (!directions.TryGetValue(key, out int existing))
                directions[key] = existing = Emit($"DIRECTION('',{key})");
            return existing;
        }

        internal static string Escape(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s ?? "")
            {
                if (c == '\'') sb.Append("''");
                else if (c == '\\') sb.Append("\\\\");
                else if (c < 32 || c > 126) sb.Append("\\X2\\").Append(((int)c).ToString("X4")).Append("\\X0\\");
                else sb.Append(c);
            }
            return sb.ToString();
        }

        public string Write(StepFile file, string productName, StepWriteOptions options)
        {
            options = options ?? StepWriteOptions.Default;
            var units = options.ExportUnits ?? file.Units;
            scale = LengthUnits.Factor(file.Units, units);
            string name = string.IsNullOrWhiteSpace(productName) ? "Model" : productName;

            int lengthUnit = EmitLengthUnit(units);
            int radUnit = Emit("(NAMED_UNIT(*)PLANE_ANGLE_UNIT()SI_UNIT($,.RADIAN.))");
            int srUnit = Emit("(NAMED_UNIT(*)SI_UNIT($,.STERADIAN.)SOLID_ANGLE_UNIT())");
            int uncert = Emit($"UNCERTAINTY_MEASURE_WITH_UNIT(LENGTH_MEASURE(1.E-05),#{lengthUnit},'distance_accuracy_value','edge curve and vertex point accuracy')");
            int context = Emit($"(GEOMETRIC_REPRESENTATION_CONTEXT(3)GLOBAL_UNCERTAINTY_ASSIGNED_CONTEXT((#{uncert}))GLOBAL_UNIT_ASSIGNED_CONTEXT((#{lengthUnit},#{radUnit},#{srUnit}))REPRESENTATION_CONTEXT('',''))");

            int appCtx = Emit("APPLICATION_CONTEXT('automotive design')");
            Emit($"APPLICATION_PROTOCOL_DEFINITION('international standard','automotive_design',2000,#{appCtx})");
            int prodCtx = Emit($"PRODUCT_CONTEXT('',#{appCtx},'mechanical')");
            int pdCtx = Emit($"PRODUCT_DEFINITION_CONTEXT('part definition',#{appCtx},'design')");

            int origin = Emit("CARTESIAN_POINT('',(0.,0.,0.))");
            int axisPl = Emit($"AXIS2_PLACEMENT_3D('',#{origin},#{Direction((0, 0, 1))},#{Direction((1, 0, 0))})");

            (int Pd, int Rep) Product(string nm, IEnumerable<int> items, bool assembly)
            {
                int prod = Emit($"PRODUCT('{Escape(nm)}','{Escape(nm)}','',(#{prodCtx}))");
                Emit($"PRODUCT_RELATED_PRODUCT_CATEGORY('part',$,(#{prod}))");
                int pdf = Emit($"PRODUCT_DEFINITION_FORMATION_WITH_SPECIFIED_SOURCE('','',#{prod},.NOT_KNOWN.)");
                int pd = Emit($"PRODUCT_DEFINITION('design','',#{pdf},#{pdCtx})");
                int pds = Emit($"PRODUCT_DEFINITION_SHAPE('','',#{pd})");
                string list = string.Join(",", new[] { axisPl }.Concat(items).Select(x => "#" + x));
                int rep = Emit(assembly
                    ? $"SHAPE_REPRESENTATION('{Escape(nm)}',({list}),#{context})"
                    : $"ADVANCED_BREP_SHAPE_REPRESENTATION('{Escape(nm)}',({list}),#{context})");
                Emit($"SHAPE_DEFINITION_REPRESENTATION(#{pds},#{rep})");
                return (pd, rep);
            }

            var definitions = new Dictionary<StepPart, (int Pd, int Rep)>();
            (int Pd, int Rep)? Define(StepPart part)
            {
                if (definitions.TryGetValue(part, out var known)) return known;
                var solids = new List<int>();
                foreach (var shape in part.shapes)
                {
                    Polyhedron poly;
                    try { poly = shape.ToPolyhedron(); }
                    catch (ArgumentException ex)
                    {
                        if (options.SkipInvalidShapes) { options.SkippedShapes.Add($"{part.Name}: {ex.Message}"); continue; }
                        throw new InvalidOperationException($"Part '{part.Name}': {ex.Message}", ex);
                    }
                    if (poly.Faces.Count == 0) continue;
                    if (options.ValidateSolids)
                    {
                        var issues = poly.Validate();
                        if (issues.Count > 0)
                        {
                            string msg = $"Part '{part.Name}', shape '{shape.ShapeName}': {issues[0]}";
                            if (options.SkipInvalidShapes) { options.SkippedShapes.Add(msg); continue; }
                            throw new InvalidOperationException(msg);
                        }
                    }
                    int sid = WriteSolid(poly, shape.ShapeName);
                    solids.Add(sid);
                    var color = part.ColorOf(shape) ?? options.DefaultColor;
                    if (options.IncludeColors && color.HasValue) styled.Add((sid, color.Value));
                }
                if (solids.Count == 0) return null;
                var def = Product(part.Name, solids, assembly: false);
                definitions[part] = def;
                return def;
            }

            var occurrences = new List<((int Pd, int Rep) Def, string Name, int Placement)>();
            foreach (var part in file.parts)
            {
                var def = Define(part);
                if (def.HasValue) occurrences.Add((def.Value, part.Name, axisPl));
            }
            foreach (var inst in file.instances)
            {
                if (!inst.Placement.IsRigid)
                    throw new InvalidOperationException($"Instance '{inst.Name}': a placement must be a pure rotation + translation (no scale or mirror). Transform the part's shapes instead.");
                var def = Define(inst.Part);
                if (!def.HasValue) continue;
                var p = inst.Placement;
                int loc = Emit($"CARTESIAN_POINT('',{Pt((p.Tx, p.Ty, p.Tz))})");
                int ax = Emit($"AXIS2_PLACEMENT_3D('',#{loc},#{Direction(p.ApplyDirection((0, 0, 1)))},#{Direction(p.ApplyDirection((1, 0, 0)))})");
                occurrences.Add((def.Value, inst.Name, ax));
            }

            var placements = occurrences.Select(o => o.Placement).Where(x => x != axisPl).Distinct();
            var (asmPd, asmRep) = Product(name, placements, assembly: true);

            int occNo = 0;
            foreach (var (def, occName, placement) in occurrences)
            {
                int nauo = Emit($"NEXT_ASSEMBLY_USAGE_OCCURRENCE('{++occNo}','{Escape(occName)}','',#{asmPd},#{def.Pd},$)");
                int pdsNauo = Emit($"PRODUCT_DEFINITION_SHAPE('','',#{nauo})");
                int itd = Emit($"ITEM_DEFINED_TRANSFORMATION('','',#{axisPl},#{placement})");
                int rel = Emit($"(REPRESENTATION_RELATIONSHIP('','',#{def.Rep},#{asmRep})REPRESENTATION_RELATIONSHIP_WITH_TRANSFORMATION(#{itd})SHAPE_REPRESENTATION_RELATIONSHIP())");
                Emit($"CONTEXT_DEPENDENT_SHAPE_REPRESENTATION(#{rel},#{pdsNauo})");
            }

            WriteStyles(context);

            string unitName = UnitLabel(units);
            string description = string.IsNullOrWhiteSpace(file.Description) ? $"{name} - boundary representation" : file.Description;
            var stp = new StringBuilder();
            stp.Append("ISO-10303-21;\n");
            stp.Append("HEADER;\n");
            string plural = units == LengthUnit.Inch ? "inches" : units == LengthUnit.Foot ? "feet" : unitName + "s";
            string basis = units == LengthUnit.Inch || units == LengthUnit.Foot
                ? $" STEP defines imperial units relative to SI, so a MILLIMETRE unit also appears below only as the base of the {unitName.ToUpperInvariant()} conversion."
                : "";
            stp.Append($"/* Length unit: {unitName.ToUpperInvariant()}. Every coordinate in this file is in {plural}.{basis} */\n");
            stp.Append($"FILE_DESCRIPTION(('{Escape(description)}','Length unit: {unitName}'),'2;1');\n");
            stp.Append($"FILE_NAME('{Escape(name)}.stp','{DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)}',('{Escape(file.Author)}'),('{Escape(file.Organization)}'),'QPAC.Sculptor','{Escape(options.OriginatingSystem)}','');\n");
            stp.Append("FILE_SCHEMA(('AUTOMOTIVE_DESIGN { 1 0 10303 214 1 1 1 1 }'));\n");
            stp.Append("ENDSEC;\n");
            stp.Append("DATA;\n");
            stp.Append(data);
            stp.Append("ENDSEC;\n");
            stp.Append("END-ISO-10303-21;\n");
            return stp.ToString();
        }

        private static string UnitLabel(LengthUnit u)
        {
            switch (u)
            {
                case LengthUnit.Inch: return "inch";
                case LengthUnit.Foot: return "foot";
                case LengthUnit.Centimeter: return "centimeter";
                case LengthUnit.Meter: return "meter";
                default: return "millimeter";
            }
        }

        private int EmitLengthUnit(LengthUnit u)
        {
            switch (u)
            {
                case LengthUnit.Millimeter: return Emit("(LENGTH_UNIT()NAMED_UNIT(*)SI_UNIT(.MILLI.,.METRE.))");
                case LengthUnit.Centimeter: return Emit("(LENGTH_UNIT()NAMED_UNIT(*)SI_UNIT(.CENTI.,.METRE.))");
                case LengthUnit.Meter: return Emit("(LENGTH_UNIT()NAMED_UNIT(*)SI_UNIT($,.METRE.))");
                default:
                    string label = u == LengthUnit.Foot ? "FOOT" : "INCH";
                    int mm = Emit("(LENGTH_UNIT()NAMED_UNIT(*)SI_UNIT(.MILLI.,.METRE.))");
                    int dim = Emit("DIMENSIONAL_EXPONENTS(1.,0.,0.,0.,0.,0.,0.)");
                    int meas = Emit($"LENGTH_MEASURE_WITH_UNIT(LENGTH_MEASURE({F(LengthUnits.MillimetersPer(u))}),#{mm})");
                    return Emit($"(CONVERSION_BASED_UNIT('{label}',#{meas})LENGTH_UNIT()NAMED_UNIT(#{dim}))");
            }
        }

        private int WriteSolid(Polyhedron poly, string name)
        {
            var pointId = new Dictionary<int, int>();
            var vertexId = new Dictionary<int, int>();
            int Vertex(int v)
            {
                if (vertexId.TryGetValue(v, out int known)) return known;
                int cp = Emit($"CARTESIAN_POINT('',{Pt(poly.Vertices[v])})");
                pointId[v] = cp;
                return vertexId[v] = Emit($"VERTEX_POINT('',#{cp})");
            }

            var edges = new Dictionary<(int, int), int>();
            int Edge(int a, int b)
            {
                int lo = Math.Min(a, b), hi = Math.Max(a, b);
                if (edges.TryGetValue((lo, hi), out int known)) return known;
                int va = Vertex(lo), vb = Vertex(hi);
                var d = Vec.Sub(poly.Vertices[hi], poly.Vertices[lo]);
                int vec = Emit($"VECTOR('',#{Direction(d)},{Len(Vec.Length(d))})");
                int line = Emit($"LINE('',#{pointId[lo]},#{vec})");
                return edges[(lo, hi)] = Emit($"EDGE_CURVE('',#{va},#{vb},#{line},.T.)");
            }

            int Loop(int[] loop)
            {
                var oriented = new List<int>(loop.Length);
                for (int k = 0; k < loop.Length; k++)
                {
                    int a = loop[k], b = loop[(k + 1) % loop.Length];
                    int ec = Edge(a, b);
                    oriented.Add(Emit($"ORIENTED_EDGE('',*,*,#{ec},.{(a < b ? "T" : "F")}.)"));
                }
                return Emit($"EDGE_LOOP('',({string.Join(",", oriented.Select(x => "#" + x))}))");
            }

            var faces = new List<int>(poly.Faces.Count);
            foreach (var f in poly.Faces)
            {
                var bounds = new List<int> { Emit($"FACE_OUTER_BOUND('',#{Loop(f.Outer)},.T.)") };
                foreach (var h in f.Holes) bounds.Add(Emit($"FACE_BOUND('',#{Loop(h)},.T.)"));

                var n = poly.FaceNormal(f);
                var p0 = poly.Vertices[f.Outer[0]];
                var e = Vec.Sub(poly.Vertices[f.Outer[1]], p0);
                var r = Vec.Normalize(Vec.Sub(e, Vec.Scale(n, Vec.Dot(e, n))));
                if (r == (0, 0, 0)) r = Vec.AnyPerpendicular(n);
                Vertex(f.Outer[0]);
                int ax = Emit($"AXIS2_PLACEMENT_3D('',#{pointId[f.Outer[0]]},#{Direction(n)},#{Direction(r)})");
                int pl = Emit($"PLANE('',#{ax})");
                faces.Add(Emit($"ADVANCED_FACE('',({string.Join(",", bounds.Select(x => "#" + x))}),#{pl},.T.)"));
            }

            int shell = Emit($"CLOSED_SHELL('',({string.Join(",", faces.Select(x => "#" + x))}))");
            return Emit($"MANIFOLD_SOLID_BREP('{Escape(name)}',#{shell})");
        }

        private void WriteStyles(int context)
        {
            if (styled.Count == 0) return;
            var styleFor = new Dictionary<StepColor, int>();
            var items = new List<int>();
            foreach (var (solid, color) in styled)
            {
                if (!styleFor.TryGetValue(color, out int psa))
                {
                    int rgb = Emit($"COLOUR_RGB('',{F(color.R / 255.0)},{F(color.G / 255.0)},{F(color.B / 255.0)})");
                    int fac = Emit($"FILL_AREA_STYLE_COLOUR('',#{rgb})");
                    int fas = Emit($"FILL_AREA_STYLE('',(#{fac}))");
                    int ssfa = Emit($"SURFACE_STYLE_FILL_AREA(#{fas})");
                    int sss = Emit($"SURFACE_SIDE_STYLE('',(#{ssfa}))");
                    int ssu = Emit($"SURFACE_STYLE_USAGE(.BOTH.,#{sss})");
                    styleFor[color] = psa = Emit($"PRESENTATION_STYLE_ASSIGNMENT((#{ssu}))");
                }
                items.Add(Emit($"STYLED_ITEM('color',(#{psa}),#{solid})"));
            }
            Emit($"MECHANICAL_DESIGN_GEOMETRIC_PRESENTATION_REPRESENTATION('',({string.Join(",", items.Select(x => "#" + x))}),#{context})");
        }
    }
}
