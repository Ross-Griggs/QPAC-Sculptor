namespace QPAC.Sculptor
{
    /// <summary>
    /// Base for every solid. <see cref="Xf"/> is applied to the shape's geometry at export and render
    /// time; set it fluently with the extension methods in <see cref="StepShapeTransformExtensions"/>.
    /// Derive from this and override <see cref="CreateSolid"/> to add a custom shape.
    /// </summary>
    public abstract class StepShape
    {
        public Transform Xf = Transform.Identity;

        /// <summary>Overrides the part colour for this shape (STEP export and WPF preview).</summary>
        public StepColor? Color;

        public virtual string ShapeName => GetType().Name;

        /// <summary>The solid in the shape's own coordinates, before <see cref="Xf"/>. Override to add a custom shape.</summary>
        public virtual Polyhedron CreateSolid() => new Polyhedron();

        /// <summary>The finished solid: welded, oriented outward and moved by <see cref="Xf"/>.</summary>
        public Polyhedron ToPolyhedron() => CreateSolid().Normalize().Transformed(Xf);

        public double ComputeVolume() => ToPolyhedron().Volume;
    }

    /// <summary>
    /// Transform helpers shared by every <see cref="StepShape"/>. Chaining preserves the concrete type —
    /// <c>new Cone(...).RotateAbout(...)</c> still returns a <see cref="Cone"/>.
    /// </summary>
    public static class StepShapeTransformExtensions
    {
        /// <summary>Rotate <paramref name="degrees"/> about the axis line through <paramref name="point"/> along <paramref name="axis"/>.</summary>
        public static T RotateAbout<T>(
            this T shape,
            (double X, double Y, double Z) point,
            (double X, double Y, double Z) axis,
            double degrees) where T : StepShape
        {
            shape.Xf = shape.Xf.Then(Transform.RotationAbout(point, axis, degrees));
            return shape;
        }

        public static T Translate<T>(this T shape, double dx, double dy, double dz) where T : StepShape
        {
            shape.Xf = shape.Xf.Then(Transform.Translation(dx, dy, dz));
            return shape;
        }

        /// <summary>Reflect across the plane through <paramref name="point"/> with the given normal.</summary>
        public static T MirrorAcross<T>(this T shape, (double X, double Y, double Z) point, (double X, double Y, double Z) normal) where T : StepShape
        {
            shape.Xf = shape.Xf.Then(Transform.Mirror(point, normal));
            return shape;
        }

        public static T TransformBy<T>(this T shape, Transform transform) where T : StepShape
        {
            shape.Xf = shape.Xf.Then(transform);
            return shape;
        }

        public static T WithColor<T>(this T shape, StepColor color) where T : StepShape
        {
            shape.Color = color;
            return shape;
        }
    }
}
