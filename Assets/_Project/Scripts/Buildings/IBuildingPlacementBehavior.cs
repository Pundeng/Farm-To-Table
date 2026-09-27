using System.Collections.Generic;
using UnityEngine;

namespace CozyFoodFactory.Buildings
{
    public interface IBuildingPlacementBehavior
    {
        bool CanPlace(
            Vector2Int anchorCell,
            Vector2Int footprint,
            BuildingRotation rotation);

        void InitializePlacedBuilding(GameObject buildingObject, BuildingPlacement placement);
    }

    public interface IContinuousBuildingPlacementBehavior : IBuildingPlacementBehavior
    {
    }

    public interface IBuildingPortPreviewProvider
    {
        IReadOnlyList<BuildingPortPreview> PortPreviews { get; }
    }
}
