using CozyFoodFactory.Food;
using CozyFoodFactory.Logistics;
using NUnit.Framework;
using UnityEngine;

namespace CozyFoodFactory.Tests.EditMode
{
    public sealed class HarvesterTests
    {
        private static readonly CropDefinition Apple = new(
            "Apple", new FoodItemData("apple", FoodItemKind.RawIngredient), 2f);
        private static readonly CropDefinition Pear = new(
            "Pear", new FoodItemData("pear", FoodItemKind.RawIngredient), 3f);

        [Test]
        public void Harvester_RequiresMatureCropAndCollectionInterval()
        {
            var plot = new FarmPlotProcess(2);
            var harvester = new HarvesterProcess(1f, 2);

            Assert.That(harvester.Advance(5f, null), Is.Zero);
            plot.SelectCrop(Apple);
            Assert.That(harvester.Advance(5f, plot), Is.Zero);
            plot.Advance(2f);
            Assert.That(harvester.Advance(0.9f, plot), Is.Zero);
            Assert.That(harvester.Advance(0.1f, plot), Is.EqualTo(1));
            Assert.That(plot.MatureCount, Is.Zero);
            Assert.That(harvester.PeekOutput(), Is.EqualTo(Apple.Output));

            plot.Advance(2f);
            Assert.That(harvester.Advance(0.9f, plot), Is.Zero);
            Assert.That(harvester.Advance(0.1f, plot), Is.EqualTo(1));
        }

        [Test]
        public void FullBuffer_PausesCollectionUntilOutputIsTaken()
        {
            var plot = new FarmPlotProcess(3);
            plot.SelectCrop(Apple);
            plot.Advance(6f);
            var harvester = new HarvesterProcess(1f, 1);

            Assert.That(harvester.Advance(5f, plot), Is.EqualTo(1));
            Assert.That(plot.MatureCount, Is.EqualTo(2));
            Assert.That(harvester.Advance(20f, plot), Is.Zero);
            Assert.That(harvester.TryTakeOutput(out _), Is.True);
            Assert.That(harvester.Advance(0.9f, plot), Is.Zero);
            Assert.That(harvester.Advance(0.1f, plot), Is.EqualTo(1));
        }

        [Test]
        public void HarvestedCrop_TransfersToBeltOnlyWhenReceivingCellIsFree()
        {
            var plot = new FarmPlotProcess(2);
            plot.SelectCrop(Apple);
            plot.Advance(4f);
            var harvester = new HarvesterProcess(1f, 1);
            harvester.Advance(1f, plot);
            var transport = new BeltTransportSystem(1f);
            BeltCell belt = transport.AddBelt(Vector2Int.zero, GridDirection.East);
            belt.TryAccept(new FoodItemData("blocked", FoodItemKind.ProcessedFood),
                GridDirection.East);
            transport.RegisterOutputSource(new HarvesterSource(harvester));

            transport.Advance(0f);
            Assert.That(harvester.OutputCount, Is.EqualTo(1));
            Assert.That(harvester.Advance(5f, plot), Is.Zero);
            Assert.That(plot.MatureCount, Is.EqualTo(1));

            transport.RemoveBelt(belt);
            BeltCell replacement = transport.AddBelt(Vector2Int.zero, GridDirection.East);
            transport.Advance(0f);
            Assert.That(replacement.Item.Item, Is.EqualTo(Apple.Output));
            Assert.That(harvester.OutputCount, Is.Zero);
            Assert.That(harvester.Advance(1f, plot), Is.EqualTo(1));
        }

        [Test]
        public void ChangingCrop_PreservesFoodAlreadyHarvestedAndOnBelt()
        {
            var plot = new FarmPlotProcess(2);
            plot.SelectCrop(Apple);
            plot.Advance(4f);
            var harvester = new HarvesterProcess(1f, 2);
            harvester.Advance(2f, plot);
            var transport = new BeltTransportSystem(1f);
            BeltCell belt = transport.AddBelt(Vector2Int.zero, GridDirection.East);
            transport.RegisterOutputSource(new HarvesterSource(harvester));
            transport.Advance(0f);

            plot.SelectCrop(Pear);

            Assert.That(plot.MatureCount, Is.Zero);
            Assert.That(harvester.OutputCount, Is.EqualTo(1));
            Assert.That(harvester.PeekOutput(), Is.EqualTo(Apple.Output));
            Assert.That(belt.Item.Item, Is.EqualTo(Apple.Output));
        }

        private sealed class HarvesterSource : IItemOutputSource
        {
            private readonly HarvesterProcess process;

            public HarvesterSource(HarvesterProcess process)
            {
                this.process = process;
            }

            public Vector2Int OutputCell => Vector2Int.zero;
            public GridDirection OutputDirection => GridDirection.East;
            public bool HasOutput => process.HasOutput;
            public ITransportItem PeekOutput() => process.PeekOutput();

            public bool TryTakeOutput(out ITransportItem item)
            {
                bool taken = process.TryTakeOutput(out FoodItemData crop);
                item = crop;
                return taken;
            }
        }
    }
}
