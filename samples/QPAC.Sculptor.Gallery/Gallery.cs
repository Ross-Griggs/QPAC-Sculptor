using QPAC.Sculptor;

namespace QPAC.Sculptor.Gallery;

// One of everything, laid out on a grid in inches.
public static class Gallery
{
    public static StepFile Build()
    {
        var file = new StepFile { Description = "QPAC.Sculptor feature gallery", Author = "QPAC.Sculptor" };
        double col = 0;
        double Next() => (col += 8) - 8;

        var primitives = file.AddPart(new StepPart("Primitives", StepColor.SheetMetal));
        double x = Next();
        primitives.Add(new Rectangle("Box", x, 0, 1, 4, 3, 2));
        primitives.Add(new Rectangle("Filleted box", x, 6, 1, 4, 3, 2).WithFillet(0.4));
        primitives.Add(new Rectangle("Chamfered box", x, 12, 1, 4, 3, 2).WithChamfer(0.3));
        x = Next();
        primitives.Add(new Cylinder("Cylinder", x, 0, 0, x, 0, 3, 1.5, 48));
        primitives.Add(new Cylinder("Filleted cylinder", x, 6, 0, x, 6, 3, 1.5, 48).WithFillet(0.3));
        primitives.Add(new Cone("Cone", x, 12, 0, 3, 2, 0.5, 48).WithFillet(0.2));
        x = Next();
        primitives.Add(new Sphere("Sphere", x, 0, 2, 2));
        primitives.Add(new Torus("Torus", (x, 6, 0.6), (0, 0, 1), 2, 0.6));
        primitives.Add(new Tube("Pipe", (x - 2, 12, 1), (x + 2, 12, 1), 1, 0.85));

        var sheet = file.AddPart(new StepPart("Sheet metal", StepColor.Galvanized));
        x = Next();
        sheet.Add(Extrusion.AlongZ("Perforated panel", 0, 0.0625, Profiles.RoundedRectangle(x, 0, 6, 4, 0.25))
            .AddHoleGrid(x - 2.5, -1.5, 6, 4, 1, 1, 0.2, 24));
        sheet.Add(Extrusion.AlongZ("Fan plate", 0, 0.125, Profiles.Rectangle(x, 6, 6, 6))
            .AddCircularCut(x, 6, 2.2, 96)
            .AddBoltCircle(x, 6, 2.6, 8, 0.13, 22.5)
            .AddSlotCut(x - 2.6, 3.25, x + 2.6, 3.25, 0.3));
        sheet.Add(new Extrusion("Channel", (x - 3, 12, 0), (0, 1, 0), (0, 0, 1), 6).SetProfile(Profiles.Channel(3, 1.5, 0.0625, 0.0625)));

        var machined = file.AddPart(new StepPart("Machined", StepColor.Mount));
        x = Next();
        machined.Add(Extrusion.AlongZ("Tray", 0, 1.5, Profiles.RoundedRectangle(x, 0, 6, 4, 0.5)).Shell(0.15, 0.2).AddCircularCut(x, 0, 0.4).FilletEdges(0.05, 3, front: false));
        machined.Add(Extrusion.AlongZ("Ribbed plate", 0, 1, Profiles.Rectangle(x, 6, 6, 4)).AddPocketGrid(0.25, 0.2, 0.15, 3, 2));
        machined.Add(Extrusion.AlongZ("Drafted boss", 0, 2, Profiles.RoundedRectangle(x, 12, 4, 3, 0.5)).WithDraft(5).FilletEdges(0.2, 4, front: false)
            .AddRectangularPocket(x, 12, 1.5, 1, 0.5, 0.2, fromBack: true));

        var formed = file.AddPart(new StepPart("Formed", StepColor.Harness));
        x = Next();
        formed.Add(Sweep.Rod("Wire harness", new (double, double, double)[] { (x - 3, 0, 0.3), (x + 2, 0, 0.3), (x + 2, 2, 0.3), (x + 2, 2, 3) }, 0.2, bendRadius: 0.75));
        formed.Add(new Sweep("Handrail", Profiles.RoundedRectangle(0, 0, 0.6, 0.3, 0.1), new (double, double, double)[] { (x - 3, 5, 0), (x - 3, 5, 2), (x + 3, 5, 2), (x + 3, 5, 0) }, 0.5));
        formed.Add(new Revolve("Inlet bell", (x, 12, 0), (0, 0, 1), new (double R, double Z)[] { (1.0, 0), (2.4, 0), (2.4, 0.15), (1.2, 0.15), (1.15, 1.5), (1.0, 1.5) }).FilletProfile(0.06, 4, 1, 2));

        var hvac = file.AddPart(new StepPart("Transitions", StepColor.Insulation));
        x = Next();
        hvac.Add(Loft.SquareToRound("Square to round", (x, 0, 0), 5, 5, 3, 3));
        hvac.Add(new Loft("Offset reducer", (x, 7, 0), (1, 0, 0), (0, 1, 0), 3, Profiles.Rectangle(0, 0, 5, 4), Profiles.RoundedRectangle(0, 0, 3, 2, 0.3)) { TopOffset = (0.8, 0) });

        // One fan definition, many placed copies.
        var fan = new StepPart("Fan", StepColor.Fan);
        fan.Add(new Cylinder("Hub", 0, 0, 0, 0, 0, 1, 0.6, 32));
        for (int b = 0; b < 6; b++)
            fan.Add(new Rectangle($"Blade {b + 1}", 1.4, 0, 0.5, 1.6, 0.08, 0.8).WithFillet(0.03, 2)
                .RotateAbout((1.4, 0, 0.5), (1, 0, 0), 25)
                .RotateAbout((0, 0, 0), (0, 0, 1), 60 * b));
        x = Next();
        for (int i = 0; i < 3; i++)
            file.AddInstance(fan, Transform.RotationAbout((0, 0, 0), (0, 0, 1), 20 * i).Then(Transform.Translation(x, 6 * i, 0)), $"Fan {i + 1}");

        return file;
    }
}
