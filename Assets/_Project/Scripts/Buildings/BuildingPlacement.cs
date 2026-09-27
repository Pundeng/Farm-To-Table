using System;
using System.Collections.Generic;
using UnityEngine;

namespace CozyFoodFactory.Buildings
{
    public sealed class BuildingPlacement
    {
        private readonly Vector2Int[] occupiedCells;

        public BuildingPlacement(
            string definitionId,
            Vector2Int anchorCell,
            Vector2Int footprint,
            BuildingRotation rotation,
            IReadOnlyList<Vector2Int> localOccupiedCells = null)
        {
            if (string.IsNullOrWhiteSpace(definitionId))
            {
                throw new ArgumentException("A building definition ID is required.", nameof(definitionId));
            }

            DefinitionId = definitionId;
            AnchorCell = anchorCell;
            Footprint = footprint;
            Rotation = rotation;
            RotatedFootprint = rotation.GetRotatedFootprint(footprint);
            occupiedCells = CreateOccupiedCells(anchorCell, footprint, rotation,
                localOccupiedCells);
        }

        public string DefinitionId { get; }

        public Vector2Int AnchorCell { get; }

        public Vector2Int Footprint { get; }

        public BuildingRotation Rotation { get; }

        public Vector2Int RotatedFootprint { get; }

        public IReadOnlyList<Vector2Int> OccupiedCells => occupiedCells;

        private static Vector2Int[] CreateOccupiedCells(Vector2Int anchorCell,
            Vector2Int footprint, BuildingRotation rotation,
            IReadOnlyList<Vector2Int> localOccupiedCells)
        {
            if (localOccupiedCells != null && localOccupiedCells.Count > 0)
            {
                var cells = new Vector2Int[localOccupiedCells.Count];
                var unique = new HashSet<Vector2Int>();
                for (int index = 0; index < cells.Length; index++)
                {
                    Vector2Int local = localOccupiedCells[index];
                    if (local.x < 0 || local.y < 0 || local.x >= footprint.x ||
                        local.y >= footprint.y || !unique.Add(local))
                    {
                        throw new ArgumentException("Occupied cells must be unique and within the footprint.",
                            nameof(localOccupiedCells));
                    }

                    cells[index] = anchorCell + rotation.RotateCell(local, footprint);
                }

                return cells;
            }

            Vector2Int rotatedFootprint = rotation.GetRotatedFootprint(footprint);
            var rectangularCells = new Vector2Int[rotatedFootprint.x * rotatedFootprint.y];
            int cellIndex = 0;
            for (int y = 0; y < rotatedFootprint.y; y++)
            {
                for (int x = 0; x < rotatedFootprint.x; x++)
                {
                    rectangularCells[cellIndex++] = anchorCell + new Vector2Int(x, y);
                }
            }

            return rectangularCells;
        }
    }
}
