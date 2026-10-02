using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using CozyFoodFactory.Buildings;
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
            foreach (IItemInputReceiver input in receiver.InputReceivers)
                transport.RegisterInputReceiver(input);
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
            MarketInputPort westPort = receiver.Ports.First(port => port.Side == GridDirection.West);
            MarketInputPort eastPort = receiver.Ports.First(port => port.Side == GridDirection.East);
            BeltCell west = transport.AddBelt(westPort.ExternalCell, westPort.IncomingDirection);
            BeltCell east = transport.AddBelt(eastPort.ExternalCell, eastPort.IncomingDirection);
            west.TryAccept(new FoodItemData("apple", FoodItemKind.RawIngredient),
                westPort.IncomingDirection);
            east.TryAccept(new FoodItemData("apple", FoodItemKind.RawIngredient),
                eastPort.IncomingDirection);
            foreach (IItemInputReceiver input in receiver.InputReceivers)
                transport.RegisterInputReceiver(input);

            transport.Advance(1f);

            Assert.That(receiver.InputReceivers.All(input => input.AllowsConcurrentInput), Is.True);
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
            var receiver = new MarketReceiver(new Vector2Int(1, -2), inventory);
            var transport = new BeltTransportSystem(1f);
            BeltCell belt = transport.AddBelt(Vector2Int.zero, GridDirection.East);
            transport.RegisterOutputSource(new HarvesterSource(harvester));
            foreach (IItemInputReceiver input in receiver.InputReceivers)
                transport.RegisterInputReceiver(input);

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
        [TestCase(0, 0)]
        [TestCase(17, -9)]
        [TestCase(-21, -13)]
        public void PerimeterLayoutHasTwelveUniqueExternalLanesAndNoCentralInput(int x, int y)
        {
            Vector2Int anchor = new(x, y);
            MarketInputPort[] ports = MarketPortLayout.Generate(anchor);
            Assert.That(MarketPortLayout.Footprint, Is.EqualTo(new Vector2Int(5, 5)));
            Assert.That(ports.Length, Is.EqualTo(12));
            Assert.That(ports.Select(port => port.ExternalCell).Distinct().Count(), Is.EqualTo(12));
            foreach (GridDirection side in Enum.GetValues(typeof(GridDirection)))
                Assert.That(ports.Count(port => port.Side == side), Is.EqualTo(3));
            foreach (MarketInputPort port in ports)
            {
                Vector2Int local = port.EdgeCell - anchor;
                Assert.That(local.x, Is.InRange(0, 4));
                Assert.That(local.y, Is.InRange(0, 4));
                Assert.That(local.x == 0 || local.x == 4 || local.y == 0 || local.y == 4, Is.True);
                Assert.That(local.x == 0 || local.x == 4
                    ? local.y >= 1 && local.y <= 3
                    : local.x >= 1 && local.x <= 3, Is.True);
                Assert.That(port.ExternalCell + port.IncomingDirection.ToOffset(), Is.EqualTo(port.EdgeCell));
            }
            foreach (Vector2Int corner in new[]
                { Vector2Int.zero, new Vector2Int(4, 0), new Vector2Int(0, 4), new Vector2Int(4, 4) })
                Assert.That(ports.Any(port => port.EdgeCell - anchor == corner), Is.False);
            var receiver = new MarketReceiver(anchor, new MarketInventory());
            Assert.That(receiver.InputReceivers.Select(input => input.InputCell).Distinct().Count(), Is.EqualTo(12));
            Assert.That(receiver.InputReceivers.Count, Is.EqualTo(12));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)]
        [TestCase(8)] [TestCase(9)] [TestCase(10)] [TestCase(11)]
        public void EveryPerimeterLaneDeliversExactlyOnce(int index)
        {
            var inventory = new MarketInventory();
            var receiver = new MarketReceiver(new Vector2Int(20, -12), inventory);
            var transport = RegisterMarket(receiver);
            MarketInputPort port = receiver.Ports[index];
            BeltCell belt = transport.AddBelt(port.ExternalCell, port.IncomingDirection);
            var food = new FoodItemData("lane-food", FoodItemKind.RawIngredient, 5);
            FoodItemData reported = null;
            int events = 0;
            receiver.FoodDelivered += (delivered, count) => { reported = delivered; events++; };
            Assert.That(belt.TryAccept(food, port.IncomingDirection), Is.True);
            transport.Advance(1f);
            transport.Advance(1f);
            Assert.That(belt.HasItem, Is.False);
            Assert.That(inventory.TotalDelivered, Is.EqualTo(1));
            Assert.That(inventory.Currency, Is.EqualTo(5));
            Assert.That(events, Is.EqualTo(1));
            Assert.That(reported, Is.SameAs(food));
        }

        [Test]
        public void WrongFacingDiagonalAndInternalBeltsDoNotDeliver()
        {
            var receiver = new MarketReceiver(Vector2Int.zero, new MarketInventory());
            var transport = RegisterMarket(receiver);
            var food = new FoodItemData("lane-food", FoodItemKind.RawIngredient);
            foreach (MarketInputPort port in receiver.Ports)
            {
                BeltCell away = transport.AddBelt(port.ExternalCell, port.Side);
                away.TryAccept(food, port.Side);
            }
            BeltCell diagonal = transport.AddBelt(new Vector2Int(-1, -1), GridDirection.East);
            diagonal.TryAccept(food, GridDirection.East);
            BeltCell unrelated = transport.AddBelt(new Vector2Int(3, 3), GridDirection.West);
            unrelated.TryAccept(food, GridDirection.West);
            BeltCell internalBelt = transport.AddBelt(Vector2Int.one, GridDirection.South);
            internalBelt.TryAccept(food, GridDirection.South);
            for (int step = 0; step < 5; step++) transport.Advance(1f);
            Assert.That(receiver.Inventory.TotalDelivered, Is.Zero);
            Assert.That(internalBelt.HasItem, Is.True);
        }

        [Test]
        public void AllTwelveLanesShareObjectivesAndCurrencyWithoutLossOrStarvation()
        {
            var inventory = new MarketInventory();
            var receiver = new MarketReceiver(new Vector2Int(7, -5), inventory);
            var carrot = new FoodItemData("carrot", FoodItemKind.RawIngredient, 2);
            var sauce = new FoodItemData("tomato-sauce", FoodItemKind.ProcessedFood, 3);
            var order = new FoodOrder("lanes", "Lanes", new[]
            {
                new FoodOrderRequirement(carrot, 50), new FoodOrderRequirement(sauce, 50)
            }, Array.Empty<UnlockKey>());
            using var sequence = new FoodOrderSequence(new[] { order }, receiver, new UnlockState());
            var transport = RegisterMarket(receiver);
            BeltCell[] belts = receiver.Ports.Select(port =>
                transport.AddBelt(port.ExternalCell, port.IncomingDirection)).ToArray();
            var deliveredOrder = new List<string>();
            receiver.FoodDelivered += (food, count) => deliveredOrder.Add(food.Id);
            for (int round = 0; round < 3; round++)
            {
                for (int index = 0; index < belts.Length; index++)
                    Assert.That(belts[index].TryAccept(index % 2 == 0 ? carrot : sauce,
                        receiver.Ports[index].IncomingDirection), Is.True);
                transport.Advance(1f);
                Assert.That(belts.All(belt => !belt.HasItem), Is.True);
            }
            transport.Advance(1f);
            Assert.That(inventory.TotalDelivered, Is.EqualTo(36));
            Assert.That(inventory.Currency, Is.EqualTo(90));
            Assert.That(sequence.ActiveOrder.GetDeliveredCount(order.Requirements[0]), Is.EqualTo(18));
            Assert.That(sequence.ActiveOrder.GetDeliveredCount(order.Requirements[1]), Is.EqualTo(18));
            Assert.That(deliveredOrder.Count, Is.EqualTo(36));
            Assert.That(deliveredOrder.Take(12), Is.EqualTo(deliveredOrder.Skip(12).Take(12)));
            Assert.That(deliveredOrder.Take(12), Is.EqualTo(deliveredOrder.Skip(24).Take(12)));
        }

        [Test]
        public void ThreeCenterLanesOnOneSideDeliverAndRemovalRebuildKeepsOtherPortsActive()
        {
            var receiver = new MarketReceiver(Vector2Int.zero, new MarketInventory());
            var transport = RegisterMarket(receiver);
            MarketInputPort[] side = receiver.Ports.Where(port => port.Side == GridDirection.North).ToArray();
            Assert.That(side.Length, Is.EqualTo(3));
            BeltCell[] belts = side.Select(port => transport.AddBelt(port.ExternalCell,
                port.IncomingDirection)).ToArray();
            var food = new FoodItemData("center-lane-food", FoodItemKind.RawIngredient);
            foreach (int index in Enumerable.Range(0, belts.Length))
                belts[index].TryAccept(food, side[index].IncomingDirection);
            transport.Advance(1f);
            Assert.That(receiver.Inventory.TotalDelivered, Is.EqualTo(3));
            Assert.That(transport.RemoveBelt(belts[0]), Is.True);
            belts[1].TryAccept(food, side[1].IncomingDirection);
            transport.Advance(1f);
            Assert.That(receiver.Inventory.TotalDelivered, Is.EqualTo(4));
            belts[0] = transport.AddBelt(side[0].ExternalCell, side[0].IncomingDirection);
            foreach (int index in Enumerable.Range(0, belts.Length))
                belts[index].TryAccept(food, side[index].IncomingDirection);
            transport.Advance(1f);
            Assert.That(receiver.Inventory.TotalDelivered, Is.EqualTo(7));
            foreach (IItemInputReceiver input in receiver.InputReceivers) transport.UnregisterInputReceiver(input);
            foreach (int index in Enumerable.Range(0, belts.Length))
                belts[index].TryAccept(food, side[index].IncomingDirection);
            transport.Advance(1f);
            Assert.That(receiver.Inventory.TotalDelivered, Is.EqualTo(7));
        }

        [Test]
        public void MarketOccupancyReservesOnlyTwentyFiveCellsAndLeavesAllExternalLanesFree()
        {
            Vector2Int anchor = new(-7, 12);
            var occupancy = new GridOccupancy();
            Assert.That(occupancy.TryRegister(nameof(Market), anchor, MarketPortLayout.Footprint,
                BuildingRotation.Degrees0, out _), Is.True);
            foreach (MarketInputPort port in MarketPortLayout.Generate(anchor))
                Assert.That(occupancy.TryRegister(nameof(Belt), port.ExternalCell, Vector2Int.one,
                    BuildingRotation.Degrees0, out _), Is.True);
            Assert.That(occupancy.TryRegister(nameof(Belt), anchor + Vector2Int.one, Vector2Int.one,
                BuildingRotation.Degrees0, out _), Is.False);
        }

        [Test]
        public void ReceiverReconstructionAndSavedBeltStatesRestoreEveryLane()
        {
            Vector2Int anchor = new(12, -23);
            var original = new MarketReceiver(anchor, new MarketInventory());
            var restored = new MarketReceiver(anchor, new MarketInventory());
            Assert.That(restored.Ports.Select(port => (port.ExternalCell, port.IncomingDirection)),
                Is.EqualTo(original.Ports.Select(port => (port.ExternalCell, port.IncomingDirection))));
            var transport = RegisterMarket(restored);
            var food = new FoodItemData("restored-food", FoodItemKind.RawIngredient, 4);
            foreach (MarketInputPort port in restored.Ports)
            {
                var state = new SavedBelt { item = SavedFood.From(food),
                    entryDirection = port.IncomingDirection, progress = 0.5f,
                    outputMask = BeltCell.Bit(port.IncomingDirection) };
                BeltCell belt = transport.AddBelt(port.ExternalCell, port.IncomingDirection);
                belt.SetOutputs(state.outputMask, state.nextOutputIndex);
                belt.RestoreItem(state.item.ToFood(), state.entryDirection, state.progress);
            }
            transport.Advance(0.5f);
            Assert.That(restored.Inventory.TotalDelivered, Is.EqualTo(12));
            Assert.That(restored.Inventory.Currency, Is.EqualTo(48));
            transport.Advance(1f);
            Assert.That(restored.Inventory.TotalDelivered, Is.EqualTo(12));
        }

        [Test]
        public void JsonWorldRoundTripDerivesPortsAndRetainsDeliveryProgress()
        {
            Vector2Int anchor = new(5, -13);
            var original = new MarketReceiver(anchor, new MarketInventory());
            var food = new FoodItemData("saved-food", FoodItemKind.RawIngredient, 4);
            var world = new FactoryWorldData
            {
                buildings = original.Ports.Select(port => new SavedBuilding
                {
                    definitionId = nameof(Belt), x = port.ExternalCell.x, y = port.ExternalCell.y,
                    rotation = (BuildingRotation)((int)port.IncomingDirection * 90),
                    belt = new SavedBelt { item = SavedFood.From(food),
                        entryDirection = port.IncomingDirection, progress = 0.5f,
                        outputMask = BeltCell.Bit(port.IncomingDirection) }
                }).ToArray()
            };
            FactoryWorldData loaded = JsonUtility.FromJson<FactoryWorldData>(JsonUtility.ToJson(world));
            FactoryWorldSnapshotValidator.RestoreSerializedNulls(loaded);
            Assert.DoesNotThrow(() => FactoryWorldSnapshotValidator.Validate(loaded));
            var receiver = new MarketReceiver(anchor, new MarketInventory());
            var transport = RegisterMarket(receiver);
            foreach (SavedBuilding saved in loaded.buildings)
            {
                BeltCell belt = transport.AddBelt(new Vector2Int(saved.x, saved.y), saved.rotation.ToGridDirection());
                belt.RestoreItem(saved.belt.item.ToFood(), saved.belt.entryDirection, saved.belt.progress);
            }
            transport.Advance(0.5f);
            Assert.That(receiver.Ports.Count, Is.EqualTo(12));
            Assert.That(receiver.Inventory.TotalDelivered, Is.EqualTo(12));
        }

        [Test]
        public void SimultaneousMarketDeliveryOrderDoesNotDependOnBeltRegistrationOrder()
        {
            string[] Run(bool reverse)
            {
                var receiver = new MarketReceiver(new Vector2Int(-12, 8), new MarketInventory());
                var transport = RegisterMarket(receiver);
                var events = new List<string>();
                receiver.FoodDelivered += (food, count) => events.Add(food.Id);
                IEnumerable<int> indices = Enumerable.Range(0, 12);
                if (reverse) indices = indices.Reverse();
                foreach (int index in indices)
                {
                    MarketInputPort port = receiver.Ports[index];
                    BeltCell belt = transport.AddBelt(port.ExternalCell, port.IncomingDirection);
                    belt.TryAccept(new FoodItemData($"lane-{index}", FoodItemKind.RawIngredient),
                        port.IncomingDirection);
                }
                transport.Advance(1f);
                Assert.That(events.Distinct().Count(), Is.EqualTo(12));
                return events.ToArray();
            }
            Assert.That(Run(true), Is.EqualTo(Run(false)));
        }

        [Test]
        public void ReconstructedObjectiveAndInventoryContinueCountingThroughAllPorts()
        {
            var food = new FoodItemData("restored-food", FoodItemKind.RawIngredient, 4);
            var inventory = new MarketInventory();
            inventory.Restore(20, new Dictionary<FoodItemData, int> { [food] = 5 });
            var receiver = new MarketReceiver(new Vector2Int(12, 9), inventory);
            var order = new FoodOrder("restored", "Restored", new[]
                { new FoodOrderRequirement(food, 50) }, Array.Empty<UnlockKey>());
            using var sequence = new FoodOrderSequence(new[] { order }, receiver, new UnlockState());
            sequence.Restore(0, new Dictionary<FoodItemData, int> { [food] = 3 });
            var transport = RegisterMarket(receiver);
            foreach (MarketInputPort port in receiver.Ports)
                transport.AddBelt(port.ExternalCell, port.IncomingDirection).TryAccept(food, port.IncomingDirection);
            transport.Advance(1f);
            transport.Advance(1f);
            Assert.That(sequence.ActiveOrder.GetDeliveredCount(order.Requirements[0]), Is.EqualTo(15));
            Assert.That(inventory.TotalDelivered, Is.EqualTo(17));
            Assert.That(inventory.Currency, Is.EqualTo(68));
        }

        private static BeltTransportSystem RegisterMarket(MarketReceiver receiver)
        {
            var transport = new BeltTransportSystem(1f);
            foreach (IItemInputReceiver input in receiver.InputReceivers)
                transport.RegisterInputReceiver(input);
            return transport;
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
