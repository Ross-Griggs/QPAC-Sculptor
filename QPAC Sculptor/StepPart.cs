using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QPAC.Sculptor
{
    public class StepPart
    {
        public string Name;
        public List<StepShape> shapes = new List<StepShape>();
        public string Type;
        public StepPart(string name) => Name = name;

        public T Add<T>(T obj) where T : StepShape
        {
            shapes.Add(obj);
            return obj;
        }

        /// <summary>
        /// Rotate every shape in this part <paramref name="degrees"/> about the axis line
        /// through <paramref name="point"/> pointing along <paramref name="axis"/>. The
        /// part moves rigidly, so it works no matter which shape types it holds. Chainable.
        /// </summary>
        public StepPart RotateAbout(
            (double X, double Y, double Z) point,
            (double X, double Y, double Z) axis,
            double degrees)
        {
            var rot = Transform.RotationAbout(point, axis, degrees);
            foreach (var s in shapes) s.Xf = s.Xf.Then(rot);
            return this;
        }
    }

    /// <summary>
    /// Base for every drawable shape. <see cref="Xf"/> carries an optional rotation applied
    /// to the shape's geometry at STEP-export time; set it fluently with
    /// <see cref="StepShapeTransformExtensions.RotateAbout"/> (any shape) or
    /// <see cref="StepPart.RotateAbout"/> (a whole part).
    /// </summary>
    public abstract class StepShape
    {
        public Transform Xf = Transform.Identity;
    }
    public class Rectangle : StepShape
    {
        public string Name;
        public double Cx, Cy, Cz;
        public double Sx, Sy, Sz;

        public Rectangle(string name, double cx, double cy, double cz, double sx, double sy, double sz)
        {
            Name = name;
            Cx = cx;
            Cy = cy;
            Cz = cz;
            Sx = sx;
            Sy = sy;
            Sz = sz;
        }

        /// <summary>
        /// Legacy helper: orient the box so its local X/Y/Z axes align with the given basis
        /// vectors (must be orthonormal), pivoting about its center. Kept for callers that
        /// already derive basis vectors by hand; new code should prefer
        /// <see cref="StepShapeTransformExtensions.RotateAbout"/>, which takes a plain axis
        /// and angle instead.
        /// </summary>
        public Rectangle Oriented(
            (double X, double Y, double Z) ax,
            (double X, double Y, double Z) ay,
            (double X, double Y, double Z) az)
        {
            Xf = Xf.Then(Transform.FromBasis((Cx, Cy, Cz), ax, ay, az));
            return this;
        }
    }

    public class Cone : StepShape
    {
        public string Name;
        public double Cx, Cy, Cz;           
        public double Height;   
        public double BottomRadius, TopRadius;
        public int Sides;

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
    }

    /// <summary>
    /// A faceted cylinder swept between two arbitrary points in world space (inches).
    /// Unlike <see cref="Cone"/> (locked to the +Z axis), a Cylinder can point in any
    /// direction, so it suits in-plane wire-harness runs. <see cref="Sides"/> controls
    /// the facet count shared by the 3D visual and the STEP export so the two stay
    /// identical.
    /// </summary>
    public class Cylinder : StepShape
    {
        public string Name;
        public double X1, Y1, Z1;   // start point
        public double X2, Y2, Z2;   // end point
        public double Radius;
        public int Sides;

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
    }

    public class Extrusion : StepShape
    {
        public string Name;
        public (double X, double Y, double Z) Origin;
        public (double X, double Y, double Z) U;
        public (double X, double Y, double Z) V;
        public double Depth;
        public List<(double U, double V)> Outer = new List<(double U, double V)>();
        public List<List<(double U, double V)>> Cuts = new List<List<(double U, double V)>>();

        public Extrusion(
            string name,
            (double X, double Y, double Z) origin,
            (double X, double Y, double Z) u,
            (double X, double Y, double Z) v,
            double depth)
        {
            Name = name;
            Origin = origin; U = u; V = v; Depth = depth;
        }

        public static List<(double U, double V)> Circle(double cu, double cv, double radius, int sides = 32)
        {
            var loop = new List<(double U, double V)>();
            int n = Math.Max(8, sides);
            for (int i = 0; i < n; i++)
            {
                double a = 2.0 * Math.PI * i / n;
                loop.Add((cu + radius * Math.Cos(a), cv + radius * Math.Sin(a)));
            }
            return loop;
        }

        public Extrusion AddCircle(double cu, double cv, double radius, int sides = 256)
        {
            Outer = Circle(cu, cv, radius, sides);
            return this;
        }

        public Extrusion AddCircularCut(double cu, double cv, double radius, int sides = 256)
        {
            var loop = Circle(cu, cv, radius, sides);
            loop.Reverse();
            Cuts.Add(loop);
            return this;
        }
    }
}
