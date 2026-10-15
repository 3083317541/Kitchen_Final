using System.Collections.Generic;
using Kitchen.Data;
using Kitchen.Grid;
using Kitchen.View;
using UnityEditor;
using UnityEngine;

namespace Kitchen.EditorTools
{
    /// <summary>
    /// Authors a visible preview of the kitchen into the scene at build time.
    ///
    /// Without this the scene file holds only empty roots, because the floor, grid, cookers and chefs
    /// are all created at runtime. That makes the Scene view look empty and makes the level impossible
    /// to inspect or to dress with art.
    ///
    /// The preview instantiates the <b>generated prefabs</b> and keeps the prefab link, so editing a
    /// prefab immediately updates the Scene view and the running game. Prefabs live under a container
    /// named <see cref="PreviewContainerName"/>, which the runtime deletes as it builds its own
    /// objects, so the preview never becomes duplicated state.
    /// </summary>
    public static class KitchenScenePreview
    {
        /// <summary>Container the preview objects are authored under. The runtime deletes it.</summary>
        public const string PreviewContainerName = "Preview";

        /// <summary>Populates the world roots with a preview of the level.</summary>
        public static void Build(LevelConfig level, Transform gridRoot, Transform cookerRoot, Transform staffRoot)
        {
            if (level == null)
            {
                Debug.LogWarning("[KitchenScenePreview] No level was supplied, so no preview was authored.");
                return;
            }

            // The prefab links are refreshed first so the preview instances connect to the prefabs
            // that were just generated.
            KitchenPrefabGenerator.AssignGridViewPrefabs(
                AssetDatabase.LoadAssetAtPath<GameObject>(KitchenPrefabGenerator.FloorPrefabPath),
                AssetDatabase.LoadAssetAtPath<GameObject>(KitchenPrefabGenerator.CellPrefabPath));

            KitchenGridMath math = KitchenGridMath.FromLevel(level);

            BuildGrid(level, gridRoot);
            BuildCookers(level, math, cookerRoot);
            BuildStaff(level, math, staffRoot);
        }

        // ------------------------------------------------------------------ grid and floor

        private static void BuildGrid(LevelConfig level, Transform gridRoot)
        {
            if (gridRoot == null)
            {
                return;
            }

            Transform container = CreateContainer(gridRoot);
            GridView view = GridView.CreateForLevel(level, container);

            Debug.Log($"[KitchenScenePreview] Authored the floor and {level.Width * level.Height} grid cell(s)" +
                $"{(view != null ? string.Empty : " (the grid view could not be created)")}.");
        }

        // ------------------------------------------------------------------ cookers

        private static void BuildCookers(LevelConfig level, KitchenGridMath math, Transform cookerRoot)
        {
            if (cookerRoot == null)
            {
                return;
            }

            Transform container = CreateContainer(cookerRoot);
            IReadOnlyList<CookerPlacement> placements = level.StartingCookers;
            int built = 0;
            int linked = 0;

            for (int i = 0; i < placements.Count; i++)
            {
                CookerPlacement placement = placements[i];
                if (!placement.IsValid)
                {
                    continue;
                }

                Vector3 world = math.GridToWorld(placement.GridPosition);
                GameObject prefab = placement.Definition.ViewPrefab;

                // Instantiating through PrefabUtility keeps the prefab connection, so an edit to the
                // prefab is reflected here immediately.
                GameObject instance = prefab != null
                    ? PrefabUtility.InstantiatePrefab(prefab, container) as GameObject
                    : ViewFactory.CreateCookerView(placement.Definition);

                if (instance == null)
                {
                    continue;
                }

                if (instance.transform.parent != container)
                {
                    instance.transform.SetParent(container, false);
                }

                instance.transform.position = world;
                instance.name = $"Cooker_{i + 1}_{placement.Definition.Id}";

                if (prefab != null)
                {
                    linked++;
                }

                CookerView view = instance.GetComponent<CookerView>();
                if (view != null)
                {
                    view.Configure(placement.Definition);
                    view.EnsureBuilt();
                }

                instance.SetActive(true);
                built++;
            }

            Debug.Log($"[KitchenScenePreview] Authored {built} cooker preview(s), {linked} linked to a prefab.");
        }

        // ------------------------------------------------------------------ staff

        private static void BuildStaff(LevelConfig level, KitchenGridMath math, Transform staffRoot)
        {
            if (staffRoot == null)
            {
                return;
            }

            Transform container = CreateContainer(staffRoot);
            Vector2Int centre = math.CentreCell;
            IReadOnlyList<Vector2Int> offsets = level.StaffSpawnOffsets;
            GameObject prefab = level.StaffPrefab;
            int built = 0;

            for (int i = 0; i < level.StaffCount; i++)
            {
                Vector2Int offset = i < offsets.Count ? offsets[i] : Vector2Int.zero;
                Vector3 world = math.GridToWorld(centre + offset);

                GameObject instance = prefab != null
                    ? PrefabUtility.InstantiatePrefab(prefab, container) as GameObject
                    : ViewFactory.CreateStaffView();

                if (instance == null)
                {
                    continue;
                }

                if (instance.transform.parent != container)
                {
                    instance.transform.SetParent(container, false);
                }

                instance.transform.position = world;
                instance.name = $"Staff_{i + 1}";

                StaffView view = instance.GetComponent<StaffView>();
                if (view != null)
                {
                    view.Configure(Whitebox.StaffBodyColor, Whitebox.StaffHeadColor, Whitebox.StaffSelectedColor);
                    view.EnsureBuilt();
                    view.ApplyWorldPosition(world);
                }

                instance.SetActive(true);
                built++;
            }

            Debug.Log($"[KitchenScenePreview] Authored {built} chef preview(s), " +
                $"{(prefab != null ? "linked to a prefab" : "built from primitives")}.");
        }

        // ------------------------------------------------------------------ helpers

        private static Transform CreateContainer(Transform parent)
        {
            GameObject go = new GameObject(PreviewContainerName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            return go.transform;
        }
    }
}
