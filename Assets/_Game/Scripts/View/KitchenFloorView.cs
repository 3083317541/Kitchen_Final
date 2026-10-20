using UnityEngine;

namespace Kitchen.View
{
    [DisallowMultipleComponent]
    public sealed class KitchenFloorView : MonoBehaviour
    {
        [SerializeField] private GameObject tilePrefab;
        [SerializeField] private Material lightTile;
        [SerializeField] private Material greenTile;
        [SerializeField] private Material borderMaterial;
        [SerializeField] private Material groutMaterial;
        [SerializeField] private Material cellOverlayMaterial;

        public Material CellOverlayMaterial => cellOverlayMaterial;
        public int TileCount { get; private set; }

        public void Build(int width, int height, float cellSize)
        {
            Transform previous = transform.Find("TileLayout");
            if (previous != null)
            {
                previous.name = "RetiredLayout";
                previous.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(previous.gameObject);
                else DestroyImmediate(previous.gameObject);
            }

            Transform layout = new GameObject("TileLayout").transform;
            layout.SetParent(transform, false);
            TileCount = 0;
            float mapWidth = width * cellSize;
            float mapDepth = height * cellSize;
            AddSlab(layout, "WoodenEdge", new Vector3(0f, -0.23f, 0f),
                new Vector3(mapWidth + 0.26f, 0.30f, mapDepth + 0.26f), borderMaterial);
            AddSlab(layout, "Grout", new Vector3(0f, -0.10f, 0f),
                new Vector3(mapWidth + 0.04f, 0.15f, mapDepth + 0.04f), groutMaterial);

            if (tilePrefab != null)
            {
                for (int column = 0; column < width; column++)
                {
                    for (int row = 0; row < height; row++)
                    {
                        GameObject tile = Instantiate(tilePrefab, layout);
                        tile.name = $"Tile_{column}_{row}";
                        tile.transform.localPosition = new Vector3(
                            (column - (width - 1) * 0.5f) * cellSize, 0f,
                            (row - (height - 1) * 0.5f) * cellSize);
                        tile.transform.localScale = new Vector3(cellSize * 0.985f, 1f, cellSize * 0.985f);
                        Material material = (column + row) % 2 == 0 ? lightTile : greenTile;
                        foreach (var renderer in tile.GetComponentsInChildren<Renderer>(true))
                        {
                            var materials = renderer.sharedMaterials;
                            for (int index = 0; index < materials.Length; index++) materials[index] = material;
                            renderer.sharedMaterials = materials;
                        }
                        TileCount++;
                    }
                }
            }
            GameLayers.SetLayerRecursively(layout.gameObject, GameLayers.Ground);
        }

        private static void AddSlab(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            GameObject slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = name;
            slab.transform.SetParent(parent, false);
            slab.transform.localPosition = position;
            slab.transform.localScale = size;
            slab.GetComponent<Renderer>().sharedMaterial = material;
            var collider = slab.GetComponent<Collider>();
            collider.enabled = false;
            if (Application.isPlaying) Destroy(collider);
            else DestroyImmediate(collider);
        }
    }
}
