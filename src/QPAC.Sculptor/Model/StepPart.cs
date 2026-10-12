using System.Collections.Generic;
using System.Linq;

namespace QPAC.Sculptor
{
    /// <summary>One component in the exported assembly tree, made of any number of shapes.</summary>
    public class StepPart
    {
        public string Name;
        public List<StepShape> shapes = new List<StepShape>();
        public string Type;

        /// <summary>Colour for every shape in this part that does not set its own.</summary>
        public StepColor? Color;

        public StepPart(string name) => Name = name;

        public StepPart(string name, StepColor color) : this(name) => Color = color;

        public T Add<T>(T obj) where T : StepShape
        {
            shapes.Add(obj);
            return obj;
        }

        /// <summary>Rotate every shape <paramref name="degrees"/> about the axis line through <paramref name="point"/> along <paramref name="axis"/>.</summary>
        public StepPart RotateAbout(
            (double X, double Y, double Z) point,
            (double X, double Y, double Z) axis,
            double degrees)
            => TransformBy(Transform.RotationAbout(point, axis, degrees));

        public StepPart Translate(double dx, double dy, double dz) => TransformBy(Transform.Translation(dx, dy, dz));

        public StepPart MirrorAcross((double X, double Y, double Z) point, (double X, double Y, double Z) normal)
            => TransformBy(Transform.Mirror(point, normal));

        public StepPart TransformBy(Transform transform)
        {
            foreach (var s in shapes) s.Xf = s.Xf.Then(transform);
            return this;
        }

        public StepColor? ColorOf(StepShape shape) => shape.Color ?? Color;

        public double ComputeVolume() => shapes.Sum(s => s.ComputeVolume());
    }

    /// <summary>A placed copy of a <see cref="StepPart"/>; see <see cref="StepFile.AddInstance(StepPart, Transform, string)"/>.</summary>
    public class StepInstance
    {
        public StepPart Part;
        public Transform Placement;
        public string Name;

        public StepInstance(StepPart part, Transform placement, string name = null)
        {
            Part = part;
            Placement = placement;
            Name = name ?? part.Name;
        }
    }
}
