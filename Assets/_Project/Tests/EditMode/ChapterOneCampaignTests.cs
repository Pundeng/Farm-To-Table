using System;
using System.Linq;
using CozyFoodFactory.Buildings;
using CozyFoodFactory.Food;
using CozyFoodFactory.Grid;
using CozyFoodFactory.Logistics;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CozyFoodFactory.Tests.EditMode
{
    public sealed class ChapterOneCampaignTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/Demo.unity";

        [Test]
        public void FixedMarket_UsesFiveByFiveFootprintCenteredOnItsAnchor()
        {
            WithScene((market, buildings) =>
            {
                Assert.That(market.Footprint, Is.EqualTo(new Vector2Int(5, 5)));
                Assert.That(market.InputPorts.Count, Is.EqualTo(12));
                Assert.That(market.InputPorts.Select(port => port.ExternalCell).Distinct().Count(), Is.EqualTo(12));
                Assert.That(market.transform.position,
                    Is.EqualTo(buildings.GridSystem.GridToWorld(
                        market.AnchorCell + new Vector2Int(2, 2))));
            });
        }

        [Test]
        public void MarketIsCenteredInReservedHubBesideSouthStartingParcel()
        {
            WithScene((market, _) =>
            {
                TerritorySystem territories = market.Territories;
                Assert.That(market.AnchorCell, Is.EqualTo(new Vector2Int(2, 2)));
                Assert.That(territories.CoordinateAtCell(market.AnchorCell),
                    Is.EqualTo(Vector2Int.zero));
                Assert.That(territories.GetPurchaseStatus(Vector2Int.zero, 1000),
                    Is.EqualTo(TerritoryPurchaseStatus.ReservedHub));
                Assert.That(territories.PurchasedCoordinates, Is.EquivalentTo(
                    new[] { new Vector2Int(-1, -1), new Vector2Int(0, -1),
                        new Vector2Int(1, -1), new Vector2Int(-1, 0),
                        new Vector2Int(1, 0), new Vector2Int(-1, 1),
                        new Vector2Int(0, 1), new Vector2Int(1, 1) }));
                Assert.That(territories.GetParcelVisualState(new Vector2Int(0, -1), 1000),
                    Is.EqualTo(TerritoryParcelVisualState.Owned));
                Assert.That(territories.GetParcelVisualState(new Vector2Int(0, -2), 1000),
                    Is.EqualTo(TerritoryParcelVisualState.Purchasable));
                Assert.That(territories.GetParcelVisualState(new Vector2Int(1, 0), 1000),
                    Is.EqualTo(TerritoryParcelVisualState.Owned));
            });
        }

        [Test]
        public void AuthoredChapter_HasTenObjectivesAndRequiredUnlocks()
        {
            WithScene((market, _) =>
            {
                string[] ids = { "O1", "O2", "O3", "O4", "O5", "O6", "O7", "O8", "O9", "O10" };
                string[] foods = { "carrot", "Roasted Carrot", "Tomato Sauce",
                    "Potato Slice", "French Fries", "Tomato Soup",
                    "Loaded Fries", "Garden Lunch", "Egg", "Tomato Omelette" };
                int[] quantities = { 30, 40, 70, 90, 150, 220, 300, 400, 500, 700 };
                Assert.That(market.Orders.Select(order => order.Id), Is.EqualTo(ids));
                for (int index = 0; index < ids.Length; index++)
                {
                    Assert.That(market.Orders[index].Requirements, Has.Count.EqualTo(1));
                    Assert.That(market.Orders[index].Requirements[0].Food.Id,
                        Is.EqualTo(foods[index]));
                    Assert.That(market.Orders[index].Requirements[0].Quantity,
                        Is.EqualTo(quantities[index]));
                }

                AssertUnlock(market.Orders[0], UnlockKey.MachineCategory, "Processor");
                AssertUnlock(market.Orders[1], UnlockKey.MachineCategory, "BasicMixer");
                AssertUnlock(market.Orders[1], UnlockKey.CropCategory, "Tomato");
                AssertUnlock(market.Orders[1], UnlockKey.CropCategory, "Onion");
                AssertUnlock(market.Orders[2], UnlockKey.MachineCategory, "Cutter");
                AssertUnlock(market.Orders[2], UnlockKey.CropCategory, "Potato");
                AssertUnlock(market.Orders[7], UnlockKey.MachineCategory, "TradeBuilding");
                AssertUnlock(market.Orders[9], "chapter", "chapter_1_complete");
                var tradeController = new SerializedObject(
                    FindBuildings(market.gameObject.scene));
                SerializedProperty trades = tradeController.FindProperty("tradeRecipes");
                Assert.That(trades.arraySize, Is.EqualTo(1));
                SerializedProperty chickenTrade = trades.GetArrayElementAtIndex(0);
                Assert.That(chickenTrade.FindPropertyRelative("id").stringValue,
                    Is.EqualTo("chicken-garden-egg"));
                Assert.That(chickenTrade.FindPropertyRelative("villageId").stringValue,
                    Is.EqualTo("chicken-village"));
                Assert.That(chickenTrade.FindPropertyRelative("inputQuantity").intValue,
                    Is.EqualTo(1));
                Assert.That(chickenTrade.FindPropertyRelative("outputQuantity").intValue,
                    Is.EqualTo(2));
                var marketData = new SerializedObject(market);
                Assert.That(marketData.FindProperty("seedOffers").arraySize, Is.Zero);
            });
        }

        [Test]
        public void AuthoredChapter_RecipesMatchTheEightRequiredTransformations()
        {
            WithScene((_, buildings) =>
            {
                Assert.That(buildings.ProcessorRecipes, Has.Count.EqualTo(3));
                Assert.That(buildings.MixerRecipes, Has.Count.EqualTo(4));
                Assert.That(buildings.CutterRecipes, Has.Count.EqualTo(1));
                AssertProcessing(buildings, "carrot", CookingProperty.Heat,
                    "Roasted Carrot");
                AssertProcessing(buildings, "Potato Slice", CookingProperty.Heat,
                    "French Fries");
                AssertProcessing(buildings, "Tomato Sauce", CookingProperty.Water,
                    "Tomato Soup");
                AssertMixing(buildings, "tomato", "onion", "Tomato Sauce");
                AssertMixing(buildings, "French Fries", "Tomato Sauce", "Loaded Fries");
                AssertMixing(buildings, "Loaded Fries", "Tomato Soup", "Garden Lunch");
                AssertMixing(buildings, "Egg", "Tomato Sauce", "Tomato Omelette");
                Assert.That(buildings.CutterRecipes[0].Input.Id, Is.EqualTo("potato"));
                Assert.That(buildings.CutterRecipes[0].Output.Id, Is.EqualTo("Potato Slice"));
                Assert.That(buildings.ProcessorRecipes.All(recipe =>
                    recipe.Property is CookingProperty.Heat or CookingProperty.Water), Is.True);
                Assert.That(buildings.ProcessorRecipes.Any(recipe =>
                    recipe.Output.Id is "Dried Apple" or "Vegetable Base"), Is.False);
            });
        }

        [Test]
        public void AuthoredChapter_RecipeCatalogsMatchUniquelyAndOldFoodDoesNotAdvanceO1()
        {
            WithScene((market, buildings) =>
            {
                var processing = new ProcessingRecipeCatalog(buildings.ProcessorRecipes);
                var mixing = new MixingRecipeCatalog(buildings.MixerRecipes);
                var cutting = new CuttingRecipeCatalog(buildings.CutterRecipes);
                FoodItemData raw(string id) => new(id, FoodItemKind.RawIngredient);
                FoodItemData prepared(string id) =>
                    new(id, FoodItemKind.ProcessedFood);

                Assert.That(processing.Find(raw("carrot"), CookingProperty.Heat,
                    out ProcessingRecipe roasted), Is.EqualTo(ProcessingRecipeMatch.Unique));
                Assert.That(roasted.Output.Id, Is.EqualTo("Roasted Carrot"));
                Assert.That(processing.Find(prepared("Potato Slice"), CookingProperty.Heat,
                    out ProcessingRecipe fries), Is.EqualTo(ProcessingRecipeMatch.Unique));
                Assert.That(fries.Output.Id, Is.EqualTo("French Fries"));
                Assert.That(processing.Find(prepared("Tomato Sauce"), CookingProperty.Water,
                    out ProcessingRecipe soup), Is.EqualTo(ProcessingRecipeMatch.Unique));
                Assert.That(soup.Output.Id, Is.EqualTo("Tomato Soup"));
                Assert.That(mixing.Find(raw("tomato"), raw("onion"),
                    out MixingRecipe sauce), Is.EqualTo(ProcessingRecipeMatch.Unique));
                Assert.That(sauce.Output.Id, Is.EqualTo("Tomato Sauce"));
                Assert.That(mixing.Find(prepared("French Fries"), prepared("Tomato Sauce"),
                    out MixingRecipe loaded), Is.EqualTo(ProcessingRecipeMatch.Unique));
                Assert.That(loaded.Output.Id, Is.EqualTo("Loaded Fries"));
                Assert.That(mixing.Find(prepared("Loaded Fries"), prepared("Tomato Soup"),
                    out MixingRecipe lunch), Is.EqualTo(ProcessingRecipeMatch.Unique));
                Assert.That(lunch.Output.Id, Is.EqualTo("Garden Lunch"));
                Assert.That(mixing.Find(raw("Egg"), prepared("Tomato Sauce"),
                    out _), Is.EqualTo(ProcessingRecipeMatch.Unique));
                var omeletteMixer = new BasicMixerProcess(mixing);
                Assert.That(omeletteMixer.TryAccept(0, raw("Egg")), Is.True);
                Assert.That(omeletteMixer.TryAccept(1, prepared("Tomato Sauce")), Is.True);
                Assert.That(omeletteMixer.PeekOutput().Id, Is.EqualTo("Tomato Omelette"));
                Assert.That(cutting.Find(raw("potato"), out CuttingRecipe slices),
                    Is.EqualTo(ProcessingRecipeMatch.Unique));
                var cutter = new CutterProcess(cutting, 1f);
                Assert.That(cutter.TryAccept(raw("potato")), Is.True);
                Assert.That(cutter.Advance(1f, true), Is.True);
                Assert.That(cutter.TryTakePair(out FoodItemData first,
                    out FoodItemData second), Is.True);
                Assert.That(first.Id, Is.EqualTo("Potato Slice"));
                Assert.That(second.Id, Is.EqualTo("Potato Slice"));

                Assert.That(processing.Find(raw("apple"), CookingProperty.Cold,
                    out _), Is.EqualTo(ProcessingRecipeMatch.None));
                var receiver = new MarketReceiver(Vector2Int.zero, new MarketInventory());
                using var orders = new FoodOrderSequence(market.Orders, receiver,
                    new UnlockState());
                receiver.TryAcceptItem(raw("apple"), GridDirection.East);
                receiver.TryAcceptItem(prepared("Dried Apple"), GridDirection.East);
                Assert.That(orders.ActiveOrder.Order.Id, Is.EqualTo("O1"));
                Assert.That(orders.ActiveOrder.GetDeliveredCount(
                    orders.ActiveOrder.Order.Requirements[0]), Is.Zero);
            });
        }

        [Test]
        public void AuthoredChapter_DeliveriesAdvanceEveryStageAndGrantOnlyDueRewards()
        {
            WithScene((market, _) =>
            {
                var receiver = new MarketReceiver(Vector2Int.zero, new MarketInventory());
                var unlocks = new UnlockState();
                using var sequence = new FoodOrderSequence(market.Orders, receiver, unlocks);
                Assert.That(unlocks.IsUnlocked(UnlockKey.MachineCategory, "Processor"),
                    Is.False);
                for (int index = 0; index < market.Orders.Count; index++)
                {
                    FoodOrder order = market.Orders[index];
                    Assert.That(sequence.ActiveOrder.Order.Id, Is.EqualTo(order.Id));
                    FoodOrderRequirement requirement = order.Requirements[0];
                    for (int delivered = 0; delivered < requirement.Quantity; delivered++)
                        Assert.That(receiver.TryAcceptItem(requirement.Food,
                            GridDirection.East), Is.True);
                    Assert.That(sequence.CompletedOrders, Has.Count.EqualTo(index + 1));
                    foreach (UnlockKey reward in order.Unlocks)
                        Assert.That(unlocks.IsUnlocked(reward.Category, reward.Id), Is.True);
                    if (index == 0)
                        Assert.That(unlocks.IsUnlocked(UnlockKey.MachineCategory,
                            "BasicMixer"), Is.False);
                    if (index == 4)
                        Assert.That(unlocks.IsUnlocked("chapter", "chapter_1_complete"),
                            Is.False);
                    if (index == 7)
                    {
                        Assert.That(unlocks.IsUnlocked(UnlockKey.MachineCategory,
                            "TradeBuilding"), Is.True);
                        Assert.That(unlocks.IsUnlocked("chapter", "chapter_1_complete"),
                            Is.False);
                    }
                }
                Assert.That(sequence.ActiveOrder, Is.Null);
                Assert.That(unlocks.IsUnlocked("chapter", "chapter_1_complete"), Is.True);
            });
        }

        [Test]
        public void AuthoredFarm_OffersCarrotFirstAndKeepsFruitOutsideMainObjectives()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/DemoFarmPlot.prefab");
            Assert.That(prefab, Is.Not.Null);
            FarmPlot plot = prefab.GetComponent<FarmPlot>();
            Assert.That(plot, Is.Not.Null);
            Assert.That(plot.AvailableCrops[0].Id, Is.EqualTo("Carrot"));
            Assert.That(plot.AvailableCrops.Any(crop => crop.Id == "Apple"), Is.True);
            Assert.That(plot.AvailableCrops.Any(crop => crop.Id == "Basil"), Is.False);
        }

        [Test]
        public void FourPropertySlotsKeepTheirSerializedNumbers()
        {
            Assert.That((int)CookingProperty.Heat, Is.Zero);
            Assert.That((int)CookingProperty.Water, Is.EqualTo(1));
            Assert.That((int)CookingProperty.Time, Is.EqualTo(2));
            Assert.That((int)CookingProperty.Cold, Is.EqualTo(3));
            WithScene((_, buildings) =>
                Assert.That(buildings.ProcessorRecipes.Select(recipe => recipe.Property),
                    Is.SubsetOf(new[] { CookingProperty.Heat, CookingProperty.Water })));
        }

        [Test]
        public void ChapterSourceAccess_OpensHeatThenWaterAndLeavesTimeColdUnavailable()
        {
            var root = new GameObject("Chapter Property access");
            try
            {
                var grid = root.AddComponent<GridSystem>();
                var occupancy = new GridOccupancy();
                int completed = 0;
                var sources = new[]
                {
                    new PropertySourceSetup { cell = new Vector2Int(0, 0),
                        property = CookingProperty.Heat },
                    new PropertySourceSetup { cell = new Vector2Int(0, 5),
                        property = CookingProperty.Water },
                    new PropertySourceSetup { cell = new Vector2Int(0, 10),
                        property = CookingProperty.Time },
                    new PropertySourceSetup { cell = new Vector2Int(0, 15),
                        property = CookingProperty.Cold }
                };
                var supply = new PropertySupplyPlayMode(grid, null, occupancy,
                    root.transform, sources, false, null,
                    property => property == CookingProperty.Heat && completed >= 1 ||
                        property == CookingProperty.Water && completed >= 5);
                Assert.That(supply.TryPlaceCollector(Vector2Int.right,
                    sources[0].cell), Is.False);
                completed = 1;
                Assert.That(supply.TryPlaceCollector(Vector2Int.right,
                    sources[0].cell), Is.True);
                Assert.That(supply.TryPlaceCollector(new Vector2Int(1, 5),
                    sources[1].cell), Is.False);
                completed = 5;
                Assert.That(supply.TryPlaceCollector(new Vector2Int(1, 5),
                    sources[1].cell), Is.True);
                Assert.That(supply.TryPlaceCollector(new Vector2Int(1, 10),
                    sources[2].cell), Is.False);
                Assert.That(supply.TryPlaceCollector(new Vector2Int(1, 15),
                    sources[3].cell), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [TestCase("Egg", FoodItemKind.RawIngredient, "O9", 500, 499)]
        [TestCase("Tomato Omelette", FoodItemKind.ProcessedFood, "O10", 700, 699)]
        public void LargeObjectiveProgressSurvivesSaveAndCompletesAtTarget(
            string foodId, FoodItemKind kind, string objective, int target, int beforeTarget)
        {
            var food = new FoodItemData(foodId, kind);
            using var source = new LargeOrderSession(food, objective, target);
            for (int index = 0; index < beforeTarget; index++)
                Assert.That(source.Receiver.TryAcceptItem(food, GridDirection.East), Is.True);
            Assert.That(source.Orders.ActiveOrder.GetDeliveredCount(
                source.Orders.ActiveOrder.Order.Requirements[0]), Is.EqualTo(beforeTarget));

            string json = source.Saves.ToJson();
            using var restored = new LargeOrderSession(food, objective, target);
            Assert.That(restored.Saves.TryLoadJson(json, out string error), Is.True, error);
            Assert.That(restored.Orders.ActiveOrder.GetDeliveredCount(
                restored.Orders.ActiveOrder.Order.Requirements[0]), Is.EqualTo(beforeTarget));
            restored.Receiver.TryAcceptItem(food, GridDirection.East);
            Assert.That(restored.Orders.ActiveOrder, Is.Null);
            Assert.That(restored.Unlocks.IsUnlocked("chapter", "chapter_1_complete"),
                Is.EqualTo(objective == "O10"));
        }

        private static void AssertUnlock(FoodOrder order, string category, string id) =>
            Assert.That(order.Unlocks.Any(key => key.Category == category && key.Id == id),
                Is.True, $"Missing {category}:{id} after {order.Id}");

        private static void AssertProcessing(BuildingPlacementController buildings,
            string input, CookingProperty property, string output) =>
            Assert.That(buildings.ProcessorRecipes.Any(recipe =>
                recipe.Input.Id == input && recipe.Property == property &&
                recipe.Output.Id == output), Is.True, output);

        private static void AssertMixing(BuildingPlacementController buildings,
            string a, string b, string output) =>
            Assert.That(buildings.MixerRecipes.Any(recipe =>
                recipe.Matches(new FoodItemData(a, a is "tomato" or "onion"
                    ? FoodItemKind.RawIngredient : FoodItemKind.ProcessedFood),
                    new FoodItemData(b, b is "tomato" or "onion"
                    ? FoodItemKind.RawIngredient : FoodItemKind.ProcessedFood)) &&
                recipe.Output.Id == output), Is.True, output);

        private static void WithScene(Action<Market, BuildingPlacementController> inspect)
        {
            Scene scene = EditorSceneManager.OpenPreviewScene(ScenePath);
            try
            {
                Market market = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Market>(true))
                    .Single();
                BuildingPlacementController buildings = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<BuildingPlacementController>(true))
                    .Single();
                inspect(market, buildings);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static BuildingPlacementController FindBuildings(Scene scene) =>
            scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<BuildingPlacementController>(true))
                .Single();

        private sealed class LargeOrderSession : IDisposable
        {
            public readonly MarketReceiver Receiver;
            public readonly FoodOrderSequence Orders;
            public readonly UnlockState Unlocks = new();
            public readonly ProgressionSaveService Saves;

            public LargeOrderSession(FoodItemData food, string objective, int quantity)
            {
                var inventory = new MarketInventory();
                Receiver = new MarketReceiver(Vector2Int.zero, inventory);
                Orders = new FoodOrderSequence(new[]
                {
                    new FoodOrder(objective, food.Id,
                        new[] { new FoodOrderRequirement(food, quantity) },
                        objective == "O10"
                            ? new[] { new UnlockKey("chapter", "chapter_1_complete") }
                            : Array.Empty<UnlockKey>())
                }, Receiver, Unlocks);
                Saves = new ProgressionSaveService(inventory, Orders, Unlocks,
                    new SeedShop(Array.Empty<SeedShopOffer>(), inventory, Unlocks),
                    new RecipeDiscoveryRegistry(), Array.Empty<ProcessingRecipe>(),
                    Array.Empty<MixingRecipe>());
            }

            public void Dispose() => Orders.Dispose();
        }
    }
}
