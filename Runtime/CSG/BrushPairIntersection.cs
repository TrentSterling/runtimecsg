using System.Collections.Generic;
using UnityEngine;

namespace RuntimeCSG
{
    /// <summary>
    /// Determines brush overlap and categorizes polygons/points against brush volumes.
    /// </summary>
    public static class BrushPairIntersection
    {
        /// <summary>
        /// Returns true if two brushes geometrically overlap using a separating-plane test.
        /// For each plane of brush A, checks if all vertices of B are in front (separated).
        /// Symmetric check for brush B's planes against A's vertices.
        /// </summary>
        public static bool BrushesOverlap(List<CSGPlane> planesA, List<CSGPlane> planesB)
        {
            // Test A's planes: if all of B's vertices are in front of any A plane, they're separated
            if (IsSeparatedByPlanes(planesA, planesB))
                return false;

            // Test B's planes against A's vertices
            if (IsSeparatedByPlanes(planesB, planesA))
                return false;

            return true;
        }

        static bool IsSeparatedByPlanes(List<CSGPlane> testPlanes, List<CSGPlane> otherPlanes)
        {
            // Compute vertices of the other brush via three-plane intersection
            var otherVertices = ComputeVertices(otherPlanes);
            if (otherVertices.Count == 0) return false;

            for (int i = 0; i < testPlanes.Count; i++)
            {
                bool allInFront = true;
                for (int v = 0; v < otherVertices.Count; v++)
                {
                    if (testPlanes[i].DistanceTo(otherVertices[v]) < -CSGPlane.DefaultEpsilon)
                    {
                        allInFront = false;
                        break;
                    }
                }
                if (allInFront) return true;
            }

            return false;
        }

        static List<Vector3> ComputeVertices(List<CSGPlane> planes)
        {
            var vertices = new List<Vector3>();
            int n = planes.Count;
            for (int i = 0; i < n - 2; i++)
            {
                for (int j = i + 1; j < n - 1; j++)
                {
                    for (int k = j + 1; k < n; k++)
                    {
                        if (BrushBoundsUtil.IntersectThreePlanes(planes[i], planes[j], planes[k], out var pt))
                        {
                            if (BrushBoundsUtil.IsInsideAllPlanes(pt, planes))
                                vertices.Add(pt);
                        }
                    }
                }
            }
            return vertices;
        }

        /// <summary>
        /// Categorize a single point against a brush's planes.
        /// - Outside any plane → Outside
        /// - On one or more planes (within epsilon) and not outside any → Aligned/ReverseAligned
        /// - Inside all planes → Inside
        /// </summary>
        public static PolygonCategory CategorizePoint(
            Vector3 point, List<CSGPlane> planes, Vector3 polygonNormal)
        {
            bool onPlane = false;
            int onPlaneIndex = -1;

            for (int i = 0; i < planes.Count; i++)
            {
                double dist = planes[i].DistanceTo(point);
                if (dist > CSGPlane.DefaultEpsilon)
                    return PolygonCategory.Outside;
                if (dist > -CSGPlane.DefaultEpsilon)
                {
                    onPlane = true;
                    onPlaneIndex = i;
                }
            }

            if (onPlane)
            {
                // Compare polygon normal with the brush plane normal
                var planeNormal = planes[onPlaneIndex].Normal;
                float dot = Vector3.Dot(polygonNormal, planeNormal);
                return dot > 0 ? PolygonCategory.Aligned : PolygonCategory.ReverseAligned;
            }

            return PolygonCategory.Inside;
        }

        /// <summary>
        /// Categorize a polygon fragment against a brush's planes.
        /// The polygon should already be split so it doesn't span any plane.
        /// Uses the polygon centroid for robust classification — vertex-based
        /// classification fails when vertices lie on edges/corners of the brush.
        /// </summary>
        public static PolygonCategory CategorizePolygon(CSGPolygon polygon, List<CSGPlane> planes)
        {
            Vector3 centroid = Vector3.zero;
            for (int i = 0; i < polygon.Vertices.Count; i++)
                centroid += polygon.Vertices[i].Position;
            centroid /= polygon.Vertices.Count;

            return CategorizePoint(centroid, planes, polygon.Plane.Normal);
        }
    }
}
