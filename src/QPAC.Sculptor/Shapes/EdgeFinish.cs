namespace QPAC.Sculptor
{
    /// <summary>How the edges around an extrusion's cap face are finished: a round fillet or a 45° chamfer.</summary>
    public sealed class EdgeFinish
    {
        public double Size;
        public int Segments;
        public bool IsChamfer;

        public EdgeFinish(double size, int segments, bool isChamfer)
        {
            Size = size;
            Segments = segments;
            IsChamfer = isChamfer;
        }

        public static EdgeFinish Fillet(double radius, int segments = 6) => new EdgeFinish(radius, segments, false);

        public static EdgeFinish Chamfer(double distance) => new EdgeFinish(distance, 1, true);

        // (distance from the cap, inset of the profile) samples from the cap inward.
        internal (double T, double Inset)[] Samples()
        {
            if (Size <= 0) return new[] { (0.0, 0.0) };
            if (IsChamfer) return new[] { (0.0, Size), (Size, 0.0) };
            int k = System.Math.Max(1, Segments);
            var s = new (double T, double Inset)[k + 1];
            for (int i = 0; i <= k; i++)
            {
                double a = System.Math.PI / 2 * i / k;
                s[i] = (Size * (1 - System.Math.Cos(a)), Size * (1 - System.Math.Sin(a)));
            }
            s[k] = (Size, 0);
            return s;
        }
    }
}
