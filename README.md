# QPAC.Sculptor

A lightweight .NET library for building 3D solids in code and exporting them to **STEP**
(ISO 10303-21, AP214) files that open in SolidWorks, Inventor, Fusion, FreeCAD and any other CAD
package.

- Primitives: box, cylinder, cone/frustum, sphere, torus, tube.
- Profile operations: extrude, revolve, sweep along a path, loft between two profiles.
- Extrusion features: through-cuts, blind pockets from either face, shelling, rib grids,
  filleted or chamfered edges, draft, bolt circles, hole grids, slots.
- Profiles: rectangles, rounded rectangles, circles, ellipses, polygons, slots, arcs, and
  sheet-metal sections (channel, angle, Z) with bend radii; fillet / chamfer / offset any profile.
- Transforms: rotate, translate, mirror, place; instance one part many times.
- Colours, inch or metric output, and validation with readable error messages.

Targets `net7.0` and `netstandard2.0`. The WPF preview lives in
[`QPAC.Sculptor.Wpf`](https://github.com/Ross-Griggs/QPAC.Sculptor.Wpf).

**New here? Read [Building STEP files from scratch](docs/QPAC.Sculptor-Walkthrough.pdf)**, a
function-by-function walkthrough with a tested example and a rendered result for every function.
It is generated from [`docs/walkthrough`](docs/walkthrough/README.md); rebuild it after API changes.

## Install

The package is published to the private **qpacsystems** GitHub Packages feed. With that feed in
your NuGet sources (`QPACSource` on Q-PAC machines):

```
dotnet add package QPAC.Sculptor --version 1.1.0
```

## Quick start

```csharp
using QPAC.Sculptor;

var file = new StepFile();                                   // units: inches by default
var panel = file.AddPart(new StepPart("Fan panel", StepColor.SheetMetal));

panel.Add(Extrusion.AlongZ("Plate", 0, 0.125, Profiles.RoundedRectangle(0, 0, 24, 24, 0.5))
    .AddCircularCut(0, 0, 9.5, 128)                          // fan opening
    .AddBoltCircle(0, 0, 10.5, 8, 0.1875, startDegrees: 22.5) // mounting holes
    .ChamferEdges(0.02));

panel.Add(new Cylinder("Stud", 11, 11, 0, 11, 11, 1, 0.125));

file.WriteStep("panel.step", "Fan panel");
```

## Units

Every coordinate is in `StepFile.Units`, which defaults to **inches**. The written file says so
in three places: a comment at the top, the `FILE_DESCRIPTION`, and the unit entity the geometry
uses (`CONVERSION_BASED_UNIT('INCH', …)`).

You'll also see `SI_UNIT(.MILLI.,.METRE.)` in an inch file. That isn't a second unit: STEP can only
define an inch as "25.4 millimetres", so the millimetre entity appears as the base of the inch
definition. CAD tools read the file as inches.

To hand a metric file to someone, convert on export (the model itself stays in inches):

```csharp
file.WriteStep("panel-mm.step", "Fan panel", new StepWriteOptions { ExportUnits = LengthUnit.Millimeter });
```

## Shapes

| Shape | Use it for |
|---|---|
| `Rectangle(name, cx, cy, cz, sx, sy, sz)` | Boxes. `.WithFillet(r)` / `.WithChamfer(d)` round or bevel all 12 edges. `Rectangle.FromCorners`. |
| `Cylinder(name, start, end, radius)` | Rods and pins between two points. `.WithFillet(r)` rounds both rims. |
| `Cone(name, cx, cy, cz, height, r1, r2)` | Frustums on +Z; `Cone.Between(a, b, r1, r2)` for any direction. |
| `Sphere`, `Torus`, `Tube` | Balls, rings and gaskets, pipe and sleeves (`InnerRadius` 0 = solid). |
| `Extrusion(name, origin, u, v, depth)` | Any 2D profile pushed along U×V (see below). `Extrusion.AlongZ` is the short form. |
| `Revolve(name, origin, axis, (R, Z) profile, angle)` | Lathe parts: inlet bells, flanges, hubs. Partial angles leave flat ends. `.FilletProfile(r)`. |
| `Sweep(name, profile, path, bendRadius)` | Wire harnesses, piping, handrails, formed rod. `Sweep.Rod(...)` for round stock. |
| `Loft(name, origin, u, v, height, bottom, top)` | Square-to-round transitions, reducers, offset ducts (`TopOffset`). `Loft.SquareToRound`. |
| `MeshSolid(name, polyhedron)` | Any closed polyhedron you build yourself. |

Every shape takes `.RotateAbout(point, axis, degrees)`, `.Translate(dx, dy, dz)`,
`.MirrorAcross(point, normal)`, `.TransformBy(transform)` and `.WithColor(color)`, and they chain:
`new Cone(...).RotateAbout(...)` is still a `Cone`.

### Extrusion features

```csharp
var tray = Extrusion.AlongZ("Tray", 0, 2, Profiles.RoundedRectangle(0, 0, 10, 6, 1))
    .Shell(wallThickness: 0.125, floorThickness: 0.25)       // hollow it out, open on top
    .AddCircularCut(0, 0, 0.5)                               // drain hole through the floor
    .FilletEdges(0.0625, front: false);                      // round the bottom edges

var ribbed = Extrusion.AlongZ("Ribbed plate", 0, 1, Profiles.Rectangle(0, 0, 12, 8))
    .AddPocketGrid(wallThickness: 0.25, floorThickness: 0.125, ribThickness: 0.125, columns: 4, rows: 3);

var boss = Extrusion.AlongZ("Boss", 0, 2, Profiles.Circle(0, 0, 1.5))
    .WithDraft(3)                                            // walls taper 3°
    .AddCircularPocket(0, 0, 0.5, depth: 0.75, fromBack: true);
```

| Method | Effect |
|---|---|
| `AddCut(loop)`, `AddCircularCut`, `AddRectangularCut`, `AddSlotCut` | Through-holes. |
| `AddBoltCircle`, `AddHoleGrid` | Hole patterns. |
| `AddPocket(loop, depth, fromBack)`, `AddCircularPocket`, `AddRectangularPocket` | Blind pockets that leave a floor; holes inside a pocket go through its floor. |
| `Shell(wall, floor, openFront)` | Turns the solid into a tray (floor 0 = open tube). |
| `AddPocketGrid(wall, floor, rib, cols, rows)` | Shell that leaves stiffening ribs (convex outlines). |
| `FilletCorners(r)` | Rounds the edges that run along the extrusion. |
| `FilletEdges(r, front, back)` / `ChamferEdges(d, front, back)` | Rounds or bevels the edges around either cap face. |
| `WithDraft(degrees)` | Tapers the walls. |

Geometry that cannot make a valid solid (a hole outside the plate, a fillet too big for the
profile, overlapping pockets) throws an `ArgumentException` naming the shape and the problem,
instead of quietly writing a broken file. `StepFile.Validate()` lists every problem without
throwing, and `StepWriteOptions.SkipInvalidShapes` exports everything else and reports what it
left out.

### Profiles

`Profiles` builds closed `(U, V)` loops for extrusions, sweeps and lofts:
`Rectangle`, `RoundedRectangle`, `Circle`, `Ellipse`, `RegularPolygon`, `Slot`, `Arc`, `Polygon`,
`Channel`, `Angle`, `ZSection`, `ThickPolyline` (any sheet-metal section from a centreline), and
the operations `Fillet`, `FilletCorners`, `Chamfer`, `Offset`, `Translate`, `Rotate`, `Scale`,
`MirrorU`, `MirrorV`, `Area`.

```csharp
var rail = new Extrusion("Rail", (0, 0, 0), (0, 1, 0), (0, 0, 1), 48)
    .SetProfile(Profiles.Channel(width: 3, flangeHeight: 1.5, thickness: 0.0625, insideBendRadius: 0.0625));
```

## Parts, colours and instances

Each `StepPart` becomes one component in the CAD assembly tree. Colours apply per part
(`new StepPart("Door", StepColor.SheetMetal)`) or per shape (`.WithColor(...)`); `StepColor`
includes Q-PAC presets (`SheetMetal`, `Galvanized`, `Fan`, `Controller`, `Channel`, `Mount`,
`Harness`, …) and `StepColor.FromHex("#1565B0")`.

Repeated components (a fan array) should be **instances**. The geometry is written once and placed
many times, so the file is much smaller and CAD counts them as one part:

```csharp
var fan = new StepPart("Fan", StepColor.Fan);
fan.Add(new Cylinder("Hub", 0, 0, 0, 0, 0, 4, 3));
for (int row = 0; row < 3; row++)
    for (int col = 0; col < 4; col++)
        file.AddInstance(fan, col * 30, row * 30, 0, $"Fan {row * 4 + col + 1}");
```

Instance placements must be rigid (rotation + translation). To mirror or scale, transform the
shapes themselves.

## Measuring and checking

```csharp
double cubicInches = part.ComputeVolume();     // e.g. for weights: × density
var (min, max) = file.Bounds();
List<string> problems = file.Validate();
Polyhedron solid = shape.ToPolyhedron();       // vertices + planar faces, world coordinates
```

## Repository layout

```
src/QPAC.Sculptor/          the library
  Model/                    StepFile, StepPart, StepInstance, StepColor, LengthUnit
  Shapes/                   every shape + EdgeFinish
  Geometry/                 Transform, Polyhedron, Profiles, Tessellator (+ internal helpers)
  Export/                   Step (public entry point), StepWriter, StepWriteOptions
tests/QPAC.Sculptor.Tests/  xUnit tests (volumes, manifold checks, STEP output)
samples/QPAC.Sculptor.Gallery/  writes a STEP file with one of everything
tools/validate-step.py      checks a STEP file with OpenCascade (pip install cadquery-ocp)
```

Try the gallery and check its output:

```
dotnet run --project samples/QPAC.Sculptor.Gallery -- out
python tools/validate-step.py out/gallery.step
```

## Releasing

1. Bump `<Version>` in `src/QPAC.Sculptor/QPAC.Sculptor.csproj`.
2. Push to `master`. CI runs the tests, packs, and pushes that version to the qpacsystems feed
   (a version that already exists is skipped). It needs the repository secret
   `QPAC_PACKAGES_PAT`: a classic PAT with `write:packages`.
3. Release `QPAC.Sculptor.Wpf` after the core version it depends on is on the feed.

`AssemblyVersion` stays at `1.0.0.0` across all 1.x packages on purpose. Consumers compiled
against one version must keep binding to another at runtime. The assembly file is named
`QPAC Sculptor.dll` for the same reason.

## License

MIT
