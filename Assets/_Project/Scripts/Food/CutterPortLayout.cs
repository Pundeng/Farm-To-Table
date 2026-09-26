using System;
using FantasyShapez.Buildings;
using FantasyShapez.Logistics;
using UnityEngine;

namespace FantasyShapez.Food
{
    public static class CutterPortLayout
    {
        public static readonly Vector2Int Footprint = new(1, 2);

        public static Vector2Int InputCell(Vector2Int anchor, BuildingRotation rotation) =>
            anchor + rotation.RotateCell(Vector2Int.zero, Footprint);

        public static GridDirection InputFacing(BuildingRotation rotation) =>
            Rotate(GridDirection.South, rotation);

        public static GridDirection IncomingDirection(BuildingRotation rotation) =>
            Rotate(GridDirection.North, rotation);

        public static Vector2Int OutputCell(Vector2Int anchor,
            BuildingRotation rotation, int side) =>
            anchor + rotation.RotateCell(Vector2Int.up, Footprint) +
            OutputDirection(rotation, side).ToOffset();

        public static GridDirection OutputDirection(BuildingRotation rotation, int side) =>
            Rotate(side == 0 ? GridDirection.West : side == 1
                ? GridDirection.East : throw new ArgumentOutOfRangeException(nameof(side)),
                rotation);

        private static GridDirection Rotate(GridDirection direction,
            BuildingRotation rotation)
        {
            int steps = (int)rotation / 90;
            if (steps is < 0 or > 3 || steps * 90 != (int)rotation)
                throw new ArgumentOutOfRangeException(nameof(rotation));
            return (GridDirection)(((int)direction + steps) % 4);
        }
    }
}
