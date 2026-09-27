using System;
using System.Collections.Generic;
using CozyFoodFactory.Buildings;
using CozyFoodFactory.Logistics;
using UnityEngine;

namespace CozyFoodFactory.Food
{
    public sealed class HarvesterPlacementBehavior :
        MonoBehaviour, IBuildingPlacementBehavior, IBuildingPortPreviewProvider
    {
        [SerializeField] private BeltTransportCoordinator transportCoordinator = null;

        private static readonly IReadOnlyList<BuildingPortPreview> HarvesterPorts =
            new[]
            {
                new BuildingPortPreview(BuildingPortKind.Input,
                    new Vector2(0f, -0.7f), BuildingRotation.Degrees0),
                new BuildingPortPreview(BuildingPortKind.Output,
                    new Vector2(0f, 0.85f), BuildingRotation.Degrees0)
            };

        public IReadOnlyList<BuildingPortPreview> PortPreviews => HarvesterPorts;

        public static Vector2Int GetFarmCell(
            Vector2Int anchorCell,
            Vector2Int footprint,
            BuildingRotation rotation)
        {
            Vector2Int rotatedFootprint = rotation.GetRotatedFootprint(footprint);
            return rotation is BuildingRotation.Degrees0 or BuildingRotation.Degrees90
                ? anchorCell
                : anchorCell + rotatedFootprint - Vector2Int.one;
        }

        public static Vector2Int GetAnchorForFarmCell(
            Vector2Int farmCell,
            Vector2Int footprint,
            BuildingRotation rotation)
        {
            Vector2Int rotatedFootprint = rotation.GetRotatedFootprint(footprint);
            return rotation is BuildingRotation.Degrees0 or BuildingRotation.Degrees90
                ? farmCell
                : farmCell - rotatedFootprint + Vector2Int.one;
        }

        public bool CanPlace(
            Vector2Int anchorCell,
            Vector2Int footprint,
            BuildingRotation rotation)
        {
            return transportCoordinator != null &&
                FarmPlot.GetAt(GetFarmCell(anchorCell, footprint, rotation)) != null;
        }

        public void InitializePlacedBuilding(GameObject buildingObject, BuildingPlacement placement)
        {
            Harvester harvester = buildingObject.GetComponent<Harvester>();
            if (harvester == null)
            {
                throw new InvalidOperationException(
                    "The Harvester prefab must contain a Harvester component.");
            }

            harvester.Initialize(placement, transportCoordinator);
        }
    }
}
