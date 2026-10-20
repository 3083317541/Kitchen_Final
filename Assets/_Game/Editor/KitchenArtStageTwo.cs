using System;
using System.IO;
using System.Linq;
using Kitchen.Data;
using Kitchen.Runtime;
using Kitchen.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kitchen.EditorTools
{
    public static class KitchenArtStageTwo
    {
        public const string BackupFolder = "Assets/_Game/Art/Backups/StageTwo";
        public const string FloorPath = "Assets/_Game/Prefabs/Grid/Grid_Floor.prefab";
        public const string TilePath = "Assets/_Game/Prefabs/Art/FloorTile_Kenney.prefab";
        public const string ShowcasePath = "Assets/_Game/Scenes/ArtShowcase.unity";
        private const string Furniture = "Assets/ThirdParty/Kenney/FurnitureKit/Models/";
        private const string Food = "Assets/ThirdParty/Kenney/FoodKit/Models/";
        private const string Materials = "Assets/_Game/Art/Materials/KitchenArt/";

        [MenuItem("Kitchen/Art/Apply Workstations And Floor (Art Only)")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play before replacing art.");
            Directory.CreateDirectory(BackupFolder);
            Directory.CreateDirectory(Materials);
            AssetDatabase.Refresh();
            WhiteboxMaterialLibrary.Load();
            foreach (string id in new[] { "chopping_station", "frying_pan", "assembly_table", "dish_washer" })
                Backup(CookerPath(id));
            Backup(FloorPath);
            CorrectChopper();
            BuildCooker("frying_pan", "kitchenStove");
            BuildCooker("assembly_table", "kitchenCabinetDrawer");
            BuildCooker("dish_washer", "kitchenSink");
            BuildFloor();
            AssetDatabase.SaveAssets();
            Debug.Log("[Art Stage Two] Four station fronts and tiled floor ready. Scenes and game data preserved.");
        }

        public static string CookerPath(string id) => "Assets/_Game/Prefabs/Cookers/Cooker_" + id + ".prefab";

        private static void Backup(string path)
        {
            string destination = BackupFolder + "/" + Path.GetFileName(path);
            if (AssetDatabase.LoadMainAssetAtPath(destination) == null && !AssetDatabase.CopyAsset(path, destination))
                throw new IOException("Could not back up " + path);
        }

        private static void CorrectChopper()
        {
            string path = CookerPath("chopping_station");
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform counter = root.transform.Find("Visual/ArtOffset/Counter");
                if (counter == null) throw new InvalidOperationException("Stage one chopper art is required.");
                counter.localRotation = Quaternion.Euler(0f, 180f, 0f);
                KitchenArtStageOne.PlaceOnFloor(counter.gameObject, Vector3.zero);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void BuildCooker(string id, string model)
        {
            string path = CookerPath(id);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform previous = root.transform.Find("Visual");
                if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
                Transform visual = Child(root.transform, "Visual");
                Transform offset = Child(visual, "ArtOffset");
                var cabinet = KitchenArtStageOne.Spawn(Furniture + model + ".fbx", offset, "Counter");
                cabinet.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                cabinet.transform.localScale = new Vector3(0.38f, 0.23f, 0.38f);
                KitchenArtStageOne.PlaceOnFloor(cabinet, Vector3.zero);
                float top = 1.035f;
                Vector3 inputPosition = new Vector3(-0.25f, top + 0.015f, 0.18f);
                Vector3 outputPosition = new Vector3(0.40f, top + 0.015f, 0.18f);
                if (id == "frying_pan")
                {
                    var pan = KitchenArtStageOne.Spawn(Food + "frying-pan.fbx", offset, "FryingPan");
                    pan.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                    KitchenArtStageOne.FitWidth(pan, 1.15f);
                    KitchenArtStageOne.PlaceOnFloor(pan, new Vector3(-0.12f, top + 0.01f, 0.16f));
                    inputPosition = new Vector3(-0.27f, top + 0.09f, 0.16f);
                    outputPosition = inputPosition;
                }
                else
                {
                    int plates = id == "dish_washer" ? 3 : 1;
                    for (int index = 0; index < plates; index++)
                    {
                        var plate = KitchenArtStageOne.Spawn(Food + "plate.fbx", offset, "Plate_" + index);
                        KitchenArtStageOne.FitWidth(plate, 0.43f);
                        KitchenArtStageOne.PlaceOnFloor(plate, new Vector3(0.51f, top + index * 0.04f + 0.01f, 0.30f));
                    }
                    if (id == "dish_washer")
                    {
                        inputPosition = new Vector3(-0.16f, top - 0.04f, 0.14f);
                        outputPosition = new Vector3(0.51f, top + 0.15f, 0.30f);
                    }
                }
                Transform input = Child(offset, "InputAnchor");
                input.localPosition = inputPosition;
                Transform output = Child(offset, "OutputAnchor");
                output.localPosition = outputPosition;
                Child(offset, "EffectAnchor").localPosition = new Vector3(0f, top + 0.20f, 0.16f);
                var display = root.GetComponent<CookerItemDisplay>() ?? root.AddComponent<CookerItemDisplay>();
                var settings = new SerializedObject(display);
                settings.FindProperty("inputAnchor").objectReferenceValue = input;
                settings.FindProperty("outputAnchor").objectReferenceValue = output;
                settings.ApplyModifiedPropertiesWithoutUndo();
                var view = root.GetComponent<CookerView>();
                settings = new SerializedObject(view);
                float maxHeight = KitchenArtStageOne.BoundsOf(offset.gameObject).max.y;
                settings.FindProperty("bodySize").vector3Value = new Vector3(1.65f, maxHeight, 1.72f);
                settings.ApplyModifiedPropertiesWithoutUndo();
                view.EnsureBuilt();
                GameLayers.SetLayerRecursively(root, GameLayers.Interactable);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log("[Art Stage Two] " + id + " bounds " + KitchenArtStageOne.BoundsOf(offset.gameObject).size);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static Transform Child(Transform parent, string name)
        {
            Transform child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static Material Material(string name, Color color, bool overlay = false)
        {
            string path = Materials + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find(overlay ? "Sprites/Default" : "Standard"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.1f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void BuildFloor()
        {
            var tile = new GameObject("FloorTile_Kenney");
            try
            {
                var model = KitchenArtStageOne.Spawn(Furniture + "floorFull.fbx", tile.transform, "Tile");
                Bounds bounds = KitchenArtStageOne.BoundsOf(model);
                model.transform.localScale = Vector3.Scale(model.transform.localScale,
                    new Vector3(1f / bounds.size.x, 0.09f / bounds.size.y, 1f / bounds.size.z));
                bounds = KitchenArtStageOne.BoundsOf(model);
                model.transform.localPosition -= new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
                GameLayers.SetLayerRecursively(tile, GameLayers.Ground);
                PrefabUtility.SaveAsPrefabAsset(tile, TilePath);
            }
            finally { UnityEngine.Object.DestroyImmediate(tile); }

            GameObject root = PrefabUtility.LoadPrefabContents(FloorPath);
            try
            {
                foreach (Transform child in root.transform.Cast<Transform>().ToArray())
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                foreach (var renderer in root.GetComponents<Renderer>()) UnityEngine.Object.DestroyImmediate(renderer);
                foreach (var filter in root.GetComponents<MeshFilter>()) UnityEngine.Object.DestroyImmediate(filter);
                root.transform.localPosition = Vector3.zero;
                root.transform.localRotation = Quaternion.identity;
                root.transform.localScale = Vector3.one;
                var view = root.GetComponent<KitchenFloorView>() ?? root.AddComponent<KitchenFloorView>();
                var settings = new SerializedObject(view);
                settings.FindProperty("tilePrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(TilePath);
                settings.FindProperty("lightTile").objectReferenceValue = Material("TileCream", new Color(0.89f, 0.85f, 0.71f));
                settings.FindProperty("greenTile").objectReferenceValue = Material("TileSage", new Color(0.53f, 0.68f, 0.57f));
                settings.FindProperty("borderMaterial").objectReferenceValue = Material("WoodenEdge", new Color(0.47f, 0.30f, 0.17f));
                settings.FindProperty("groutMaterial").objectReferenceValue = Material("Grout", new Color(0.60f, 0.56f, 0.46f));
                settings.FindProperty("cellOverlayMaterial").objectReferenceValue = Material("HiddenGrid", Color.clear, true);
                settings.ApplyModifiedPropertiesWithoutUndo();
                view.Build(5, 5, 2f);
                GameLayers.SetLayerRecursively(root, GameLayers.Ground);
                PrefabUtility.SaveAsPrefabAsset(root, FloorPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        [MenuItem("Kitchen/Art/Refresh Floor Preview (Art Only)")]
        public static void RefreshFloorPreview()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play before editing the preview.");
            var runtime = UnityEngine.Object.FindObjectOfType<GameRuntime>();
            if (runtime == null) throw new InvalidOperationException("Open the Kitchen scene first.");
            var settings = new SerializedObject(runtime);
            var level = settings.FindProperty("levelConfig").objectReferenceValue as LevelConfig;
            var grid = settings.FindProperty("gridRoot").objectReferenceValue as Transform;
            if (level == null || grid == null) throw new InvalidOperationException("Kitchen level or grid root missing.");
            Transform old = grid.Find(KitchenScenePreview.PreviewContainerName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            Transform container = Child(grid, KitchenScenePreview.PreviewContainerName);
            GridView.CreateForLevel(level, container);
            Undo.RegisterCreatedObjectUndo(container.gameObject, "Refresh floor preview");
            EditorSceneManager.MarkSceneDirty(runtime.gameObject.scene);
        }

        [MenuItem("Kitchen/Art/Open Workstation Showcase")]
        public static void OpenShowcase()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ShowcasePath)) EditorSceneManager.OpenScene(ShowcasePath);
            else CreateShowcase();
        }

        public static void CreateShowcase()
        {
            if (File.Exists(ShowcasePath)) throw new IOException("Showcase already exists; preserve manual edits.");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var floor = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(FloorPath));
            floor.GetComponent<KitchenFloorView>().Build(5, 5, 2f);
            string[] ids = { "chopping_station", "frying_pan", "assembly_table", "dish_washer" };
            for (int index = 0; index < ids.Length; index++)
            {
                var cooker = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CookerPath(ids[index])));
                cooker.SetActive(true);
                cooker.transform.position = new Vector3(index % 2 == 0 ? 2.1f : -2.1f, 0f, index < 2 ? 1.5f : -1.6f);
            }
            var panda = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(KitchenArtStageOne.StaffPath));
            panda.SetActive(true);
            panda.transform.position = new Vector3(0f, 0f, 3.1f);
            var idle = AssetDatabase.LoadAllAssetsAtPath("Assets/ThirdParty/Quaternius/SushiRestaurant/Models/Panda.fbx")
                .OfType<AnimationClip>().First(clip => clip.name.EndsWith("|Idle"));
            idle.SampleAnimation(panda.transform.Find("Visual/Model/ArtOffset/Panda").gameObject, 0f);
            var light = new GameObject("Sun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.85f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.64f, 0.68f);
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(8f, 12f, 14f);
            camera.transform.LookAt(new Vector3(0f, 0f, 0f));
            camera.orthographic = true;
            camera.orthographicSize = 6.4f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.77f, 0.82f, 0.82f);
            Child(null, "ArtDressing");
            EditorSceneManager.SaveScene(scene, ShowcasePath);
        }
    }
}
