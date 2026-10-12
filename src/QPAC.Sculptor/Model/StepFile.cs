using QPAC.Sculptor;
using System.Collections.Generic;
using System.Linq;

// Lives in the global namespace because published consumers (QPACSPFA, FS1Product) were compiled
// against it there; moving it is a binary break.
public class StepFile
{
    public List<StepPart> parts = new List<StepPart>();

    /// <summary>Extra placed copies of parts; written once as a definition and referenced per copy.</summary>
    public List<StepInstance> instances = new List<StepInstance>();

    /// <summary>The unit every coordinate in this model is expressed in. Defaults to inches.</summary>
    public LengthUnit Units = LengthUnit.Inch;

    public string Author = "";
    public string Organization = "Q-PAC";
    public string Description = "";

    public StepPart AddPart(StepPart part)
    {
        parts.Add(part);
        return part;
    }

    public StepPart AddPart(string name) => AddPart(new StepPart(name));

    /// <summary>
    /// Places a copy of <paramref name="part"/> with a rigid <paramref name="placement"/>. The part does
    /// not need to be in <see cref="parts"/>; CAD tools show every copy as the same component.
    /// </summary>
    public StepInstance AddInstance(StepPart part, Transform placement, string name = null)
    {
        var inst = new StepInstance(part, placement, name);
        instances.Add(inst);
        return inst;
    }

    public StepInstance AddInstance(StepPart part, double dx, double dy, double dz, string name = null)
        => AddInstance(part, Transform.Translation(dx, dy, dz), name);

    public int Count
    {
        get { return parts.Count; }
    }

    /// <summary>Every solid in world coordinates (instances included), paired with its part.</summary>
    public IEnumerable<(StepPart Part, StepShape Shape, Polyhedron Solid)> Solids()
    {
        foreach (var p in parts)
            foreach (var s in p.shapes)
                yield return (p, s, s.ToPolyhedron());
        foreach (var i in instances)
            foreach (var s in i.Part.shapes)
                yield return (i.Part, s, s.ToPolyhedron().Transformed(i.Placement));
    }

    public ((double X, double Y, double Z) Min, (double X, double Y, double Z) Max) Bounds()
    {
        var all = Solids().Select(s => s.Solid).Where(s => s.Vertices.Count > 0).ToList();
        if (all.Count == 0) return ((0, 0, 0), (0, 0, 0));
        return ((all.Min(s => s.Min.X), all.Min(s => s.Min.Y), all.Min(s => s.Min.Z)),
                (all.Max(s => s.Max.X), all.Max(s => s.Max.Y), all.Max(s => s.Max.Z)));
    }

    /// <summary>
    /// Checks every shape and instance and returns readable problems (empty when the model will
    /// export as clean solids).
    /// </summary>
    public List<string> Validate()
    {
        var problems = new List<string>();
        foreach (var p in parts.Concat(instances.Select(i => i.Part)).Distinct())
            foreach (var s in p.shapes)
            {
                try
                {
                    foreach (var issue in s.ToPolyhedron().Validate())
                        problems.Add($"{p.Name} / {s.ShapeName}: {issue}");
                }
                catch (System.ArgumentException ex)
                {
                    problems.Add($"{p.Name} / {s.ShapeName}: {ex.Message}");
                }
            }
        foreach (var i in instances)
            if (!i.Placement.IsRigid)
                problems.Add($"instance '{i.Name}': placement must be a pure rotation + translation (no scale or mirror)");
        return problems;
    }

    public void WriteStep(string path, string productName) => Step.WriteStep(path, this, productName);

    public void WriteStep(string path, string productName, StepWriteOptions options) => Step.WriteStep(path, this, productName, options);
}
