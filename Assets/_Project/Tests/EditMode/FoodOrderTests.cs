using System;
using System.Reflection;
using FantasyShapez.Food;
using FantasyShapez.Logistics;
using NUnit.Framework;
using UnityEngine;

namespace FantasyShapez.Tests.EditMode
{
    public sealed class FoodOrderTests
    {
        private static readonly FoodItemData Apple =
            new("apple", FoodItemKind.RawIngredient);
        private static readonly FoodItemData DriedApple =
            new("Dried Apple", FoodItemKind.ProcessedFood);

        [Test]
        public void RestoringEastFieldGrantsPotatoExactlyOnce()
        {
            var unlocks = new UnlockState();
            var east = new FarmableRegion("East Field", "East Field",
                new Vector2Int(5, -4), new Vector2Int(4, 9), false,
                new UnlockKey(UnlockKey.RegionAccessCategory, "East Field"),
                new[] { new UnlockKey(UnlockKey.CropCategory, "Potato") });
            var starter = new FarmableRegion("Starter", "Starter",
                new Vector2Int(-5, -4), new Vector2Int(10, 9), true);
            var regions = new RegionState(new[] { starter, east }, unlocks);
            Assert.That(regions.TryRestore(east.Id), Is.False);
            Assert.That(unlocks.IsUnlocked(UnlockKey.CropCategory, "Potato"), Is.False);
            unlocks.Grant(new UnlockKey(UnlockKey.RegionAccessCategory, east.Id));
            Assert.That(regions.TryRestore(east.Id), Is.True);
            Assert.That(regions.TryRestore(east.Id), Is.False);
            Assert.That(unlocks.IsUnlocked(UnlockKey.CropCategory, "Potato"), Is.True);
            Assert.That(unlocks.Unlocked, Has.Count.EqualTo(4));
        }

        [Test]
        public void Sequence_AdvancesWithOnlyPostActivationDeliveries()
        {
            var receiver = new MarketReceiver(Vector2Int.zero, new MarketInventory());
            var unlocks = new UnlockState();
            var firstRequirement = new FoodOrderRequirement(Apple, 2);
            var secondRequirement = new FoodOrderRequirement(DriedApple, 1);
            var first = new FoodOrder("first", "First Order",
                new[] { firstRequirement }, new[] { new UnlockKey("crop", "Onion") });
            var second = new FoodOrder("second", "Second Order",
                new[] { secondRequirement }, new[] { new UnlockKey("machine", "Oven") });

            Assert.That(receiver.TryAcceptItem(DriedApple, GridDirection.East), Is.True);
            using var sequence = new FoodOrderSequence(new[] { first, second }, receiver, unlocks);
            Assert.That(sequence.ActiveOrder.Order, Is.SameAs(first));
            Assert.That(sequence.ActiveOrder.GetDeliveredCount(firstRequirement), Is.Zero);

            Assert.That(receiver.TryAcceptItem(Apple, GridDirection.East), Is.True);
            Assert.That(sequence.ActiveOrder.GetDeliveredCount(firstRequirement), Is.EqualTo(1));
            Assert.That(receiver.TryAcceptItem(Apple, GridDirection.East), Is.True);

            Assert.That(sequence.CompletedOrders, Has.Count.EqualTo(1));
            Assert.That(sequence.CompletedOrders[0], Is.SameAs(first));
            Assert.That(unlocks.IsUnlocked("crop", "Onion"), Is.True);
            Assert.That(sequence.ActiveOrder.Order, Is.SameAs(second));
            Assert.That(sequence.ActiveOrder.GetDeliveredCount(secondRequirement), Is.Zero);
            Assert.That(receiver.TryAcceptItem(Apple, GridDirection.East), Is.True);
            Assert.That(sequence.ActiveOrder.GetDeliveredCount(secondRequirement), Is.Zero);

            Assert.That(receiver.TryAcceptItem(DriedApple, GridDirection.East), Is.True);
            Assert.That(sequence.CompletedOrders, Has.Count.EqualTo(2));
            Assert.That(sequence.ActiveOrder, Is.Null);
            Assert.That(unlocks.IsUnlocked("crop", "Onion"), Is.True);
            Assert.That(unlocks.IsUnlocked("machine", "Oven"), Is.True);
            Assert.That(receiver.Inventory.TotalDelivered, Is.EqualTo(5));
        }

        [Test]
        public void Sequence_CompletesEachOrderAndGrantsRewardsOnce()
        {
            var receiver = new MarketReceiver(Vector2Int.zero, new MarketInventory());
            var unlocks = new UnlockState();
            var order = new FoodOrder("one", "One Delivery",
                new[] { new FoodOrderRequirement(Apple, 1) },
                new[] { new UnlockKey("crop", "Onion"), new UnlockKey("machine", "Onion") });
            using var sequence = new FoodOrderSequence(new[] { order }, receiver, unlocks);
            int completions = 0;
            int grants = 0;
            sequence.Completed += _ => completions++;
            unlocks.UnlockedContent += _ => grants++;

            Assert.That(receiver.TryAcceptItem(Apple, GridDirection.North), Is.True);
            Assert.That(receiver.TryAcceptItem(Apple, GridDirection.North), Is.True);

            Assert.That(completions, Is.EqualTo(1));
            Assert.That(grants, Is.EqualTo(2));
            Assert.That(sequence.CompletedOrders, Has.Count.EqualTo(1));
            Assert.That(unlocks.Unlocked, Has.Count.EqualTo(2));
            Assert.That(unlocks.IsUnlocked("crop", "Onion"), Is.True);
            Assert.That(unlocks.IsUnlocked("machine", "Onion"), Is.True);
            Assert.That(unlocks.IsUnlocked("region", "Onion"), Is.False);
            Assert.That(unlocks.Grant(new UnlockKey("crop", "Onion")), Is.False);
            Assert.That(grants, Is.EqualTo(2));
        }

        [Test]
        public void OrdersRequiringSameFood_NeedSeparateDeliveries()
        {
            var receiver = new MarketReceiver(Vector2Int.zero, new MarketInventory());
            var requirement = new FoodOrderRequirement(Apple, 1);
            var first = new FoodOrder("first", "First",
                new[] { requirement }, Array.Empty<UnlockKey>());
            var second = new FoodOrder("second", "Second",
                new[] { requirement }, Array.Empty<UnlockKey>());
            using var sequence = new FoodOrderSequence(new[] { first, second },
                receiver, new UnlockState());

            Assert.That(receiver.TryAcceptItem(Apple, GridDirection.North), Is.True);
            Assert.That(sequence.CompletedOrders, Has.Count.EqualTo(1));
            Assert.That(sequence.ActiveOrder.Order, Is.SameAs(second));
            Assert.That(sequence.ActiveOrder.GetDeliveredCount(requirement), Is.Zero);

            Assert.That(receiver.TryAcceptItem(Apple, GridDirection.North), Is.True);
            Assert.That(sequence.CompletedOrders, Has.Count.EqualTo(2));
            Assert.That(sequence.ActiveOrder, Is.Null);
        }

        [Test]
        public void OnionCrop_UsesSharedUnlockStateAfterOrderCompletion()
        {
            var receiver = new MarketReceiver(Vector2Int.zero, new MarketInventory());
            var unlocks = new UnlockState();
            var order = new FoodOrder("onion", "Unlock Onion",
                new[] { new FoodOrderRequirement(Apple, 1) },
                new[] { new UnlockKey("crop", "Onion") });
            using var sequence = new FoodOrderSequence(new[] { order }, receiver, unlocks);
            var apple = new CropDefinition("Apple", Apple, 2f);
            var onion = new CropDefinition("Onion",
                new FoodItemData("onion", FoodItemKind.RawIngredient), 2f, "Onion");
            var plotObject = new GameObject("Order gated plot");
            try
            {
                var plot = plotObject.AddComponent<FarmPlot>();
                typeof(FarmPlot).GetField("availableCrops",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(plot, new[] { apple, onion });
                plot.Initialize(new Vector2Int(1234, 5678), unlocks);

                Assert.That(plot.IsCropUnlocked(apple), Is.True);
                Assert.That(plot.IsCropUnlocked(onion), Is.False);
                Assert.Throws<InvalidOperationException>(() => plot.SelectCrop(onion));
                Assert.That(receiver.TryAcceptItem(Apple, GridDirection.East), Is.True);
                Assert.That(sequence.ActiveOrder, Is.Null);
                Assert.That(plot.IsCropUnlocked(onion), Is.True);
                Assert.DoesNotThrow(() => plot.SelectCrop(onion));
                Assert.That(plot.SelectedCrop, Is.SameAs(onion));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(plotObject);
            }
        }

        [Test]
        public void Order_RejectsDuplicateFoodRequirementsAndOrderIds()
        {
            Assert.Throws<InvalidOperationException>(() => new FoodOrder("duplicate",
                "Duplicate", new[]
                {
                    new FoodOrderRequirement(Apple, 1),
                    new FoodOrderRequirement(new FoodItemData("apple",
                        FoodItemKind.RawIngredient), 2)
                }, Array.Empty<UnlockKey>()));

            var receiver = new MarketReceiver(Vector2Int.zero, new MarketInventory());
            var order = new FoodOrder("same", "One",
                new[] { new FoodOrderRequirement(Apple, 1) }, Array.Empty<UnlockKey>());
            Assert.Throws<ArgumentException>(() => new FoodOrderSequence(
                new[] { order, order }, receiver, new UnlockState()));
        }
    }
}
