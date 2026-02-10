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

        [MenuItem("GameObject/CSG/CSG Demo (Boolean Ops)", false, 40)]
        static void SpawnCSGDemo()
        {
            SpawnDemoScene();
        }

        /// <summary>
        /// Spawns the classic Wikipedia CSG demo: three models side by side
        /// showing Union, Subtract, and Intersect of a cube and sphere.
        /// </summary>
        public static void SpawnDemoScene()
        {
            var root = new GameObject("CSG_Demo");
            Undo.RegisterCreatedObjectUndo(root, "Spawn CSG Demo");

            float spacing = 3f;
            var ops = new (string label, CSGOperation op)[]
            {
                ("Union", CSGOperation.Additive),
                ("Subtract", CSGOperation.Subtractive),
                ("Intersect", CSGOperation.Intersect),
            };

            for (int i = 0; i < ops.Length; i++)
            {
                float x = (i - 1) * spacing;

                var modelGo = new GameObject($"CSG_{ops[i].label}");
                modelGo.transform.SetParent(root.transform, false);
                modelGo.transform.localPosition = new Vector3(x, 0, 0);
                var model = modelGo.AddComponent<CSGModel>();

                // Additive box
                var boxGo = new GameObject("Box");
                boxGo.transform.SetParent(modelGo.transform, false);
                var boxBrush = boxGo.AddComponent<CSGBrush>();
                boxBrush.Descriptor.FromCSGPlanes(BrushFactory.Box(Vector3.one * 0.6f));
                boxBrush.Descriptor.Operation = CSGOperation.Additive;
                boxBrush.Descriptor.Order = 0;

                // Sphere offset to partially overlap, with the target operation
                var sphereGo = new GameObject("Sphere");
                sphereGo.transform.SetParent(modelGo.transform, false);
                sphereGo.transform.localPosition = new Vector3(0.35f, 0.35f, 0.35f);
                var sphereBrush = sphereGo.AddComponent<CSGBrush>();
                sphereBrush.Descriptor.FromCSGPlanes(BrushFactory.Sphere(0.55f, 10, 10));
                sphereBrush.Descriptor.Operation = ops[i].op;
                sphereBrush.Descriptor.Order = 1;

                var m = model; // capture for closure
                EditorApplication.delayCall += () =>
                {
                    if (m != null)
                        m.RebuildAll();
                };
            }

            Selection.activeGameObject = root;
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.FrameSelected();
        }

        [MenuItem("GameObject/CSG/CSG Icon Shape", false, 41)]
        static void SpawnCSGIcon()
        {
            SpawnIconShape();
        }

        /// <summary>
        /// Spawns the Wikipedia CSG icon: a cube intersected with a sphere,
        /// with three cylinders subtracted along X, Y, and Z axes.
        /// (Cube ∩ Sphere) − CylX − CylY − CylZ
        /// </summary>
        public static void SpawnIconShape()
        {
            var modelGo = new GameObject("CSG_Icon");
            Undo.RegisterCreatedObjectUndo(modelGo, "Spawn CSG Icon");
            var model = modelGo.AddComponent<CSGModel>();

            int order = 0;

            // 1. Additive cube (base shape)
            var boxGo = new GameObject("Cube");
            boxGo.transform.SetParent(modelGo.transform, false);
            var boxBrush = boxGo.AddComponent<CSGBrush>();
            boxBrush.Descriptor.FromCSGPlanes(BrushFactory.Box(Vector3.one * 0.5f));
            boxBrush.Descriptor.Operation = CSGOperation.Additive;
            boxBrush.Descriptor.Order = order++;

            // 2. Intersect sphere (rounds the cube edges/corners)
            //    r=0.68 clips corners aggressively while preserving flat cube faces
            var sphereGo = new GameObject("Sphere");
            sphereGo.transform.SetParent(modelGo.transform, false);
            var sphereBrush = sphereGo.AddComponent<CSGBrush>();
            sphereBrush.Descriptor.FromCSGPlanes(BrushFactory.Sphere(0.68f, 8, 8));
            sphereBrush.Descriptor.Operation = CSGOperation.Intersect;
            sphereBrush.Descriptor.Order = order++;

            // 3-5. Subtractive cylinders along each axis (punch through-holes)
            //      r=0.28 gives prominent holes matching the Wikipedia graphic
            var cylAxes = new (string name, Vector3 euler)[]
            {
                ("Cylinder_Y", Vector3.zero),
                ("Cylinder_X", new Vector3(0, 0, 90)),
                ("Cylinder_Z", new Vector3(90, 0, 0)),
            };
            foreach (var (name, euler) in cylAxes)
            {
                var cylGo = new GameObject(name);
                cylGo.transform.SetParent(modelGo.transform, false);
                cylGo.transform.localRotation = Quaternion.Euler(euler);
                var cylBrush = cylGo.AddComponent<CSGBrush>();
                cylBrush.Descriptor.FromCSGPlanes(BrushFactory.Cylinder(0.28f, 0.8f, 12));
                cylBrush.Descriptor.Operation = CSGOperation.Subtractive;
                cylBrush.Descriptor.Order = order++;
            }

            EditorApplication.delayCall += () =>
            {
                if (model != null)
                    model.RebuildAll();
            };

            Selection.activeGameObject = modelGo;
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.FrameSelected();
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
