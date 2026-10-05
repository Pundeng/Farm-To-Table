using System;
using System.Collections.Generic;
using CozyFoodFactory.Logistics;
using System.Linq;
using CozyFoodFactory.Buildings;
using CozyFoodFactory.Food;
using NUnit.Framework;
using UnityEngine;

namespace CozyFoodFactory.Tests.EditMode
{
    public sealed class TerritoryWorldTests
    {
        private static readonly Vector2Int MarketCell = new(2, 2);
        private static readonly Vector2Int MarketFootprint = new(5, 5);

        [Test]
        public void NineCellParcelsCenterMarketAndStartWithEightOwnedNeighbors()
        {
            var settings = new TerritoryWorldSettings();
            var territories = new TerritorySystem(settings).Initialize();
            Vector2Int marketMinimum = territories.ParcelMinimumCell(
                territories.ReservedHubTerritory);

            Assert.That(TerritoryWorldSettings.ParcelSize, Is.EqualTo(9));
            Assert.That(MarketCell - marketMinimum, Is.EqualTo(new Vector2Int(2, 2)));
            Assert.That(MarketCell + MarketFootprint - Vector2Int.one - marketMinimum,
                Is.EqualTo(new Vector2Int(6, 6)));
            Assert.That(territories.PurchasedCoordinates, Has.Count.EqualTo(8));
            CollectionAssert.AreEquivalent(
                Enumerable.Range(-1, 3).SelectMany(x => Enumerable.Range(-1, 3)
                    .Select(y => new Vector2Int(x, y)))
                    .Where(parcel => parcel != territories.ReservedHubTerritory),
                territories.PurchasedCoordinates);
        }

        [Test]
        public void HubIsReservedAndSouthParcelIsStartingOwnedLand()
        {
            var settings = new TerritoryWorldSettings();
            settings.Validate(MarketCell, MarketFootprint);
            var territories = new TerritorySystem(settings).Initialize();

            Assert.That(territories.PurchasedCoordinates, Has.Count.EqualTo(8));
            Assert.That(territories.PurchasedCoordinates, Does.Contain(new Vector2Int(1, 1)));
            CollectionAssert.DoesNotContain(territories.PurchasedCoordinates,
                Vector2Int.zero);
            Assert.That(territories.IsReservedHub(Vector2Int.zero), Is.True);
            Assert.That(territories.GetPurchaseStatus(Vector2Int.zero, 1000),
                Is.EqualTo(TerritoryPurchaseStatus.ReservedHub));
            Assert.That(territories.GetParcelVisualState(Vector2Int.zero, 1000),
                Is.EqualTo(TerritoryParcelVisualState.ReservedHub));
            Assert.That(territories.IsOwnedCell(MarketCell), Is.False);
            Assert.That(territories.IsOwnedCell(new Vector2Int(3, -4)), Is.True);
            Assert.That(territories.ContainsFootprint(new Vector2Int(3, -4),
                Vector2Int.one, BuildingRotation.Degrees0), Is.True);
            Assert.That(territories.ContainsFootprint(Vector2Int.one,
                Vector2Int.one, BuildingRotation.Degrees0), Is.False);
            Assert.That(territories.ContainsFootprint(
                new Vector2Int(1, 2), Vector2Int.one, BuildingRotation.Degrees0), Is.False);
            Assert.That(territories.ContainsBuildableFootprint(new Vector2Int(2, 1),
                Vector2Int.one, BuildingRotation.Degrees0), Is.True);
            Assert.That(territories.ContainsBuildableFootprint(new Vector2Int(2, 2),
                Vector2Int.one, BuildingRotation.Degrees0), Is.True);
            Assert.That(territories.GetParcelVisualState(new Vector2Int(0, -1), 1000),
                Is.EqualTo(TerritoryParcelVisualState.Owned));
            Assert.That(territories.GetParcelVisualState(new Vector2Int(1, -1), 1000),
                Is.EqualTo(TerritoryParcelVisualState.Owned));
            Assert.That(territories.GetParcelVisualState(new Vector2Int(2, 0), 1000),
                Is.EqualTo(TerritoryParcelVisualState.Purchasable));
            Assert.That(territories.GetParcelVisualState(new Vector2Int(2, 2), 1000),
                Is.EqualTo(TerritoryParcelVisualState.Locked));
            Assert.That(territories.GetParcelVisualState(new Vector2Int(1, -1), 0),
                Is.EqualTo(TerritoryParcelVisualState.Owned));
        }

        [Test]
        public void ReservedHubAllowsFactorySpaceWhileMarketOccupancyBlocksOnlyItsFootprint()
        {
            var settings = new TerritoryWorldSettings();
            var territories = new TerritorySystem(settings).Initialize();
            var occupancy = new GridOccupancy();
            Assert.That(occupancy.TryRegister("Market", MarketCell, MarketFootprint,
                BuildingRotation.Degrees0, out _), Is.True);
            Assert.That(occupancy.OccupiedCellCount, Is.EqualTo(25));

            Assert.That(territories.GetPurchaseStatus(Vector2Int.zero, 1000),
                Is.EqualTo(TerritoryPurchaseStatus.ReservedHub));
            Assert.That(territories.GetParcelVisualState(Vector2Int.zero, 1000),
                Is.EqualTo(TerritoryParcelVisualState.ReservedHub));
            Assert.That(territories.IsOwnedCell(new Vector2Int(0, 0)), Is.False);
            Assert.That(territories.IsBuildableCell(new Vector2Int(0, 0)), Is.True);
            Assert.That(territories.ContainsBuildableFootprint(new Vector2Int(0, 0),
                new Vector2Int(2, 2), BuildingRotation.Degrees0), Is.True);
            Assert.That(occupancy.CanPlace(new Vector2Int(0, 0),
                new Vector2Int(2, 2), BuildingRotation.Degrees0), Is.True);

            for (int x = 0; x < TerritoryWorldSettings.ParcelSize; x++)
                for (int y = 0; y < TerritoryWorldSettings.ParcelSize; y++)
                {
                    Vector2Int cell = new(x, y);
                    bool marketCell = x >= MarketCell.x &&
                        x < MarketCell.x + MarketFootprint.x &&
                        y >= MarketCell.y && y < MarketCell.y + MarketFootprint.y;
                    Assert.That(territories.IsBuildableCell(cell), Is.True);
                    Assert.That(occupancy.CanPlace(cell, Vector2Int.one,
                        BuildingRotation.Degrees0), Is.EqualTo(!marketCell));
                }

            foreach (Vector2Int cell in new[]
                {
                    new Vector2Int(0, 4), new Vector2Int(8, 4),
                    new Vector2Int(4, 0), new Vector2Int(4, 8)
                })
            {
                Assert.That(territories.IsBuildableCell(cell), Is.True);
                Assert.That(occupancy.CanPlace(cell, Vector2Int.one,
                    BuildingRotation.Degrees0), Is.True);
            }

            foreach (MarketInputPort port in MarketPortLayout.Generate(MarketCell))
            {
                Assert.That(territories.IsBuildableCell(port.ExternalCell), Is.True);
                Assert.That(occupancy.CanPlace(port.ExternalCell, Vector2Int.one,
                    BuildingRotation.Degrees0), Is.True);
            }

            Assert.That(occupancy.CanPlace(MarketCell, Vector2Int.one,
                BuildingRotation.Degrees0), Is.False);
            Assert.That(occupancy.CanPlace(new Vector2Int(1, 2),
                new Vector2Int(2, 2), BuildingRotation.Degrees0), Is.False);
            Assert.That(territories.ContainsBuildableFootprint(new Vector2Int(17, 4),
                new Vector2Int(2, 1), BuildingRotation.Degrees0), Is.False);
            Assert.That(territories.TryPurchase(Vector2Int.zero, Wallet(1000)), Is.False);
            Assert.That(new PropertyWorldGenerator(settings).Generate(11,
                Vector2Int.zero), Is.Empty);
        }

        [Test]
        public void PurchaseRequiresCardinalAdjacencyAndUsesOneGlobalPrice()
        {
            var system = NewSystem();
            MarketInventory inventory = Wallet(1000);
            Assert.That(system.NextPurchaseCost, Is.EqualTo(100));
            Assert.That(system.GetPurchaseStatus(new Vector2Int(2, 2), inventory.Currency),
                Is.EqualTo(TerritoryPurchaseStatus.NotAdjacent));
            Assert.That(system.GetPurchaseStatus(new Vector2Int(1, 3), inventory.Currency),
                Is.EqualTo(TerritoryPurchaseStatus.NotAdjacent));
            Assert.That(system.GetPurchaseStatus(Vector2Int.zero, inventory.Currency),
                Is.EqualTo(TerritoryPurchaseStatus.ReservedHub));
            Assert.That(system.TryPurchase(new Vector2Int(0, 2), inventory), Is.True);

            Assert.That(system.ExpansionCount, Is.EqualTo(1));
            Assert.That(system.NextPurchaseCost, Is.EqualTo(180));
            Assert.That(system.PurchasedCoordinates.Count, Is.EqualTo(11));
            Assert.That(system.GetPurchaseStatus(new Vector2Int(0, 3), inventory.Currency),
                Is.EqualTo(TerritoryPurchaseStatus.Available));
            Assert.That(system.GetPurchaseStatus(new Vector2Int(2, 0), inventory.Currency),
                Is.EqualTo(TerritoryPurchaseStatus.Available));
            Assert.That(system.IsOwnedCell(new Vector2Int(8, -4)), Is.True);
        }

        [Test]
        public void PriceCurveAdvancesForEveryParcelAndContinuesPastAuthoredPrices()
        {
            var system = NewSystem();
            MarketInventory inventory = Wallet(100000);
            int[] expectedCosts = { 100, 180, 320, 550, 900, 1500, 2400, 3900, 6300, 10200, 16500, 26700 };
            for (int index = 0; index < expectedCosts.Length; index++)
            {
                Assert.That(system.NextPurchaseCost, Is.EqualTo(expectedCosts[index]));
                int maxY = system.PurchasedCoordinates.Max(parcel => parcel.y);
                Assert.That(system.TryPurchase(new Vector2Int(0, maxY + 1), inventory),
                    Is.True);
            }
            Assert.That(system.NextPurchaseCost, Is.EqualTo(43200));
        }

        [Test]
        public void EntireRotatedMultiCellFootprintMustBeOwned()
        {
            var system = NewSystem();
            Assert.That(system.ContainsFootprint(new Vector2Int(16, -8),
                new Vector2Int(2, 1), BuildingRotation.Degrees0), Is.True);
            Assert.That(system.ContainsFootprint(new Vector2Int(17, -8),
                new Vector2Int(2, 1), BuildingRotation.Degrees0), Is.False);
            Assert.That(system.ContainsFootprint(new Vector2Int(17, -8),
                new Vector2Int(2, 1), BuildingRotation.Degrees90), Is.True);
        }

        [Test]
        public void TerritorySaveRestorePreservesOwnershipPurchaseCountAndNextCost()
        {
            var source = NewSystem();
            MarketInventory wallet = Wallet(1000);
            Assert.That(source.TryPurchase(new Vector2Int(0, 2), wallet), Is.True);
            Assert.That(source.TryPurchase(new Vector2Int(2, 0), wallet), Is.True);
            var world = new FactoryWorldData
            {
                worldSeed = source.WorldSeed,
                territoryGenerationVersion = TerritoryWorldSettings.GenerationVersion,
                territoryPurchaseCount = source.ExpansionCount,
                reservedHubTerritory = new SavedTerritory(source.ReservedHubTerritory),
                startingTerritory = new SavedTerritory(source.StartingTerritory),
                purchasedTerritories = source.Capture()
            };
            FactoryWorldData loaded = JsonUtility.FromJson<FactoryWorldData>(
                JsonUtility.ToJson(world));
            Assert.That(loaded.reservedHubTerritory.Coordinate,
                Is.EqualTo(Vector2Int.zero));
            Assert.That(loaded.startingTerritory.Coordinate,
                Is.EqualTo(new Vector2Int(0, -1)));

            var restored = NewSystem();
            restored.Restore(loaded.worldSeed, loaded.purchasedTerritories,
                loaded.territoryPurchaseCount);

            Assert.That(restored.PurchasedCoordinates,
                Is.EquivalentTo(source.PurchasedCoordinates));
            Assert.That(restored.ExpansionCount, Is.EqualTo(2));
            Assert.That(restored.NextPurchaseCost, Is.EqualTo(320));
            Assert.That(restored.IsOwnedCell(new Vector2Int(2, -9)), Is.True);
            Assert.That(restored.GetParcelVisualState(Vector2Int.zero, 0),
                Is.EqualTo(TerritoryParcelVisualState.ReservedHub));
            Assert.That(restored.GetParcelVisualState(new Vector2Int(1, -1), 0),
                Is.EqualTo(TerritoryParcelVisualState.Owned));
            Assert.That(restored.GetParcelVisualState(new Vector2Int(0, -2), 500),
                Is.EqualTo(TerritoryParcelVisualState.Purchasable));
            Assert.That(restored.GetParcelVisualState(new Vector2Int(0, -4), 500),
                Is.EqualTo(TerritoryParcelVisualState.Locked));
            Assert.That(restored.IsBuildableCell(new Vector2Int(0, 0)), Is.True);
            Assert.That(restored.ContainsBuildableFootprint(new Vector2Int(0, 0),
                new Vector2Int(2, 2), BuildingRotation.Degrees0), Is.True);
        }

        [Test]
        public void SameSeedAndParcelReproduceSourcesAndOtherSeedsCanVary()
        {
            var generator = NewGenerator();
            var parcel = new Vector2Int(2, -1);
            var first = generator.Generate(12345, parcel);
            var repeated = generator.Generate(12345, parcel);
            var otherSeed = generator.Generate(98765, parcel);

            Assert.That(Describe(repeated), Is.EqualTo(Describe(first)));
            Assert.That(Describe(otherSeed), Is.Not.EqualTo(Describe(first)));
        }

        [Test]
        public void HeatAndWaterClustersAreGuaranteedInEarlyExpansionParcels()
        {
            var settings = new TerritoryWorldSettings();
            var generator = NewGenerator(settings);
            var territories = new TerritorySystem(settings).Initialize();
            PropertySourceSetup[] heat = generator.Generate(17,
                new Vector2Int(1, -1));
            PropertySourceSetup[] water = generator.Generate(17,
                new Vector2Int(-1, -1));
            Assert.That(heat.Count(source => source.property == CookingProperty.Heat),
                Is.GreaterThanOrEqualTo(4));
            Assert.That(water.Count(source => source.property == CookingProperty.Water),
                Is.GreaterThanOrEqualTo(4));
            Assert.That(heat.Count, Is.LessThanOrEqualTo(6));
            Assert.That(water.Count, Is.LessThanOrEqualTo(6));
            Assert.That(HasLoosePatch(heat, CookingProperty.Heat), Is.True);
            Assert.That(HasLoosePatch(water, CookingProperty.Water), Is.True);
            Assert.That(generator.Generate(17, Vector2Int.zero), Is.Empty);
            Assert.That(generator.Generate(17, territories.StartingTerritory), Is.Empty);
            Assert.That(heat.All(source => territories.CoordinateAtCell(source.cell) ==
                new Vector2Int(1, -1)), Is.True);
            Assert.That(water.All(source => territories.CoordinateAtCell(source.cell) ==
                new Vector2Int(-1, -1)), Is.True);
            AssertConnected(heat);
            AssertConnected(water);
        }

        [Test]
        public void DistanceWeightsBiasTimeAndColdOutward()
        {
            var settings = new TerritoryWorldSettings();
            PropertyDistanceWeights near = settings.WeightsAtDistance(1);
            PropertyDistanceWeights middle = settings.WeightsAtDistance(2);
            PropertyDistanceWeights far = settings.WeightsAtDistance(3);

            Assert.That(PropertyWorldGenerator.ChooseProperty(near, 0.92),
                Is.EqualTo(CookingProperty.Time));
            Assert.That(PropertyWorldGenerator.ChooseProperty(near, 0.98),
                Is.EqualTo(CookingProperty.Cold));
            Assert.That(PropertyWorldGenerator.ChooseProperty(middle, 0.75),
                Is.EqualTo(CookingProperty.Time));
            Assert.That(PropertyWorldGenerator.ChooseProperty(far, 0.5),
                Is.EqualTo(CookingProperty.Time));
            Assert.That(PropertyWorldGenerator.ChooseProperty(far, 0.8),
                Is.EqualTo(CookingProperty.Cold));
        }

        [Test]
        public void SavedGeneratedSourcesRemainTheSourceTypesAfterTerritoryRestore()
        {
            var generator = NewGenerator();
            var territory = new Vector2Int(1, -1);
            PropertySourceSetup[] generated = generator.Generate(222, territory);
            SavedPropertySource[] saved = generated.Select(source =>
                new SavedPropertySource(source)).ToArray();
            var state = NewSystem();
            state.TryPurchase(territory, Wallet(100));
            var world = new FactoryWorldData
            {
                worldSeed = 222,
                territoryGenerationVersion = TerritoryWorldSettings.GenerationVersion,
                territoryPurchaseCount = state.ExpansionCount,
                reservedHubTerritory = new SavedTerritory(state.ReservedHubTerritory),
                startingTerritory = new SavedTerritory(state.StartingTerritory),
                purchasedTerritories = state.Capture(),
                propertySources = saved
            };
            FactoryWorldData loaded = JsonUtility.FromJson<FactoryWorldData>(
                JsonUtility.ToJson(world));
            var restored = NewSystem();
            restored.Restore(loaded.worldSeed, loaded.purchasedTerritories,
                loaded.territoryPurchaseCount);
            PropertySourceSetup[] restoredSources = loaded.propertySources.Select(source =>
                source.ToSetup()).ToArray();

            Assert.That(Describe(restoredSources), Is.EqualTo(Describe(generated)));
            Assert.That(restoredSources.All(source =>
                restored.IsOwnedCell(source.cell)), Is.True);
        }

        [TestCase(1, CookingProperty.Heat)]
        [TestCase(-1, CookingProperty.Water)]
        public void GeneratedDepositFeedsCollectorPipeAndProcessor(int parcelX, CookingProperty property)
        {
            for (int seed = 0; seed < 64; seed++)
            {
                PropertySourceSetup[] patch = NewGenerator().Generate(seed, new Vector2Int(parcelX, -1));
                var network = new CookingPropertyNetwork();
                foreach (PropertySourceSetup source in patch)
                    Assert.That(network.TryAddSource(source.cell, source.property, source.capacity), Is.True);
                // An outward port on a leftmost source cannot touch another Source.
                PropertySourceSetup selected = patch.OrderBy(source => source.cell.x).First();
                Vector2Int collector = selected.cell + Vector2Int.left;
                Vector2Int pipe = collector + Vector2Int.left;
                Vector2Int demand = pipe + Vector2Int.left;
                Assert.That(network.TryAddCollector(collector, selected.cell), Is.True);
                Assert.That(network.TryAddPipe(pipe, selected.cell), Is.True);
                Assert.That(network.TryAddDemand(demand), Is.True);
                Assert.That(network.IsSupplied(demand), Is.True);
                var input = new FoodItemData("test-input", FoodItemKind.RawIngredient);
                var output = new FoodItemData("test-output", FoodItemKind.ProcessedFood);
                var processor = new ProcessorProcess(new ProcessingRecipeCatalog(new[]
                {
                    new ProcessingRecipe(input, property, output)
                }), 1f);
                Assert.That(processor.TryAccept(input, property, network.IsSupplied(demand)), Is.True);
                Assert.That(processor.Advance(1f, network.IsSupplied(demand)), Is.True);
                Assert.That(processor.PeekOutput(), Is.EqualTo(output));
            }
        }

        [Test]
        public void ManySeedsProduceConnectedDepositsAndKeepCoreEmpty()
        {
            var generator = NewGenerator();
            for (int seed = 0; seed < 256; seed++)
            {
                foreach (Vector2Int parcel in new[]
                    { new Vector2Int(1, -1), new Vector2Int(-1, -1), new Vector2Int(4, -3) })
                {
                    PropertySourceSetup[] patch = generator.Generate(seed, parcel);
                    Assert.That(patch.Length, Is.InRange(4, 6));
                    AssertConnected(patch);
                    Assert.That(patch.Select(source => source.property).Distinct().Count(), Is.EqualTo(1));
                }
                foreach (Vector2Int parcel in new[]
                    { Vector2Int.zero, Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                    Assert.That(generator.Generate(seed, parcel), Is.Empty);
            }
        }

        [Test]
        public void NegativeCellBoundariesAndAllRotationsRespectOwnership()
        {
            var system = NewSystem();
            Assert.That(system.CoordinateAtCell(new Vector2Int(-1, -1)), Is.EqualTo(new Vector2Int(-1, -1)));
            Assert.That(system.CoordinateAtCell(new Vector2Int(-8, -8)), Is.EqualTo(new Vector2Int(-1, -1)));
            Assert.That(system.CoordinateAtCell(new Vector2Int(-9, -9)), Is.EqualTo(new Vector2Int(-1, -1)));
            Assert.That(system.CoordinateAtCell(new Vector2Int(-10, -10)), Is.EqualTo(new Vector2Int(-2, -2)));
            foreach (BuildingRotation rotation in Enum.GetValues(typeof(BuildingRotation)))
            {
                Assert.That(system.ContainsFootprint(new Vector2Int(16, -8), new Vector2Int(2, 1), rotation), Is.True);
                Vector2Int crossingBoundary = (int)rotation % 180 == 0
                    ? new Vector2Int(17, -8) : new Vector2Int(16, 17);
                Assert.That(system.ContainsFootprint(crossingBoundary, new Vector2Int(2, 1), rotation), Is.False);
            }
        }

        [Test]
        public void GlobalPriceAdvancesAcrossDifferentDirectionsAndUnaffordablePurchaseIsAtomic()
        {
            var system = NewSystem();
            var wallet = Wallet(280);
            Assert.That(system.TryPurchase(new Vector2Int(0, 2), wallet), Is.True);
            Assert.That(system.TryPurchase(new Vector2Int(2, 0), wallet), Is.True);
            Assert.That(system.NextPurchaseCost, Is.EqualTo(320));
            Assert.That(wallet.Currency, Is.Zero);
            Assert.That(system.TryPurchase(new Vector2Int(0, 3), wallet), Is.False);
            Assert.That(system.ExpansionCount, Is.EqualTo(2));
            Assert.That(system.GetParcelVisualState(new Vector2Int(0, 3), 0),
                Is.EqualTo(TerritoryParcelVisualState.Purchasable));
            Assert.That(new TerritoryWorldSettings().CostForExpansionCount(int.MaxValue), Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void TouchingSourcesRetainIndependentCapacityAndCannotMergeCollectors()
        {
            var network = new CookingPropertyNetwork();
            Assert.That(network.TryAddSource(Vector2Int.zero, CookingProperty.Heat, 4), Is.True);
            Assert.That(network.TryAddSource(Vector2Int.right, CookingProperty.Heat, 7), Is.True);
            Assert.That(network.TryAddPipe(Vector2Int.left, Vector2Int.zero), Is.False);
            Assert.That(network.TryAddCollector(Vector2Int.left, Vector2Int.zero), Is.True);
            Assert.That(network.TryAddCollector(Vector2Int.up, Vector2Int.zero), Is.True);
            Assert.That(network.TryAddCollector(Vector2Int.one, Vector2Int.right), Is.False);
            Assert.That(network.TryAddSource(new Vector2Int(-2, 0), CookingProperty.Heat, 4), Is.False);
            Assert.That(network.Remove(Vector2Int.zero), Is.False);
            network.TryGetStatus(Vector2Int.zero, out PropertySupplyStatus first);
            network.TryGetStatus(Vector2Int.right, out PropertySupplyStatus second);
            Assert.That(first.Capacity, Is.EqualTo(4));
            Assert.That(second.Capacity, Is.EqualTo(7));
        }

        [Test]
        public void FrontierSnapshotsIncludeUnownedResourcesAndRejectMissingOrDuplicateDeposits()
        {
            FactoryWorldData world = FreshWorld();
            Assert.DoesNotThrow(() => ValidateWorld(world));
            Assert.That(world.propertySources.Any(source =>
                !NewSystem().IsOwnedCell(source.Cell)), Is.True);
            world.propertySources = world.propertySources.Concat(new[] { world.propertySources[0] }).ToArray();
            Assert.Throws<ArgumentException>(() => ValidateWorld(world));
            world = FreshWorld();
            world.propertySources = Array.Empty<SavedPropertySource>();
            Assert.Throws<ArgumentException>(() => ValidateWorld(world));
        }

        [Test]
        public void WorldJsonRoundTripRetainsGeneratedFrontierWithoutFixedDeliveryFixtures()
        {
            FactoryWorldData world = FreshWorld();
            FactoryWorldData loaded = JsonUtility.FromJson<FactoryWorldData>(JsonUtility.ToJson(world));
            FactoryWorldSnapshotValidator.RestoreSerializedNulls(loaded);
            Assert.DoesNotThrow(() => ValidateWorld(loaded));
            var network = new CookingPropertyNetwork();
            foreach (SavedPropertySource source in loaded.propertySources)
                Assert.That(network.TryAddSource(source.Cell, source.property, source.capacity), Is.True);
            Assert.That(network.Connections.Count(), Is.EqualTo(world.propertySources.Length));
            Assert.That(loaded.generatedTerritories.Length, Is.EqualTo(world.generatedTerritories.Length));
            Assert.That(loaded.buildings, Is.Empty);
        }

        [Test]
        public void ConstructionHistoryExcludesTerritoryChanges()
        {
            FactoryWorldData before = FreshWorld();
            FactoryWorldData after = FreshWorld();
            after.territoryPurchaseCount = 1;
            var change = ConstructionLayout.FromWorld(before).Difference(ConstructionLayout.FromWorld(after));
            Assert.That(change.Before.Buildings, Is.Empty);
            Assert.That(change.After.Buildings, Is.Empty);
            Assert.That(change.Before.Connections, Is.Empty);
            Assert.That(change.After.Connections, Is.Empty);
        }

        [Test]
        public void OverlayColorsKeepReservedHubTransparentAndFillsSubtle()
        {
            Assert.That(MarketPanel.ParcelColor(TerritoryParcelVisualState.ReservedHub).a, Is.Zero);
            foreach (TerritoryParcelVisualState state in new[]
                { TerritoryParcelVisualState.Owned, TerritoryParcelVisualState.Purchasable, TerritoryParcelVisualState.Locked })
                Assert.That(MarketPanel.ParcelColor(state).a, Is.InRange(0.1f, 0.25f));
        }

        private static FactoryWorldData FreshWorld()
        {
            var territories = NewSystem();
            var generator = NewGenerator();
            return new FactoryWorldData
            {
                worldSeed = territories.WorldSeed,
                reservedHubTerritory = new SavedTerritory(territories.ReservedHubTerritory),
                startingTerritory = new SavedTerritory(territories.StartingTerritory),
                purchasedTerritories = territories.Capture(),
                generatedTerritories = territories.RelevantCoordinates.Select(cell => new SavedTerritory(cell)).ToArray(),
                propertySources = territories.RelevantCoordinates.SelectMany(parcel =>
                    generator.Generate(territories.WorldSeed, parcel)).Select(source => new SavedPropertySource(source)).ToArray()
            };
        }

        private static void ValidateWorld(FactoryWorldData world) =>
            FactoryWorldSnapshotValidator.ValidateAgainstScene(world,
                Array.Empty<BuildingPlacementOption>(), new TerritoryWorldSettings(), Array.Empty<SavedUnlock>(),
                new[] { new ProcessingRecipe(new FoodItemData("test-input", FoodItemKind.RawIngredient, 3),
                    CookingProperty.Heat, new FoodItemData("test-output", FoodItemKind.ProcessedFood)) },
                Array.Empty<MixingRecipe>(), MarketCell, MarketFootprint);

        private static void AssertConnected(PropertySourceSetup[] patch)
        {
            var connected = new HashSet<Vector2Int> { patch[0].cell };
            for (int pass = 0; pass < patch.Length; pass++)
                foreach (PropertySourceSetup source in patch)
                    if (connected.Any(cell => Math.Abs(cell.x - source.cell.x) +
                        Math.Abs(cell.y - source.cell.y) == 1)) connected.Add(source.cell);
            Assert.That(connected.Count, Is.EqualTo(patch.Length));
        }

        private static TerritorySystem NewSystem() =>
            new TerritorySystem(new TerritoryWorldSettings()).Initialize();

        private static PropertyWorldGenerator NewGenerator(
            TerritoryWorldSettings settings = null) =>
            new(settings ?? new TerritoryWorldSettings());

        private static MarketInventory Wallet(int amount)
        {
            var inventory = new MarketInventory();
            inventory.RecordDelivery(new FoodItemData("currency-test",
                FoodItemKind.RawIngredient, amount));
            return inventory;
        }

        private static string[] Describe(PropertySourceSetup[] sources) => sources
            .Select(source => $"{source.cell.x},{source.cell.y}:{source.property}:{source.capacity}")
            .ToArray();

        private static bool HasLoosePatch(PropertySourceSetup[] sources,
            CookingProperty property) => sources.Any(center =>
            center.property == property && sources.Count(source =>
                source.property == property &&
                Mathf.Abs(source.cell.x - center.cell.x) <= 2 &&
                Mathf.Abs(source.cell.y - center.cell.y) <= 2) >= 3);
    }
}
