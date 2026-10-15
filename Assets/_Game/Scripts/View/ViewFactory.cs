using Kitchen.Data;
using UnityEngine;

namespace Kitchen.View
{
    /// <summary>
    /// Creates presentation objects without knowing anything about the domain.
    ///
    /// Every view prefers the art prefab configured on its definition or asset, and falls back to
    /// primitive whitebox geometry when that field is empty. This is the single seam where real art
    /// enters the demo: assign a prefab and material on the matching ScriptableObject and nothing else
    /// changes.
    ///
    /// The fallback is not a placeholder: it builds the same model the generated prefabs contain, so
    /// the demo runs identically whether or not prefabs exist.
    /// </summary>
    public static class ViewFactory
    {
        /// <summary>
        /// Creates an inactive cooker view. The caller parents, positions and binds it.
        /// Uses the definition's art prefab when one is assigned.
        /// </summary>
        public static GameObject CreateCookerView(CookerDefinition definition)
        {
            GameObject prefab = definition != null ? definition.ViewPrefab : null;

            if (prefab != null)
            {
                GameObject instance = Object.Instantiate(prefab);
                instance.name = "CookerView";
                instance.SetActive(false);

                CookerView prefabView = instance.GetComponent<CookerView>();
                if (prefabView == null)
                {
                    prefabView = instance.AddComponent<CookerView>();
                }

                prefabView.Configure(definition);
                GameLayers.SetLayerRecursively(instance, GameLayers.Interactable);
                return instance;
            }

            return BuildWhiteboxCookerView(definition);
        }

        /// <summary>
        /// Creates a cooker view from primitives. Also used by the prefab generator, so the generated
        /// prefab and the runtime fallback can never drift apart.
        /// </summary>
        public static GameObject BuildWhiteboxCookerView(CookerDefinition definition)
        {
            GameObject go = new GameObject("CookerView");
            go.SetActive(false);

            CookerView view = go.AddComponent<CookerView>();
            if (view != null && definition != null)
            {
                view.Configure(definition);
            }

            GameLayers.SetLayerRecursively(go, GameLayers.Interactable);
            return go;
        }

        /// <summary>
        /// Creates an inactive staff view. The caller parents, positions and binds it.
        /// Uses <paramref name="prefab"/> when one is supplied.
        /// </summary>
        public static GameObject CreateStaffView(GameObject prefab = null)
        {
            if (prefab != null)
            {
                GameObject instance = Object.Instantiate(prefab);
                instance.name = "StaffView";
                instance.SetActive(false);

                StaffView prefabView = instance.GetComponent<StaffView>();
                if (prefabView == null)
                {
                    prefabView = instance.AddComponent<StaffView>();
                }

                prefabView.Configure(Whitebox.StaffBodyColor, Whitebox.StaffHeadColor, Whitebox.StaffSelectedColor);
                GameLayers.SetLayerRecursively(instance, GameLayers.Interactable);
                return instance;
            }

            return BuildWhiteboxStaffView();
        }

        /// <summary>Creates a staff view from primitives, shared with the prefab generator.</summary>
        public static GameObject BuildWhiteboxStaffView()
        {
            GameObject go = new GameObject("StaffView");
            go.SetActive(false);

            StaffView view = go.AddComponent<StaffView>();
            if (view != null)
            {
                view.Configure(Whitebox.StaffBodyColor, Whitebox.StaffHeadColor, Whitebox.StaffSelectedColor);
            }

            GameLayers.SetLayerRecursively(go, GameLayers.Interactable);
            return go;
        }

        /// <summary>Creates the grid view. The caller parents it and calls Build.</summary>
        public static GridView CreateGridView()
        {
            GameObject go = new GameObject("GridView");
            GridView view = go.AddComponent<GridView>();
            GameLayers.SetLayerRecursively(go, GameLayers.Ground);
            return view;
        }

        /// <summary>Creates the build placement ghost.</summary>
        public static PlacementPreview CreatePlacementPreview(Transform parent)
        {
            GameObject go = new GameObject("PlacementPreview");
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }

            PlacementPreview preview = go.AddComponent<PlacementPreview>();
            GameLayers.SetLayerRecursively(go, GameLayers.Ground);
            preview.Hide();
            return preview;
        }

        /// <summary>Creates a world space token for an item, used to show stock above a cooker.</summary>
        public static GameObject CreateItemToken(ItemDefinition item, Transform parent, string objectName)
        {
            return Whitebox.BuildItemToken(item, parent, objectName, GameLayers.Ground);
        }

        /// <summary>Instantiates an item's art prefab when it has one, otherwise returns null.</summary>
        public static GameObject TryCreateItemFromPrefab(ItemDefinition item, Transform parent, string objectName)
        {
            if (item == null || item.ViewPrefab == null)
            {
                return null;
            }

            GameObject instance = Object.Instantiate(item.ViewPrefab, parent);
            instance.name = objectName;
            GameLayers.SetLayerRecursively(instance, GameLayers.Ground);
            return instance;
        }
    }
}
