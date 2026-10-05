using System;
using System.Collections.Generic;
using System.Linq;
using CozyFoodFactory.Buildings;
using CozyFoodFactory.Logistics;
using UnityEngine;

namespace CozyFoodFactory.Food
{
    [Serializable]
    public sealed class FactoryWorldData
    {
        public SavedBuilding[] buildings = Array.Empty<SavedBuilding>();
        public SavedPropertyConnection[] connections =
            Array.Empty<SavedPropertyConnection>();
        public int worldSeed;
        public int territoryGenerationVersion = TerritoryWorldSettings.GenerationVersion;
        public int territoryPurchaseCount;
        public SavedTerritory reservedHubTerritory;
        public SavedTerritory startingTerritory;
        public SavedTerritory[] purchasedTerritories = Array.Empty<SavedTerritory>();
        public SavedPropertySource[] propertySources = Array.Empty<SavedPropertySource>();
        public SavedTerritory[] generatedTerritories = Array.Empty<SavedTerritory>();
    }

    [Serializable]
    public sealed class SavedFood
    {
        public string id;
        public FoodItemKind kind;
        public int sellValue;

        public static SavedFood From(FoodItemData food) => food == null ? null : new SavedFood
        {
            id = food.Id, kind = food.Kind, sellValue = food.SellValue
        };

        public static SavedFood FromTransport(ITransportItem item)
        {
            if (item == null)
            {
                return null;
            }

            if (item is not FoodItemData food)
            {
                throw new InvalidOperationException(
                    "Factory snapshot cannot save active legacy RuneData or other non-food items.");
            }

            return From(food);
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(id) ||
                !Enum.IsDefined(typeof(FoodItemKind), kind) || sellValue <= 0)
            {
                throw new ArgumentException("Invalid saved food item.");
            }
        }

        public FoodItemData ToFood()
        {
            Validate();
            return new FoodItemData(id, kind, sellValue);
        }
    }

    [Serializable]
    public sealed class SavedBuilding
    {
        public string definitionId;
        public int x;
        public int y;
        public BuildingRotation rotation;
        public SavedFarmPlot farmPlot;
        public SavedHarvester harvester;
        public SavedBelt belt;
        public SavedProcessor processor;
        public SavedMixer mixer;
        public SavedCutter cutter;
        public SavedTradeBuilding tradeBuilding;
    }

    [Serializable]
    public sealed class SavedFarmPlot
    {
        public string cropId;
        public int matureCount;
        public float elapsedSeconds;
    }

    [Serializable]
    public sealed class SavedHarvester
    {
        public SavedFood[] outputs;
        public float elapsedSeconds;
    }

    [Serializable]
    public sealed class SavedBelt
    {
        public SavedFood item;
        public GridDirection entryDirection;
        public float progress;
        // Zero means the legacy single output specified by building rotation.
        public int outputMask;
        public int nextOutputIndex;
        public int nextInputIndex;
    }

    [Serializable]
    public sealed class SavedProcessor
    {
        public ProcessorState state;
        public SavedFood input;
        public CookingProperty activeProperty;
        public float elapsedSeconds;
        public SavedFood output;
    }

    [Serializable]
    public sealed class SavedMixer
    {
        public SavedFood slotA;
        public SavedFood slotB;
        public SavedFood output;
    }

    [Serializable]
    public sealed class SavedCutter
    {
        public CutterState state;
        public SavedFood input;
        public SavedFood output;
        public float elapsedSeconds;
    }

    [Serializable]
    public sealed class SavedPropertyConnection
    {
        public int x;
        public int y;
        public int sourceX;
        public int sourceY;
        public CookingProperty property;
        public PropertyConnectionKind kind;
        public int units;
    }

    public static class FactoryWorldSnapshotValidator
    {
        // JsonUtility expands null inline classes into empty objects. Only collapse the
        // complete four-placeholder pattern it writes for a captured building.
        public static void RestoreSerializedNulls(FactoryWorldData world)
        {
            if (world?.buildings == null)
            {
                return;
            }

            foreach (SavedBuilding building in world.buildings)
            {
                if (building == null || building.farmPlot == null ||
                    building.harvester == null || building.belt == null ||
                    building.processor == null || building.mixer == null)
                {
                    continue;
                }

                bool placeholdersMatch =
                    (building.definitionId == nameof(FarmPlot) || IsEmpty(building.farmPlot)) &&
                    (building.definitionId == nameof(Harvester) || IsEmpty(building.harvester)) &&
                    (building.definitionId == nameof(Belt) || IsEmpty(building.belt)) &&
                    (building.definitionId == nameof(Processor) || IsEmpty(building.processor)) &&
                    (building.definitionId == nameof(BasicMixer) || IsEmpty(building.mixer)) &&
                    (building.definitionId == nameof(Cutter) ||
                        building.cutter == null || IsEmpty(building.cutter)) &&
                    (building.definitionId == nameof(TradeBuilding) || building.tradeBuilding == null ||
                        string.IsNullOrEmpty(building.tradeBuilding.tradeId) &&
                        building.tradeBuilding.bufferedInput == 0 && building.tradeBuilding.pendingOutput == 0);
                if (!placeholdersMatch)
                {
                    continue;
                }

                if (building.definitionId != nameof(FarmPlot)) building.farmPlot = null;
                if (building.definitionId != nameof(Harvester)) building.harvester = null;
                if (building.definitionId != nameof(Belt)) building.belt = null;
                if (building.definitionId != nameof(Processor)) building.processor = null;
                if (building.definitionId != nameof(BasicMixer)) building.mixer = null;
                if (building.definitionId != nameof(Cutter)) building.cutter = null;
                if (building.definitionId != nameof(TradeBuilding)) building.tradeBuilding = null;

                if (building.belt != null && IsEmpty(building.belt.item))
                    building.belt.item = null;
                if (building.processor != null)
                {
                    if (IsEmpty(building.processor.input)) building.processor.input = null;
                    if (IsEmpty(building.processor.output)) building.processor.output = null;
                }
                if (building.mixer != null)
                {
                    if (IsEmpty(building.mixer.slotA)) building.mixer.slotA = null;
                    if (IsEmpty(building.mixer.slotB)) building.mixer.slotB = null;
                    if (IsEmpty(building.mixer.output)) building.mixer.output = null;
                }
                if (building.cutter != null)
                {
                    if (IsEmpty(building.cutter.input)) building.cutter.input = null;
                    if (IsEmpty(building.cutter.output)) building.cutter.output = null;
                }
            }
        }

        private static bool IsEmpty(SavedFood food) => food == null ||
            (string.IsNullOrEmpty(food.id) && food.kind == default &&
             food.sellValue == 0);

        private static bool IsEmpty(SavedFarmPlot state) =>
            string.IsNullOrEmpty(state.cropId) && state.matureCount == 0 &&
            state.elapsedSeconds == 0f;

        private static bool IsEmpty(SavedHarvester state) =>
            (state.outputs == null || state.outputs.Length == 0) &&
            state.elapsedSeconds == 0f;

        private static bool IsEmpty(SavedBelt state) => IsEmpty(state.item) &&
            state.entryDirection == default && state.progress == 0f &&
            state.outputMask == 0 && state.nextOutputIndex == 0 &&
            state.nextInputIndex == 0;

        private static bool IsEmpty(SavedProcessor state) =>
            state.state == default && IsEmpty(state.input) &&
            state.activeProperty == default && state.elapsedSeconds == 0f &&
            IsEmpty(state.output);

        private static bool IsEmpty(SavedMixer state) => IsEmpty(state.slotA) &&
            IsEmpty(state.slotB) && IsEmpty(state.output);

        private static bool IsEmpty(SavedCutter state) =>
            state.state == default && IsEmpty(state.input) &&
            IsEmpty(state.output) && state.elapsedSeconds == 0f;

        public static IEnumerable<SavedBuilding> ReconstructionOrder(
            FactoryWorldData world) => world.buildings
                .OrderBy(saved => saved.farmPlot != null ? 0 :
                    saved.harvester != null ? 1 : 2)
                .ThenBy(saved => saved.definitionId, StringComparer.Ordinal)
                .ThenBy(saved => saved.x).ThenBy(saved => saved.y);

        public static void Validate(FactoryWorldData world)
        {
            if (world?.buildings == null || world.connections == null)
            {
                throw new ArgumentException("The factory world snapshot is incomplete.");
            }

            var anchors = new HashSet<(string, int, int)>();
            foreach (SavedBuilding building in world.buildings)
            {
                if (building == null || string.IsNullOrWhiteSpace(building.definitionId) ||
                    !Enum.IsDefined(typeof(BuildingRotation), building.rotation) ||
                    !anchors.Add((building.definitionId, building.x, building.y)))
                {
                    throw new ArgumentException("Invalid or duplicate building anchor.");
                }

                int stateCount = (building.farmPlot != null ? 1 : 0) +
                    (building.harvester != null ? 1 : 0) +
                    (building.belt != null ? 1 : 0) +
                    (building.processor != null ? 1 : 0) +
                    (building.mixer != null ? 1 : 0) +
                    (building.cutter != null ? 1 : 0) +
                    (building.tradeBuilding != null ? 1 : 0);
                if (stateCount != 1 ||
                    building.definitionId != nameof(FarmPlot) && building.farmPlot != null ||
                    building.definitionId != nameof(Harvester) && building.harvester != null ||
                    building.definitionId != nameof(Belt) && building.belt != null ||
                    building.definitionId != nameof(Processor) && building.processor != null ||
                    building.definitionId != nameof(BasicMixer) && building.mixer != null ||
                    building.definitionId != nameof(Cutter) && building.cutter != null ||
                    building.definitionId != nameof(TradeBuilding) && building.tradeBuilding != null)
                {
                    string states = string.Join(", ", new[]
                    {
                        building.farmPlot != null ? nameof(building.farmPlot) : null,
                        building.harvester != null ? nameof(building.harvester) : null,
                        building.belt != null ? nameof(building.belt) : null,
                        building.processor != null ? nameof(building.processor) : null,
                        building.mixer != null ? nameof(building.mixer) : null,
                        building.cutter != null ? nameof(building.cutter) : null
                    }.Where(state => state != null));
                    throw new ArgumentException($"Building '{building.definitionId}' at " +
                        $"({building.x}, {building.y}) has mismatched state: {states}.");
                }

                ValidateState(building);
            }

            var connectionCells = new HashSet<Vector2Int>();
            foreach (SavedPropertyConnection connection in world.connections)
            {
                if (connection == null || !connectionCells.Add(new Vector2Int(
                        connection.x, connection.y)) ||
                    !Enum.IsDefined(typeof(CookingProperty), connection.property) ||
                    connection.kind is not (PropertyConnectionKind.Collector or
                        PropertyConnectionKind.Pipe or PropertyConnectionKind.Demand) ||
                    (connection.kind == PropertyConnectionKind.Demand
                        ? connection.units <= 0 : connection.units != 0))
                {
                    throw new ArgumentException("Invalid property connection.");
                }
            }
        }

        public static void ValidateAgainstScene(FactoryWorldData world,
            IReadOnlyList<BuildingPlacementOption> options,
            TerritoryWorldSettings territorySettings,
            IReadOnlyList<SavedUnlock> savedUnlocks,
            IReadOnlyList<ProcessingRecipe> processingRecipes,
            IReadOnlyList<MixingRecipe> mixingRecipes,
            Vector2Int marketCell,
            Vector2Int marketFootprint,
            IReadOnlyList<CuttingRecipe> cuttingRecipes = null,
            IReadOnlyList<TradeRecipe> tradeRecipes = null,
            float processorDuration = 1f, float cutterDuration = 1f)
        {
            cuttingRecipes ??= Array.Empty<CuttingRecipe>();
            tradeRecipes ??= Array.Empty<TradeRecipe>();
            Validate(world);
            if (options == null || territorySettings == null ||
                savedUnlocks == null || processingRecipes == null ||
                mixingRecipes == null)
            {
                throw new ArgumentNullException("Scene world definitions are missing.");
            }

            if (world.territoryGenerationVersion != TerritoryWorldSettings.GenerationVersion ||
                world.reservedHubTerritory == null ||
                world.reservedHubTerritory.Coordinate != territorySettings.HubTerritory ||
                world.startingTerritory == null ||
                world.startingTerritory.Coordinate != territorySettings.StartingTerritory ||
                world.purchasedTerritories == null || world.propertySources == null)
                throw new ArgumentException("Territory save data is missing or unsupported.");
            territorySettings.Validate(marketCell, marketFootprint);
            var territories = new TerritorySystem(territorySettings).Initialize();
            territories.Restore(world.worldSeed, world.purchasedTerritories,
                world.territoryPurchaseCount);
            if (world.generatedTerritories == null)
                throw new ArgumentException("Generated parcel records are missing.");
            var generated = new HashSet<Vector2Int>();
            foreach (SavedTerritory parcel in world.generatedTerritories)
                if (parcel == null || territories.IsReservedHub(parcel.Coordinate) ||
                    !generated.Add(parcel.Coordinate) ||
                    !territories.IsPurchased(parcel.Coordinate) &&
                        !territories.HasPurchasedNeighbor(parcel.Coordinate))
                    throw new ArgumentException("Generated parcel records are invalid.");
            if (!generated.SetEquals(territories.RelevantCoordinates))
                throw new ArgumentException("Generated parcels must cover owned land and its frontier.");
            PropertySourceSetup[] savedSources = world.propertySources
                .Where(source => source != null).Select(source => source.ToSetup())
                .OrderBy(source => source.cell.x).ThenBy(source => source.cell.y).ToArray();
            if (savedSources.Length != world.propertySources.Length ||
                savedSources.Select(source => source.cell).Distinct().Count() != savedSources.Length ||
                savedSources.Any(source => source.capacity < 1 ||
                    !Enum.IsDefined(typeof(CookingProperty), source.property) ||
                    !generated.Contains(territories.CoordinateAtCell(source.cell))))
                throw new ArgumentException("Saved Property Sources are invalid.");
            foreach (Vector2Int parcel in generated)
            {
                PropertySourceSetup[] patch = savedSources.Where(source =>
                    territories.CoordinateAtCell(source.cell) == parcel).ToArray();
                int hubDistance = Math.Abs(parcel.x - territories.ReservedHubTerritory.x) +
                    Math.Abs(parcel.y - territories.ReservedHubTerritory.y);
                if (hubDistance <= 1 || parcel == territories.StartingTerritory)
                {
                    if (patch.Length != 0)
                        throw new ArgumentException("The starting core must have no Property Sources.");
                    continue;
                }
                Vector2Int minimum = territories.ParcelMinimumCell(parcel);
                if (patch.Length < 4 || patch.Length > 6 ||
                    patch.Any(source => source.cell.x - minimum.x is < 2 or > 6 ||
                        source.cell.y - minimum.y is < 2 or > 6 ||
                        source.property != patch[0].property) ||
                    parcel == territories.StartingTerritory + Vector2Int.right &&
                        patch[0].property != CookingProperty.Heat ||
                    parcel == territories.StartingTerritory + Vector2Int.left &&
                        patch[0].property != CookingProperty.Water)
                    throw new ArgumentException("Saved Property deposit shape or type is invalid.");
                var connected = new HashSet<Vector2Int> { patch[0].cell };
                bool changed;
                do
                {
                    int before = connected.Count;
                    foreach (PropertySourceSetup source in patch)
                        if (connected.Any(cell => Math.Abs(cell.x - source.cell.x) +
                                Math.Abs(cell.y - source.cell.y) == 1)) connected.Add(source.cell);
                    changed = before != connected.Count;
                } while (changed);
                if (connected.Count != patch.Length)
                    throw new ArgumentException("Saved Property deposit is disconnected.");
            }


            var definitions = options.ToDictionary(option => option.Definition.Id,
                option => option.Definition, StringComparer.Ordinal);
            ResolveCurrentFoodValues(world, options, processingRecipes,
                mixingRecipes, cuttingRecipes, tradeRecipes);
            var sourceCells = savedSources.ToDictionary(source => source.cell);
            var cropUnlocks = new HashSet<string>(StringComparer.Ordinal);
            foreach (SavedUnlock unlock in savedUnlocks)
            {
                if (unlock?.category == UnlockKey.CropCategory)
                {
                    cropUnlocks.Add(unlock.id);
                }
            }

            var occupancy = new GridOccupancy();
            if (!occupancy.TryRegister("Market", marketCell, marketFootprint,
                    BuildingRotation.Degrees0, out _))
            {
                throw new ArgumentException("Scene fixtures overlap.");
            }

            foreach (PropertySourceSetup source in savedSources)
            {
                if (!occupancy.TryRegister("PropertySource", source.cell,
                        Vector2Int.one, BuildingRotation.Degrees0, out _))
                {
                    throw new ArgumentException("Property source overlaps a fixture.");
                }
            }

            foreach (SavedPropertyConnection connection in world.connections)
            {
                var cell = new Vector2Int(connection.x, connection.y);
                var sourceCell = new Vector2Int(connection.sourceX,
                    connection.sourceY);
                if (!territories.IsBuildableCell(cell) ||
                    !sourceCells.TryGetValue(sourceCell, out PropertySourceSetup source) ||
                    source.property != connection.property ||
                    connection.kind == PropertyConnectionKind.Collector &&
                    Mathf.Abs(cell.x - sourceCell.x) +
                    Mathf.Abs(cell.y - sourceCell.y) != 1 ||
                    !occupancy.TryRegister("PropertyConnection", cell,
                        Vector2Int.one, BuildingRotation.Degrees0, out _))
                {
                    throw new ArgumentException("Property connection conflicts with the scene.");
                }
            }

            foreach (SavedBuilding saved in world.buildings.Where(item =>
                item.definitionId != nameof(Harvester)))
            {
                Vector2Int anchor = new(saved.x, saved.y);
                if (!definitions.TryGetValue(saved.definitionId,
                        out BuildingDefinition definition) ||
                    !territories.ContainsBuildableFootprint(anchor,
                        definition.Footprint, saved.rotation) ||
                    !occupancy.TryRegister(definition, anchor,
                        saved.rotation, out _))
                {
                    throw new ArgumentException("Building conflicts with the scene.");
                }

                if (saved.farmPlot != null)
                {
                    CropDefinition selectedCrop = null;
                    if (!string.IsNullOrEmpty(saved.farmPlot.cropId))
                    {
                        FarmPlot plot = definition.InstancePrefab?.GetComponent<FarmPlot>();
                        CropDefinition crop = plot?.AvailableCrops.FirstOrDefault(item =>
                            item.Id == saved.farmPlot.cropId);
                        if (crop == null ||
                            !string.IsNullOrEmpty(crop.RequiredUnlockId) &&
                            !cropUnlocks.Contains(crop.RequiredUnlockId))
                        {
                            throw new ArgumentException("Saved crop is unavailable.");
                        }
                        selectedCrop = crop;
                    }

                    FarmPlot prefabPlot = definition.InstancePrefab?.GetComponent<FarmPlot>();
                    if (prefabPlot == null)
                    {
                        throw new ArgumentException("Farm Plot prefab is unavailable.");
                    }
                    new FarmPlotProcess(prefabPlot.MatureCapacity).Restore(selectedCrop,
                        saved.farmPlot.matureCount, saved.farmPlot.elapsedSeconds);
                }

                if (saved.processor != null)
                {
                    SavedProcessor process = saved.processor;
                    new ProcessorProcess(new ProcessingRecipeCatalog(processingRecipes),
                        processorDuration).Restore(process.state, process.input?.ToFood(),
                            process.activeProperty, process.output?.ToFood(),
                            process.elapsedSeconds);
                }

                if (saved.mixer != null)
                {
                    SavedMixer mixer = saved.mixer;
                    new BasicMixerProcess(new MixingRecipeCatalog(mixingRecipes)).Restore(
                        mixer.slotA?.ToFood(), mixer.slotB?.ToFood(), mixer.output?.ToFood());
                }

                if (saved.tradeBuilding != null)
                    new TradeProcess(tradeRecipes).Restore(saved.tradeBuilding);

                if (saved.cutter != null)
                    new CutterProcess(new CuttingRecipeCatalog(cuttingRecipes), cutterDuration)
                        .Restore(saved.cutter.state, saved.cutter.input?.ToFood(),
                            saved.cutter.output?.ToFood(), saved.cutter.elapsedSeconds);
            }

            foreach (SavedBuilding saved in world.buildings.Where(item =>
                item.definitionId == nameof(Harvester)))
            {
                if (!definitions.TryGetValue(nameof(Harvester),
                        out BuildingDefinition definition))
                {
                    throw new ArgumentException("Harvester definition is unavailable.");
                }
                Vector2Int anchor = new(saved.x, saved.y);
                if (!territories.ContainsBuildableFootprint(anchor,
                        definition.Footprint, saved.rotation))
                    throw new ArgumentException("Harvester is outside purchased territory.");
                Vector2Int farmCell = HarvesterPlacementBehavior.GetFarmCell(
                    anchor, definition.Footprint, saved.rotation);
                if (!occupancy.TryGetUnderlyingBuilding(farmCell,
                        out BuildingPlacement farmPlacement) ||
                    farmPlacement.DefinitionId != nameof(FarmPlot) ||
                    !occupancy.TryRegisterOver(definition.Id, anchor,
                        definition.Footprint, saved.rotation, farmCell,
                        farmPlacement, out _))
                {
                    throw new ArgumentException("Invalid Harvester overlay.");
                }

                Harvester prefab = definition.InstancePrefab?.GetComponent<Harvester>();
                if (prefab == null)
                {
                    throw new ArgumentException("Harvester output is unavailable.");
                }
                new HarvesterProcess(prefab.HarvestInterval, prefab.OutputCapacity).Restore(
                    saved.harvester.outputs.Select(food => food.ToFood()).ToArray(),
                    saved.harvester.elapsedSeconds);
            }
        }

        private static void ResolveCurrentFoodValues(FactoryWorldData world,
            IReadOnlyList<BuildingPlacementOption> options,
            IReadOnlyList<ProcessingRecipe> processing,
            IReadOnlyList<MixingRecipe> mixing, IReadOnlyList<CuttingRecipe> cutting,
            IReadOnlyList<TradeRecipe> trades)
        {
            var foods = new Dictionary<string, FoodItemData>(StringComparer.Ordinal);
            void Add(FoodItemData food)
            {
                if (food?.IsValid != true)
                    throw new ArgumentException("Invalid authored food.");
                if (foods.TryGetValue(food.Id, out FoodItemData previous) &&
                    (previous.Kind != food.Kind || previous.SellValue != food.SellValue))
                    throw new ArgumentException($"Inconsistent authored food: {food.Id}.");
                foods[food.Id] = food;
            }
            foreach (BuildingPlacementOption option in options)
            {
                FarmPlot plot = option.Definition.InstancePrefab?.GetComponent<FarmPlot>();
                if (plot != null)
                    foreach (CropDefinition crop in plot.AvailableCrops) Add(crop.Output);
            }
            foreach (ProcessingRecipe recipe in processing) { Add(recipe.Input); Add(recipe.Output); }
            foreach (MixingRecipe recipe in mixing)
            { Add(recipe.IngredientA); Add(recipe.IngredientB); Add(recipe.Output); }
            foreach (CuttingRecipe recipe in cutting) { Add(recipe.Input); Add(recipe.Output); }
            foreach (TradeRecipe recipe in trades) { Add(recipe.Input); Add(recipe.Output); }
            void Resolve(SavedFood saved)
            {
                if (saved == null) return;
                if (!foods.TryGetValue(saved.id, out FoodItemData current) || current.Kind != saved.kind)
                    throw new ArgumentException($"Saved food is unavailable: {saved.id}.");
                saved.sellValue = current.SellValue;
            }
            foreach (SavedBuilding building in world.buildings)
            {
                Resolve(building.belt?.item);
                if (building.harvester != null)
                    foreach (SavedFood food in building.harvester.outputs) Resolve(food);
                Resolve(building.processor?.input); Resolve(building.processor?.output);
                Resolve(building.mixer?.slotA); Resolve(building.mixer?.slotB); Resolve(building.mixer?.output);
                Resolve(building.cutter?.input); Resolve(building.cutter?.output);
            }
        }

        private static void ValidateState(SavedBuilding building)
        {
            SavedFarmPlot plot = building.farmPlot;
            if (plot != null && (plot.matureCount < 0 ||
                !IsFiniteNonnegative(plot.elapsedSeconds) ||
                string.IsNullOrEmpty(plot.cropId) &&
                (plot.matureCount != 0 || plot.elapsedSeconds != 0f)))
            {
                throw new ArgumentException("Invalid Farm Plot state.");
            }

            SavedHarvester harvester = building.harvester;
            if (harvester != null && (harvester.outputs == null ||
                !IsFiniteNonnegative(harvester.elapsedSeconds)))
            {
                throw new ArgumentException("Invalid Harvester state.");
            }

            if (harvester != null)
            {
                foreach (SavedFood food in harvester.outputs)
                {
                    food?.Validate();
                    if (food == null || food.kind != FoodItemKind.RawIngredient)
                    {
                        throw new ArgumentException("Invalid Harvester output.");
                    }
                }
            }

            SavedBelt belt = building.belt;
            if (belt != null && (!Enum.IsDefined(typeof(GridDirection),
                    belt.entryDirection) || !IsFiniteNonnegative(belt.progress) ||
                belt.progress > 1f || belt.item == null && belt.progress != 0f ||
                belt.outputMask < 0 || belt.outputMask > BeltCell.AllDirections ||
                belt.outputMask != 0 &&
                    (belt.outputMask & BeltCell.Bit(building.rotation.ToGridDirection())) == 0 ||
                belt.nextOutputIndex < 0 || belt.nextOutputIndex > 3 ||
                belt.nextInputIndex < 0 || belt.nextInputIndex > 3))
            {
                throw new ArgumentException("Invalid belt state.");
            }
            belt?.item?.Validate();

            SavedProcessor processor = building.processor;
            if (processor != null && (!Enum.IsDefined(typeof(ProcessorState),
                    processor.state) || !IsFiniteNonnegative(processor.elapsedSeconds) ||
                !Enum.IsDefined(typeof(CookingProperty), processor.activeProperty) ||
                processor.state == ProcessorState.Idle &&
                    (processor.input != null || processor.output != null ||
                     processor.elapsedSeconds != 0f) ||
                processor.state != ProcessorState.Idle &&
                    (processor.input == null || processor.output == null)))
            {
                throw new ArgumentException("Invalid Processor state.");
            }
            processor?.input?.Validate();
            processor?.output?.Validate();

            SavedMixer mixer = building.mixer;
            if (mixer != null && mixer.output != null &&
                (mixer.slotA != null || mixer.slotB != null))
            {
                throw new ArgumentException("Invalid Mixer state.");
            }
            mixer?.slotA?.Validate();
            mixer?.slotB?.Validate();
            mixer?.output?.Validate();

            if (building.tradeBuilding != null &&
                (building.tradeBuilding.bufferedInput < 0 || building.tradeBuilding.pendingOutput < 0 ||
                 building.tradeBuilding.bufferedInput > 0 && building.tradeBuilding.pendingOutput > 0 ||
                 string.IsNullOrEmpty(building.tradeBuilding.tradeId) &&
                 (building.tradeBuilding.bufferedInput != 0 || building.tradeBuilding.pendingOutput != 0)))
                throw new ArgumentException("Invalid Trade Building state.");
            SavedCutter cutter = building.cutter;
            if (cutter != null && (!Enum.IsDefined(typeof(CutterState), cutter.state) ||
                !IsFiniteNonnegative(cutter.elapsedSeconds) ||
                cutter.state == CutterState.Idle &&
                    (cutter.input != null || cutter.output != null ||
                     cutter.elapsedSeconds != 0f) ||
                cutter.state != CutterState.Idle &&
                    (cutter.input == null || cutter.output == null) ||
                cutter.state == CutterState.WaitingForOutputs &&
                    cutter.elapsedSeconds != 0f))
                throw new ArgumentException("Invalid Cutter state.");
            cutter?.input?.Validate();
            cutter?.output?.Validate();
        }

        private static bool IsFiniteNonnegative(float value) =>
            value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
