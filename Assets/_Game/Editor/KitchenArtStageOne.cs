using System;
using System.IO;
using System.Linq;
using Kitchen.View;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Kitchen.EditorTools
{
    public static class KitchenArtStageOne
    {
        public const string ArtFolder = "Assets/_Game/Prefabs/Art";
        public const string BackupFolder = "Assets/_Game/Art/Backups/StageOne";
        public const string StaffPath = "Assets/_Game/Prefabs/Staff/Staff_Chef.prefab";
        public const string CookerPath = "Assets/_Game/Prefabs/Cookers/Cooker_chopping_station.prefab";
        private const string FoodFolder = "Assets/ThirdParty/Kenney/FoodKit/Models";
        private const string SushiFolder = "Assets/ThirdParty/Quaternius/SushiRestaurant";
        private const string ControllerPath = "Assets/_Game/Art/Animations/Panda.controller";

        [MenuItem("Kitchen/Art/Apply Stage One (Art Only)")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Stop Play before applying art.");
            }

            RequireAsset<GameObject>(SushiFolder + "/Models/Panda.fbx");
            RequireAsset<GameObject>("Assets/ThirdParty/Kenney/FurnitureKit/Models/kitchenCabinet.fbx");
            Directory.CreateDirectory(ArtFolder);
            Directory.CreateDirectory(BackupFolder);
            Directory.CreateDirectory("Assets/_Game/Art/Animations");
            AssetDatabase.Refresh();
            WhiteboxMaterialLibrary.Load();
            Backup(StaffPath);
            Backup(CookerPath);
            Backup("Assets/_Game/Prefabs/Ingredients/Item_tomato.prefab");
            Backup("Assets/_Game/Prefabs/Ingredients/Item_tomato_slice.prefab");
            ConfigureClips();
            BuildController();
            BuildStaff();
            BuildCooker(CookerPath, false);
            BuildCooker(ArtFolder + "/Chopping_Quaternius_Comparison.prefab", true);
            BuildItem("tomato", "tomato");
            BuildItem("tomato_slice", "tomato-slice");
            AssetDatabase.SaveAssets();
            Debug.Log("[Art Stage One] Updated Panda, chopping station and two tomato prefabs. Gameplay assets and scenes preserved.");
        }

        private static T RequireAsset<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new InvalidOperationException("Missing art asset: " + path);
            }
            return asset;
        }

        private static void Backup(string path)
        {
            string destination = BackupFolder + "/" + Path.GetFileName(path);
            if (AssetDatabase.LoadMainAssetAtPath(destination) == null && !AssetDatabase.CopyAsset(path, destination))
            {
                throw new IOException("Could not preserve original prefab: " + path);
            }
        }

        private static void ConfigureClips()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(SushiFolder + "/Models/Panda.fbx");
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                clip.loopTime = clip.name.EndsWith("|Idle") || clip.name.EndsWith("|Walk") ||
                    clip.name.EndsWith("|Chop_Loop");
            }
            importer.clipAnimations = clips;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.SaveAndReimport();
        }

        private static AnimationClip Clip(string suffix)
        {
            return AssetDatabase.LoadAllAssetsAtPath(SushiFolder + "/Models/Panda.fbx")
                .OfType<AnimationClip>().First(clip => clip.name.EndsWith("|" + suffix));
        }

        private static void BuildController()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }
            if (controller.layers.Length == 0)
            {
                controller.AddLayer("Base Layer");
            }
            controller.parameters = new[]
            {
                new AnimatorControllerParameter { name = "Selected", type = AnimatorControllerParameterType.Bool },
                new AnimatorControllerParameter { name = "Action", type = AnimatorControllerParameterType.Int }
            };
            var machine = controller.layers[0].stateMachine;
            foreach (var child in machine.states.ToArray())
            {
                machine.RemoveState(child.state);
            }
            foreach (var transition in machine.anyStateTransitions.ToArray())
            {
                machine.RemoveAnyStateTransition(transition);
            }
            string[] names = { "Idle", "Walk", "Chop_Loop" };
            for (int index = 0; index < names.Length; index++)
            {
                var state = machine.AddState(names[index], new Vector3(280f, 60f + index * 70f));
                state.motion = Clip(names[index]);
                state.writeDefaultValues = false;
                var transition = machine.AddAnyStateTransition(state);
                transition.hasExitTime = false;
                transition.duration = 0.12f;
                transition.canTransitionToSelf = false;
                transition.AddCondition(AnimatorConditionMode.Equals, index, "Action");
                if (index == 0)
                {
                    machine.defaultState = state;
                }
            }
            EditorUtility.SetDirty(controller);
        }

        private static void BuildStaff()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(StaffPath);
            try
            {
                Transform visual = ResetVisual(root);
                Transform model = Child(visual, "Model");
                Transform offset = Child(model, "ArtOffset");
                var panda = Spawn(SushiFolder + "/Models/Panda.fbx", offset, "Panda");
                Clip("Idle").SampleAnimation(panda, 0f);
                Bounds initial = BoundsOf(panda);
                float scale = 1.55f / initial.size.y;
                offset.localScale = Vector3.one * scale;
                Bounds scaled = BoundsOf(panda);
                offset.localPosition -= new Vector3(scaled.center.x, scaled.min.y, scaled.center.z);
                var animator = panda.GetComponentInChildren<Animator>() ?? panda.AddComponent<Animator>();
                animator.runtimeAnimatorController = RequireAsset<AnimatorController>(ControllerPath);
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                if (root.GetComponent<StaffAnimationDriver>() == null)
                {
                    root.AddComponent<StaffAnimationDriver>();
                }
                var settings = new SerializedObject(root.GetComponent<StaffView>());
                settings.FindProperty("bodyHeight").floatValue = 1.0f;
                settings.FindProperty("bodyRadius").floatValue = 0.28f;
                settings.ApplyModifiedPropertiesWithoutUndo();
                root.GetComponent<StaffView>().EnsureBuilt();
                SetLayers(root);
                PrefabUtility.SaveAsPrefabAsset(root, StaffPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void BuildCooker(string path, bool quaternius)
        {
            bool existing = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
            GameObject root = existing ? PrefabUtility.LoadPrefabContents(path) : new GameObject("Chopping_Quaternius_Comparison");
            try
            {
                var view = root.GetComponent<CookerView>() ?? root.AddComponent<CookerView>();
                Transform visual = ResetVisual(root);
                Transform offset = Child(visual, "ArtOffset");
                GameObject cabinet;
                if (quaternius)
                {
                    cabinet = Spawn(SushiFolder + "/Models/Environment_Counter_Straight.fbx", offset, "Counter");
                    cabinet.transform.localScale *= 0.45f;
                }
                else
                {
                    cabinet = Spawn("Assets/ThirdParty/Kenney/FurnitureKit/Models/kitchenCabinet.fbx", offset, "Counter");
                    cabinet.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    cabinet.transform.localScale = new Vector3(0.38f, 0.23f, 0.38f);
                }
                Bounds bounds = BoundsOf(cabinet);
                cabinet.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                float top = BoundsOf(cabinet).max.y;
                var board = Spawn(FoodFolder + "/cutting-board.fbx", offset, "CuttingBoard");
                FitWidth(board, 0.82f);
                PlaceOnFloor(board, new Vector3(-0.20f, top + 0.012f, 0.22f));
                float boardTop = BoundsOf(board).max.y;
                var knife = Spawn(FoodFolder + "/cooking-knife.fbx", offset, "Knife");
                knife.transform.localRotation = Quaternion.Euler(0f, 35f, 0f);
                FitWidth(knife, 0.43f);
                PlaceOnFloor(knife, new Vector3(0.42f, top + 0.015f, 0.12f));
                Transform input = Child(offset, "InputAnchor");
                input.localPosition = new Vector3(-0.20f, boardTop + 0.01f, 0.22f);
                Transform output = Child(offset, "OutputAnchor");
                output.localPosition = new Vector3(0.40f, top + 0.02f, -0.28f);
                Child(offset, "EffectAnchor").localPosition = new Vector3(0f, top + 0.18f, 0f);
                var display = root.GetComponent<CookerItemDisplay>() ?? root.AddComponent<CookerItemDisplay>();
                var settings = new SerializedObject(display);
                settings.FindProperty("inputAnchor").objectReferenceValue = input;
                settings.FindProperty("outputAnchor").objectReferenceValue = output;
                settings.ApplyModifiedPropertiesWithoutUndo();
                settings = new SerializedObject(view);
                settings.FindProperty("bodySize").vector3Value = new Vector3(1.65f, top, 1.72f);
                settings.ApplyModifiedPropertiesWithoutUndo();
                view.EnsureBuilt();
                SetLayers(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                if (existing) PrefabUtility.UnloadPrefabContents(root);
                else UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void BuildItem(string id, string model)
        {
            string path = "Assets/_Game/Prefabs/Ingredients/Item_" + id + ".prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (Transform child in root.transform.Cast<Transform>().ToArray())
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
                foreach (var renderer in root.GetComponents<Renderer>())
                {
                    UnityEngine.Object.DestroyImmediate(renderer);
                }
                foreach (var filter in root.GetComponents<MeshFilter>())
                {
                    UnityEngine.Object.DestroyImmediate(filter);
                }
                var art = Spawn(FoodFolder + "/" + model + ".fbx", Child(root.transform, "Visual"), model);
                FitWidth(art, id == "tomato" ? 0.36f : 0.40f);
                PlaceOnFloor(art, Vector3.zero);
                SetLayers(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Transform ResetVisual(GameObject root)
        {
            Transform old = root.transform.Find("Visual");
            if (old != null)
            {
                UnityEngine.Object.DestroyImmediate(old.gameObject);
            }
            return Child(root.transform, "Visual");
        }

        private static Transform Child(Transform parent, string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        internal static GameObject Spawn(string path, Transform parent, string name)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(RequireAsset<GameObject>(path), parent);
            instance.name = name;
            instance.SetActive(true);
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.gameObject.SetActive(true);
                renderer.enabled = true;
            }
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }
            foreach (var body in instance.GetComponentsInChildren<Rigidbody>(true))
            {
                UnityEngine.Object.DestroyImmediate(body);
            }
            return instance;
        }

        internal static Bounds BoundsOf(GameObject model)
        {
            bool found = false;
            Bounds bounds = default;
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                Bounds current = renderer.bounds;
                if (renderer is SkinnedMeshRenderer skinned)
                {
                    var baked = new Mesh();
                    skinned.BakeMesh(baked, true);
                    var vertices = baked.vertices;
                    if (vertices.Length > 0)
                    {
                        current = new Bounds(skinned.transform.TransformPoint(vertices[0]), Vector3.zero);
                        foreach (var vertex in vertices)
                        {
                            current.Encapsulate(skinned.transform.TransformPoint(vertex));
                        }
                    }
                    UnityEngine.Object.DestroyImmediate(baked);
                }
                if (!found) { bounds = current; found = true; }
                else bounds.Encapsulate(current);
            }
            return bounds;
        }

        internal static void FitWidth(GameObject model, float width)
        {
            Bounds bounds = BoundsOf(model);
            model.transform.localScale *= width / Mathf.Max(0.0001f, Mathf.Max(bounds.size.x, bounds.size.z));
        }

        internal static void PlaceOnFloor(GameObject model, Vector3 position)
        {
            Bounds bounds = BoundsOf(model);
            model.transform.localPosition += position - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        }

        private static void SetLayers(GameObject root)
        {
            GameLayers.SetLayerRecursively(root, GameLayers.Interactable);
        }
    }
}
