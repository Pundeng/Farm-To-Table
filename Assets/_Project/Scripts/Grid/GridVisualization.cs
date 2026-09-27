using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CozyFoodFactory.Grid
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class GridVisualization : MonoBehaviour
    {
        [SerializeField] private GridSystem gridSystem = null;
        [SerializeField] private Camera targetCamera = null;
        [SerializeField, Min(0)] private int visibleCellPadding = 2;
        [SerializeField, Min(2)] private int majorLineInterval = 5;
        [SerializeField] private Color minorLineColor = new(0.35f, 0.42f, 0.5f, 0.35f);
        [SerializeField] private Color majorLineColor = new(0.55f, 0.67f, 0.78f, 0.65f);

        private Mesh gridMesh;
        private Material gridMaterial;
        private Vector2Int lastMinimum = new(int.MaxValue, int.MaxValue);
        private Vector2Int lastMaximum = new(int.MinValue, int.MinValue);
        private float lastCellSize = -1f;

        private void OnEnable()
        {
            gridMesh = new Mesh { name = "Runtime Grid Lines" };
            gridMesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = gridMesh;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                Debug.LogError("Grid visualization could not find the Sprites/Default shader.", this);
                enabled = false;
                return;
            }

            gridMaterial = new Material(shader) { name = "Runtime Grid Material" };
            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = gridMaterial;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.sortingOrder = -100;
        }

        private void LateUpdate()
        {
            if (gridSystem == null || targetCamera == null || !targetCamera.orthographic)
            {
                return;
            }

            float halfHeight = targetCamera.orthographicSize;
            float halfWidth = halfHeight * targetCamera.aspect;
            Vector3 cameraPosition = targetCamera.transform.position;

            Vector2Int minimum = gridSystem.WorldToGrid(
                new Vector2(cameraPosition.x - halfWidth, cameraPosition.y - halfHeight));
            Vector2Int maximum = gridSystem.WorldToGrid(
                new Vector2(cameraPosition.x + halfWidth, cameraPosition.y + halfHeight));
            minimum -= Vector2Int.one * visibleCellPadding;
            maximum += Vector2Int.one * (visibleCellPadding + 1);

            if (minimum == lastMinimum && maximum == lastMaximum &&
                Mathf.Approximately(lastCellSize, gridSystem.CellSize))
            {
                return;
            }

            RebuildMesh(minimum, maximum);
            lastMinimum = minimum;
            lastMaximum = maximum;
            lastCellSize = gridSystem.CellSize;
        }

        private void RebuildMesh(Vector2Int minimum, Vector2Int maximum)
        {
            int verticalLineCount = maximum.x - minimum.x + 1;
            int horizontalLineCount = maximum.y - minimum.y + 1;
            var vertices = new List<Vector3>((verticalLineCount + horizontalLineCount) * 2);
            var colors = new List<Color>(vertices.Capacity);
            var indices = new List<int>(vertices.Capacity);

            float minimumX = gridSystem.Origin.x + minimum.x * gridSystem.CellSize;
            float maximumX = gridSystem.Origin.x + maximum.x * gridSystem.CellSize;
            float minimumY = gridSystem.Origin.y + minimum.y * gridSystem.CellSize;
            float maximumY = gridSystem.Origin.y + maximum.y * gridSystem.CellSize;

            for (int x = minimum.x; x <= maximum.x; x++)
            {
                float worldX = gridSystem.Origin.x + x * gridSystem.CellSize;
                AddLine(vertices, colors, indices,
                    new Vector3(worldX, minimumY, 0f),
                    new Vector3(worldX, maximumY, 0f),
                    GetLineColor(x));
            }

            for (int y = minimum.y; y <= maximum.y; y++)
            {
                float worldY = gridSystem.Origin.y + y * gridSystem.CellSize;
                AddLine(vertices, colors, indices,
                    new Vector3(minimumX, worldY, 0f),
                    new Vector3(maximumX, worldY, 0f),
                    GetLineColor(y));
            }

            gridMesh.Clear();
            gridMesh.SetVertices(vertices);
            gridMesh.SetColors(colors);
            gridMesh.SetIndices(indices, MeshTopology.Lines, 0);
            gridMesh.RecalculateBounds();
        }

        private Color GetLineColor(int lineIndex)
        {
            return lineIndex % majorLineInterval == 0 ? majorLineColor : minorLineColor;
        }

        private static void AddLine(
            ICollection<Vector3> vertices,
            ICollection<Color> colors,
            ICollection<int> indices,
            Vector3 start,
            Vector3 end,
            Color color)
        {
            int startIndex = vertices.Count;
            vertices.Add(start);
            vertices.Add(end);
            colors.Add(color);
            colors.Add(color);
            indices.Add(startIndex);
            indices.Add(startIndex + 1);
        }

        private void OnDestroy()
        {
            if (gridMesh != null)
            {
                Destroy(gridMesh);
            }

            if (gridMaterial != null)
            {
                Destroy(gridMaterial);
            }
        }
    }
}
