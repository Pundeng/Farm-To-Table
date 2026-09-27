using System;
using System.Collections.Generic;
using CozyFoodFactory.Buildings;
using CozyFoodFactory.Logistics;
using UnityEngine;

namespace CozyFoodFactory.Food
{
    public sealed class BasicMixerPlacementBehavior :
        MonoBehaviour, IBuildingPlacementBehavior, IBuildingPortPreviewProvider
    {
        private BeltTransportCoordinator transportCoordinator;
        private MixingRecipe[] recipes = Array.Empty<MixingRecipe>();
        private RecipeDiscoveryRegistry discoveries;

        private static readonly IReadOnlyList<BuildingPortPreview> Ports = new[]
        {
            new BuildingPortPreview(BuildingPortKind.Input,
                new Vector2(-0.85f, -0.5f), BuildingRotation.Degrees270),
            new BuildingPortPreview(BuildingPortKind.Input,
                new Vector2(-0.85f, 0.5f), BuildingRotation.Degrees270),
            new BuildingPortPreview(BuildingPortKind.Output,
                new Vector2(0.85f, 0.5f), BuildingRotation.Degrees90)
        };

        public IReadOnlyList<BuildingPortPreview> PortPreviews => Ports;

        public void Configure(BeltTransportCoordinator coordinator,
            MixingRecipe[] availableRecipes,
            RecipeDiscoveryRegistry discoveryRegistry = null)
        {
            transportCoordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            recipes = availableRecipes ?? throw new ArgumentNullException(nameof(availableRecipes));
            discoveries = discoveryRegistry;
        }

        public bool CanPlace(Vector2Int anchorCell, Vector2Int footprint,
            BuildingRotation rotation) =>
            transportCoordinator != null && footprint == BasicMixerPortLayout.Footprint;

        public void InitializePlacedBuilding(GameObject buildingObject,
            BuildingPlacement placement)
        {
            if (transportCoordinator == null)
            {
                throw new InvalidOperationException("Mixer transport must be configured.");
            }

            BasicMixer mixer = buildingObject.AddComponent<BasicMixer>();
            mixer.Initialize(placement, transportCoordinator,
                new MixingRecipeCatalog(recipes), discoveries);
        }
    }
}
