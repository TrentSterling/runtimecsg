using System.Collections.Generic;
using UnityEngine;

namespace RuntimeCSG
{
    /// <summary>
    /// Utility for computing axis-aligned bounding boxes from brush planes.
    /// Finds the convex hull vertices by intersecting plane triplets.
    /// </summary>
    public static class BrushBoundsUtil
    {
        /// <summary>
        /// Compute the AABB of a convex brush defined by a set of planes.
        /// Works by finding all vertices (plane triplet intersections) that lie inside all planes.
        /// </summary>
        public static Bounds ComputeBounds(List<CSGPlane> planes)
        {
            if (planes == null || planes.Count < 4)
                return new Bounds(Vector3.zero, Vector3.zero);

            var points = new List<Vector3>();
            int count = planes.Count;

            for (int i = 0; i < count - 2; i++)
            {
                for (int j = i + 1; j < count - 1; j++)
                {
                    for (int k = j + 1; k < count; k++)
                    {
                        if (IntersectThreePlanes(planes[i], planes[j], planes[k], out var point))
                        {
                            // Check if point is inside (or on) all planes
                            if (IsInsideAllPlanes(point, planes))
                                points.Add(point);
                        }
                    }
                }
            }

            if (points.Count == 0)
                return new Bounds(Vector3.zero, Vector3.zero);

            var min = points[0];
            var max = points[0];
            for (int i = 1; i < points.Count; i++)
            {
                min = Vector3.Min(min, points[i]);
                max = Vector3.Max(max, points[i]);
            }

            var bounds = new Bounds();
            bounds.SetMinMax(min, max);
            return bounds;
        }

        /// <summary>
        /// Find the intersection point of three planes.
        /// Each plane: Ax + By + Cz + D = 0, so the system is Ax + By + Cz = -D.
        /// Uses the cross-product formula: point = (-D0*(N1×N2) + -D1*(N2×N0) + -D2*(N0×N1)) / det
        /// </summary>
        static bool IntersectThreePlanes(CSGPlane p0, CSGPlane p1, CSGPlane p2, out Vector3 point)
        {
            point = Vector3.zero;

            // Normal vectors (as doubles for precision)
            var n0 = new Vector3d(p0.A, p0.B, p0.C);
            var n1 = new Vector3d(p1.A, p1.B, p1.C);
            var n2 = new Vector3d(p2.A, p2.B, p2.C);

            var n1xn2 = Vector3d.Cross(n1, n2);
            double det = Vector3d.Dot(n0, n1xn2);

            if (System.Math.Abs(det) < 1e-10)
                return false;

            var n2xn0 = Vector3d.Cross(n2, n0);
            var n0xn1 = Vector3d.Cross(n0, n1);

            double invDet = 1.0 / det;
            double x = (-p0.D * n1xn2.x + -p1.D * n2xn0.x + -p2.D * n0xn1.x) * invDet;
            double y = (-p0.D * n1xn2.y + -p1.D * n2xn0.y + -p2.D * n0xn1.y) * invDet;
            double z = (-p0.D * n1xn2.z + -p1.D * n2xn0.z + -p2.D * n0xn1.z) * invDet;

            if (double.IsNaN(x) || double.IsInfinity(x) ||
                double.IsNaN(y) || double.IsInfinity(y) ||
                double.IsNaN(z) || double.IsInfinity(z))
                return false;

            point = new Vector3((float)x, (float)y, (float)z);
            return true;
        }

        struct Vector3d
        {
            public double x, y, z;
            public Vector3d(double x, double y, double z) { this.x = x; this.y = y; this.z = z; }
            public static Vector3d Cross(Vector3d a, Vector3d b) =>
                new Vector3d(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
            public static double Dot(Vector3d a, Vector3d b) => a.x * b.x + a.y * b.y + a.z * b.z;
        }

        static bool IsInsideAllPlanes(Vector3 point, List<CSGPlane> planes, double epsilon = 1e-4)
        {
            for (int i = 0; i < planes.Count; i++)
            {
                if (planes[i].DistanceTo(point) > epsilon)
                    return false;
            }
            return true;
        }
    }
}
