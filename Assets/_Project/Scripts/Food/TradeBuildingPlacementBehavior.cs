using System;
using System.Collections.Generic;
using CozyFoodFactory.Buildings;
using CozyFoodFactory.Logistics;
using UnityEngine;

namespace CozyFoodFactory.Food
{
    public sealed class TradeBuildingPlacementBehavior : MonoBehaviour,
        IBuildingPlacementBehavior, IBuildingPortPreviewProvider
    {
        private BeltTransportCoordinator coordinator;
        private IReadOnlyList<TradeRecipe> recipes;
        private static readonly IReadOnlyList<BuildingPortPreview> ports = new[]
        {
            new BuildingPortPreview(BuildingPortKind.Input, new Vector2(-1.4f, -1f), BuildingRotation.Degrees270),
            new BuildingPortPreview(BuildingPortKind.Input, new Vector2(-1.4f, 0f), BuildingRotation.Degrees270),
            new BuildingPortPreview(BuildingPortKind.Input, new Vector2(-1.4f, 1f), BuildingRotation.Degrees270),
            new BuildingPortPreview(BuildingPortKind.Output, new Vector2(-1f, -1.4f), BuildingRotation.Degrees180),
            new BuildingPortPreview(BuildingPortKind.Output, new Vector2(0f, -1.4f), BuildingRotation.Degrees180),
            new BuildingPortPreview(BuildingPortKind.Output, new Vector2(1f, -1.4f), BuildingRotation.Degrees180)
        };
        public IReadOnlyList<BuildingPortPreview> PortPreviews => ports;
        public void Configure(BeltTransportCoordinator transport, IReadOnlyList<TradeRecipe> trades)
        { coordinator = transport; recipes = trades; _ = new TradeProcess(trades); }
        public bool CanPlace(Vector2Int anchorCell, Vector2Int footprint, BuildingRotation rotation) =>
            coordinator != null && footprint == TradePortLayout.Footprint;
        public void InitializePlacedBuilding(GameObject buildingObject, BuildingPlacement placement) =>
            buildingObject.AddComponent<TradeBuilding>().Initialize(placement, coordinator, recipes);
    }
}
