using System;
using CozyFoodFactory.Buildings;
using CozyFoodFactory.Logistics;
using UnityEngine;

namespace CozyFoodFactory.Food
{
    public static class TradePortLayout
    {
        public static readonly Vector2Int Footprint = new(3, 3);
        public static GridDirection Rotate(GridDirection direction, BuildingRotation rotation)
        {
            if (!Enum.IsDefined(typeof(BuildingRotation), rotation))
                throw new ArgumentOutOfRangeException(nameof(rotation));
            return (GridDirection)(((int)direction + (int)rotation / 90) % 4);
        }
        private static void Check(int index)
        {
            if (index < 0 || index > 2) throw new ArgumentOutOfRangeException(nameof(index));
        }
        public static Vector2Int InputCell(Vector2Int anchor, BuildingRotation rotation, int index)
        {
            Check(index);
            return anchor + rotation.RotateCell(new Vector2Int(0, index), Footprint);
        }
        public static Vector2Int OutputCell(Vector2Int anchor, BuildingRotation rotation, int index)
        {
            Check(index);
            return anchor + rotation.RotateCell(new Vector2Int(index, 0), Footprint) +
                Rotate(GridDirection.South, rotation).ToOffset();
        }
    }
}
