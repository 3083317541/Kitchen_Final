using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kitchen.Core;
using Kitchen.Data;
using Kitchen.Entities;
using Kitchen.Runtime;
using Kitchen.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kitchen.EditorTools
{
    public static class KitchenArtStageOneVerifier
    {
        [Serializable]
        public class Check
        {
            public string name;
            public bool passed;
            public string detail;
        }

        [Serializable]
        public class Report
        {
            public string unityVersion;
            public Check[] checks;
        }

        private static readonly List<Check> Checks = new List<Check>();

        public static void Run()
        {
            Checks.Clear();
            Directory.CreateDirectory("Logs");
            try
            {
                EditorSceneManager.OpenScene("Assets/_Game/Scenes/Kitchen.unity", OpenSceneMode.Single);
                KitchenAcceptanceVerifier.Run("Logs/acceptance_stage1.json");
                ValidateRuntime();
                RenderComparison();
            }
            catch (Exception exception)
            {
                Record("exception", false, exception.ToString());
                Debug.LogException(exception);
            }
            finally
            {
                File.WriteAllText("Logs/art_stage1_report.json", JsonUtility.ToJson(new Report
                {
                    unityVersion = Application.unityVersion,
                    checks = Checks.ToArray()
                }, true));
            }
            if (Checks.Any(check => !check.passed))
            {
                throw new InvalidOperationException("Art stage one checks failed; read Logs/art_stage1_report.json.");
            }
        }

        private static void ValidateRuntime()
        {
            var runtime = UnityEngine.Object.FindObjectOfType<GameRuntime>();
            var settings = new SerializedObject(runtime);
            settings.FindProperty("levelConfig").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<LevelConfig>("Assets/_Game/Resources/Kitchen/Level_01.asset");
            settings.FindProperty("levelManager").objectReferenceValue = null;
            settings.ApplyModifiedPropertiesWithoutUndo();
            runtime.Boot();
            runtime.StartLevel();
            StaffRuntime employee = runtime.Staff.Staff[0];
            var chopperDefinition = AssetDatabase.LoadAssetAtPath<CookerDefinition>("Assets/_Game/Data/Cookers/Cooker_chopping_station.asset");
            if (chopperDefinition == null)
            {
                chopperDefinition = runtime.Cookers.Catalogue.First(definition => definition.Id == "chopping_station");
            }
            bool built = runtime.TryBuildCooker(chopperDefinition, new Vector2Int(2, 2));
            var cooker = runtime.Cookers.GetCookerAt(new Vector2Int(2, 2));
            Record("build_replacement", built && cooker != null && cooker.View.GetComponent<CookerItemDisplay>() != null,
                "The real first level purchases and spawns the replacement through its existing definition.");
            var display = cooker.View.GetComponent<CookerItemDisplay>();
            var tomato = runtime.Level.Items.First(item => item.Id == "tomato");
            var sliced = runtime.Level.Items.First(item => item.Id == "tomato_slice");
            var recipe = runtime.Recipes.RecipesFor(CookerKind.ChoppingStation)
                .First(candidate => candidate.Inputs.Any(input => input.Item == tomato));
            cooker.PreferredRecipeId = recipe.Id;
            Record("front_only_assignment", !runtime.Staff.TryAssignCooker(employee, cooker),
                "Staff on the south side cannot activate a north-facing station.");
            Move(runtime, employee, Vector2Int.left);
            Move(runtime, employee, Vector2Int.up);
            Move(runtime, employee, Vector2Int.up);
            Move(runtime, employee, Vector2Int.right);
            Record("front_assignment", runtime.Staff.TryAssignCooker(employee, cooker) && cooker.IsActive,
                "Staff arrives at the actual front work cell before assignment.");
            for (int index = 0; index < 4; index++)
            {
                runtime.Work.Tick(0.31f);
                runtime.Cookers.Tick(0f);
            }
            Record("reserved_input_display", display.DisplayedInput == tomato,
                "The chopping station displays the tomato reserved by the existing work strategy.");
            Transform input = cooker.View.transform.Find("Visual/ArtOffset/InputAnchor/InputItem");
            int inputId = input != null ? input.GetInstanceID() : 0;
            string stockBefore = runtime.Inventory.Describe();
            display.Refresh();
            display.Refresh();
            Record("readonly_reused_display", inputId != 0 &&
                cooker.View.transform.Find("Visual/ArtOffset/InputAnchor/InputItem").GetInstanceID() == inputId &&
                stockBefore == runtime.Inventory.Describe(),
                "Repeated refreshes reuse the model and do not change stock.");
            float progress = cooker.WorkProgress;
            Move(runtime, employee, Vector2Int.left);
            runtime.Work.Tick(1f);
            runtime.Cookers.Tick(0f);
            Record("pause_keeps_input", Mathf.Approximately(progress, cooker.WorkProgress) && display.DisplayedInput == tomato,
                "Leaving the work cell preserves progress and the reserved tomato visual.");
            Move(runtime, employee, Vector2Int.right);
            runtime.Staff.TryAssignCooker(employee, cooker);
            int stock = runtime.Inventory.TotalCount(sliced);
            for (int index = 0; index < 12 && runtime.Inventory.TotalCount(sliced) == stock; index++)
            {
                runtime.Work.Tick(0.31f);
                runtime.Cookers.Tick(0f);
            }
            Record("completion_stock_only", runtime.Inventory.TotalCount(sliced) == stock + recipe.Outputs[0].Count &&
                !cooker.HasAnyOutput && display.DisplayedOutput == sliced,
                "Completed slices enter inventory once; the output model is only a temporary completion cue.");
            employee.View.GetComponent<StaffAnimationDriver>().Refresh();
            var animator = employee.View.Animator;
            animator.Update(0f);
            Record("panda_animation", animator != null && animator.runtimeAnimatorController != null &&
                animator.GetInteger("Action") == 2 && !animator.applyRootMotion,
                "Panda uses the imported controller and chopping action while assigned; root motion is disabled.");
            Record("single_selection_ring", employee.View.GetComponentsInChildren<Transform>(true)
                .Count(transform => transform.name == "SelectionRing") == 1,
                "Prefab adoption does not create a second selection ring.");
            SampleEmployee(employee);
            Bounds staffBounds = KitchenArtStageOne.BoundsOf(employee.View.transform.Find("Visual/Model").gameObject);
            Record("panda_actual_size", staffBounds.size.y > 1.4f && staffBounds.size.y < 1.7f &&
                Mathf.Abs(staffBounds.min.y - employee.View.transform.position.y) < 0.15f,
                "Posed mesh bounds: " + staffBounds);
            Record("materials", employee.View.GetComponentsInChildren<Renderer>(true)
                .Concat(cooker.View.GetComponentsInChildren<Renderer>(true))
                .All(renderer => renderer.sharedMaterials.All(material => material != null && material.shader != null &&
                    material.shader.name != "Hidden/InternalErrorShader")),
                "All static and skinned renderers have valid Built-in materials.");
            Bounds bounds = KitchenArtStageOne.BoundsOf(cooker.View.transform.Find("Visual/ArtOffset").gameObject);
            Record("station_footprint", bounds.size.x <= 1.8f && bounds.size.z <= 1.8f,
                "Actual station bounds: " + bounds.size.ToString("F3"));
            int reserved = runtime.Inventory.ReservedCount(tomato);
            int ticks = cooker.WorkTicks;
            bool directions = true;
            for (int index = 0; index < 4; index++)
            {
                directions &= runtime.Cookers.TryRotateCooker(cooker);
                Vector3 expected = new Vector3(CookerFacingUtility.Offset(cooker.Facing).x, 0f,
                    CookerFacingUtility.Offset(cooker.Facing).y);
                directions &= Vector3.Dot(cooker.View.transform.Find("Visual").forward, expected) > 0.99f;
            }
            Record("rotation_preserves_manual_work", directions && employee.AssignedCookerId == 0 &&
                runtime.Inventory.ReservedCount(tomato) == reserved && cooker.WorkTicks == ticks,
                "Four rotations align the visual front, release the task and preserve reserved ingredients and progress.");
            runtime.Staff.TryAssignCooker(employee, cooker);
            cooker.View.SetSelected(true);
            runtime.UI.RefreshAll();
            SampleEmployee(employee);
            Capture(runtime.WorldCamera, "Logs/art_stage1_kitchen.png", true);
            Record("kitchen_render", File.Exists("Logs/art_stage1_kitchen.png"),
                "Rendered the actual kitchen camera with its current HUD.");
        }

        private static void Move(GameRuntime runtime, StaffRuntime employee, Vector2Int direction)
        {
            if (!runtime.Staff.TryMove(employee, direction))
            {
                throw new InvalidOperationException("Could not move test staff " + direction);
            }
            employee.View.GetComponent<StaffAnimationDriver>()?.Refresh();
            runtime.Staff.Tick(1f);
            runtime.Cookers.Tick(0f);
        }

        private static void SampleEmployee(StaffRuntime employee)
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath("Assets/ThirdParty/Quaternius/SushiRestaurant/Models/Panda.fbx")
                .OfType<AnimationClip>().First(candidate => candidate.name.EndsWith("|Idle"));
            var panda = employee.View.transform.Find("Visual/Model/ArtOffset/Panda");
            clip.SampleAnimation(panda.gameObject, 0f);
        }

        private static void RenderComparison()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientLight = new Color(0.55f, 0.57f, 0.62f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            var light = new GameObject("ArtComparisonLight").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            light.shadows = LightShadows.Soft;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(0f, -0.12f, 0f);
            floor.transform.localScale = new Vector3(12f, 0.2f, 7f);
            var material = new Material(Shader.Find("Standard"));
            material.color = new Color(0.73f, 0.76f, 0.73f);
            material.SetFloat("_Glossiness", 0.12f);
            floor.GetComponent<Renderer>().sharedMaterial = material;
            string[] paths =
            {
                KitchenArtStageOne.BackupFolder + "/Cooker_chopping_station.prefab",
                KitchenArtStageOne.CookerPath,
                KitchenArtStageOne.ArtFolder + "/Chopping_Quaternius_Comparison.prefab"
            };
            for (int index = 0; index < paths.Length; index++)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(paths[index]));
                instance.SetActive(true);
                instance.transform.position = new Vector3((1 - index) * 3f, 0f, 0f);
                var label = new GameObject("Label").AddComponent<TextMesh>();
                label.text = new[] { "Original", "Kenney", "Quaternius" }[index];
                label.anchor = TextAnchor.MiddleCenter;
                label.fontSize = 48;
                label.characterSize = 0.12f;
                label.color = new Color(0.12f, 0.14f, 0.15f);
                label.transform.position = new Vector3((1 - index) * 3f, 0.03f, 1.65f);
                label.transform.rotation = Quaternion.Euler(40f, 180f, 0f);
                var panda = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(KitchenArtStageOne.StaffPath));
                panda.SetActive(true);
                panda.transform.position = new Vector3((1 - index) * 3f + 0.9f, 0f, -1.6f);
                var model = panda.transform.Find("Visual/Model/ArtOffset/Panda");
                var idle = AssetDatabase.LoadAllAssetsAtPath("Assets/ThirdParty/Quaternius/SushiRestaurant/Models/Panda.fbx")
                    .OfType<AnimationClip>().First(clip => clip.name.EndsWith("|Idle"));
                idle.SampleAnimation(model.gameObject, 0f);
                if (index == 2)
                {
                    Bounds bounds = KitchenArtStageOne.BoundsOf(instance.transform.Find("Visual/ArtOffset").gameObject);
                    Record("comparison_actual_size", bounds.size.x > 1.7f && bounds.size.x < 1.9f,
                        "Quaternius comparison bounds: " + bounds.size);
                }
            }
            var camera = new GameObject("ArtComparisonCamera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 8f, 9.5f);
            camera.transform.LookAt(new Vector3(0f, 0.4f, 0f));
            camera.orthographic = true;
            camera.orthographicSize = 4.4f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.88f, 0.90f, 0.91f);
            Capture(camera, "Logs/art_stage1_comparison.png", false);
            Record("comparison_render", File.Exists("Logs/art_stage1_comparison.png"),
                "Original, Kenney and Quaternius cabinets rendered with the same camera and lighting.");
        }

        internal static void Capture(Camera camera, string path, bool includeHud)
        {
            if (includeHud)
            {
                foreach (var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>())
                {
                    if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                    {
                        canvas.renderMode = RenderMode.ScreenSpaceCamera;
                        canvas.worldCamera = camera;
                        canvas.planeDistance = 2f;
                    }
                }
                Canvas.ForceUpdateCanvases();
            }
            var target = new RenderTexture(1920, 1080, 24) { antiAliasing = 4 };
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            float previousAspect = camera.aspect;
            camera.targetTexture = target;
            camera.aspect = 1920f / 1080f;
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            camera.targetTexture = previousTarget;
            camera.aspect = previousAspect;
            RenderTexture.active = previousActive;
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(image);
        }

        private static void Record(string name, bool passed, string detail)
        {
            Checks.Add(new Check { name = name, passed = passed, detail = detail });
            Debug.Log("[Art Stage One] " + (passed ? "PASS " : "FAIL ") + name + ": " + detail);
        }
    }
}
