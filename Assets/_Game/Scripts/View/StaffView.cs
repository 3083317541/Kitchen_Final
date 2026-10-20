using Kitchen.Entities;
using UnityEngine;

namespace Kitchen.View
{
    /// <summary>
    /// Presentation of one employee: its model, the selection marker and the smoothing of its
    /// movement. It holds no gameplay state beyond a reference to
    /// <see cref="StaffRuntime"/>, so replacing the model or its animation cannot affect the rules.
    /// </summary>
    [DisallowMultipleComponent]
    public class StaffView : MonoBehaviour
    {
        [Header("Art replacement")]
        [Tooltip("When set, this prefab is instantiated instead of the generated whitebox model.")]
        [SerializeField] private GameObject modelPrefab;
        [SerializeField] private Material bodyMaterial;
        [SerializeField] private Material headMaterial;
        [SerializeField] private Material selectionMaterial;

        [Header("Fallback appearance")]
        [SerializeField] private Color bodyColor = new Color(0.85f, 0.72f, 0.45f);
        [SerializeField] private Color headColor = new Color(0.94f, 0.86f, 0.72f);
        [SerializeField] private Color selectionColor = new Color(0.30f, 0.95f, 0.60f, 0.45f);
        [SerializeField] private float bodyHeight = 0.9f;
        [SerializeField] private float bodyRadius = 0.26f;
        [SerializeField] private float selectionRingAlpha = 0.45f;

        [Header("Authoring notes")]
        [Tooltip("Colours equal to the static defaults mean 'inherit from the shared whitebox palette'.")]
        [SerializeField] private bool reservedForFutureUse;

        private Transform visualRoot;
        private Transform modelRoot;
        private GameObject selectionRing;
        private Animator animator;
        private bool isSelected;

        /// <summary>Runtime state this view is displaying.</summary>
        public StaffRuntime Runtime { get; private set; }

        /// <summary>Optional animator found on an art model, driven by simple state names.</summary>
        public Animator Animator => animator;

        /// <summary>The transform that follows the smoothed world position.</summary>
        public Transform ModelRoot => modelRoot != null ? modelRoot : transform;

        /// <summary>Links this view to its runtime state and builds the model once.</summary>
        public void Bind(StaffRuntime runtime)
        {
            Runtime = runtime;
            EnsureBuilt();
            ApplyWorldPosition(runtime != null ? runtime.WorldPosition : transform.position);
            SetSelected(runtime != null && runtime.IsSelected);
        }

        /// <summary>Applies the shared whitebox look. Called by the view factory at creation time.</summary>
        public void Configure(Color fallbackBody, Color fallbackHead, Color fallbackSelection)
        {
            bodyColor = fallbackBody;
            headColor = fallbackHead;
            selectionColor = new Color(fallbackSelection.r, fallbackSelection.g, fallbackSelection.b, selectionRingAlpha);
        }

        /// <summary>
        /// True when a configured model prefab is this very object, which would nest a copy of the
        /// view inside itself.
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

        /// <summary>Name of the generated model container, also used to adopt it after instantiation.</summary>
        private const string VisualRootName = "Visual";

        /// <summary>Name of the generated model root inside the visual container.</summary>
        private const string ModelRootName = "Model";

        /// <summary>
        /// Re-finds the model container after instantiation. `modelRoot` is not serialised, so it is
        /// null on a fresh instance even though the prefab already contains the model.
        /// </summary>
        private void RecoverModelRoot()
        {
            if (modelRoot != null || visualRoot == null)
            {
                return;
            }

            Transform existing = visualRoot.Find(ModelRootName);
            if (existing != null)
            {
                modelRoot = existing;
            }
        }

        /// <summary>
        /// Re-resolves the shared materials from the asset library, repairing an instance whose
        /// material reference came out null and would otherwise be drawn magenta.
        /// </summary>
        public void RefreshMaterials()
        {
            if (visualRoot == null)
            {
                return;
            }

            Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (renderer.sharedMaterial != null && renderer.sharedMaterial.shader != null &&
                    renderer.sharedMaterial.shader.name != "Hidden/InternalErrorShader")
                {
                    continue;
                }

                renderer.sharedMaterial = bodyMaterial != null
                    ? bodyMaterial
                    : Whitebox.SolidMaterial(bodyColor, "StaffBody");
            }
        }

        /// <summary>Builds the visual hierarchy once.</summary>
        public void EnsureBuilt()
        {
            // `visualRoot` is not serialised, so an instantiated prefab has it null even though the
            // model is already there. The existing container is adopted so the model is not duplicated.
            if (visualRoot == null)
            {
                Transform existing = transform.Find(VisualRootName);
                if (existing != null)
                {
                    visualRoot = existing;
                    visualRoot.gameObject.name = VisualRootName;
                }
            }

            if (visualRoot == null)
            {
                visualRoot = new GameObject(VisualRootName).transform;
                visualRoot.SetParent(transform, false);

                if (modelPrefab != null && IsSelfReference(modelPrefab))
                {
                    modelPrefab = null;
                }

                if (modelPrefab != null)
                {
                    GameObject model = Instantiate(modelPrefab, visualRoot);
                    model.name = "ArtModel";
                    modelRoot = model.transform;
                    animator = model.GetComponentInChildren<Animator>();
                    GameLayers.SetLayerRecursively(model, gameObject.layer);
                }
                else
                {
                    BuildWhitebox();
                }
            }
            else
            {
                RecoverModelRoot();
                if (animator == null && modelRoot != null)
                {
                    animator = modelRoot.GetComponentInChildren<Animator>();
                }
            }

            // Both the freshly built and the adopted path need these.
            if (selectionRing == null)
            {
                Transform existingRing = visualRoot.Find("SelectionRing");
                if (existingRing != null)
                {
                    selectionRing = existingRing.gameObject;
                }
                else
                {
                    BuildSelectionRing();
                }
            }

            EnsureClickCollider();
            RefreshMaterials();
        }

        private void BuildWhitebox()
        {
            int layer = gameObject.layer;

            modelRoot = new GameObject(ModelRootName).transform;
            modelRoot.SetParent(visualRoot, false);

            Material body = bodyMaterial != null ? bodyMaterial : Whitebox.SolidMaterial(bodyColor, "StaffBody");
            Material head = headMaterial != null ? headMaterial : Whitebox.SolidMaterial(headColor, "StaffHead");
            Material dark = Whitebox.SolidMaterial(Whitebox.DarkMetalColor, "StaffDetail");

            float headY = bodyHeight + bodyRadius * 0.85f;

            // Legs give the model a readable stance from a raised camera.
            AddPart(PrimitiveType.Capsule, "LegLeft", new Vector3(-bodyRadius * 0.55f, bodyRadius * 0.7f, 0f),
                new Vector3(bodyRadius * 0.7f, bodyRadius * 0.7f, bodyRadius * 0.7f), dark);
            AddPart(PrimitiveType.Capsule, "LegRight", new Vector3(bodyRadius * 0.55f, bodyRadius * 0.7f, 0f),
                new Vector3(bodyRadius * 0.7f, bodyRadius * 0.7f, bodyRadius * 0.7f), dark);

            // Torso: a capsule whose height is decided here, never by the art's scale.
            AddPart(PrimitiveType.Capsule, "Torso", new Vector3(0f, bodyHeight * 0.5f + bodyRadius * 0.5f, 0f),
                new Vector3(bodyRadius * 2f, bodyHeight * 0.5f, bodyRadius * 2f), body);

            // Arms.
            AddPart(PrimitiveType.Capsule, "ArmLeft", new Vector3(-bodyRadius * 1.25f, bodyHeight * 0.62f, 0f),
                new Vector3(bodyRadius * 0.55f, bodyHeight * 0.34f, bodyRadius * 0.55f), body);
            AddPart(PrimitiveType.Capsule, "ArmRight", new Vector3(bodyRadius * 1.25f, bodyHeight * 0.62f, 0f),
                new Vector3(bodyRadius * 0.55f, bodyHeight * 0.34f, bodyRadius * 0.55f), body);

            // Head plus a chef hat, which also makes the facing direction obvious.
            AddPart(PrimitiveType.Sphere, "Head", new Vector3(0f, headY, 0f),
                Vector3.one * (bodyRadius * 1.75f), head);
            AddPart(PrimitiveType.Cylinder, "Hat", new Vector3(0f, headY + bodyRadius * 1.35f, 0f),
                new Vector3(bodyRadius * 1.7f, bodyRadius * 0.45f, bodyRadius * 1.7f), head);
            AddPart(PrimitiveType.Sphere, "HatTop", new Vector3(0f, headY + bodyRadius * 1.95f, 0f),
                Vector3.one * (bodyRadius * 0.85f), head);

            // A short nose marks the front of the model.
            AddPart(PrimitiveType.Cube, "Nose", new Vector3(0f, headY, bodyRadius * 0.85f),
                new Vector3(bodyRadius * 0.4f, bodyRadius * 0.4f, bodyRadius * 0.55f), head);

            GameLayers.SetLayerRecursively(visualRoot.gameObject, layer);
        }

        private GameObject AddPart(PrimitiveType type, string objectName, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = objectName;
            go.transform.SetParent(modelRoot, false);
            go.transform.localPosition = localPosition;
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

        private void BuildSelectionRing()
        {
            // A ground decal, so the marker never hides the shaded floor or the model itself.
            selectionRing = Whitebox.Decal("SelectionRing", visualRoot, new Vector3(0f, 0.035f, 0f), 1.75f, selectionColor, gameObject.layer);
            selectionRing.SetActive(isSelected);
        }

        /// <summary>The employee is the click target, so the root carries a capsule collider.</summary>
        private void EnsureClickCollider()
        {
            CapsuleCollider capsule = GetComponent<CapsuleCollider>();
            if (capsule == null)
            {
                capsule = gameObject.AddComponent<CapsuleCollider>();
            }

            float height = Mathf.Max(1f, bodyHeight + bodyRadius * 2f);
            capsule.height = height;
            capsule.radius = bodyRadius * 1.4f;
            capsule.center = new Vector3(0f, height * 0.5f, 0f);
        }

        /// <summary>Called every frame by StaffManager while the model is walking.</summary>
        public void ApplyWorldPosition(Vector3 worldPosition)
        {
            transform.position = worldPosition;
        }

        /// <summary>Faces the visual model towards an interaction target.</summary>
        public void FaceTowards(Vector3 worldPosition)
        {
            if (modelRoot == null)
            {
                return;
            }

            Vector3 direction = worldPosition - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
            {
                modelRoot.rotation = Quaternion.LookRotation(direction);
            }
        }

        public void SetSelected(bool selected)
        {
            isSelected = selected;
            if (selectionRing != null)
            {
                selectionRing.SetActive(selected);
            }

            if (animator != null)
            {
                // Art models may define these states; missing states are simply ignored by Unity.
                animator.SetBool("Selected", selected);
            }
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
