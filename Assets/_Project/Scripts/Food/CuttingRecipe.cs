using System;
using System.Collections.Generic;
using UnityEngine;

namespace FantasyShapez.Food
{
    [Serializable]
    public sealed class CuttingRecipe
    {
        [SerializeField] private FoodItemData input;
        [SerializeField] private FoodItemData output;

        public CuttingRecipe(FoodItemData input, FoodItemData output)
        {
            this.input = input ?? throw new ArgumentNullException(nameof(input));
            this.output = output ?? throw new ArgumentNullException(nameof(output));
        }

        public FoodItemData Input => input;
        public FoodItemData Output => output;
        public bool Matches(FoodItemData food) => input != null && output != null &&
            input.Equals(food);
    }

    public sealed class CuttingRecipeCatalog
    {
        private readonly IReadOnlyList<CuttingRecipe> recipes;

        public CuttingRecipeCatalog(IReadOnlyList<CuttingRecipe> recipes) =>
            this.recipes = recipes ?? throw new ArgumentNullException(nameof(recipes));

        public ProcessingRecipeMatch Find(FoodItemData food, out CuttingRecipe recipe)
        {
            recipe = null;
            if (food == null) return ProcessingRecipeMatch.None;
            foreach (CuttingRecipe candidate in recipes)
            {
                if (candidate?.Matches(food) != true) continue;
                if (recipe != null)
                {
                    recipe = null;
                    return ProcessingRecipeMatch.Ambiguous;
                }
                recipe = candidate;
            }
            return recipe == null ? ProcessingRecipeMatch.None :
                ProcessingRecipeMatch.Unique;
        }
    }
}
