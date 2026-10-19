using Kitchen.Core;
using UnityEngine;

namespace Kitchen.View
{
    public sealed class WorkPositionIndicator
    {
        private readonly Transform root;
        private readonly Transform workPad;
        private readonly Renderer[] renderers;

        public WorkPositionIndicator(Transform parent, float cellSize, float arrowHeight)
        {
            root = new GameObject("WorkPositionIndicator").transform;
            root.SetParent(parent, false);
            workPad = new GameObject("WorkPad").transform;
            workPad.SetParent(root, false);
            workPad.localPosition = new Vector3(0f, 0.045f, cellSize);

            float halfWidth = cellSize * 0.34f;
            AddDecal("PadLeft", workPad, new Vector3(-halfWidth, 0f, 0f), new Vector2(0.07f, halfWidth * 2f));
            AddDecal("PadRight", workPad, new Vector3(halfWidth, 0f, 0f), new Vector2(0.07f, halfWidth * 2f));
            AddDecal("PadFront", workPad, new Vector3(0f, 0f, halfWidth), new Vector2(halfWidth * 2f, 0.07f));
            AddDecal("PadBack", workPad, new Vector3(0f, 0f, -halfWidth), new Vector2(halfWidth * 2f, 0.07f));

            Transform arrow = new GameObject("FacingArrow").transform;
            arrow.SetParent(root, false);
            arrow.localPosition = new Vector3(0f, arrowHeight, cellSize * 0.32f);
            AddDecal("Shaft", arrow, Vector3.zero, new Vector2(0.10f, 0.32f));
            Transform leftWing = AddDecal("ArrowLeft", arrow, new Vector3(-0.08f, 0f, 0.10f), new Vector2(0.10f, 0.26f));
            Transform rightWing = AddDecal("ArrowRight", arrow, new Vector3(0.08f, 0f, 0.10f), new Vector2(0.10f, 0.26f));
            leftWing.localRotation = Quaternion.Euler(90f, 45f, 0f);
            rightWing.localRotation = Quaternion.Euler(90f, -45f, 0f);
            renderers = root.GetComponentsInChildren<Renderer>(true);
        }

        public bool IsVisible => root != null && root.gameObject.activeSelf;

        private static Transform AddDecal(string name, Transform parent, Vector3 position, Vector2 size)
        {
            GameObject decal = Whitebox.Decal(name, parent, position, 1f, Color.white, parent.gameObject.layer);
            decal.transform.localScale = new Vector3(size.x, size.y, 1f);
            return decal.transform;
        }

        public void Refresh(CookerFacing facing, bool open, bool occupied, bool showPad)
        {
            root.localRotation = Quaternion.Euler(0f, (int)CookerFacingUtility.Normalize(facing) * 90f, 0f);
            workPad.gameObject.SetActive(showPad);
            Color color = !open ? new Color(1f, 0.25f, 0.20f, 0.9f) :
                occupied ? new Color(1f, 0.73f, 0.18f, 0.9f) : new Color(0.30f, 0.95f, 0.65f, 0.9f);
            Material material = Whitebox.OverlayMaterial(color, "WorkPosition");
            for (int index = 0; index < renderers.Length; index++)
            {
                renderers[index].sharedMaterial = material;
            }
        }

        public void SetVisible(bool visible)
        {
            root.gameObject.SetActive(visible);
        }
    }
}
