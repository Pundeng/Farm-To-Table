using System;
using UnityEngine;

namespace CozyFoodFactory.Grid
{
    /// <summary>
    /// Converts between continuous world positions and integer grid cells.
    /// This class intentionally has no scene or rendering responsibilities.
    /// </summary>
    public sealed class Grid2D
    {
        public Grid2D(float cellSize, Vector2 origin = default)
        {
            if (cellSize <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(cellSize), "Cell size must be greater than zero.");
            }

            CellSize = cellSize;
            Origin = origin;
        }

        public float CellSize { get; }

        public Vector2 Origin { get; }

        public Vector2Int WorldToGrid(Vector2 worldPosition)
        {
            Vector2 localPosition = worldPosition - Origin;
            return new Vector2Int(
                Mathf.FloorToInt(localPosition.x / CellSize),
                Mathf.FloorToInt(localPosition.y / CellSize));
        }

        public Vector2Int WorldToGrid(Vector3 worldPosition)
        {
            return WorldToGrid((Vector2)worldPosition);
        }

        public Vector2 GridToWorld(Vector2Int gridCoordinate)
        {
            return Origin + new Vector2(
                (gridCoordinate.x + 0.5f) * CellSize,
                (gridCoordinate.y + 0.5f) * CellSize);
        }
    }
}
