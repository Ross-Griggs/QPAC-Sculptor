using QPAC.Sculptor;
using Xunit;

namespace QPAC.Sculptor.Tests;

public class GeometryTests
{
    private static double TriangleArea(Polyhedron p, (int A, int B, int C) t, (double X, double Y, double Z) normal)
    {
        var a = p.Vertices[t.A]; var b = p.Vertices[t.B]; var c = p.Vertices[t.C];
        var ab = (b.X - a.X, b.Y - a.Y, b.Z - a.Z);
        var ac = (c.X - a.X, c.Y - a.Y, c.Z - a.Z);
        var cr = (ab.Item2 * ac.Item3 - ab.Item3 * ac.Item2, ab.Item3 * ac.Item1 - ab.Item1 * ac.Item3, ab.Item1 * ac.Item2 - ab.Item2 * ac.Item1);
        return 0.5 * (cr.Item1 * normal.X + cr.Item2 * normal.Y + cr.Item3 * normal.Z);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void TriangulationCoversEveryFaceExactly(int which)
    {
        StepShape shape = which switch
        {
            0 => Extrusion.AlongZ("plate", 0, 1, Profiles.Channel(4, 2, 0.25, 0.25)),
            1 => Extrusion.AlongZ("perf", 0, 1, Profiles.Rectangle(0, 0, 10, 6)).AddHoleGrid(-4, -2, 5, 3, 2, 2, 0.4).AddSlotCut(-4, 2.6, 4, 2.6, 0.3),
            2 => Extrusion.AlongZ("tray", 0, 2, Profiles.RoundedRectangle(0, 0, 10, 6, 1)).Shell(0.25, 0.5).AddCircularCut(2, 0, 0.5).AddCircularCut(-2, 0, 0.5),
            // Rectangular holes on a grid share coordinates: bridges must not run through other holes' corners.
            3 => Extrusion.AlongZ("ribbed", 0, 1, Profiles.Rectangle(0, 0, 12, 8)).AddPocketGrid(0.25, 0.125, 0.125, 4, 3, openFront: false),
            _ => Extrusion.AlongZ("louvres", 0, 0.1, Profiles.Rectangle(0, 0, 20, 12)).AddHoleGrid(-8, -4, 9, 5, 2, 2, 0.5, 4),
        };
        var p = shape.ToPolyhedron();
        foreach (var f in p.Faces)
        {
            var n = p.FaceNormal(f);
            double faceArea = f.Loops.Sum(l => 0.5 * Dot(p.LoopNormal(l), n));
            var tris = Tessellator.Triangulate(p, f);
            Assert.All(tris, t => Assert.True(TriangleArea(p, t, n) > -1e-12, "triangle wound backwards"));
            Assert.Equal(faceArea, tris.Sum(t => TriangleArea(p, t, n)), 9);
        }
    }

    private static double Dot((double X, double Y, double Z) a, (double X, double Y, double Z) b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    [Fact]
    public void TriangulationSurvivesManyHoleLayouts()
    {
        var shapes = new List<StepShape>();
        for (int cols = 1; cols <= 6; cols++)
            for (int rows = 1; rows <= 4; rows++)
                shapes.Add(Extrusion.AlongZ("grid", 0, 1, Profiles.Rectangle(0, 0, 12, 8)).AddPocketGrid(0.25, 0.1, 0.2, cols, rows));

        var staggered = Extrusion.AlongZ("staggered", 0, 0.1, Profiles.Rectangle(0, 0, 20, 12));
        for (int r = 0; r < 4; r++)
            for (int c = 0; c < 6; c++)
                staggered.AddRectangularCut(-7.5 + c * 3 + (r % 2) * 1.5, -4.5 + r * 3, 1, 1);
        shapes.Add(staggered);

        var mixed = new Extrusion("mixed", (0, 0, 0), (1, 0, 0), (0, 0, 1), 0.1).SetProfile(Profiles.RoundedRectangle(0, 0, 20, 12, 2));
        for (int c = 0; c < 5; c++)
        {
            mixed.AddRectangularCut(-8 + c * 4, 3, 2, 2);
            mixed.AddCircularCut(-8 + c * 4, -3, 1, 6 + c);
            mixed.AddSlotCut(-9 + c * 4, 0, -7 + c * 4, 0, 0.6);
        }
        shapes.Add(mixed.RotateAbout((0, 0, 0), (1, 1, 1), 33));

        foreach (var shape in shapes)
        {
            var p = shape.ToPolyhedron();
            foreach (var f in p.Faces)
            {
                var n = p.FaceNormal(f);
                double faceArea = f.Loops.Sum(l => 0.5 * Dot(p.LoopNormal(l), n));
                var tris = Tessellator.Triangulate(p, f);
                Assert.All(tris, t => Assert.True(TriangleArea(p, t, n) > -1e-12, $"{shape.ShapeName}: triangle wound backwards"));
                Assert.Equal(faceArea, tris.Sum(t => TriangleArea(p, t, n)), 8);
            }
        }
    }

    [Fact]
    public void TransformInverseAndComposition()
    {
        var t = Transform.RotationAbout((1, 2, 3), (1, 1, 0), 37).Then(Transform.Translation(4, -2, 9));
        Assert.True(t.IsRigid);
        var p = (3.0, -1.0, 7.0);
        var back = t.Inverse().Apply(t.Apply(p));
        Assert.Equal(p.Item1, back.X, 9);
        Assert.Equal(p.Item2, back.Y, 9);
        Assert.Equal(p.Item3, back.Z, 9);

        Assert.False(Transform.Mirror((0, 0, 0), (0, 0, 1)).IsRigid);
        Assert.False(Transform.Scale(2).IsRigid);
    }

    [Fact]
    public void PlacementBuildsOrthonormalFrame()
    {
        var t = Transform.Placement((5, 0, 0), (0, 1, 0), (1, 0, 0));
        Assert.True(t.IsRigid);
        var z = t.ApplyDirection((0, 0, 1));
        Assert.Equal(1, z.X, 12);
        var o = t.Apply((0, 0, 0));
        Assert.Equal(5, o.X, 12);
    }

    [Fact]
    public void ProfileOperations()
    {
        var sq = Profiles.Rectangle(0, 0, 2, 2);
        Assert.Equal(4, Profiles.Area(sq), 12);
        Assert.Equal(9, Profiles.Area(Profiles.Offset(sq, 0.5)), 12);
        Assert.Equal(1, Profiles.Area(Profiles.Offset(sq, -0.5)), 12);
        Assert.Equal(4 - 4 * 0.125, Profiles.Area(Profiles.Chamfer(sq, 0.5)), 12);
        Assert.Equal(4 - (4 - Math.PI) * 0.25, Profiles.Area(Profiles.Fillet(sq, 0.5, 64)), 3);
        Assert.Equal(4, Profiles.Area(Profiles.MirrorU(Profiles.Translate(sq, 3, 0))), 12);
        Assert.Throws<ArgumentException>(() => Profiles.Offset(sq, -1.5));
    }

    [Fact]
    public void ColorsParse()
    {
        Assert.Equal(new StepColor(0x15, 0x65, 0xB0), StepColor.FromHex("#1565B0"));
        Assert.Equal("#A0AEB8", StepColor.SheetMetal.ToHex());
        Assert.Throws<FormatException>(() => StepColor.FromHex("nope"));
    }

    [Fact]
    public void SurfaceAreaOfBox()
    {
        var p = new Rectangle("b", 0, 0, 0, 1, 2, 3).ToPolyhedron();
        Assert.Equal(22, p.SurfaceArea, 12);
    }

    [Fact]
    public void LegacyApiStillWorks()
    {
        var file = new StepFile();
        var part = file.AddPart(new StepPart("Legacy") { Type = "Fan" });
        part.Add(new Rectangle("r", 0, 0, 0, 1, 1, 1)).RotateAbout((0, 0, 0), (0, 0, 1), 45);
        part.Add(new Cone("c", 0, 0, 0, 1, 1, 0.5));
        part.Add(new Cylinder("y", 0, 0, 0, 1, 0, 0, 0.1));
        var ex = part.Add(new Extrusion("e", (0, 0, 2), (1, 0, 0), (0, 1, 0), 0.1));
        ex.Outer = Extrusion.Circle(0, 0, 1);
        ex.AddCircularCut(0, 0, 0.5, 32);
        part.RotateAbout((0, 0, 0), (1, 0, 0), 10);
        Assert.Equal(1, file.Count);
        Assert.Equal(4, file.parts[0].shapes.Count);
        Assert.Empty(file.Validate());
    }
}
