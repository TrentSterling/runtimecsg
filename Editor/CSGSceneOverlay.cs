using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RuntimeCSG.Editor
{
    /// <summary>
    /// Scene view overlay toolbar for CSG editing.
    /// Shows quick-add buttons, toggle buttons for visuals, and status when a CSGModel or CSGBrush is selected.
    /// </summary>
    [InitializeOnLoad]
    static class CSGSceneOverlay
    {
        static GUIStyle _headerStyle;
        static GUIStyle _buttonStyle;
        static GUIStyle _buttonActiveStyle;
        static GUIStyle _toggleOnStyle;
        static GUIStyle _toggleOffStyle;
        static bool _stylesInit;
        static bool _subtractive;
        static CSGModel _lastKnownModel;

        static CSGSceneOverlay()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        static CSGModel FindActiveModel()
        {
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

            if (_lastKnownModel != null)
                return _lastKnownModel;

            var found = Object.FindObjectOfType<CSGModel>();
            if (found != null)
                _lastKnownModel = found;

            return found;
        }

        static void OnSceneGUI(SceneView sceneView)
        {
            var model = FindActiveModel();
            if (model == null) return;

            HandleKeyboardShortcuts(model);
            DrawToolbar(sceneView, model);
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

            _toggleOnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 9,
                fixedHeight = 20,
                padding = new RectOffset(4, 4, 2, 2)
            };
            _toggleOnStyle.normal.textColor = new Color(0.3f, 1f, 0.5f);

            _toggleOffStyle = new GUIStyle(_toggleOnStyle);
            _toggleOffStyle.normal.textColor = new Color(0.5f, 0.5f, 0.5f);
        }

        static void DrawToolbar(SceneView sceneView, CSGModel model)
        {
            Handles.BeginGUI();
            EnsureStyles();

            float panelW = 160f;
            float panelH = 200f;
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

            // Rebuild + Demo buttons
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Rebuild", _buttonStyle))
                model.RebuildAll();
            if (GUILayout.Button("Demo", _buttonStyle))
                CSGModelEditor.SpawnDemoScene();
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            // --- Visual toggle buttons ---
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Wire", CSGEditorVisuals.ShowWireframes ? _toggleOnStyle : _toggleOffStyle))
                CSGEditorVisuals.ShowWireframes = !CSGEditorVisuals.ShowWireframes;
            if (GUILayout.Button("Chunks", CSGEditorVisuals.ShowChunkGrid ? _toggleOnStyle : _toggleOffStyle))
                CSGEditorVisuals.ShowChunkGrid = !CSGEditorVisuals.ShowChunkGrid;
            if (GUILayout.Button("Normals", CSGEditorVisuals.ShowNormals ? _toggleOnStyle : _toggleOffStyle))
                CSGEditorVisuals.ShowNormals = !CSGEditorVisuals.ShowNormals;
            GUILayout.EndHorizontal();

            GUILayout.Space(2);

            // Brush count + shortcut hint
            var infoStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.6f, 0.6f, 0.6f) },
                alignment = TextAnchor.MiddleCenter
            };
            var brushes = model.GetComponentsInChildren<CSGBrush>();
            GUILayout.Label($"{brushes.Length} brushes | Tab=mode", infoStyle);

            GUILayout.EndArea();
            Handles.EndGUI();
        }
    }
}
