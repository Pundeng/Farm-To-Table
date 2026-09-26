using System;
using System.IO;
using System.Linq;
using System.Reflection;
using FantasyShapez.Buildings;
using FantasyShapez.Food;
using FantasyShapez.Logistics;
using FantasyShapez.Runes;
using NUnit.Framework;
using UnityEngine;

namespace FantasyShapez.Tests.EditMode
{
    public sealed class ProgressionSaveTests
    {
        private sealed class Session : IDisposable
        {
            public readonly FoodItemData Apple =
                new("apple", FoodItemKind.RawIngredient);
            public readonly FoodItemData DriedApple =
                new("Dried Apple", FoodItemKind.ProcessedFood);
            public readonly MarketInventory Inventory = new();
            public readonly UnlockState Unlocks = new();
            public readonly RecipeDiscoveryRegistry Discoveries = new();
            public readonly MarketReceiver Receiver;
            public readonly FoodOrderSequence Orders;
            public readonly SeedShop Shop;
            public readonly RegionState Regions;
            public readonly ProcessingRecipe Recipe;
            public readonly ProgressionSaveService Saves;

            public Session()
            {
                Receiver = new MarketReceiver(Vector2Int.zero, Inventory);
                Orders = new FoodOrderSequence(new[]
                {
                    new FoodOrder("apple-order", "Apple Order",
                        new[] { new FoodOrderRequirement(Apple, 2) },
                        new[]
                        {
                            new UnlockKey(UnlockKey.CropCategory, "Onion"),
                            new UnlockKey(UnlockKey.SeedShopCategory, "Basil"),
                            new UnlockKey(UnlockKey.RegionAccessCategory, "East")
                        }),
                    new FoodOrder("dried-order", "Dried Order",
                        new[] { new FoodOrderRequirement(DriedApple, 2) },
                        new[] { new UnlockKey("machine", "Oven") })
                }, Receiver, Unlocks);
                Shop = new SeedShop(new[]
                {
                    new SeedShopOffer("Basil", "Basil Seeds", 1,
                        new UnlockKey(UnlockKey.SeedShopCategory, "Basil"))
                }, Inventory, Unlocks);
                Regions = new RegionState(new[]
                {
                    new FarmableRegion("Starter", "Starter", Vector2Int.zero,
                        Vector2Int.one, true),
                    new FarmableRegion("East", "East", Vector2Int.right,
                        Vector2Int.one, false,
                        new UnlockKey(UnlockKey.RegionAccessCategory, "East"),
                        price: 1)
                }, Unlocks);
                Recipe = new ProcessingRecipe(Apple, CookingProperty.Air, DriedApple);
                Saves = new ProgressionSaveService(Inventory, Orders, Unlocks, Shop, Regions,
                    Discoveries, new[] { Recipe }, Array.Empty<MixingRecipe>());
            }

            public void Dispose() => Orders.Dispose();
        }

        [Test]
        public void PurchasedRegion_RoundTripPreservesRemainingCurrencyAndFarmability()
        {
            using var source = new Session();
            source.Receiver.TryAcceptItem(source.Apple, GridDirection.East);
            source.Receiver.TryAcceptItem(source.Apple, GridDirection.East);
            Assert.That(source.Regions.TryPurchase("East", source.Inventory), Is.True);
            Assert.That(source.Inventory.Currency, Is.EqualTo(1));

            string json = source.Saves.ToJson();
            using var destination = new Session();
            Assert.That(destination.Saves.TryLoadJson(json, out string error),
                Is.True, error);
            Assert.That(destination.Inventory.Currency, Is.EqualTo(1));
            Assert.That(destination.Regions.CanFarm(Vector2Int.right), Is.True);
            Assert.That(destination.Regions.TryPurchase("East", destination.Inventory),
                Is.False);
            Assert.That(destination.Inventory.Currency, Is.EqualTo(1));
        }

        [Test]
        public void RoundTrip_RestoresOrderProgressPurchasesRegionsAndDiscoveries()
        {
            using var source = new Session();
            source.Receiver.TryAcceptItem(source.Apple, GridDirection.East);
            source.Receiver.TryAcceptItem(source.Apple, GridDirection.East);
            Assert.That(source.Shop.TryPurchase("Basil"), Is.True);
            Assert.That(source.Regions.TryRestore("East"), Is.True);
            Assert.That(source.Discoveries.Record(source.Recipe), Is.True);
            source.Receiver.TryAcceptItem(source.DriedApple, GridDirection.East);

            string json = source.Saves.ToJson();
            using var restored = new Session();
            Assert.That(restored.Saves.TryLoadJson(json, out string error), Is.True, error);
            Assert.That(restored.Inventory.Currency, Is.EqualTo(2));
            Assert.That(restored.Inventory.TotalDelivered, Is.EqualTo(3));
            Assert.That(restored.Orders.CompletedOrders, Has.Count.EqualTo(1));
            Assert.That(restored.Orders.ActiveOrder.Order.Id, Is.EqualTo("dried-order"));
            Assert.That(restored.Orders.ActiveOrder.GetDeliveredCount(
                restored.Orders.ActiveOrder.Order.Requirements[0]), Is.EqualTo(1));
            Assert.That(restored.Unlocks.IsUnlocked(UnlockKey.CropCategory, "Onion"), Is.True);
            Assert.That(restored.Shop.GetState("Basil"), Is.EqualTo(SeedShopOfferState.Purchased));
            Assert.That(restored.Regions.GetStatus("East"), Is.EqualTo(RegionStatus.Restored));
            Assert.That(restored.Discoveries.DiscoveredRecipes, Has.Count.EqualTo(1));
            Assert.That(restored.Discoveries.Record(restored.Recipe), Is.False);

            int unlockCount = restored.Unlocks.Unlocked.Count;
            restored.Receiver.TryAcceptItem(restored.DriedApple, GridDirection.East);
            Assert.That(restored.Orders.CompletedOrders, Has.Count.EqualTo(2));
            Assert.That(restored.Unlocks.Unlocked.Count, Is.EqualTo(unlockCount + 1));
            restored.Receiver.TryAcceptItem(restored.DriedApple, GridDirection.East);
            Assert.That(restored.Unlocks.Unlocked.Count, Is.EqualTo(unlockCount + 1));
        }

        [Test]
        public void InvalidOrMissingSave_LeavesLiveProgressionUntouched()
        {
            using var session = new Session();
            session.Receiver.TryAcceptItem(session.Apple, GridDirection.East);
            string json = session.Saves.ToJson();
            var invalid = JsonUtility.FromJson<ProgressionSaveData>(json);
            invalid.activeProgress[0].count = 2;
            Assert.That(session.Saves.TryLoadJson(JsonUtility.ToJson(invalid), out _), Is.False);
            Assert.That(session.Saves.TryLoadJson("{}", out _), Is.False);
            Assert.That(session.Saves.TryLoadJson("not json", out _), Is.False);
            Assert.That(session.Saves.TryLoad(Path.Combine(Application.temporaryCachePath,
                $"missing-progression-{Guid.NewGuid():N}.json"), out _), Is.False);
            Assert.That(session.Orders.CompletedOrders, Is.Empty);
            Assert.That(session.Orders.ActiveOrder.GetDeliveredCount(
                session.Orders.ActiveOrder.Order.Requirements[0]), Is.EqualTo(1));
            Assert.That(session.Inventory.Currency, Is.EqualTo(1));
            Assert.That(session.Unlocks.IsUnlocked(UnlockKey.CropCategory, "Onion"), Is.False);
        }

        [Test]
        public void LoadingEarlierUnlockState_ClearsLockedCropOnExistingPlot()
        {
            using var session = new Session();
            string earlierSave = session.Saves.ToJson();
            session.Unlocks.Grant(new UnlockKey(UnlockKey.CropCategory, "Onion"));
            var plotObject = new GameObject("Saved progression plot");
            try
            {
                var plot = plotObject.AddComponent<FarmPlot>();
                var onion = new CropDefinition("Onion",
                    new FoodItemData("onion", FoodItemKind.RawIngredient), 2f, "Onion");
                typeof(FarmPlot).GetField("availableCrops",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(plot, new[] { onion });
                plot.Initialize(new Vector2Int(1300, 1300), session.Unlocks);
                plot.SelectCrop(onion);

                Assert.That(session.Saves.TryLoadJson(earlierSave, out string error),
                    Is.True, error);
                Assert.That(plot.IsCropUnlocked(onion), Is.False);
                Assert.That(plot.SelectedCrop, Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(plotObject);
            }
        }

        [Test]
        public void VersionTwoSnapshot_RoundTripsWorldBuffersAndPropertyTestLoad()
        {
            using var source = new Session();
            var world = new FactoryWorldData
            {
                buildings = new[]
                {
                    new SavedBuilding
                    {
                        definitionId = nameof(FarmPlot), x = 2, y = 3,
                        farmPlot = new SavedFarmPlot
                        {
                            cropId = "Apple", matureCount = 2, elapsedSeconds = 0.4f
                        }
                    },
                    new SavedBuilding
                    {
                        definitionId = nameof(Harvester), x = 2, y = 3,
                        harvester = new SavedHarvester
                        {
                            outputs = new[] { SavedFood.From(source.Apple) },
                            elapsedSeconds = 0.25f
                        }
                    },
                    new SavedBuilding
                    {
                        definitionId = nameof(Belt), x = 4, y = 3,
                        belt = new SavedBelt
                        {
                            item = SavedFood.From(source.Apple),
                            entryDirection = GridDirection.East, progress = 0.75f
                        }
                    },
                    new SavedBuilding
                    {
                        definitionId = nameof(Processor), x = 5, y = 3,
                        processor = new SavedProcessor
                        {
                            state = ProcessorState.Processing,
                            input = SavedFood.From(source.Apple),
                            output = SavedFood.From(source.DriedApple),
                            activeProperty = CookingProperty.Air,
                            elapsedSeconds = 0.5f
                        }
                    },
                    new SavedBuilding
                    {
                        definitionId = nameof(BasicMixer), x = 8, y = 3,
                        mixer = new SavedMixer { slotA = SavedFood.From(source.Apple) }
                    }
                },
                connections = new[]
                {
                    new SavedPropertyConnection
                    {
                        x = 1, y = 4, sourceX = 0, sourceY = 4,
                        property = CookingProperty.Air,
                        kind = PropertyConnectionKind.Demand, units = 1
                    }
                }
            };
            var saves = new ProgressionSaveService(source.Inventory, source.Orders,
                source.Unlocks, source.Shop, source.Regions, source.Discoveries,
                new[] { source.Recipe }, Array.Empty<MixingRecipe>(), () => world);

            string json = saves.ToJson();
            var parsed = JsonUtility.FromJson<ProgressionSaveData>(json);
            FactoryWorldSnapshotValidator.RestoreSerializedNulls(parsed.world);
            FactoryWorldSnapshotValidator.Validate(parsed.world);
            Assert.That(parsed.version, Is.EqualTo(2));
            Assert.That(parsed.world.buildings, Has.Length.EqualTo(5));
            Assert.That(parsed.world.buildings[2].belt.progress, Is.EqualTo(0.75f));
            Assert.That(parsed.world.buildings[3].processor.elapsedSeconds, Is.EqualTo(0.5f));
            Assert.That(parsed.world.connections[0].kind,
                Is.EqualTo(PropertyConnectionKind.Demand));

            using var restored = new Session();
            Assert.That(restored.Saves.TryLoadJson(json, out string notice), Is.True,
                notice);
            Assert.That(notice, Is.Null);
        }

        [Test]
        public void InvalidWorld_IsRejectedBeforeProgressionChanges()
        {
            using var source = new Session();
            source.Receiver.TryAcceptItem(source.Apple, GridDirection.East);
            ProgressionSaveData data = JsonUtility.FromJson<ProgressionSaveData>(
                source.Saves.ToJson());
            data.world.buildings = new[]
            {
                new SavedBuilding
                {
                    definitionId = nameof(Belt), belt = new SavedBelt
                    {
                        item = new SavedFood
                        {
                            id = "apple", kind = FoodItemKind.RawIngredient,
                            sellValue = 0
                        }
                    }
                }
            };

            using var destination = new Session();
            Assert.That(destination.Saves.TryLoadJson(JsonUtility.ToJson(data),
                out _), Is.False);
            Assert.That(destination.Inventory.Currency, Is.Zero);
            Assert.That(destination.Orders.ActiveOrder.GetDeliveredCount(
                destination.Orders.ActiveOrder.Order.Requirements[0]), Is.Zero);
        }

        [Test]
        public void VersionOneLoads_AndFailedMigrationKeepsOriginalFile()
        {
            using var session = new Session();
            session.Receiver.TryAcceptItem(session.Apple, GridDirection.East);
            ProgressionSaveData legacy = JsonUtility.FromJson<ProgressionSaveData>(
                session.Saves.ToJson());
            legacy.version = 1;
            legacy.world = null;
            string original = JsonUtility.ToJson(legacy);
            using var restored = new Session();
            Assert.That(restored.Saves.TryLoadJson(original, out string error),
                Is.True, error);
            Assert.That(restored.Inventory.Currency, Is.EqualTo(1));

            string path = Path.Combine(Application.temporaryCachePath,
                $"progression-migration-{Guid.NewGuid():N}.json");
            try
            {
                File.WriteAllText(path, original);
                var failing = new ProgressionSaveService(session.Inventory,
                    session.Orders, session.Unlocks, session.Shop, session.Regions,
                    session.Discoveries, new[] { session.Recipe },
                    Array.Empty<MixingRecipe>(),
                    () => throw new InvalidOperationException(
                        "Active legacy RuneData cannot be saved."));
                Assert.That(failing.TrySave(path, out error), Is.False);
                Assert.That(error, Does.Contain("legacy RuneData"));
                Assert.That(File.ReadAllText(path), Is.EqualTo(original));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void SceneValidation_AllowsPlotOverlayButRejectsOccupiedNeighbor()
        {
            var plotObject = new GameObject("Snapshot plot prefab");
            var harvesterObject = new GameObject("Snapshot harvester prefab");
            try
            {
                plotObject.AddComponent<FarmPlot>();
                harvesterObject.AddComponent<Harvester>();
                BuildingPlacementOption MakeOption(string id, Vector2Int footprint,
                    GameObject prefab)
                {
                    var definition = new BuildingDefinition();
                    typeof(BuildingDefinition).GetField("id",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                        ?.SetValue(definition, id);
                    typeof(BuildingDefinition).GetField("footprint",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                        ?.SetValue(definition, footprint);
                    typeof(BuildingDefinition).GetField("instancePrefab",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                        ?.SetValue(definition, prefab);
                    var option = new BuildingPlacementOption();
                    typeof(BuildingPlacementOption).GetField("definition",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                        ?.SetValue(option, definition);
                    return option;
                }

                var options = new[]
                {
                    MakeOption(nameof(FarmPlot), Vector2Int.one, plotObject),
                    MakeOption(nameof(Harvester), new Vector2Int(1, 2),
                        harvesterObject),
                    MakeOption(nameof(Belt), Vector2Int.one, null)
                };
                var unlocks = new UnlockState();
                var regions = new RegionState(new[]
                {
                    new FarmableRegion("Starter", "Starter", Vector2Int.zero,
                        new Vector2Int(3, 3), true)
                }, unlocks);
                var keys = new[]
                {
                    new SavedUnlock { category = UnlockKey.RegionCategory, id = "Starter" }
                };
                var world = new FactoryWorldData
                {
                    buildings = new[]
                    {
                        new SavedBuilding
                        {
                            definitionId = nameof(FarmPlot), x = 0, y = 0,
                            farmPlot = new SavedFarmPlot()
                        },
                        new SavedBuilding
                        {
                            definitionId = nameof(Harvester), x = 0, y = 0,
                            harvester = new SavedHarvester
                            {
                                outputs = Array.Empty<SavedFood>()
                            }
                        }
                    }
                };
                Assert.DoesNotThrow(() => FactoryWorldSnapshotValidator.ValidateAgainstScene(
                    world, options, Array.Empty<PropertySourceSetup>(), regions, keys,
                    Array.Empty<ProcessingRecipe>(), Array.Empty<MixingRecipe>(),
                    new Vector2Int(10, 4), new Vector2Int(10, 1)));
                Assert.DoesNotThrow(() => FactoryWorldSnapshotValidator.ValidateAgainstScene(
                    world, options, Array.Empty<PropertySourceSetup>(), regions, keys,
                    Array.Empty<ProcessingRecipe>(), Array.Empty<MixingRecipe>(),
                    new Vector2Int(10, 4), null));

                world.buildings = new[]
                {
                    world.buildings[0], world.buildings[1],
                    new SavedBuilding
                    {
                        definitionId = nameof(Belt), x = 0, y = 1,
                        belt = new SavedBelt()
                    }
                };
                Assert.Throws<ArgumentException>(() =>
                    FactoryWorldSnapshotValidator.ValidateAgainstScene(world, options,
                        Array.Empty<PropertySourceSetup>(), regions, keys,
                        Array.Empty<ProcessingRecipe>(), Array.Empty<MixingRecipe>(),
                        new Vector2Int(10, 4), new Vector2Int(10, 1)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(plotObject);
                UnityEngine.Object.DestroyImmediate(harvesterObject);
            }
        }

        [Test]
        public void ActiveRuneTransport_IsRejectedByFoodSnapshotCodec()
        {
            Assert.Throws<InvalidOperationException>(() =>
                SavedFood.FromTransport(new RuneData(RuneBaseShape.Circle)));
        }

        [Test]
        public void ReconstructionOrder_PlacesPlotBeforeOverlayRegardlessOfFileOrder()
        {
            var world = new FactoryWorldData
            {
                buildings = new[]
                {
                    new SavedBuilding { definitionId = nameof(Harvester),
                        harvester = new SavedHarvester() },
                    new SavedBuilding { definitionId = nameof(Belt),
                        belt = new SavedBelt() },
                    new SavedBuilding { definitionId = nameof(FarmPlot),
                        farmPlot = new SavedFarmPlot() }
                }
            };

            Assert.That(FactoryWorldSnapshotValidator.ReconstructionOrder(world)
                .Select(building => building.definitionId).ToArray(),
                Is.EqualTo(new[] { nameof(FarmPlot), nameof(Harvester), nameof(Belt) }));
        }

        [Test]
        public void ProcessRestore_PreservesTimersBuffersAndOccupiedInput()
        {
            var apple = new FoodItemData("apple", FoodItemKind.RawIngredient);
            var dried = new FoodItemData("dried", FoodItemKind.ProcessedFood);
            var plot = new FarmPlotProcess(4);
            plot.Restore(new CropDefinition("Apple", apple, 2f), 2, 0.4f);
            Assert.That(plot.MatureCount, Is.EqualTo(2));
            Assert.That(plot.ElapsedTime, Is.EqualTo(0.4f));

            var harvester = new HarvesterProcess(1f, 4);
            harvester.Restore(new[] { apple, apple }, 0.25f);
            Assert.That(harvester.OutputCount, Is.EqualTo(2));
            Assert.That(harvester.ElapsedTime, Is.EqualTo(0.25f));

            var processor = new ProcessorProcess(new ProcessingRecipeCatalog(new[]
            {
                new ProcessingRecipe(apple, CookingProperty.Air, dried)
            }), 1f);
            processor.Restore(ProcessorState.Processing, apple,
                CookingProperty.Air, dried, 0.5f);
            Assert.That(processor.State, Is.EqualTo(ProcessorState.Processing));
            Assert.That(processor.ElapsedTime, Is.EqualTo(0.5f));
            Assert.That(processor.Advance(0.5f, true), Is.True);
            Assert.That(processor.PeekOutput(), Is.EqualTo(dried));

            var mixer = new BasicMixerProcess(new MixingRecipeCatalog(new[]
            {
                new MixingRecipe(apple, dried, dried)
            }));
            mixer.Restore(apple, null, null);
            Assert.That(mixer.InputA, Is.EqualTo(apple));
            Assert.That(mixer.TryAccept(1, dried), Is.True);
            Assert.That(mixer.PeekOutput(), Is.EqualTo(dried));
        }

        [Test]
        public void OccupiedBeltRestore_KeepsPayloadEntryAndProgress()
        {
            var belt = new BeltCell(new Vector2Int(2, 3), GridDirection.East);
            var food = new FoodItemData("apple", FoodItemKind.RawIngredient);
            belt.RestoreItem(food, GridDirection.North, 0.75f);

            Assert.That(belt.Item.Item, Is.SameAs(food));
            Assert.That(belt.Item.EntryDirection, Is.EqualTo(GridDirection.North));
            Assert.That(belt.Item.Progress, Is.EqualTo(0.75f));
            Assert.That(belt.TryAccept(food, GridDirection.East), Is.False);
        }

        [Test]
        public void DisconnectedPropertySection_RetainsSavedSourceOwner()
        {
            var network = new CookingPropertyNetwork();
            Vector2Int source = Vector2Int.zero;
            Vector2Int disconnected = new(5, 0);
            Assert.That(network.TryAddSource(source, CookingProperty.Air, 4), Is.True);
            Assert.That(network.TryRestoreConnection(new PropertyConnection(disconnected,
                source, CookingProperty.Air, PropertyConnectionKind.Pipe)), Is.True);
            Assert.That(network.TryGetConnection(disconnected, out PropertyConnection restored),
                Is.True);
            Assert.That(restored.SourceCell, Is.EqualTo(source));
            Assert.That(network.IsConnectedToSource(disconnected), Is.False);
        }

        [Test]
        public void CapturedBuildingStates_SerializeValidateAndKeepReconstructionOrder()
        {
            var plotObject = new GameObject("Captured Farm Plot");
            var beltObject = new GameObject("Captured Belt");
            var coordinatorObject = new GameObject("Capture transport");
            try
            {
                FarmPlot plot = plotObject.AddComponent<FarmPlot>();
                plot.Initialize(new Vector2Int(-4, 2));
                Belt belt = beltObject.AddComponent<Belt>();
                belt.Initialize(new Vector2Int(-2, 2), GridDirection.East,
                    coordinatorObject.AddComponent<BeltTransportCoordinator>());
                var world = new FactoryWorldData
                {
                    buildings = new[]
                    {
                        new SavedBuilding
                        {
                            definitionId = nameof(Belt), x = -2, y = 2,
                            belt = belt.CaptureWorldState()
                        },
                        new SavedBuilding
                        {
                            definitionId = nameof(FarmPlot), x = -4, y = 2,
                            farmPlot = plot.CaptureWorldState()
                        }
                    }
                };
                using var source = new Session();
                var saves = new ProgressionSaveService(source.Inventory, source.Orders,
                    source.Unlocks, source.Shop, source.Regions, source.Discoveries,
                    new[] { source.Recipe }, Array.Empty<MixingRecipe>(), () => world);

                string json = saves.ToJson();
                ProgressionSaveData parsed = JsonUtility.FromJson<ProgressionSaveData>(json);
                FactoryWorldSnapshotValidator.RestoreSerializedNulls(parsed.world);
                Assert.DoesNotThrow(() => FactoryWorldSnapshotValidator.Validate(parsed.world));
                Assert.That(parsed.world.buildings[0].farmPlot, Is.Null);
                Assert.That(parsed.world.buildings[0].belt.item, Is.Null);
                Assert.That(parsed.world.buildings[1].belt, Is.Null);
                Assert.That(FactoryWorldSnapshotValidator.ReconstructionOrder(parsed.world)
                    .Select(building => building.definitionId).ToArray(),
                    Is.EqualTo(new[] { nameof(FarmPlot), nameof(Belt) }));

                using var restored = new Session();
                Assert.That(restored.Saves.TryLoadJson(json, out string error), Is.True,
                    error);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(beltObject);
                UnityEngine.Object.DestroyImmediate(plotObject);
                UnityEngine.Object.DestroyImmediate(coordinatorObject);
            }
        }

        [Test]
        public void SerializedNullRepair_RejectsNonemptyMismatchedStateWithLocation()
        {
            var building = new SavedBuilding
            {
                definitionId = nameof(Belt), x = 4, y = 7,
                belt = new SavedBelt(),
                farmPlot = new SavedFarmPlot { cropId = "Apple" },
                harvester = new SavedHarvester(),
                processor = new SavedProcessor(),
                mixer = new SavedMixer()
            };
            var world = new FactoryWorldData { buildings = new[] { building } };
            FactoryWorldSnapshotValidator.RestoreSerializedNulls(world);

            ArgumentException error = Assert.Throws<ArgumentException>(() =>
                FactoryWorldSnapshotValidator.Validate(world));
            Assert.That(error.Message, Does.Contain("Belt"));
            Assert.That(error.Message, Does.Contain("(4, 7)"));
            Assert.That(error.Message, Does.Contain("farmPlot"));
        }
    }
}
