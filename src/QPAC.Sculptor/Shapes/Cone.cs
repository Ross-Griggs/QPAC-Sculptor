using System;
using System.Collections.Generic;

namespace QPAC.Sculptor
{
    /// <summary>A frustum standing on the +Z axis: base centre (<see cref="Cx"/>, <see cref="Cy"/>, <see cref="Cz"/>), rising <see cref="Height"/>.</summary>
    public class Cone : StepShape
    {
        public string Name;
        public double Cx, Cy, Cz;
        public double Height;
        public double BottomRadius, TopRadius;
        public int Sides;

        /// <summary>Rounds the bottom and top rim edges.</summary>
        public double EdgeRadius;
        public int EdgeSegments = 6;

        public Cone(string name, double cx, double cy, double cz, double height, double bottomRadius, double topRadius, int sides = 32)
        {
            Name = name;
            Cx = cx;
            Cy = cy;
            Cz = cz;
            Height = height;
            BottomRadius = bottomRadius;
            TopRadius = topRadius;
            Sides = sides;
        }

        /// <summary>Cone or frustum between two arbitrary points (any direction).</summary>
        public static Cone Between(string name, (double X, double Y, double Z) baseCenter, (double X, double Y, double Z) topCenter,
            double bottomRadius, double topRadius, int sides = 32)
        {
            var axis = Vec.Sub(topCenter, baseCenter);
            double h = Vec.Length(axis);
            var cone = new Cone(name, 0, 0, 0, h, bottomRadius, topRadius, sides);
            if (h > 1e-12)
            {
                var z = Vec.Scale(axis, 1 / h);
                cone.Xf = Transform.Placement(baseCenter, Vec.AnyPerpendicular(z), z);
            }
            return cone;
        }

        public override string ShapeName => Name;

        public Cone WithFillet(double radius, int segments = 6)
        {
            EdgeRadius = radius;
            EdgeSegments = segments;
            return this;
        }

        public override Polyhedron CreateSolid()
        {
            // Old exports clamped zero radii to a hair so a pointed cone still had a top face; keep that.
            double rb = Math.Max(1e-4, BottomRadius), rt = Math.Max(1e-4, TopRadius);
            var profile = new List<(double R, double Z)> { (0, 0), (rb, 0), (rt, Height), (0, Height) };
            var r = new Revolve(Name, (Cx, Cy, Cz), (0, 0, 1), profile, 360, Math.Max(8, Sides)) { ReferenceDirection = (1, 0, 0) };
            if (EdgeRadius > 0) r.FilletProfile(EdgeRadius, EdgeSegments, 1, 2);
            return r.CreateSolid();
        }
    }
}
