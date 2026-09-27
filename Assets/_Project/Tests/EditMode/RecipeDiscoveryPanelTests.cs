using System;
using FantasyShapez.Food;
using FantasyShapez.UI;
using NUnit.Framework;

namespace FantasyShapez.Tests.EditMode
{
    public sealed class RecipeDiscoveryPanelTests
    {
        [Test]
        public void CurrentOrderRevealsItsRecipeAndPrerequisitesButNotFutureRecipes()
        {
            var apple = new FoodItemData("Apple", FoodItemKind.RawIngredient);
            var potato = new FoodItemData("Potato", FoodItemKind.RawIngredient);
            var dried = new FoodItemData("Dried Apple", FoodItemKind.ProcessedFood);
            var cut = new FoodItemData("Cut Potato", FoodItemKind.ProcessedFood);
            var fries = new FoodItemData("French Fries", FoodItemKind.ProcessedFood);
            var driedRecipe = new ProcessingRecipe(apple, CookingProperty.Air, dried);
            var friesRecipe = new ProcessingRecipe(cut, CookingProperty.Heat, fries);
            var cutterRecipe = new CuttingRecipe(potato, cut);
            var order = new FoodOrder("fries", "French Fries",
                new[] { new FoodOrderRequirement(fries, 2) },
                Array.Empty<UnlockKey>());
            var catalog = new RecipeBookCatalog(
                new[] { driedRecipe, friesRecipe }, Array.Empty<MixingRecipe>(),
                new[] { cutterRecipe }, Array.Empty<DiscoveredRecipe>(), order);

            Assert.That(catalog.Disclosure(new DiscoveredRecipe(friesRecipe)),
                Is.EqualTo(RecipeDisclosure.Hint));
            Assert.That(catalog.Disclosure(new DiscoveredRecipe(cutterRecipe)),
                Is.EqualTo(RecipeDisclosure.Hint));
            Assert.That(catalog.Disclosure(new DiscoveredRecipe(driedRecipe)),
                Is.EqualTo(RecipeDisclosure.Unknown));
            Assert.That(catalog.DiscoveredCount, Is.Zero);
            Assert.That(catalog.FindVisibleProducer(cut).Kind,
                Is.EqualTo(DiscoveredRecipeKind.Cutting));
        }

        [Test]
        public void ViewingOneDiscoveryLeavesAnotherNew()
        {
            var registry = new RecipeDiscoveryRegistry();
            var apple = new FoodItemData("Apple", FoodItemKind.RawIngredient);
            var dried = new FoodItemData("Dried Apple", FoodItemKind.ProcessedFood);
            var baked = new FoodItemData("Baked Apple", FoodItemKind.ProcessedFood);
            registry.Record(new ProcessingRecipe(apple, CookingProperty.Air, dried));
            registry.Record(new ProcessingRecipe(apple, CookingProperty.Heat, baked));
            Assert.That(registry.MarkViewed(registry.DiscoveredRecipes[0]), Is.True);
            Assert.That(registry.IsNew(registry.DiscoveredRecipes[0]), Is.False);
            Assert.That(registry.IsNew(registry.DiscoveredRecipes[1]), Is.True);
        }

        [Test]
        public void FirstDiscoveryShowsPopupAndBookUpdatesWithoutRepeatPopup()
        {
            var registry = new RecipeDiscoveryRegistry();
            var popups = new RecipeDiscoveryPopupQueue();
            registry.Discovered += popups.Show;
            var apple = new FoodItemData("apple", FoodItemKind.RawIngredient);
            var driedApple = new FoodItemData("Dried Apple", FoodItemKind.ProcessedFood);
            var recipe = new ProcessingRecipe(apple, CookingProperty.Air, driedApple);

            Assert.That(registry.Record(recipe), Is.True);
            Assert.That(popups.Current.Output, Is.EqualTo(driedApple));
            Assert.That(RecipeDiscoveryPanel.FormatRequirements(popups.Current),
                Is.EqualTo("Ingredient: apple; Property: Air"));
            Assert.That(registry.DiscoveredRecipes.Count, Is.EqualTo(1));

            popups.Dismiss();
            Assert.That(registry.Record(new ProcessingRecipe(apple,
                CookingProperty.Air, driedApple)), Is.False);
            Assert.That(popups.Current, Is.Null);
            Assert.That(registry.DiscoveredRecipes.Count, Is.EqualTo(1));

            var tomato = new FoodItemData("tomato", FoodItemKind.RawIngredient);
            var onion = new FoodItemData("onion", FoodItemKind.RawIngredient);
            var vegetableBase = new FoodItemData("Vegetable Base",
                FoodItemKind.ProcessedFood);
            Assert.That(registry.Record(new MixingRecipe(tomato, onion, vegetableBase)),
                Is.True);
            Assert.That(popups.Current.Output, Is.EqualTo(vegetableBase));
            Assert.That(RecipeDiscoveryPanel.FormatRequirements(popups.Current),
                Is.EqualTo("Ingredients: tomato + onion"));
            Assert.That(registry.DiscoveredRecipes.Count, Is.EqualTo(2));
        }

        [Test]
        public void DiscoveriesQueueUntilPlayerDismissesEachPopup()
        {
            var registry = new RecipeDiscoveryRegistry();
            var popups = new RecipeDiscoveryPopupQueue();
            registry.Discovered += popups.Show;
            var apple = new FoodItemData("apple", FoodItemKind.RawIngredient);
            var first = new FoodItemData("Dried Apple", FoodItemKind.ProcessedFood);
            var second = new FoodItemData("Baked Apple", FoodItemKind.ProcessedFood);

            registry.Record(new ProcessingRecipe(apple, CookingProperty.Air, first));
            registry.Record(new ProcessingRecipe(apple, CookingProperty.Heat, second));
            Assert.That(popups.Current.Output, Is.EqualTo(first));
            popups.Dismiss();
            Assert.That(popups.Current.Output, Is.EqualTo(second));
            popups.Dismiss();
            Assert.That(popups.Current, Is.Null);
        }
    }
}
