using UnityEngine;

namespace RuntimeCSG
{
    /// <summary>
    /// Per-chunk mesh container. Manages a child GameObject with MeshFilter, MeshRenderer, and MeshCollider.
    /// </summary>
    public class CSGMeshChunk
    {
        public Vector3Int Coord { get; private set; }
        public GameObject GameObject { get; private set; }
        public MeshFilter MeshFilter { get; private set; }
        public MeshRenderer MeshRenderer { get; private set; }
        public MeshCollider MeshCollider { get; private set; }

        public CSGMeshChunk(Vector3Int coord, Transform parent, Material material)
        {
            Coord = coord;

            GameObject = new GameObject($"CSGChunk_{coord.x}_{coord.y}_{coord.z}");
            GameObject.transform.SetParent(parent, false);
            GameObject.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;

            MeshFilter = GameObject.AddComponent<MeshFilter>();
            MeshRenderer = GameObject.AddComponent<MeshRenderer>();
            MeshCollider = GameObject.AddComponent<MeshCollider>();

            if (material != null)
                MeshRenderer.sharedMaterial = material;
        }

        public void SetMesh(Mesh mesh)
        {
            if (MeshFilter != null)
                MeshFilter.sharedMesh = mesh;
            if (MeshCollider != null)
                MeshCollider.sharedMesh = mesh;
        }

        public void Destroy()
        {
            if (GameObject != null)
            {
                if (MeshFilter != null && MeshFilter.sharedMesh != null)
                    Object.DestroyImmediate(MeshFilter.sharedMesh);

                #if UNITY_EDITOR
                if (!Application.isPlaying)
                    Object.DestroyImmediate(GameObject);
                else
                #endif
                    Object.Destroy(GameObject);
            }
        }
    }
}
