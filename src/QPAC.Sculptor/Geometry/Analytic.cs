using System;
using System.Collections.Generic;
using System.Linq;

namespace QPAC.Sculptor
{
    // The exact surface or circle behind a group of facets. Shapes tag facets and edge chains with these
    // while they build; the STEP writer turns each tagged group into one curved face or edge. The facets
    // themselves never change, so the preview and every polyhedral calculation ignore the tags.
    internal abstract class AnalyticSurface
    {
        public abstract double Distance((double X, double Y, double Z) p);

        /// <summary>The surface's own normal at (or nearest to) <paramref name="p"/>.</summary>
        public abstract (double X, double Y, double Z) Normal((double X, double Y, double Z) p);

        /// <summary>The surface moved by a similarity transform that scales lengths by <paramref name="scale"/>.</summary>
        public abstract AnalyticSurface Transformed(Transform xf, double scale);
    }

    internal sealed class CylinderSurface : AnalyticSurface
    {
        public readonly (double X, double Y, double Z) Point;
        public readonly (double X, double Y, double Z) Axis;
        public readonly double Radius;

        public CylinderSurface((double X, double Y, double Z) point, (double X, double Y, double Z) axis, double radius)
        {
            Point = point; Axis = Vec.Normalize(axis); Radius = radius;
        }

        private (double X, double Y, double Z) Radial((double X, double Y, double Z) p)
        {
            var q = Vec.Sub(p, Point);
            return Vec.Sub(q, Vec.Scale(Axis, Vec.Dot(q, Axis)));
        }

        public override double Distance((double X, double Y, double Z) p) => Math.Abs(Vec.Length(Radial(p)) - Radius);

        public override (double X, double Y, double Z) Normal((double X, double Y, double Z) p) => Vec.Normalize(Radial(p));

        public override AnalyticSurface Transformed(Transform xf, double scale)
            => new CylinderSurface(xf.Apply(Point), xf.ApplyDirection(Axis), Radius * scale);
    }

    /// <summary>A cone opening along <see cref="Axis"/> from <see cref="Apex"/>.</summary>
    internal sealed class ConeSurface : AnalyticSurface
    {
        public readonly (double X, double Y, double Z) Apex;
        public readonly (double X, double Y, double Z) Axis;
        public readonly double SemiAngle;

        public ConeSurface((double X, double Y, double Z) apex, (double X, double Y, double Z) axis, double semiAngle)
        {
            Apex = apex; Axis = Vec.Normalize(axis); SemiAngle = semiAngle;
        }

        public override double Distance((double X, double Y, double Z) p)
        {
            var q = Vec.Sub(p, Apex);
            double h = Vec.Dot(q, Axis);
            double r = Vec.Length(Vec.Sub(q, Vec.Scale(Axis, h)));
            return Math.Abs(r * Math.Cos(SemiAngle) - h * Math.Sin(SemiAngle));
        }

        public override (double X, double Y, double Z) Normal((double X, double Y, double Z) p)
        {
            var q = Vec.Sub(p, Apex);
            var radial = Vec.Normalize(Vec.Sub(q, Vec.Scale(Axis, Vec.Dot(q, Axis))));
            return Vec.Normalize(Vec.Sub(Vec.Scale(radial, Math.Cos(SemiAngle)), Vec.Scale(Axis, Math.Sin(SemiAngle))));
        }

        public override AnalyticSurface Transformed(Transform xf, double scale)
            => new ConeSurface(xf.Apply(Apex), xf.ApplyDirection(Axis), SemiAngle);
    }

    internal sealed class SphereSurface : AnalyticSurface
    {
        public readonly (double X, double Y, double Z) Center;
        public readonly double Radius;

        public SphereSurface((double X, double Y, double Z) center, double radius)
        {
            Center = center; Radius = radius;
        }

        public override double Distance((double X, double Y, double Z) p) => Math.Abs(Vec.Length(Vec.Sub(p, Center)) - Radius);

        public override (double X, double Y, double Z) Normal((double X, double Y, double Z) p) => Vec.Normalize(Vec.Sub(p, Center));

        public override AnalyticSurface Transformed(Transform xf, double scale)
            => new SphereSurface(xf.Apply(Center), Radius * scale);
    }

    internal sealed class TorusSurface : AnalyticSurface
    {
        public readonly (double X, double Y, double Z) Center;
        public readonly (double X, double Y, double Z) Axis;
        public readonly double MajorRadius, MinorRadius;

        public TorusSurface((double X, double Y, double Z) center, (double X, double Y, double Z) axis, double major, double minor)
        {
            Center = center; Axis = Vec.Normalize(axis); MajorRadius = major; MinorRadius = minor;
        }

        private (double X, double Y, double Z) TubeCenter((double X, double Y, double Z) p)
        {
            var q = Vec.Sub(p, Center);
            var radial = Vec.Normalize(Vec.Sub(q, Vec.Scale(Axis, Vec.Dot(q, Axis))));
            return Vec.Add(Center, Vec.Scale(radial, MajorRadius));
        }

        public override double Distance((double X, double Y, double Z) p) => Math.Abs(Vec.Length(Vec.Sub(p, TubeCenter(p))) - MinorRadius);

        public override (double X, double Y, double Z) Normal((double X, double Y, double Z) p) => Vec.Normalize(Vec.Sub(p, TubeCenter(p)));

        public override AnalyticSurface Transformed(Transform xf, double scale)
            => new TorusSurface(xf.Apply(Center), xf.ApplyDirection(Axis), MajorRadius * scale, MinorRadius * scale);
    }

    internal sealed class PlaneSurface : AnalyticSurface
    {
        public readonly (double X, double Y, double Z) Point;
        public readonly (double X, double Y, double Z) Normal0;

        public PlaneSurface((double X, double Y, double Z) point, (double X, double Y, double Z) normal)
        {
            Point = point; Normal0 = Vec.Normalize(normal);
        }

        public override double Distance((double X, double Y, double Z) p) => Math.Abs(Vec.Dot(Vec.Sub(p, Point), Normal0));

        public override (double X, double Y, double Z) Normal((double X, double Y, double Z) p) => Normal0;

        public override AnalyticSurface Transformed(Transform xf, double scale)
            => new PlaneSurface(xf.Apply(Point), xf.ApplyDirection(Normal0));
    }

    internal sealed class Circle3D
    {
        public readonly (double X, double Y, double Z) Center;
        public readonly (double X, double Y, double Z) Normal;
        public readonly double Radius;

        public Circle3D((double X, double Y, double Z) center, (double X, double Y, double Z) normal, double radius)
        {
            Center = center; Normal = Vec.Normalize(normal); Radius = radius;
        }

        public (double X, double Y, double Z) RefDirection => Vec.AnyPerpendicular(Normal);

        public (double X, double Y, double Z) PointAt(double radians)
        {
            var x = RefDirection;
            var y = Vec.Cross(Normal, x);
            return Vec.Add(Center, Vec.Add(Vec.Scale(x, Radius * Math.Cos(radians)), Vec.Scale(y, Radius * Math.Sin(radians))));
        }

        /// <summary>Direction of travel at <paramref name="p"/> when going counter-clockwise about <see cref="Normal"/>.</summary>
        public (double X, double Y, double Z) Tangent((double X, double Y, double Z) p) => Vec.Cross(Normal, Vec.Sub(p, Center));

        public bool LiesOn(AnalyticSurface s, double tol)
        {
            for (int k = 0; k < 8; k++)
                if (s.Distance(PointAt(k * Math.PI / 4)) > tol) return false;
            return true;
        }

        public Circle3D Transformed(Transform xf, double scale)
            => new Circle3D(xf.Apply(Center), xf.ApplyDirection(Normal), Radius * scale);
    }

    /// <summary>A chain of polyhedron vertices that all sit on one circle; consecutive pairs are facet edges.</summary>
    internal sealed class CurveRun
    {
        public int[] Chain;
        public readonly Circle3D Curve;

        public CurveRun(int[] chain, Circle3D curve)
        {
            Chain = chain;
            Curve = curve;
        }
    }

    /// <summary>A run of a 2D loop's edges that lie on one circle: edges Start … Start+Segments−1 (wrapping).</summary>
    internal readonly struct Arc2D
    {
        public readonly int Start;
        public readonly int Segments;
        public readonly (double U, double V) Center;
        public readonly double Radius;

        public Arc2D(int start, int segments, (double U, double V) center, double radius)
        {
            Start = start; Segments = segments; Center = center; Radius = radius;
        }
    }

    internal static class Arcs
    {
        // A tessellated circle steps at most this far per segment (an 8-sided circle); regular hexagons
        // and coarser polygons stay polygons.
        private const double MaxStep = Math.PI / 4 + 1e-9;

        // Three points always share a circle, so an arc needs at least three segments to be told apart
        // from a polyline that happens to bend twice.
        private const int MinSegments = 3;

        /// <summary>
        /// Finds the runs of a polyline whose vertices step evenly around one circle — what every
        /// circle, fillet, slot end and bend in <see cref="Profiles"/> produces. A full circle comes
        /// back as two halves so that no run closes on itself.
        /// </summary>
        public static List<Arc2D> Find(IReadOnlyList<(double U, double V)> pts, bool closed)
        {
            int n = pts.Count;
            var result = new List<Arc2D>();
            if (n < MinSegments + 1) return result;

            var fit = new (bool Ok, (double U, double V) C, double R, int Turn)[n];
            for (int i = 0; i < n; i++)
            {
                if (!closed && (i == 0 || i == n - 1)) continue;
                fit[i] = Fit(pts[(i - 1 + n) % n], pts[i], pts[(i + 1) % n]);
            }

            bool Same(int i, int j)
            {
                var a = fit[i]; var b = fit[j];
                if (!a.Ok || !b.Ok || a.Turn != b.Turn) return false;
                double tol = 1e-7 * Math.Max(1, a.R);
                return Math.Abs(a.R - b.R) <= tol && Polygon2D.Dist(a.C, b.C) <= tol;
            }

            int first = 0, count = n;
            if (closed)
            {
                first = -1;
                for (int i = 0; i < n && first < 0; i++)
                    if (!Same((i - 1 + n) % n, i)) first = i;
                if (first < 0)
                {
                    int half = n / 2;
                    result.Add(new Arc2D(0, half, fit[0].C, fit[0].R));
                    result.Add(new Arc2D(half, n - half, fit[0].C, fit[0].R));
                    return result;
                }
            }
            else { first = 1; count = n - 2; }

            // Groups of consecutive vertices fitting one circle; a group of k vertices spans k+1 edges.
            var groups = new List<(int FirstVertex, int Vertices)>();
            int gStart = -1, gLen = 0, prev = -1;
            for (int k = 0; k < count; k++)
            {
                int i = (first + k) % n;
                if (gLen > 0 && Same(prev, i)) gLen++;
                else
                {
                    if (gLen > 0) groups.Add((gStart, gLen));
                    gLen = 0;
                    if (fit[i].Ok) { gStart = i; gLen = 1; }
                }
                prev = i;
            }
            if (gLen > 0) groups.Add((gStart, gLen));

            int lastEdge = int.MinValue;
            foreach (var (fv, len) in groups)
            {
                int startEdge = (fv - 1 + n) % n;
                int segments = len + 1;
                // Two circles that meet share the edge between them; it goes to the first one.
                if (startEdge == lastEdge) { startEdge = (startEdge + 1) % n; segments--; }
                lastEdge = (startEdge + segments - 1) % n;
                if (segments >= MinSegments) result.Add(new Arc2D(startEdge, segments, fit[fv].C, fit[fv].R));
            }
            if (closed && result.Count > 1)
            {
                var a = result[0];
                var z = result[result.Count - 1];
                if ((z.Start + z.Segments - 1) % n == a.Start)
                {
                    if (a.Segments - 1 >= MinSegments) result[0] = new Arc2D((a.Start + 1) % n, a.Segments - 1, a.Center, a.Radius);
                    else result.RemoveAt(0);
                }
            }
            return result;
        }

        private static (bool Ok, (double U, double V) C, double R, int Turn) Fit((double U, double V) a, (double U, double V) b, (double U, double V) c)
        {
            // Work relative to the middle point: coordinates are often large next to a small arc.
            double ax = a.U - b.U, ay = a.V - b.V, cx = c.U - b.U, cy = c.V - b.V;
            double la = Math.Sqrt(ax * ax + ay * ay), lc = Math.Sqrt(cx * cx + cy * cy);
            double d = 2 * (ax * cy - ay * cx);
            if (la < 1e-12 || lc < 1e-12 || Math.Abs(d) < 1e-15 * la * lc) return default;
            double a2 = ax * ax + ay * ay, c2 = cx * cx + cy * cy;
            double ux = (cy * a2 - ay * c2) / d, uy = (ax * c2 - cx * a2) / d;
            double r = Math.Sqrt(ux * ux + uy * uy);
            if (Math.Abs(la - lc) > 1e-7 * Math.Max(1, r)) return default;
            if (2 * Math.Asin(Math.Min(1, la / (2 * r))) > MaxStep) return default;
            return (true, (b.U + ux, b.V + uy), r, d > 0 ? -1 : 1);
        }

        /// <summary>
        /// The surface a straight meridian edge p → q sweeps turning about the axis through
        /// <paramref name="origin"/> along unit <paramref name="w"/> (R = distance from the axis, Z = along
        /// it): a cylinder, a cone, or null for a flat ring.
        /// </summary>
        public static AnalyticSurface Revolved((double R, double Z) p, (double R, double Z) q,
            (double X, double Y, double Z) origin, (double X, double Y, double Z) w, double tol)
        {
            if (Math.Abs(q.Z - p.Z) <= tol) return null;
            if (Math.Abs(q.R - p.R) <= tol) return p.R > tol ? new CylinderSurface(origin, w, p.R) : null;
            double slope = (q.R - p.R) / (q.Z - p.Z);
            var apex = Vec.Add(origin, Vec.Scale(w, p.Z - p.R / slope));
            return new ConeSurface(apex, slope > 0 ? w : Vec.Scale(w, -1), Math.Atan(Math.Abs(slope)));
        }

        /// <summary>The sphere or torus a meridian arc sweeps; null for a self-intersecting torus.</summary>
        public static AnalyticSurface RevolvedArc((double R, double Z) center, double radius,
            (double X, double Y, double Z) origin, (double X, double Y, double Z) w, double tol)
        {
            var c = Vec.Add(origin, Vec.Scale(w, center.Z));
            if (Math.Abs(center.R) <= tol) return new SphereSurface(c, radius);
            return center.R > radius + tol ? new TorusSurface(c, w, center.R, radius) : null;
        }

        /// <summary>(distance from the axis, height along it) of <paramref name="p"/>.</summary>
        public static (double R, double Z) Meridian((double X, double Y, double Z) p, (double X, double Y, double Z) origin, (double X, double Y, double Z) w)
        {
            var q = Vec.Sub(p, origin);
            double z = Vec.Dot(q, w);
            return (Vec.Length(Vec.Sub(q, Vec.Scale(w, z))), z);
        }

        /// <summary>
        /// Tags the side walls of a prism built by joining the 2D loop at depth <paramref name="ta"/>
        /// (vertices <paramref name="a"/>) to the same loop at <paramref name="tb"/> (vertices <paramref name="b"/>),
        /// whose wall faces were added in loop order from <paramref name="firstFace"/>.
        /// </summary>
        public static void TagPrism(
            Polyhedron solid, int firstFace, IReadOnlyList<(double U, double V)> loop, int[] a, int[] b,
            Func<(double U, double V), double, (double X, double Y, double Z)> place,
            (double X, double Y, double Z) axis, double ta, double tb)
        {
            int n = loop.Count;
            foreach (var arc in Find(loop, closed: true))
            {
                var surface = new CylinderSurface(place(arc.Center, ta), axis, arc.Radius);
                for (int e = 0; e < arc.Segments; e++) solid.Faces[firstFace + (arc.Start + e) % n].Surface = surface;
                int[] Chain(int[] ring) => Enumerable.Range(0, arc.Segments + 1).Select(e => ring[(arc.Start + e) % n]).ToArray();
                solid.Curves.Add(new CurveRun(Chain(a), new Circle3D(place(arc.Center, ta), axis, arc.Radius)));
                solid.Curves.Add(new CurveRun(Chain(b), new Circle3D(place(arc.Center, tb), axis, arc.Radius)));
            }
        }
    }
}
