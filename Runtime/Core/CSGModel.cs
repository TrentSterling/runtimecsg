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
            EnsureDefaultMaterial();
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
            SetDirtyBounds(bounds);
        }

        /// <summary>
        /// Mark chunks overlapping the given bounds as dirty.
        /// Used to clean up stale chunks when a brush moves or is removed.
        /// </summary>
        public void SetDirtyBounds(Bounds bounds)
        {
            if (_chunks == null) return;

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

        void EnsureDefaultMaterial()
        {
            if (_defaultMaterial != null) return;

            // Try to find the built-in default material
            var defaultShader = Shader.Find("Standard");
            if (defaultShader == null)
                defaultShader = Shader.Find("Universal Render Pipeline/Lit");
            if (defaultShader == null)
                defaultShader = Shader.Find("HDRP/Lit");

            if (defaultShader != null)
            {
                _defaultMaterial = new Material(defaultShader);
                _defaultMaterial.name = "CSG Default";
                _defaultMaterial.color = new Color(0.7f, 0.7f, 0.7f);
                _defaultMaterial.hideFlags = HideFlags.DontSave;
            }
        }

        static List<CSGPolygon> ClipPolygonsToAABB(List<CSGPolygon> polygons, Bounds bounds)
        {
            var min = bounds.min;
            var max = bounds.max;

            // 6 inward-facing clip planes (front = inside the AABB)
            var clipPlanes = new CSGPlane[]
            {
                new CSGPlane( 1,  0,  0, -min.x), // x >= min.x
                new CSGPlane(-1,  0,  0,  max.x), // x <= max.x
                new CSGPlane( 0,  1,  0, -min.y), // y >= min.y
                new CSGPlane( 0, -1,  0,  max.y), // y <= max.y
                new CSGPlane( 0,  0,  1, -min.z), // z >= min.z
                new CSGPlane( 0,  0, -1,  max.z), // z <= max.z
            };

            var current = polygons;
            foreach (var clipPlane in clipPlanes)
            {
                var clipped = new List<CSGPolygon>(current.Count);
                foreach (var polygon in current)
                {
                    PolygonClipper.Split(polygon, clipPlane,
                        out var front, out _, out var cf, out _);
                    if (front != null) clipped.Add(front);
                    if (cf != null) clipped.Add(cf);
                }
                current = clipped;
                if (current.Count == 0) break;
            }

            return current;
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

            // Build brush data for CSG engine
            var brushDataList = new List<ChiselCSGEngine.BrushData>();
            foreach (var brush in overlapping)
            {
                var polygons = brush.GeneratePolygons();
                if (polygons.Count == 0) continue;

                brushDataList.Add(new ChiselCSGEngine.BrushData
                {
                    Polygons = polygons,
                    WorldPlanes = brush.GetWorldPlanes(),
                    Operation = brush.Operation,
                    Order = brush.Order
                });
            }

            var resultPolygons = ChiselCSGEngine.Process(brushDataList);

            if (resultPolygons.Count == 0)
            {
                _chunks.RemoveChunk(coord, transform);
                return;
            }

            // Clip polygons to chunk bounds so each chunk only contains its own geometry
            resultPolygons = ClipPolygonsToAABB(resultPolygons, chunkBounds);

            if (resultPolygons.Count == 0)
            {
                _chunks.RemoveChunk(coord, transform);
                return;
            }

            // Transform vertices from world space to model local space
            // (chunks are children of this transform, so mesh must be in local space)
            var worldToLocal = transform.worldToLocalMatrix;
            for (int p = 0; p < resultPolygons.Count; p++)
            {
                var poly = resultPolygons[p];
                for (int v = 0; v < poly.Vertices.Count; v++)
                {
                    var vert = poly.Vertices[v];
                    vert.Position = worldToLocal.MultiplyPoint3x4(vert.Position);
                    vert.Normal = worldToLocal.MultiplyVector(vert.Normal).normalized;
                    poly.Vertices[v] = vert;
                }
            }

            var mesh = CSGMeshBuilder.Build(resultPolygons);
            _chunks.SetChunkMesh(coord, mesh, transform, _defaultMaterial);
        }
    }
}
