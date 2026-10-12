using System.Globalization;
using QPAC.Sculptor;
using QPAC.Sculptor.Gallery;

// Usage: dotnet run --project samples/QPAC.Sculptor.Gallery [output-folder]
// Writes gallery.step (inches), gallery-mm.step, and one file per shape plus volumes.csv for
// checking with tools/validate-step.py.
string outDir = args.Length > 0 ? args[0] : "gallery-output";
Directory.CreateDirectory(outDir);

var file = Gallery.Build();
var problems = file.Validate();
foreach (var p in problems) Console.WriteLine($"PROBLEM: {p}");

file.WriteStep(Path.Combine(outDir, "gallery.step"), "Gallery");
file.WriteStep(Path.Combine(outDir, "gallery-mm.step"), "Gallery", new StepWriteOptions { ExportUnits = LengthUnit.Millimeter });

var shapesDir = Directory.CreateDirectory(Path.Combine(outDir, "shapes")).FullName;
using var csv = new StreamWriter(Path.Combine(outDir, "volumes.csv"));
csv.WriteLine("file,volume_in3");
int n = 0;
foreach (var part in file.parts.Append(file.instances[0].Part))
    foreach (var shape in part.shapes)
    {
        var single = new StepFile();
        single.AddPart(part.Name).Add(shape);
        string name = $"{++n:00}-{string.Concat(shape.ShapeName.Where(char.IsLetterOrDigit))}.step";
        single.WriteStep(Path.Combine(shapesDir, name), shape.ShapeName);
        csv.WriteLine($"{name},{shape.ComputeVolume().ToString("R", CultureInfo.InvariantCulture)}");
    }

var (min, max) = file.Bounds();
Console.WriteLine($"Wrote {n} shapes to {Path.GetFullPath(outDir)}; bounds {min} .. {max}; {problems.Count} problems");
return problems.Count == 0 ? 0 : 1;
