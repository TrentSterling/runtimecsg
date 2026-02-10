using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RuntimeCSG.Editor
{
    /// <summary>
    /// Visualization engine for CSG editing.
    /// Wireframes use real GameObjects (HideFlags.HideAndDontSave) for VR/screenshot visibility.
    /// Face hover highlight uses Handles drawing (no GameObjects = no picking interference).
    /// Polygon data is cached per-brush and only regenerated when the brush version changes.
    /// </summary>
    [InitializeOnLoad]
    static class CSGEditorVisuals
    {
        // --- Toggle state (set by CSGSceneOverlay toolbar) ---
        public static bool ShowWireframes = true;
        public static bool ShowChunkGrid = false;
        public static bool ShowNormals = false;

        // --- Hover state (read by CSGBrushEditor to skip hover on dragged face) ---
        public static CSGBrush HoveredBrush;
        public static int HoveredFaceIndex = -1;

        // --- Overlay material for wireframe GOs ---
        static Material _overlayMat;

        // --- Wireframe GameObjects per brush ---
        static readonly Dictionary<CSGBrush, GameObject> _wireframeGOs = new Dictionary<CSGBrush, GameObject>();

        // --- Polygon cache per brush ---
        static readonly Dictionary<CSGBrush, CachedBrushData> _brushCache = new Dictionary<CSGBrush, CachedBrushData>();
        static int _cachedPolyCount;
        static CSGPolygon _highlightPoly; // polygon to highlight this frame

        // --- Chunk grid GameObjects (pooled) ---
        static readonly List<GameObject> _chunkGridGOs = new List<GameObject>();
        static readonly Dictionary<Vector3Int, float> _chunkFlashTimes = new Dictionary<Vector3Int, float>();

        // --- Persistent model (survives deselect) ---
        static CSGModel _lastKnownModel;

        // --- Hover change detection (for repaint) ---
        static CSGBrush _prevHoveredBrush;
        static int _prevHoveredFace = -1;

        // --- Stats style ---
        static GUIStyle _statsStyle;

        // --- Shader source ---
        const string OverlayShaderSource = @"
Shader ""Hidden/CSGEditorOverlay""
{
    Properties
    {
        _Color (""Color"", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { ""Queue""=""Overlay+100"" ""RenderType""=""Transparent"" }
        ZTest Always
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include ""UnityCG.cginc""

            fixed4 _Color;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return _Color;
            }
            ENDCG
        }
    }
}";

        class CachedBrushData
        {
            public List<CSGPolygon> Polygons;
            public List<CSGPlane> WorldPlanes;
            public int Version;
            public bool MeshDirty;
        }

        static CSGEditorVisuals()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
            AssemblyReloadEvents.beforeAssemblyReload -= CleanupVisuals;
            AssemblyReloadEvents.beforeAssemblyReload += CleanupVisuals;
        }

        // =====================================================================
        // Lifecycle
        // =====================================================================

        static void EnsureVisuals()
        {
            if (_overlayMat == null)
            {
                var shader = ShaderUtil.CreateShaderAsset(OverlayShaderSource);
                _overlayMat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }
        }

        static void CleanupVisuals()
        {
            CleanupWireframes();
            _brushCache.Clear();

            foreach (var go in _chunkGridGOs)
                if (go != null) Object.DestroyImmediate(go);
            _chunkGridGOs.Clear();
            _chunkFlashTimes.Clear();

            if (_overlayMat != null) { Object.DestroyImmediate(_overlayMat); _overlayMat = null; }
        }

        // =====================================================================
        // Main update
        // =====================================================================

        static void OnSceneGUI(SceneView sceneView)
        {
            var model = FindActiveModel();
            if (model == null)
            {
                CleanupWireframes();
                HoveredBrush = null;
                HoveredFaceIndex = -1;
                _highlightPoly = null;
                return;
            }

            EnsureVisuals();

            // Gather brushes once per frame
            var brushes = model.GetComponentsInChildren<CSGBrush>();
            RefreshCache(brushes);

            // Face hover detection FIRST (so HandleBrushPicking reads fresh hover)
            UpdateFaceHover(brushes);

            // Click-to-select: raycast hovered face → select that brush
            HandleBrushPicking();

            // Wireframes (real GameObjects, overlay shader, picking disabled)
            if (ShowWireframes)
                UpdateWireframes(brushes);
            else
                CleanupWireframes();

            // Face highlight (Handles drawing — no GameObjects, no picking issues)
            DrawFaceHighlight();

            // Chunk grid
            if (ShowChunkGrid)
                UpdateChunkGrid(model);
            else
                HideChunkGrid();

            // Stats bar
            DrawStatsBar(sceneView, model, brushes.Length);

            // Force repaint when hover state changes (so highlight follows mouse in real-time)
            if (HoveredBrush != _prevHoveredBrush || HoveredFaceIndex != _prevHoveredFace)
            {
                _prevHoveredBrush = HoveredBrush;
                _prevHoveredFace = HoveredFaceIndex;
                sceneView.Repaint();
            }
        }

        static void OnEditorUpdate()
        {
            if (_chunkFlashTimes.Count > 0)
                SceneView.RepaintAll();
        }

        static CSGModel FindActiveModel()
        {
            // 1. Check current selection (existing behavior)
            var go = Selection.activeGameObject;
            if (go != null)
            {
                var model = go.GetComponent<CSGModel>();
                if (model == null) model = go.GetComponentInParent<CSGModel>();
                if (model != null)
                {
                    _lastKnownModel = model;
                    return model;
                }
            }

            // 2. Fall back to last known model (if it still exists)
            if (_lastKnownModel != null)
                return _lastKnownModel;

            // 3. Find any model in the scene
            var found = Object.FindObjectOfType<CSGModel>();
            if (found != null)
                _lastKnownModel = found;

            return found;
        }

        // =====================================================================
        // Polygon cache — only regenerates when brush.Version changes
        // =====================================================================

        static void RefreshCache(CSGBrush[] brushes)
        {
            var activeBrushes = new HashSet<CSGBrush>(brushes);

            // Remove destroyed/inactive brush entries
            var toRemove = new List<CSGBrush>();
            foreach (var kvp in _brushCache)
                if (kvp.Key == null || !activeBrushes.Contains(kvp.Key))
                    toRemove.Add(kvp.Key);
            foreach (var key in toRemove)
                _brushCache.Remove(key);

            _cachedPolyCount = 0;
            foreach (var brush in brushes)
            {
                if (!brush.isActiveAndEnabled) continue;

                if (!_brushCache.TryGetValue(brush, out var cached))
                {
                    cached = new CachedBrushData();
                    _brushCache[brush] = cached;
                }

                if (cached.Version != brush.Version || cached.Polygons == null)
                {
                    cached.Polygons = brush.GeneratePolygons();
                    cached.WorldPlanes = brush.GetWorldPlanes();
                    cached.Version = brush.Version;
                    cached.MeshDirty = true;
                }
                else
                {
                    cached.MeshDirty = false;
                }

                _cachedPolyCount += cached.Polygons.Count;
            }
        }

        // =====================================================================
        // Click-to-select brush
        // =====================================================================

        static void HandleBrushPicking()
        {
            Event e = Event.current;
            if (e.type != EventType.MouseDown || e.button != 0) return;
            if (GUIUtility.hotControl != 0) return;
            if (e.alt) return; // Alt = orbit camera

            if (HoveredBrush == null) return;

            // Don't steal clicks from face handles on the already-selected brush
            if (Selection.activeGameObject == HoveredBrush.gameObject) return;

            Selection.activeGameObject = HoveredBrush.gameObject;
            e.Use();
        }

        // =====================================================================
        // Wireframe meshes (real GameObjects, overlay shader)
        // =====================================================================

        static void UpdateWireframes(CSGBrush[] brushes)
        {
            var activeBrushSet = new HashSet<CSGBrush>(brushes);
            var selectedGo = Selection.activeGameObject;

            // Remove stale wireframe GOs
            var toRemove = new List<CSGBrush>();
            foreach (var kvp in _wireframeGOs)
                if (kvp.Key == null || !activeBrushSet.Contains(kvp.Key) || kvp.Value == null)
                    toRemove.Add(kvp.Key);
            foreach (var key in toRemove)
            {
                if (_wireframeGOs.TryGetValue(key, out var go) && go != null)
                    Object.DestroyImmediate(go);
                _wireframeGOs.Remove(key);
            }

            foreach (var brush in brushes)
            {
                if (!brush.isActiveAndEnabled) continue;
                if (!_brushCache.TryGetValue(brush, out var cached)) continue;

                bool isSelected = selectedGo != null && brush.gameObject == selectedGo;
                Color wireColor = GetWireColor(brush.Operation, isSelected);

                if (!_wireframeGOs.TryGetValue(brush, out var wireGo) || wireGo == null)
                {
                    wireGo = CreateWireframeGO(cached.Polygons, wireColor);
                    _wireframeGOs[brush] = wireGo;
                }
                else
                {
                    // Always update color (cheap)
                    var mr = wireGo.GetComponent<MeshRenderer>();
                    if (mr != null && mr.sharedMaterial != null)
                        mr.sharedMaterial.SetColor("_Color", wireColor);

                    // Only rebuild mesh when brush data actually changed
                    if (cached.MeshDirty)
                        BuildWireLineMesh(wireGo.GetComponent<MeshFilter>(), cached.Polygons);
                }
            }
        }

        static void CleanupWireframes()
        {
            foreach (var kvp in _wireframeGOs)
                if (kvp.Value != null) Object.DestroyImmediate(kvp.Value);
            _wireframeGOs.Clear();
        }

        static Color GetWireColor(CSGOperation op, bool selected)
        {
            float alpha = selected ? 0.9f : 0.35f;
            switch (op)
            {
                case CSGOperation.Subtractive: return new Color(1f, 0.3f, 0.3f, alpha);
                case CSGOperation.Intersect:   return new Color(0.3f, 0.3f, 1f, alpha);
                default:                       return new Color(0.3f, 1f, 0.3f, alpha);
            }
        }

        static GameObject CreateWireframeGO(List<CSGPolygon> polygons, Color color)
        {
            var go = new GameObject("__CSGWire")
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();

            var mat = new Material(_overlayMat) { hideFlags = HideFlags.HideAndDontSave };
            mat.SetColor("_Color", color);
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            BuildWireLineMesh(mf, polygons);

            // Disable scene picking so wireframe GOs can't intercept clicks
            SceneVisibilityManager.instance.DisablePicking(go, true);

            return go;
        }

        static void BuildWireLineMesh(MeshFilter mf, List<CSGPolygon> polygons)
        {
            if (polygons == null || polygons.Count == 0)
            {
                if (mf.sharedMesh != null) mf.sharedMesh.Clear();
                return;
            }

            var edgeSet = new HashSet<long>();
            var verts = new List<Vector3>();
            var indices = new List<int>();

            foreach (var polygon in polygons)
            {
                if (polygon.Vertices.Count < 2) continue;
                for (int i = 0; i < polygon.Vertices.Count; i++)
                {
                    int j = (i + 1) % polygon.Vertices.Count;
                    var a = polygon.Vertices[i].Position;
                    var b = polygon.Vertices[j].Position;

                    long ha = HashVec(a);
                    long hb = HashVec(b);
                    long edgeKey = ha < hb ? ha * 1000003L + hb : hb * 1000003L + ha;

                    if (edgeSet.Add(edgeKey))
                    {
                        int idx = verts.Count;
                        verts.Add(a);
                        verts.Add(b);
                        indices.Add(idx);
                        indices.Add(idx + 1);
                    }
                }
            }

            var mesh = mf.sharedMesh;
            if (mesh == null)
            {
                mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
                mf.sharedMesh = mesh;
            }

            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetIndices(indices.ToArray(), MeshTopology.Lines, 0);
            mesh.RecalculateBounds();
        }

        static long HashVec(Vector3 v)
        {
            int x = Mathf.RoundToInt(v.x * 1000f);
            int y = Mathf.RoundToInt(v.y * 1000f);
            int z = Mathf.RoundToInt(v.z * 1000f);
            return ((long)x * 73856093L) ^ ((long)y * 19349663L) ^ ((long)z * 83492791L);
        }

        // =====================================================================
        // Face hover detection (uses cached polygons)
        // =====================================================================

        static void UpdateFaceHover(CSGBrush[] brushes)
        {
            // Don't hover when dragging a handle
            if (GUIUtility.hotControl != 0)
            {
                HoveredBrush = null;
                HoveredFaceIndex = -1;
                _highlightPoly = null;
                return;
            }

            Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);

            float closestDist = float.MaxValue;
            CSGBrush closestBrush = null;
            int closestFace = -1;
            CSGPolygon closestPoly = null;

            foreach (var brush in brushes)
            {
                if (!brush.isActiveAndEnabled) continue;
                if (!_brushCache.TryGetValue(brush, out var cached)) continue;

                for (int f = 0; f < cached.Polygons.Count; f++)
                {
                    if (RaycastPolygon(ray, cached.Polygons[f], out float dist))
                    {
                        if (dist > 0f && dist < closestDist)
                        {
                            closestDist = dist;
                            closestBrush = brush;
                            closestFace = f;
                            closestPoly = cached.Polygons[f];
                        }
                    }
                }
            }

            HoveredBrush = closestBrush;
            HoveredFaceIndex = closestFace;
            _highlightPoly = closestPoly;
        }

        static bool RaycastPolygon(Ray ray, CSGPolygon polygon, out float distance)
        {
            distance = 0f;
            if (polygon.Vertices.Count < 3) return false;

            Vector3 normal = polygon.Plane.Normal;
            float denom = Vector3.Dot(ray.direction, normal);
            if (Mathf.Abs(denom) < 1e-6f) return false;

            float d = (float)polygon.Plane.D;
            float t = -(Vector3.Dot(ray.origin, normal) + d) / denom;
            if (t < 0f) return false;

            Vector3 hitPoint = ray.origin + ray.direction * t;

            // Point-in-polygon via cross product sign consistency
            int count = polygon.Vertices.Count;
            bool? positive = null;
            for (int i = 0; i < count; i++)
            {
                int j = (i + 1) % count;
                Vector3 edge = polygon.Vertices[j].Position - polygon.Vertices[i].Position;
                Vector3 toPoint = hitPoint - polygon.Vertices[i].Position;
                float sign = Vector3.Dot(Vector3.Cross(edge, toPoint), normal);

                if (positive == null)
                    positive = sign >= 0f;
                else if ((sign >= 0f) != positive.Value)
                    return false;
            }

            distance = t;
            return true;
        }

        // =====================================================================
        // Face highlight (pure Handles drawing — no GameObjects, no picking)
        // =====================================================================

        static void DrawFaceHighlight()
        {
            if (_highlightPoly == null) return;

            var poly = _highlightPoly;
            int count = poly.Vertices.Count;
            if (count < 3) return;

            Vector3 normal = poly.Plane.Normal;
            float offset = 0.002f;

            // Build vertex array with slight normal offset to avoid z-fighting
            var verts = new Vector3[count];
            for (int i = 0; i < count; i++)
                verts[i] = poly.Vertices[i].Position + normal * offset;

            // Filled polygon (semi-transparent cyan)
            Handles.color = new Color(0.2f, 0.8f, 1f, 0.18f);
            Handles.DrawAAConvexPolygon(verts);

            // Outline (bright cyan)
            Handles.color = new Color(0.3f, 0.9f, 1f, 0.9f);
            for (int i = 0; i < count; i++)
            {
                int j = (i + 1) % count;
                Handles.DrawLine(verts[i], verts[j]);
            }

            // Normal arrow (amber, only when toggle is on)
            if (ShowNormals)
            {
                Vector3 center = Vector3.zero;
                for (int i = 0; i < count; i++)
                    center += verts[i];
                center /= count;

                Handles.color = new Color(1f, 0.9f, 0.3f, 0.9f);
                Handles.DrawLine(center, center + normal * 0.25f);
                Handles.ConeHandleCap(0, center + normal * 0.25f,
                    Quaternion.LookRotation(normal), 0.04f, EventType.Repaint);
            }
        }

        // =====================================================================
        // Chunk grid overlay
        // =====================================================================

        static void UpdateChunkGrid(CSGModel model)
        {
            EnsureVisuals();

            var coords = new List<Vector3Int>();
            foreach (var coord in model.GetActiveChunkCoords())
                coords.Add(coord);

            while (_chunkGridGOs.Count < coords.Count)
            {
                var go = CreateWireCube();
                go.SetActive(false);
                _chunkGridGOs.Add(go);
            }

            double time = EditorApplication.timeSinceStartup;

            for (int i = 0; i < _chunkGridGOs.Count; i++)
            {
                if (i < coords.Count)
                {
                    var coord = coords[i];
                    var bounds = model.GetChunkBounds(coord);

                    _chunkGridGOs[i].SetActive(true);
                    _chunkGridGOs[i].transform.position = bounds.center;
                    _chunkGridGOs[i].transform.localScale = bounds.size;

                    var mr = _chunkGridGOs[i].GetComponent<MeshRenderer>();
                    if (mr != null && mr.sharedMaterial != null)
                    {
                        float alpha = 0.1f;
                        if (_chunkFlashTimes.TryGetValue(coord, out float flashStart))
                        {
                            float elapsed = (float)(time - flashStart);
                            if (elapsed < 0.4f)
                                alpha = Mathf.Lerp(0.25f, 0.0f, elapsed / 0.4f);
                            else
                                _chunkFlashTimes.Remove(coord);
                        }
                        mr.sharedMaterial.SetColor("_Color", new Color(0.2f, 1f, 0.5f, alpha));
                    }
                }
                else
                {
                    _chunkGridGOs[i].SetActive(false);
                }
            }
        }

        static void HideChunkGrid()
        {
            foreach (var go in _chunkGridGOs)
                if (go != null) go.SetActive(false);
        }

        static GameObject CreateWireCube()
        {
            var go = new GameObject("__CSGChunkGrid") { hideFlags = HideFlags.HideAndDontSave };

            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();

            var mat = new Material(_overlayMat) { hideFlags = HideFlags.HideAndDontSave };
            mat.SetColor("_Color", new Color(0.2f, 1f, 0.5f, 0.1f));
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            var verts = new Vector3[]
            {
                new Vector3(-0.5f, -0.5f, -0.5f), new Vector3( 0.5f, -0.5f, -0.5f),
                new Vector3( 0.5f, -0.5f,  0.5f), new Vector3(-0.5f, -0.5f,  0.5f),
                new Vector3(-0.5f,  0.5f, -0.5f), new Vector3( 0.5f,  0.5f, -0.5f),
                new Vector3( 0.5f,  0.5f,  0.5f), new Vector3(-0.5f,  0.5f,  0.5f),
            };
            var indices = new int[]
            {
                0,1, 1,2, 2,3, 3,0,
                4,5, 5,6, 6,7, 7,4,
                0,4, 1,5, 2,6, 3,7,
            };

            var mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
            mesh.vertices = verts;
            mesh.SetIndices(indices, MeshTopology.Lines, 0);
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;

            SceneVisibilityManager.instance.DisablePicking(go, true);

            return go;
        }

        public static void NotifyChunkRebuilt(Vector3Int coord)
        {
            _chunkFlashTimes[coord] = (float)EditorApplication.timeSinceStartup;
        }

        // =====================================================================
        // Stats bar
        // =====================================================================

        static void DrawStatsBar(SceneView sceneView, CSGModel model, int brushCount)
        {
            Handles.BeginGUI();

            if (_statsStyle == null)
            {
                _statsStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 10,
                    normal = { textColor = new Color(0.4f, 1f, 0.4f) }
                };
            }

            string stats = $"Brushes: {brushCount} | Polys: {_cachedPolyCount} | Chunks: {model.ChunkCount} | Rebuild: {model.LastRebuildMs:F1}ms";

            float w = 380f;
            float h = 22f;
            float x = (sceneView.position.width - w) * 0.5f;
            float y = sceneView.position.height - 50f;
            Rect r = new Rect(x, y, w, h);

            EditorGUI.DrawRect(r, new Color(0.08f, 0.08f, 0.08f, 0.8f));
            GUI.Label(r, stats, _statsStyle);

            Handles.EndGUI();
        }
    }
}
