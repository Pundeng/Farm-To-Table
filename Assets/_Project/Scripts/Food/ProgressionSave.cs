using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FantasyShapez.Buildings;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FantasyShapez.Food
{
    [Serializable]
    public sealed class ProgressionSaveData
    {
        public int version = 2;
        public long currency;
        public SavedDelivery[] deliveries;
        public string[] completedOrderIds;
        public string activeOrderId;
        public SavedDelivery[] activeProgress;
        public SavedUnlock[] unlocks;
        public string[] purchasedCropIds;
        public SavedRecipe[] discoveries;
        public FactoryWorldData world;
    }

    [Serializable]
    public sealed class SavedDelivery
    {
        public string id;
        public FoodItemKind kind;
        public int count;
    }

    [Serializable]
    public sealed class SavedUnlock
    {
        public string category;
        public string id;
    }

    [Serializable]
    public sealed class SavedRecipe
    {
        public DiscoveredRecipeKind kind;
        public string ingredientAId;
        public FoodItemKind ingredientAKind;
        public string ingredientBId;
        public FoodItemKind ingredientBKind;
        public CookingProperty property;
        public string outputId;
        public FoodItemKind outputKind;
    }

    public sealed class ProgressionSaveService
    {
        private readonly MarketInventory inventory;
        private readonly FoodOrderSequence orders;
        private readonly UnlockState unlocks;
        private readonly SeedShop shop;
        private readonly RegionState regions;
        private readonly RecipeDiscoveryRegistry discoveries;
        private readonly IReadOnlyList<ProcessingRecipe> processorRecipes;
        private readonly IReadOnlyList<MixingRecipe> mixerRecipes;
        private IReadOnlyList<CuttingRecipe> cutterRecipes = Array.Empty<CuttingRecipe>();
        private bool isDemo;
        private readonly Func<FactoryWorldData> captureWorld;
        private readonly Action<FactoryWorldData, IReadOnlyList<SavedUnlock>> validateWorld;

        public ProgressionSaveService(Market market, BuildingPlacementController buildings)
            : this(market?.Inventory, market?.OrderSequence, market?.Unlocks,
                market?.SeedShop, market?.Regions, buildings?.RecipeDiscoveries,
                buildings?.ProcessorRecipes, buildings?.MixerRecipes)
        {
            if (buildings == null)
            {
                throw new ArgumentNullException(nameof(buildings));
            }

            captureWorld = buildings.CaptureWorldSnapshot;
            validateWorld = buildings.ValidateWorldSnapshot;
            cutterRecipes = buildings.CutterRecipes;
            isDemo = buildings.IsFoodDemo;
        }

        public ProgressionSaveService(MarketInventory inventory, FoodOrderSequence orders,
            UnlockState unlocks, SeedShop shop, RegionState regions,
            RecipeDiscoveryRegistry discoveries,
            IReadOnlyList<ProcessingRecipe> processorRecipes,
            IReadOnlyList<MixingRecipe> mixerRecipes,
            Func<FactoryWorldData> captureWorld = null,
            Action<FactoryWorldData, IReadOnlyList<SavedUnlock>> validateWorld = null)
        {
            this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            this.orders = orders ?? throw new ArgumentNullException(nameof(orders));
            this.unlocks = unlocks ?? throw new ArgumentNullException(nameof(unlocks));
            this.shop = shop ?? throw new ArgumentNullException(nameof(shop));
            this.regions = regions ?? throw new ArgumentNullException(nameof(regions));
            this.discoveries = discoveries ?? throw new ArgumentNullException(nameof(discoveries));
            this.processorRecipes = processorRecipes ??
                throw new ArgumentNullException(nameof(processorRecipes));
            this.mixerRecipes = mixerRecipes ?? throw new ArgumentNullException(nameof(mixerRecipes));
            this.captureWorld = captureWorld ?? (() => new FactoryWorldData());
            this.validateWorld = validateWorld ?? ((world, _) =>
                FactoryWorldSnapshotValidator.Validate(world));
        }

        public static string DefaultPath =>
            Path.Combine(Application.persistentDataPath, "cozy-food-factory-progress.json");

        public static string DemoPath =>
            Path.Combine(Application.persistentDataPath, "cozy-food-factory-demo.json");

        public string ToJson() => JsonUtility.ToJson(Capture(), true);

        public bool TryLoadJson(string json, out string error)
        {
            ProgressionSaveData data;
            try
            {
                data = JsonUtility.FromJson<ProgressionSaveData>(json);
                MigrateLegacyDemo(data);
                if (data?.version == 2)
                {
                    FactoryWorldSnapshotValidator.RestoreSerializedNulls(data.world);
                    validateWorld(data.world, data.unlocks);
                }
                else if (data?.version != 1)
                {
                    throw new ArgumentException("Missing or unsupported save version.");
                }

                ApplyValidated(data);
                error = null;
                return true;
            }
            catch (Exception exception) when (exception is ArgumentException or
                InvalidOperationException or NullReferenceException)
            {
                error = exception.Message;
                return false;
            }
        }

        public bool TrySave(string path, out string error)
        {
            string temporaryPath = path + ".tmp";
            try
            {
                File.WriteAllText(temporaryPath, ToJson(), new UTF8Encoding(false));
                if (File.Exists(path))
                {
                    File.Replace(temporaryPath, path, null);
                }
                else
                {
                    File.Move(temporaryPath, path);
                }

                error = null;
                return true;
            }
            catch (Exception exception) when (exception is IOException or
                UnauthorizedAccessException or ArgumentException or
                InvalidOperationException or NullReferenceException)
            {
                error = exception.Message;
                return false;
            }
            finally
            {
                try
                {
                    if (File.Exists(temporaryPath))
                    {
                        File.Delete(temporaryPath);
                    }
                }
                catch (IOException)
                {
                    // Cleanup must not replace the original save or hide the save result.
                }
                catch (UnauthorizedAccessException)
                {
                    // The original save remains untouched when replacement failed.
                }
            }
        }

        public bool TryLoad(string path, out string error)
        {
            try
            {
                if (!File.Exists(path))
                {
                    error = "No progression save exists at the displayed path.";
                    return false;
                }

                return TryLoadJson(File.ReadAllText(path), out error);
            }
            catch (Exception exception) when (exception is IOException or
                UnauthorizedAccessException or ArgumentException)
            {
                error = exception.Message;
                return false;
            }
        }

        public bool TryReadValidated(string path, out ProgressionSaveData data,
            out string error)
        {
            data = null;
            try
            {
                if (!File.Exists(path))
                {
                    error = "No progression save exists at the displayed path.";
                    return false;
                }

                data = JsonUtility.FromJson<ProgressionSaveData>(File.ReadAllText(path));
                MigrateLegacyDemo(data);
                if (data?.version == 2)
                {
                    FactoryWorldSnapshotValidator.RestoreSerializedNulls(data.world);
                    validateWorld(data.world, data.unlocks);
                }
                ApplyValidated(data, false);
                error = null;
                return true;
            }
            catch (Exception exception) when (exception is IOException or
                UnauthorizedAccessException or ArgumentException or
                InvalidOperationException or NullReferenceException)
            {
                data = null;
                error = exception.Message;
                return false;
            }
        }

        public void ApplySnapshot(ProgressionSaveData data)
        {
            if (data?.version == 2)
            {
                validateWorld(data.world, data.unlocks);
            }
            ApplyValidated(data);
        }

        private void MigrateLegacyDemo(ProgressionSaveData data)
        {
            if (!isDemo || orders.Orders.Count < 2 || data == null ||
                data.completedOrderIds == null || data.unlocks == null)
                return;

            bool legacyOrderPosition = data.completedOrderIds.Length == 0 &&
                    data.activeOrderId == "First Harvest" ||
                data.completedOrderIds.Length == 1 &&
                    data.completedOrderIds[0] == "First Harvest" &&
                    string.IsNullOrEmpty(data.activeOrderId);
            if (!legacyOrderPosition) return;

            var keys = data.unlocks.ToList();
            if (data.world?.buildings?.Any(building =>
                    building?.farmPlot?.cropId == "Tomato") == true)
                AddIfMissing(keys, UnlockKey.CropCategory, "Tomato");
            data.unlocks = keys.ToArray();

            // Milestone 01 had one order. Preserve its completed state and existing
            // factory while opening the new second order in memory. The file is
            // rewritten only by an explicit successful Save.
            bool completedOldOrder = data.completedOrderIds.Length == 1 &&
                data.completedOrderIds[0] == "First Harvest" &&
                string.IsNullOrEmpty(data.activeOrderId) &&
                data.activeProgress?.Length == 0;
            if (!completedOldOrder) return;

            AddIfMissing(keys, UnlockKey.MachineCategory, nameof(Processor));
            if (keys.Any(key => key?.category == UnlockKey.RegionCategory &&
                    key.id == "East Field"))
                AddIfMissing(keys, UnlockKey.CropCategory, "Potato");
            data.unlocks = keys.ToArray();
            data.activeOrderId = orders.Orders[1].Id;
            data.activeProgress = orders.Orders[1].Requirements.Select(requirement =>
                new SavedDelivery { id = requirement.Food.Id,
                    kind = requirement.Food.Kind, count = 0 }).ToArray();
        }

        private static void AddIfMissing(List<SavedUnlock> keys,
            string category, string id)
        {
            if (!keys.Any(key => key?.category == category && key.id == id))
                keys.Add(new SavedUnlock { category = category, id = id });
        }

        private ProgressionSaveData Capture()
        {
            FoodOrderProgress active = orders.ActiveOrder;
            var data = new ProgressionSaveData
            {
                currency = inventory.Currency,
                deliveries = inventory.DeliveredCounts.Select(entry => new SavedDelivery
                {
                    id = entry.Key.Id, kind = entry.Key.Kind, count = entry.Value
                }).ToArray(),
                completedOrderIds = orders.CompletedOrders.Select(order => order.Id).ToArray(),
                activeOrderId = active?.Order.Id,
                activeProgress = active == null ? Array.Empty<SavedDelivery>() :
                    active.Order.Requirements.Select(requirement => new SavedDelivery
                    {
                        id = requirement.Food.Id,
                        kind = requirement.Food.Kind,
                        count = active.GetDeliveredCount(requirement)
                    }).ToArray(),
                unlocks = unlocks.Unlocked.Select(key => new SavedUnlock
                {
                    category = key.Category, id = key.Id
                }).ToArray(),
                purchasedCropIds = shop.PurchasedCropIds.ToArray(),
                discoveries = discoveries.DiscoveredRecipes.Select(recipe => new SavedRecipe
                {
                    kind = recipe.Kind,
                    ingredientAId = recipe.IngredientA.Id,
                    ingredientAKind = recipe.IngredientA.Kind,
                    ingredientBId = recipe.IngredientB?.Id,
                    ingredientBKind = recipe.IngredientB?.Kind ?? default,
                    property = recipe.Property ?? default,
                    outputId = recipe.Output.Id,
                    outputKind = recipe.Output.Kind
                }).ToArray(),
                world = captureWorld()
            };

            validateWorld(data.world, data.unlocks);
            return data;
        }

        private void ApplyValidated(ProgressionSaveData data, bool apply = true)
        {
            if (data == null || data.version is not (1 or 2) || data.currency < 0 ||
                data.deliveries == null || data.completedOrderIds == null ||
                data.activeProgress == null || data.unlocks == null ||
                data.purchasedCropIds == null || data.discoveries == null)
            {
                throw new ArgumentException("Missing or unsupported progression save data.");
            }

            var deliveries = new Dictionary<FoodItemData, int>();
            foreach (SavedDelivery saved in data.deliveries)
            {
                FoodItemData food = ReadFood(saved?.id, saved?.kind ?? (FoodItemKind)(-1));
                if (saved.count <= 0 || !deliveries.TryAdd(food, saved.count))
                {
                    throw new ArgumentException("Invalid saved delivery counts.");
                }
            }

            long total = deliveries.Values.Sum(count => (long)count);
            if (total > int.MaxValue)
            {
                throw new ArgumentException("Too many saved deliveries.");
            }

            IReadOnlyList<FoodOrder> authoredOrders = orders.Orders;
            if (data.completedOrderIds.Length > authoredOrders.Count)
            {
                throw new ArgumentException("Unknown completed order.");
            }

            for (int index = 0; index < data.completedOrderIds.Length; index++)
            {
                if (data.completedOrderIds[index] != authoredOrders[index].Id)
                {
                    throw new ArgumentException("Completed orders are out of sequence.");
                }
            }

            int completedCount = data.completedOrderIds.Length;
            bool hasActive = completedCount < authoredOrders.Count;
            if (hasActive && data.activeOrderId != authoredOrders[completedCount].Id ||
                !hasActive && (!string.IsNullOrEmpty(data.activeOrderId) ||
                    data.activeProgress.Length != 0))
            {
                throw new ArgumentException("Invalid active order ID.");
            }

            Dictionary<FoodItemData, int> progress = null;
            if (hasActive)
            {
                progress = new Dictionary<FoodItemData, int>();
                FoodOrder active = authoredOrders[completedCount];
                if (data.activeProgress.Length != active.Requirements.Count)
                {
                    throw new ArgumentException("Incomplete saved order progress.");
                }

                foreach (SavedDelivery saved in data.activeProgress)
                {
                    FoodItemData food = ReadFood(saved?.id, saved?.kind ?? (FoodItemKind)(-1));
                    if (!progress.TryAdd(food, saved.count))
                    {
                        throw new ArgumentException("Duplicate order progress entry.");
                    }
                }

                foreach (FoodOrderRequirement requirement in active.Requirements)
                {
                    if (!progress.TryGetValue(requirement.Food, out int count) ||
                        count < 0 || count >= requirement.Quantity ||
                        count > (deliveries.TryGetValue(requirement.Food, out int delivered)
                            ? delivered : 0))
                    {
                        throw new ArgumentException("Invalid saved order progress.");
                    }
                }
            }

            var unlocks = new List<UnlockKey>();
            var uniqueUnlocks = new HashSet<UnlockKey>();
            foreach (SavedUnlock saved in data.unlocks)
            {
                var key = new UnlockKey(saved?.category, saved?.id);
                if (!uniqueUnlocks.Add(key))
                {
                    throw new ArgumentException("Duplicate saved unlock.");
                }

                unlocks.Add(key);
            }

            foreach (FoodOrder order in authoredOrders.Take(completedCount))
            {
                if (order.Unlocks.Any(key => !uniqueUnlocks.Contains(key)))
                {
                    throw new ArgumentException("A completed order is missing its reward.");
                }
            }

            var purchases = new HashSet<string>(StringComparer.Ordinal);
            foreach (string cropId in data.purchasedCropIds)
            {
                if (string.IsNullOrWhiteSpace(cropId) || !purchases.Add(cropId) ||
                    !shop.Offers.Any(offer => offer.CropId == cropId) ||
                    !uniqueUnlocks.Contains(new UnlockKey(UnlockKey.CropCategory, cropId)))
                {
                    throw new ArgumentException("Invalid saved Seed Shop purchase.");
                }
            }

            foreach (FarmableRegion region in regions.Regions)
            {
                var restored = new UnlockKey(UnlockKey.RegionCategory, region.Id);
                if (region.InitiallyRestored && !uniqueUnlocks.Contains(restored) ||
                    uniqueUnlocks.Contains(restored) && region.HasRequirement &&
                    !uniqueUnlocks.Contains(new UnlockKey(region.RequiredUnlockCategory,
                        region.RequiredUnlockId)) ||
                    uniqueUnlocks.Contains(restored) &&
                    region.RestorationUnlocks.Any(reward =>
                        !uniqueUnlocks.Contains(reward)))
                {
                    throw new ArgumentException("Invalid saved region state.");
                }
            }

            var discoveries = new List<DiscoveredRecipe>();
            var uniqueRecipes = new HashSet<DiscoveredRecipe>();
            foreach (SavedRecipe saved in data.discoveries)
            {
                DiscoveredRecipe recipe = ResolveRecipe(saved);
                if (recipe == null || !uniqueRecipes.Add(recipe))
                {
                    throw new ArgumentException("Unknown or duplicate saved discovery.");
                }

                discoveries.Add(recipe);
            }

            // All inputs are checked before changing the live session.
            if (!apply)
            {
                return;
            }

            inventory.Restore(data.currency, deliveries);
            this.unlocks.Restore(unlocks);
            shop.RestorePurchases(purchases);
            orders.Restore(completedCount, progress);
            this.discoveries.Restore(discoveries);
        }

        private DiscoveredRecipe ResolveRecipe(SavedRecipe saved)
        {
            if (saved == null || string.IsNullOrWhiteSpace(saved.ingredientAId) ||
                string.IsNullOrWhiteSpace(saved.outputId) ||
                !Enum.IsDefined(typeof(FoodItemKind), saved.ingredientAKind) ||
                !Enum.IsDefined(typeof(FoodItemKind), saved.outputKind))
            {
                return null;
            }

            if (saved.kind == DiscoveredRecipeKind.Processing)
            {
                ProcessingRecipe match = processorRecipes.FirstOrDefault(recipe =>
                    recipe.Input?.Id == saved.ingredientAId &&
                    recipe.Input.Kind == saved.ingredientAKind &&
                    recipe.Output?.Id == saved.outputId &&
                    recipe.Output.Kind == saved.outputKind &&
                    recipe.Property == saved.property);
                return match == null ? null : new DiscoveredRecipe(match);
            }

            if (saved.kind == DiscoveredRecipeKind.Mixing &&
                !string.IsNullOrWhiteSpace(saved.ingredientBId) &&
                Enum.IsDefined(typeof(FoodItemKind), saved.ingredientBKind))
            {
                MixingRecipe match = mixerRecipes.FirstOrDefault(recipe =>
                    recipe.Output?.Id == saved.outputId &&
                    recipe.Output.Kind == saved.outputKind &&
                    (recipe.IngredientA?.Id == saved.ingredientAId &&
                     recipe.IngredientA.Kind == saved.ingredientAKind &&
                     recipe.IngredientB?.Id == saved.ingredientBId &&
                     recipe.IngredientB.Kind == saved.ingredientBKind ||
                     recipe.IngredientA?.Id == saved.ingredientBId &&
                     recipe.IngredientA.Kind == saved.ingredientBKind &&
                     recipe.IngredientB?.Id == saved.ingredientAId &&
                     recipe.IngredientB.Kind == saved.ingredientAKind));
                return match == null ? null : new DiscoveredRecipe(match);
            }

            if (saved.kind == DiscoveredRecipeKind.Cutting)
            {
                CuttingRecipe match = cutterRecipes.FirstOrDefault(recipe =>
                    recipe.Input?.Id == saved.ingredientAId &&
                    recipe.Input.Kind == saved.ingredientAKind &&
                    recipe.Output?.Id == saved.outputId &&
                    recipe.Output.Kind == saved.outputKind);
                return match == null ? null : new DiscoveredRecipe(match);
            }

            return null;
        }

        private static FoodItemData ReadFood(string id, FoodItemKind kind)
        {
            if (string.IsNullOrWhiteSpace(id) ||
                !Enum.IsDefined(typeof(FoodItemKind), kind))
            {
                throw new ArgumentException("Invalid saved food ID.");
            }

            return new FoodItemData(id, kind);
        }
    }

    public static class FactoryWorldLoadSession
    {
        private static ProgressionSaveData pending;
        private static bool rollingBack;
        private static int sceneIndex;

        public static bool IsReconstructing { get; private set; }
        public static string LastMessage { get; private set; }

        public static bool TryBegin(ProgressionSaveData data, out string error)
        {
            if (IsReconstructing || data?.version != 2)
            {
                error = "A version 2 world load is required and no load may already be running.";
                return false;
            }

            sceneIndex = SceneManager.GetActiveScene().buildIndex;
            if (sceneIndex < 0)
            {
                error = "This scene is not in Build Settings; factory reload is unavailable.";
                return false;
            }

            pending = data;
            LastMessage = "Reconstructing factory...";
            IsReconstructing = true;
            rollingBack = false;
            SceneManager.sceneLoaded += OnSceneLoaded;
            if (SceneManager.LoadSceneAsync(sceneIndex, LoadSceneMode.Single) != null)
            {
                error = null;
                return true;
            }

            SceneManager.sceneLoaded -= OnSceneLoaded;
            IsReconstructing = false;
            pending = null;
            error = "The factory scene could not be reloaded.";
            LastMessage = error;
            return false;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (rollingBack)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                rollingBack = false;
                IsReconstructing = false;
                return;
            }

            try
            {
                BuildingPlacementController buildings = UnityEngine.Object
                    .FindAnyObjectByType<BuildingPlacementController>();
                if (buildings == null || buildings.Market == null)
                {
                    throw new InvalidOperationException(
                        "The reloaded scene has no factory controller or Market.");
                }

                var saves = new ProgressionSaveService(buildings.Market, buildings);
                saves.ApplySnapshot(pending);
                buildings.RestoreWorldSnapshot(pending.world, pending.unlocks);
                LastMessage = "Factory and progression loaded.";
                pending = null;
                SceneManager.sceneLoaded -= OnSceneLoaded;
                IsReconstructing = false;
            }
            catch (Exception exception)
            {
                LastMessage = $"Factory load failed: {exception.Message} " +
                    "Reloading a clean scene.";
                Debug.LogError(LastMessage);
                pending = null;
                rollingBack = true;
                if (SceneManager.LoadSceneAsync(sceneIndex, LoadSceneMode.Single) == null)
                {
                    LastMessage += " Clean reload also failed; simulation remains paused.";
                    SceneManager.sceneLoaded -= OnSceneLoaded;
                }
            }
        }
    }
}
