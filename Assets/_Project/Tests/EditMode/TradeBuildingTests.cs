using System;
using System.Reflection;
using CozyFoodFactory.Buildings;
using CozyFoodFactory.Food;
using CozyFoodFactory.Logistics;
using NUnit.Framework;
using UnityEngine;

namespace CozyFoodFactory.Tests
{
    public sealed class TradeBuildingTests
    {
        private static readonly FoodItemData Food = new("Dried Apple", FoodItemKind.ProcessedFood);
        private static readonly FoodItemData Egg = new("Egg", FoodItemKind.RawIngredient);
        private static readonly FoodItemData Milk = new("Milk", FoodItemKind.RawIngredient);
        private static TradeRecipe[] Recipes => new[]
        {
            new TradeRecipe("egg", "demo", Food, 3, Egg, 2),
            new TradeRecipe("milk", "demo", Food, 3, Milk, 2)
        };

        [Test]
        public void RequiresSelectionAndRejectsWrongInputWithoutChangingCounts()
        {
            var process = new TradeProcess(Recipes);
            Assert.That(process.TryAccept(Food), Is.False);
            Assert.That(process.Select("egg"), Is.True);
            Assert.That(process.TryAccept(Milk), Is.False);
            Assert.That(process.BufferedInput, Is.Zero);
            Assert.That(process.PendingOutput, Is.Zero);
        }

        [Test]
        public void ConvertsExactBatchAndRetainsOutputUntilTaken()
        {
            var process = new TradeProcess(Recipes);
            process.Select("egg");
            process.TryAccept(Food); process.TryAccept(Food);
            Assert.That(process.BufferedInput, Is.EqualTo(2));
            Assert.That(process.PendingOutput, Is.Zero);
            Assert.That(process.Select("milk"), Is.False);
            process.TryAccept(Food);
            Assert.That(process.BufferedInput, Is.Zero);
            Assert.That(process.PendingOutput, Is.EqualTo(2));
            Assert.That(process.TryAccept(Food), Is.False);
            Assert.That(process.Select("milk"), Is.False);
            Assert.That(process.PeekOutput(), Is.EqualTo(Egg));
            Assert.That(process.PeekOutput(), Is.EqualTo(Egg));
            Assert.That(process.PendingOutput, Is.EqualTo(2));
            Assert.That(process.TryTakeOutput(out var first), Is.True);
            Assert.That(first, Is.EqualTo(Egg));
            Assert.That(process.TryTakeOutput(out var second), Is.True);
            Assert.That(second, Is.EqualTo(Egg));
            Assert.That(process.TryTakeOutput(out _), Is.False);
            Assert.That(process.Select("milk"), Is.True);
            for (int i = 0; i < 3; i++) process.TryAccept(Food);
            Assert.That(process.PeekOutput(), Is.EqualTo(Milk));
        }

        [Test]
        public void ChickenCampaignTradeConvertsOneGardenLunchIntoTwoEggs()
        {
            var lunch = new FoodItemData("Garden Lunch", FoodItemKind.ProcessedFood);
            var egg = new FoodItemData("Egg", FoodItemKind.RawIngredient);
            var recipe = new TradeRecipe("chicken-garden-egg", "chicken-village",
                lunch, 1, egg, 2);
            var process = new TradeProcess(new[] { recipe });
            Assert.That(process.Select("chicken-garden-egg"), Is.True);
            Assert.That(process.TryAccept(new FoodItemData("Loaded Fries",
                FoodItemKind.ProcessedFood)), Is.False);
            Assert.That(process.TryAccept(lunch), Is.True);
            Assert.That(process.BufferedInput, Is.Zero);
            Assert.That(process.PendingOutput, Is.EqualTo(2));
            Assert.That(process.PeekOutput(), Is.EqualTo(egg));
            SavedTradeBuilding saved = process.Capture();
            var restored = new TradeProcess(new[] { recipe });
            restored.Restore(saved);
            Assert.That(restored.PendingOutput, Is.EqualTo(2));
            Assert.That(restored.TryTakeOutput(out FoodItemData first), Is.True);
            Assert.That(first, Is.EqualTo(egg));
            Assert.That(restored.TryTakeOutput(out FoodItemData second), Is.True);
            Assert.That(second, Is.EqualTo(egg));
            Assert.That(restored.TryTakeOutput(out _), Is.False);
        }

        [TestCase(2, 0)]
        [TestCase(0, 2)]
        [TestCase(0, 1)]
        public void RestoresPartialBatchAndUndeliveredOutput(int input, int output)
        {
            var process = new TradeProcess(Recipes);
            process.Restore(new SavedTradeBuilding
                { tradeId = "egg", bufferedInput = input, pendingOutput = output });
            var restored = new TradeProcess(Recipes);
            restored.Restore(process.Capture());
            Assert.That(restored.BufferedInput, Is.EqualTo(input));
            Assert.That(restored.PendingOutput, Is.EqualTo(output));
            Assert.That(restored.SelectedTrade.Id, Is.EqualTo("egg"));
            Assert.That(restored.CanChangeTrade, Is.False);
        }

        [Test]
        public void RejectsInvalidSaveAndDuplicateTradeIds()
        {
            var process = new TradeProcess(Recipes);
            Assert.Throws<ArgumentException>(() => process.Restore(
                new SavedTradeBuilding { tradeId = "missing" }));
            Assert.Throws<ArgumentException>(() => process.Restore(
                new SavedTradeBuilding { tradeId = "egg", bufferedInput = 3 }));
            Assert.Throws<ArgumentException>(() => process.Restore(
                new SavedTradeBuilding { tradeId = "egg", pendingOutput = 3 }));
            Assert.Throws<ArgumentException>(() => process.Restore(
                new SavedTradeBuilding { tradeId = "egg", bufferedInput = 1, pendingOutput = 1 }));
            var recipe = Recipes[0];
            Assert.Throws<ArgumentException>(() => new TradeProcess(new[] { recipe, recipe }));
        }

        [Test]
        public void SnapshotRoundTripPreservesTradeAndExistingBuildingNulls()
        {
            var world = new FactoryWorldData { buildings = new[]
            {
                new SavedBuilding { definitionId = nameof(TradeBuilding),
                    tradeBuilding = new SavedTradeBuilding { tradeId = "egg", pendingOutput = 1 } },
                new SavedBuilding { definitionId = nameof(Belt), x = 6, belt = new SavedBelt() }
            } };
            var parsed = JsonUtility.FromJson<FactoryWorldData>(JsonUtility.ToJson(world));
            FactoryWorldSnapshotValidator.RestoreSerializedNulls(parsed);
            Assert.DoesNotThrow(() => FactoryWorldSnapshotValidator.Validate(parsed));
            Assert.That(parsed.buildings[0].tradeBuilding.pendingOutput, Is.EqualTo(1));
            Assert.That(parsed.buildings[0].belt, Is.Null);
            Assert.That(parsed.buildings[1].tradeBuilding, Is.Null);
        }

        [TestCase(BuildingRotation.Degrees0)]
        [TestCase(BuildingRotation.Degrees90)]
        [TestCase(BuildingRotation.Degrees180)]
        [TestCase(BuildingRotation.Degrees270)]
        public void RealBeltPortsHandleWrongFoodAndBlockedOutputs(BuildingRotation rotation)
        {
            var host = new GameObject("Trade transport test");
            var machine = new GameObject("Trade test");
            try
            {
                var coordinator = host.AddComponent<BeltTransportCoordinator>();
                var trade = machine.AddComponent<TradeBuilding>();
                Vector2Int anchor = new(10, 10);
                trade.Initialize(new BuildingPlacement(nameof(TradeBuilding), anchor,
                    TradePortLayout.Footprint, rotation), coordinator, Recipes);
                trade.Process.Select("egg");
                var system = (BeltTransportSystem)typeof(BeltTransportCoordinator)
                    .GetField("transportSystem", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(coordinator);
                GridDirection incoming = TradePortLayout.Rotate(GridDirection.East, rotation);
                var inputs = new BeltCell[3];
                for (int i = 0; i < 3; i++)
                {
                    inputs[i] = system.AddBelt(TradePortLayout.InputCell(anchor, rotation, i) -
                        incoming.ToOffset(), incoming);
                    inputs[i].TryAccept(i == 0 ? Milk : Food, incoming);
                }
                system.Advance(2f);
                Assert.That(inputs[0].HasItem, Is.True, "Wrong food remains on its Belt.");
                Assert.That(trade.Process.BufferedInput, Is.EqualTo(1), "Shared input reservation.");
                system.Advance(2f);
                Assert.That(trade.Process.BufferedInput, Is.EqualTo(2));
                Assert.That(trade.CanRemove, Is.False);
                system.RemoveBelt(inputs[1]);
                inputs[1] = system.AddBelt(TradePortLayout.InputCell(anchor, rotation, 1) -
                    incoming.ToOffset(), incoming);
                inputs[1].TryAccept(Food, incoming);
                system.Advance(2f);
                Assert.That(trade.Process.PendingOutput, Is.EqualTo(2));
                Assert.That(trade.OutputBlocked, Is.True);
                system.Advance(2f);
                Assert.That(trade.Process.PendingOutput, Is.EqualTo(2));
                GridDirection outward = TradePortLayout.Rotate(GridDirection.South, rotation);
                // Test the third exit independently; the first two may be absent.
                BeltCell exit = system.AddBelt(TradePortLayout.OutputCell(anchor, rotation, 2), outward);
                system.Advance(0f);
                Assert.That(exit.Item.Item, Is.EqualTo(Egg));
                Assert.That(trade.Process.PendingOutput, Is.EqualTo(1));
                system.Advance(0f);
                Assert.That(trade.Process.PendingOutput, Is.EqualTo(1), "Occupied exit retains output.");
                system.RemoveBelt(exit);
                exit = system.AddBelt(TradePortLayout.OutputCell(anchor, rotation, 2), outward);
                system.Advance(0f);
                Assert.That(exit.Item.Item, Is.EqualTo(Egg));
                Assert.That(trade.Process.PendingOutput, Is.Zero);
                Assert.That(trade.CanRemove, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(machine);
                UnityEngine.Object.DestroyImmediate(host);
            }
        }
    }
}
