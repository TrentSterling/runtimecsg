using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RuntimeCSG.Editor
{
    [CustomEditor(typeof(CSGBrush))]
    [CanEditMultipleObjects]
    public class CSGBrushEditor : UnityEditor.Editor
    {
        SerializedProperty _descriptor;

        void OnEnable()
        {
            _descriptor = serializedObject.FindProperty("_descriptor");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var brush = (CSGBrush)target;

            EditorGUILayout.LabelField("CSG Brush", EditorStyles.boldLabel);

            var opProp = _descriptor.FindPropertyRelative("Operation");
            EditorGUILayout.PropertyField(opProp);

            var orderProp = _descriptor.FindPropertyRelative("Order");
            EditorGUILayout.PropertyField(orderProp);

            var matProp = _descriptor.FindPropertyRelative("MaterialIndex");
            EditorGUILayout.PropertyField(matProp);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Planes: {_descriptor.FindPropertyRelative("Planes").arraySize}");

            // Apply property changes and trigger rebuild if anything changed
            if (serializedObject.ApplyModifiedProperties())
            {
                foreach (var t in targets)
                {
                    var b = (CSGBrush)t;
                    var m = b.GetComponentInParent<CSGModel>();
                    if (m != null) m.RebuildAll();
                }
            }

            EditorGUILayout.Space();

            // --- Change this brush's shape ---
            EditorGUILayout.LabelField("Change Shape", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Box"))
                SetBrushShape(brush, BrushFactory.Box());
            if (GUILayout.Button("Wedge"))
                SetBrushShape(brush, BrushFactory.Wedge());
            if (GUILayout.Button("Cylinder"))
                SetBrushShape(brush, BrushFactory.Cylinder());
            if (GUILayout.Button("Sphere"))
                SetBrushShape(brush, BrushFactory.Sphere());
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // --- Add new sibling brushes (additive) ---
            EditorGUILayout.LabelField("Add New Brush", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ Box"))
                AddSiblingBrush(brush, "Box", BrushFactory.Box(), CSGOperation.Additive);
            if (GUILayout.Button("+ Wedge"))
                AddSiblingBrush(brush, "Wedge", BrushFactory.Wedge(), CSGOperation.Additive);
            if (GUILayout.Button("+ Cylinder"))
                AddSiblingBrush(brush, "Cylinder", BrushFactory.Cylinder(), CSGOperation.Additive);
            if (GUILayout.Button("+ Sphere"))
                AddSiblingBrush(brush, "Sphere", BrushFactory.Sphere(), CSGOperation.Additive);
            EditorGUILayout.EndHorizontal();

            // --- Add new sibling brushes (subtractive) ---
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("- Box"))
                AddSiblingBrush(brush, "Box", BrushFactory.Box(), CSGOperation.Subtractive);
            if (GUILayout.Button("- Wedge"))
                AddSiblingBrush(brush, "Wedge", BrushFactory.Wedge(), CSGOperation.Subtractive);
            if (GUILayout.Button("- Cylinder"))
                AddSiblingBrush(brush, "Cylinder", BrushFactory.Cylinder(), CSGOperation.Subtractive);
            if (GUILayout.Button("- Sphere"))
                AddSiblingBrush(brush, "Sphere", BrushFactory.Sphere(), CSGOperation.Subtractive);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            if (GUILayout.Button("Force Rebuild Parent"))
            {
                var model = brush.GetComponentInParent<CSGModel>();
                if (model != null)
                    model.RebuildAll();
            }
        }

        void SetBrushShape(CSGBrush brush, List<CSGPlane> planes)
        {
            Undo.RecordObject(brush, "Set Brush Shape");
            brush.Descriptor.FromCSGPlanes(planes);
            EditorUtility.SetDirty(brush);

            var model = brush.GetComponentInParent<CSGModel>();
            if (model != null)
                model.RebuildAll();
        }

        void AddSiblingBrush(CSGBrush currentBrush, string name, List<CSGPlane> planes, CSGOperation operation)
        {
            var model = currentBrush.GetComponentInParent<CSGModel>();
            if (model == null) return;

            var go = new GameObject($"CSGBrush_{name}");
            Undo.RegisterCreatedObjectUndo(go, $"Add {name} Brush");
            go.transform.SetParent(model.transform, false);

            // Offset from current brush so they don't stack
            go.transform.position = currentBrush.transform.position + Vector3.right * 1.5f;

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

        void OnSceneGUI()
        {
            var brush = (CSGBrush)target;
            if (!brush.isActiveAndEnabled) return;

            DrawBrushWireframe(brush);
            DrawFaceHandles(brush);
        }

        void DrawBrushWireframe(CSGBrush brush)
        {
            var polygons = brush.GeneratePolygons();
            if (polygons.Count == 0) return;

            Color wireColor;
            switch (brush.Operation)
            {
                case CSGOperation.Subtractive:
                    wireColor = new Color(1f, 0.3f, 0.3f, 0.8f);
                    break;
                case CSGOperation.Intersect:
                    wireColor = new Color(0.3f, 0.3f, 1f, 0.8f);
                    break;
                default:
                    wireColor = new Color(0.3f, 1f, 0.3f, 0.8f);
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

        void DrawFaceHandles(CSGBrush brush)
        {
            var planes = brush.GetWorldPlanes();
            if (planes.Count == 0) return;

            var bounds = brush.GetBounds();
            var center = bounds.center;
            var invMatrix = brush.transform.worldToLocalMatrix;

            Handles.color = new Color(1f, 1f, 0f, 0.5f);

            for (int i = 0; i < planes.Count; i++)
            {
                var plane = planes[i];
                Vector3 normal = plane.Normal;
                Vector3 faceCenter = center + normal * (float)(-plane.DistanceTo(center));

                float handleSize = HandleUtility.GetHandleSize(faceCenter) * 0.08f;

                EditorGUI.BeginChangeCheck();
                Vector3 newPos = Handles.Slider(faceCenter, normal, handleSize, Handles.DotHandleCap, 0.01f);

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(brush, "Move Brush Face");

                    Vector3 newLocalPoint = invMatrix.MultiplyPoint3x4(newPos);
                    var localPlanes = brush.Descriptor.Planes;
                    if (i < localPlanes.Count)
                    {
                        var lp = localPlanes[i];
                        double newD = -(lp.A * newLocalPoint.x + lp.B * newLocalPoint.y + lp.C * newLocalPoint.z);
                        localPlanes[i] = new SerializablePlane(lp.A, lp.B, lp.C, newD);
                    }

                    EditorUtility.SetDirty(brush);

                    // Trigger rebuild immediately so the mesh updates while dragging
                    var model = brush.GetComponentInParent<CSGModel>();
                    if (model != null)
                        model.RebuildAll();
                }
            }
        }
    }
}
