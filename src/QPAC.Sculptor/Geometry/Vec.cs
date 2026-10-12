using System;

namespace QPAC.Sculptor
{
    internal static class Vec
    {
        public static (double X, double Y, double Z) Add((double X, double Y, double Z) a, (double X, double Y, double Z) b)
            => (a.X + b.X, a.Y + b.Y, a.Z + b.Z);

        public static (double X, double Y, double Z) Sub((double X, double Y, double Z) a, (double X, double Y, double Z) b)
            => (a.X - b.X, a.Y - b.Y, a.Z - b.Z);

        public static (double X, double Y, double Z) Scale((double X, double Y, double Z) a, double s)
            => (a.X * s, a.Y * s, a.Z * s);

        public static double Dot((double X, double Y, double Z) a, (double X, double Y, double Z) b)
            => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        public static (double X, double Y, double Z) Cross((double X, double Y, double Z) a, (double X, double Y, double Z) b)
            => (a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

        public static double Length((double X, double Y, double Z) a) => Math.Sqrt(Dot(a, a));

        public static (double X, double Y, double Z) Normalize((double X, double Y, double Z) a)
        {
            double l = Length(a);
            if (l < 1e-12) return (0, 0, 0);
            return (a.X / l, a.Y / l, a.Z / l);
        }

        public static (double X, double Y, double Z) Lerp((double X, double Y, double Z) a, (double X, double Y, double Z) b, double t)
            => (a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);

        public static (double X, double Y, double Z) AnyPerpendicular((double X, double Y, double Z) n)
        {
            var helper = Math.Abs(n.Z) > 0.9 ? (1.0, 0.0, 0.0) : (0.0, 0.0, 1.0);
            return Normalize(Cross(helper, n));
        }

        // Rodrigues rotation of v about a unit axis.
        public static (double X, double Y, double Z) Rotate((double X, double Y, double Z) v, (double X, double Y, double Z) unitAxis, double radians)
        {
            double c = Math.Cos(radians), s = Math.Sin(radians);
            var k = unitAxis;
            var kxv = Cross(k, v);
            double kdv = Dot(k, v);
            return (v.X * c + kxv.X * s + k.X * kdv * (1 - c),
                    v.Y * c + kxv.Y * s + k.Y * kdv * (1 - c),
                    v.Z * c + kxv.Z * s + k.Z * kdv * (1 - c));
        }

        public static (double X, double Y, double Z) Frame(
            (double X, double Y, double Z) origin,
            (double X, double Y, double Z) u,
            (double X, double Y, double Z) v,
            (double X, double Y, double Z) w,
            double pu, double pv, double pw)
            => (origin.X + pu * u.X + pv * v.X + pw * w.X,
                origin.Y + pu * u.Y + pv * v.Y + pw * w.Y,
                origin.Z + pu * u.Z + pv * v.Z + pw * w.Z);
    }
}
