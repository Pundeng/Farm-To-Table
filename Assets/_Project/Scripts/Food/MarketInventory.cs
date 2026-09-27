using System;
using System.Collections.Generic;

namespace CozyFoodFactory.Food
{
    public sealed class MarketInventory
    {
        private readonly Dictionary<FoodItemData, int> deliveredCounts = new();

        public IReadOnlyDictionary<FoodItemData, int> DeliveredCounts => deliveredCounts;

        public int TotalDelivered { get; private set; }

        public long Currency { get; private set; }

        public bool TrySpendCurrency(long amount)
        {
            if (amount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            if (Currency < amount)
            {
                return false;
            }

            Currency -= amount;
            return true;
        }

        public int GetDeliveredCount(FoodItemData food)
        {
            if (food == null)
            {
                throw new ArgumentNullException(nameof(food));
            }

            return deliveredCounts.TryGetValue(food, out int count) ? count : 0;
        }

        public int RecordDelivery(FoodItemData food)
        {
            if (food == null)
            {
                throw new ArgumentNullException(nameof(food));
            }

            if (!food.IsValid)
            {
                throw new ArgumentException("The delivered food is invalid.", nameof(food));
            }

            int count = GetDeliveredCount(food) + 1;
            deliveredCounts[food] = count;
            TotalDelivered++;
            Currency += food.SellValue;
            return count;
        }

        public void Restore(long currency, IReadOnlyDictionary<FoodItemData, int> counts)
        {
            if (currency < 0 || counts == null)
            {
                throw new ArgumentException("Invalid Market inventory state.");
            }

            long total = 0;
            foreach (var entry in counts)
            {
                if (entry.Key?.IsValid != true || entry.Value <= 0)
                {
                    throw new ArgumentException("Invalid delivery count.", nameof(counts));
                }

                total += entry.Value;
            }

            if (total > int.MaxValue)
            {
                throw new ArgumentException("Too many deliveries.", nameof(counts));
            }

            deliveredCounts.Clear();
            foreach (var entry in counts)
            {
                deliveredCounts.Add(entry.Key, entry.Value);
            }

            TotalDelivered = (int)total;
            Currency = currency;
        }
    }
}
