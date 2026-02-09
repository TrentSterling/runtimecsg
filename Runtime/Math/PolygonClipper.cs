using System.Collections.Generic;
using UnityEngine;

namespace RuntimeCSG
{
    /// <summary>
    /// Sutherland-Hodgman polygon clipping against a CSGPlane.
    /// Splits a polygon into front and back halves.
    /// </summary>
    public static class PolygonClipper
    {
        /// <summary>
        /// Split a polygon by a plane into front and back polygons.
        /// Either output may be null if all vertices are on one side.
        /// </summary>
        public static void Split(
            CSGPolygon polygon,
            CSGPlane plane,
            out CSGPolygon front,
            out CSGPolygon back,
            out CSGPolygon coplanarFront,
            out CSGPolygon coplanarBack,
            double epsilon = CSGPlane.DefaultEpsilon)
        {
            front = null;
            back = null;
            coplanarFront = null;
            coplanarBack = null;

            var classification = plane.ClassifyPolygon(polygon, epsilon);

            switch (classification)
            {
                case PlaneClassification.OnPlane:
                    // Check if polygon faces same direction as plane
                    double dot = polygon.Plane.A * plane.A +
                                 polygon.Plane.B * plane.B +
                                 polygon.Plane.C * plane.C;
                    if (dot > 0)
                        coplanarFront = polygon;
                    else
                        coplanarBack = polygon;
                    return;

                case PlaneClassification.Front:
                    front = polygon;
                    return;

                case PlaneClassification.Back:
                    back = polygon;
                    return;

                case PlaneClassification.Spanning:
                    SplitSpanning(polygon, plane, out front, out back, epsilon);
                    return;
            }
        }

        static void SplitSpanning(
            CSGPolygon polygon,
            CSGPlane plane,
            out CSGPolygon front,
            out CSGPolygon back,
            double epsilon)
        {
            var frontVerts = new List<CSGVertex>(polygon.Vertices.Count);
            var backVerts = new List<CSGVertex>(polygon.Vertices.Count);

            int count = polygon.Vertices.Count;
            for (int i = 0; i < count; i++)
            {
                int j = (i + 1) % count;

                var vi = polygon.Vertices[i];
                var vj = polygon.Vertices[j];

                double di = plane.DistanceTo(vi.Position);
                double dj = plane.DistanceTo(vj.Position);

                var ci = Classify(di, epsilon);
                var cj = Classify(dj, epsilon);

                if (ci != PlaneClassification.Back)
                    frontVerts.Add(vi);
                if (ci != PlaneClassification.Front)
                    backVerts.Add(vi);

                if ((ci == PlaneClassification.Front && cj == PlaneClassification.Back) ||
                    (ci == PlaneClassification.Back && cj == PlaneClassification.Front))
                {
                    float t = (float)(di / (di - dj));
                    t = Mathf.Clamp01(t);
                    var mid = CSGVertex.Lerp(vi, vj, t);
                    frontVerts.Add(mid);
                    backVerts.Add(mid);
                }
            }

            front = frontVerts.Count >= 3
                ? new CSGPolygon(frontVerts, polygon.Plane, polygon.MaterialIndex)
                : null;
            back = backVerts.Count >= 3
                ? new CSGPolygon(backVerts, polygon.Plane, polygon.MaterialIndex)
                : null;
        }

        static PlaneClassification Classify(double distance, double epsilon)
        {
            if (distance > epsilon) return PlaneClassification.Front;
            if (distance < -epsilon) return PlaneClassification.Back;
            return PlaneClassification.OnPlane;
        }
    }
}
