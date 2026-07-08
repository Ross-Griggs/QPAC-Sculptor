using System;

namespace QPAC.Sculptor
{
    /// <summary>
    /// A rigid transform — a rotation plus a translation — applied to a shape's geometry
    /// as <c>p → R·p + t</c>. Build one with <see cref="RotationAbout"/> (rotate a number
    /// of degrees around an axis line) and stack several with <see cref="Then"/>.
    /// <see cref="Identity"/> leaves geometry untouched. This is the single rotation model
    /// every shape shares — see <see cref="StepShapeTransformExtensions.RotateAbout"/> and
    /// <see cref="StepPart.RotateAbout"/>.
    /// </summary>
    public readonly struct Transform
    {
        // Rotation matrix R (row-major) and translation t. A point maps to R·p + t;
        // a direction maps to R·d (translation does not affect directions).
        public readonly double M11, M12, M13;
        public readonly double M21, M22, M23;
        public readonly double M31, M32, M33;
        public readonly double Tx, Ty, Tz;

        public Transform(
            double m11, double m12, double m13,
            double m21, double m22, double m23,
            double m31, double m32, double m33,
            double tx, double ty, double tz)
        {
            M11 = m11; M12 = m12; M13 = m13;
            M21 = m21; M22 = m22; M23 = m23;
            M31 = m31; M32 = m32; M33 = m33;
            Tx = tx; Ty = ty; Tz = tz;
        }

        public static Transform Identity => new Transform(1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0);

        public bool IsIdentity =>
            M11 == 1 && M12 == 0 && M13 == 0 &&
            M21 == 0 && M22 == 1 && M23 == 0 &&
            M31 == 0 && M32 == 0 && M33 == 1 &&
            Tx == 0 && Ty == 0 && Tz == 0;

        /// <summary>
        /// Rotation by <paramref name="degrees"/> (right-hand rule) around the axis line
        /// through <paramref name="point"/> pointing along <paramref name="axis"/>. The
        /// point stays fixed and everything else swings around it. A zero-length axis
        /// yields <see cref="Identity"/>.
        /// </summary>
        public static Transform RotationAbout(
            (double X, double Y, double Z) point,
            (double X, double Y, double Z) axis,
            double degrees)
        {
            double len = Math.Sqrt(axis.X * axis.X + axis.Y * axis.Y + axis.Z * axis.Z);
            if (len < 1e-12) return Identity;
            double ux = axis.X / len, uy = axis.Y / len, uz = axis.Z / len;

            double rad = degrees * Math.PI / 180.0;
            double c = Math.Cos(rad), s = Math.Sin(rad), t = 1 - c;

            // Rodrigues' rotation matrix for a unit axis (ux,uy,uz).
            double m11 = c + ux * ux * t, m12 = ux * uy * t - uz * s, m13 = ux * uz * t + uy * s;
            double m21 = uy * ux * t + uz * s, m22 = c + uy * uy * t, m23 = uy * uz * t - ux * s;
            double m31 = uz * ux * t - uy * s, m32 = uz * uy * t + ux * s, m33 = c + uz * uz * t;

            // Keep `point` fixed: t = point − R·point.
            double tx = point.X - (m11 * point.X + m12 * point.Y + m13 * point.Z);
            double ty = point.Y - (m21 * point.X + m22 * point.Y + m23 * point.Z);
            double tz = point.Z - (m31 * point.X + m32 * point.Y + m33 * point.Z);

            return new Transform(m11, m12, m13, m21, m22, m23, m31, m32, m33, tx, ty, tz);
        }

        /// <summary>
        /// A transform whose rotation maps the local X/Y/Z axes onto the given basis
        /// vectors while pivoting about <paramref name="center"/>. Backs the legacy
        /// <see cref="Rectangle.Oriented"/> helper so callers that pass raw basis vectors
        /// still work unchanged.
        /// </summary>
        public static Transform FromBasis(
            (double X, double Y, double Z) center,
            (double X, double Y, double Z) ax,
            (double X, double Y, double Z) ay,
            (double X, double Y, double Z) az)
        {
            // R's columns are the target axes.
            double m11 = ax.X, m12 = ay.X, m13 = az.X;
            double m21 = ax.Y, m22 = ay.Y, m23 = az.Y;
            double m31 = ax.Z, m32 = ay.Z, m33 = az.Z;
            double tx = center.X - (m11 * center.X + m12 * center.Y + m13 * center.Z);
            double ty = center.Y - (m21 * center.X + m22 * center.Y + m23 * center.Z);
            double tz = center.Z - (m31 * center.X + m32 * center.Y + m33 * center.Z);
            return new Transform(m11, m12, m13, m21, m22, m23, m31, m32, m33, tx, ty, tz);
        }

        /// <summary>Maps a point: <c>R·p + t</c>.</summary>
        public (double X, double Y, double Z) Apply((double X, double Y, double Z) p) =>
            (M11 * p.X + M12 * p.Y + M13 * p.Z + Tx,
             M21 * p.X + M22 * p.Y + M23 * p.Z + Ty,
             M31 * p.X + M32 * p.Y + M33 * p.Z + Tz);

        /// <summary>Maps a direction: <c>R·d</c> (no translation).</summary>
        public (double X, double Y, double Z) ApplyDirection((double X, double Y, double Z) d) =>
            (M11 * d.X + M12 * d.Y + M13 * d.Z,
             M21 * d.X + M22 * d.Y + M23 * d.Z,
             M31 * d.X + M32 * d.Y + M33 * d.Z);

        /// <summary>
        /// Compose so that <c>this</c> is applied first, then <paramref name="next"/>.
        /// Lets rotations stack, e.g. spin about Z then tilt about the radial axis.
        /// </summary>
        public Transform Then(Transform next)
        {
            double r11 = next.M11 * M11 + next.M12 * M21 + next.M13 * M31;
            double r12 = next.M11 * M12 + next.M12 * M22 + next.M13 * M32;
            double r13 = next.M11 * M13 + next.M12 * M23 + next.M13 * M33;
            double r21 = next.M21 * M11 + next.M22 * M21 + next.M23 * M31;
            double r22 = next.M21 * M12 + next.M22 * M22 + next.M23 * M32;
            double r23 = next.M21 * M13 + next.M22 * M23 + next.M23 * M33;
            double r31 = next.M31 * M11 + next.M32 * M21 + next.M33 * M31;
            double r32 = next.M31 * M12 + next.M32 * M22 + next.M33 * M32;
            double r33 = next.M31 * M13 + next.M32 * M23 + next.M33 * M33;

            double tx = next.M11 * Tx + next.M12 * Ty + next.M13 * Tz + next.Tx;
            double ty = next.M21 * Tx + next.M22 * Ty + next.M23 * Tz + next.Ty;
            double tz = next.M31 * Tx + next.M32 * Ty + next.M33 * Tz + next.Tz;

            return new Transform(r11, r12, r13, r21, r22, r23, r31, r32, r33, tx, ty, tz);
        }
    }

    /// <summary>
    /// Rotation helpers shared by every <see cref="StepShape"/>. Because these are
    /// generic, chaining preserves the concrete type — e.g.
    /// <c>new Cone(...).RotateAbout(...)</c> still returns a <see cref="Cone"/>.
    /// </summary>
    public static class StepShapeTransformExtensions
    {
        /// <summary>
        /// Rotate this shape <paramref name="degrees"/> about the axis line through
        /// <paramref name="point"/> pointing along <paramref name="axis"/>. Works on any
        /// shape type and stacks with any rotation already applied.
        /// </summary>
        public static T RotateAbout<T>(
            this T shape,
            (double X, double Y, double Z) point,
            (double X, double Y, double Z) axis,
            double degrees) where T : StepShape
        {
            shape.Xf = shape.Xf.Then(Transform.RotationAbout(point, axis, degrees));
            return shape;
        }
    }
}
