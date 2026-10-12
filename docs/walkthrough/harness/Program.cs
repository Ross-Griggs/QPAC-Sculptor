using System.Globalization;
using System.Reflection;
using System.Text;
using QPAC.Sculptor;

// Runs every walkthrough example (generated into Snippets.g.cs by build.py) and writes, per example:
//   <id>.json  triangles + sharp edges for the renderer, or the 2D loop for profile examples
//   <id>.txt   anything the example printed
//   <id>.step  written by the example itself
// Exit code = number of failed examples.
string outDir = Path.GetFullPath(args.Length > 0 ? args[0] : "out");
Directory.CreateDirectory(outDir);
Directory.SetCurrentDirectory(outDir);
int failed = 0;

foreach (var m in typeof(Walkthrough.Snippets).GetMethods(BindingFlags.Public | BindingFlags.Static).OrderBy(m => m.Name))
{
    string id = m.Name.Substring(m.Name.IndexOf('_') + 1);
    var console = new StringWriter();
    var original = Console.Out;
    Console.SetOut(console);
    try
    {
        object result = m.Invoke(null, null);
        Console.SetOut(original);
        if (result is StepFile file) File.WriteAllText($"{id}.json", MeshJson(file));
        else if (result is List<(double U, double V)> loop) File.WriteAllText($"{id}.json", LoopJson(loop));
        File.WriteAllText($"{id}.txt", console.ToString());
        Console.WriteLine($"OK   {id}");
    }
    catch (TargetInvocationException e)
    {
        Console.SetOut(original);
        failed++;
        Console.WriteLine($"FAIL {id}: {e.InnerException?.GetType().Name}: {e.InnerException?.Message}");
    }
}
return failed;

static string N(double v) => Math.Round(v, 5).ToString(CultureInfo.InvariantCulture);

static string LoopJson(List<(double U, double V)> loop)
    => "{\"loop\":[" + string.Join(",", loop.Select(p => $"[{N(p.U)},{N(p.V)}]")) + "]}";

static string MeshJson(StepFile file)
{
    var sb = new StringBuilder("{\"solids\":[");
    bool firstSolid = true;
    var edges = new StringBuilder();
    foreach (var (part, shape, solid) in file.Solids())
    {
        if (solid.Faces.Count == 0) continue;
        var c = part.ColorOf(shape) ?? StepColor.SheetMetal;
        if (!firstSolid) sb.Append(',');
        firstSolid = false;
        sb.Append($"{{\"color\":[{c.R},{c.G},{c.B}],\"tris\":[");
        bool firstTri = true;
        foreach (var (a, b, cc) in Tessellator.Triangulate(solid))
        {
            if (!firstTri) sb.Append(',');
            firstTri = false;
            var p = solid.Vertices[a]; var q = solid.Vertices[b]; var r = solid.Vertices[cc];
            sb.Append($"[{N(p.X)},{N(p.Y)},{N(p.Z)},{N(q.X)},{N(q.Y)},{N(q.Z)},{N(r.X)},{N(r.Y)},{N(r.Z)}]");
        }
        sb.Append("]}");

        // Sharp edges (faces meeting at more than 30°) give the picture its outlines.
        var normals = solid.Faces.Select(solid.FaceNormal).ToList();
        var owner = new Dictionary<(int, int), int>();
        for (int f = 0; f < solid.Faces.Count; f++)
            foreach (var loop in solid.Faces[f].Loops)
                for (int k = 0; k < loop.Length; k++)
                {
                    int a = loop[k], b = loop[(k + 1) % loop.Length];
                    var key = a < b ? (a, b) : (b, a);
                    if (!owner.TryGetValue(key, out int g)) { owner[key] = f; continue; }
                    var n1 = normals[f]; var n2 = normals[g];
                    if (n1.X * n2.X + n1.Y * n2.Y + n1.Z * n2.Z > 0.866) continue;
                    var p = solid.Vertices[a]; var q = solid.Vertices[b];
                    if (edges.Length > 0) edges.Append(',');
                    edges.Append($"[{N(p.X)},{N(p.Y)},{N(p.Z)},{N(q.X)},{N(q.Y)},{N(q.Z)}]");
                }
    }
    sb.Append("],\"edges\":[").Append(edges).Append("]}");
    return sb.ToString();
}
