using UnityEngine;

namespace RuntimeCSG
{
    /// <summary>
    /// Planar UV projection for CSG polygon faces.
    /// Projects UVs based on the polygon's plane normal, using the two most significant axes.
    /// </summary>
    public static class UVProjector
    {
        /// <summary>
        /// Apply planar UV projection to all vertices in a polygon.
        /// </summary>
        public static void ProjectPlanar(CSGPolygon polygon, float uvScale = 1f)
        {
            if (polygon.Vertices.Count == 0) return;

            Vector3 normal = polygon.Plane.Normal;
            GetProjectionAxes(normal, out var uAxis, out var vAxis);

            for (int i = 0; i < polygon.Vertices.Count; i++)
            {
                var vert = polygon.Vertices[i];
                vert.UV = new Vector2(
                    Vector3.Dot(vert.Position, uAxis) * uvScale,
                    Vector3.Dot(vert.Position, vAxis) * uvScale
                );
                polygon.Vertices[i] = vert;
            }
        }

        /// <summary>
        /// Get the U and V projection axes for a given face normal.
        /// Uses the two axes most perpendicular to the normal.
        /// </summary>
        public static void GetProjectionAxes(Vector3 normal, out Vector3 uAxis, out Vector3 vAxis)
        {
            float ax = Mathf.Abs(normal.x);
            float ay = Mathf.Abs(normal.y);
            float az = Mathf.Abs(normal.z);

            if (ax >= ay && ax >= az)
            {
                // X-dominant: project onto YZ
                uAxis = Vector3.forward;
                vAxis = Vector3.up;
            }
            else if (ay >= ax && ay >= az)
            {
                // Y-dominant: project onto XZ
                uAxis = Vector3.right;
                vAxis = Vector3.forward;
            }
            else
            {
                // Z-dominant: project onto XY
                uAxis = Vector3.right;
                vAxis = Vector3.up;
            }
        }
    }
}
