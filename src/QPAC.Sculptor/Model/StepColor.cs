using System;
using System.Globalization;

namespace QPAC.Sculptor
{
    /// <summary>An sRGB colour written to STEP as a surface style and used by the WPF preview.</summary>
    public readonly struct StepColor : IEquatable<StepColor>
    {
        public readonly byte R, G, B;

        public StepColor(byte r, byte g, byte b) { R = r; G = g; B = b; }

        /// <summary>Parses <c>#RRGGBB</c> or <c>RRGGBB</c>.</summary>
        public static StepColor FromHex(string hex)
        {
            var h = (hex ?? "").TrimStart('#');
            if (h.Length != 6) throw new FormatException($"'{hex}' is not a #RRGGBB colour.");
            return new StepColor(
                byte.Parse(h.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                byte.Parse(h.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                byte.Parse(h.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
        }

        public string ToHex() => $"#{R:X2}{G:X2}{B:X2}";

        public bool Equals(StepColor other) => R == other.R && G == other.G && B == other.B;
        public override bool Equals(object obj) => obj is StepColor c && Equals(c);
        public override int GetHashCode() => (R << 16) | (G << 8) | B;
        public static bool operator ==(StepColor a, StepColor b) => a.Equals(b);
        public static bool operator !=(StepColor a, StepColor b) => !a.Equals(b);
        public override string ToString() => ToHex();

        public static StepColor SheetMetal => new StepColor(160, 174, 184);
        public static StepColor Galvanized => new StepColor(190, 196, 200);
        public static StepColor Fan => new StepColor(21, 101, 176);
        public static StepColor Controller => new StepColor(230, 120, 30);
        public static StepColor Channel => new StepColor(90, 103, 112);
        public static StepColor Mount => new StepColor(120, 128, 135);
        public static StepColor Harness => new StepColor(35, 35, 40);
        public static StepColor Border => new StepColor(60, 70, 80);
        public static StepColor Insulation => new StepColor(235, 220, 160);
        public static StepColor Rubber => new StepColor(25, 25, 25);
    }
}
