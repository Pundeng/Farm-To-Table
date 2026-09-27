using System;
using CozyFoodFactory.Buildings;
using CozyFoodFactory.Logistics;
using UnityEngine;

namespace CozyFoodFactory.Food
{
    public static class BasicMixerPortLayout
    {
        public static readonly Vector2Int Footprint = new(2, 2);
        private static readonly Vector2Int InputA = Vector2Int.zero;
        private static readonly Vector2Int InputB = Vector2Int.up;
        private static readonly Vector2Int Output = Vector2Int.one;

        public static Vector2Int GetInputCell(Vector2Int anchor,
            BuildingRotation rotation, int slot) => anchor + rotation.RotateCell(
                slot == 0 ? InputA : slot == 1 ? InputB :
                    throw new ArgumentOutOfRangeException(nameof(slot)), Footprint);

        public static Vector2Int GetInputOutsideCell(Vector2Int anchor,
            BuildingRotation rotation, int slot) =>
            GetInputCell(anchor, rotation, slot) + GetInputFacing(rotation).ToOffset();

        public static Vector2Int GetOutputOutsideCell(Vector2Int anchor,
            BuildingRotation rotation) => anchor + rotation.RotateCell(Output, Footprint) +
            GetOutputFacing(rotation).ToOffset();

        public static GridDirection GetInputFacing(BuildingRotation rotation) =>
            Rotate(GridDirection.West, rotation);

        public static GridDirection GetIncomingDirection(BuildingRotation rotation) =>
            (GridDirection)(((int)GetInputFacing(rotation) + 2) % 4);

        public static GridDirection GetOutputFacing(BuildingRotation rotation) =>
            Rotate(GridDirection.East, rotation);

        private static GridDirection Rotate(GridDirection direction, BuildingRotation rotation)
        {
            int steps = (int)rotation / 90;
            if (steps < 0 || steps > 3 || steps * 90 != (int)rotation)
            {
                throw new ArgumentOutOfRangeException(nameof(rotation));
            }

            return (GridDirection)(((int)direction + steps) % 4);
        }
    }
}
