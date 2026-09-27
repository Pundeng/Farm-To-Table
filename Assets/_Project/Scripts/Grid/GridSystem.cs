using UnityEngine;

namespace CozyFoodFactory.Grid
{
    public sealed class GridSystem : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float cellSize = 1f;
        [SerializeField] private Vector2 origin = Vector2.zero;

        private Grid2D grid;

        public float CellSize => cellSize;

        public Vector2 Origin => origin;

        public Vector2Int WorldToGrid(Vector3 worldPosition)
        {
            return GetGrid().WorldToGrid(worldPosition);
        }

        public Vector3 GridToWorld(Vector2Int gridCoordinate)
        {
            Vector2 worldPosition = GetGrid().GridToWorld(gridCoordinate);
            return new Vector3(worldPosition.x, worldPosition.y, 0f);
        }

        private Grid2D GetGrid()
        {
            if (grid == null || !Mathf.Approximately(grid.CellSize, cellSize) || grid.Origin != origin)
            {
                grid = new Grid2D(cellSize, origin);
            }

            return grid;
        }

        private void OnValidate()
        {
            cellSize = Mathf.Max(0.01f, cellSize);
            grid = null;
        }
    }
}
