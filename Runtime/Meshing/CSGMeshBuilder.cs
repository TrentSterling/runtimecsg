using System.Collections.Generic;
using UnityEngine;

namespace RuntimeCSG
{
    /// <summary>
    /// Converts a list of CSG polygons into a Unity Mesh.
    /// Handles fan triangulation of convex polygons and UV projection.
    /// </summary>
    public static class CSGMeshBuilder
    {
        /// <summary>
        /// Build a Unity Mesh from a list of CSG polygons.
        /// </summary>
        public static Mesh Build(List<CSGPolygon> polygons, float uvScale = 1f)
        {
            if (polygons == null || polygons.Count == 0)
                return null;

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            for (int p = 0; p < polygons.Count; p++)
            {
                var polygon = polygons[p];
                if (polygon.Vertices.Count < 3) continue;

                // Apply UV projection
                UVProjector.ProjectPlanar(polygon, uvScale);

                int startIndex = vertices.Count;

                // Add vertices
                for (int v = 0; v < polygon.Vertices.Count; v++)
                {
                    var vert = polygon.Vertices[v];
                    vertices.Add(vert.Position);
                    normals.Add(polygon.Plane.Normal); // Use face normal for flat shading
                    uvs.Add(vert.UV);
                }

                // Fan triangulation (convex polygon)
                for (int v = 1; v < polygon.Vertices.Count - 1; v++)
                {
                    triangles.Add(startIndex);
                    triangles.Add(startIndex + v);
                    triangles.Add(startIndex + v + 1);
                }
            }

            if (vertices.Count == 0)
                return null;

            var mesh = new Mesh();
            mesh.name = "CSG Mesh";

            if (vertices.Count > 65535)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>
        /// Build a mesh with submeshes, one per unique material index.
        /// </summary>
        public static Mesh BuildWithSubmeshes(List<CSGPolygon> polygons, out int[] materialIndices, float uvScale = 1f)
        {
            materialIndices = null;
            if (polygons == null || polygons.Count == 0)
                return null;

            // Group polygons by material index
            var groups = new SortedDictionary<int, List<CSGPolygon>>();
            for (int i = 0; i < polygons.Count; i++)
            {
                int mat = polygons[i].MaterialIndex;
                if (!groups.TryGetValue(mat, out var list))
                {
                    list = new List<CSGPolygon>();
                    groups[mat] = list;
                }
                list.Add(polygons[i]);
            }

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var submeshTriangles = new List<List<int>>();
            var matIndexList = new List<int>();

            foreach (var kvp in groups)
            {
                matIndexList.Add(kvp.Key);
                var tris = new List<int>();

                foreach (var polygon in kvp.Value)
                {
                    if (polygon.Vertices.Count < 3) continue;
                    UVProjector.ProjectPlanar(polygon, uvScale);

                    int startIndex = vertices.Count;
                    for (int v = 0; v < polygon.Vertices.Count; v++)
                    {
                        var vert = polygon.Vertices[v];
                        vertices.Add(vert.Position);
                        normals.Add(polygon.Plane.Normal);
                        uvs.Add(vert.UV);
                    }

                    for (int v = 1; v < polygon.Vertices.Count - 1; v++)
                    {
                        tris.Add(startIndex);
                        tris.Add(startIndex + v);
                        tris.Add(startIndex + v + 1);
                    }
                }

                submeshTriangles.Add(tris);
            }

            if (vertices.Count == 0)
                return null;

            materialIndices = matIndexList.ToArray();

            var mesh = new Mesh();
            mesh.name = "CSG Mesh";
            if (vertices.Count > 65535)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = submeshTriangles.Count;
            for (int i = 0; i < submeshTriangles.Count; i++)
                mesh.SetTriangles(submeshTriangles[i], i);
            mesh.RecalculateBounds();

            return mesh;
        }
    }
}
