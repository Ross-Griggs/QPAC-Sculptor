using QPAC.Sculptor;
using System.Collections.Generic;
public class StepFile
{
    public List<StepPart> parts = new List<StepPart>();
    public StepPart AddPart(StepPart part)
    {
        parts.Add(part);
        return part;
    }
    public int Count
    {
        get { return parts.Count; }
    }
}