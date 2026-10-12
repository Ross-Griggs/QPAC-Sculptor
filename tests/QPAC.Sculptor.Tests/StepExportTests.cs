using System.Globalization;
using System.Text.RegularExpressions;
using QPAC.Sculptor;
using Xunit;

namespace QPAC.Sculptor.Tests;

public class StepExportTests
{
    private static StepFile Sample()
    {
        var file = new StepFile();
        var part = file.AddPart(new StepPart("Panel", StepColor.SheetMetal));
        part.Add(new Rectangle("p", 0, 0, 0, 10, 10, 0.0625));
        part.Add(new Cylinder("pin", 0, 0, 0, 0, 0, 2, 0.25)).WithColor(StepColor.Fan);
        return file;
    }

    private static int Count(string text, string entity) => Regex.Matches(text, $@"={entity}\(").Count;

    [Fact]
    public void DefaultsToInchesAndSaysSo()
    {
        var text = Step.ToStepString(Sample(), "Widget");
        Assert.Contains("CONVERSION_BASED_UNIT('INCH'", text);
        Assert.Contains("LENGTH_MEASURE(25.4)", text);
        Assert.Contains("/* Length unit: INCH.", text);
        Assert.Contains("'Length unit: inch'", text);
        Assert.Matches(@"GLOBAL_UNIT_ASSIGNED_CONTEXT\(\(#\d+", text);
        Assert.DoesNotContain("SPFA", text);
    }

    [Fact]
    public void ExportUnitsConvertCoordinates()
    {
        var file = new StepFile();
        file.AddPart("Box").Add(new Rectangle("b", 0, 0, 0, 2, 2, 2));
        var mm = Step.ToStepString(file, "Box", new StepWriteOptions { ExportUnits = LengthUnit.Millimeter });
        Assert.DoesNotContain("INCH", mm);
        Assert.Contains("SI_UNIT(.MILLI.,.METRE.)", mm);
        Assert.Contains("CARTESIAN_POINT('',(25.4,25.4,25.4))", mm);

        file.Units = LengthUnit.Millimeter;
        var native = Step.ToStepString(file, "Box");
        Assert.Contains("CARTESIAN_POINT('',(1.0,1.0,1.0))", native);
    }

    [Fact]
    public void OneSolidPerShapeAndAnEdgeCurvePerSharedEdge()
    {
        var text = Step.ToStepString(Sample(), "Widget");
        Assert.Equal(2, Count(text, "MANIFOLD_SOLID_BREP"));
        // Box: 12 edges, 6 faces. Cylinder (16 sides): 48 edges, 18 faces.
        Assert.Equal(12 + 48, Count(text, "EDGE_CURVE"));
        Assert.Equal(6 + 18, Count(text, "ADVANCED_FACE"));
        Assert.Equal(2 * (12 + 48), Count(text, "ORIENTED_EDGE"));
    }

    [Fact]
    public void ColorsAreWrittenOncePerColor()
    {
        var text = Step.ToStepString(Sample(), "Widget");
        Assert.Equal(2, Count(text, "COLOUR_RGB"));
        Assert.Equal(2, Count(text, "STYLED_ITEM"));
        Assert.Single(Regex.Matches(text, "MECHANICAL_DESIGN_GEOMETRIC_PRESENTATION_REPRESENTATION"));

        var plain = Step.ToStepString(Sample(), "Widget", new StepWriteOptions { IncludeColors = false });
        Assert.Equal(0, Count(plain, "COLOUR_RGB"));
    }

    [Fact]
    public void InstancesShareOneDefinition()
    {
        var file = new StepFile();
        var fan = new StepPart("Fan");
        fan.Add(new Cylinder("hub", 0, 0, 0, 0, 0, 1, 1));
        for (int i = 0; i < 4; i++) file.AddInstance(fan, Transform.Translation(i * 3, 0, 0), $"Fan {i + 1}");
        file.AddInstance(fan, Transform.RotationAbout((0, 0, 0), (1, 0, 0), 90).Then(Transform.Translation(0, 5, 0)));

        var text = Step.ToStepString(file, "Array");
        Assert.Equal(1, Count(text, "MANIFOLD_SOLID_BREP"));
        Assert.Equal(5, Count(text, "NEXT_ASSEMBLY_USAGE_OCCURRENCE"));
        Assert.Contains("'Fan 3'", text);

        var bounds = file.Bounds();
        Assert.Equal(-1, bounds.Min.X, 9);
        Assert.Equal(10, bounds.Max.X, 9);
    }

    [Fact]
    public void NonRigidInstanceIsRejected()
    {
        var file = new StepFile();
        var p = new StepPart("p");
        p.Add(new Rectangle("b", 0, 0, 0, 1, 1, 1));
        file.AddInstance(p, Transform.Scale(2));
        Assert.Throws<InvalidOperationException>(() => Step.ToStepString(file, "x"));
        Assert.Contains(file.Validate(), s => s.Contains("placement"));
    }

    [Fact]
    public void EmptyPartsAreSkipped()
    {
        var file = new StepFile();
        file.AddPart("Empty");
        file.AddPart("Real").Add(new Rectangle("b", 0, 0, 0, 1, 1, 1));
        var text = Step.ToStepString(file, "x");
        Assert.Equal(1, Count(text, "NEXT_ASSEMBLY_USAGE_OCCURRENCE"));
        Assert.DoesNotContain("'Empty'", text);
    }

    [Fact]
    public void NamesAreEscaped()
    {
        var file = new StepFile();
        file.AddPart("Bob's \\ part ø").Add(new Rectangle("b", 0, 0, 0, 1, 1, 1));
        var text = Step.ToStepString(file, "x");
        Assert.Contains("PRODUCT('Bob''s \\\\ part \\X2\\00F8\\X0\\'", text);
        Assert.All(text, c => Assert.True(c < 128));
    }

    [Fact]
    public void InvalidShapeErrorNamesThePart()
    {
        var file = new StepFile();
        file.AddPart("Door").Add(Extrusion.AlongZ("skin", 0, 1, Profiles.Rectangle(0, 0, 1, 1)).AddCircularCut(9, 9, 1));
        var ex = Assert.Throws<InvalidOperationException>(() => Step.ToStepString(file, "x"));
        Assert.Contains("Door", ex.Message);
        Assert.NotEmpty(file.Validate());

        file.parts[0].Add(new Rectangle("frame", 0, 0, 0, 1, 1, 1));
        var lenient = new StepWriteOptions { SkipInvalidShapes = true };
        var text = Step.ToStepString(file, "x", lenient);
        Assert.Equal(1, Count(text, "MANIFOLD_SOLID_BREP"));
        Assert.Single(lenient.SkippedShapes);
    }

    [Fact]
    public void WritesFileToDisk()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sculptor-{Guid.NewGuid():N}.step");
        try
        {
            Step.WriteStep(path, Sample(), "Widget");
            Sample().WriteStep(path, "Widget");
            var text = File.ReadAllText(path);
            Assert.StartsWith("ISO-10303-21;", text);
            Assert.EndsWith("END-ISO-10303-21;\n", text);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void NumbersUseInvariantCulture()
    {
        var prev = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var text = Step.ToStepString(Sample(), "x");
            Assert.Contains("(5.0,5.0,0.03125)", text);
        }
        finally { CultureInfo.CurrentCulture = prev; }
    }
}
