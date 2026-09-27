using System;
using System.Reflection;
using CozyFoodFactory.Food;
using CozyFoodFactory.Logistics;
using NUnit.Framework;
using UnityEngine;

namespace CozyFoodFactory.Tests.EditMode
{
    public sealed class MarketDeliveryTests
    {
        [Test]
        public void Inventory_TracksFoodByIdAndKind()
        {
            var inventory = new MarketInventory();
            var rawApple = new FoodItemData("apple", FoodItemKind.RawIngredient);
            var processedApple = new FoodItemData("apple", FoodItemKind.ProcessedFood);

            Assert.That(inventory.RecordDelivery(rawApple), Is.EqualTo(1));
            Assert.That(inventory.RecordDelivery(new FoodItemData("apple",
                FoodItemKind.RawIngredient)), Is.EqualTo(2));
            Assert.That(inventory.RecordDelivery(processedApple), Is.EqualTo(1));
            Assert.That(inventory.RecordDelivery(new FoodItemData("pear",
                FoodItemKind.RawIngredient)), Is.EqualTo(1));
            Assert.That(inventory.GetDeliveredCount(rawApple), Is.EqualTo(2));
            Assert.That(inventory.GetDeliveredCount(processedApple), Is.EqualTo(1));
            Assert.That(inventory.TotalDelivered, Is.EqualTo(4));
            Assert.That(inventory.Currency, Is.EqualTo(4));
        }

        [Test]
        public void SellValues_AwardCurrencyPerDeliveryWithoutChangingFoodCounts()
        {
            var inventory = new MarketInventory();
            var rawApple = new FoodItemData("apple", FoodItemKind.RawIngredient, 2);
            var driedApple = new FoodItemData("Dried Apple", FoodItemKind.ProcessedFood, 5);

            Assert.That(inventory.RecordDelivery(rawApple), Is.EqualTo(1));
            Assert.That(inventory.RecordDelivery(new FoodItemData("apple",
                FoodItemKind.RawIngredient, 2)), Is.EqualTo(2));
            Assert.That(inventory.RecordDelivery(driedApple), Is.EqualTo(1));
            Assert.That(inventory.GetDeliveredCount(rawApple), Is.EqualTo(2));
            Assert.That(inventory.GetDeliveredCount(driedApple), Is.EqualTo(1));
            Assert.That(inventory.TotalDelivered, Is.EqualTo(3));
            Assert.That(inventory.Currency, Is.EqualTo(9));
            Assert.That(rawApple.SellValue, Is.EqualTo(2));
            Assert.That(driedApple.SellValue, Is.EqualTo(5));
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                new FoodItemData("unsellable", FoodItemKind.RawIngredient, 0));
        }

        [Test]
        public void Receiver_AcceptsFoodAndReportsSuccessfulDelivery()
        {
            var inventory = new MarketInventory();
            var receiver = new MarketReceiver(Vector2Int.zero, inventory);
            var apple = new FoodItemData("apple", FoodItemKind.RawIngredient);
            FoodItemData delivered = null;
            int deliveredCount = 0;
            receiver.FoodDelivered += (food, count) =>
            {
                delivered = food;
                deliveredCount = count;
            };

            Assert.That(receiver.TryAcceptItem(apple, GridDirection.East), Is.True);
            Assert.That(delivered, Is.SameAs(apple));
            Assert.That(deliveredCount, Is.EqualTo(1));
            Assert.That(inventory.TotalDelivered, Is.EqualTo(1));
            Assert.That(inventory.Currency, Is.EqualTo(1));
        }

        [Test]
        public void Receiver_RejectsNonFoodAndInvalidDirectionsWithoutCounting()
        {
            var inventory = new MarketInventory();
            var receiver = new MarketReceiver(Vector2Int.zero, inventory);
            var apple = new FoodItemData("apple", FoodItemKind.RawIngredient);

            Assert.That(receiver.TryAcceptItem(new NonFoodItem(),
                GridDirection.East), Is.False);
            Assert.That(receiver.TryAcceptItem(apple, (GridDirection)99), Is.False);
            Assert.That(inventory.TotalDelivered, Is.Zero);
            Assert.That(inventory.Currency, Is.Zero);

            var transport = new BeltTransportSystem(1f);
            BeltCell belt = transport.AddBelt(Vector2Int.left, GridDirection.East);
            belt.TryAccept(new NonFoodItem(), GridDirection.East);
            transport.RegisterInputReceiver(receiver);
            transport.Advance(1f);
            Assert.That(belt.HasItem, Is.True);
            Assert.That(inventory.TotalDelivered, Is.Zero);
            Assert.That(inventory.Currency, Is.Zero);
        }

        private sealed class NonFoodItem : ITransportItem { }

        [Test]
        public void TwoBelts_DeliverFoodToMarketInSameStep()
        {
            var inventory = new MarketInventory();
            var receiver = new MarketReceiver(Vector2Int.zero, inventory);
            var transport = new BeltTransportSystem(1f);
            BeltCell west = transport.AddBelt(Vector2Int.left, GridDirection.East);
            BeltCell east = transport.AddBelt(Vector2Int.right, GridDirection.West);
            west.TryAccept(new FoodItemData("apple", FoodItemKind.RawIngredient),
                GridDirection.East);
            east.TryAccept(new FoodItemData("apple", FoodItemKind.RawIngredient),
                GridDirection.West);
            transport.RegisterInputReceiver(receiver);

            transport.Advance(1f);

            Assert.That(receiver.AllowsConcurrentInput, Is.True);
            Assert.That(inventory.GetDeliveredCount(new FoodItemData("apple",
                FoodItemKind.RawIngredient)), Is.EqualTo(2));
            Assert.That(west.HasItem, Is.False);
            Assert.That(east.HasItem, Is.False);
            Assert.That(inventory.Currency, Is.EqualTo(2));
        }

        [Test]
        public void PlotThroughHarvesterAndBelt_DeliversAppleToMarket()
        {
            var apple = new CropDefinition("Apple",
                new FoodItemData("apple", FoodItemKind.RawIngredient, 3), 2f);
            var plot = new FarmPlotProcess(1);
            plot.SelectCrop(apple);
            plot.Advance(2f);
            var harvester = new HarvesterProcess(1f, 1);
            Assert.That(harvester.Advance(1f, plot), Is.EqualTo(1));

            var inventory = new MarketInventory();
            var receiver = new MarketReceiver(Vector2Int.right, inventory);
            var transport = new BeltTransportSystem(1f);
            BeltCell belt = transport.AddBelt(Vector2Int.zero, GridDirection.East);
            transport.RegisterOutputSource(new HarvesterSource(harvester));
            transport.RegisterInputReceiver(receiver);

            transport.Advance(0f);
            Assert.That(belt.HasItem, Is.True);
            transport.Advance(1f);

            Assert.That(belt.HasItem, Is.False);
            Assert.That(inventory.GetDeliveredCount(apple.Output), Is.EqualTo(1));
            Assert.That(inventory.TotalDelivered, Is.EqualTo(1));
            Assert.That(inventory.Currency, Is.EqualTo(3));
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
                bool taken = process.TryTakeOutput(out FoodItemData food);
                item = food;
                return taken;
            }
        }
    }

    public sealed class SeedShopTests
    {
        private static readonly FoodItemData Apple =
            new("apple", FoodItemKind.RawIngredient);

        [Test]
        public void Purchase_RequiresAvailabilityAndCurrencyThenUnlocksCropOnce()
        {
            var inventory = new MarketInventory();
            var unlocks = new UnlockState();
            var requirement = new UnlockKey(UnlockKey.SeedShopCategory, "Basil");
            var offer = new SeedShopOffer("Basil", "Basil Seeds", 3, requirement);
            var shop = new SeedShop(new[] { offer }, inventory, unlocks);

            Assert.That(shop.GetState("Basil"), Is.EqualTo(SeedShopOfferState.Locked));
            Assert.That(shop.TryPurchase("Basil"), Is.False);
            inventory.RecordDelivery(Apple);
            inventory.RecordDelivery(Apple);
            Assert.That(shop.GetState("Basil"), Is.EqualTo(SeedShopOfferState.Locked));
            Assert.That(shop.TryPurchase("Basil"), Is.False);
            Assert.That(inventory.Currency, Is.EqualTo(2));

            unlocks.Grant(requirement);
            Assert.That(shop.GetState("Basil"), Is.EqualTo(SeedShopOfferState.Available));
            Assert.That(shop.TryPurchase("Basil"), Is.False);
            Assert.That(inventory.Currency, Is.EqualTo(2));
            inventory.RecordDelivery(Apple);
            Assert.That(shop.GetState("Basil"), Is.EqualTo(SeedShopOfferState.Affordable));

            Assert.That(shop.TryPurchase("Basil"), Is.True);
            Assert.That(inventory.Currency, Is.Zero);
            Assert.That(unlocks.IsUnlocked(UnlockKey.CropCategory, "Basil"), Is.True);
            Assert.That(shop.GetState("Basil"), Is.EqualTo(SeedShopOfferState.Purchased));
            Assert.That(shop.TryPurchase("Basil"), Is.False);
            Assert.That(inventory.Currency, Is.Zero);
            Assert.That(inventory.TotalDelivered, Is.EqualTo(3));
        }

        [Test]
        public void OrderAvailabilityReward_PreservesDirectOnionUnlockAndEnablesShop()
        {
            var inventory = new MarketInventory();
            var receiver = new MarketReceiver(Vector2Int.zero, inventory);
            var unlocks = new UnlockState();
            var order = new FoodOrder("first", "First Harvest",
                new[] { new FoodOrderRequirement(Apple, 1) },
                new[]
                {
                    new UnlockKey(UnlockKey.CropCategory, "Onion"),
                    new UnlockKey(UnlockKey.SeedShopCategory, "Basil")
                });
            using var sequence = new FoodOrderSequence(new[] { order }, receiver, unlocks);
            var shop = new SeedShop(new[]
            {
                new SeedShopOffer("Basil", "Basil Seeds", 1,
                    new UnlockKey(UnlockKey.SeedShopCategory, "Basil"))
            }, inventory, unlocks);

            Assert.That(shop.GetState("Basil"), Is.EqualTo(SeedShopOfferState.Locked));
            Assert.That(receiver.TryAcceptItem(Apple, GridDirection.East), Is.True);
            Assert.That(unlocks.IsUnlocked(UnlockKey.CropCategory, "Onion"), Is.True);
            Assert.That(unlocks.IsUnlocked(UnlockKey.CropCategory, "Basil"), Is.False);
            Assert.That(shop.GetState("Basil"), Is.EqualTo(SeedShopOfferState.Affordable));
            Assert.That(shop.TryPurchase("Basil"), Is.True);
            Assert.That(inventory.Currency, Is.Zero);
            Assert.That(unlocks.IsUnlocked(UnlockKey.CropCategory, "Basil"), Is.True);
        }

        [Test]
        public void ExistingCropUnlock_CannotBePurchasedAgain()
        {
            var inventory = new MarketInventory();
            inventory.RecordDelivery(Apple);
            var unlocks = new UnlockState();
            unlocks.Grant(new UnlockKey(UnlockKey.CropCategory, "Basil"));
            var shop = new SeedShop(new[]
            {
                new SeedShopOffer("Basil", "Basil Seeds", 1)
            }, inventory, unlocks);

            Assert.That(shop.GetState("Basil"), Is.EqualTo(SeedShopOfferState.AlreadyUnlocked));
            Assert.That(shop.TryPurchase("Basil"), Is.False);
            Assert.That(inventory.Currency, Is.EqualTo(1));
        }

        [Test]
        public void PurchasedBasil_BecomesSelectableOnExistingFarmPlot()
        {
            var inventory = new MarketInventory();
            inventory.RecordDelivery(Apple);
            var unlocks = new UnlockState();
            var shop = new SeedShop(new[]
            {
                new SeedShopOffer("Basil", "Basil Seeds", 1)
            }, inventory, unlocks);
            var basil = new CropDefinition("Basil",
                new FoodItemData("basil", FoodItemKind.RawIngredient), 2f, "Basil");
            var plotObject = new GameObject("Shop gated plot");
            try
            {
                var plot = plotObject.AddComponent<FarmPlot>();
                typeof(FarmPlot).GetField("availableCrops",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(plot, new[] { basil });
                plot.Initialize(new Vector2Int(701, 701), unlocks);

                Assert.That(plot.IsCropUnlocked(basil), Is.False);
                Assert.Throws<InvalidOperationException>(() => plot.SelectCrop(basil));
                Assert.That(shop.TryPurchase("Basil"), Is.True);
                Assert.That(plot.IsCropUnlocked(basil), Is.True);
                Assert.DoesNotThrow(() => plot.SelectCrop(basil));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(plotObject);
            }
        }
    }
}
