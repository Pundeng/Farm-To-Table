using System;
using CozyFoodFactory.Buildings;
using UnityEngine;

namespace CozyFoodFactory.Logistics
{
    public enum GridDirection
    {
        North,
        East,
        South,
        West
    }

    public static class GridDirectionExtensions
    {
        public static Vector2Int ToOffset(this GridDirection direction)
        {
            return direction switch
            {
                GridDirection.North => Vector2Int.up,
                GridDirection.East => Vector2Int.right,
                GridDirection.South => Vector2Int.down,
                GridDirection.West => Vector2Int.left,
                _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
            };
        }

        public static GridDirection ToGridDirection(this BuildingRotation rotation)
        {
            return rotation switch
            {
                BuildingRotation.Degrees0 => GridDirection.North,
                BuildingRotation.Degrees90 => GridDirection.East,
                BuildingRotation.Degrees180 => GridDirection.South,
                BuildingRotation.Degrees270 => GridDirection.West,
                _ => throw new ArgumentOutOfRangeException(nameof(rotation), rotation, null)
            };
        }
    }
}
