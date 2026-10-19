using System.Collections.Generic;
using UnityEngine;

namespace Kitchen.View
{
    /// <summary>
    /// Named registry of the whitebox materials, stored as a real asset so prefabs can reference
    /// them.
    ///
    /// The first version built materials in memory with <see cref="HideFlags.DontSave"/>. Prefabs
    /// serialised those references as null, which made every instanced cooker render with Unity's
    /// magenta missing-material shader. Materials are therefore authored as assets and looked up
    /// here by name; the in-memory creation remains only as a fallback for a scene that runs without
    /// generated assets.
    ///
    /// The asset lives under Resources so the runtime can load it without a scene reference.
    /// </summary>
    [CreateAssetMenu(menuName = "Kitchen/Whitebox Material Library", fileName = "WhiteboxMaterials")]
    public class WhiteboxMaterialLibrary : ScriptableObject
    {
        /// <summary>Resources path the runtime loads the library from.</summary>
        public const string ResourcesPath = "Kitchen/WhiteboxMaterials";

        [SerializeField] private List<Material> materials = new List<Material>();

        private Dictionary<string, Material> lookup;

        /// <summary>Every material in the library.</summary>
        public IReadOnlyList<Material> Materials => materials;

        /// <summary>Empties the library. Used at the start of a generation pass.</summary>
        public void Clear()
        {
            materials.Clear();
            lookup = null;
        }

        /// <summary>Adds a material under its own name. Used by the editor generator.</summary>
        public void Add(Material material)
        {
            if (material == null || materials.Contains(material))
            {
                return;
            }

            materials.Add(material);
            lookup = null;
        }

        /// <summary>Finds a material by name. Returns null when the library does not hold it.</summary>
        public Material Find(string materialName)
        {
            if (string.IsNullOrEmpty(materialName))
            {
                return null;
            }

            if (lookup == null)
            {
                lookup = new Dictionary<string, Material>(materials.Count);
                for (int i = 0; i < materials.Count; i++)
                {
                    Material material = materials[i];
                    if (material != null && !lookup.ContainsKey(material.name))
                    {
                        lookup.Add(material.name, material);
                    }
                }
            }

            return lookup.TryGetValue(materialName, out Material found) ? found : null;
        }

        /// <summary>Loads the generated library from Resources, or null when it does not exist.</summary>
        public static WhiteboxMaterialLibrary Load()
        {
            WhiteboxMaterialLibrary library = Resources.Load<WhiteboxMaterialLibrary>(ResourcesPath);
            if (library != null)
            {
                Whitebox.RegisterLibrary(library);
            }

            return library;
        }
    }
}
