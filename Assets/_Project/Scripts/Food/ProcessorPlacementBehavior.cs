using System;
using System.Collections.Generic;
using CozyFoodFactory.Buildings;
using CozyFoodFactory.Logistics;
using UnityEngine;

namespace CozyFoodFactory.Food
{
    public sealed class ProcessorPlacementBehavior :
        MonoBehaviour, IBuildingPlacementBehavior, IBuildingPortPreviewProvider
    {
        [SerializeField] private BeltTransportCoordinator transportCoordinator = null;
        [SerializeField, Min(0.01f)] private float processingDuration = 1f;
        [SerializeField] private ProcessingRecipe[] recipes = Array.Empty<ProcessingRecipe>();
        private RecipeDiscoveryRegistry discoveries;

        private static readonly IReadOnlyList<BuildingPortPreview> Ports =
            new[]
            {
                new BuildingPortPreview(BuildingPortKind.Input,
                    new Vector2(-0.85f, 0.5f), BuildingRotation.Degrees270),
                new BuildingPortPreview(BuildingPortKind.Output,
                    new Vector2(-0.5f, 0.85f), BuildingRotation.Degrees0),
                new BuildingPortPreview(BuildingPortKind.PropertyInput,
                    new Vector2(0.5f, 0.15f), BuildingRotation.Degrees180)
            };

        public IReadOnlyList<BuildingPortPreview> PortPreviews => Ports;

        public void Configure(BeltTransportCoordinator coordinator,
            ProcessingRecipe[] availableRecipes, float duration,
            RecipeDiscoveryRegistry discoveryRegistry = null)
        {
            transportCoordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            recipes = availableRecipes ?? throw new ArgumentNullException(nameof(availableRecipes));
            discoveries = discoveryRegistry;
            if (duration <= 0f || float.IsNaN(duration) || float.IsInfinity(duration))
            {
                throw new ArgumentOutOfRangeException(nameof(duration));
            }

            processingDuration = duration;
        }

        public bool CanPlace(Vector2Int anchorCell, Vector2Int footprint,
            BuildingRotation rotation)
        {
            return transportCoordinator != null &&
                GetComponent<BuildingPlacementController>()?.PropertySupply != null &&
                footprint == ProcessorPortLayout.Footprint;
        }

        public void InitializePlacedBuilding(GameObject buildingObject,
            BuildingPlacement placement)
        {
            PropertySupplyPlayMode supply =
                GetComponent<BuildingPlacementController>()?.PropertySupply;
            if (supply == null || transportCoordinator == null)
            {
                throw new InvalidOperationException("Processor supply and transport must be configured.");
            }

            Processor processor = buildingObject.AddComponent<Processor>();
            processor.Initialize(placement, transportCoordinator, supply,
                new ProcessingRecipeCatalog(recipes), processingDuration, discoveries);
        }

        private void OnValidate()
        {
            processingDuration = Mathf.Max(0.01f, processingDuration);
        }
    }
}
