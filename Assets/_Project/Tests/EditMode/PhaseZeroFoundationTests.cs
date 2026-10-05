using System;
using System.IO;
using System.Linq;
using System.Reflection;
using CozyFoodFactory.Buildings;
using CozyFoodFactory.Food;
using CozyFoodFactory.Logistics;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CozyFoodFactory.Tests.EditMode
{
    public sealed class PhaseZeroFoundationTests
    {
        private sealed class Session : IDisposable
        {
            public readonly FoodItemData Carrot = new("carrot", FoodItemKind.RawIngredient);
            public readonly FoodItemData Tomato = new("tomato", FoodItemKind.RawIngredient);
            public readonly MarketReceiver Receiver;
            public readonly FoodOrderSequence Orders;
            public readonly ProgressionSaveService Saves;
            public Session()
            {
                var inventory = new MarketInventory();
                var unlocks = new UnlockState();
                Receiver = new MarketReceiver(Vector2Int.zero, inventory);
                Orders = new FoodOrderSequence(new[] { new FoodOrder("multi", "Multi",
                    new[] { new FoodOrderRequirement(Carrot, 10), new FoodOrderRequirement(Tomato, 10) },
                    Array.Empty<UnlockKey>()) }, Receiver, unlocks);
                Saves = new ProgressionSaveService(inventory, Orders, unlocks,
                    new SeedShop(Array.Empty<SeedShopOffer>(), inventory, unlocks),
                    new RecipeDiscoveryRegistry(), Array.Empty<ProcessingRecipe>(), Array.Empty<MixingRecipe>());
            }
            public void Dispose() => Orders.Dispose();
        }

        [Test]
        public void PartlyCompleteProgressRestoresAndRejectsWholeCompletion()
        {
            using var session = new Session();
            for (int i = 0; i < 10; i++) session.Receiver.TryAcceptItem(session.Carrot, GridDirection.East);
            for (int i = 0; i < 5; i++) session.Receiver.TryAcceptItem(session.Tomato, GridDirection.East);
            var progress = new System.Collections.Generic.Dictionary<FoodItemData, int>
                { [session.Carrot] = 10, [session.Tomato] = 5 };
            using var restored = new FoodOrderProgress(session.Orders.ActiveOrder.Order, session.Receiver, progress);
            Assert.That(restored.GetDeliveredCount(restored.Order.Requirements[0]), Is.EqualTo(10));
            Assert.That(restored.GetDeliveredCount(restored.Order.Requirements[1]), Is.EqualTo(5));
            progress[session.Tomato] = 10;
            Assert.Throws<ArgumentException>(() => FoodOrderProgress.ValidateSavedProgress(restored.Order, progress, _ => 10));
        }

        [Test]
        public void MixerRestoreRejectsUnproducedOutputAndConflictingInputs()
        {
            var a = new FoodItemData("a", FoodItemKind.RawIngredient);
            var b = new FoodItemData("b", FoodItemKind.RawIngredient);
            var output = new FoodItemData("mixed", FoodItemKind.ProcessedFood);
            var process = new BasicMixerProcess(new MixingRecipeCatalog(new[] { new MixingRecipe(a, b, output) }));
            Assert.Throws<ArgumentException>(() => process.Restore(null, null, a));
            Assert.Throws<ArgumentException>(() => process.Restore(a, b, null));
            Assert.DoesNotThrow(() => process.Restore(null, null, output));
        }

        [Test]
        public void MultiRequirementProgressRoundTripsAndCompletesOnce()
        {
            using var source = new Session();
            for (int i = 0; i < 10; i++) source.Receiver.TryAcceptItem(source.Carrot, GridDirection.East);
            for (int i = 0; i < 5; i++) source.Receiver.TryAcceptItem(source.Tomato, GridDirection.East);
            using var restored = new Session();
            Assert.That(restored.Saves.TryLoadJson(source.Saves.ToJson(), out string error), Is.True, error);
            FoodOrderProgress active = restored.Orders.ActiveOrder;
            Assert.That(active.GetDeliveredCount(active.Order.Requirements[0]), Is.EqualTo(10));
            Assert.That(active.GetDeliveredCount(active.Order.Requirements[1]), Is.EqualTo(5));
            for (int i = 0; i < 6; i++) restored.Receiver.TryAcceptItem(restored.Tomato, GridDirection.East);
            Assert.That(restored.Orders.CompletedOrders, Has.Count.EqualTo(1));
            Assert.That(restored.Orders.ActiveOrder, Is.Null);
        }

        [Test]
        public void EntirelyCompleteActiveProgressIsRejectedWithoutApplying()
        {
            using var session = new Session();
            var data = JsonUtility.FromJson<ProgressionSaveData>(session.Saves.ToJson());
            data.deliveries = new[] {
                new SavedDelivery { id = "carrot", kind = FoodItemKind.RawIngredient, count = 10 },
                new SavedDelivery { id = "tomato", kind = FoodItemKind.RawIngredient, count = 10 } };
            data.activeProgress = data.deliveries;
            Assert.That(session.Saves.TryLoadJson(JsonUtility.ToJson(data), out _), Is.False);
            Assert.That(session.Receiver.Inventory.TotalDelivered, Is.Zero);
        }

        [Test]
        public void OverwriteRetainsOnePreviousValidSaveAndFailedWriteKeepsPrimary()
        {
            using var session = new Session();
            string directory = Path.Combine(Path.GetTempPath(), "phase-zero-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "save.json");
            try
            {
                Assert.That(session.Saves.TrySave(path, out string error), Is.True, error);
                string first = File.ReadAllText(path);
                session.Receiver.TryAcceptItem(session.Carrot, GridDirection.East);
                Assert.That(session.Saves.TrySave(path, out error), Is.True, error);
                Assert.That(File.ReadAllText(path + ".bak"), Is.EqualTo(first));
                string second = File.ReadAllText(path);
                session.Receiver.TryAcceptItem(session.Carrot, GridDirection.East);
                Assert.That(session.Saves.TrySave(path, out error), Is.True, error);
                Assert.That(File.ReadAllText(path + ".bak"), Is.EqualTo(second));
                string primary = File.ReadAllText(path);
                Directory.CreateDirectory(path + ".tmp");
                Assert.That(session.Saves.TrySave(path, out _), Is.False);
                Assert.That(File.ReadAllText(path), Is.EqualTo(primary));
                Assert.That(File.ReadAllText(path + ".bak"), Is.EqualTo(second));
                Directory.Delete(path + ".tmp");
                File.WriteAllText(path, "broken primary");
                Assert.That(session.Saves.TrySave(path, out error), Is.True, error);
                Assert.That(File.ReadAllText(path + ".bak"), Is.EqualTo(second));
            }
            finally { Directory.Delete(directory, true); }
        }

        [Test]
        public void CurrentChapterOnePassesContentValidation()
        {
            WithScene((_, controller) => Assert.That(controller.ValidateChapterOneContent(), Is.Empty));
        }

        [Test]
        public void MissingHeatRecipeReportsBlockedObjectiveTwo()
        {
            WithScene((_, controller) =>
            {
                Set(controller, "processorRecipes", controller.ProcessorRecipes
                    .Where(recipe => recipe.Input.Id != "carrot").ToArray());
                Assert.That(controller.ValidateChapterOneContent().Any(error =>
                    error.Contains("O2") && error.Contains("Roasted Carrot")), Is.True);
            });
        }

        [Test]
        public void DuplicateRecipesAreReportedAndRejectedByPreflight()
        {
            WithScene((market, controller) =>
            {
                ProcessingRecipe recipe = controller.ProcessorRecipes[0];
                Set(controller, "processorRecipes", controller.ProcessorRecipes.Concat(new[] { recipe }).ToArray());
                Assert.That(controller.ValidateChapterOneContent().Any(error => error.Contains("Ambiguous")), Is.True);
                FactoryWorldData world = FreshWorld(market);
                world.buildings = new[] { new SavedBuilding { definitionId = nameof(Processor), x = 0, y = -8,
                    processor = new SavedProcessor { state = ProcessorState.Processing,
                        input = SavedFood.From(recipe.Input), output = SavedFood.From(recipe.Output),
                        activeProperty = recipe.Property, elapsedSeconds = 0.5f } } };
                Assert.Throws<ArgumentException>(() => controller.ValidateWorldSnapshot(world, Array.Empty<SavedUnlock>()));
            });
        }

        [TestCase(CutterState.Processing, 1.1f, 1f)]
        [TestCase(CutterState.WaitingForOutput, 0.5f, 1f)]
        [TestCase(CutterState.Processing, 0.5f, 0.25f)]
        public void CutterTimerMismatchFailsScenePreflight(CutterState state, float elapsed, float duration)
        {
            WithScene((market, controller) =>
            {
                FactoryWorldData world = CutterWorld(market, controller);
                Set(controller, "cutterDuration", duration);
                world.buildings[0].cutter.state = state;
                world.buildings[0].cutter.elapsedSeconds = elapsed;
                Assert.Throws<ArgumentException>(() => controller.ValidateWorldSnapshot(world, Array.Empty<SavedUnlock>()));
            });
        }

        [Test]
        public void DuplicateCutterRecipesFailPreflight()
        {
            WithScene((market, controller) =>
            {
                FactoryWorldData world = CutterWorld(market, controller);
                Set(controller, "cutterRecipes", controller.CutterRecipes.Concat(controller.CutterRecipes).ToArray());
                Assert.Throws<ArgumentException>(() => controller.ValidateWorldSnapshot(world, Array.Empty<SavedUnlock>()));
            });
        }

        [Test]
        public void RestoringOutputContinuationAfterTopologyPreservesNextBranch()
        {
            var system = new BeltTransportSystem(1f);
            BeltCell splitter = system.AddBelt(Vector2Int.zero, GridDirection.North);
            splitter.SetOutputs(BeltCell.Bit(GridDirection.East) | BeltCell.Bit(GridDirection.North), 1);
            BeltCell north = system.AddBelt(Vector2Int.up, GridDirection.North);
            BeltCell east = system.AddBelt(Vector2Int.right, GridDirection.North);
            Assert.That(splitter.NextOutputIndex, Is.Zero, "Incomplete topology changed the saved east cursor.");
            splitter.RestoreOutputCursor(1);
            splitter.RestoreInputCursor(3);
            splitter.RestoreItem(new FoodItemData("carrot", FoodItemKind.RawIngredient), GridDirection.North, 0.4f);
            Assert.That(splitter.NextInputIndex, Is.EqualTo(3));
            system.Advance(1f);
            Assert.That(east.HasItem, Is.True);
            Assert.That(north.HasItem, Is.False);
        }

        [Test]
        public void SellValueEditsResolveFromCurrentContentButKindChangesReject()
        {
            WithScene((market, controller) =>
            {
                FactoryWorldData world = CutterWorld(market, controller);
                world.buildings[0].cutter.input.sellValue = 77;
                world.buildings[0].cutter.output.sellValue = 88;
                Assert.DoesNotThrow(() => controller.ValidateWorldSnapshot(world, Array.Empty<SavedUnlock>()));
                Assert.That(world.buildings[0].cutter.output.sellValue,
                    Is.EqualTo(controller.CutterRecipes[0].Output.SellValue));
                world.buildings[0].cutter.input.kind = FoodItemKind.ProcessedFood;
                Assert.Throws<ArgumentException>(() => controller.ValidateWorldSnapshot(world, Array.Empty<SavedUnlock>()));
            });
        }

        [Test]
        public void InconsistentFoodCatalogFailsValidation()
        {
            WithScene((_, controller) =>
            {
                ProcessingRecipe original = controller.ProcessorRecipes[0];
                Set(controller, "processorRecipes", new[] { new ProcessingRecipe(
                    new FoodItemData(original.Input.Id, original.Input.Kind, 99), original.Property, original.Output) }
                    .Concat(controller.ProcessorRecipes.Skip(1)).ToArray());
                Assert.That(controller.ValidateChapterOneContent().Any(error => error.Contains("inconsistent")), Is.True);
            });
        }

        [Test]
        public void SplitterCursorSurvivesWholeWorldReconstruction()
        {
            WithScene((market, controller) =>
            {
                typeof(Market).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(market, null);
                typeof(BuildingPlacementController).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, null);
                FactoryWorldData world = FreshWorld(market);
                world.buildings = new[] {
                    new SavedBuilding { definitionId = nameof(Belt), x = 0, y = 0,
                        belt = new SavedBelt { outputMask = BeltCell.Bit(GridDirection.East) | BeltCell.Bit(GridDirection.North),
                            nextOutputIndex = (int)GridDirection.East, nextInputIndex = 3,
                            item = SavedFood.From(controller.ProcessorRecipes[0].Input), progress = 0.4f } },
                    new SavedBuilding { definitionId = nameof(Belt), x = 1, y = 0, belt = new SavedBelt() },
                    new SavedBuilding { definitionId = nameof(Belt), x = 0, y = 1, belt = new SavedBelt() } };
                // Degrees0 faces North. The north neighbor exists before the east neighbor.
                controller.RestoreWorldSnapshot(world, Array.Empty<SavedUnlock>());
                SavedBelt restored = controller.CaptureWorldSnapshot().buildings.Single(building =>
                    building.definitionId == nameof(Belt) && building.x == 0 && building.y == 0).belt;
                Assert.That(restored.nextOutputIndex, Is.EqualTo((int)GridDirection.East));
                Assert.That(restored.nextInputIndex, Is.EqualTo(3));
                Assert.That(restored.outputMask, Is.EqualTo(world.buildings[0].belt.outputMask));
                Assert.That(restored.progress, Is.EqualTo(0.4f));
            });
        }

        private static FactoryWorldData CutterWorld(Market market, BuildingPlacementController controller)
        {
            FactoryWorldData world = FreshWorld(market);
            CuttingRecipe recipe = controller.CutterRecipes[0];
            world.buildings = new[] { new SavedBuilding { definitionId = nameof(Cutter), x = 0, y = -8,
                cutter = new SavedCutter { state = CutterState.Processing, elapsedSeconds = 0.5f,
                    input = SavedFood.From(recipe.Input), output = SavedFood.From(recipe.Output) } } };
            return world;
        }

        private static FactoryWorldData FreshWorld(Market market)
        {
            var territories = new TerritorySystem(market.Territories.Settings).Initialize();
            var generator = new PropertyWorldGenerator(territories.Settings);
            return new FactoryWorldData { worldSeed = territories.WorldSeed,
                reservedHubTerritory = new SavedTerritory(territories.ReservedHubTerritory),
                startingTerritory = new SavedTerritory(territories.StartingTerritory),
                purchasedTerritories = territories.Capture(),
                generatedTerritories = territories.RelevantCoordinates.Select(cell => new SavedTerritory(cell)).ToArray(),
                propertySources = territories.RelevantCoordinates.SelectMany(parcel => generator.Generate(territories.WorldSeed, parcel))
                    .Select(source => new SavedPropertySource(source)).ToArray() };
        }
        private static void Set(object target, string name, object value) => target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void WithScene(Action<Market, BuildingPlacementController> action)
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/Demo.unity");
            try { action(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Market>(true)).Single(),
                scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<BuildingPlacementController>(true)).Single()); }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
