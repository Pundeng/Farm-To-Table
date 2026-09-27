using System.Collections.Generic;
using UnityEngine;

namespace CozyFoodFactory.Buildings
{
    public sealed class GridOccupancy
    {
        private readonly Dictionary<Vector2Int, BuildingPlacement> buildingsByCell = new();
        private readonly Dictionary<Vector2Int, BuildingPlacement> overlaysByCell = new();
        private readonly HashSet<BuildingPlacement> overlayPlacements = new();

        public int OccupiedCellCount
        {
            get
            {
                int count = buildingsByCell.Count;
                foreach (Vector2Int cell in overlaysByCell.Keys)
                {
                    if (!buildingsByCell.ContainsKey(cell))
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public bool CanPlace(Vector2Int anchorCell, Vector2Int footprint, BuildingRotation rotation)
        {
            return CanPlace(anchorCell, footprint, rotation, null);
        }

        public bool CanPlace(
            Vector2Int anchorCell,
            Vector2Int footprint,
            BuildingRotation rotation,
            ISet<BuildingPlacement> ignoredPlacements)
        {
            return CanPlace(new BuildingPlacement("Candidate", anchorCell, footprint, rotation),
                ignoredPlacements);
        }

        public bool CanPlace(BuildingDefinition definition, Vector2Int anchorCell,
            BuildingRotation rotation, ISet<BuildingPlacement> ignoredPlacements = null)
        {
            return CanPlace(new BuildingPlacement(definition.Id, anchorCell,
                definition.Footprint, rotation, definition.OccupiedCells), ignoredPlacements);
        }

        private bool CanPlace(BuildingPlacement candidate,
            ISet<BuildingPlacement> ignoredPlacements)
        {
            foreach (Vector2Int cell in candidate.OccupiedCells)
            {
                if ((buildingsByCell.TryGetValue(cell, out BuildingPlacement occupant) &&
                     (ignoredPlacements == null || !ignoredPlacements.Contains(occupant))) ||
                    (overlaysByCell.TryGetValue(cell, out occupant) &&
                     (ignoredPlacements == null || !ignoredPlacements.Contains(occupant))))
                {
                    return false;
                }
            }

            return true;
        }

        public bool TryRegister(
            string definitionId,
            Vector2Int anchorCell,
            Vector2Int footprint,
            BuildingRotation rotation,
            out BuildingPlacement placement)
        {
            return TryRegister(new BuildingPlacement(definitionId, anchorCell, footprint,
                rotation), out placement);
        }

        public bool TryRegister(BuildingDefinition definition, Vector2Int anchorCell,
            BuildingRotation rotation, out BuildingPlacement placement)
        {
            return TryRegister(new BuildingPlacement(definition.Id, anchorCell,
                definition.Footprint, rotation, definition.OccupiedCells), out placement);
        }

        private bool TryRegister(BuildingPlacement candidate, out BuildingPlacement placement)
        {
            if (!CanPlace(candidate, null))
            {
                placement = null;
                return false;
            }

            placement = candidate;
            foreach (Vector2Int cell in placement.OccupiedCells)
            {
                buildingsByCell.Add(cell, placement);
            }

            return true;
        }

        public bool TryGetBuilding(Vector2Int cell, out BuildingPlacement placement)
        {
            return overlaysByCell.TryGetValue(cell, out placement) ||
                buildingsByCell.TryGetValue(cell, out placement);
        }

        public bool TryGetUnderlyingBuilding(Vector2Int cell, out BuildingPlacement placement)
        {
            return buildingsByCell.TryGetValue(cell, out placement);
        }

        public bool CanPlaceOver(
            Vector2Int anchorCell,
            Vector2Int footprint,
            BuildingRotation rotation,
            Vector2Int underlyingCell,
            BuildingPlacement underlying)
        {
            if (underlying == null ||
                !buildingsByCell.TryGetValue(underlyingCell, out BuildingPlacement registered) ||
                !ReferenceEquals(registered, underlying))
            {
                return false;
            }

            bool coversUnderlying = false;
            Vector2Int rotatedFootprint = rotation.GetRotatedFootprint(footprint);
            for (int y = 0; y < rotatedFootprint.y; y++)
            {
                for (int x = 0; x < rotatedFootprint.x; x++)
                {
                    Vector2Int cell = anchorCell + new Vector2Int(x, y);
                    if (overlaysByCell.ContainsKey(cell))
                    {
                        return false;
                    }

                    if (cell == underlyingCell)
                    {
                        coversUnderlying = true;
                    }
                    else if (buildingsByCell.ContainsKey(cell))
                    {
                        return false;
                    }
                }
            }

            return coversUnderlying;
        }

        public bool TryRegisterOver(
            string definitionId,
            Vector2Int anchorCell,
            Vector2Int footprint,
            BuildingRotation rotation,
            Vector2Int underlyingCell,
            BuildingPlacement underlying,
            out BuildingPlacement placement)
        {
            if (!CanPlaceOver(anchorCell, footprint, rotation, underlyingCell, underlying))
            {
                placement = null;
                return false;
            }

            placement = new BuildingPlacement(definitionId, anchorCell, footprint, rotation);
            foreach (Vector2Int cell in placement.OccupiedCells)
            {
                overlaysByCell.Add(cell, placement);
            }

            overlayPlacements.Add(placement);
            return true;
        }

        public bool TryRestore(BuildingPlacement placement)
        {
            if (placement == null || !CanPlace(placement, null))
            {
                return false;
            }

            foreach (Vector2Int cell in placement.OccupiedCells)
            {
                buildingsByCell.Add(cell, placement);
            }

            return true;
        }

        public bool Remove(BuildingPlacement placement)
        {
            if (placement == null)
            {
                return false;
            }

            bool removedAnyCell = false;
            if (overlayPlacements.Remove(placement))
            {
                foreach (Vector2Int cell in placement.OccupiedCells)
                {
                    if (overlaysByCell.TryGetValue(cell, out BuildingPlacement overlay) &&
                        ReferenceEquals(overlay, placement))
                    {
                        overlaysByCell.Remove(cell);
                        removedAnyCell = true;
                    }
                }

                return removedAnyCell;
            }

            foreach (Vector2Int cell in placement.OccupiedCells)
            {
                if (overlaysByCell.ContainsKey(cell))
                {
                    return false;
                }
            }

            foreach (Vector2Int cell in placement.OccupiedCells)
            {
                if (buildingsByCell.TryGetValue(cell, out BuildingPlacement occupyingBuilding) &&
                    ReferenceEquals(occupyingBuilding, placement))
                {
                    buildingsByCell.Remove(cell);
                    removedAnyCell = true;
                }
            }

            return removedAnyCell;
        }
    }
}
