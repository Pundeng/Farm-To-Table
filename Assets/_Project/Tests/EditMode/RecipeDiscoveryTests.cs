using CozyFoodFactory.Food;
using NUnit.Framework;

namespace CozyFoodFactory.Tests.EditMode
{
    public sealed class RecipeDiscoveryTests
    {
        private static readonly FoodItemData Apple =
            new("apple", FoodItemKind.RawIngredient);
        private static readonly FoodItemData Tomato =
            new("tomato", FoodItemKind.RawIngredient);
        private static readonly FoodItemData Onion =
            new("onion", FoodItemKind.RawIngredient);
        private static readonly FoodItemData DriedApple =
            new("Dried Apple", FoodItemKind.ProcessedFood);
        private static readonly FoodItemData VegetableBase =
            new("Vegetable Base", FoodItemKind.ProcessedFood);

        [Test]
        public void Processor_DiscoversOnCompletionOnceAcrossRepeatedProduction()
        {
            var registry = new RecipeDiscoveryRegistry();
            var recipe = new ProcessingRecipe(Apple, CookingProperty.Cold, DriedApple);
            var process = new ProcessorProcess(
                new ProcessingRecipeCatalog(new[] { recipe }), 1f, registry);
            int notifications = 0;
            registry.Discovered += _ => notifications++;

            Assert.That(process.TryAccept(Apple, CookingProperty.Cold, true), Is.True);
            Assert.That(registry.DiscoveredRecipes, Is.Empty);
            Assert.That(process.Advance(1f, false), Is.False);
            Assert.That(registry.DiscoveredRecipes, Is.Empty);
            Assert.That(process.Advance(1f, true), Is.True);
            Assert.That(process.Advance(1f, true), Is.False);
            Assert.That(registry.DiscoveredRecipes.Count, Is.EqualTo(1));
            Assert.That(notifications, Is.EqualTo(1));
            DiscoveredRecipe discovery = registry.DiscoveredRecipes[0];
            Assert.That(discovery.Kind, Is.EqualTo(DiscoveredRecipeKind.Processing));
            Assert.That(discovery.IngredientA, Is.EqualTo(Apple));
            Assert.That(discovery.IngredientB, Is.Null);
            Assert.That(discovery.Property, Is.EqualTo(CookingProperty.Cold));
            Assert.That(discovery.Output, Is.EqualTo(DriedApple));

            Assert.That(process.TryTakeOutput(out _), Is.True);
            Assert.That(process.TryAccept(Apple, CookingProperty.Cold, true), Is.True);
            Assert.That(process.Advance(1f, true), Is.True);
            Assert.That(registry.DiscoveredRecipes.Count, Is.EqualTo(1));
            Assert.That(notifications, Is.EqualTo(1));
        }

        [Test]
        public void Mixer_DiscoversOnSecondIngredientOnceForEitherInputOrder()
        {
            var registry = new RecipeDiscoveryRegistry();
            var recipe = new MixingRecipe(Tomato, Onion, VegetableBase);
            var process = new BasicMixerProcess(
                new MixingRecipeCatalog(new[] { recipe }), registry);
            int notifications = 0;
            registry.Discovered += _ => notifications++;

            Assert.That(process.TryAccept(1, Onion), Is.True);
            Assert.That(registry.DiscoveredRecipes, Is.Empty);
            Assert.That(process.TryAccept(0, Tomato), Is.True);
            Assert.That(registry.DiscoveredRecipes.Count, Is.EqualTo(1));
            Assert.That(notifications, Is.EqualTo(1));
            DiscoveredRecipe discovery = registry.DiscoveredRecipes[0];
            Assert.That(discovery.Kind, Is.EqualTo(DiscoveredRecipeKind.Mixing));
            Assert.That(discovery.IngredientA, Is.EqualTo(Tomato));
            Assert.That(discovery.IngredientB, Is.EqualTo(Onion));
            Assert.That(discovery.Property, Is.Null);
            Assert.That(discovery.Output, Is.EqualTo(VegetableBase));

            Assert.That(process.TryTakeOutput(out _), Is.True);
            Assert.That(process.TryAccept(0, Tomato), Is.True);
            Assert.That(process.TryAccept(1, Onion), Is.True);
            Assert.That(registry.DiscoveredRecipes.Count, Is.EqualTo(1));
            Assert.That(notifications, Is.EqualTo(1));
        }

        [Test]
        public void SharedRegistry_TracksBothMachineTypesByRecipeIdentity()
        {
            var registry = new RecipeDiscoveryRegistry();
            var processing = new ProcessorProcess(
                new ProcessingRecipeCatalog(new[]
                {
                    new ProcessingRecipe(Apple, CookingProperty.Cold, DriedApple)
                }), 1f, registry);
            var mixing = new BasicMixerProcess(
                new MixingRecipeCatalog(new[]
                {
                    new MixingRecipe(Tomato, Onion, VegetableBase)
                }), registry);

            Assert.That(processing.TryAccept(Apple, CookingProperty.Cold, true), Is.True);
            Assert.That(processing.Advance(1f, true), Is.True);
            Assert.That(mixing.TryAccept(0, Tomato), Is.True);
            Assert.That(mixing.TryAccept(1, Onion), Is.True);
            Assert.That(registry.DiscoveredRecipes.Count, Is.EqualTo(2));
            Assert.That(registry.DiscoveredRecipes[0].Kind,
                Is.EqualTo(DiscoveredRecipeKind.Processing));
            Assert.That(registry.DiscoveredRecipes[1].Kind,
                Is.EqualTo(DiscoveredRecipeKind.Mixing));

            // A separate machine can use an equivalent recipe without rediscovering it.
            var anotherMixer = new BasicMixerProcess(
                new MixingRecipeCatalog(new[]
                {
                    new MixingRecipe(Onion, Tomato, VegetableBase)
                }), registry);
            Assert.That(anotherMixer.TryAccept(0, Onion), Is.True);
            Assert.That(anotherMixer.TryAccept(1, Tomato), Is.True);
            Assert.That(registry.DiscoveredRecipes.Count, Is.EqualTo(2));
        }

        [Test]
        public void InvalidOrAmbiguousInputs_DoNotCreateDiscoveries()
        {
            var registry = new RecipeDiscoveryRegistry();
            var processingRecipe = new ProcessingRecipe(Apple, CookingProperty.Cold, DriedApple);
            var processor = new ProcessorProcess(new ProcessingRecipeCatalog(new[]
            {
                processingRecipe, processingRecipe
            }), 1f, registry);
            var mixingRecipe = new MixingRecipe(Tomato, Onion, VegetableBase);
            var mixer = new BasicMixerProcess(new MixingRecipeCatalog(new[]
            {
                mixingRecipe, mixingRecipe
            }), registry);

            Assert.That(processor.TryAccept(Apple, CookingProperty.Cold, true), Is.False);
            Assert.That(processor.Advance(1f, true), Is.False);
            Assert.That(mixer.TryAccept(0, Tomato), Is.True);
            Assert.That(mixer.TryAccept(1, Onion), Is.False);
            Assert.That(registry.DiscoveredRecipes, Is.Empty);
        }
    }
}
