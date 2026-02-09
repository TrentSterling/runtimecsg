using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace RuntimeCSG
{
    public enum PlaneClassification
    {
        Front,
        Back,
        OnPlane,
        Spanning
    }

    /// <summary>
    /// Plane defined by normal + distance (Ax + By + Cz + D = 0).
    /// Double precision for CSG accuracy, float conversion for mesh output.
    /// </summary>
    [Serializable]
    public readonly struct CSGPlane : IEquatable<CSGPlane>
    {
        public const double DefaultEpsilon = 1e-5;

        public readonly double A, B, C, D;

        public Vector3 Normal => new Vector3((float)A, (float)B, (float)C);
        public double Distance => D;

        public CSGPlane(double a, double b, double c, double d)
        {
            double len = Math.Sqrt(a * a + b * b + c * c);
            if (len < 1e-12)
            {
                A = 0; B = 1; C = 0; D = 0;
                return;
            }
            double inv = 1.0 / len;
            A = a * inv;
            B = b * inv;
            C = c * inv;
            D = d * inv;
        }

        public CSGPlane(Vector3 normal, float distance)
            : this(normal.x, normal.y, normal.z, distance) { }

        public CSGPlane(Vector3 normal, Vector3 pointOnPlane)
            : this(normal.x, normal.y, normal.z,
                  -(normal.x * (double)pointOnPlane.x +
                    normal.y * (double)pointOnPlane.y +
                    normal.z * (double)pointOnPlane.z)) { }

        public static CSGPlane FromPoints(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            return new CSGPlane(normal, a);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double DistanceTo(Vector3 point)
        {
            return A * point.x + B * point.y + C * point.z + D;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double DistanceTo(double x, double y, double z)
        {
            return A * x + B * y + C * z + D;
        }

        public PlaneClassification ClassifyPoint(Vector3 point, double epsilon = DefaultEpsilon)
        {
            double dist = DistanceTo(point);
            if (dist > epsilon) return PlaneClassification.Front;
            if (dist < -epsilon) return PlaneClassification.Back;
            return PlaneClassification.OnPlane;
        }

        public PlaneClassification ClassifyPolygon(CSGPolygon polygon, double epsilon = DefaultEpsilon)
        {
            int front = 0, back = 0;
            for (int i = 0; i < polygon.Vertices.Count; i++)
            {
                var c = ClassifyPoint(polygon.Vertices[i].Position, epsilon);
                if (c == PlaneClassification.Front) front++;
                else if (c == PlaneClassification.Back) back++;
            }

            if (front > 0 && back > 0) return PlaneClassification.Spanning;
            if (front > 0) return PlaneClassification.Front;
            if (back > 0) return PlaneClassification.Back;
            return PlaneClassification.OnPlane;
        }

        public CSGPlane Flipped() => new CSGPlane(-A, -B, -C, -D);

        public UnityEngine.Plane ToUnityPlane()
        {
            return new UnityEngine.Plane(Normal, (float)D);
        }

        public bool Equals(CSGPlane other)
        {
            return Math.Abs(A - other.A) < DefaultEpsilon &&
                   Math.Abs(B - other.B) < DefaultEpsilon &&
                   Math.Abs(C - other.C) < DefaultEpsilon &&
                   Math.Abs(D - other.D) < DefaultEpsilon;
        }

        public override bool Equals(object obj) => obj is CSGPlane p && Equals(p);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + A.GetHashCode();
                hash = hash * 31 + B.GetHashCode();
                hash = hash * 31 + C.GetHashCode();
                hash = hash * 31 + D.GetHashCode();
                return hash;
            }
        }
        public override string ToString() => $"CSGPlane({A:F4}, {B:F4}, {C:F4}, {D:F4})";

        public static bool operator ==(CSGPlane left, CSGPlane right) => left.Equals(right);
        public static bool operator !=(CSGPlane left, CSGPlane right) => !left.Equals(right);
    }
}
