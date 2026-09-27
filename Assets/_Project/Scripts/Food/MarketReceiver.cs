using System;
using CozyFoodFactory.Logistics;
using UnityEngine;

namespace CozyFoodFactory.Food
{
    public sealed class MarketReceiver : IItemInputReceiver
    {
        public MarketReceiver(Vector2Int inputCell, MarketInventory inventory)
        {
            InputCell = inputCell;
            Inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        }

        public event Action<FoodItemData, int> FoodDelivered;

        public Vector2Int InputCell { get; }

        public bool AllowsConcurrentInput => true;

        public MarketInventory Inventory { get; }

        public bool CanAcceptItem(ITransportItem item, GridDirection incomingDirection)
        {
            return item is FoodItemData food && food.IsValid &&
                Enum.IsDefined(typeof(GridDirection), incomingDirection);
        }

        public bool TryAcceptItem(ITransportItem item, GridDirection incomingDirection)
        {
            if (!CanAcceptItem(item, incomingDirection))
            {
                return false;
            }

            var food = (FoodItemData)item;
            int count = Inventory.RecordDelivery(food);
            FoodDelivered?.Invoke(food, count);
            return true;
        }
    }
}
