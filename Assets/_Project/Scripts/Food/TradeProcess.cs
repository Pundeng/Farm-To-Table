using System;
using System.Collections.Generic;
using System.Linq;

namespace CozyFoodFactory.Food
{
    // Counts refer to items owned by this building. Pending output is never discarded.
    public sealed class TradeProcess
    {
        private readonly IReadOnlyList<TradeRecipe> recipes;
        public IReadOnlyList<TradeRecipe> Recipes => recipes;
        public TradeRecipe SelectedTrade { get; private set; }
        public int BufferedInput { get; private set; }
        public int PendingOutput { get; private set; }
        public bool CanChangeTrade => BufferedInput == 0 && PendingOutput == 0;
        public TradeProcess(IReadOnlyList<TradeRecipe> recipes)
        {
            if (recipes == null) throw new ArgumentNullException(nameof(recipes));
            var copy = recipes.ToArray();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var recipe in copy)
            {
                if (recipe == null) throw new ArgumentException("Missing trade.");
                recipe.Validate();
                if (!ids.Add(recipe.Id)) throw new ArgumentException("Duplicate trade ID.");
            }
            this.recipes = Array.AsReadOnly(copy);
        }
        public bool Select(string id)
        {
            var recipe = recipes.FirstOrDefault(r => r.Id == id);
            if (recipe == null || !CanChangeTrade) return false;
            SelectedTrade = recipe;
            return true;
        }
        public bool CanAccept(FoodItemData food) => SelectedTrade != null &&
            food?.IsValid == true && SelectedTrade.Input.Equals(food) && PendingOutput == 0;
        public bool TryAccept(FoodItemData food)
        {
            if (!CanAccept(food)) return false;
            BufferedInput++;
            if (BufferedInput == SelectedTrade.InputQuantity)
            {
                BufferedInput = 0;
                PendingOutput = SelectedTrade.OutputQuantity;
            }
            return true;
        }
        public FoodItemData PeekOutput() => PendingOutput > 0 ? SelectedTrade.Output : null;
        public bool TryTakeOutput(out FoodItemData food)
        {
            food = PeekOutput();
            if (food == null) return false;
            PendingOutput--;
            return true;
        }
        public SavedTradeBuilding Capture() => new SavedTradeBuilding
        {
            tradeId = SelectedTrade?.Id, bufferedInput = BufferedInput,
            pendingOutput = PendingOutput
        };
        public void Restore(SavedTradeBuilding saved)
        {
            if (saved == null) throw new ArgumentNullException(nameof(saved));
            var recipe = recipes.FirstOrDefault(r => r.Id == saved.tradeId);
            if (saved.bufferedInput < 0 || saved.pendingOutput < 0 ||
                (string.IsNullOrEmpty(saved.tradeId)
                    ? saved.bufferedInput != 0 || saved.pendingOutput != 0
                    : recipe == null || saved.bufferedInput >= recipe.InputQuantity ||
                      saved.pendingOutput > recipe.OutputQuantity ||
                      saved.pendingOutput > 0 && saved.bufferedInput > 0))
                throw new ArgumentException("Invalid or unavailable saved trade.");
            SelectedTrade = recipe;
            BufferedInput = saved.bufferedInput;
            PendingOutput = saved.pendingOutput;
        }
    }
}
