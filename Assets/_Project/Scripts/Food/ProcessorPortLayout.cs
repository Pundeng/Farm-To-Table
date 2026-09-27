using System;
using CozyFoodFactory.Buildings;
using CozyFoodFactory.Logistics;
using UnityEngine;

namespace CozyFoodFactory.Food
{
    public static class ProcessorPortLayout
    {
        public static readonly Vector2Int Footprint = new(2, 2);
        public static readonly Vector2Int Chamber = Vector2Int.up;
        public static readonly Vector2Int PropertyModule = Vector2Int.one;
        public static readonly Vector2Int Support = Vector2Int.zero;

        public static Vector2Int GetChamberCell(Vector2Int anchor,
            BuildingRotation rotation) => anchor + rotation.RotateCell(Chamber, Footprint);

        public static Vector2Int GetPropertyCell(Vector2Int anchor,
            BuildingRotation rotation) => anchor + rotation.RotateCell(PropertyModule, Footprint);

        public static GridDirection GetFoodInputFacing(BuildingRotation rotation) =>
            Rotate(GridDirection.West, rotation);

        public static GridDirection GetFoodIncomingDirection(BuildingRotation rotation) =>
            (GridDirection)(((int)GetFoodInputFacing(rotation) + 2) % 4);

        public static GridDirection GetPropertyInputFacing(BuildingRotation rotation) =>
            Rotate(GridDirection.South, rotation);

        public static GridDirection GetFoodOutputFacing(BuildingRotation rotation) =>
            Rotate(GridDirection.North, rotation);

        public static Vector2Int GetFoodInputOutsideCell(Vector2Int anchor,
            BuildingRotation rotation) => GetChamberCell(anchor, rotation) +
            GetFoodInputFacing(rotation).ToOffset();

        public static Vector2Int GetPropertyOutsideCell(Vector2Int anchor,
            BuildingRotation rotation) => GetPropertyCell(anchor, rotation) +
            GetPropertyInputFacing(rotation).ToOffset();

        public static Vector2Int GetFoodOutputOutsideCell(Vector2Int anchor,
            BuildingRotation rotation)
        {
            Vector2Int cell = GetChamberCell(anchor, rotation);
            Vector2Int direction = GetFoodOutputFacing(rotation).ToOffset();
            do
            {
                cell += direction;
            }
            while (IsOccupiedCell(cell - anchor, rotation));

            return cell;
        }

        private static bool IsOccupiedCell(Vector2Int rotatedCell,
            BuildingRotation rotation)
        {
            return rotatedCell == rotation.RotateCell(Chamber, Footprint) ||
                rotatedCell == rotation.RotateCell(PropertyModule, Footprint) ||
                rotatedCell == rotation.RotateCell(Support, Footprint);
        }

        private static GridDirection Rotate(GridDirection direction,
            BuildingRotation rotation)
        {
            int steps = (int)rotation / 90;
            if (steps < 0 || steps > 3)
            {
                throw new ArgumentOutOfRangeException(nameof(rotation), rotation, null);
            }

            return (GridDirection)(((int)direction + steps) % 4);
        }
    }
}
