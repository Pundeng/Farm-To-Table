using System;

namespace CozyFoodFactory.Food
{
    public sealed class BasicMixerProcess
    {
        private readonly MixingRecipeCatalog catalog;
        private readonly RecipeDiscoveryRegistry discoveries;
        private FoodItemData inputA;
        private FoodItemData inputB;
        private FoodItemData output;

        public BasicMixerProcess(MixingRecipeCatalog catalog,
            RecipeDiscoveryRegistry discoveries = null)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.discoveries = discoveries;
        }

        public FoodItemData InputA => inputA;
        public FoodItemData InputB => inputB;
        public bool HasOutput => output != null;
        public FoodItemData PeekOutput() => output;

        public void Restore(FoodItemData slotA, FoodItemData slotB,
            FoodItemData pendingOutput)
        {
            if (slotA != null && slotB != null ||
                pendingOutput != null && (slotA != null || slotB != null ||
                    !catalog.CanProduce(pendingOutput)) ||
                slotA != null && !catalog.CanStart(slotA) ||
                slotB != null && !catalog.CanStart(slotB))
            {
                throw new ArgumentException("Mixer state cannot be restored.");
            }

            inputA = slotA;
            inputB = slotB;
            output = pendingOutput;
        }

        public bool CanAccept(int slot, FoodItemData food)
        {
            if (slot is < 0 or > 1 || food == null || HasOutput ||
                (slot == 0 ? inputA : inputB) != null)
            {
                return false;
            }

            FoodItemData other = slot == 0 ? inputB : inputA;
            return other == null
                ? catalog.CanStart(food)
                : catalog.Find(food, other, out _) == ProcessingRecipeMatch.Unique;
        }

        public bool TryAccept(int slot, FoodItemData food)
        {
            if (!CanAccept(slot, food))
            {
                return false;
            }

            if (slot == 0)
            {
                inputA = food;
            }
            else
            {
                inputB = food;
            }

            if (inputA != null && inputB != null)
            {
                catalog.Find(inputA, inputB, out MixingRecipe recipe);
                output = recipe.Output;
                inputA = null;
                inputB = null;
                discoveries?.Record(recipe);
            }

            return true;
        }

        public bool TryTakeOutput(out FoodItemData food)
        {
            food = output;
            output = null;
            return food != null;
        }
    }
}
