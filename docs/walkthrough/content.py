"""Text and examples for the QPAC.Sculptor walkthrough PDF.

Every `code` block below is compiled and executed by build.py (through harness/) before it is
printed, so the PDF can only show code that works. Kinds:
  model   - code that builds `file` (a StepFile) and writes it; the result is rendered in 3D
  profile - code that builds `profile` (a 2D loop); the loop is plotted
  code    - code that is run and whose console output is printed
  wpf     - WPF code; shown but compiled separately (needs Windows + the WPF package)
  text    - prose only
"""

VERSION = "1.1.1"

INTRO = [
    "This walkthrough builds STEP files from nothing, one function at a time. Each page explains "
    "a single function: what it is for, its parameters, a complete example, and a picture of the "
    "solid that example produces.",
    "Every example in this document was compiled and run against QPAC.Sculptor "
    f"{VERSION} to make its picture, so the code and the pictures always match. The STEP files "
    "the examples write were also opened with OpenCascade, an independent CAD kernel, to confirm "
    "they are valid solids.",
    "Lengths are in inches unless a page says otherwise. Angles are in degrees.",
]

SECTIONS = []


def section(title, intro, items):
    SECTIONS.append({"title": title, "intro": intro, "items": items})


# --------------------------------------------------------------------------------------------
section("Part 1 - From an empty project to a STEP file", [
    "These five steps are all you need to produce a valid STEP file. Later parts go through every "
    "shape and feature in detail.",
], [
    {"id": "s0", "kind": "text", "title": "Step 0 - Create a project and add the package",
     "what": "QPAC.Sculptor is a normal NuGet package published to the private qpacsystems GitHub "
             "Packages feed. Any .NET project can use it: console tools, web services, WPF apps "
             "or class libraries.",
     "shell": 'dotnet new console -n MyStepTool\n'
              'cd MyStepTool\n'
              'dotnet nuget add source "https://nuget.pkg.github.com/qpacsystems/index.json" ^\n'
              '    --name QPACSource --username <github-user> --password <token> --store-password-in-clear-text\n'
              f'dotnet add package QPAC.Sculptor --version {VERSION}',
     "notes": [
         "The token is a GitHub personal access token (classic) with read:packages. On Q-PAC "
         "machines the QPACSource feed is usually configured already; skip that line if it is.",
         "Reference an exact version, or 1.1.*. A reference of 1.0.* never picks up 1.1.0.",
         "The core package targets netstandard2.0 and net7.0, so it works from .NET Framework "
         "4.7.2 up through .NET 8 and later. Only the separate QPAC.Sculptor.Wpf package needs "
         "Windows.",
         "Add 'using QPAC.Sculptor;' to your file. Everything is in that namespace except StepFile, "
         "which is in the global namespace so that older compiled consumers keep working.",
     ]},
    {"id": "s1", "kind": "code", "title": "Step 1 - Create the file: StepFile",
     "what": "A StepFile is the whole document: one .step file. It holds the parts, the unit "
             "every coordinate is measured in, and a few header fields.",
     "signature": "new StepFile { Units, Author, Organization, Description }",
     "params": [
         ("Units", "LengthUnit the model is drawn in. Default Inch. Also Millimeter, Centimeter, Meter, Foot."),
         ("Author / Organization", "Written into the STEP header. Organization defaults to \"Q-PAC\"."),
         ("Description", "Header description. Defaults to empty; when it is empty the header uses \"<product name> - boundary representation\"."),
     ],
     "code": 'var file = new StepFile { Description = "My first model", Author = "R. Griggs" };\n'
             'Console.WriteLine($"Drawing in {file.Units}, {file.parts.Count} parts so far");',
     "notes": ["Decide the unit first and draw everything in it. Converting to another unit on "
               "export is a separate option (step 4)."]},
    {"id": "s2", "kind": "code", "title": "Step 2 - Add a part: AddPart / StepPart",
     "what": "A part is one component in the CAD assembly tree, such as a door, a fan or a panel. "
             "It holds one or more shapes. Each shape stays a separate solid; the shapes are not "
             "fused together.",
     "signature": "file.AddPart(new StepPart(name[, color]))   or   file.AddPart(name)",
     "params": [
         ("name", "Component name shown in the CAD tree."),
         ("color", "Optional StepColor for every shape in the part."),
         ("Type", "Optional free-text field for your own use (the WPF preview can colour by it)."),
     ],
     "code": 'var file = new StepFile();\n'
             'var panel = file.AddPart(new StepPart("Side panel", StepColor.SheetMetal) { Type = "Panel" });\n'
             'var bracket = file.AddPart("Bracket");\n'
             'Console.WriteLine(string.Join(", ", file.parts.Select(p => p.Name)));',
     "notes": ["A part with no shapes is simply left out of the file."]},
    {"id": "s3", "kind": "model", "title": "Step 3 - Add a shape: part.Add",
     "what": "Shapes are the solids. part.Add returns the shape you pass it, so you can keep "
             "configuring it in the same statement.",
     "signature": "part.Add(shape)  ->  the same shape",
     "code": 'var file = new StepFile();\n'
             'var part = file.AddPart(new StepPart("Block", StepColor.SheetMetal));\n'
             'part.Add(new Rectangle("Base", 0, 0, 0.5, 6, 4, 1));          // 6 x 4 x 1 box\n'
             'part.Add(new Cylinder("Peg", 2, 1, 1, 2, 1, 2.5, 0.375));     // peg standing on it\n'
             'file.WriteStep("block.step", "Block");'},
    {"id": "s4", "kind": "code", "title": "Step 4 - Write the file: WriteStep",
     "what": "Writes the model as an ISO 10303-21 (AP214) STEP file that SolidWorks, Inventor, "
             "Fusion, FreeCAD and other CAD tools open.",
     "signature": "file.WriteStep(path, productName[, options])\n"
                  "Step.WriteStep(path, file, productName[, options])\n"
                  "Step.WriteStep(stream, file, productName[, options])\n"
                  "Step.ToStepString(file, productName[, options])",
     "params": [
         ("productName", "Name of the root assembly. Blank becomes \"Model\"."),
         ("options.ExportUnits", "Convert every coordinate to another unit on the way out (null = keep Units)."),
         ("options.IncludeColors", "Write colours (default true)."),
         ("options.DefaultColor", "Colour for shapes with none (default: leave uncoloured)."),
         ("options.ValidateSolids", "Fully check every solid and throw on the first problem (default false)."),
         ("options.SkipInvalidShapes", "Leave out shapes that fail instead of throwing; see SkippedShapes."),
     ],
     "code": 'var file = new StepFile();\n'
             'file.AddPart("Block").Add(new Rectangle("b", 0, 0, 0, 2, 2, 2));\n'
             '\n'
             'file.WriteStep("block-in.step", "Block");                                  // inches\n'
             'file.WriteStep("block-mm.step", "Block",\n'
             '    new StepWriteOptions { ExportUnits = LengthUnit.Millimeter });         // converted to mm\n'
             'string text = Step.ToStepString(file, "Block");\n'
             'Console.WriteLine(text.Split(\'\\n\')[2]);',
     "notes": [
         "Why does an inch file mention millimetres? STEP cannot define an inch on its own, only "
         "as \"25.4 x MILLIMETRE\". So an inch file contains a MILLIMETRE unit plus "
         "CONVERSION_BASED_UNIT('INCH', ...), and all geometry uses the inch unit. The comment on "
         "the third line of the file (printed above) says this. CAD tools read the file as inches.",
         "A StepWriteOptions object collects SkippedShapes across writes. Use a new one for each "
         "export.",
     ]},
    {"id": "s5", "kind": "code", "title": "Step 5 - Check it: Validate",
     "what": "Validate() builds every shape and lists anything that would not make a clean "
             "solid, without throwing. Run it before you export, and in your tests.",
     "signature": "file.Validate()  ->  List<string>",
     "code": 'var file = new StepFile();\n'
             'var door = file.AddPart("Door");\n'
             'door.Add(Extrusion.AlongZ("Skin", 0, 0.05, Profiles.Rectangle(0, 0, 10, 10))\n'
             '    .AddCircularCut(20, 0, 1));                 // hole is outside the 10 x 10 skin\n'
             'foreach (var problem in file.Validate()) Console.WriteLine(problem);',
     "notes": [
         "An empty list means every shape will export as a closed, consistent solid. Solids that "
         "pass through themselves, and shapes that overlap each other, are not checked.",
         "Validate() also reports zero-size shapes (a zero-length cylinder, or an extrusion whose "
         "profile was never set) as \"only 0 faces\". WriteStep silently skips those. If you create "
         "zero-size shapes on purpose, filter them out or ignore those messages.",
     ]},
])

# --------------------------------------------------------------------------------------------
section("Part 2 - Primitive shapes", [
    "Primitives are solids defined by a few numbers. Every shape also accepts the transform "
    "methods in Part 6 and WithColor(...).",
    "The preview draws curved surfaces as flat facets, and the sides / segments parameters set how "
    "many. The STEP file writes true cylinders, cones, spheres and tori whatever the count, so the "
    "count only changes how the preview looks.",
], [
    {"id": "box", "kind": "model", "title": "Rectangle - a box",
     "what": "A box given by its centre and its full size along X, Y and Z.",
     "signature": "new Rectangle(name, cx, cy, cz, sx, sy, sz)",
     "params": [("cx, cy, cz", "Centre of the box."), ("sx, sy, sz", "Full size along X, Y, Z.")],
     "code": 'var file = new StepFile();\n'
             'file.AddPart(new StepPart("Box", StepColor.SheetMetal))\n'
             '    .Add(new Rectangle("Box", 0, 0, 1, 6, 4, 2));\n'
             'file.WriteStep("box.step", "Box");'},
    {"id": "boxcorners", "kind": "model", "title": "Rectangle.FromCorners - a box between two corners",
     "what": "The same box, specified by two opposite corners instead of a centre and sizes. "
             "Corner order doesn't matter.",
     "signature": "Rectangle.FromCorners(name, (x1, y1, z1), (x2, y2, z2))",
     "code": 'var file = new StepFile();\n'
             'file.AddPart(new StepPart("Slab", StepColor.Channel))\n'
             '    .Add(Rectangle.FromCorners("Slab", (0, 0, 0), (8, 3, 0.5)));\n'
             'file.WriteStep("slab.step", "Slab");'},
    {"id": "boxfillet", "kind": "model", "title": "Rectangle.WithFillet / WithChamfer - rounded or bevelled box",
     "what": "Rounds (or bevels) all twelve edges of the box at once.",
     "signature": "box.WithFillet(radius, segments = 6)\nbox.WithChamfer(distance)",
     "params": [("radius / distance", "Size of the round or bevel. Clamped to half the smallest box size."),
                ("segments", "Facets around each rounded edge.")],
     "code": 'var file = new StepFile();\n'
             'var part = file.AddPart(new StepPart("Blocks", StepColor.Mount));\n'
             'part.Add(new Rectangle("Filleted", 0, 0, 1, 4, 3, 2).WithFillet(0.4, 8));\n'
             'part.Add(new Rectangle("Chamfered", 6, 0, 1, 4, 3, 2).WithChamfer(0.3));\n'
             'file.WriteStep("blocks.step", "Blocks");',
     "notes": ["A radius equal to half the smallest size rounds that direction completely; for a "
               "cube that gives a ball-like solid."]},
    {"id": "boxoriented", "kind": "model", "title": "Rectangle.Oriented - turn a box onto axes (legacy)",
     "what": "Turns the box about its centre so its local X/Y/Z line up with three perpendicular "
             "unit vectors that you supply. This comes from 1.0. For new code, RotateAbout "
             "(Part 6) is easier.",
     "signature": "box.Oriented(xAxis, yAxis, zAxis)",
     "code": 'var file = new StepFile();\n'
             'double c = Math.Sqrt(0.5);\n'
             'file.AddPart(new StepPart("Turned", StepColor.Fan))\n'
             '    .Add(new Rectangle("Turned", 0, 0, 0, 6, 1, 1).Oriented((c, c, 0), (-c, c, 0), (0, 0, 1)));\n'
             'file.WriteStep("turned.step", "Turned");',
     "notes": ["The three vectors must be unit length and at right angles to each other. "
               "Nothing checks this for you."]},
    {"id": "cyl", "kind": "model", "title": "Cylinder - a rod between two points",
     "what": "A round rod between two end-centre points, in any direction.",
     "signature": "new Cylinder(name, x1, y1, z1, x2, y2, z2, radius, sides = 16)\n"
                  "new Cylinder(name, start, end, radius, sides = 16)\n"
                  ".WithFillet(radius, segments = 6)",
     "params": [("start / end", "End-centre points."), ("radius", "Rod radius."),
                ("sides", "Facets around the rod.")],
     "code": 'var file = new StepFile();\n'
             'var part = file.AddPart(new StepPart("Rods", StepColor.Mount));\n'
             'part.Add(new Cylinder("Upright", 0, 0, 0, 0, 0, 4, 0.5, 32));\n'
             'part.Add(new Cylinder("Diagonal", (2, 0, 0), (5, 0, 3), 0.4, 32));\n'
             'part.Add(new Cylinder("Rounded", (7, 0, 0), (7, 0, 3), 0.75, 48).WithFillet(0.2));\n'
             'file.WriteStep("rods.step", "Rods");',
     "notes": ["A zero-length cylinder is skipped on export (no error)."]},
    {"id": "cone", "kind": "model", "title": "Cone - cones and frustums",
     "what": "A cone or truncated cone. The constructor stands it on +Z; Cone.Between points it "
             "anywhere.",
     "signature": "new Cone(name, cx, cy, cz, height, bottomRadius, topRadius, sides = 32)\n"
                  "Cone.Between(name, baseCentre, topCentre, bottomRadius, topRadius, sides = 32)\n"
                  ".WithFillet(radius, segments = 6)",
     "code": 'var file = new StepFile();\n'
             'var part = file.AddPart(new StepPart("Cones", StepColor.Controller));\n'
             'part.Add(new Cone("Frustum", 0, 0, 0, 3, 2, 1, 48));\n'
             'part.Add(new Cone("Point", 5, 0, 0, 3, 1.5, 0, 48));\n'
             'part.Add(Cone.Between("Sideways", (8, 0, 1), (12, 0, 1), 1, 0.5, 48).WithFillet(0.1));\n'
             'file.WriteStep("cones.step", "Cones");',
     "notes": ["A radius of 0 is raised to 0.0001, so a pointed cone has a tiny flat tip (same as "
               "version 1.0)."]},
    {"id": "sphere", "kind": "model", "title": "Sphere",
     "what": "A ball.",
     "signature": "new Sphere(name, cx, cy, cz, radius, segments = 32, rings = 16)",
     "params": [("segments", "Facets around the equator."), ("rings", "Bands from pole to pole.")],
     "code": 'var file = new StepFile();\n'
             'file.AddPart(new StepPart("Ball", StepColor.Fan))\n'
             '    .Add(new Sphere("Ball", 0, 0, 2, 2, 48, 24));\n'
             'file.WriteStep("ball.step", "Ball");'},
    {"id": "torus", "kind": "model", "title": "Torus - rings and gaskets",
     "what": "A doughnut around an axis. majorRadius is the distance to the centre of the tube, "
             "and minorRadius is the tube's own radius.",
     "signature": "new Torus(name, centre, axis, majorRadius, minorRadius, segments = 48, tubeSegments = 16)",
     "code": 'var file = new StepFile();\n'
             'file.AddPart(new StepPart("Gasket", StepColor.Rubber))\n'
             '    .Add(new Torus("O-ring", (0, 0, 0.5), (0, 0, 1), 3, 0.5));\n'
             'file.WriteStep("oring.step", "Gasket");',
     "notes": ["Requires 0 < minorRadius <= majorRadius."]},
    {"id": "tube", "kind": "model", "title": "Tube - pipe, sleeves, spacers",
     "what": "A hollow round tube between two points. An innerRadius of 0 gives a solid rod.",
     "signature": "new Tube(name, start, end, outerRadius, innerRadius, sides = 32)",
     "code": 'var file = new StepFile();\n'
             'file.AddPart(new StepPart("Pipe", StepColor.Galvanized))\n'
             '    .Add(new Tube("Pipe", (0, 0, 1), (8, 0, 1), 1, 0.85, 48));\n'
             'file.WriteStep("pipe.step", "Pipe");',
     "notes": ["innerRadius must be smaller than outerRadius."]},
])

# --------------------------------------------------------------------------------------------
section("Part 3 - Extrusions", [
    "An extrusion takes a closed 2D outline (a profile) and pushes it a distance to make a solid. "
    "Most sheet-metal and machined parts are extrusions. This part covers the sketch plane, "
    "outlines, holes, pockets, hollowing and edge finishing.",
    "Terms used below. The front face is the face in the sketch plane, at the origin. The back "
    "face is the one Depth away. With Extrusion.AlongZ(name, z, ...) the front face is the bottom, "
    "at height z.",
], [
    {"id": "exnew", "kind": "model", "title": "new Extrusion - the sketch plane",
     "what": "Defines where the 2D sketch lives in 3D space and how far to push it.",
     "signature": "new Extrusion(name, origin, u, v, depth)",
     "params": [
         ("origin", "3D point where profile coordinate (0, 0) sits."),
         ("u, v", "3D directions of the sketch's U and V axes. They are normalised, and v is made perpendicular to u."),
         ("depth", "Distance pushed along W = U x V. Negative pushes the other way."),
     ],
     "code": 'var file = new StepFile();\n'
             'var part = file.AddPart(new StepPart("Planes", StepColor.SheetMetal));\n'
             '// Same L-shaped outline sketched on three planes:\n'
             'var outline = Profiles.Polygon((0, 0), (3, 0), (3, 1), (1, 1), (1, 3), (0, 3));\n'
             'part.Add(new Extrusion("On XY", (0, 0, 0), (1, 0, 0), (0, 1, 0), 0.5).SetProfile(outline));   // pushes +Z\n'
             'part.Add(new Extrusion("On YZ", (5, 0, 0), (0, 1, 0), (0, 0, 1), 0.5).SetProfile(outline));   // pushes +X\n'
             'part.Add(new Extrusion("On XZ", (8, 4, 0), (1, 0, 0), (0, 0, 1), 0.5).SetProfile(outline));   // pushes -Y\n'
             'file.WriteStep("planes.step", "Planes");',
     "notes": ["Work out the push direction as U x V. For example, (1,0,0) x (0,0,1) = (0,-1,0), "
               "so the XZ sketch goes toward -Y."]},
    {"id": "exalongz", "kind": "model", "title": "Extrusion.AlongZ and SetProfile",
     "what": "AlongZ is shorthand for the most common case: sketch on the XY plane at height z and "
             "push up +Z. SetProfile replaces the outline with any closed loop, usually one from "
             "Profiles (Part 4).",
     "signature": "Extrusion.AlongZ(name, z, depth, profile)\n"
                  ".SetProfile(loop)   .AddRectangle(cu, cv, w, h, cornerRadius = 0)\n"
                  ".AddPolygon(points...)   .AddCircle(cu, cv, radius, sides = 256)",
     "code": 'var file = new StepFile();\n'
             'var part = file.AddPart(new StepPart("Outlines", StepColor.SheetMetal));\n'
             'part.Add(Extrusion.AlongZ("Hexagon", 0, 1, Profiles.RegularPolygon(0, 0, 1.5, 6)));\n'
             'part.Add(new Extrusion("Rounded", (0, 0, 0), (1, 0, 0), (0, 1, 0), 0.5).AddRectangle(4, 0, 3, 2, 0.4));\n'
             'part.Add(new Extrusion("Disc", (0, 0, 0), (1, 0, 0), (0, 1, 0), 0.25).AddCircle(8, 0, 1.25, 96));\n'
             'file.WriteStep("outlines.step", "Outlines");',
     "notes": [
         "AddCircle sets the outline to a circle; it does not add a hole. The name comes from 1.0. "
         "Use AddCircularCut for a hole.",
         "Loops can wind either way. The outline must not cross itself.",
     ]},
    {"id": "excut", "kind": "model", "title": "AddCut / AddCircularCut / AddRectangularCut / AddSlotCut - through-holes",
     "what": "Each cut removes material through the whole thickness.",
     "signature": ".AddCut(loop)\n.AddCircularCut(cu, cv, radius, sides = 256)\n"
                  ".AddRectangularCut(cu, cv, w, h, cornerRadius = 0)\n.AddSlotCut(u1, v1, u2, v2, width)",
     "code": 'var file = new StepFile();\n'
             'file.AddPart(new StepPart("Bracket", StepColor.Galvanized))\n'
             '    .Add(Extrusion.AlongZ("Bracket", 0, 0.25, Profiles.RoundedRectangle(0, 0, 10, 5, 0.5))\n'
             '        .AddCircularCut(-3.5, 0, 1, 64)\n'
             '        .AddRectangularCut(0, 0, 2, 2, 0.25)\n'
             '        .AddSlotCut(2.5, -1, 4, 1, 0.5)\n'
             '        .AddCut(Profiles.RegularPolygon(-3.5, 1.8, 0.35, 6)));\n'
             'file.WriteStep("bracket.step", "Bracket");',
     "notes": ["Cuts must lie fully inside the outline and must not overlap each other. Breaking "
               "this is reported by Validate() and throws at WriteStep or in the preview, not when "
               "you call AddCut."]},
    {"id": "expattern", "kind": "model", "title": "AddBoltCircle / AddHoleGrid - hole patterns",
     "what": "Evenly spaced round holes: on a circle, or in rows and columns.",
     "signature": ".AddBoltCircle(cu, cv, patternRadius, count, holeRadius, startDegrees = 0, sides = 32)\n"
                  ".AddHoleGrid(u0, v0, columns, rows, pitchU, pitchV, holeRadius, sides = 32)",
     "params": [("startDegrees", "Angle of the first hole, measured from +U."),
                ("u0, v0", "Centre of the first (bottom-left) hole of the grid.")],
     "code": 'var file = new StepFile();\n'
             'var part = file.AddPart(new StepPart("Plates", StepColor.Galvanized));\n'
             'part.Add(Extrusion.AlongZ("Fan plate", 0, 0.125, Profiles.Rectangle(0, 0, 8, 8))\n'
             '    .AddCircularCut(0, 0, 3, 128)\n'
             '    .AddBoltCircle(0, 0, 3.4, 8, 0.15, 22.5));\n'
             'part.Add(Extrusion.AlongZ("Perforated", 0, 0.0625, Profiles.Rectangle(10, 0, 8, 8))\n'
             '    .AddHoleGrid(7, -3, 7, 7, 1, 1, 0.25, 24));\n'
             'file.WriteStep("patterns.step", "Plates");'},
    {"id": "expocket", "kind": "model", "title": "AddPocket / AddCircularPocket / AddRectangularPocket - blind cuts",
     "what": "A pocket removes material from one face down to a depth and leaves a floor. "
             "Pockets come from the front face by default, or from the back with fromBack: true.",
     "signature": ".AddPocket(loop, depth, fromBack = false)\n"
                  ".AddCircularPocket(cu, cv, radius, depth, fromBack = false, sides = 64)\n"
                  ".AddRectangularPocket(cu, cv, w, h, depth, cornerRadius = 0, fromBack = false)",
     "code": 'var file = new StepFile();\n'
             'file.AddPart(new StepPart("Block", StepColor.Mount))\n'
             '    .Add(Extrusion.AlongZ("Block", 0, 2, Profiles.Rectangle(0, 0, 8, 5))\n'
             '        .AddRectangularPocket(-2, 0, 3, 3, 1.25, 0.3, fromBack: true)   // opens on top\n'
             '        .AddCircularPocket(2, 0, 1.2, 0.75, fromBack: true)\n'
             '        .AddCircularCut(2, 0, 0.4, 32));                                // through the pocket floor\n'
             'file.WriteStep("pockets.step", "Block");',
     "notes": [
         "Depth must be greater than 0 and less than the extrusion depth. Use a cut to go all the "
         "way through.",
         "Pockets on the same face must not overlap. A front pocket and a back pocket may overlap "
         "if their depths together leave material between them.",
         "A cut drawn inside a pocket goes through the pocket floor. A cut that crosses a pocket's "
         "edge is an error.",
         "Pocket walls are always straight. Draft (WithDraft) does not tilt them.",
     ]},
    {"id": "exshell", "kind": "model", "title": "Shell - hollow it into a tray or tube",
     "what": "Offsets the outline inward by wallThickness and pockets it down to floorThickness, "
             "turning a solid into a tray. With floorThickness 0 it cuts all the way through "
             "instead, leaving an open-ended tube or duct.",
     "signature": ".Shell(wallThickness, floorThickness, openFront = true)",
     "params": [("openFront", "true opens the front face (the bottom with AlongZ); false opens the back (the top).")],
     "code": 'var file = new StepFile();\n'
             'var part = file.AddPart(new StepPart("Shells", StepColor.Galvanized));\n'
             'part.Add(Extrusion.AlongZ("Tray", 0, 2, Profiles.RoundedRectangle(0, 0, 8, 5, 0.75))\n'
             '    .Shell(0.125, 0.25, openFront: false)                    // floor at the bottom, open on top\n'
             '    .AddCircularCut(2.5, 0, 0.4, 32));                       // drain hole in the floor\n'
             'part.Add(Extrusion.AlongZ("Duct", 0, 6, Profiles.RoundedRectangle(10, 0, 4, 4, 0.5))\n'
             '    .Shell(0.0625, 0));                                      // open tube\n'
             'file.WriteStep("shells.step", "Shells");',
     "notes": [
         "Shell reads the outline when you call it, so set the profile first.",
         "The wall must be thinner than any rounded corner's radius, and less than half the "
         "narrowest section of the outline. Otherwise the inward offset turns inside out, and "
         "Shell itself throws straight away.",
         "Shell does not follow draft. On a drafted part, a thin wall can break through the tapered "
         "side, and nothing detects it.",
     ]},
    {"id": "exgrid", "kind": "model", "title": "AddPocketGrid - ribbed, lightened plates",
     "what": "Like Shell, but leaves a grid of stiffening ribs standing in the hollow.",
     "signature": ".AddPocketGrid(wallThickness, floorThickness, ribThickness, columns, rows, openFront = true)",
     "code": 'var file = new StepFile();\n'
             'file.AddPart(new StepPart("Ribbed", StepColor.Mount))\n'
             '    .Add(Extrusion.AlongZ("Ribbed plate", 0, 1, Profiles.Rectangle(0, 0, 12, 8))\n'
             '        .AddPocketGrid(0.25, 0.125, 0.125, 4, 3, openFront: false));\n'
             'file.WriteStep("ribbed.step", "Ribbed");',
     "notes": [
         "The hollowed interior must be convex: rectangles, circles and rounded rectangles work, an "
         "L-shape does not (it throws immediately).",
         "floorThickness must be greater than 0. Unlike Shell, 0 is an error, reported when the shape is built.",
         "Set the profile before calling it.",
     ]},
    {"id": "exfilletcorners", "kind": "model", "title": "FilletCorners - round the corners of the outline",
     "what": "Rounds every corner of the outline, which rounds the edges that run along the "
             "length of the extrusion.",
     "signature": ".FilletCorners(radius, segmentsPerCorner = 8)",
     "code": 'var file = new StepFile();\n'
             'file.AddPart(new StepPart("Rounded", StepColor.SheetMetal))\n'
             '    .Add(Extrusion.AlongZ("Bar", 0, 3, Profiles.Polygon((0, 0), (4, 0), (4, 1), (1, 1), (1, 3), (0, 3)))\n'
             '        .FilletCorners(0.3));\n'
             'file.WriteStep("rounded.step", "Rounded");',
     "notes": ["It changes the outline only, not cuts. Call it before Shell or AddPocketGrid if the "
               "inside should be rounded too."]},
    {"id": "exfillet", "kind": "model", "title": "FilletEdges / ChamferEdges - round or bevel the cap edges",
     "what": "Rounds or bevels the edges around the front and/or back faces. This also rounds the "
             "lips of through-holes that run from face to face.",
     "signature": ".FilletEdges(radius, segments = 6, front = true, back = true)\n"
                  ".ChamferEdges(distance, front = true, back = true)",
     "code": 'var file = new StepFile();\n'
             'var part = file.AddPart(new StepPart("Edges", StepColor.Mount));\n'
             'part.Add(Extrusion.AlongZ("Filleted", 0, 1, Profiles.RoundedRectangle(0, 0, 5, 3, 0.5))\n'
             '    .AddCircularCut(0, 0, 0.6, 48)\n'
             '    .FilletEdges(0.2, 6));\n'
             'part.Add(Extrusion.AlongZ("Chamfered", 0, 1, Profiles.Rectangle(7, 0, 5, 3))\n'
             '    .ChamferEdges(0.15, front: false));               // top (back-face) edges only\n'
             'file.WriteStep("edges.step", "Edges");',
     "notes": [
         "The front and back sizes together must not exceed the depth. The radius must also be "
         "smaller than any rounded corner's radius, and less than half the narrowest section, or "
         "the shape throws a clear error. Sharp corners are fine.",
         "Pocket edges, and holes that start or end at a pocket floor, are not rounded.",
         "For a fully rounded block, use FilletCorners(r) and FilletEdges(r) together, or "
         "Rectangle.WithFillet.",
     ]},
    {"id": "exdraft", "kind": "model", "title": "WithDraft - taper the walls",
     "what": "Tilts the outer walls inward by an angle as they go toward the back face, like a "
             "moulded or cast part. Through-holes taper too and get larger toward the back.",
     "signature": ".WithDraft(degrees)",
     "code": 'var file = new StepFile();\n'
             'file.AddPart(new StepPart("Boss", StepColor.Controller))\n'
             '    .Add(Extrusion.AlongZ("Boss", 0, 2, Profiles.RoundedRectangle(0, 0, 4, 3, 0.5))\n'
             '        .WithDraft(8)\n'
             '        .FilletEdges(0.15, 4, front: false));\n'
             'file.WriteStep("boss.step", "Boss");',
     "notes": ["Pockets and Shell interiors are not drafted (see the Shell page)."]},
])

# --------------------------------------------------------------------------------------------
section("Part 4 - Profiles: 2D outlines", [
    "Profiles builds closed 2D loops of (U, V) points. Use them as outlines and cuts for "
    "extrusions, as sections for sweeps, and as the ends of lofts. Every Profiles shape comes "
    "back counter-clockwise. The plots show the loop with a dot at each vertex; the cross marks "
    "(0, 0).",
], [
    {"id": "prect", "kind": "profile", "title": "Profiles.Rectangle / RoundedRectangle",
     "signature": "Profiles.Rectangle(cu, cv, w, h)\nProfiles.RoundedRectangle(cu, cv, w, h, radius, segmentsPerCorner = 8)",
     "what": "Rectangles centred on (cu, cv). The corner radius is clamped to fit.",
     "code": 'var profile = Profiles.RoundedRectangle(0, 0, 6, 3, 0.75);'},
    {"id": "pcircle", "kind": "profile", "title": "Profiles.Circle / Ellipse",
     "signature": "Profiles.Circle(cu, cv, radius, sides = 64)\nProfiles.Ellipse(cu, cv, radiusU, radiusV, sides = 64)",
     "what": "Round outlines with sides straight segments.",
     "code": 'var profile = Profiles.Ellipse(0, 0, 3, 1.5, 48);'},
    {"id": "ppoly", "kind": "profile", "title": "Profiles.RegularPolygon",
     "signature": "Profiles.RegularPolygon(cu, cv, radius, sides, rotationDegrees = 0)",
     "what": "Hexagons, octagons and so on. The radius is measured to the corners.",
     "code": 'var profile = Profiles.RegularPolygon(0, 0, 2, 6, 30);'},
    {"id": "pslot", "kind": "profile", "title": "Profiles.Slot",
     "signature": "Profiles.Slot(u1, v1, u2, v2, width, segmentsPerEnd = 16)",
     "what": "An obround slot between two end-centre points.",
     "code": 'var profile = Profiles.Slot(-2, 0, 2, 1, 1);'},
    {"id": "parc", "kind": "profile", "title": "Profiles.Arc",
     "signature": "Profiles.Arc(cu, cv, innerRadius, outerRadius, startDegrees, endDegrees, segments = 32)",
     "what": "A ring sector. An innerRadius of 0 gives a pie slice.",
     "code": 'var profile = Profiles.Arc(0, 0, 1.5, 3, 20, 160);'},
    {"id": "ppolygon", "kind": "profile", "title": "Profiles.Polygon",
     "signature": "Profiles.Polygon(points...)",
     "what": "Any outline you describe point by point, in either winding direction.",
     "code": 'var profile = Profiles.Polygon((0, 0), (4, 0), (4, 1), (1.5, 1), (1.5, 3), (0, 3));'},
    {"id": "pchannel", "kind": "profile", "title": "Profiles.Channel - C / U section",
     "signature": "Profiles.Channel(width, flangeHeight, thickness, insideBendRadius = 0, bendSegments = 8)",
     "what": "A formed channel opening toward +V. width and flangeHeight are outside dimensions, "
             "and the origin is the centre of the outside of the web.",
     "code": 'var profile = Profiles.Channel(3, 1.5, 0.125, 0.125);'},
    {"id": "pangle", "kind": "profile", "title": "Profiles.Angle - L section",
     "signature": "Profiles.Angle(legU, legV, thickness, insideBendRadius = 0, bendSegments = 8)",
     "what": "A formed angle with the outside corner at the origin and the legs along +U and +V.",
     "code": 'var profile = Profiles.Angle(2, 1.5, 0.125, 0.125);'},
    {"id": "pzsection", "kind": "profile", "title": "Profiles.ZSection",
     "signature": "Profiles.ZSection(flange, height, thickness, insideBendRadius = 0, bendSegments = 8)",
     "what": "A Z section: bottom flange toward -U, web up +V, top flange toward +U. The web's "
             "outside face is at U = 0 and it spans U = 0 to U = thickness. The bottom flange tip is at "
             "U = -flange + thickness/2 and the top flange tip at U = flange + thickness/2.",
     "code": 'var profile = Profiles.ZSection(1.5, 3, 0.125, 0.125);'},
    {"id": "pthick", "kind": "profile", "title": "Profiles.ThickPolyline - any sheet-metal section",
     "signature": "Profiles.ThickPolyline(centreline, thickness, insideBendRadius = 0, bendSegments = 8)",
     "what": "Thickens an open centreline into a constant-thickness section and rounds each bend. "
             "Channel, Angle and ZSection are built this way. Use it for hat sections, J-channels "
             "or any custom form.",
     "code": 'var hat = new (double U, double V)[] { (-3, 0), (-1.5, 0), (-1.5, 2), (1.5, 2), (1.5, 0), (3, 0) };\n'
             'var profile = Profiles.ThickPolyline(hat, 0.125, 0.125);',
     "notes": ["Thickness is centred on the line. Bend radius is the inside radius."]},
    {"id": "ptube", "kind": "profile", "title": "Profiles.RectangularTubeInner",
     "signature": "Profiles.RectangularTubeInner(cu, cv, w, h, wall, outerRadius = 0, segmentsPerCorner = 8)",
     "what": "The inside loop of a rectangular tube. Pair it with a RoundedRectangle outline of the "
             "same size, as in the extrusion below.",
     "code": 'var profile = Profiles.RectangularTubeInner(0, 0, 4, 2, 0.25, 0.5);',
     "notes": ["Full use: Extrusion.AlongZ(\"Tube\", 0, 36, Profiles.RoundedRectangle(0, 0, 4, 2, 0.5))"
               ".AddCut(Profiles.RectangularTubeInner(0, 0, 4, 2, 0.25, 0.5))"]},
    {"id": "pfillet", "kind": "profile", "title": "Profiles.Fillet / FilletCorners",
     "signature": "Profiles.Fillet(loop, radius, segmentsPerCorner = 8)\n"
                  "Profiles.FilletCorners(loop, radius, segmentsPerCorner, cornerIndices...)",
     "what": "Rounds every corner, or only the corners whose vertex numbers you list (starting at 0).",
     "code": 'var l = Profiles.Polygon((0, 0), (4, 0), (4, 1), (1.5, 1), (1.5, 3), (0, 3));\n'
             'var profile = Profiles.FilletCorners(l, 0.5, 8, 1, 4);      // outer corner at (4,0) and top-right (1.5,3)'},
    {"id": "pchamfer", "kind": "profile", "title": "Profiles.Chamfer",
     "signature": "Profiles.Chamfer(loop, distance)",
     "what": "Cuts every corner back by distance along both edges.",
     "code": 'var profile = Profiles.Chamfer(Profiles.Rectangle(0, 0, 4, 3), 0.5);'},
    {"id": "poffset", "kind": "profile", "title": "Profiles.Offset",
     "signature": "Profiles.Offset(loop, distance)",
     "what": "Grows the loop outward (positive distance) or shrinks it (negative), keeping sharp "
             "mitred corners. Useful for clearance cut-outs or a lip around an opening.",
     "code": 'var profile = Profiles.Offset(Profiles.Polygon((0, 0), (3, 0), (3, 2), (0, 2)), 0.5);',
     "notes": ["Throws immediately if shrinking would turn the loop inside out."]},
    {"id": "pmove", "kind": "profile", "title": "Translate / Rotate / Scale / MirrorU / MirrorV / Area",
     "signature": "Profiles.Translate(loop, du, dv)   Profiles.Rotate(loop, degrees, cu = 0, cv = 0)\n"
                  "Profiles.Scale(loop, factor, cu = 0, cv = 0)   Profiles.MirrorU(loop, axisU = 0)\n"
                  "Profiles.MirrorV(loop, axisV = 0)   Profiles.Area(loop)",
     "what": "Move a loop around in 2D before you use it. Area always returns a positive value.",
     "code": 'var l = Profiles.Polygon((0, 0), (3, 0), (3, 1), (1, 1), (1, 2), (0, 2));\n'
             'var profile = Profiles.Rotate(Profiles.MirrorU(Profiles.Translate(l, 1, 0)), 15);\n'
             'Console.WriteLine($"Area = {Profiles.Area(profile):F3}");'},
])

# --------------------------------------------------------------------------------------------
section("Part 5 - Revolves, sweeps and lofts", [
    "These three operations turn a 2D section into a solid by spinning it, carrying it along a "
    "path, or blending between two sections.",
], [
    {"id": "revolve", "kind": "model", "title": "Revolve - spin a section around an axis",
     "what": "The lathe operation. The profile is a list of (R, Z) points: R is the distance from "
             "the axis (never negative) and Z is the position along it. A full revolve is closed; "
             "a partial one gets flat end faces.",
     "signature": "new Revolve(name, origin, axis, profile, angleDegrees = 360, segments = 64)\n"
                  "  .StartDegrees   .ReferenceDirection   .FilletProfile(radius, segmentsPerCorner = 6, corners...)",
     "params": [("profile", "Closed list of (R, Z) points, in any order around the loop."),
                ("ReferenceDirection", "Where angle 0 points (perpendicular to the axis); picked automatically if left unset."),
                ("FilletProfile corners", "Indices into the profile list; none = round every corner.")],
     "code": 'var file = new StepFile();\n'
             'var part = file.AddPart(new StepPart("Turned", StepColor.Galvanized));\n'
             '// Inlet bell: point 0 (1,0) ... point 5 (1,1.5). Round the two outside flange corners (1 and 2).\n'
             'part.Add(new Revolve("Inlet bell", (0, 0, 0), (0, 0, 1),\n'
             '        new (double R, double Z)[] { (1.0, 0), (2.4, 0), (2.4, 0.15), (1.2, 0.15), (1.15, 1.5), (1.0, 1.5) })\n'
             '    .FilletProfile(0.06, 4, 1, 2));\n'
             'part.Add(new Revolve("Quarter ring", (6, 0, 0), (0, 0, 1),\n'
             '    new (double R, double Z)[] { (1, 0), (2, 0), (2, 1), (1, 1) }, angleDegrees: 90));\n'
             'file.WriteStep("turned.step", "Turned");',
     "notes": ["A profile point with R = 0 lies on the axis and closes the solid there, as in a "
               "solid knob."]},
    {"id": "sweep", "kind": "model", "title": "Sweep and Sweep.Rod - carry a section along a path",
     "what": "Moves a 2D section along a 3D path of points, for wire harnesses, piping, handrails "
             "and formed rod. bendRadius rounds the corners of the path.",
     "signature": "new Sweep(name, profile, path, bendRadius = 0, bendSegments = 8)   .Up\n"
                  "Sweep.Rod(name, path, radius, bendRadius = 0, sides = 16)",
     "params": [("path", "Points the section's centre passes through."),
                ("Up", "Direction the section's U axis points at the start of the path. Chosen automatically if left unset.")],
     "code": 'var file = new StepFile();\n'
             'var part = file.AddPart(new StepPart("Formed", StepColor.Harness));\n'
             'part.Add(Sweep.Rod("Wire", new (double, double, double)[] { (0, 0, 0.3), (5, 0, 0.3), (5, 3, 0.3), (5, 3, 3) },\n'
             '    0.2, bendRadius: 0.75));\n'
             '// 1 x 1/4 strap. Up = +Y keeps the 1" width along Y, so the strap bends across its thin direction.\n'
             'part.Add(new Sweep("Strap", Profiles.Rectangle(0, 0, 1, 0.25),\n'
             '    new (double, double, double)[] { (8, 0, 0), (8, 0, 3), (12, 0, 3), (12, 0, 0) }, 0.5) { Up = (0, 1, 0) });\n'
             'file.WriteStep("formed.step", "Formed");',
     "notes": [
         "If a straight leg is too short for the bend radius, the radius is reduced automatically.",
         "A path that doubles back (a turn sharper than about 174 degrees) is rejected. Add a point "
         "or a bend radius.",
         "Keep the bend radius larger than half the section's size. A tighter bend makes the solid "
         "fold through itself, and nothing detects that.",
     ]},
    {"id": "loft", "kind": "model", "title": "Loft and Loft.SquareToRound - blend between two sections",
     "what": "Blends a bottom section into a top section a height above it, for square-to-round "
             "transitions, reducers and offset ducts.",
     "signature": "new Loft(name, origin, u, v, height, bottom, top)   .TopOffset = (du, dv)\n"
                  "Loft.SquareToRound(name, origin, width, depth, diameter, height, sides = 48)",
     "code": 'var file = new StepFile();\n'
             'var part = file.AddPart(new StepPart("Transitions", StepColor.Insulation));\n'
             'part.Add(Loft.SquareToRound("Square to round", (0, 0, 0), 6, 6, 4, 4));\n'
             'part.Add(new Loft("Offset reducer", (9, 0, 0), (1, 0, 0), (0, 1, 0), 4,\n'
             '    Profiles.Rectangle(0, 0, 6, 4), Profiles.RoundedRectangle(0, 0, 3, 2, 0.4)) { TopOffset = (1, 0) });\n'
             'file.WriteStep("transitions.step", "Transitions");',
     "notes": [
         "The two sections may have different numbers of points. Corners are kept, and the top is "
         "rotated so each bottom point pairs with the nearest top point, which avoids a twist.",
         "A loft is always solid. It can't be hollowed, so a sheet-metal transition can only be "
         "shown as its outer envelope, and ComputeVolume() returns the envelope's volume, not the "
         "sheet's.",
     ]},
    {"id": "mesh", "kind": "model", "title": "MeshSolid - bring your own polyhedron",
     "what": "Any closed solid made of flat faces that you build yourself, for example from "
             "imported data.",
     "signature": "var m = new Polyhedron();  m.AddVertex(p);  m.AddFace(int[] loop[, holes...])\n"
                  "new MeshSolid(name, m)",
     "code": 'var m = new Polyhedron();\n'
             'int a = m.AddVertex((0, 0, 0)), b = m.AddVertex((3, 0, 0)), c = m.AddVertex((0, 3, 0)), d = m.AddVertex((0, 0, 3));\n'
             'm.AddFace(new[] { a, b, c });\n'
             'm.AddFace(new[] { a, b, d });\n'
             'm.AddFace(new[] { b, c, d });\n'
             'm.AddFace(new[] { c, a, d });          // winding doesn\'t matter; it is fixed for you\n'
             'var file = new StepFile();\n'
             'file.AddPart(new StepPart("Wedge", StepColor.Controller)).Add(new MeshSolid("Tetrahedron", m));\n'
             'file.WriteStep("tetra.step", "Wedge");',
     "notes": ["Faces must be flat and must close the solid, with every edge shared by exactly two "
               "faces. WriteStep does not reject an open or warped mesh on its own. file.Validate(), "
               "m.Validate() or StepWriteOptions.ValidateSolids = true will catch it."]},
])

# --------------------------------------------------------------------------------------------
section("Part 6 - Moving things", [
    "Each shape carries a transform that is applied when it is exported or previewed.",
], [
    {"id": "rotate", "kind": "model", "title": "RotateAbout / Translate - order matters",
     "what": "RotateAbout spins a shape about an axis line, using the right-hand rule: thumb "
             "along the axis, fingers show positive rotation. Translate moves it. Calls apply in "
             "the order you write them, and each acts on the result of the one before, in world "
             "coordinates.",
     "signature": "shape.RotateAbout(point, axis, degrees)   shape.Translate(dx, dy, dz)",
     "code": 'var file = new StepFile();\n'
             'var hub = file.AddPart(new StepPart("Impeller", StepColor.Fan));\n'
             'hub.Add(new Cylinder("Hub", 0, 0, 0, 0, 0, 1, 0.6, 32));\n'
             'for (int i = 0; i < 6; i++)\n'
             '    hub.Add(new Rectangle($"Blade {i + 1}", 1.6, 0, 0.5, 2, 0.08, 0.8)\n'
             '        .RotateAbout((1.6, 0, 0.5), (1, 0, 0), 30)          // 1) pitch the blade about its own length\n'
             '        .RotateAbout((0, 0, 0), (0, 0, 1), 60 * i));        // 2) then swing it round the hub\n'
             'file.WriteStep("impeller.step", "Impeller");',
     "notes": ["Chaining keeps the shape's type, so new Cone(...).RotateAbout(...) is still a Cone."]},
    {"id": "mirror", "kind": "model", "title": "MirrorAcross / TransformBy",
     "what": "MirrorAcross reflects a shape across a plane. TransformBy applies any Transform you "
             "have built.",
     "signature": "shape.MirrorAcross(point, normal)   shape.TransformBy(transform)",
     "code": 'var file = new StepFile();\n'
             'var part = file.AddPart(new StepPart("Handed", StepColor.SheetMetal));\n'
             'var outline = Profiles.Polygon((0, 0), (3, 0), (3, 0.5), (0.5, 2), (0, 2));\n'
             'part.Add(Extrusion.AlongZ("Right hand", 0, 0.5, outline).Translate(1, 0, 0));\n'
             'part.Add(Extrusion.AlongZ("Left hand", 0, 0.5, outline).Translate(1, 0, 0)\n'
             '    .MirrorAcross((0, 0, 0), (1, 0, 0)));                       // mirror across the YZ plane\n'
             'part.Add(new Rectangle("Spacer", 0, 1, 1, 0.5, 0.5, 2)\n'
             '    .TransformBy(Transform.RotationAbout((0, 1, 0), (0, 1, 0), 45)));\n'
             'file.WriteStep("handed.step", "Handed");',
     "notes": ["A mirrored solid is re-wound automatically, so it is still valid (not inside out)."]},
    {"id": "partmove", "kind": "model", "title": "Moving a whole part",
     "what": "StepPart has the same methods: RotateAbout, Translate, MirrorAcross and "
             "TransformBy. They move the shapes that are in the part at that moment.",
     "signature": "part.RotateAbout(...)   part.Translate(...)   part.MirrorAcross(...)   part.TransformBy(...)",
     "code": 'var file = new StepFile();\n'
             'var leg = file.AddPart(new StepPart("Leg", StepColor.Channel));\n'
             'leg.Add(new Rectangle("Post", 0, 0, 2, 0.5, 0.5, 4));\n'
             'leg.Add(new Rectangle("Foot", 0, 0, 0.1, 2, 2, 0.2));\n'
             'leg.RotateAbout((0, 0, 0), (0, 1, 0), 20).Translate(3, 0, 0);    // add shapes FIRST, then move\n'
             'leg.Add(new Sphere("Not moved", 0, 0, 0, 0.4));                  // added afterwards: stays put\n'
             'file.WriteStep("leg.step", "Leg");',
     "notes": ["Add every shape before you move the part. A shape added later stays where it was "
               "drawn, like the sphere at the origin in the picture."]},
    {"id": "transform", "kind": "code", "title": "Transform - building transforms yourself",
     "what": "Transform values combine with Then(...) in reading order. Use them with TransformBy, "
             "and as instance placements (Part 7).",
     "signature": "Transform.Identity   Translation(dx, dy, dz)   RotationAbout(point, axis, deg)\n"
                  "Mirror(point, normal)   Scale(factor[, centre])   Placement(origin, xAxis, zAxis)\n"
                  "FromBasis(centre, ax, ay, az)   a.Then(b)   t.Inverse()   t.Apply(p)   t.ApplyDirection(d)\n"
                  "t.IsRigid   t.IsIdentity   t.Determinant",
     "code": 'var t = Transform.RotationAbout((0, 0, 0), (0, 0, 1), 90)        // first rotate...\n'
             '    .Then(Transform.Translation(10, 0, 0));                        // ...then move\n'
             'Console.WriteLine(t.Apply((1, 0, 0)));                             // (10, 1, 0)\n'
             'Console.WriteLine(t.Inverse().Apply((10, 1, 0)));                  // back to (1, 0, 0)\n'
             'var frame = Transform.Placement((5, 5, 0), (0, 1, 0), (1, 0, 0));  // local Z -> world +X\n'
             'Console.WriteLine($"rigid: {frame.IsRigid}, scale rigid: {Transform.Scale(2).IsRigid}");',
     "notes": ["Printed values can show tiny rounding errors such as 6.1E-17; treat those as zero."]},
])

# --------------------------------------------------------------------------------------------
section("Part 7 - Colours, assemblies and instances", [], [
    {"id": "color", "kind": "model", "title": "StepColor and WithColor",
     "what": "Colours are written into the STEP file, and the WPF preview uses them too. Give a "
             "whole part a colour, or override it on single shapes.",
     "signature": "new StepPart(name, color)   shape.WithColor(color)\n"
                  "StepColor.FromHex(\"#RRGGBB\")   new StepColor(r, g, b)   presets: SheetMetal, Galvanized, Fan,\n"
                  "Controller, Channel, Mount, Harness, Border, Insulation, Rubber",
     "code": 'var file = new StepFile();\n'
             'var part = file.AddPart(new StepPart("Panel", StepColor.SheetMetal));      // part colour\n'
             'part.Add(new Rectangle("Panel", 0, 0, 0.05, 8, 5, 0.1));\n'
             'part.Add(new Cylinder("Knob", 2, 1, 0.1, 2, 1, 0.8, 0.4, 32)).WithColor(StepColor.FromHex("#C0392B"));\n'
             'part.Add(new Rectangle("Label", -2, 0, 0.12, 3, 1.5, 0.04)).WithColor(StepColor.Controller);\n'
             'file.WriteStep("colours.step", "Panel");',
     "notes": ["Precedence: shape colour, then part colour, then StepWriteOptions.DefaultColor "
               "(uncoloured if that is null). The WPF preview falls back to sheet-metal grey instead "
               "of uncoloured."]},
    {"id": "instance", "kind": "model", "title": "AddInstance - one part, many copies",
     "what": "Draw a part once around its own origin, then place copies of it. The geometry is "
             "written once and referenced by each copy. Files stay small, and CAD counts the "
             "copies as one part in the bill of materials.",
     "signature": "file.AddInstance(part, dx, dy, dz[, name])\nfile.AddInstance(part, placement[, name])",
     "code": 'var file = new StepFile();\n'
             'var fan = new StepPart("Plug fan", StepColor.Fan);                  // not AddPart: only the copies appear\n'
             'fan.Add(new Cylinder("Motor", 0, 0, 0, 0, 0, 3, 1.2, 32));\n'
             'fan.Add(new Revolve("Wheel", (0, 0, 3), (0, 0, 1),\n'
             '    new (double R, double Z)[] { (1.2, 0), (3.5, 0), (3.5, 1.5), (3.2, 1.5), (1.2, 0.2) }, 360, 48));\n'
             'for (int row = 0; row < 2; row++)\n'
             '    for (int col = 0; col < 3; col++)\n'
             '        file.AddInstance(fan, col * 9, row * 9, 0, $"Fan {row * 3 + col + 1}");\n'
             'file.AddInstance(fan, Transform.RotationAbout((0, 0, 0), (1, 0, 0), -90)\n'
             '    .Then(Transform.Translation(9, 20, 3.5)), "Spare (on its side)");\n'
             'file.WriteStep("array.step", "Fan array");',
     "notes": [
         "Placements must be rigid: rotation and translation only. A scale or mirror throws at "
         "WriteStep, because STEP assemblies can't scale or mirror a copy. Transform the shapes "
         "instead. The WPF preview does not reject them, so check with Validate().",
         "If the part is also added with AddPart, it appears once more at its drawn position.",
     ]},
])

# --------------------------------------------------------------------------------------------
section("Part 8 - Checking, measuring and handling errors", [], [
    {"id": "errors", "kind": "code", "title": "When an error is reported",
     "what": "Most impossible geometry throws an exception rather than writing a broken file. "
             "Some problems are only found when you ask for a check, and self-intersection is never "
             "found. Some checks run when you call a method; most run when the shape is built.",
     "code": 'var plate = Extrusion.AlongZ("Plate", 0, 0.25, Profiles.Rectangle(0, 0, 4, 4)).AddCircularCut(9, 0, 1);\n'
             'Console.WriteLine("AddCircularCut accepted the bad hole - nothing is checked yet");\n'
             'try { plate.ToPolyhedron(); }\n'
             'catch (ArgumentException e) { Console.WriteLine("Building it: " + e.Message); }\n'
             '\n'
             'try { Extrusion.AlongZ("Strip", 0, 1, Profiles.Rectangle(0, 0, 4, 0.5)).Shell(0.3, 0.1); }\n'
             'catch (ArgumentException e) { Console.WriteLine("Shell checks immediately: " + e.Message.Split(\'(\')[0]); }',
     "table": [
         ("Checked immediately (throws at the call)", "Shell and AddPocketGrid offset errors (wall too thick for the "
          "outline), AddPocketGrid convexity and 'no room for ribs', Profiles.Offset, Profiles.ThickPolyline; "
          "AddPocketGrid on an extrusion with no profile yet"),
         ("Checked when built (listed by Validate(); throws at WriteStep and in the WPF preview)", "Cuts and pockets "
          "inside the outline and not overlapping, pocket depths (including a Shell or AddPocketGrid floor that is 0 "
          "or too deep), fillet/chamfer sizes, self-crossing outlines, sweeps that double back, torus/tube radii"),
         ("Checked by Validate() and on request", "Solid checks for every shape (closed, consistently oriented, flat "
          "faces): run by file.Validate(), StepWriteOptions.ValidateSolids or Polyhedron.Validate(). WriteStep alone "
          "doesn't run them, which matters mostly for MeshSolid"),
         ("Not detected", "Solids that pass through themselves (very tight sweeps, thin shells on steep "
          "draft) and shapes that overlap each other"),
         ("Silently skipped on export", "Zero-size shapes; Validate() still lists them"),
     ]},
    {"id": "skip", "kind": "code", "title": "SkipInvalidShapes - export what you can",
     "what": "For exports triggered from a UI, writing a partial file plus a warning is often "
             "better than failing. SkipInvalidShapes leaves out the bad shapes and lists them.",
     "code": 'var file = new StepFile();\n'
             'var door = file.AddPart("Door");\n'
             'door.Add(Extrusion.AlongZ("Skin", 0, 0.05, Profiles.Rectangle(0, 0, 10, 10)).AddCircularCut(20, 0, 1));\n'
             'door.Add(new Rectangle("Frame", 0, 0, 1, 10, 10, 0.5));\n'
             'var options = new StepWriteOptions { SkipInvalidShapes = true };\n'
             'file.WriteStep("door.step", "Door", options);\n'
             'foreach (var s in options.SkippedShapes) Console.WriteLine("Skipped: " + s);',
     "notes": ["The WPF preview has no skip option, and a bad shape there throws. In a UI, run "
               "file.Validate() first and show the problems before you build the preview."]},
    {"id": "measure", "kind": "code", "title": "ComputeVolume / Bounds / ToPolyhedron",
     "what": "Measure what you built, for example for weights or space claims.",
     "signature": "shape.ComputeVolume()   part.ComputeVolume()   file.Bounds()   shape.ToPolyhedron()\n"
                  "Polyhedron: Volume, SurfaceArea, Min, Max, Vertices, Faces, Validate()",
     "code": 'var file = new StepFile();\n'
             'var plate = file.AddPart("Plate");\n'
             'plate.Add(Extrusion.AlongZ("p", 0, 0.125, Profiles.Rectangle(0, 0, 24, 24)).AddCircularCut(0, 0, 9.5, 128));\n'
             'double cubicInches = plate.ComputeVolume();\n'
             'Console.WriteLine($"Volume {cubicInches:F2} in^3, steel weight {cubicInches * 0.284:F2} lb");\n'
             'var (min, max) = file.Bounds();\n'
             'Console.WriteLine($"Bounds {min} to {max}");\n'
             'var solid = plate.shapes[0].ToPolyhedron();\n'
             'Console.WriteLine($"{solid.Faces.Count} faces, area {solid.SurfaceArea:F1} in^2, problems: {solid.Validate().Count}");',
     "notes": ["Each call to ComputeVolume, Validate, WriteStep or the WPF Build rebuilds the "
               "geometry. Keep the result of ToPolyhedron() if you need it more than once."]},
])

# --------------------------------------------------------------------------------------------
section("Part 9 - Previewing in WPF (QPAC.Sculptor.Wpf)", [
    "The WPF package shows the same solids in a HelixToolkit viewport. Add the "
    "QPAC.Sculptor.Wpf package, also reference QPAC.Sculptor 1.1.1 or later directly (it fixes "
    "preview triangulation of grid-aligned holes; the WPF package alone may resolve 1.1.0), and put a HelixViewport3D named viewport in your "
    "window. The examples in this part are compiled and run in a WPF test program. They show no "
    "pictures because their output is a live 3D view.",
], [
    {"id": "wpfbuild", "kind": "wpf", "title": "SteptoVisual3D.Build",
     "what": "Turns a StepFile into visuals you add to the viewport: one SculptorVisual3D per part "
             "and per instance. With ShowEdges on, it also returns outline (LinesVisual3D) visuals.",
     "code": 'using HelixToolkit.Wpf;\nusing QPAC.Sculptor;\nusing QPAC.Sculptor.Wpf;\n\n'
             'var stepFile = new StepFile();\n'
             'stepFile.AddPart(new StepPart("Box", StepColor.Fan)).Add(new Rectangle("b", 0, 0, 0, 2, 2, 2));\n'
             'viewport.Children.Add(new DefaultLights());\n'
             'foreach (var visual in SteptoVisual3D.Build(stepFile))\n'
             '    viewport.Children.Add(visual);\n'
             'viewport.ZoomExtents();'},
    {"id": "wpfoptions", "kind": "wpf", "title": "SculptorRenderOptions",
     "what": "Controls shading, outlines and fallback colours.",
     "params": [("SmoothShading / CreaseAngleDegrees", "Blend facets that meet at less than the angle (default on, 30 degrees)."),
                ("ShowEdges / EdgeColor / EdgeThickness", "Outline sharp edges."),
                ("TypeColors", "Colour by StepPart.Type when the part has no colour."),
                ("DefaultColor", "Last fallback (sheet-metal grey)."),
                ("Opacity, SpecularPower", "Material look.")],
     "code": 'using System.Windows.Media;\nusing QPAC.Sculptor;\nusing QPAC.Sculptor.Wpf;\n\n'
             'var stepFile = new StepFile();\n'
             'stepFile.AddPart(new StepPart("Fan unit") { Type = "Fan" }).Add(new Cylinder("hub", 0, 0, 0, 0, 0, 1, 1, 48));\n'
             'var options = new SculptorRenderOptions { ShowEdges = true, EdgeColor = Colors.Black };\n'
             'options.TypeColors["Fan"] = Colors.SteelBlue;\n'
             'foreach (var visual in SteptoVisual3D.Build(stepFile, options))\n'
             '    viewport.Children.Add(visual);',
     "notes": ["Colour order in the preview: shape, part, TypeColors[part.Type], DefaultColor."]},
    {"id": "wpfpick", "kind": "wpf", "title": "Picking a part with the mouse",
     "what": "Every part visual is a SculptorVisual3D, which knows its Part, its Instance and its "
             "Name.",
     "code": 'using System.Windows;\nusing System.Windows.Input;\nusing HelixToolkit.Wpf;\nusing QPAC.Sculptor.Wpf;\n\n'
             'viewport.MouseDown += (s, e) =>\n'
             '{\n'
             '    var hit = viewport.Viewport.FindHits(e.GetPosition(viewport)).FirstOrDefault();\n'
             '    if (hit?.Visual is SculptorVisual3D picked)\n'
             '        MessageBox.Show($"Clicked {picked.Name}");\n'
             '};'},
    {"id": "wpfother", "kind": "text", "title": "BuildModel / BuildPart / ToMesh and threading",
     "what": "Other entry points for special cases:",
     "table": [
         ("SteptoVisual3D.BuildModel(file, options)", "One frozen Model3DGroup for the whole model. The fastest option; "
          "no per-part picking and no outlines."),
         ("SteptoVisual3D.BuildPart(part, options)", "The visual for one part, e.g. after it changed. It does not update "
          "that part's instance visuals or outlines."),
         ("SteptoVisual3D.BuildPartModel(part, options)", "One part as a frozen Model3DGroup."),
         ("SteptoVisual3D.ToMesh(shape, creaseAngle)", "A frozen MeshGeometry3D; creaseAngle 0 = flat shading."),
         ("ToColor / ToStepColor", "Convert between StepColor and WPF Color."),
     ],
     "notes": [
         "Threading: call Build and BuildPart on the UI thread. To keep the UI responsive with big "
         "models, call BuildModel or BuildPartModel on a background thread (the results are "
         "frozen), then put the result into a ModelVisual3D on the UI thread.",
         "Only rebuild what changed. Tessellating is the expensive step, and lower sides/segments "
         "values preview faster.",
     ]},
])

# --------------------------------------------------------------------------------------------
section("Part 10 - Putting it together", [
    "A complete fan-wall cell built from scratch: a frame of formed channel, a fan plate with a "
    "bolt pattern, a blank-off, a fan used three times through instances, a wire harness, and a "
    "check before export.",
], [
    {"id": "full", "kind": "model", "title": "Worked example: fan-wall cell",
     "view": {"elev": 28, "azim": -58},
     "code": 'var file = new StepFile { Description = "Fan wall cell - walkthrough example" };\n'
             'double W = 40, H = 28, t = 0.0625;                       // cell size and sheet gauge\n'
             '\n'
             '// 1. Frame: four rails of 2" channel, flanges toward the front (+Z).\n'
             'var frame = file.AddPart(new StepPart("Frame", StepColor.Channel));\n'
             'var channel = Profiles.Channel(2, 1, t, t);\n'
             'frame.Add(new Extrusion("Bottom rail", (0, 1, 0), (0, 1, 0), (0, 0, 1), W).SetProfile(channel));\n'
             'frame.Add(new Extrusion("Top rail", (0, H - 1, 0), (0, 1, 0), (0, 0, 1), W).SetProfile(channel));\n'
             'frame.Add(new Extrusion("Left rail", (1, 2, 0), (-1, 0, 0), (0, 0, 1), H - 4).SetProfile(channel));\n'
             'frame.Add(new Extrusion("Right rail", (W - 1, 2, 0), (-1, 0, 0), (0, 0, 1), H - 4).SetProfile(channel));\n'
             '\n'
             '// 2. Fan plate (left half) and blank-off (right half) sitting on the rail webs.\n'
             'var plate = file.AddPart(new StepPart("Fan plate", StepColor.Galvanized));\n'
             'plate.Add(Extrusion.AlongZ("Plate", -t, t, Profiles.Rectangle(W / 4, H / 2, W / 2, H))\n'
             '    .AddCircularCut(W / 4, H / 2, 8, 128)\n'
             '    .AddBoltCircle(W / 4, H / 2, 9, 8, 0.15, 22.5)\n'
             '    .AddCircularCut(W / 4, 3, 0.35, 24));                 // grommet hole for the motor lead\n'
             'file.AddPart(new StepPart("Blank-off", StepColor.SheetMetal))\n'
             '    .Add(Extrusion.AlongZ("Blank", -t, t, Profiles.Rectangle(3 * W / 4, H / 2, W / 2, H)));\n'
             '\n'
             '// 3. One fan definition, placed behind the plate opening (and two spares beside the cell).\n'
             'var fan = new StepPart("Plug fan", StepColor.Fan);\n'
             'fan.Add(new Cylinder("Motor", 0, 0, -9, 0, 0, -3, 2.5, 32));\n'
             'fan.Add(new Revolve("Wheel", (0, 0, -3), (0, 0, 1),\n'
             '    new (double R, double Z)[] { (2.5, 0), (7.5, 0), (7.5, 2.5), (6.8, 2.5), (2.5, 0.3) }, 360, 64));\n'
             'file.AddInstance(fan, W / 4, H / 2, 0, "Fan 1");\n'
             'file.AddInstance(fan, W + 12, 8, 0, "Spare A");\n'
             'file.AddInstance(fan, W + 12, 25, 0, "Spare B");\n'
             '\n'
             '// 4. Harness along the rails to the controller box.\n'
             'file.AddPart(new StepPart("Controller", StepColor.Controller)).Add(new Rectangle("Box", 3 * W / 4, H / 2, 2, 8, 6, 4));\n'
             'var leadPath = new (double, double, double)[]\n'
             '{\n'
             '    (W / 4, H / 2 - 2.5, -6),        // leaves the motor\n'
             '    (W / 4, 3, -6), (W / 4, 3, 1),   // down, then forward through the grommet\n'
             '    (3 * W / 4, 3, 1),               // along the bottom rail\n'
             '    (3 * W / 4, H / 2 - 3, 1),       // up into the controller\n'
             '};\n'
             'file.AddPart(new StepPart("Harness", StepColor.Harness)).Add(Sweep.Rod("Motor lead", leadPath, 0.2, bendRadius: 1.5));\n'
             '\n'
             '// 5. Check, then write.\n'
             'var problems = file.Validate();\n'
             'Console.WriteLine(problems.Count == 0 ? "No problems" : string.Join("\\n", problems));\n'
             'file.WriteStep("fan-cell.step", "Fan wall cell");'},
])

# --------------------------------------------------------------------------------------------
TROUBLESHOOTING = [
    ("NU1101 / NU1301: cannot find QPAC.Sculptor", "The qpacsystems feed isn't configured, or its token expired (Step 0)."),
    ("A new version is published but the app builds the old one", "The reference is 1.0.*, or NuGet cached a "
     "republished version: delete %USERPROFILE%\\.nuget\\packages\\qpac.sculptor\\<version>."),
    ("FileNotFoundException: Could not load 'QPAC Sculptor, Version=1.0.x.x'", "A package was built with a "
     "different AssemblyVersion. It must stay 1.0.0.0 for every 1.x release."),
    ("ArgumentException naming a shape", "That shape's geometry is impossible; the message says why. Run file.Validate() "
     "to see every problem at once."),
    ("CAD shows the model 25.4 times too big or too small", "StepFile.Units doesn't match the unit you drew in. Set "
     "Units to your drawing unit; use ExportUnits to convert."),
    ("Holes look like polygons in CAD", "A circle needs at least 8 sides to be recognised as a circle, and the "
     "shape must have no FilletEdges, ChamferEdges or draft. Otherwise it is written faceted."),
    ("Shapes added after part.RotateAbout(...) didn't move", "Part transforms apply only to shapes already in the "
     "part (Part 6)."),
    ("A tray comes out upside down", "Shell's openFront defaults to true, and with AlongZ the front face is the "
     "bottom. Pass openFront: false for a tray that opens upward."),
    ("CI fails with 401/403 on the qpacsystems feed", "The QPAC_PACKAGES_PAT secret is missing or expired. Update it "
     "in both repositories. In the WPF repo the failure shows at 'Add qpacsystems feed' or 'Restore', on pull "
     "requests too."),
]

# Findings from the accuracy review of this document's first draft, plus the author's own list.
SUGGESTIONS_INTRO = (
    "Before publishing, this walkthrough was reviewed against the library source code by a "
    "second reviewer. Every factual problem found was corrected above. These are the issues the "
    "review turned up with the library itself and with how this guide approaches it, most "
    "important first."
)
SUGGESTIONS = [
    ("Edge fillets and lofts are still faceted",
     "Since 1.2 the STEP file has true cylinders, cones, spheres and tori. Still faceted:
"
     "- FilletEdges and ChamferEdges bands, and draft;
"
     "- Rectangle.WithFillet;
"
     "- lofts;
"
     "- ellipses;
"
     "- sharp-cornered sweeps.
"
     "They would need blended corner, ruled, elliptical-edge or B-spline faces."),
    ("No way to subtract one solid from another",
     "There are no boolean operations (union or subtract between shapes). Cuts and pockets cover "
     "plates and blocks, but hollow lofts, off-axis holes through revolved parts and notched "
     "channels can't be modelled. Simple booleans would cover these, and the loft limitation in "
     "Part 5 goes away with them."),
    ("Validate() and the exporter disagree on zero-size shapes",
     "WriteStep skips them silently, but Validate() reports them. A configurator that requires an "
     "empty Validate() will fail on intentional zero sizes. Validate should skip empty solids, "
     "as the exporter does."),
    ("Some errors are not detected",
     "Self-intersecting solids (tight sweep bends, thin shells on drafted parts) pass every "
     "check. Draft doesn't reach pockets or Shell interiors, so the shell wall can break through "
     "the tapered side. Shell should follow the draft, and Validate should test for "
     "self-intersection."),
    ("Error timing is inconsistent",
     "Shell and AddPocketGrid throw at the call, while AddCut and AddPocket throw only when "
     "the shape is built. Pick one; checking at build time everywhere is friendlier for "
     "configurators, and errors should then go through Validate() consistently."),
    ("Preview and export can differ",
     "The WPF preview accepts non-rigid instance placements that WriteStep rejects. It "
     "colours uncoloured shapes grey where STEP leaves them uncoloured, and it has no "
     "SkipInvalidShapes. Sharing one rule set between the two would make 'the preview matches "
     "the file' strictly true."),
    ("Geometry is rebuilt on every call",
     "Validate, WriteStep, ComputeVolume and each WPF Build all re-tessellate every shape, and "
     "ShowEdges does it again per instance. Caching the polyhedron per shape (cleared when the "
     "shape changes) would make big SPFA models much faster."),
    ("Legacy names and defaults are confusing",
     "Extrusion.AddCircle sets the outline rather than adding a hole. Default facet counts vary "
     "(AddCircularCut 256, Profiles.Circle 64, Cylinder 16). Shell's openFront default "
     "produces upside-down trays with AlongZ. New, clearer names (SetCircle, and an opening-side "
     "enum) could be added while the old ones are kept, and the defaults made consistent."),
    ("StepWriteOptions.SkippedShapes never resets",
     "Reusing one options object accumulates results from earlier exports. Clear the list at the "
     "start of each write, or return the list from WriteStep."),
    ("Releases can be missed",
     "CI publishes the version in the csproj and skips versions that already exist. A fix "
     "merged without a version bump therefore never ships, with no warning. Also, a manual run "
     "publishes from any branch. CI should fail when the code changed but the version didn't, "
     "and should publish only from master."),
    ("Ownership of publishing",
     "Both repositories sit under a personal GitHub account and publish with one personal token. "
     "Today the CI secret holds the same token as the author's local NuGet configuration, so when "
     "it expires CI and the author's local restores break together. "
     "Moving the repositories into the qpacsystems organisation lets CI use the built-in token."),
    ("About this guide",
     "This document explains each function separately. That is good for reference, but new users "
     "would also benefit from 2-3 short task-based tutorials, for example 'model a door panel' "
     "and 'export a configured SPFA', built from real Q-PAC parts. Parameter tables are written by "
     "hand, so they can drift from the code when signatures change. The examples can't drift, "
     "because they are compiled when the PDF is built, but generating the tables from the XML "
     "documentation would close that gap too."),
]
