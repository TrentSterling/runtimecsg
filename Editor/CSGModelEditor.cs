using System.Collections.Generic;
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

            // --- Additive brushes ---
            EditorGUILayout.LabelField("Add Brush (Additive)", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ Box"))
                AddBrush(model, "Box", BrushFactory.Box(), CSGOperation.Additive);
            if (GUILayout.Button("+ Wedge"))
                AddBrush(model, "Wedge", BrushFactory.Wedge(), CSGOperation.Additive);
            if (GUILayout.Button("+ Cylinder"))
                AddBrush(model, "Cylinder", BrushFactory.Cylinder(), CSGOperation.Additive);
            if (GUILayout.Button("+ Sphere"))
                AddBrush(model, "Sphere", BrushFactory.Sphere(), CSGOperation.Additive);
            EditorGUILayout.EndHorizontal();

            // --- Subtractive brushes ---
            EditorGUILayout.LabelField("Add Brush (Subtractive)", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("- Box"))
                AddBrush(model, "Box", BrushFactory.Box(), CSGOperation.Subtractive);
            if (GUILayout.Button("- Wedge"))
                AddBrush(model, "Wedge", BrushFactory.Wedge(), CSGOperation.Subtractive);
            if (GUILayout.Button("- Cylinder"))
                AddBrush(model, "Cylinder", BrushFactory.Cylinder(), CSGOperation.Subtractive);
            if (GUILayout.Button("- Sphere"))
                AddBrush(model, "Sphere", BrushFactory.Sphere(), CSGOperation.Subtractive);
            EditorGUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();
        }

        void AddBrush(CSGModel model, string name, List<CSGPlane> planes, CSGOperation operation)
        {
            var go = new GameObject($"CSGBrush_{name}");
            Undo.RegisterCreatedObjectUndo(go, $"Add {name} Brush");
            go.transform.SetParent(model.transform, false);

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

        // --- Menu items for quick creation ---

        [MenuItem("GameObject/CSG/CSG Model (Empty)", false, 10)]
        static void CreateEmptyModel()
        {
            var go = new GameObject("CSGModel");
            go.AddComponent<CSGModel>();
            Undo.RegisterCreatedObjectUndo(go, "Create CSG Model");
            Selection.activeGameObject = go;
        }

        [MenuItem("GameObject/CSG/CSG Model + Box", false, 11)]
        static void CreateModelWithBox()
        {
            var go = new GameObject("CSGModel");
            var model = go.AddComponent<CSGModel>();
            Undo.RegisterCreatedObjectUndo(go, "Create CSG Model + Box");

            var brushGo = new GameObject("CSGBrush_Box");
            brushGo.transform.SetParent(go.transform, false);
            var brush = brushGo.AddComponent<CSGBrush>();
            brush.Descriptor.FromCSGPlanes(BrushFactory.Box());

            EditorApplication.delayCall += () =>
            {
                if (model != null)
                    model.RebuildAll();
            };

            Selection.activeGameObject = go;
        }

        [MenuItem("GameObject/CSG/Add Box Brush", true)]
        static bool ValidateAddBrush()
        {
            return Selection.activeGameObject != null &&
                   Selection.activeGameObject.GetComponentInParent<CSGModel>() != null;
        }

        [MenuItem("GameObject/CSG/Add Box Brush", false, 20)]
        static void MenuAddBox()
        {
            AddBrushFromMenu("Box", BrushFactory.Box(), CSGOperation.Additive);
        }

        [MenuItem("GameObject/CSG/Add Wedge Brush", true)]
        static bool ValidateAddWedge() => ValidateAddBrush();

        [MenuItem("GameObject/CSG/Add Wedge Brush", false, 21)]
        static void MenuAddWedge()
        {
            AddBrushFromMenu("Wedge", BrushFactory.Wedge(), CSGOperation.Additive);
        }

        [MenuItem("GameObject/CSG/Add Cylinder Brush", true)]
        static bool ValidateAddCylinder() => ValidateAddBrush();

        [MenuItem("GameObject/CSG/Add Cylinder Brush", false, 22)]
        static void MenuAddCylinder()
        {
            AddBrushFromMenu("Cylinder", BrushFactory.Cylinder(), CSGOperation.Additive);
        }

        [MenuItem("GameObject/CSG/Add Sphere Brush", true)]
        static bool ValidateAddSphere() => ValidateAddBrush();

        [MenuItem("GameObject/CSG/Add Sphere Brush", false, 23)]
        static void MenuAddSphere()
        {
            AddBrushFromMenu("Sphere", BrushFactory.Sphere(), CSGOperation.Additive);
        }

        [MenuItem("GameObject/CSG/Add Subtractive Box", true)]
        static bool ValidateAddSubBox() => ValidateAddBrush();

        [MenuItem("GameObject/CSG/Add Subtractive Box", false, 30)]
        static void MenuAddSubBox()
        {
            AddBrushFromMenu("Box", BrushFactory.Box(), CSGOperation.Subtractive);
        }

        [MenuItem("GameObject/CSG/Add Subtractive Cylinder", true)]
        static bool ValidateAddSubCylinder() => ValidateAddBrush();

        [MenuItem("GameObject/CSG/Add Subtractive Cylinder", false, 31)]
        static void MenuAddSubCylinder()
        {
            AddBrushFromMenu("Cylinder", BrushFactory.Cylinder(), CSGOperation.Subtractive);
        }

        static void AddBrushFromMenu(string name, List<CSGPlane> planes, CSGOperation operation)
        {
            var model = Selection.activeGameObject.GetComponentInParent<CSGModel>();
            if (model == null) return;

            var go = new GameObject($"CSGBrush_{name}");
            Undo.RegisterCreatedObjectUndo(go, $"Add {name} Brush");
            go.transform.SetParent(model.transform, false);

            // Offset from current selection so brushes don't stack
            if (Selection.activeGameObject != model.gameObject)
            {
                go.transform.position = Selection.activeGameObject.transform.position + Vector3.right * 1.5f;
            }

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
    }
}
