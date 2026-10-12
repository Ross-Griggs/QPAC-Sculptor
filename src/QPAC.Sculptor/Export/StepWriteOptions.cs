namespace QPAC.Sculptor
{
    public class StepWriteOptions
    {
        /// <summary>
        /// Unit to write the file in. Null keeps the model's own <see cref="StepFile.Units"/>; anything
        /// else converts every coordinate (e.g. a model drawn in inches exported for a metric customer).
        /// </summary>
        public LengthUnit? ExportUnits;

        /// <summary>Write part/shape colours as STEP surface styles.</summary>
        public bool IncludeColors = true;

        /// <summary>Colour for shapes whose part and shape both leave it unset (null = leave uncoloured).</summary>
        public StepColor? DefaultColor;

        /// <summary>Run <see cref="Polyhedron.Validate"/> on every solid and throw on the first problem.</summary>
        public bool ValidateSolids;

        /// <summary>Leave out shapes whose geometry is invalid instead of throwing (they are listed in <see cref="SkippedShapes"/>).</summary>
        public bool SkipInvalidShapes;

        /// <summary>Filled in by the writer when <see cref="SkipInvalidShapes"/> drops a shape.</summary>
        public System.Collections.Generic.List<string> SkippedShapes = new System.Collections.Generic.List<string>();

        public string OriginatingSystem = "QPAC.Sculptor";

        public static StepWriteOptions Default => new StepWriteOptions();
    }
}
