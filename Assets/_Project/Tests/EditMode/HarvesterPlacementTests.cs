using FantasyShapez.Buildings;
using FantasyShapez.Food;
using FantasyShapez.Logistics;
using NUnit.Framework;
using UnityEngine;

namespace FantasyShapez.Tests.EditMode
{
    public sealed class HarvesterPlacementTests
    {
        private static readonly Vector2Int Footprint = new(1, 2);

        [TestCase(BuildingRotation.Degrees0, 4, 7, 4, 8, 4, 9)]
        [TestCase(BuildingRotation.Degrees90, 4, 7, 5, 7, 6, 7)]
        [TestCase(BuildingRotation.Degrees180, 4, 8, 4, 7, 4, 6)]
        [TestCase(BuildingRotation.Degrees270, 5, 7, 4, 7, 3, 7)]
        public void RotatedHarvester_CoversPlotAndOutputsBeyondFreeCell(
            BuildingRotation rotation,
            int farmX, int farmY,
            int machineX, int machineY,
            int outputX, int outputY)
        {
            var anchor = new Vector2Int(4, 7);
            Vector2Int farmCell = HarvesterPlacementBehavior.GetFarmCell(
                anchor, Footprint, rotation);
            Vector2Int machineCell = farmCell + rotation.ToGridDirection().ToOffset();
            Vector2Int outputCell = machineCell + rotation.ToGridDirection().ToOffset();

            Assert.That(farmCell, Is.EqualTo(new Vector2Int(farmX, farmY)));
            Assert.That(HarvesterPlacementBehavior.GetAnchorForFarmCell(
                farmCell, Footprint, rotation), Is.EqualTo(anchor));
            Assert.That(machineCell, Is.EqualTo(new Vector2Int(machineX, machineY)));
            Assert.That(outputCell, Is.EqualTo(new Vector2Int(outputX, outputY)));

            var occupancy = new GridOccupancy();
            Assert.That(occupancy.TryRegister(nameof(FarmPlot), farmCell, Vector2Int.one,
                BuildingRotation.Degrees0, out BuildingPlacement plot), Is.True);
            Assert.That(occupancy.TryRegisterOver(nameof(Harvester), anchor, Footprint,
                rotation, farmCell, plot, out BuildingPlacement harvester), Is.True);
            Assert.That(occupancy.OccupiedCellCount, Is.EqualTo(2));
            Assert.That(occupancy.TryGetBuilding(farmCell, out BuildingPlacement top), Is.True);
            Assert.That(top, Is.SameAs(harvester));
            Assert.That(occupancy.TryGetUnderlyingBuilding(farmCell,
                out BuildingPlacement covered), Is.True);
            Assert.That(covered, Is.SameAs(plot));
            Assert.That(occupancy.CanPlace(machineCell, Vector2Int.one,
                BuildingRotation.Degrees0), Is.False);
            Assert.That(occupancy.Remove(plot), Is.False);

            Assert.That(occupancy.Remove(harvester), Is.True);
            Assert.That(occupancy.TryGetBuilding(farmCell, out top), Is.True);
            Assert.That(top, Is.SameAs(plot));
            Assert.That(occupancy.CanPlace(machineCell, Vector2Int.one,
                BuildingRotation.Degrees0), Is.True);
        }

        [Test]
        public void Harvester_RequiresFreeSecondCellAndCannotStackOnAnotherHarvester()
        {
            var anchor = Vector2Int.zero;
            var occupancy = new GridOccupancy();
            Assert.That(occupancy.TryRegister(nameof(FarmPlot), anchor, Vector2Int.one,
                BuildingRotation.Degrees0, out BuildingPlacement plot), Is.True);
            Assert.That(occupancy.TryRegister("Belt", Vector2Int.up, Vector2Int.one,
                BuildingRotation.Degrees0, out BuildingPlacement belt), Is.True);
            Assert.That(occupancy.CanPlaceOver(anchor, Footprint, BuildingRotation.Degrees0,
                anchor, plot), Is.False);
            Assert.That(occupancy.Remove(belt), Is.True);
            Assert.That(occupancy.TryRegisterOver(nameof(Harvester), anchor, Footprint,
                BuildingRotation.Degrees0, anchor, plot, out _), Is.True);
            Assert.That(occupancy.CanPlaceOver(anchor, Footprint, BuildingRotation.Degrees0,
                anchor, plot), Is.False);
        }

        [TestCase(BuildingRotation.Degrees90, true)]
        [TestCase(BuildingRotation.Degrees90, false)]
        [TestCase(BuildingRotation.Degrees270, true)]
        [TestCase(BuildingRotation.Degrees270, false)]
        public void VerticallyAdjacentPlots_AcceptIndependentHorizontalHarvesters(
            BuildingRotation rotation, bool upperFirst)
        {
            var occupancy = new GridOccupancy();
            var lowerCell = new Vector2Int(-10, 1);
            var upperCell = lowerCell + Vector2Int.up;
            Assert.That(occupancy.TryRegister(nameof(FarmPlot), lowerCell, Vector2Int.one,
                BuildingRotation.Degrees0, out BuildingPlacement lowerPlot), Is.True);
            Assert.That(occupancy.TryRegister(nameof(FarmPlot), upperCell, Vector2Int.one,
                BuildingRotation.Degrees0, out BuildingPlacement upperPlot), Is.True);

            Vector2Int firstCell = upperFirst ? upperCell : lowerCell;
            Vector2Int secondCell = upperFirst ? lowerCell : upperCell;
            BuildingPlacement firstPlot = upperFirst ? upperPlot : lowerPlot;
            BuildingPlacement secondPlot = upperFirst ? lowerPlot : upperPlot;
            Vector2Int firstAnchor = HarvesterPlacementBehavior.GetAnchorForFarmCell(
                firstCell, Footprint, rotation);
            Vector2Int secondAnchor = HarvesterPlacementBehavior.GetAnchorForFarmCell(
                secondCell, Footprint, rotation);

            Assert.That(occupancy.TryRegisterOver(nameof(Harvester), firstAnchor, Footprint,
                rotation, firstCell, firstPlot, out BuildingPlacement firstHarvester), Is.True);
            Assert.That(occupancy.TryRegisterOver(nameof(Harvester), secondAnchor, Footprint,
                rotation, secondCell, secondPlot, out BuildingPlacement secondHarvester), Is.True);

            Vector2Int direction = rotation.ToGridDirection().ToOffset();
            Assert.That(firstHarvester.OccupiedCells,
                Is.EquivalentTo(new[] { firstCell, firstCell + direction }));
            Assert.That(secondHarvester.OccupiedCells,
                Is.EquivalentTo(new[] { secondCell, secondCell + direction }));
            Assert.That(occupancy.OccupiedCellCount, Is.EqualTo(4));
            Assert.That(occupancy.TryGetBuilding(firstCell, out BuildingPlacement firstAtPlot),
                Is.True);
            Assert.That(firstAtPlot, Is.SameAs(firstHarvester));
            Assert.That(occupancy.TryGetBuilding(secondCell, out BuildingPlacement secondAtPlot),
                Is.True);
            Assert.That(secondAtPlot, Is.SameAs(secondHarvester));
            Assert.That(occupancy.TryGetUnderlyingBuilding(firstCell, out BuildingPlacement firstUnderlying),
                Is.True);
            Assert.That(firstUnderlying, Is.SameAs(firstPlot));
            Assert.That(occupancy.TryGetUnderlyingBuilding(secondCell, out BuildingPlacement secondUnderlying),
                Is.True);
            Assert.That(secondUnderlying, Is.SameAs(secondPlot));
        }
    }

    public sealed class FarmableRegionTests
    {
        [Test]
        public void Purchases_RequireCardinalAdjacencyProgressionAndFunds()
        {
            var unlocks = new UnlockState();
            var inventory = new MarketInventory();
            var regions = new RegionState(new[]
            {
                new FarmableRegion("start", "Start", Vector2Int.zero,
                    new Vector2Int(2, 2), true),
                new FarmableRegion("east", "East", new Vector2Int(2, 0),
                    new Vector2Int(2, 2), false,
                    new UnlockKey(UnlockKey.RegionAccessCategory, "east"),
                    new[] { new UnlockKey(UnlockKey.CropCategory, "Potato") }, 2),
                new FarmableRegion("diagonal", "Diagonal", new Vector2Int(2, 2),
                    Vector2Int.one, false, price: 1)
            }, unlocks);

            Assert.That(regions.GetPurchaseStatus("east", 99),
                Is.EqualTo(RegionPurchaseStatus.ProgressionLocked));
            Assert.That(regions.TryPurchase("east", inventory), Is.False);
            unlocks.Grant(new UnlockKey(UnlockKey.RegionAccessCategory, "east"));
            Assert.That(regions.GetPurchaseStatus("east", 99),
                Is.EqualTo(RegionPurchaseStatus.Available));
            Assert.That(regions.GetPurchaseStatus("diagonal", 99),
                Is.EqualTo(RegionPurchaseStatus.NotAdjacent));
            Assert.That(regions.TryPurchase("diagonal", inventory), Is.False);
            Assert.That(regions.GetPurchaseStatus("east", 0),
                Is.EqualTo(RegionPurchaseStatus.Unaffordable));
            inventory.RecordDelivery(new FoodItemData("apple",
                FoodItemKind.RawIngredient, 2));
            Assert.That(regions.TryPurchase("east", inventory), Is.True);
            Assert.That(regions.TryPurchase("east", inventory), Is.False);
            Assert.That(inventory.Currency, Is.Zero);
            Assert.That(regions.CanFarm(new Vector2Int(2, 0)), Is.True);
            Assert.That(unlocks.IsUnlocked(UnlockKey.CropCategory, "Potato"), Is.True);
            Assert.That(regions.GetPurchaseStatus("diagonal", 1),
                Is.EqualTo(RegionPurchaseStatus.Available));
        }

        [Test]
        public void RegionMapClick_DoesNotConsumePlacementOrOccupiedRestoredCells()
        {
            Assert.That(MarketPanel.ShouldConsumeRegionClick(RegionStatus.Locked,
                true, false), Is.True);
            Assert.That(MarketPanel.ShouldConsumeRegionClick(RegionStatus.Restorable,
                true, false), Is.True);
            Assert.That(MarketPanel.ShouldConsumeRegionClick(RegionStatus.Locked,
                false, true), Is.False);
            Assert.That(MarketPanel.ShouldConsumeRegionClick(RegionStatus.Restored,
                true, false), Is.False);
            Assert.That(MarketPanel.ShouldConsumeRegionClick(RegionStatus.Restored,
                false, true), Is.False);
            Assert.That(MarketPanel.ShouldConsumeRegionClick(RegionStatus.Restored,
                false, false), Is.False);
            Assert.That(MarketPanel.ShouldConsumeRegionClick(RegionStatus.Restored,
                false, false, true), Is.True);
        }

        private static RegionState CreateRegions(UnlockState unlocks)
        {
            return new RegionState(new[]
            {
                new FarmableRegion("starter", "Starter Fields",
                    new Vector2Int(-2, -1), new Vector2Int(3, 2), true),
                new FarmableRegion("east", "East Field",
                    new Vector2Int(1, -1), new Vector2Int(2, 2), false,
                    new UnlockKey(UnlockKey.RegionAccessCategory, "east"))
            }, unlocks);
        }

        [Test]
        public void Region_RestorationRequiresUnlockAndGrantsFarmableCellsOnce()
        {
            var unlocks = new UnlockState();
            RegionState regions = CreateRegions(unlocks);

            Assert.That(regions.GetStatus("starter"), Is.EqualTo(RegionStatus.Restored));
            Assert.That(regions.GetStatus("east"), Is.EqualTo(RegionStatus.Locked));
            Assert.That(regions.CanFarm(new Vector2Int(-2, -1)), Is.True);
            Assert.That(regions.CanFarm(Vector2Int.zero), Is.True);
            Assert.That(regions.CanFarm(new Vector2Int(1, 0)), Is.False);
            Assert.That(regions.CanFarm(new Vector2Int(-3, 0)), Is.False);
            Assert.That(regions.TryRestore("east"), Is.False);

            unlocks.Grant(new UnlockKey(UnlockKey.RegionAccessCategory, "east"));
            Assert.That(regions.GetStatus("east"), Is.EqualTo(RegionStatus.Restorable));
            Assert.That(regions.CanFarm(new Vector2Int(1, 0)), Is.False);
            Assert.That(regions.TryRestore("east"), Is.True);
            Assert.That(regions.GetStatus("east"), Is.EqualTo(RegionStatus.Restored));
            Assert.That(regions.CanFarm(new Vector2Int(1, 0)), Is.True);
            Assert.That(regions.CanFarm(new Vector2Int(3, 0)), Is.False);
            Assert.That(regions.TryRestore("east"), Is.False);
            Assert.That(unlocks.IsUnlocked(UnlockKey.RegionCategory, "east"), Is.True);
        }

        [Test]
        public void MarketOrderCompletion_MakesRegionRestorableWithoutRestoringIt()
        {
            var unlocks = new UnlockState();
            RegionState regions = CreateRegions(unlocks);
            var receiver = new MarketReceiver(Vector2Int.zero, new MarketInventory());
            var apple = new FoodItemData("apple", FoodItemKind.RawIngredient);
            var order = new FoodOrder("first", "First Harvest",
                new[] { new FoodOrderRequirement(apple, 1) },
                new[] { new UnlockKey(UnlockKey.RegionAccessCategory, "east") });
            using var sequence = new FoodOrderSequence(new[] { order }, receiver, unlocks);

            Assert.That(regions.GetStatus("east"), Is.EqualTo(RegionStatus.Locked));
            Assert.That(receiver.TryAcceptItem(apple, GridDirection.East), Is.True);
            Assert.That(regions.GetStatus("east"), Is.EqualTo(RegionStatus.Restorable));
            Assert.That(regions.CanFarm(new Vector2Int(1, 0)), Is.False);
            Assert.That(regions.TryRestore("east"), Is.True);
            Assert.That(regions.CanFarm(new Vector2Int(1, 0)), Is.True);
            Assert.That(receiver.Inventory.Currency, Is.EqualTo(1));
        }

        [Test]
        public void FarmPlotPlacement_AllowsOnlyRestoredFarmableCells()
        {
            var unlocks = new UnlockState();
            RegionState regions = CreateRegions(unlocks);
            var behaviorObject = new GameObject("Farm Plot Placement Test");
            try
            {
                var behavior = behaviorObject.AddComponent<FarmPlotPlacementBehavior>();
                behavior.Configure(regions);
                Assert.That(behavior.CanPlace(Vector2Int.zero, Vector2Int.one,
                    BuildingRotation.Degrees0), Is.True);
                Assert.That(behavior.CanPlace(new Vector2Int(1, 0), Vector2Int.one,
                    BuildingRotation.Degrees0), Is.False);
                Assert.That(behavior.CanPlace(new Vector2Int(-3, 0), Vector2Int.one,
                    BuildingRotation.Degrees0), Is.False);

                var occupancy = new GridOccupancy();
                Assert.That(occupancy.TryRegister(nameof(FarmPlot), Vector2Int.zero,
                    Vector2Int.one, BuildingRotation.Degrees0,
                    out BuildingPlacement existingPlot), Is.True);
                unlocks.Grant(new UnlockKey(UnlockKey.RegionAccessCategory, "east"));
                Assert.That(regions.TryRestore("east"), Is.True);

                Assert.That(behavior.CanPlace(new Vector2Int(1, 0), Vector2Int.one,
                    BuildingRotation.Degrees0), Is.True);
                Assert.That(occupancy.TryGetBuilding(Vector2Int.zero,
                    out BuildingPlacement preserved), Is.True);
                Assert.That(preserved, Is.SameAs(existingPlot));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(behaviorObject);
            }
        }
    }
}
