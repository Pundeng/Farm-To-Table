using System;
using System.Collections.Generic;
using System.Linq;
using FantasyShapez.Buildings;
using FantasyShapez.Logistics;
using UnityEngine;

namespace FantasyShapez.Food
{
    [Serializable]
    public sealed class FactoryWorldData
    {
        public SavedBuilding[] buildings = Array.Empty<SavedBuilding>();
        public SavedPropertyConnection[] connections =
            Array.Empty<SavedPropertyConnection>();
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
                        building.cutter == null || IsEmpty(building.cutter));
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
            state.entryDirection == default && state.progress == 0f;

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
                    (building.cutter != null ? 1 : 0);
                if (stateCount != 1 ||
                    building.definitionId != nameof(FarmPlot) && building.farmPlot != null ||
                    building.definitionId != nameof(Harvester) && building.harvester != null ||
                    building.definitionId != nameof(Belt) && building.belt != null ||
                    building.definitionId != nameof(Processor) && building.processor != null ||
                    building.definitionId != nameof(BasicMixer) && building.mixer != null ||
                    building.definitionId != nameof(Cutter) && building.cutter != null)
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
            IReadOnlyList<PropertySourceSetup> sources,
            RegionState regions,
            IReadOnlyList<SavedUnlock> savedUnlocks,
            IReadOnlyList<ProcessingRecipe> processingRecipes,
            IReadOnlyList<MixingRecipe> mixingRecipes,
            Vector2Int marketCell, Vector2Int? hubCell,
            IReadOnlyList<CuttingRecipe> cuttingRecipes = null)
        {
            cuttingRecipes ??= Array.Empty<CuttingRecipe>();
            Validate(world);
            if (options == null || sources == null || regions == null ||
                savedUnlocks == null || processingRecipes == null ||
                mixingRecipes == null)
            {
                throw new ArgumentNullException("Scene world definitions are missing.");
            }

            var definitions = options.ToDictionary(option => option.Definition.Id,
                option => option.Definition, StringComparer.Ordinal);
            var sourceCells = sources.ToDictionary(source => source.cell);
            var restoredRegions = new HashSet<string>(StringComparer.Ordinal);
            var cropUnlocks = new HashSet<string>(StringComparer.Ordinal);
            foreach (SavedUnlock unlock in savedUnlocks)
            {
                if (unlock?.category == UnlockKey.RegionCategory)
                {
                    restoredRegions.Add(unlock.id);
                }
                else if (unlock?.category == UnlockKey.CropCategory)
                {
                    cropUnlocks.Add(unlock.id);
                }
            }

            var occupancy = new GridOccupancy();
            if (!occupancy.TryRegister("Market", marketCell, Vector2Int.one,
                    BuildingRotation.Degrees0, out _) ||
                hubCell.HasValue && !occupancy.TryRegister("Hub", hubCell.Value,
                    Vector2Int.one, BuildingRotation.Degrees0, out _))
            {
                throw new ArgumentException("Scene fixtures overlap.");
            }

            foreach (Vector2Int fixedCell in sourceCells.Keys)
            {
                if (!occupancy.TryRegister("PropertySource", fixedCell,
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
                if (!sourceCells.TryGetValue(sourceCell, out PropertySourceSetup source) ||
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
                if (!definitions.TryGetValue(saved.definitionId,
                        out BuildingDefinition definition) ||
                    !occupancy.TryRegister(definition, new Vector2Int(saved.x, saved.y),
                        saved.rotation, out _))
                {
                    throw new ArgumentException("Building conflicts with the scene.");
                }

                if (saved.farmPlot != null)
                {
                    Vector2Int cell = new(saved.x, saved.y);
                    if (!regions.Regions.Any(region =>
                            restoredRegions.Contains(region.Id) && region.Contains(cell)))
                    {
                        throw new ArgumentException("Farm Plot is outside restored farmland.");
                    }

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
                    }

                    FarmPlot prefabPlot = definition.InstancePrefab?.GetComponent<FarmPlot>();
                    if (prefabPlot == null ||
                        saved.farmPlot.matureCount > prefabPlot.MatureCapacity)
                    {
                        throw new ArgumentException("Farm Plot capacity is invalid.");
                    }
                }

                if (saved.belt != null && saved.belt.item != null &&
                    !IsAuthoredFood(saved.belt.item, options, processingRecipes,
                        mixingRecipes, cuttingRecipes))
                {
                    throw new ArgumentException("Belt item is not authored in this scene.");
                }

                if (saved.processor != null)
                {
                    SavedProcessor process = saved.processor;
                    if (process.state != ProcessorState.Idle &&
                        !processingRecipes.Any(recipe =>
                            Matches(recipe.Input, process.input) &&
                            recipe.Property == process.activeProperty &&
                            Matches(recipe.Output, process.output)))
                    {
                        throw new ArgumentException("Processor recipe is unavailable.");
                    }
                }

                if (saved.mixer != null)
                {
                    SavedMixer mixer = saved.mixer;
                    if (mixer.slotA != null && mixer.slotB != null ||
                        mixer.slotA != null && !mixingRecipes.Any(recipe =>
                            Matches(recipe.IngredientA, mixer.slotA) ||
                            Matches(recipe.IngredientB, mixer.slotA)) ||
                        mixer.slotB != null && !mixingRecipes.Any(recipe =>
                            Matches(recipe.IngredientA, mixer.slotB) ||
                            Matches(recipe.IngredientB, mixer.slotB)) ||
                        mixer.output != null && !mixingRecipes.Any(recipe =>
                            Matches(recipe.Output, mixer.output)))
                    {
                        throw new ArgumentException("Mixer food is unavailable.");
                    }
                }

                if (saved.cutter != null && saved.cutter.state != CutterState.Idle &&
                    !cuttingRecipes.Any(recipe =>
                        Matches(recipe.Input, saved.cutter.input) &&
                        Matches(recipe.Output, saved.cutter.output)))
                    throw new ArgumentException("Cutter recipe is unavailable.");
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
                if (prefab == null ||
                    saved.harvester.outputs.Length > prefab.OutputCapacity ||
                    saved.harvester.outputs.Any(food =>
                        !IsAuthoredFood(food, options, processingRecipes,
                            mixingRecipes, cuttingRecipes)))
                {
                    throw new ArgumentException("Harvester output is unavailable.");
                }
            }
        }

        private static bool IsAuthoredFood(SavedFood food,
            IReadOnlyList<BuildingPlacementOption> options,
            IReadOnlyList<ProcessingRecipe> processingRecipes,
            IReadOnlyList<MixingRecipe> mixingRecipes,
            IReadOnlyList<CuttingRecipe> cuttingRecipes)
        {
            if (processingRecipes.Any(recipe => Matches(recipe.Input, food) ||
                    Matches(recipe.Output, food)) ||
                mixingRecipes.Any(recipe => Matches(recipe.IngredientA, food) ||
                    Matches(recipe.IngredientB, food) || Matches(recipe.Output, food)) ||
                cuttingRecipes.Any(recipe => Matches(recipe.Input, food) ||
                    Matches(recipe.Output, food)))
            {
                return true;
            }

            FarmPlot plot = options.FirstOrDefault(option =>
                option.Definition.Id == nameof(FarmPlot))?.Definition.InstancePrefab
                ?.GetComponent<FarmPlot>();
            return plot != null && plot.AvailableCrops.Any(crop =>
                Matches(crop.Output, food));
        }

        private static bool Matches(FoodItemData authored, SavedFood saved) =>
            authored != null && saved != null && authored.Id == saved.id &&
            authored.Kind == saved.kind && authored.SellValue == saved.sellValue;

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
                belt.progress > 1f || belt.item == null && belt.progress != 0f))
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
