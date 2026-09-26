using System;
using System.Collections.Generic;
using FantasyShapez.Buildings;
using FantasyShapez.Logistics;
using UnityEngine;

namespace FantasyShapez.Food
{
    public sealed class CutterPlacementBehavior : MonoBehaviour,
        IBuildingPlacementBehavior, IBuildingPortPreviewProvider
    {
        private BeltTransportCoordinator coordinator;
        private CuttingRecipe[] recipes = Array.Empty<CuttingRecipe>();
        private RecipeDiscoveryRegistry discoveries;
        private float duration;

        private static readonly IReadOnlyList<BuildingPortPreview> Ports = new[]
        {
            new BuildingPortPreview(BuildingPortKind.Input,
                new Vector2(0f, -0.9f), BuildingRotation.Degrees180),
            new BuildingPortPreview(BuildingPortKind.Output,
                new Vector2(-0.85f, 0.5f), BuildingRotation.Degrees270),
            new BuildingPortPreview(BuildingPortKind.Output,
                new Vector2(0.85f, 0.5f), BuildingRotation.Degrees90)
        };

        public IReadOnlyList<BuildingPortPreview> PortPreviews => Ports;

        public void Configure(BeltTransportCoordinator transport,
            CuttingRecipe[] availableRecipes, float processingDuration,
            RecipeDiscoveryRegistry registry)
        {
            coordinator = transport ?? throw new ArgumentNullException(nameof(transport));
            recipes = availableRecipes ?? throw new ArgumentNullException(nameof(availableRecipes));
            duration = processingDuration;
            discoveries = registry;
        }

        public bool CanPlace(Vector2Int anchorCell, Vector2Int footprint,
            BuildingRotation rotation) =>
            coordinator != null && footprint == CutterPortLayout.Footprint;

        public void InitializePlacedBuilding(GameObject buildingObject,
            BuildingPlacement placement)
        {
            Cutter cutter = buildingObject.AddComponent<Cutter>();
            cutter.Initialize(placement, coordinator,
                new CuttingRecipeCatalog(recipes), duration, discoveries);
        }
    }
}
