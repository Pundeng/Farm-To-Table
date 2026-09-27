using System;
using System.Collections.Generic;
using UnityEngine;

namespace CozyFoodFactory.Buildings
{
    [Serializable]
    public sealed class BuildingPlacementOption
    {
        private static readonly IReadOnlyList<BuildingPortPreview> NoPortPreviews =
            Array.Empty<BuildingPortPreview>();

        [SerializeField] private BuildingDefinition definition = new();
        [SerializeField] private MonoBehaviour placementBehavior = null;

        public BuildingDefinition Definition => definition;

        public IBuildingPlacementBehavior PlacementBehavior =>
            placementBehavior as IBuildingPlacementBehavior;

        public bool SupportsContinuousPlacement =>
            placementBehavior is IContinuousBuildingPlacementBehavior;

        public IReadOnlyList<BuildingPortPreview> PortPreviews =>
            placementBehavior is IBuildingPortPreviewProvider provider
                ? provider.PortPreviews
                : NoPortPreviews;

        public void SetRuntimePlacementBehavior(MonoBehaviour behavior)
        {
            if (behavior is not IBuildingPlacementBehavior)
            {
                throw new ArgumentException("A placement behavior is required.", nameof(behavior));
            }

            placementBehavior = behavior;
        }

        public void Validate()
        {
            definition?.Validate();

            if (placementBehavior != null && placementBehavior is not IBuildingPlacementBehavior)
            {
                placementBehavior = null;
            }
        }
    }
}
