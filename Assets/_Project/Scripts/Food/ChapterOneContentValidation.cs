using System;
using System.Collections.Generic;
using System.Linq;
using CozyFoodFactory.Buildings;
using CozyFoodFactory.Logistics;

namespace CozyFoodFactory.Food
{
    // Pure static check used by the Editor command and EditMode tests, never gameplay.
    public static class ChapterOneContentValidation
    {
        public static IReadOnlyList<string> Validate(
            IReadOnlyList<BuildingPlacementOption> buildings, IReadOnlyList<CropDefinition> crops,
            IReadOnlyList<FoodOrder> orders, IReadOnlyList<ProcessingRecipe> processing,
            IReadOnlyList<MixingRecipe> mixing, IReadOnlyList<CuttingRecipe> cutting,
            IReadOnlyList<TradeRecipe> trades, string villageId,
            IReadOnlyList<CookingProperty> properties)
        {
            var errors = new List<string>();
            if (buildings == null || crops == null || orders == null || processing == null ||
                mixing == null || cutting == null || trades == null || properties == null)
                return new[] { "Chapter 1 configuration has missing collections." };
            var buildingIds = new HashSet<string>(StringComparer.Ordinal);
            var supported = new HashSet<string> { nameof(Belt), nameof(FarmPlot), nameof(Harvester),
                nameof(Processor), nameof(BasicMixer), nameof(Cutter), nameof(TradeBuilding) };
            foreach (BuildingPlacementOption option in buildings)
            {
                string id = option?.Definition?.Id;
                if (string.IsNullOrWhiteSpace(id) || !buildingIds.Add(id))
                    errors.Add($"Missing or duplicate building ID: {id}.");
                else if (!supported.Contains(id)) errors.Add($"Unsupported Chapter 1 machine family: {id}.");
                if (option?.Definition != null && (option.Definition.Footprint.x < 1 ||
                    option.Definition.Footprint.y < 1)) errors.Add($"Invalid footprint: {id}.");
            }
            foreach (string id in new[] { nameof(FarmPlot), nameof(Harvester), nameof(Belt) })
                if (!buildingIds.Contains(id)) errors.Add($"Missing production building: {id}.");

            var foods = new Dictionary<string, FoodItemData>(StringComparer.Ordinal);
            void Food(FoodItemData food, string context)
            {
                if (food?.IsValid != true) { errors.Add($"{context}: invalid food."); return; }
                if (foods.TryGetValue(food.Id, out FoodItemData previous) &&
                    (previous.Kind != food.Kind || previous.SellValue != food.SellValue))
                    errors.Add($"{context}: food {food.Id} has inconsistent kind or sell value.");
                foods[food.Id] = food;
            }
            void Unique(HashSet<string> ids, string id, string context)
            {
                if (string.IsNullOrWhiteSpace(id) || !ids.Add(id))
                    errors.Add($"{context}: missing or duplicate ID {id}.");
            }
            void Family(string id, int count)
            {
                if (count > 0 && !buildingIds.Contains(id))
                    errors.Add($"Recipes require unavailable machine family {id}.");
            }
            var cropIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (CropDefinition crop in crops)
            {
                Unique(cropIds, crop?.Id, "Crop");
                if (crop == null) continue;
                try { crop.Validate(); } catch (InvalidOperationException e) { errors.Add(e.Message); }
                Food(crop.Output, $"Crop {crop.Id}");
            }
            Family(nameof(Processor), processing.Count); Family(nameof(BasicMixer), mixing.Count);
            Family(nameof(Cutter), cutting.Count); Family(nameof(TradeBuilding), trades.Count);
            var processSignatures = new HashSet<(FoodItemData, CookingProperty)>();
            foreach (ProcessingRecipe recipe in processing)
            {
                if (recipe == null) { errors.Add("Missing processing recipe."); continue; }
                Food(recipe.Input, "Processing input"); Food(recipe.Output, "Processing output");
                if (!Enum.IsDefined(typeof(CookingProperty), recipe.Property)) errors.Add("Invalid recipe property.");
                if (!processSignatures.Add((recipe.Input, recipe.Property)))
                    errors.Add($"Ambiguous Processor signature: {recipe.Input?.Id}/{recipe.Property}.");
            }
            var mixerCatalog = new MixingRecipeCatalog(mixing);
            foreach (MixingRecipe recipe in mixing)
            {
                if (recipe == null) { errors.Add("Missing Mixer recipe."); continue; }
                Food(recipe.IngredientA, "Mixer input A"); Food(recipe.IngredientB, "Mixer input B");
                Food(recipe.Output, "Mixer output");
                if (mixerCatalog.Find(recipe.IngredientA, recipe.IngredientB, out _) != ProcessingRecipeMatch.Unique)
                    errors.Add($"Ambiguous Mixer signature: {recipe.IngredientA?.Id}+{recipe.IngredientB?.Id}.");
            }
            var cutterInputs = new HashSet<FoodItemData>();
            foreach (CuttingRecipe recipe in cutting)
            {
                if (recipe == null) { errors.Add("Missing Cutter recipe."); continue; }
                Food(recipe.Input, "Cutter input"); Food(recipe.Output, "Cutter output");
                if (!cutterInputs.Add(recipe.Input)) errors.Add($"Ambiguous Cutter input: {recipe.Input?.Id}.");
            }
            var tradeIds = new HashSet<string>(StringComparer.Ordinal);
            var tradeInputs = new HashSet<FoodItemData>();
            foreach (TradeRecipe recipe in trades)
            {
                Unique(tradeIds, recipe?.Id, "Trade");
                if (recipe == null) continue;
                try { recipe.Validate(); } catch (ArgumentException e) { errors.Add(e.Message); }
                Food(recipe.Input, "Trade input"); Food(recipe.Output, "Trade output");
                if (recipe.VillageId != villageId) errors.Add($"Trade {recipe.Id}: unavailable village {recipe.VillageId}.");
                else if (!tradeInputs.Add(recipe.Input)) errors.Add($"Ambiguous trade input: {recipe.Input?.Id}.");
            }
            if (trades.Count > 0 && string.IsNullOrWhiteSpace(villageId)) errors.Add("Missing Chapter 1 village ID.");
            string objectiveError = CampaignObjectiveValidation.GetError(orders);
            if (objectiveError != null) errors.Add(objectiveError);
            foreach (FoodOrder order in orders)
            {
                if (order?.Requirements == null || order.Unlocks == null) continue;
                foreach (FoodOrderRequirement requirement in order.Requirements)
                    Food(requirement?.Food, $"Objective {order.Id}");
                foreach (UnlockKey unlock in order.Unlocks.Where(key => key != null))
                {
                    if (unlock.Category == UnlockKey.MachineCategory && !buildingIds.Contains(unlock.Id))
                        errors.Add($"Objective {order.Id}: missing machine unlock target {unlock.Id}.");
                    if (unlock.Category == UnlockKey.CropCategory &&
                        !crops.Any(crop => crop?.RequiredUnlockId == unlock.Id))
                        errors.Add($"Objective {order.Id}: missing crop unlock target {unlock.Id}.");
                }
            }
            if (errors.Count != 0) return errors;

            var unlocked = new HashSet<UnlockKey>();
            var available = new HashSet<FoodItemData>();
            bool Machine(string id) => buildingIds.Contains(id) &&
                unlocked.Contains(new UnlockKey(UnlockKey.MachineCategory, id));
            for (int stage = 0; stage < orders.Count; stage++)
            {
                foreach (CropDefinition crop in crops)
                    if (string.IsNullOrEmpty(crop.RequiredUnlockId) ||
                        unlocked.Contains(new UnlockKey(UnlockKey.CropCategory, crop.RequiredUnlockId)))
                        available.Add(crop.Output);
                bool changed;
                do
                {
                    int before = available.Count;
                    if (Machine(nameof(Processor)))
                        foreach (ProcessingRecipe recipe in processing)
                            if (available.Contains(recipe.Input) && properties.Contains(recipe.Property) &&
                                BuildingPlacementController.IsChapterOnePropertyAvailable(recipe.Property, stage))
                                available.Add(recipe.Output);
                    if (Machine(nameof(BasicMixer)))
                        foreach (MixingRecipe recipe in mixing)
                            if (available.Contains(recipe.IngredientA) && available.Contains(recipe.IngredientB))
                                available.Add(recipe.Output);
                    if (Machine(nameof(Cutter)) && CutterProcess.OutputQuantity > 0)
                        foreach (CuttingRecipe recipe in cutting)
                            if (available.Contains(recipe.Input)) available.Add(recipe.Output);
                    if (Machine(nameof(TradeBuilding)))
                        foreach (TradeRecipe recipe in trades)
                            if (available.Contains(recipe.Input)) available.Add(recipe.Output);
                    changed = before != available.Count;
                } while (changed);
                FoodOrder order = orders[stage];
                foreach (FoodOrderRequirement requirement in order.Requirements)
                    if (!available.Contains(requirement.Food))
                        errors.Add($"Objective {order.Id}: {requirement.Food.Id} cannot be produced before its reward. " +
                            Explain(requirement.Food, stage) + " " +
                            $"Available foods: {string.Join(", ", available.Select(food => food.Id).OrderBy(id => id))}.");
                if (errors.Count != 0) return errors;
                foreach (UnlockKey unlock in order.Unlocks) unlocked.Add(unlock);
            }
            foreach (FoodItemData food in foods.Values)
                if (!available.Contains(food)) errors.Add($"Content {food.Id} has no obtainable Chapter 1 producer.");
            return errors;

            string Explain(FoodItemData food, int stage)
            {
                var dependencies = new List<string>();
                string Input(FoodItemData item) => item.Id + (available.Contains(item) ? " available" : " unavailable");
                string Gate(string machine) => Machine(machine) ? "unlocked" : "locked";
                foreach (CropDefinition crop in crops.Where(crop => crop.Output.Equals(food)))
                    dependencies.Add($"crop {crop.Id} requires crop unlock {crop.RequiredUnlockId}");
                foreach (ProcessingRecipe recipe in processing.Where(recipe => recipe.Output.Equals(food)))
                    dependencies.Add($"Processor {Gate(nameof(Processor))}; {Input(recipe.Input)}; " +
                        $"{recipe.Property} deposit {(properties.Contains(recipe.Property) ? "present" : "missing")}, " +
                        $"access {(BuildingPlacementController.IsChapterOnePropertyAvailable(recipe.Property, stage) ? "open" : "closed")}");
                foreach (MixingRecipe recipe in mixing.Where(recipe => recipe.Output.Equals(food)))
                    dependencies.Add($"Mixer {Gate(nameof(BasicMixer))}; {Input(recipe.IngredientA)}, {Input(recipe.IngredientB)}");
                foreach (CuttingRecipe recipe in cutting.Where(recipe => recipe.Output.Equals(food)))
                    dependencies.Add($"Cutter {Gate(nameof(Cutter))}; {Input(recipe.Input)}; output x{CutterProcess.OutputQuantity}");
                foreach (TradeRecipe recipe in trades.Where(recipe => recipe.Output.Equals(food)))
                    dependencies.Add($"TradeBuilding {Gate(nameof(TradeBuilding))}; {Input(recipe.Input)}; village {recipe.VillageId}");
                return dependencies.Count == 0 ? "No crop or recipe produces this food." : string.Join(" | ", dependencies) + ".";
            }
        }
    }
}
