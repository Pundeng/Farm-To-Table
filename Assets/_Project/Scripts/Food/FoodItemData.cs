using System;
using CozyFoodFactory.Logistics;
using UnityEngine;

namespace CozyFoodFactory.Food
{
    public enum FoodItemKind
    {
        RawIngredient,
        ProcessedFood
    }

    [Serializable]
    public sealed class FoodItemData : ITransportItem, IEquatable<FoodItemData>
    {
        [SerializeField] private string id;
        [SerializeField] private FoodItemKind kind;
        [SerializeField, Min(1)] private int sellValue = 1;

        public FoodItemData(string id, FoodItemKind kind, int sellValue = 1)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("A food item ID is required.", nameof(id));
            }

            if (!Enum.IsDefined(typeof(FoodItemKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }

            if (sellValue <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sellValue));
            }

            this.id = id;
            this.kind = kind;
            this.sellValue = sellValue;
        }

        public string Id => id;

        public FoodItemKind Kind => kind;

        public int SellValue => sellValue;

        public bool IsValid => !string.IsNullOrWhiteSpace(id) &&
            Enum.IsDefined(typeof(FoodItemKind), kind) && sellValue > 0;

        public bool Equals(FoodItemData other)
        {
            return other != null && id == other.id && kind == other.kind;
        }

        public override bool Equals(object obj)
        {
            return obj is FoodItemData other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(id, kind);
        }

        public override string ToString()
        {
            return id;
        }
    }
}
