using UnityEngine;

namespace Kitchen.View
{
    /// <summary>
    /// Names of the layers this demo relies on. They are created by the editor scene generator;
    /// the constants are kept here so runtime code never hard codes a raw layer index.
    /// </summary>
    public static class GameLayers
    {
        public const string InteractableName = "Interactable";
        public const string GroundName = "Ground";

        private static int cachedInteractable = -1;
        private static int cachedGround = -1;

        /// <summary>Layer used by cookers and staff: the only things a click can select.</summary>
        public static int Interactable
        {
            get
            {
                if (cachedInteractable < 0)
                {
                    cachedInteractable = LayerMask.NameToLayer(InteractableName);
                    if (cachedInteractable < 0)
                    {
                        cachedInteractable = 0;
                    }
                }

                return cachedInteractable;
            }
        }

        /// <summary>Layer used by the floor and the per-cell quads: where a click lands.</summary>
        public static int Ground
        {
            get
            {
                if (cachedGround < 0)
                {
                    cachedGround = LayerMask.NameToLayer(GroundName);
                    if (cachedGround < 0)
                    {
                        cachedGround = 0;
                    }
                }

                return cachedGround;
            }
        }

        /// <summary>Mask matching only selectable cookers and staff.</summary>
        public static int InteractableMask => 1 << Interactable;

        /// <summary>Mask matching only ground geometry.</summary>
        public static int GroundMask => 1 << Ground;

        /// <summary>Recursively assigns a layer to a hierarchy.</summary>
        public static void SetLayerRecursively(GameObject target, int layer)
        {
            if (target == null)
            {
                return;
            }

            target.layer = layer;
            Transform t = target.transform;
            for (int i = 0; i < t.childCount; i++)
            {
                SetLayerRecursively(t.GetChild(i).gameObject, layer);
            }
        }
    }
}
