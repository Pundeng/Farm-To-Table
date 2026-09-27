using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace CozyFoodFactory.Grid
{
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(LineRenderer))]
    public sealed class GridHoverHighlight : MonoBehaviour
    {
        [SerializeField] private GridSystem gridSystem = null;
        [SerializeField] private Camera targetCamera = null;
        [SerializeField] private Color highlightColor = new(1f, 0.78f, 0.2f, 1f);
        [SerializeField] private Vector2Int hoveredCell;

        private LineRenderer lineRenderer;
        private Material highlightMaterial;

        public Vector2Int HoveredCell => hoveredCell;

        private void OnEnable()
        {
            lineRenderer = GetComponent<LineRenderer>();
            lineRenderer.loop = true;
            lineRenderer.positionCount = 4;
            lineRenderer.useWorldSpace = true;
            lineRenderer.numCornerVertices = 2;
            lineRenderer.startColor = highlightColor;
            lineRenderer.endColor = highlightColor;
            lineRenderer.shadowCastingMode = ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;
            lineRenderer.sortingOrder = 100;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                highlightMaterial = new Material(shader) { name = "Runtime Hover Material" };
                lineRenderer.sharedMaterial = highlightMaterial;
            }
        }

        private void Update()
        {
            if (gridSystem == null || targetCamera == null || Mouse.current == null)
            {
                lineRenderer.enabled = false;
                return;
            }

            Ray cursorRay = targetCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Mathf.Approximately(cursorRay.direction.z, 0f))
            {
                lineRenderer.enabled = false;
                return;
            }

            float distanceToGridPlane = -cursorRay.origin.z / cursorRay.direction.z;
            if (distanceToGridPlane < 0f)
            {
                lineRenderer.enabled = false;
                return;
            }

            Vector3 cursorWorldPosition = cursorRay.GetPoint(distanceToGridPlane);
            hoveredCell = gridSystem.WorldToGrid(cursorWorldPosition);
            UpdateOutline(gridSystem.GridToWorld(hoveredCell));
            lineRenderer.enabled = true;
        }

        private void UpdateOutline(Vector3 center)
        {
            float halfCell = gridSystem.CellSize * 0.5f;
            float lineWidth = Mathf.Clamp(targetCamera.orthographicSize * 0.008f, 0.025f, 0.12f);
            lineRenderer.startWidth = lineWidth;
            lineRenderer.endWidth = lineWidth;
            lineRenderer.SetPosition(0, center + new Vector3(-halfCell, -halfCell, -0.01f));
            lineRenderer.SetPosition(1, center + new Vector3(-halfCell, halfCell, -0.01f));
            lineRenderer.SetPosition(2, center + new Vector3(halfCell, halfCell, -0.01f));
            lineRenderer.SetPosition(3, center + new Vector3(halfCell, -halfCell, -0.01f));
        }

        private void OnDestroy()
        {
            if (highlightMaterial != null)
            {
                Destroy(highlightMaterial);
            }
        }
    }
}
