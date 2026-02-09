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

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Quick Setup", EditorStyles.boldLabel);
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
            if (GUILayout.Button("Force Rebuild Parent"))
            {
                var model = brush.GetComponentInParent<CSGModel>();
                if (model != null)
                    model.RebuildAll();
            }

            serializedObject.ApplyModifiedProperties();
        }

        void SetBrushShape(CSGBrush brush, System.Collections.Generic.List<CSGPlane> planes)
        {
            Undo.RecordObject(brush, "Set Brush Shape");
            brush.Descriptor.FromCSGPlanes(planes);
            EditorUtility.SetDirty(brush);
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

                    float delta = Vector3.Dot(newPos - faceCenter, normal);
                    var localPlanes = brush.Descriptor.Planes;
                    if (i < localPlanes.Count)
                    {
                        var lp = localPlanes[i];
                        localPlanes[i] = new SerializablePlane(lp.A, lp.B, lp.C, lp.D - delta);
                    }

                    EditorUtility.SetDirty(brush);
                }
            }
        }
    }
}
