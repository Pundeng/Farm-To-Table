using CozyFoodFactory.Food;
using NUnit.Framework;

namespace CozyFoodFactory.Tests.EditMode
{
    public sealed class FarmPlotTests
    {
        private static readonly CropDefinition Apple = new(
            "Apple", new FoodItemData("apple", FoodItemKind.RawIngredient), 2f);
        private static readonly CropDefinition Pear = new(
            "Pear", new FoodItemData("pear", FoodItemKind.RawIngredient), 3f);

        [Test]
        public void UnselectedPlot_DoesNotGrowOrHarvest()
        {
            var plot = new FarmPlotProcess(2);

            Assert.That(plot.Advance(10f), Is.Zero);
            Assert.That(plot.MatureCount, Is.Zero);
            Assert.That(plot.TryHarvest(out _), Is.False);
        }

        [Test]
        public void SelectedCrop_MaturesThenCanBeHarvested()
        {
            var plot = new FarmPlotProcess(2);
            plot.SelectCrop(Apple);

            Assert.That(plot.Advance(1.9f), Is.Zero);
            Assert.That(plot.Advance(0.1f), Is.EqualTo(1));
            Assert.That(plot.MatureCount, Is.EqualTo(1));
            Assert.That(plot.TryHarvest(out FoodItemData crop), Is.True);
            Assert.That(crop, Is.EqualTo(Apple.Output));
            Assert.That(crop, Is.Not.SameAs(Apple.Output));
        }

        [Test]
        public void ChangingCrop_ClearsMatureCropsAndGrowthProgress()
        {
            var plot = new FarmPlotProcess(3);
            plot.SelectCrop(Apple);
            plot.Advance(3f);
            Assert.That(plot.MatureCount, Is.EqualTo(1));
            Assert.That(plot.ElapsedTime, Is.EqualTo(1f));

            plot.SelectCrop(Pear);

            Assert.That(plot.MatureCount, Is.Zero);
            Assert.That(plot.ElapsedTime, Is.Zero);
            Assert.That(plot.Advance(2.9f), Is.Zero);
            Assert.That(plot.Advance(0.1f), Is.EqualTo(1));
            Assert.That(plot.TryHarvest(out FoodItemData crop), Is.True);
            Assert.That(crop, Is.EqualTo(Pear.Output));
        }

        [Test]
        public void ClearingCrop_RemovesMatureCropsAndStopsGrowth()
        {
            var plot = new FarmPlotProcess(2);
            plot.SelectCrop(Apple);
            plot.Advance(2f);

            plot.SelectCrop(null);

            Assert.That(plot.SelectedCrop, Is.Null);
            Assert.That(plot.MatureCount, Is.Zero);
            Assert.That(plot.ElapsedTime, Is.Zero);
            Assert.That(plot.Advance(20f), Is.Zero);
            Assert.That(plot.TryHarvest(out _), Is.False);
        }

        [Test]
        public void FullMatureCapacity_PausesUntilHarvestThenRestartsTiming()
        {
            var plot = new FarmPlotProcess(1);
            plot.SelectCrop(Apple);
            Assert.That(plot.Advance(5f), Is.EqualTo(1));
            Assert.That(plot.MatureCount, Is.EqualTo(1));
            Assert.That(plot.ElapsedTime, Is.Zero);
            Assert.That(plot.Advance(20f), Is.Zero);

            Assert.That(plot.TryHarvest(out FoodItemData first), Is.True);
            Assert.That(first, Is.EqualTo(Apple.Output));
            Assert.That(plot.Advance(1.9f), Is.Zero);
            Assert.That(plot.Advance(0.1f), Is.EqualTo(1));
        }

    }
}
