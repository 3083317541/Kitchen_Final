using System.Collections.Generic;
using Kitchen.Core;
using Kitchen.Data;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kitchen.View
{
    /// <summary>
    /// The whitebox look: colours, shared materials and helpers that build primitive geometry.
    ///
    /// Two material families are used and the split matters for a 3D top down view:
    /// <list type="bullet">
    /// <item><b>Solid</b> materials use the lit Standard shader, so cookers, chefs, ingredients and
    /// the floor slab are genuinely shaded 3D objects with volume and cast shadows.</item>
    /// <item><b>Overlay</b> materials use an unlit alpha blended shader. They are reserved for flat
    /// ground decals such as the cell grid, highlights and the build ghost, which must not look like
    /// solid slabs and must never hide the shaded floor underneath.</item>
    /// </list>
    ///
    /// Every material is cached, so the demo does not allocate one material per object, and every
    /// generated object stays replaceable through the Art Replacement fields on the definitions.
    /// </summary>
    public static class Whitebox
    {
        // ------------------------------------------------------------------ palette

        public static readonly Color FloorColor = new Color(0.26f, 0.27f, 0.30f);
        public static readonly Color FloorEdgeColor = new Color(0.15f, 0.16f, 0.19f);
        public static readonly Color GridCellColor = new Color(0.62f, 0.68f, 0.78f, 0.16f);
        public static readonly Color HighlightValidColor = new Color(0.30f, 0.90f, 0.42f, 0.45f);
        public static readonly Color HighlightInvalidColor = new Color(0.95f, 0.28f, 0.24f, 0.45f);
        public static readonly Color HighlightSelectColor = new Color(0.30f, 0.75f, 1f, 0.45f);
        public static readonly Color StaffBodyColor = new Color(0.90f, 0.76f, 0.48f);
        public static readonly Color StaffHeadColor = new Color(0.96f, 0.88f, 0.76f);
        public static readonly Color StaffSelectedColor = new Color(0.25f, 0.98f, 0.60f, 0.55f);
        public static readonly Color InactiveAccentColor = new Color(0.42f, 0.42f, 0.46f);
        public static readonly Color MetalColor = new Color(0.58f, 0.60f, 0.64f);
        public static readonly Color DetailBoardColor = new Color(0.62f, 0.46f, 0.28f);
        public static readonly Color DetailGlassColor = new Color(0.55f, 0.78f, 0.85f);
        public static readonly Color DarkMetalColor = new Color(0.22f, 0.23f, 0.26f);

        // ------------------------------------------------------------------ material cache

        private static readonly Dictionary<int, Material> SolidCache = new Dictionary<int, Material>();
        private static readonly Dictionary<int, Material> OverlayCache = new Dictionary<int, Material>();

        private static WhiteboxMaterialLibrary library;

        /// <summary>
        /// Supplies the asset backed materials. Once registered, materials are resolved by name from
        /// this library and every prefab keeps a reference to a real asset instead of a transient
        /// in-memory material.
        /// </summary>
        public static void RegisterLibrary(WhiteboxMaterialLibrary materialLibrary)
        {
            library = materialLibrary;
        }

        /// <summary>The material library in use, or null when running without generated assets.</summary>
        public static WhiteboxMaterialLibrary Library => library;

        /// <summary>
        /// Stable asset name for a colour. The generator writes materials under exactly these names,
        /// so the same call resolves to the same asset at generation time and at runtime.
        /// </summary>
        public static string MaterialName(int prefix, Color color)
        {
            return $"M_Kitchen_{prefix}_{ColorUtility.ToHtmlStringRGB(color)}";
        }

        private static Material FromLibrary(int prefix, Color color, string label)
        {
            if (library == null)
            {
                return null;
            }

            Material material = library.Find(MaterialName(prefix, color));
            if (material != null && material.shader != null && material.shader.name != "Hidden/InternalErrorShader")
            {
                return material;
            }

            return null;
        }

        /// <summary>
        /// A lit, opaque material. Used for everything with volume so the scene reads as 3D.
        /// </summary>
        public static Material SolidMaterial(Color color, string label = "Solid")
        {
            Material fromAssets = FromLibrary(0, color, label);
            if (fromAssets != null)
            {
                return fromAssets;
            }

            int key = color.GetHashCode();
            if (SolidCache.TryGetValue(key, out Material cached) && cached != null)
            {
                return cached;
            }

            Material material = new Material(FindShader("Standard", "Diffuse", "Unlit/Color"))
            {
                name = MaterialName(0, color)
            };

            SetColor(material, color);

            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", 0.15f);
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0.0f);
            }

            SolidCache[key] = material;
            return material;
        }

        /// <summary>
        /// An unlit, alpha blended material for flat ground decals: the cell grid, cell highlights,
        /// the build ghost and the selection ring.
        /// </summary>
        public static Material OverlayMaterial(Color color, string label = "Overlay")
        {
            Color keyed = new Color(color.r, color.g, color.b, Mathf.Round(color.a * 100f) / 100f);

            Material fromAssets = FromLibrary(1, keyed, label);
            if (fromAssets != null)
            {
                return fromAssets;
            }

            int key = keyed.GetHashCode() ^ 0x5F3A;
            if (OverlayCache.TryGetValue(key, out Material cached) && cached != null)
            {
                return cached;
            }

            Material material = new Material(FindShader("Sprites/Default", "Unlit/Transparent", "Unlit/Color"))
            {
                name = MaterialName(1, keyed)
            };

            SetColor(material, keyed);

            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", Texture2D.whiteTexture);
            }

            // Ground decals must never occlude the shaded geometry they sit next to.
            material.renderQueue = (int)RenderQueue.Transparent;

            OverlayCache[key] = material;
            return material;
        }

        /// <summary>Kept for callers that predate the solid/overlay split.</summary>
        public static Material TransparentMaterial(Color color, string label = "Highlight")
        {
            return OverlayMaterial(color, label);
        }

        private static Shader FindShader(params string[] candidates)
        {
            for (int i = 0; i < candidates.Length; i++)
            {
                Shader shader = Shader.Find(candidates[i]);
                if (shader != null)
                {
                    return shader;
                }
            }

            return Shader.Find("Standard");
        }

        private static void SetColor(Material material, Color color)
        {
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
        }

        // ------------------------------------------------------------------ primitive builders

        /// <summary>
        /// Builds a lit primitive child, strips its collider and applies a layer.
        /// </summary>
        public static GameObject Primitive(
            PrimitiveType type,
            string objectName,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Color color,
            int layer,
            bool keepCollider = false)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = objectName;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;

            Collider collider = go.GetComponent<Collider>();
            if (collider != null && !keepCollider)
            {
                Object.DestroyImmediate(collider);
            }

            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = SolidMaterial(color, type.ToString());
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }

            SetLayerRecursively(go, layer);
            return go;
        }

        /// <summary>
        /// Builds a flat, unlit, alpha blended quad on the ground plane, used as a decal.
        /// The quad lies in the XZ plane and faces upwards.
        /// </summary>
        public static GameObject Decal(
            string objectName,
            Transform parent,
            Vector3 localPosition,
            float size,
            Color color,
            int layer)
        {
            GameObject go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(size, size, 1f);

            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = QuadMesh;

            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = OverlayMaterial(color, objectName);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            SetLayerRecursively(go, layer);
            return go;
        }

        /// <summary>Alias kept so existing call sites keep working.</summary>
        public static GameObject Quad(string objectName, Transform parent, Vector3 localPosition, float size, Color color, int layer)
        {
            return Decal(objectName, parent, localPosition, size, color, layer);
        }

        private static Mesh quadMesh;

        /// <summary>Unit quad on the XY plane, facing -Z, reused by every ground decal.</summary>
        public static Mesh QuadMesh
        {
            get
            {
                if (quadMesh == null)
                {
                    quadMesh = new Mesh { name = "WhiteboxQuad", hideFlags = HideFlags.DontSave };
                    quadMesh.vertices = new[]
                    {
                        new Vector3(-0.5f, -0.5f, 0f),
                        new Vector3(0.5f, -0.5f, 0f),
                        new Vector3(0.5f, 0.5f, 0f),
                        new Vector3(-0.5f, 0.5f, 0f)
                    };
                    quadMesh.uv = new[]
                    {
                        new Vector2(0f, 0f),
                        new Vector2(1f, 0f),
                        new Vector2(1f, 1f),
                        new Vector2(0f, 1f)
                    };
                    quadMesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                    quadMesh.RecalculateNormals();
                    quadMesh.RecalculateBounds();
                }

                return quadMesh;
            }
        }

        public static void SetLayerRecursively(GameObject target, int layer)
        {
            GameLayers.SetLayerRecursively(target, layer);
        }

        // ------------------------------------------------------------------ item tokens

        /// <summary>
        /// Builds a small 3D token that shows an item in the world, for example the contents of a
        /// cooker. Shape and colour come from the item definition so new items need no new code.
        /// </summary>
        public static GameObject BuildItemToken(ItemDefinition item, Transform parent, string objectName, int layer)
        {
            GameObject root = new GameObject(objectName);
            root.transform.SetParent(parent, false);

            if (item == null)
            {
                return root;
            }

            if (item.ViewPrefab != null)
            {
                // The item's own art prefab wins over the generated token.
                GameObject instance = Object.Instantiate(item.ViewPrefab, root.transform);
                instance.name = item.ViewPrefab.name;
                SetLayerRecursively(root, layer);
                return root;
            }

            Material material = item.ViewMaterial != null ? item.ViewMaterial : SolidMaterial(item.ViewColor, "Item");
            float scale = item.ViewScale;

            switch (item.Category)
            {
                case ItemCategory.Raw:
                    // A rounded piece of produce.
                    AddToken(PrimitiveType.Sphere, root.transform, new Vector3(0f, scale * 0.55f, 0f),
                        new Vector3(scale, scale * 1.1f, scale), material, layer);
                    break;

                case ItemCategory.Processed:
                    // A flat slice.
                    AddToken(PrimitiveType.Cylinder, root.transform, new Vector3(0f, scale * 0.12f, 0f),
                        new Vector3(scale * 1.35f, scale * 0.12f, scale * 1.35f), material, layer);
                    break;

                case ItemCategory.Dish:
                    // A bowl with a topping.
                    AddToken(PrimitiveType.Cylinder, root.transform, new Vector3(0f, scale * 0.35f, 0f),
                        new Vector3(scale * 1.1f, scale * 0.35f, scale * 1.1f), material, layer);
                    AddToken(PrimitiveType.Sphere, root.transform, new Vector3(0f, scale * 0.85f, 0f),
                        new Vector3(scale * 0.75f, scale * 0.4f, scale * 0.75f),
                        SolidMaterial(ToppingColor(item), "Topping"), layer);
                    break;

                default:
                    // A plate.
                    AddToken(PrimitiveType.Cylinder, root.transform, new Vector3(0f, scale * 0.08f, 0f),
                        new Vector3(scale * 1.4f, scale * 0.08f, scale * 1.4f), material, layer);
                    break;
            }

            SetLayerRecursively(root, layer);
            return root;
        }

        /// <summary>The derived colour of a dish topping, exposed so a material asset can exist for it.</summary>
        public static Color ToppingColor(ItemDefinition item)
        {
            Color baseColor = item.ViewColor;
            return new Color(
                Mathf.Clamp01(baseColor.r * 1.15f + 0.08f),
                Mathf.Clamp01(baseColor.g * 1.15f + 0.08f),
                Mathf.Clamp01(baseColor.b * 1.15f + 0.08f));
        }

        private static void AddToken(
            PrimitiveType type,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            int layer)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = "Token";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;

            Collider collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }

            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }

            SetLayerRecursively(go, layer);
        }

        // ------------------------------------------------------------------ formatting

        public static string Stack(ItemStack stack)
        {
            return stack.IsValid ? $"{stack.Item.DisplayName} x{stack.Count}" : "-";
        }

        public static string Stacks(IReadOnlyList<ItemStack> stacks)
        {
            if (stacks == null || stacks.Count == 0)
            {
                return "-";
            }

            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            for (int i = 0; i < stacks.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(Stack(stacks[i]));
            }

            return builder.ToString();
        }
    }
}
