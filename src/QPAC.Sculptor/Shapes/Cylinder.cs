using System;
using System.Collections.Generic;

namespace QPAC.Sculptor
{
    /// <summary>
    /// A faceted cylinder between two arbitrary points. <see cref="Sides"/> sets the facet count shared
    /// by the 3D preview and the STEP export.
    /// </summary>
    public class Cylinder : StepShape
    {
        public string Name;
        public double X1, Y1, Z1;
        public double X2, Y2, Z2;
        public double Radius;
        public int Sides;

        /// <summary>Rounds both end rims.</summary>
        public double EdgeRadius;
        public int EdgeSegments = 6;

        public Cylinder(string name, double x1, double y1, double z1, double x2, double y2, double z2, double radius, int sides = 16)
        {
            Name = name;
            X1 = x1;
            Y1 = y1;
            Z1 = z1;
            X2 = x2;
            Y2 = y2;
            Z2 = z2;
            Radius = radius;
            Sides = sides;
        }

        public Cylinder(string name, (double X, double Y, double Z) start, (double X, double Y, double Z) end, double radius, int sides = 16)
            : this(name, start.X, start.Y, start.Z, end.X, end.Y, end.Z, radius, sides) { }

        public override string ShapeName => Name;

        public Cylinder WithFillet(double radius, int segments = 6)
        {
            EdgeRadius = radius;
            EdgeSegments = segments;
            return this;
        }

        public override Polyhedron CreateSolid()
        {
            var axis = (X2 - X1, Y2 - Y1, Z2 - Z1);
            double len = Vec.Length(axis);
            if (len < 1e-9) return new Polyhedron();
            double r = Math.Max(1e-4, Radius);
            var profile = new List<(double R, double Z)> { (0, 0), (r, 0), (r, len), (0, len) };
            var rev = new Revolve(Name, (X1, Y1, Z1), axis, profile, 360, Math.Max(8, Sides));
            if (EdgeRadius > 0) rev.FilletProfile(EdgeRadius, EdgeSegments, 1, 2);
            return rev.CreateSolid();
        }
    }
}
