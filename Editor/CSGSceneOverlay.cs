using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RuntimeCSG.Editor
{
    /// <summary>
    /// Scene view overlay toolbar for CSG editing.
    /// Shows quick-add buttons and status when a CSGModel or CSGBrush is selected.
    /// </summary>
    [InitializeOnLoad]
    static class CSGSceneOverlay
    {
        static GUIStyle _headerStyle;
        static GUIStyle _buttonStyle;
        static GUIStyle _buttonActiveStyle;
        static GUIStyle _statusStyle;
        static bool _stylesInit;
        static bool _subtractive;

        static CSGSceneOverlay()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        static CSGModel GetActiveModel()
        {
            var go = Selection.activeGameObject;
            if (go == null) return null;
            var model = go.GetComponent<CSGModel>();
            if (model != null) return model;
            return go.GetComponentInParent<CSGModel>();
        }

        static void OnSceneGUI(SceneView sceneView)
        {
            var model = GetActiveModel();
            if (model == null) return;

            HandleKeyboardShortcuts(model);
            DrawAllBrushWireframes(model);
            DrawToolbar(sceneView, model);
            DrawStatusBar(sceneView, model);
        }

        static void DrawAllBrushWireframes(CSGModel model)
        {
            var brushes = model.GetComponentsInChildren<CSGBrush>();
            var selectedGo = Selection.activeGameObject;

            foreach (var brush in brushes)
            {
                if (!brush.isActiveAndEnabled) continue;
                // Skip the selected brush - its own editor draws the wireframe
                if (selectedGo != null && brush.gameObject == selectedGo) continue;

                var polygons = brush.GeneratePolygons();
                if (polygons.Count == 0) continue;

                Color wireColor;
                switch (brush.Operation)
                {
                    case CSGOperation.Subtractive:
                        wireColor = new Color(1f, 0.3f, 0.3f, 0.35f);
                        break;
                    case CSGOperation.Intersect:
                        wireColor = new Color(0.3f, 0.3f, 1f, 0.35f);
                        break;
                    default:
                        wireColor = new Color(0.3f, 1f, 0.3f, 0.35f);
                        break;
                }

                Handles.color = wireColor;
                foreach (var polygon in polygons)
                {
                    if (polygon.Vertices.Count < 2) continue;
                    for (int i = 0; i < polygon.Vertices.Count; i++)
                    {
                        int j = (i + 1) % polygon.Vertices.Count;
                        Handles.DrawLine(
                            polygon.Vertices[i].Position,
                            polygon.Vertices[j].Position
                        );
                    }
                }
            }
        }

        static void HandleKeyboardShortcuts(CSGModel model)
        {
            Event e = Event.current;
            if (e.type != EventType.KeyDown) return;
            if (e.alt || e.control) return;

            // Tab toggles additive/subtractive mode
            if (e.keyCode == KeyCode.Tab && !e.shift)
            {
                _subtractive = !_subtractive;
                e.Use();
                SceneView.RepaintAll();
                return;
            }

            // Shift+B: Add box
            // Shift+W: Add wedge
            // Shift+C: Add cylinder
            // Shift+S: Add sphere
            if (e.shift)
            {
                var op = _subtractive ? CSGOperation.Subtractive : CSGOperation.Additive;
                switch (e.keyCode)
                {
                    case KeyCode.B:
                        AddBrushAtSceneCenter(model, "Box", BrushFactory.Box(), op);
                        e.Use();
                        break;
                    case KeyCode.W:
                        AddBrushAtSceneCenter(model, "Wedge", BrushFactory.Wedge(), op);
                        e.Use();
                        break;
                    case KeyCode.C:
                        AddBrushAtSceneCenter(model, "Cylinder", BrushFactory.Cylinder(), op);
                        e.Use();
                        break;
                    case KeyCode.S:
                        AddBrushAtSceneCenter(model, "Sphere", BrushFactory.Sphere(), op);
                        e.Use();
                        break;
                }
            }
        }

        static void AddBrushAtSceneCenter(CSGModel model, string name, List<CSGPlane> planes, CSGOperation operation)
        {
            // Place at scene view look-at point or near selected brush
            Vector3 pos = Vector3.zero;
            var selectedBrush = Selection.activeGameObject?.GetComponent<CSGBrush>();
            if (selectedBrush != null)
            {
                pos = selectedBrush.transform.position + Vector3.right * 1.5f;
            }
            else
            {
                var sv = SceneView.lastActiveSceneView;
                if (sv != null)
                    pos = sv.pivot;
            }

            var go = new GameObject($"CSGBrush_{name}");
            Undo.RegisterCreatedObjectUndo(go, $"Add {name} Brush");
            go.transform.SetParent(model.transform, false);
            go.transform.position = pos;

            var brush = go.AddComponent<CSGBrush>();
            brush.Descriptor.FromCSGPlanes(planes);
            brush.Descriptor.Operation = operation;

            EditorApplication.delayCall += () =>
            {
                if (model != null)
                    model.RebuildAll();
            };

            Selection.activeGameObject = go;
        }

        static void EnsureStyles()
        {
            if (_stylesInit) return;
            _stylesInit = true;

            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = Color.white },
                fontSize = 11
            };

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 10,
                fixedHeight = 22,
                padding = new RectOffset(6, 6, 2, 2)
            };

            _buttonActiveStyle = new GUIStyle(_buttonStyle);
            _buttonActiveStyle.normal.textColor = new Color(0.2f, 1f, 0.4f);

            _statusStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 10,
                normal = { textColor = new Color(0.4f, 1f, 0.4f) }
            };
        }

        static void DrawToolbar(SceneView sceneView, CSGModel model)
        {
            Handles.BeginGUI();
            EnsureStyles();

            float panelW = 140f;
            float panelH = _subtractive ? 175f : 175f;
            Rect panelRect = new Rect(10, 10, panelW, panelH);

            // Background
            EditorGUI.DrawRect(panelRect, new Color(0.15f, 0.15f, 0.15f, 0.92f));

            GUILayout.BeginArea(panelRect);
            GUILayout.Space(4);

            // Mode toggle
            var op = _subtractive ? CSGOperation.Subtractive : CSGOperation.Additive;
            string modeLabel = _subtractive ? "Mode: SUBTRACT" : "Mode: ADD";
            Color modeColor = _subtractive ? new Color(1f, 0.4f, 0.4f) : new Color(0.4f, 1f, 0.4f);

            var modeStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = modeColor },
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter
            };

            if (GUILayout.Button(modeLabel, modeStyle))
                _subtractive = !_subtractive;

            GUILayout.Space(2);

            // Quick-add buttons
            GUILayout.Label("  Add Brush", _headerStyle);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button((_subtractive ? "- " : "+ ") + "Box", _buttonStyle))
                AddBrushAtSceneCenter(model, "Box", BrushFactory.Box(), op);
            if (GUILayout.Button((_subtractive ? "- " : "+ ") + "Wedge", _buttonStyle))
                AddBrushAtSceneCenter(model, "Wedge", BrushFactory.Wedge(), op);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button((_subtractive ? "- " : "+ ") + "Cyl", _buttonStyle))
                AddBrushAtSceneCenter(model, "Cylinder", BrushFactory.Cylinder(), op);
            if (GUILayout.Button((_subtractive ? "- " : "+ ") + "Sphere", _buttonStyle))
                AddBrushAtSceneCenter(model, "Sphere", BrushFactory.Sphere(), op);
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            // Rebuild button
            if (GUILayout.Button("Rebuild All", _buttonStyle))
                model.RebuildAll();

            GUILayout.Space(2);

            // Brush count
            var brushes = model.GetComponentsInChildren<CSGBrush>();
            var infoStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.6f, 0.6f, 0.6f) },
                alignment = TextAnchor.MiddleCenter
            };
            GUILayout.Label($"{brushes.Length} brushes | Tab=mode", infoStyle);

            GUILayout.EndArea();
            Handles.EndGUI();
        }

        static void DrawStatusBar(SceneView sceneView, CSGModel model)
        {
            // Show shortcuts hint at bottom of scene view
            Handles.BeginGUI();

            float w = 340f;
            float h = 20f;
            float x = (sceneView.position.width - w) * 0.5f;
            float y = sceneView.position.height - 50f;
            Rect r = new Rect(x, y, w, h);

            EditorGUI.DrawRect(r, new Color(0.08f, 0.08f, 0.08f, 0.7f));

            var style = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.5f, 0.5f, 0.5f) }
            };

            string mode = _subtractive ? "SUB" : "ADD";
            GUI.Label(r, $"Shift+B Box | Shift+W Wedge | Shift+C Cyl | Shift+S Sphere | [{mode}]", style);

            Handles.EndGUI();
        }
    }
}
