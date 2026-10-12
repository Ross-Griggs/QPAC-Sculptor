using QPAC.Sculptor;
using Xunit;

namespace QPAC.Sculptor.Tests;

public class ShapeTests
{
    private static Polyhedron Solid(StepShape shape)
    {
        var p = shape.ToPolyhedron();
        Assert.Empty(p.Validate());
        return p;
    }

    private static double PolygonArea(int n, double r) => 0.5 * n * r * r * Math.Sin(2 * Math.PI / n);

    private static void Near(double expected, double actual, double relTol)
        => Assert.True(Math.Abs(actual - expected) <= relTol * Math.Abs(expected), $"expected {expected}, got {actual}");

    [Fact]
    public void Box()
    {
        Near(24, Solid(new Rectangle("b", 1, 2, 3, 2, 3, 4)).Volume, 1e-12);
    }

    [Fact]
    public void OrientedBoxKeepsVolume()
    {
        var b = new Rectangle("b", 0, 0, 0, 2, 3, 4).Oriented((0, 1, 0), (-1, 0, 0), (0, 0, 1));
        Near(24, Solid(b).Volume, 1e-12);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FilletedAndChamferedBox(bool chamfer)
    {
        var box = new Rectangle("b", 0, 0, 0, 4, 6, 8);
        if (chamfer) box.WithChamfer(0.5); else box.WithFillet(0.5, 8);
        double a = 3, b = 5, c = 7, r = 0.5;
        double exact = chamfer
            ? 4 * 6 * 8 - 4 * (r * r / 2) * (4 + 6 + 8) + 8 * (r * r * r / 6) * 2
            : a * b * c + 2 * r * (a * b + a * c + b * c) + Math.PI * r * r * (a + b + c) + 4.0 / 3 * Math.PI * r * r * r;
        Near(exact, Solid(box).Volume, chamfer ? 0.002 : 0.01);
    }

    [Fact]
    public void FullyRoundedBoxWeldsCleanly()
    {
        Solid(new Rectangle("pill", 0, 0, 0, 2, 2, 2).WithFillet(1, 6));
    }

    [Fact]
    public void CylinderMatchesPolygonPrism()
    {
        var cyl = new Cylinder("c", 0, 0, 0, 1, 2, 2, 0.5, 24);
        Near(PolygonArea(24, 0.5) * 3, Solid(cyl).Volume, 1e-9);
    }

    [Fact]
    public void FilletedCylinderIsSmaller()
    {
        var cyl = new Cylinder("c", 0, 0, 0, 0, 0, 3, 1, 32).WithFillet(0.2, 6);
        double v = Solid(cyl).Volume;
        Assert.InRange(v, PolygonArea(32, 1) * 3 * 0.95, PolygonArea(32, 1) * 3);
    }

    [Fact]
    public void FrustumMatchesPolygonFormula()
    {
        double a1 = PolygonArea(32, 2), a2 = PolygonArea(32, 1);
        Near(5.0 / 3 * (a1 + a2 + Math.Sqrt(a1 * a2)), Solid(new Cone("k", 0, 0, 0, 5, 2, 1)).Volume, 1e-9);
    }

    [Fact]
    public void ConeBetweenPointsIsPlacedAlongAxis()
    {
        var k = Cone.Between("k", (1, 1, 1), (1, 1, 11), 2, 1);
        var p = Solid(k);
        Near(1, p.Min.Z, 1e-9);
        Near(11, p.Max.Z, 1e-9);
    }

    [Fact]
    public void SphereAndTorus()
    {
        Near(4.0 / 3 * Math.PI * 8, Solid(new Sphere("s", 0, 0, 0, 2, 48, 24)).Volume, 0.02);
        Near(2 * Math.PI * Math.PI * 3 * 0.25, Solid(new Torus("t", (0, 0, 0), (0, 0, 1), 3, 0.5, 64, 24)).Volume, 0.02);
    }

    [Fact]
    public void TubeIsHollow()
    {
        Near((PolygonArea(32, 2) - PolygonArea(32, 1.5)) * 4, Solid(new Tube("t", (0, 0, 0), (4, 0, 0), 2, 1.5)).Volume, 1e-9);
    }

    [Fact]
    public void PartialRevolveHasEndFaces()
    {
        var r = new Revolve("r", (0, 0, 0), (0, 0, 1), new (double R, double Z)[] { (1, 0), (2, 0), (2, 1), (1, 1) }, 90, 64);
        Near(Math.PI / 4 * (4 - 1), Solid(r).Volume, 0.002);
    }

    [Fact]
    public void LegacyExtrusionWithCircularCut()
    {
        var ex = new Extrusion("plate", (0, 0, 0), (1, 0, 0), (0, 1, 0), 0.5).AddCircle(0, 0, 2, 64).AddCircularCut(0, 0, 1, 64);
        Near((PolygonArea(64, 2) - PolygonArea(64, 1)) * 0.5, Solid(ex).Volume, 1e-9);
    }

    [Fact]
    public void NegativeDepthExtrudesBackward()
    {
        var ex = new Extrusion("e", (0, 0, 0), (1, 0, 0), (0, 1, 0), -2).AddRectangle(0, 0, 2, 2);
        var p = Solid(ex);
        Near(8, p.Volume, 1e-12);
        Near(-2, p.Min.Z, 1e-12);
    }

    [Fact]
    public void HolePatterns()
    {
        var ex = Extrusion.AlongZ("flange", 0, 0.25, Profiles.Circle(0, 0, 6, 128))
            .AddCircularCut(0, 0, 3, 64)
            .AddBoltCircle(0, 0, 4.5, 8, 0.25, 22.5, 24);
        Near((PolygonArea(128, 6) - PolygonArea(64, 3) - 8 * PolygonArea(24, 0.25)) * 0.25, Solid(ex).Volume, 1e-9);

        var grid = Extrusion.AlongZ("perf", 0, 0.1, Profiles.Rectangle(0, 0, 10, 10)).AddHoleGrid(-4, -4, 5, 5, 2, 2, 0.3, 16);
        Near((100 - 25 * PolygonArea(16, 0.3)) * 0.1, Solid(grid).Volume, 1e-9);
    }

    [Fact]
    public void BlindPocketsFromBothSides()
    {
        var ex = Extrusion.AlongZ("block", 0, 2, Profiles.Rectangle(0, 0, 10, 10))
            .AddRectangularPocket(-2, 0, 3, 3, 0.5)
            .AddCircularPocket(2, 0, 1, 1.25, fromBack: true, sides: 32);
        Near(200 - 9 * 0.5 - PolygonArea(32, 1) * 1.25, Solid(ex).Volume, 1e-9);
    }

    [Fact]
    public void ShellLeavesWallsAndFloor()
    {
        var tray = Extrusion.AlongZ("tray", 0, 3, Profiles.Rectangle(0, 0, 10, 8)).Shell(0.25, 0.5);
        Near(240 - 9.5 * 7.5 * 2.5, Solid(tray).Volume, 1e-9);
    }

    [Fact]
    public void ShellWithZeroFloorIsATube()
    {
        var duct = Extrusion.AlongZ("duct", 0, 10, Profiles.Rectangle(0, 0, 4, 4)).Shell(0.1, 0);
        Near((16 - 3.8 * 3.8) * 10, Solid(duct).Volume, 1e-9);
    }

    [Fact]
    public void ThroughHoleInsidePocketFloor()
    {
        var ex = Extrusion.AlongZ("tray", 0, 2, Profiles.Rectangle(0, 0, 10, 10))
            .Shell(0.5, 0.5)
            .AddCircularCut(0, 0, 1, 32);
        double expected = 200 - 81 * 1.5 - PolygonArea(32, 1) * 0.5;
        Near(expected, Solid(ex).Volume, 1e-9);
    }

    [Fact]
    public void ShelledTrayWithDrainHoleAndRoundedBase()
    {
        // Pocket floors wind opposite to cap faces; orientation must not depend on face order.
        var tray = Extrusion.AlongZ("tray", 0, 1.5, Profiles.RoundedRectangle(0, 0, 6, 4, 0.5))
            .Shell(0.15, 0.2).AddCircularCut(0, 0, 0.4, 32).FilletEdges(0.05, 3, front: false);
        Solid(tray);
    }

    [Fact]
    public void PocketGridLeavesRibs()
    {
        var ex = Extrusion.AlongZ("ribbed", 0, 2, Profiles.Rectangle(0, 0, 10, 10)).AddPocketGrid(0.5, 0.5, 0.5, 3, 3);
        double cell = (9 - 2 * 0.5) / 3;
        Near(200 - 9 * cell * cell * 1.5, Solid(ex).Volume, 1e-9);
    }

    [Fact]
    public void PocketGridOnRoundOutline()
    {
        var ex = Extrusion.AlongZ("disc", 0, 1, Profiles.Circle(0, 0, 5, 64)).AddPocketGrid(0.4, 0.3, 0.3, 4, 4);
        Assert.True(ex.Pockets.Count > 8);
        Solid(ex);
    }

    [Fact]
    public void FilletedAndChamferedExtrusionEdges()
    {
        var plain = Extrusion.AlongZ("p", 0, 1, Profiles.RoundedRectangle(0, 0, 6, 4, 0.5)).AddCircularCut(0, 0, 1, 48);
        double v0 = Solid(plain).Volume;

        var filleted = Extrusion.AlongZ("f", 0, 1, Profiles.RoundedRectangle(0, 0, 6, 4, 0.5)).AddCircularCut(0, 0, 1, 48).FilletEdges(0.2, 6);
        double v1 = Solid(filleted).Volume;
        Assert.InRange(v1, v0 * 0.95, v0 * 0.999);

        var chamfered = Extrusion.AlongZ("c", 0, 1, Profiles.Rectangle(0, 0, 6, 4)).ChamferEdges(0.1, back: false);
        Near(24 - 0.005 * 20 + 4 * 0.1 * 0.1 * 0.1 / 3, Solid(chamfered).Volume, 0.001);
    }

    [Fact]
    public void DraftTapersWalls()
    {
        var ex = Extrusion.AlongZ("d", 0, 1, Profiles.Rectangle(0, 0, 4, 4)).WithDraft(Math.Atan(0.5) * 180 / Math.PI);
        // Top is 3×3, bottom 4×4: frustum volume.
        Near(1.0 / 3 * (16 + 9 + 12), Solid(ex).Volume, 1e-9);
    }

    [Fact]
    public void FilletCornersOfOutline()
    {
        var ex = Extrusion.AlongZ("r", 0, 1, Profiles.Rectangle(0, 0, 4, 4)).FilletCorners(1, 16);
        Near(16 - (4 - Math.PI), Solid(ex).Volume, 0.002);
    }

    [Fact]
    public void SheetMetalSections()
    {
        foreach (var profile in new[]
        {
            Profiles.Channel(3, 1.5, 0.0625, 0.0625),
            Profiles.Angle(2, 2, 0.125, 0.125),
            Profiles.ZSection(1, 3, 0.0625, 0.03),
            Profiles.Channel(3, 1.5, 0.0625),
        })
        {
            var ex = Extrusion.AlongZ("rail", 0, 24, profile);
            Assert.True(Solid(ex).Volume > 0);
        }
        var angle = Extrusion.AlongZ("angle", 0, 10, Profiles.Angle(2, 2, 0.25));
        Near((2 * 0.25 + 1.75 * 0.25) * 10, Solid(angle).Volume, 1e-9);
    }

    [Fact]
    public void SlotAndPolygonCuts()
    {
        var ex = Extrusion.AlongZ("bracket", 0, 0.25, Profiles.Rectangle(0, 0, 6, 3))
            .AddSlotCut(-2, 0, -1, 0, 0.4)
            .AddCut(Profiles.RegularPolygon(1.5, 0, 0.5, 6));
        Solid(ex);
    }

    [Fact]
    public void StraightSweepMatchesPrism()
    {
        var rod = Sweep.Rod("rod", new (double, double, double)[] { (0, 0, 0), (0, 0, 5) }, 0.5, sides: 20);
        Near(PolygonArea(20, 0.5) * 5, Solid(rod).Volume, 1e-9);
    }

    [Fact]
    public void BentSweepFollowsPappus()
    {
        var path = new (double, double, double)[] { (0, 0, 0), (10, 0, 0), (10, 10, 0), (10, 10, 10) };
        var harness = Sweep.Rod("wire", path, 0.25, bendRadius: 2, sides: 16);
        harness.BendSegments = 16;
        var p = Solid(harness);
        // Each 90° bend of radius 2 trims 2 off both neighbouring legs and adds a quarter circle.
        double centerline = 30 - 2 * (2 * 2) + 2 * (Math.PI / 2 * 2);
        Near(PolygonArea(16, 0.25) * centerline, p.Volume, 0.01);
    }

    [Fact]
    public void SharpCornerSweepIsValid()
    {
        var path = new (double, double, double)[] { (0, 0, 0), (5, 0, 0), (5, 5, 2) };
        Solid(new Sweep("bar", Profiles.Rectangle(0, 0, 1, 0.5), path));
    }

    [Fact]
    public void SweepRejectsReversal()
    {
        var path = new (double, double, double)[] { (0, 0, 0), (5, 0, 0), (0, 0.01, 0) };
        Assert.Throws<ArgumentException>(() => new Sweep("bad", Profiles.Rectangle(0, 0, 1, 1), path).ToPolyhedron());
    }

    [Fact]
    public void Lofts()
    {
        var transition = Loft.SquareToRound("transition", (0, 0, 0), 12, 12, 10, 8);
        double v = Solid(transition).Volume;
        Assert.InRange(v, Math.PI * 25 * 8, 144 * 8);

        var reducer = new Loft("reducer", (0, 0, 0), (1, 0, 0), (0, 1, 0), 6, Profiles.Rectangle(0, 0, 8, 8), Profiles.Rectangle(0, 0, 4, 4)) { TopOffset = (1, 1) };
        Near(6.0 / 3 * (64 + 16 + 32), Solid(reducer).Volume, 1e-9);
    }

    [Fact]
    public void MirroredShapeStaysOutward()
    {
        var ex = Extrusion.AlongZ("m", 0, 1, Profiles.Polygon((0, 0), (3, 0), (0, 2))).MirrorAcross((0, 0, 0), (1, 0, 0));
        var p = Solid(ex);
        Near(3, p.Volume, 1e-12);
        Assert.True(p.Max.X <= 1e-12);
    }

    [Fact]
    public void InvalidGeometryExplainsItself()
    {
        var outside = Extrusion.AlongZ("plate", 0, 1, Profiles.Rectangle(0, 0, 2, 2)).AddCircularCut(5, 5, 1);
        var ex = Assert.Throws<ArgumentException>(() => outside.ToPolyhedron());
        Assert.Contains("plate", ex.Message);

        var tooDeep = Extrusion.AlongZ("p", 0, 1, Profiles.Rectangle(0, 0, 2, 2)).AddRectangularPocket(0, 0, 1, 1, 1.5);
        Assert.Throws<ArgumentException>(() => tooDeep.ToPolyhedron());

        var bigFillet = Extrusion.AlongZ("p", 0, 1, Profiles.Rectangle(0, 0, 2, 2)).FilletEdges(0.75);
        Assert.Throws<ArgumentException>(() => bigFillet.ToPolyhedron());
    }

    [Fact]
    public void DegenerateShapesAreEmptyNotErrors()
    {
        Assert.Empty(new Cylinder("z", 0, 0, 0, 0, 0, 0, 1).ToPolyhedron().Faces);
        Assert.Empty(new Extrusion("z", (0, 0, 0), (1, 0, 0), (0, 1, 0), 0).AddCircle(0, 0, 1).ToPolyhedron().Faces);
        Assert.Empty(new Rectangle("z", 0, 0, 0, 0, 1, 1).ToPolyhedron().Faces);
    }

    [Fact]
    public void CustomMeshSolid()
    {
        var m = new Polyhedron();
        int a = m.AddVertex((0, 0, 0)), b = m.AddVertex((1, 0, 0)), c = m.AddVertex((0, 1, 0)), d = m.AddVertex((0, 0, 1));
        m.AddFace(new[] { a, b, c });
        m.AddFace(new[] { a, b, d });
        m.AddFace(new[] { b, c, d });
        m.AddFace(new[] { c, a, d });
        Near(1.0 / 6, Solid(new MeshSolid("tet", m)).Volume, 1e-12);
    }
}
