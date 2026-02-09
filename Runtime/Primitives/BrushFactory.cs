using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuntimeCSG
{
    /// <summary>
    /// Factory for generating CSG plane sets that define common brush primitives.
    /// All primitives are centered at origin in local space.
    /// </summary>
    public static class BrushFactory
    {
        /// <summary>
        /// Create a box brush from half extents. Default is a 1x1x1 cube.
        /// </summary>
        public static List<CSGPlane> Box(Vector3 halfExtents)
        {
            return new List<CSGPlane>(6)
            {
                new CSGPlane( 1,  0,  0, -halfExtents.x), // +X
                new CSGPlane(-1,  0,  0, -halfExtents.x), // -X
                new CSGPlane( 0,  1,  0, -halfExtents.y), // +Y
                new CSGPlane( 0, -1,  0, -halfExtents.y), // -Y
                new CSGPlane( 0,  0,  1, -halfExtents.z), // +Z
                new CSGPlane( 0,  0, -1, -halfExtents.z), // -Z
            };
        }

        /// <summary>
        /// Create a unit box (1x1x1).
        /// </summary>
        public static List<CSGPlane> Box()
        {
            return Box(Vector3.one * 0.5f);
        }

        /// <summary>
        /// Create a wedge (triangular prism). A box with one diagonal cut
        /// removing the top-right portion.
        /// The slope goes from +X bottom to -X top.
        /// </summary>
        public static List<CSGPlane> Wedge(Vector3 halfExtents)
        {
            // 5 planes: bottom, left, right, front, back, plus the diagonal slope
            var slopeNormal = new Vector3(halfExtents.y, halfExtents.x, 0).normalized;

            return new List<CSGPlane>(5)
            {
                new CSGPlane( 0, -1,  0, -halfExtents.y),  // Bottom
                new CSGPlane(-1,  0,  0, -halfExtents.x),  // -X
                new CSGPlane( 0,  0,  1, -halfExtents.z),  // +Z
                new CSGPlane( 0,  0, -1, -halfExtents.z),  // -Z
                new CSGPlane(slopeNormal.x, slopeNormal.y, 0, 0), // Diagonal slope
            };
        }

        /// <summary>
        /// Create a wedge with default size (1x1x1).
        /// </summary>
        public static List<CSGPlane> Wedge()
        {
            return Wedge(Vector3.one * 0.5f);
        }

        /// <summary>
        /// Create a cylinder approximated by N planar sides.
        /// </summary>
        public static List<CSGPlane> Cylinder(float radius, float halfHeight, int sides = 16)
        {
            sides = Mathf.Max(sides, 3);
            var planes = new List<CSGPlane>(sides + 2);

            // Top and bottom caps
            planes.Add(new CSGPlane(0, 1, 0, -halfHeight));
            planes.Add(new CSGPlane(0, -1, 0, -halfHeight));

            // Side planes
            for (int i = 0; i < sides; i++)
            {
                float angle = (2f * Mathf.PI * i) / sides;
                double nx = Math.Cos(angle);
                double nz = Math.Sin(angle);
                planes.Add(new CSGPlane(nx, 0, nz, -radius));
            }

            return planes;
        }

        /// <summary>
        /// Create a cylinder with default size.
        /// </summary>
        public static List<CSGPlane> Cylinder()
        {
            return Cylinder(0.5f, 0.5f, 16);
        }

        /// <summary>
        /// Create an arch (a box with a cylindrical subtraction from the bottom).
        /// Defined by outer box half extents plus inner arch radius and segments.
        /// Returns planes for the outer shape only; use a subtractive cylinder for the arch cutout.
        /// For simplicity, this returns a half-cylinder shape (top half).
        /// </summary>
        public static List<CSGPlane> Arch(float width, float height, float depth, int segments = 8)
        {
            segments = Mathf.Max(segments, 3);
            float halfWidth = width * 0.5f;
            float halfDepth = depth * 0.5f;

            var planes = new List<CSGPlane>();

            // Top
            planes.Add(new CSGPlane(0, 1, 0, -height));
            // Bottom
            planes.Add(new CSGPlane(0, -1, 0, 0));
            // Front/back
            planes.Add(new CSGPlane(0, 0, 1, -halfDepth));
            planes.Add(new CSGPlane(0, 0, -1, -halfDepth));

            // Arch curve (half circle of inward-facing planes)
            for (int i = 0; i <= segments; i++)
            {
                float angle = Mathf.PI * i / segments;
                double nx = -Math.Cos(angle);
                double ny = -Math.Sin(angle);
                planes.Add(new CSGPlane(nx, ny, 0, -halfWidth));
            }

            return planes;
        }

        /// <summary>
        /// Create a sphere approximated by planes (geodesic-ish approach).
        /// Uses latitude/longitude subdivision.
        /// </summary>
        public static List<CSGPlane> Sphere(float radius, int latSegments = 8, int lonSegments = 8)
        {
            latSegments = Mathf.Max(latSegments, 4);
            lonSegments = Mathf.Max(lonSegments, 4);

            var planes = new List<CSGPlane>();

            for (int lat = 0; lat < latSegments; lat++)
            {
                float theta = Mathf.PI * (lat + 0.5f) / latSegments;
                for (int lon = 0; lon < lonSegments; lon++)
                {
                    float phi = 2f * Mathf.PI * lon / lonSegments;

                    double nx = Math.Sin(theta) * Math.Cos(phi);
                    double ny = Math.Cos(theta);
                    double nz = Math.Sin(theta) * Math.Sin(phi);

                    planes.Add(new CSGPlane(nx, ny, nz, -radius));
                }
            }

            return planes;
        }

        /// <summary>
        /// Create a sphere with default size.
        /// </summary>
        public static List<CSGPlane> Sphere()
        {
            return Sphere(0.5f, 8, 8);
        }
    }
}
