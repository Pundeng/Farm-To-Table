using System;
using System.Collections.Generic;
using UnityEngine;

namespace CozyFoodFactory.Food
{
    [Serializable]
    public sealed class MixingRecipe
    {
        [SerializeField] private FoodItemData ingredientA;
        [SerializeField] private FoodItemData ingredientB;
        [SerializeField] private FoodItemData output;

        public MixingRecipe(FoodItemData ingredientA, FoodItemData ingredientB,
            FoodItemData output)
        {
            this.ingredientA = ingredientA ?? throw new ArgumentNullException(nameof(ingredientA));
            this.ingredientB = ingredientB ?? throw new ArgumentNullException(nameof(ingredientB));
            this.output = output ?? throw new ArgumentNullException(nameof(output));
        }

        public FoodItemData IngredientA => ingredientA;
        public FoodItemData IngredientB => ingredientB;
        public FoodItemData Output => output;

        public bool Contains(FoodItemData food) => food != null && output != null &&
            (ingredientA?.Equals(food) == true || ingredientB?.Equals(food) == true);

        public bool Matches(FoodItemData first, FoodItemData second) =>
            first != null && second != null && ingredientA != null && ingredientB != null &&
            output != null &&
            ((ingredientA.Equals(first) && ingredientB.Equals(second)) ||
             (ingredientA.Equals(second) && ingredientB.Equals(first)));
    }

    public sealed class MixingRecipeCatalog
    {
        private readonly IReadOnlyList<MixingRecipe> recipes;

        public MixingRecipeCatalog(IReadOnlyList<MixingRecipe> recipes)
        {
            this.recipes = recipes ?? throw new ArgumentNullException(nameof(recipes));
        }

        public bool CanStart(FoodItemData food)
        {
            foreach (MixingRecipe recipe in recipes)
            {
                if (recipe?.Contains(food) == true)
                {
                    return true;
                }
            }

            return false;
        }

        public ProcessingRecipeMatch Find(FoodItemData first, FoodItemData second,
            out MixingRecipe recipe)
        {
            recipe = null;
            foreach (MixingRecipe candidate in recipes)
            {
                if (candidate?.Matches(first, second) != true)
                {
                    continue;
                }

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
