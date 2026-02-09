using System.Collections.Generic;
using UnityEngine;

namespace RuntimeCSG
{
    /// <summary>
    /// Convex polygon defined by an ordered list of vertices, a supporting plane, and a material index.
    /// </summary>
    public class CSGPolygon
    {
        public List<CSGVertex> Vertices;
        public CSGPlane Plane;
        public int MaterialIndex;

        public CSGPolygon()
        {
            Vertices = new List<CSGVertex>();
            Plane = default;
            MaterialIndex = 0;
        }

        public CSGPolygon(List<CSGVertex> vertices, int materialIndex = 0)
        {
            Vertices = vertices;
            MaterialIndex = materialIndex;
            RecalculatePlane();
        }

        public CSGPolygon(List<CSGVertex> vertices, CSGPlane plane, int materialIndex = 0)
        {
            Vertices = vertices;
            Plane = plane;
            MaterialIndex = materialIndex;
        }

        public void RecalculatePlane()
        {
            if (Vertices.Count >= 3)
            {
                Plane = CSGPlane.FromPoints(
                    Vertices[0].Position,
                    Vertices[1].Position,
                    Vertices[2].Position
                );
            }
        }

        public CSGPolygon Clone()
        {
            var clonedVerts = new List<CSGVertex>(Vertices.Count);
            for (int i = 0; i < Vertices.Count; i++)
                clonedVerts.Add(Vertices[i]);
            return new CSGPolygon(clonedVerts, Plane, MaterialIndex);
        }

        /// <summary>
        /// Reverse winding order and flip the plane.
        /// </summary>
        public void Flip()
        {
            Vertices.Reverse();
            for (int i = 0; i < Vertices.Count; i++)
            {
                var v = Vertices[i];
                v.Flip();
                Vertices[i] = v;
            }
            Plane = Plane.Flipped();
        }

        public bool IsDegenerate(float minArea = 1e-6f)
        {
            if (Vertices.Count < 3) return true;

            // Check triangle area using cross product
            Vector3 area = Vector3.zero;
            var v0 = Vertices[0].Position;
            for (int i = 1; i < Vertices.Count - 1; i++)
            {
                area += Vector3.Cross(
                    Vertices[i].Position - v0,
                    Vertices[i + 1].Position - v0
                );
            }
            return area.sqrMagnitude < minArea * minArea;
        }
    }
}
