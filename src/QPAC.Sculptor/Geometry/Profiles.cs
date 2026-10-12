using System;
using System.Collections.Generic;
using System.Linq;

namespace QPAC.Sculptor
{
    /// <summary>
    /// Ready-made closed 2D profiles (counter-clockwise <c>(U, V)</c> loops) for extrusions,
    /// sweeps, lofts and revolves, plus operations to fillet, chamfer, offset and move them.
    /// </summary>
    public static class Profiles
    {
        public static List<(double U, double V)> Rectangle(double cu, double cv, double width, double height)
        {
            double hw = width / 2, hh = height / 2;
            return new List<(double U, double V)> { (cu - hw, cv - hh), (cu + hw, cv - hh), (cu + hw, cv + hh), (cu - hw, cv + hh) };
        }

        public static List<(double U, double V)> RoundedRectangle(double cu, double cv, double width, double height, double radius, int segmentsPerCorner = 8)
        {
            double r = Math.Min(radius, Math.Min(width, height) / 2);
            if (r <= 0) return Rectangle(cu, cv, width, height);
            double hw = width / 2 - r, hh = height / 2 - r;
            int k = Math.Max(1, segmentsPerCorner);
            var loop = new List<(double U, double V)>();
            var centers = new[] { (cu + hw, cv - hh), (cu + hw, cv + hh), (cu - hw, cv + hh), (cu - hw, cv - hh) };
            for (int c = 0; c < 4; c++)
            {
                double a0 = -Math.PI / 2 + c * Math.PI / 2;
                for (int i = 0; i <= k; i++)
                {
                    double a = a0 + Math.PI / 2 * i / k;
                    loop.Add((centers[c].Item1 + r * Math.Cos(a), centers[c].Item2 + r * Math.Sin(a)));
                }
            }
            return Polygon2D.Clean(loop);
        }

        public static List<(double U, double V)> Circle(double cu, double cv, double radius, int sides = 64)
            => Ellipse(cu, cv, radius, radius, sides);

        public static List<(double U, double V)> Ellipse(double cu, double cv, double radiusU, double radiusV, int sides = 64)
        {
            int n = Math.Max(8, sides);
            var loop = new List<(double U, double V)>(n);
            for (int i = 0; i < n; i++)
            {
                double a = 2.0 * Math.PI * i / n;
                loop.Add((cu + radiusU * Math.Cos(a), cv + radiusV * Math.Sin(a)));
            }
            return loop;
        }

        /// <summary>Regular polygon (hexagon, octagon …) with the given circumscribed radius.</summary>
        public static List<(double U, double V)> RegularPolygon(double cu, double cv, double radius, int sides, double rotationDegrees = 0)
        {
            int n = Math.Max(3, sides);
            double a0 = rotationDegrees * Math.PI / 180;
            var loop = new List<(double U, double V)>(n);
            for (int i = 0; i < n; i++)
            {
                double a = a0 + 2.0 * Math.PI * i / n;
                loop.Add((cu + radius * Math.Cos(a), cv + radius * Math.Sin(a)));
            }
            return loop;
        }

        /// <summary>Obround slot from centre (<paramref name="u1"/>,<paramref name="v1"/>) to (<paramref name="u2"/>,<paramref name="v2"/>).</summary>
        public static List<(double U, double V)> Slot(double u1, double v1, double u2, double v2, double width, int segmentsPerEnd = 16)
        {
            double r = width / 2;
            double a = Math.Atan2(v2 - v1, u2 - u1);
            int k = Math.Max(2, segmentsPerEnd);
            var loop = new List<(double U, double V)>();
            for (int i = 0; i <= k; i++)
            {
                double t = a - Math.PI / 2 + Math.PI * i / k;
                loop.Add((u2 + r * Math.Cos(t), v2 + r * Math.Sin(t)));
            }
            for (int i = 0; i <= k; i++)
            {
                double t = a + Math.PI / 2 + Math.PI * i / k;
                loop.Add((u1 + r * Math.Cos(t), v1 + r * Math.Sin(t)));
            }
            return Polygon2D.Clean(loop);
        }

        /// <summary>Annular sector (a ring segment) between two radii, spanning angles in degrees.</summary>
        public static List<(double U, double V)> Arc(double cu, double cv, double innerRadius, double outerRadius, double startDegrees, double endDegrees, int segments = 32)
        {
            double a0 = startDegrees * Math.PI / 180, a1 = endDegrees * Math.PI / 180;
            int k = Math.Max(1, segments);
            var loop = new List<(double U, double V)>();
            for (int i = 0; i <= k; i++)
            {
                double a = a0 + (a1 - a0) * i / k;
                loop.Add((cu + outerRadius * Math.Cos(a), cv + outerRadius * Math.Sin(a)));
            }
            for (int i = k; i >= 0; i--)
            {
                double a = a0 + (a1 - a0) * i / k;
                if (innerRadius > 0) loop.Add((cu + innerRadius * Math.Cos(a), cv + innerRadius * Math.Sin(a)));
                else { loop.Add((cu, cv)); break; }
            }
            return Polygon2D.WithWinding(loop, true);
        }

        /// <summary>Closed polygon through the given points (any winding).</summary>
        public static List<(double U, double V)> Polygon(params (double U, double V)[] points)
            => Polygon2D.WithWinding(Polygon2D.Clean(points), true);

        /// <summary>
        /// A channel (C / U section) opening toward +V: <paramref name="width"/> across the web,
        /// <paramref name="flangeHeight"/> tall, sheet <paramref name="thickness"/>, with an inside bend
        /// radius (0 for sharp corners). Origin is the outside-bottom centre of the web.
        /// </summary>
        public static List<(double U, double V)> Channel(double width, double flangeHeight, double thickness, double insideBendRadius = 0, int bendSegments = 8)
        {
            double hw = width / 2;
            var centerline = new List<(double U, double V)>
            {
                (-hw + thickness / 2, flangeHeight),
                (-hw + thickness / 2, thickness / 2),
                (hw - thickness / 2, thickness / 2),
                (hw - thickness / 2, flangeHeight),
            };
            return ThickPolyline(centerline, thickness, insideBendRadius, bendSegments);
        }

        /// <summary>An angle (L section) with legs along +U and +V from the outside corner at the origin.</summary>
        public static List<(double U, double V)> Angle(double legU, double legV, double thickness, double insideBendRadius = 0, int bendSegments = 8)
        {
            var centerline = new List<(double U, double V)>
            {
                (legU, thickness / 2),
                (thickness / 2, thickness / 2),
                (thickness / 2, legV),
            };
            return ThickPolyline(centerline, thickness, insideBendRadius, bendSegments);
        }

        /// <summary>A Z section: bottom flange toward −U, web up +V, top flange toward +U.</summary>
        public static List<(double U, double V)> ZSection(double flange, double height, double thickness, double insideBendRadius = 0, int bendSegments = 8)
        {
            double t2 = thickness / 2;
            var centerline = new List<(double U, double V)>
            {
                (-flange + t2, t2), (t2, t2), (t2, height - t2), (flange + t2, height - t2),
            };
            return ThickPolyline(centerline, thickness, insideBendRadius, bendSegments);
        }

        /// <summary>Inner loop of a rectangular tube with the given wall; pair it with <see cref="RoundedRectangle"/> as the outline.</summary>
        public static List<(double U, double V)> RectangularTubeInner(double cu, double cv, double width, double height, double wall, double outerRadius = 0, int segmentsPerCorner = 8)
            => RoundedRectangle(cu, cv, width - 2 * wall, height - 2 * wall, Math.Max(0, outerRadius - wall), segmentsPerCorner);

        /// <summary>
        /// Thickens an open centreline into a closed sheet-metal style profile of constant
        /// <paramref name="thickness"/>, rounding each bend with the given inside radius.
        /// </summary>
        public static List<(double U, double V)> ThickPolyline(IReadOnlyList<(double U, double V)> centerline, double thickness, double insideBendRadius = 0, int bendSegments = 8)
        {
            if (centerline.Count < 2) throw new ArgumentException("Centreline needs at least two points.", nameof(centerline));
            if (thickness <= 0) throw new ArgumentException("Thickness must be positive.", nameof(thickness));
            var path = insideBendRadius > 0
                ? FilletOpen(centerline, insideBendRadius + thickness / 2, bendSegments)
                : centerline.ToList();

            var left = new List<(double U, double V)>();
            var right = new List<(double U, double V)>();
            double h = thickness / 2;
            int n = path.Count;
            for (int i = 0; i < n; i++)
            {
                (double U, double V) nrm;
                if (i == 0) nrm = LeftNormal(path[0], path[1]);
                else if (i == n - 1) nrm = LeftNormal(path[n - 2], path[n - 1]);
                else
                {
                    var n0 = LeftNormal(path[i - 1], path[i]);
                    var n1 = LeftNormal(path[i], path[i + 1]);
                    var b = (n0.U + n1.U, n0.V + n1.V);
                    double bl2 = b.Item1 * b.Item1 + b.Item2 * b.Item2;
                    double s = bl2 < 1e-12 ? 0 : 2.0 / bl2;
                    nrm = (b.Item1 * s, b.Item2 * s);
                }
                left.Add((path[i].U + nrm.U * h, path[i].V + nrm.V * h));
                right.Add((path[i].U - nrm.U * h, path[i].V - nrm.V * h));
            }
            right.Reverse();
            var loop = right.Concat(left).ToList();
            return Polygon2D.WithWinding(Polygon2D.Clean(loop), true);
        }

        private static (double U, double V) LeftNormal((double U, double V) a, (double U, double V) b)
        {
            var e = Polygon2D.Unit((b.U - a.U, b.V - a.V));
            return (-e.V, e.U);
        }

        /// <summary>Rounds every corner of a closed profile with the given radius.</summary>
        public static List<(double U, double V)> Fillet(IReadOnlyList<(double U, double V)> loop, double radius, int segmentsPerCorner = 8)
            => FilletCorners(loop, radius, segmentsPerCorner, Enumerable.Range(0, loop.Count).ToArray());

        /// <summary>Rounds only the listed corners (vertex indices) of a closed profile.</summary>
        public static List<(double U, double V)> FilletCorners(IReadOnlyList<(double U, double V)> loop, double radius, int segmentsPerCorner, params int[] corners)
        {
            var set = new HashSet<int>(corners);
            int n = loop.Count;
            var r = new List<(double U, double V)>();
            for (int i = 0; i < n; i++)
            {
                if (!set.Contains(i) || radius <= 0) { r.Add(loop[i]); continue; }
                r.AddRange(CornerArc(loop[(i - 1 + n) % n], loop[i], loop[(i + 1) % n], radius, segmentsPerCorner, closedLimit: true));
            }
            return Polygon2D.Clean(r);
        }

        /// <summary>Cuts every corner of a closed profile back by <paramref name="distance"/>.</summary>
        public static List<(double U, double V)> Chamfer(IReadOnlyList<(double U, double V)> loop, double distance)
        {
            int n = loop.Count;
            var r = new List<(double U, double V)>();
            for (int i = 0; i < n; i++)
            {
                var a = loop[(i - 1 + n) % n]; var b = loop[i]; var c = loop[(i + 1) % n];
                double la = Polygon2D.Dist(a, b), lc = Polygon2D.Dist(c, b);
                double d = Math.Min(distance, 0.5 * Math.Min(la, lc));
                var ea = Polygon2D.Unit((a.U - b.U, a.V - b.V));
                var ec = Polygon2D.Unit((c.U - b.U, c.V - b.V));
                r.Add((b.U + ea.U * d, b.V + ea.V * d));
                r.Add((b.U + ec.U * d, b.V + ec.V * d));
            }
            return Polygon2D.Clean(r);
        }

        /// <summary>
        /// Grows (positive <paramref name="distance"/>) or shrinks (negative) a closed profile with mitred
        /// corners. Holes and outlines are both handled by their winding.
        /// </summary>
        public static List<(double U, double V)> Offset(IReadOnlyList<(double U, double V)> loop, double distance)
        {
            bool ccw = Polygon2D.SignedArea(loop) > 0;
            return Polygon2D.OffsetLeft(loop, ccw ? -distance : distance, "Profiles.Offset");
        }

        public static List<(double U, double V)> Translate(IEnumerable<(double U, double V)> loop, double du, double dv)
            => loop.Select(p => (p.U + du, p.V + dv)).ToList();

        public static List<(double U, double V)> Rotate(IEnumerable<(double U, double V)> loop, double degrees, double cu = 0, double cv = 0)
        {
            double a = degrees * Math.PI / 180, c = Math.Cos(a), s = Math.Sin(a);
            return loop.Select(p => (cu + (p.U - cu) * c - (p.V - cv) * s, cv + (p.U - cu) * s + (p.V - cv) * c)).ToList();
        }

        public static List<(double U, double V)> Scale(IEnumerable<(double U, double V)> loop, double factor, double cu = 0, double cv = 0)
            => loop.Select(p => (cu + (p.U - cu) * factor, cv + (p.V - cv) * factor)).ToList();

        /// <summary>Mirror across the line U = <paramref name="axisU"/>; winding is restored to counter-clockwise.</summary>
        public static List<(double U, double V)> MirrorU(IEnumerable<(double U, double V)> loop, double axisU = 0)
            => Polygon2D.WithWinding(loop.Select(p => (2 * axisU - p.U, p.V)), true);

        public static List<(double U, double V)> MirrorV(IEnumerable<(double U, double V)> loop, double axisV = 0)
            => Polygon2D.WithWinding(loop.Select(p => (p.U, 2 * axisV - p.V)), true);

        public static double Area(IReadOnlyList<(double U, double V)> loop) => Math.Abs(Polygon2D.SignedArea(loop));

        internal static List<(double U, double V)> FilletOpen(IReadOnlyList<(double U, double V)> path, double radius, int segments)
        {
            var r = new List<(double U, double V)> { path[0] };
            for (int i = 1; i < path.Count - 1; i++)
                r.AddRange(CornerArc(path[i - 1], path[i], path[i + 1], radius, segments, closedLimit: false));
            r.Add(path[path.Count - 1]);
            return Polygon2D.Clean(r);
        }

        private static IEnumerable<(double U, double V)> CornerArc((double U, double V) a, (double U, double V) b, (double U, double V) c, double radius, int segments, bool closedLimit)
        {
            var e1 = Polygon2D.Unit((a.U - b.U, a.V - b.V));
            var e2 = Polygon2D.Unit((c.U - b.U, c.V - b.V));
            double cos = Math.Max(-1, Math.Min(1, e1.U * e2.U + e1.V * e2.V));
            double theta = Math.Acos(cos);
            if (theta < 1e-6 || Math.PI - theta < 1e-6) { yield return b; yield break; }

            double tangent = radius / Math.Tan(theta / 2);
            // Never eat more than half of either neighbouring edge on a closed loop (the next corner needs the rest).
            double limit = Math.Min(Polygon2D.Dist(a, b), Polygon2D.Dist(c, b)) * (closedLimit ? 0.5 : 1.0);
            double r = radius;
            if (tangent > limit) { tangent = limit; r = tangent * Math.Tan(theta / 2); }

            var t1 = (b.U + e1.U * tangent, b.V + e1.V * tangent);
            var t2 = (b.U + e2.U * tangent, b.V + e2.V * tangent);
            var bis = Polygon2D.Unit((e1.U + e2.U, e1.V + e2.V));
            double cd = r / Math.Sin(theta / 2);
            var center = (b.U + bis.U * cd, b.V + bis.V * cd);

            double a0 = Math.Atan2(t1.Item2 - center.Item2, t1.Item1 - center.Item1);
            double a1 = Math.Atan2(t2.Item2 - center.Item2, t2.Item1 - center.Item1);
            double sweep = a1 - a0;
            while (sweep > Math.PI) sweep -= 2 * Math.PI;
            while (sweep < -Math.PI) sweep += 2 * Math.PI;
            int k = Math.Max(1, segments);
            for (int i = 0; i <= k; i++)
            {
                double ang = a0 + sweep * i / k;
                yield return (center.Item1 + r * Math.Cos(ang), center.Item2 + r * Math.Sin(ang));
            }
        }
    }
}
