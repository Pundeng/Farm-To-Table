using System;
using UnityEngine;

namespace CozyFoodFactory.Buildings
{
    public enum BuildingRotation
    {
        Degrees0 = 0,
        Degrees90 = 90,
        Degrees180 = 180,
        Degrees270 = 270
    }

    public static class BuildingRotationExtensions
    {
        public static BuildingRotation RotateClockwise(this BuildingRotation rotation)
        {
            return rotation switch
            {
                BuildingRotation.Degrees0 => BuildingRotation.Degrees90,
                BuildingRotation.Degrees90 => BuildingRotation.Degrees180,
                BuildingRotation.Degrees180 => BuildingRotation.Degrees270,
                BuildingRotation.Degrees270 => BuildingRotation.Degrees0,
                _ => throw new ArgumentOutOfRangeException(nameof(rotation), rotation, null)
            };
        }

        public static Vector2Int GetRotatedFootprint(this BuildingRotation rotation, Vector2Int footprint)
        {
            if (footprint.x <= 0 || footprint.y <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(footprint), "Footprint dimensions must be positive.");
            }

            return rotation switch
            {
                BuildingRotation.Degrees0 or BuildingRotation.Degrees180 => footprint,
                BuildingRotation.Degrees90 or BuildingRotation.Degrees270 =>
                    new Vector2Int(footprint.y, footprint.x),
                _ => throw new ArgumentOutOfRangeException(nameof(rotation), rotation, null)
            };
        }

        public static Vector2Int RotateCell(this BuildingRotation rotation, Vector2Int cell,
            Vector2Int footprint)
        {
            rotation.GetRotatedFootprint(footprint);
            return rotation switch
            {
                BuildingRotation.Degrees0 => cell,
                BuildingRotation.Degrees90 => new Vector2Int(cell.y, footprint.x - 1 - cell.x),
                BuildingRotation.Degrees180 => new Vector2Int(footprint.x - 1 - cell.x,
                    footprint.y - 1 - cell.y),
                BuildingRotation.Degrees270 => new Vector2Int(footprint.y - 1 - cell.y, cell.x),
                _ => throw new ArgumentOutOfRangeException(nameof(rotation), rotation, null)
            };
        }
    }
}
