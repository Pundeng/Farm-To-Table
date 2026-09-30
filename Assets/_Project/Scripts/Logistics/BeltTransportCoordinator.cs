using System;
using System.Collections.Generic;
using CozyFoodFactory.Grid;
using CozyFoodFactory.Food;
using UnityEngine;

namespace CozyFoodFactory.Logistics
{
    [Serializable]
    public sealed class FoodVisualDefinition
    {
        [SerializeField] private string foodId;
        [SerializeField] private Sprite sprite;

        public string FoodId => foodId;
        public Sprite Sprite => sprite;
    }

    [DefaultExecutionOrder(100)]
    public sealed class BeltTransportCoordinator : MonoBehaviour
    {
        [SerializeField] private GridSystem gridSystem = null;
        [SerializeField, Min(0.01f)] private float movementSpeed = 1f;
        [SerializeField] private FoodVisualDefinition[] foodVisuals =
            Array.Empty<FoodVisualDefinition>();

        private readonly Dictionary<BeltCell, Belt> beltViews = new();
        private BeltTransportSystem transportSystem;

        public Sprite GetFoodSprite(FoodItemData food)
        {
            if (food == null || foodVisuals == null) return null;
            foreach (FoodVisualDefinition visual in foodVisuals)
                if (visual != null && visual.Sprite != null &&
                    string.Equals(visual.FoodId, food.Id,
                        StringComparison.OrdinalIgnoreCase))
                    return visual.Sprite;
            return null;
        }

        public BeltCell RegisterBelt(Belt belt, Vector2Int cell, GridDirection direction)
        {
            BeltCell beltCell = GetSystem().AddBelt(cell, direction);
            beltViews.Add(beltCell, belt);
            return beltCell;
        }

        public void UnregisterBelt(BeltCell beltCell)
        {
            if (beltCell != null && GetSystem().RemoveBelt(beltCell))
            {
                beltViews.Remove(beltCell);
                RefreshConnections();
            }
        }

        public void RefreshConnections()
        {
            foreach (KeyValuePair<BeltCell, Belt> entry in beltViews)
            {
                if (entry.Value == null) continue;
                int incoming = 0;
                for (int index = 0; index < 4; index++)
                {
                    GridDirection direction = (GridDirection)index;
                    Vector2Int neighbor = entry.Key.Cell - direction.ToOffset();
                    if (GetSystem().TryGetBelt(neighbor, out BeltCell source) &&
                        source.HasOutput(direction) &&
                        !entry.Key.HasOutput((GridDirection)((index + 2) & 3)))
                        incoming |= BeltCell.Bit((GridDirection)((index + 2) & 3));
                }
                entry.Value.RefreshConnections(incoming);
            }
        }

        public void RegisterOutputSource(IItemOutputSource source)
        {
            GetSystem().RegisterOutputSource(source);
        }

        public void RegisterOutputPair(IItemOutputPairSource source) =>
            GetSystem().RegisterOutputPair(source);

        public void UnregisterOutputPair(IItemOutputPairSource source) =>
            GetSystem().UnregisterOutputPair(source);

        public bool CanAcceptOutputPair(Vector2Int cellA, Vector2Int cellB) =>
            GetSystem().CanAcceptOutputPair(cellA, cellB);

        public bool CanAcceptOutput(Vector2Int cell) =>
            GetSystem().CanAcceptOutput(cell);

        public void RegisterInputReceiver(IItemInputReceiver receiver)
        {
            GetSystem().RegisterInputReceiver(receiver);
        }

        public void UnregisterInputReceiver(IItemInputReceiver receiver)
        {
            GetSystem().UnregisterInputReceiver(receiver);
        }

        public void UnregisterOutputSource(IItemOutputSource source)
        {
            GetSystem().UnregisterOutputSource(source);
        }

        private void LateUpdate()
        {
            // One coordinated LateUpdate owns every belt move.
            if (!FactoryWorldLoadSession.IsReconstructing)
            {
                GetSystem().Advance(Time.deltaTime);
            }

            foreach (KeyValuePair<BeltCell, Belt> beltView in beltViews)
            {
                beltView.Value.RefreshItemVisual(gridSystem, beltView.Key);
            }
        }

        private BeltTransportSystem GetSystem()
        {
            return transportSystem ??= new BeltTransportSystem(movementSpeed);
        }

        private void OnValidate()
        {
            movementSpeed = Mathf.Max(0.01f, movementSpeed);
            transportSystem = null;
        }
    }
}
