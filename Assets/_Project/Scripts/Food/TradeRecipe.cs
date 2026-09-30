using System;
using UnityEngine;

namespace CozyFoodFactory.Food
{
    [Serializable]
    public sealed class TradeRecipe
    {
        [SerializeField] private string id;
        [SerializeField] private string villageId;
        [SerializeField] private FoodItemData input;
        [SerializeField, Min(1)] private int inputQuantity = 3;
        [SerializeField] private FoodItemData output;
        [SerializeField, Min(1)] private int outputQuantity = 2;
        public string Id => id;
        public string VillageId => villageId;
        public FoodItemData Input => input;
        public FoodItemData Output => output;
        public int InputQuantity => inputQuantity;
        public int OutputQuantity => outputQuantity;
        public string Label => $"{input.Id} x{inputQuantity} → {output.Id} x{outputQuantity}";
        public TradeRecipe(string id, string villageId, FoodItemData input,
            int inputQuantity, FoodItemData output, int outputQuantity)
        {
            this.id = id; this.villageId = villageId; this.input = input;
            this.inputQuantity = inputQuantity; this.output = output;
            this.outputQuantity = outputQuantity;
            Validate();
        }
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(villageId) ||
                input?.IsValid != true || output?.IsValid != true ||
                inputQuantity < 1 || outputQuantity < 1)
                throw new ArgumentException("Invalid village trade.");
        }
    }

    [Serializable]
    public sealed class SavedTradeBuilding
    {
        public string tradeId;
        public int bufferedInput;
        public int pendingOutput;
    }
}
