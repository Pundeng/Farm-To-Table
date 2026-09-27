using UnityEngine;

namespace CozyFoodFactory.Buildings
{
    public sealed class PlacedBuilding : MonoBehaviour
    {
        public BuildingPlacement Placement { get; private set; }

        public void Initialize(BuildingPlacement placement)
        {
            Placement = placement;
        }
    }
}
