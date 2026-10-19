using Kitchen.Core;
using Kitchen.Data;
using Kitchen.Cookers;
using UnityEngine;

namespace Kitchen.View
{
    /// <summary>
    /// Presentation of one cooker: its model, collider, click target and visual state.
    ///
    /// It deliberately knows nothing about recipes, prices or timing. All of that lives in
    /// <see cref="Kitchen.Cookers.CookerRuntime"/> and the managers, so replacing the whitebox with
    /// real art can never change the game rules.
    /// </summary>
    [DisallowMultipleComponent]
    public class CookerView : MonoBehaviour
    {
        [Header("Art replacement")]
        [Tooltip("When set, this prefab is instantiated instead of the generated whitebox geometry.")]
        [SerializeField] private GameObject modelPrefab;

        [SerializeField] private Material bodyMaterial;
        [SerializeField] private Material accentMaterial;

        [Header("Fallback appearance")]
        [Tooltip("Used only when the CookerDefinition leaves its colour at the default value.")]
        [SerializeField] private Color bodyColor = new Color(0.72f, 0.72f, 0.78f);
        [SerializeField] private Color accentColor = new Color(0.35f, 0.62f, 0.85f);
        [SerializeField] private Color inactiveAccentColor = new Color(0.45f, 0.45f, 0.48f);
        [SerializeField] private Vector3 bodySize = new Vector3(1.6f, 0.42f, 1.6f);

        /// <summary>Name of the generated model container, also used to adopt it after instantiation.</summary>
        private const string VisualRootName = "Visual";

        /// <summary>Largest horizontal size a cooker may occupy inside a single cell.</summary>
        public const float MaxFootprint = 1.8f;

        private Transform visualRoot;
        private Quaternion visualBaseRotation = Quaternion.identity;
        private Renderer accentRenderer;
        private Renderer bodyRenderer;
        private Material activeAccentInstance;
        private Material inactiveAccentInstance;
        private bool isSelected;
        private bool isActive;
        private CookerDefinition definition;
        private WorkPositionIndicator workPositionIndicator;

        public bool HasWorkPositionIndicator => workPositionIndicator != null && workPositionIndicator.IsVisible;

        /// <summary>Configuration this view was built from. Null until Configure is called.</summary>
        public CookerDefinition Definition => definition;

        /// <summary>Runtime state this view is displaying.</summary>
        public Cookers.CookerRuntime Runtime { get; private set; }

        /// <summary>
        /// Applies a definition. Values the prefab already carries win over the definition, so the
        /// scene can override a colour for a specific instance.
        /// </summary>
        public void Configure(CookerDefinition cookerDefinition)
        {
            definition = cookerDefinition;
            if (definition == null)
            {
                return;
            }

            if (bodyMaterial == null)
            {
                bodyMaterial = definition.BodyMaterial;
            }

            if (accentMaterial == null)
            {
                accentMaterial = definition.AccentMaterial;
            }

            if (bodyColor == DefaultBodyColor)
            {
                bodyColor = definition.BodyColor;
            }

            if (accentColor == DefaultAccentColor)
            {
                accentColor = definition.AccentColor;
            }

            if (bodySize == DefaultBodySize)
            {
                bodySize = definition.BodySize;
            }

            // `modelPrefab` is a separate nested art model, for example a skinned mesh that should sit
            // inside the whitebox collider. It must never default to the definition's view prefab:
            // that prefab *is* this view, so nesting it would clone the whole cooker inside itself.
            // The view prefab is applied by ViewFactory, not here.
        }

        /// <summary>A colour equal to this value means "inherit from the definition".</summary>
        public static Color DefaultBodyColor => new Color(0.72f, 0.72f, 0.78f);

        /// <summary>A colour equal to this value means "inherit from the definition".</summary>
        public static Color DefaultAccentColor => new Color(0.35f, 0.62f, 0.85f);

        /// <summary>A size equal to this value means "inherit from the definition".</summary>
        public static Vector3 DefaultBodySize => new Vector3(1.6f, 0.42f, 1.6f);

        /// <summary>Links this view to its runtime state and refreshes the look.</summary>
        public void Bind(Cookers.CookerRuntime runtime)
        {
            Runtime = runtime;
            if (runtime == null)
            {
                return;
            }

            if (definition == null)
            {
                Configure(runtime.Definition);
            }

            EnsureBuilt();
            if (workPositionIndicator == null)
            {
                float cellSize = runtime.Owner != null ? runtime.Owner.Grid.CellSize : 2f;
                workPositionIndicator = new WorkPositionIndicator(transform, cellSize, bodySize.y + 0.30f);
            }
            ApplyState();
        }

        /// <summary>
        /// Builds the visual hierarchy once.
        ///
        /// `visualRoot` is not serialised, so a freshly instantiated prefab has it null even though
        /// the prefab already contains the whole model. The existing child is adopted instead of a
        /// second model being built, which would otherwise leave the view with two of everything.
        /// </summary>
        public void EnsureBuilt()
        {
            if (visualRoot == null)
            {
                Transform existing = transform.Find(VisualRootName);
                if (existing != null)
                {
                    visualRoot = existing;
                    visualBaseRotation = visualRoot.localRotation;
                    visualRoot.gameObject.name = VisualRootName;
                }
            }

            if (visualRoot != null)
            {
                EnsureClickCollider();
                return;
            }

            visualRoot = new GameObject(VisualRootName).transform;
            visualRoot.SetParent(transform, false);

            if (modelPrefab != null)
            {
                // Guard against a prefab referencing itself, which would nest a copy of the whole
                // cooker inside itself every time it is instantiated. That happens when the art hooks
                // point at the view prefab instead of a separate model.
                if (IsSelfReference(modelPrefab))
                {
                    modelPrefab = null;
                }
            }

            if (modelPrefab != null)
            {
                GameObject model = Instantiate(modelPrefab, visualRoot);
                model.name = "ArtModel";
                GameLayers.SetLayerRecursively(model, gameObject.layer);
                if (accentRenderer == null)
                {
                    accentRenderer = FindRendererWithTag(model, "Accent");
                }

                if (bodyRenderer == null)
                {
                    bodyRenderer = FindRendererWithTag(model, "Body");
                }
            }
            else
            {
                BuildWhitebox();
            }

            EnsureClickCollider();
            RefreshMaterials();
        }

        /// <summary>
        /// Re-resolves the shared materials from the asset library. A prefab saved by an older build
        /// can carry a null material reference, which shows as Unity's magenta error shader; calling
        /// this repairs such an instance without regenerating anything.
        /// </summary>
        public void RefreshMaterials()
        {
            if (visualRoot == null)
            {
                return;
            }

            MeshRenderer[] renderers = visualRoot.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (renderer.sharedMaterial != null && renderer.sharedMaterial.shader != null &&
                    renderer.sharedMaterial.shader.name != "Hidden/InternalErrorShader")
                {
                    continue;
                }

                // Fall back to the body colour so the object stays visible instead of magenta.
                renderer.sharedMaterial = bodyMaterial != null
                    ? bodyMaterial
                    : Whitebox.SolidMaterial(bodyColor, "CookerBody");
            }
        }

        private void BuildWhitebox()
        {
            int layer = gameObject.layer;

            // A worktop on legs reads as real volume. The footprint is clamped to one cell so the
            // art can never decide how much grid a cooker occupies.
            Vector3 size = new Vector3(
                Mathf.Min(MaxFootprint, Mathf.Max(0.2f, bodySize.x)),
                Mathf.Clamp(bodySize.y, 0.25f, 1.2f),
                Mathf.Min(MaxFootprint, Mathf.Max(0.2f, bodySize.z)));

            Material body = bodyRendererMaterial();
            Material frame = Whitebox.SolidMaterial(Whitebox.DarkMetalColor, "CookerFrame");

            // Worktop.
            AddBox("Worktop", new Vector3(0f, size.y - 0.05f, 0f), new Vector3(size.x, 0.1f, size.z), body);

            // Plinth, slightly inset so the top overhangs it.
            AddBox("Plinth", new Vector3(0f, 0.12f, 0f), new Vector3(size.x * 0.86f, 0.24f, size.z * 0.86f), frame);

            // Four legs.
            const float legInset = 0.18f;
            float lx = size.x * 0.5f - legInset;
            float lz = size.z * 0.5f - legInset;
            AddBox("LegFrontLeft", new Vector3(-lx, 0.26f, lz), new Vector3(0.1f, 0.5f, 0.1f), frame);
            AddBox("LegFrontRight", new Vector3(lx, 0.26f, lz), new Vector3(0.1f, 0.5f, 0.1f), frame);
            AddBox("LegBackLeft", new Vector3(-lx, 0.26f, -lz), new Vector3(0.1f, 0.5f, 0.1f), frame);
            AddBox("LegBackRight", new Vector3(lx, 0.26f, -lz), new Vector3(0.1f, 0.5f, 0.1f), frame);

            // Accent band on the front edge, which also doubles as the active / inactive indicator.
            Material accent = accentMaterial != null ? accentMaterial : Whitebox.SolidMaterial(accentColor, "CookerAccent");
            GameObject accentGo = AddBox("Accent", new Vector3(0f, size.y - 0.14f, size.z * 0.5f),
                new Vector3(size.x * 0.94f, 0.09f, 0.05f), accent);
            accentRenderer = accentGo.GetComponent<Renderer>();

            activeAccentInstance = accent;
            inactiveAccentInstance = Whitebox.SolidMaterial(inactiveAccentColor, "CookerAccentOff");

            BuildKindDecoration(size, accent);

            GameLayers.SetLayerRecursively(visualRoot.gameObject, layer);
        }

        private Material bodyRendererMaterial()
        {
            return bodyMaterial != null ? bodyMaterial : Whitebox.SolidMaterial(bodyColor, "CookerBody");
        }

        private GameObject AddBox(string objectName, Vector3 localPosition, Vector3 localScale, Material material)
        {
            return BuildPart(PrimitiveType.Cube, objectName, localPosition, localScale, Quaternion.identity, material);
        }

        private GameObject AddCylinder(string objectName, Vector3 localPosition, Vector3 localScale, Material material, Vector3 eulerAngles)
        {
            return BuildPart(PrimitiveType.Cylinder, objectName, localPosition, localScale, Quaternion.Euler(eulerAngles), material);
        }

        private GameObject BuildPart(
            PrimitiveType type,
            string objectName,
            Vector3 localPosition,
            Vector3 localScale,
            Quaternion localRotation,
            Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = objectName;
            go.transform.SetParent(visualRoot, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            go.transform.localScale = localScale;

            Collider collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }

            return go;
        }

        /// <summary>
        /// Gives each cooker a recognisable 3D silhouette: a board and blade, a pan with a handle,
        /// an assembly turntable with dishes, or a washer cabinet with a porthole.
        /// </summary>
        private void BuildKindDecoration(Vector3 size, Material accent)
        {
            if (definition == null)
            {
                return;
            }

            float top = size.y;
            Material dark = Whitebox.SolidMaterial(Whitebox.DarkMetalColor, "CookerDetail");
            Material metal = Whitebox.SolidMaterial(Whitebox.MetalColor, "CookerMetal");

            switch (definition.Kind)
            {
                case CookerKind.ChoppingStation:
                    AddBox("ChoppingBoard", new Vector3(0f, top + 0.03f, 0f),
                        new Vector3(size.x * 0.62f, 0.06f, size.z * 0.46f),
                        Whitebox.SolidMaterial(Whitebox.DetailBoardColor, "Board"));
                    AddBox("Blade", new Vector3(size.x * 0.2f, top + 0.14f, 0f),
                        new Vector3(0.03f, 0.16f, size.z * 0.34f), metal);
                    AddBox("BladeHandle", new Vector3(size.x * 0.2f, top + 0.14f, -size.z * 0.28f),
                        new Vector3(0.06f, 0.06f, 0.2f), dark);
                    break;

                case CookerKind.FryingPan:
                    AddCylinder("Pan", new Vector3(0f, top + 0.05f, 0f),
                        new Vector3(0.42f, 0.05f, 0.42f), dark, Vector3.zero);
                    AddCylinder("PanRim", new Vector3(0f, top + 0.08f, 0f),
                        new Vector3(0.46f, 0.02f, 0.46f), metal, Vector3.zero);
                    AddBox("PanHandle", new Vector3(0f, top + 0.06f, 0.52f),
                        new Vector3(0.07f, 0.05f, 0.42f), dark);
                    break;

                case CookerKind.AssemblyTable:
                    AddCylinder("Turntable", new Vector3(0f, top + 0.04f, 0f),
                        new Vector3(0.62f, 0.04f, 0.62f), metal, Vector3.zero);
                    AddCylinder("DishA", new Vector3(-0.26f, top + 0.13f, 0.24f),
                        new Vector3(0.2f, 0.06f, 0.2f), accent, Vector3.zero);
                    AddCylinder("DishB", new Vector3(0.28f, top + 0.13f, 0.06f),
                        new Vector3(0.2f, 0.06f, 0.2f), accent, Vector3.zero);
                    AddCylinder("DishC", new Vector3(-0.06f, top + 0.13f, -0.3f),
                        new Vector3(0.2f, 0.06f, 0.2f), accent, Vector3.zero);
                    break;

                case CookerKind.DishWasher:
                    // A tall cabinet makes this station obviously different in silhouette.
                    AddBox("Cabinet", new Vector3(0f, top + 0.3f, 0f),
                        new Vector3(size.x * 0.94f, 0.6f, size.z * 0.94f), bodyRendererMaterial());
                    AddCylinder("Porthole", new Vector3(0f, top + 0.32f, size.z * 0.48f),
                        new Vector3(0.24f, 0.02f, 0.24f),
                        Whitebox.SolidMaterial(Whitebox.DetailGlassColor, "Glass"),
                        new Vector3(90f, 0f, 0f));
                    AddBox("Vent", new Vector3(0f, top + 0.63f, 0f),
                        new Vector3(size.x * 0.5f, 0.04f, size.z * 0.5f), dark);
                    break;
            }
        }

        /// <summary>The cooker is the click target, so the root always carries one collider.</summary>
        private void EnsureClickCollider()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (box == null)
            {
                box = gameObject.AddComponent<BoxCollider>();
            }

            // Size is derived from the grid cell, never from the model's scale.
            float height = Mathf.Max(0.6f, bodySize.y + 0.4f);
            box.size = new Vector3(MaxFootprint, height, MaxFootprint);
            box.center = new Vector3(0f, height * 0.5f, 0f);
        }

        /// <summary>
        /// True when a configured model prefab is this very object, which would nest a copy of the
        /// view inside itself. Comparing asset paths is enough and avoids touching the prefab API.
        /// </summary>
        private bool IsSelfReference(GameObject candidate)
        {
            if (candidate == null)
            {
                return false;
            }

#if UNITY_EDITOR
            string candidatePath = UnityEditor.AssetDatabase.GetAssetPath(candidate);
            string ownPath = UnityEditor.AssetDatabase.GetAssetPath(gameObject);
            return !string.IsNullOrEmpty(candidatePath) && candidatePath == ownPath;
#else
            return false;
#endif
        }

        private static Renderer FindRendererWithTag(GameObject root, string nameContains)        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i].name.IndexOf(nameContains, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return renderers[i];
                }
            }

            return renderers.Length > 0 ? renderers[0] : null;
        }

        /// <summary>Refreshes the active / selected look from the runtime state.</summary>
        public void ApplyState()
        {
            bool active = Runtime != null && (Runtime.IsActive || Runtime.IsHeating);
            bool selected = isSelected;

            if (accentRenderer != null)
            {
                if (active)
                {
                    accentRenderer.sharedMaterial = activeAccentInstance != null ? activeAccentInstance : accentRenderer.sharedMaterial;
                }
                else
                {
                    accentRenderer.sharedMaterial = inactiveAccentInstance != null ? inactiveAccentInstance : accentRenderer.sharedMaterial;
                }
            }

            // Selection is shown with a slight lift so it is visible even on a busy grid.
            if (visualRoot != null)
            {
                visualRoot.localPosition = new Vector3(0f, selected ? 0.12f : 0f, 0f);
                if (Runtime != null)
                {
                    visualRoot.localRotation = Quaternion.Euler(0f, (int)Runtime.Facing * 90f, 0f) * visualBaseRotation;
                }
            }

            if (Runtime != null && Runtime.Owner != null && workPositionIndicator != null)
            {
                bool open = Runtime.Owner.Cookers.IsWorkPositionOpen(Runtime.WorkPosition);
                bool occupied = Runtime.Owner.Grid.HasStaff(Runtime.WorkPosition);
                workPositionIndicator.Refresh(Runtime.Facing, open, occupied, selected);
            }
        }

        /// <summary>Called by CookerManager when the activation state changes.</summary>
        public void SetActiveState(bool active)
        {
            isActive = active;
            ApplyState();
        }

        /// <summary>Called by CookerManager when the selection changes.</summary>
        public void SetSelected(bool selected)
        {
            isSelected = selected;
            ApplyState();
        }

        /// <summary>Teleports the model, used after the cooker is moved between cells.</summary>
        public void SnapTo(Vector3 worldPosition)
        {
            transform.position = worldPosition;
        }

        /// <summary>
        /// World space bounds of the whole model. The acceptance suite uses it to prove the cooker
        /// stays inside its single grid cell whatever the art's scale happens to be.
        /// </summary>
        public bool TryGetWorldBounds(out Bounds bounds)
        {
            bounds = new Bounds(transform.position, Vector3.zero);

            if (visualRoot == null)
            {
                return false;
            }

            MeshRenderer[] renderers = visualRoot.GetComponentsInChildren<MeshRenderer>(true);
            bool any = false;

            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                // Ground decals are flat overlays, not part of the model's volume.
                if (renderer.sharedMaterial != null && renderer.sharedMaterial.shader != null &&
                    renderer.sharedMaterial.shader.name == "Sprites/Default")
                {
                    continue;
                }

                if (!any)
                {
                    bounds = renderer.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return any;
        }

        /// <summary>Destroys this view's GameObject.</summary>
        public void Dispose()
        {
            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
            else
            {
                DestroyImmediate(gameObject);
            }
        }
    }
}
