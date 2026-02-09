using System.Collections.Generic;
using UnityEngine;

namespace RuntimeCSG
{
    /// <summary>
    /// Grid-based spatial partitioning for CSG chunks.
    /// Maps world positions to chunk coordinates and manages per-chunk mesh containers.
    /// </summary>
    public class ChunkManager
    {
        readonly float _chunkSize;
        readonly Dictionary<Vector3Int, CSGMeshChunk> _chunks = new Dictionary<Vector3Int, CSGMeshChunk>();

        public float ChunkSize => _chunkSize;
        public int ChunkCount => _chunks.Count;

        public ChunkManager(float chunkSize)
        {
            _chunkSize = Mathf.Max(chunkSize, 1f);
        }

        /// <summary>
        /// Convert a world position to chunk coordinates.
        /// </summary>
        public Vector3Int WorldToChunk(Vector3 worldPos)
        {
            return new Vector3Int(
                Mathf.FloorToInt(worldPos.x / _chunkSize),
                Mathf.FloorToInt(worldPos.y / _chunkSize),
                Mathf.FloorToInt(worldPos.z / _chunkSize)
            );
        }

        /// <summary>
        /// Get the world-space AABB for a chunk coordinate.
        /// </summary>
        public Bounds GetChunkBounds(Vector3Int coord)
        {
            Vector3 min = new Vector3(
                coord.x * _chunkSize,
                coord.y * _chunkSize,
                coord.z * _chunkSize
            );
            Vector3 size = Vector3.one * _chunkSize;
            return new Bounds(min + size * 0.5f, size);
        }

        /// <summary>
        /// Get all chunk coordinates that overlap a given bounds.
        /// </summary>
        public List<Vector3Int> GetOverlappingChunks(Bounds bounds)
        {
            var min = WorldToChunk(bounds.min);
            var max = WorldToChunk(bounds.max);

            var result = new List<Vector3Int>();
            for (int x = min.x; x <= max.x; x++)
            for (int y = min.y; y <= max.y; y++)
            for (int z = min.z; z <= max.z; z++)
                result.Add(new Vector3Int(x, y, z));

            return result;
        }

        /// <summary>
        /// Set the mesh for a chunk, creating the chunk container if needed.
        /// </summary>
        public void SetChunkMesh(Vector3Int coord, Mesh mesh, Transform parent, Material material)
        {
            if (mesh == null)
            {
                RemoveChunk(coord, parent);
                return;
            }

            if (!_chunks.TryGetValue(coord, out var chunk))
            {
                chunk = new CSGMeshChunk(coord, parent, material);
                _chunks[coord] = chunk;
            }

            chunk.SetMesh(mesh);
        }

        /// <summary>
        /// Remove a chunk and destroy its GameObject.
        /// </summary>
        public void RemoveChunk(Vector3Int coord, Transform parent)
        {
            if (_chunks.TryGetValue(coord, out var chunk))
            {
                chunk.Destroy();
                _chunks.Remove(coord);
            }
        }

        /// <summary>
        /// Remove all chunks and destroy their GameObjects.
        /// </summary>
        public void Clear(Transform parent)
        {
            foreach (var kvp in _chunks)
                kvp.Value.Destroy();
            _chunks.Clear();
        }

        /// <summary>
        /// Check if a chunk exists at the given coordinate.
        /// </summary>
        public bool HasChunk(Vector3Int coord)
        {
            return _chunks.ContainsKey(coord);
        }
    }
}
