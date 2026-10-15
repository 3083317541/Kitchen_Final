using TMPro;
using UnityEngine;

namespace Kitchen.EditorTools
{
    /// <summary>
    /// Small helpers used by the scene generator.
    /// </summary>
    public static class KitchenEditorExtensions
    {
        /// <summary>
        /// True when a TextMeshPro font asset can actually render text: it needs a material, an
        /// atlas texture and at least one glyph. A font asset that fails this renders nothing, which
        /// is exactly the "no text in the UI" symptom.
        /// </summary>
        public static bool IsUsable(this TMP_FontAsset font)
        {
            if (font == null)
            {
                return false;
            }

            if (font.material == null || font.atlasTexture == null)
            {
                return false;
            }

            if (font.characterTable == null || font.characterTable.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < font.characterTable.Count; i++)
            {
                TMP_Character character = font.characterTable[i];
                if (character != null && character.glyph != null)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
