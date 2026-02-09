using UnityEditor;
using UnityEngine;

namespace RuntimeCSG.Editor
{
    [CustomEditor(typeof(CSGModel))]
    public class CSGModelEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var model = (CSGModel)target;

            EditorGUILayout.LabelField("CSG Model", EditorStyles.boldLabel);

            DrawDefaultInspector();

            EditorGUILayout.Space();

            var brushes = model.GetComponentsInChildren<CSGBrush>();
            EditorGUILayout.LabelField($"Brushes: {brushes.Length}");

            EditorGUILayout.Space();

            if (GUILayout.Button("Rebuild All"))
            {
                model.RebuildAll();
                EditorUtility.SetDirty(model);
            }

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Add Brush", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ Box"))
                AddBrush(model, "Box", BrushFactory.Box());
            if (GUILayout.Button("+ Wedge"))
                AddBrush(model, "Wedge", BrushFactory.Wedge());
            if (GUILayout.Button("+ Cylinder"))
                AddBrush(model, "Cylinder", BrushFactory.Cylinder());
            if (GUILayout.Button("+ Sphere"))
                AddBrush(model, "Sphere", BrushFactory.Sphere());
            EditorGUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();
        }

        void AddBrush(CSGModel model, string name, System.Collections.Generic.List<CSGPlane> planes)
        {
            var go = new GameObject($"CSGBrush_{name}");
            Undo.RegisterCreatedObjectUndo(go, $"Add {name} Brush");
            go.transform.SetParent(model.transform, false);

            var brush = go.AddComponent<CSGBrush>();
            brush.Descriptor.FromCSGPlanes(planes);

            Selection.activeGameObject = go;
        }
    }
}
