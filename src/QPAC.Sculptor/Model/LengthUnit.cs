namespace QPAC.Sculptor
{
    public enum LengthUnit
    {
        Inch,
        Millimeter,
        Centimeter,
        Meter,
        Foot,
    }

    public static class LengthUnits
    {
        public static double MillimetersPer(LengthUnit unit)
        {
            switch (unit)
            {
                case LengthUnit.Inch: return 25.4;
                case LengthUnit.Centimeter: return 10.0;
                case LengthUnit.Meter: return 1000.0;
                case LengthUnit.Foot: return 304.8;
                default: return 1.0;
            }
        }

        /// <summary>Factor that converts a length in <paramref name="from"/> to <paramref name="to"/>.</summary>
        public static double Factor(LengthUnit from, LengthUnit to) => MillimetersPer(from) / MillimetersPer(to);
    }
}
