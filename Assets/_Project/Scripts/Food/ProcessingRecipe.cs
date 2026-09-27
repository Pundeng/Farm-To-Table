using System;
using System.Collections.Generic;
using UnityEngine;

namespace CozyFoodFactory.Food
{
    [Serializable]
    public sealed class ProcessingRecipe
    {
        [SerializeField] private FoodItemData input;
        [SerializeField] private CookingProperty property;
        [SerializeField] private FoodItemData output;

        public ProcessingRecipe(FoodItemData input, CookingProperty property,
            FoodItemData output)
        {
            this.input = input ?? throw new ArgumentNullException(nameof(input));
            this.output = output ?? throw new ArgumentNullException(nameof(output));
            if (!Enum.IsDefined(typeof(CookingProperty), property))
            {
                throw new ArgumentOutOfRangeException(nameof(property), property, null);
            }

            this.property = property;
        }

        public FoodItemData Input => input;
        public CookingProperty Property => property;
        public FoodItemData Output => output;

        public bool Matches(FoodItemData food, CookingProperty suppliedProperty)
        {
            return input != null && output != null &&
                input.Equals(food) && property == suppliedProperty;
        }
    }

    public enum ProcessingRecipeMatch
    {
        None,
        Unique,
        Ambiguous
    }

    public sealed class ProcessingRecipeCatalog
    {
        private readonly IReadOnlyList<ProcessingRecipe> recipes;

        public ProcessingRecipeCatalog(IReadOnlyList<ProcessingRecipe> recipes)
        {
            this.recipes = recipes ?? throw new ArgumentNullException(nameof(recipes));
        }

        public ProcessingRecipeMatch Find(FoodItemData food,
            CookingProperty property, out ProcessingRecipe recipe)
        {
            recipe = null;
            if (food == null)
            {
                return ProcessingRecipeMatch.None;
            }

            foreach (ProcessingRecipe candidate in recipes)
            {
                if (candidate?.Matches(food, property) != true)
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
