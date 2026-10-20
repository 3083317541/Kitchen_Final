using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kitchen.Core;
using Kitchen.Data;
using Kitchen.Grid;
using Kitchen.Runtime;
using Kitchen.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Kitchen.EditorTools
{
    public static class KitchenArtStageTwoVerifier
    {
        [Serializable]
        private class AcceptanceSummary
        {
            public int total;
            public int passed;
            public int failed;
        }

        public static void Run()
        {
            Directory.CreateDirectory("Logs");
            EditorSceneManager.OpenScene("Assets/_Game/Scenes/Kitchen.unity");
            KitchenAcceptanceVerifier.Run("Logs/acceptance_stage2.json");
            var acceptance = JsonUtility.FromJson<AcceptanceSummary>(File.ReadAllText("Logs/acceptance_stage2.json"));
            List<KitchenArtStageOneVerifier.Check> checks = ValidateRuntime();
            checks.Add(new KitchenArtStageOneVerifier.Check
            {
                name = "existing_acceptance", passed = acceptance.failed == 0 && acceptance.total > 0,
                detail = acceptance.passed + "/" + acceptance.total
            });
            if (!File.Exists(KitchenArtStageTwo.ShowcasePath)) KitchenArtStageTwo.CreateShowcase();
            else EditorSceneManager.OpenScene(KitchenArtStageTwo.ShowcasePath);
            KitchenArtStageOneVerifier.Capture(Camera.main, "Logs/art_stage2_showcase.png", false);
            WriteReport("Logs/art_stage2_report.json", checks);
        }

        public static List<KitchenArtStageOneVerifier.Check> ValidateRuntime()
        {
            var checks = new List<KitchenArtStageOneVerifier.Check>();
            var runtime = UnityEngine.Object.FindObjectOfType<GameRuntime>();
            var settings = new SerializedObject(runtime);
            settings.FindProperty("levelConfig").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<LevelConfig>("Assets/_Game/Resources/Kitchen/Level_Sandbox.asset");
            settings.FindProperty("levelManager").objectReferenceValue = null;
            settings.ApplyModifiedPropertiesWithoutUndo();
            runtime.Boot();
            runtime.StartLevel();
            Add(checks, "four_runtime_stations", runtime.Cookers.Cookers.Count == 4,
                "All four devices spawn through existing definitions.");
            var preview = new GameObject("ArtGhostCheck").AddComponent<PlacementPreview>();
            foreach (var cooker in runtime.Cookers.Cookers)
            {
                Transform visual = cooker.View.transform.Find("Visual");
                Transform counter = visual.Find("ArtOffset/Counter");
                Bounds bounds = KitchenArtStageOne.BoundsOf(visual.gameObject);
                Add(checks, cooker.Definition.Id + "_model", counter != null &&
                    cooker.View.GetComponent<CookerItemDisplay>() != null &&
                    bounds.size.x <= 1.8f && bounds.size.z <= 1.8f && bounds.size.y > 0.9f,
                    "Actual model bounds: " + bounds.size);
                bool fronts = true;
                for (int index = 0; index < 4; index++)
                {
                    Vector2Int offset = CookerFacingUtility.Offset(cooker.Facing);
                    Vector3 expected = new Vector3(offset.x, 0f, offset.y);
                    fronts &= Vector3.Dot(-counter.forward, expected) > 0.99f;
                    fronts &= runtime.Cookers.TryRotateCooker(cooker);
                }
                Add(checks, cooker.Definition.Id + "_front", fronts,
                    "Imported cabinet front (-Z) agrees with all four actual work-cell directions.");
                preview.Show(cooker.Definition, Vector3.zero, Vector2Int.zero);
                preview.ShowWorkPosition(CookerFacing.East, 2f, true, false);
                Transform ghost = preview.transform.Find("PlacementGhost/ArtGhost");
                var sourceMeshes = visual.GetComponentsInChildren<MeshFilter>(true).Select(filter => filter.sharedMesh).ToArray();
                var ghostMeshes = ghost.GetComponentsInChildren<MeshFilter>(true).Select(filter => filter.sharedMesh).ToArray();
                bool geometry = sourceMeshes.Length == ghostMeshes.Length && sourceMeshes.All(ghostMeshes.Contains);
                preview.SetValid(false, Vector2Int.zero);
                bool tint = ghost.GetComponentsInChildren<Renderer>(true).All(renderer =>
                    renderer.sharedMaterials.All(material => material != null && material.color.r > material.color.g &&
                        material.color.a < 0.8f));
                bool noPhysics = !ghost.GetComponentsInChildren<Collider>(true).Any(collider => collider.enabled) &&
                    !ghost.GetComponentsInChildren<Behaviour>(true).Any(behaviour => behaviour.enabled);
                Add(checks, cooker.Definition.Id + "_ghost", geometry && tint && noPhysics &&
                    Vector3.Dot(preview.transform.Find("PlacementGhost").forward, Vector3.right) > 0.99f,
                    "Preview matches model meshes, rotates East, tints all material slots and has no active interactions.");
            }
            preview.Dispose();
            var floor = runtime.Grid.View.GetComponentInChildren<KitchenFloorView>();
            Add(checks, "runtime_floor", floor != null && floor.TileCount == runtime.Level.Width * runtime.Level.Height,
                "LevelConfig.FloorPrefab is used by the real runtime grid.");
            Transform gridRoot = settings.FindProperty("gridRoot").objectReferenceValue as Transform;
            Add(checks, "old_grid_preview_removed", gridRoot.GetComponentsInChildren<GridView>().Length == 1 &&
                !gridRoot.GetComponentsInChildren<Transform>().Any(child => child.name == "Preview"),
                "Only the live grid remains; the saved grey floor cannot overlap the new floor.");
            ValidateFloorSizing(runtime.Level, checks);
            var employee = runtime.Staff.Staff[0];
            var idle = AssetDatabase.LoadAllAssetsAtPath("Assets/ThirdParty/Quaternius/SushiRestaurant/Models/Panda.fbx")
                .OfType<AnimationClip>().First(clip => clip.name.EndsWith("|Idle"));
            idle.SampleAnimation(employee.View.transform.Find("Visual/Model/ArtOffset/Panda").gameObject, 0f);
            runtime.UI.RefreshAll();
            KitchenArtStageOneVerifier.Capture(runtime.WorldCamera, "Logs/art_stage2_kitchen.png", true);
            return checks;
        }

        private static void ValidateFloorSizing(LevelConfig source, List<KitchenArtStageOneVerifier.Check> checks)
        {
            LevelConfig level = UnityEngine.Object.Instantiate(source);
            var settings = new SerializedObject(level);
            settings.FindProperty("width").intValue = 7;
            settings.FindProperty("height").intValue = 3;
            settings.FindProperty("cellSize").floatValue = 1.5f;
            settings.FindProperty("mapCenter").vector3Value = new Vector3(4f, 0.75f, -3f);
            settings.FindProperty("groundHeight").floatValue = 1.5f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            var holder = new GameObject("ArtFloorCheck");
            GridView editorView = GridView.CreateForLevel(level, holder.transform);
            var grid = holder.AddComponent<GridManager>();
            grid.BuildGrid(level);
            var liveView = new GameObject("RuntimeGrid").AddComponent<GridView>();
            liveView.transform.SetParent(holder.transform, false);
            liveView.Build(grid, level);
            foreach (var view in new[] { editorView, liveView })
            {
                var layout = view.GetComponentInChildren<KitchenFloorView>();
                Bounds bounds = KitchenArtStageOne.BoundsOf(layout.transform.Find("TileLayout").gameObject);
                bool geometry = layout.TileCount == 21 && view.CellCount == 21 &&
                    Mathf.Abs(bounds.center.x - 4f) < 0.01f && Mathf.Abs(bounds.center.z + 3f) < 0.01f &&
                    Mathf.Abs(bounds.max.y - 2.25f) < 0.01f &&
                    Mathf.Abs(bounds.size.x - 10.76f) < 0.02f && Mathf.Abs(bounds.size.z - 4.76f) < 0.02f;
                var cell = view.transform.Find("Cells/Cell_0_0").GetComponent<Renderer>();
                bool clear = Mathf.Approximately(cell.sharedMaterial.color.a, 0f);
                view.SetHighlight(Vector2Int.zero, true, true);
                bool highlighted = view.IsHighlighted(Vector2Int.zero) && cell.sharedMaterial.color.a > 0.1f;
                view.ClearHighlights();
                bool restored = Mathf.Approximately(cell.sharedMaterial.color.a, 0f);
                Add(checks, view == editorView ? "floor_editor_metrics" : "floor_runtime_metrics",
                    geometry && clear && highlighted && restored,
                    "7x3 map at nonzero origin/height; floor top, tile count, extent and highlight restoration: " + bounds);
                view.BuildFromLevel(level);
                Add(checks, view == editorView ? "floor_editor_rebuild" : "floor_runtime_rebuild",
                    view.GetComponentsInChildren<KitchenFloorView>().Length == 1,
                    "Rebuilding leaves only one active art floor.");
            }
            holder.SetActive(false);
            if (Application.isPlaying) UnityEngine.Object.Destroy(holder);
            else UnityEngine.Object.DestroyImmediate(holder);
            UnityEngine.Object.DestroyImmediate(level);
        }

        private static void Add(List<KitchenArtStageOneVerifier.Check> checks, string name, bool passed, string detail)
        {
            checks.Add(new KitchenArtStageOneVerifier.Check { name = name, passed = passed, detail = detail });
            Debug.Log("[Art Stage Two] " + (passed ? "PASS " : "FAIL ") + name + ": " + detail);
        }

        public static void WriteReport(string path, List<KitchenArtStageOneVerifier.Check> checks)
        {
            File.WriteAllText(path, JsonUtility.ToJson(new KitchenArtStageOneVerifier.Report
            { unityVersion = Application.unityVersion, checks = checks.ToArray() }, true));
            if (checks.Any(check => !check.passed)) throw new InvalidOperationException("Art stage two checks failed: " + path);
        }
    }
}
