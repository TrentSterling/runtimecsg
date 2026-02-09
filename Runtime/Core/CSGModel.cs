using System.Collections.Generic;
using UnityEngine;

namespace RuntimeCSG
{
    /// <summary>
    /// Root container for CSG operations. Owns the chunk system and manages rebuilds.
    /// All CSGBrush children participate in this model's CSG evaluation.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class CSGModel : MonoBehaviour
    {
        [SerializeField, Tooltip("World-space size of each chunk cell.")]
        float _chunkSize = 16f;

        [SerializeField, Tooltip("Milliseconds per frame to spend on chunk rebuilds.")]
        float _frameBudgetMs = 8f;

        [SerializeField, Tooltip("Default material for generated meshes.")]
        Material _defaultMaterial;

        ChunkManager _chunks;
        readonly Queue<Vector3Int> _dirtyQueue = new Queue<Vector3Int>();
        readonly HashSet<Vector3Int> _dirtySet = new HashSet<Vector3Int>();

        public float ChunkSize => _chunkSize;
        public Material DefaultMaterial => _defaultMaterial;

        void OnEnable()
        {
            _chunks = new ChunkManager(_chunkSize);
            RebuildAll();
        }

        void OnDisable()
        {
            _chunks?.Clear(transform);
        }

        void Update()
        {
            ProcessDirtyQueue();
        }

        /// <summary>
        /// Mark chunks overlapping this brush as dirty.
        /// </summary>
        public void SetDirty(CSGBrush brush)
        {
            if (_chunks == null) return;

            var bounds = brush.GetBounds();
            var dirtyCoords = _chunks.GetOverlappingChunks(bounds);

            foreach (var coord in dirtyCoords)
            {
                if (_dirtySet.Add(coord))
                    _dirtyQueue.Enqueue(coord);
            }
        }

        /// <summary>
        /// Force rebuild all chunks.
        /// </summary>
        public void RebuildAll()
        {
            if (_chunks == null)
                _chunks = new ChunkManager(_chunkSize);

            _chunks.Clear(transform);
            _dirtyQueue.Clear();
            _dirtySet.Clear();

            var brushes = GetComponentsInChildren<CSGBrush>(false);
            if (brushes.Length == 0) return;

            // Collect all chunk coords that contain brushes
            var allCoords = new HashSet<Vector3Int>();
            foreach (var brush in brushes)
            {
                var bounds = brush.GetBounds();
                var coords = _chunks.GetOverlappingChunks(bounds);
                foreach (var c in coords) allCoords.Add(c);
            }

            // Rebuild each chunk
            foreach (var coord in allCoords)
            {
                RebuildChunk(coord, brushes);
            }
        }

        void ProcessDirtyQueue()
        {
            if (_dirtyQueue.Count == 0) return;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var brushes = GetComponentsInChildren<CSGBrush>(false);

            while (_dirtyQueue.Count > 0 && sw.Elapsed.TotalMilliseconds < _frameBudgetMs)
            {
                var coord = _dirtyQueue.Dequeue();
                _dirtySet.Remove(coord);
                RebuildChunk(coord, brushes);
            }
        }

        void RebuildChunk(Vector3Int coord, CSGBrush[] allBrushes)
        {
            var chunkBounds = _chunks.GetChunkBounds(coord);

            // Gather brushes overlapping this chunk
            var overlapping = new List<CSGBrush>();
            foreach (var brush in allBrushes)
            {
                if (!brush.isActiveAndEnabled) continue;
                if (brush.GetBounds().Intersects(chunkBounds))
                    overlapping.Add(brush);
            }

            // Sort by order
            overlapping.Sort((a, b) => a.Order.CompareTo(b.Order));

            if (overlapping.Count == 0)
            {
                _chunks.RemoveChunk(coord, transform);
                return;
            }

            // Build CSG result
            BSPTree result = null;

            foreach (var brush in overlapping)
            {
                var polygons = brush.GeneratePolygons();
                if (polygons.Count == 0) continue;

                var brushTree = BSPTree.FromPolygons(polygons);

                if (result == null)
                {
                    if (brush.Operation == CSGOperation.Additive)
                        result = brushTree;
                    continue;
                }

                result = BSPTree.Apply(result, brushTree, brush.Operation);
            }

            if (result == null)
            {
                _chunks.RemoveChunk(coord, transform);
                return;
            }

            var resultPolygons = result.ToPolygons();

            // Clip polygons to chunk bounds (optional - keeps meshes tidy)
            // For now, we include all polygons from brushes overlapping the chunk

            var mesh = CSGMeshBuilder.Build(resultPolygons);
            _chunks.SetChunkMesh(coord, mesh, transform, _defaultMaterial);
        }
    }
}
