using System;
using System.Collections.Generic;
using System.Linq;
using CozyFoodFactory.Buildings;
using CozyFoodFactory.Food;
using CozyFoodFactory.Logistics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CozyFoodFactory.UI
{
    public enum RecipeDisclosure { Unknown, Hint, Discovered }

    public sealed class RecipeBookCatalog
    {
        private readonly List<DiscoveredRecipe> recipes = new();
        private readonly HashSet<DiscoveredRecipe> discovered;
        private readonly HashSet<DiscoveredRecipe> revealed = new();
        private readonly FoodOrder currentOrder;

        public RecipeBookCatalog(IReadOnlyList<ProcessingRecipe> processors,
            IReadOnlyList<MixingRecipe> mixers,
            IReadOnlyList<CuttingRecipe> cutters,
            IReadOnlyList<DiscoveredRecipe> discoveredRecipes, FoodOrder currentOrder)
        {
            foreach (ProcessingRecipe recipe in processors ??
                Array.Empty<ProcessingRecipe>()) recipes.Add(new DiscoveredRecipe(recipe));
            foreach (MixingRecipe recipe in mixers ??
                Array.Empty<MixingRecipe>()) recipes.Add(new DiscoveredRecipe(recipe));
            foreach (CuttingRecipe recipe in cutters ??
                Array.Empty<CuttingRecipe>()) recipes.Add(new DiscoveredRecipe(recipe));
            discovered = new HashSet<DiscoveredRecipe>(discoveredRecipes ??
                Array.Empty<DiscoveredRecipe>());
            this.currentOrder = currentOrder;
            if (currentOrder != null)
            {
                foreach (FoodOrderRequirement requirement in currentOrder.Requirements)
                    RevealPrerequisites(requirement.Food);
            }
        }

        public IReadOnlyList<DiscoveredRecipe> Recipes => recipes;
        public int DiscoveredCount => discovered.Count;
        public RecipeDisclosure Disclosure(DiscoveredRecipe recipe) =>
            discovered.Contains(recipe) ? RecipeDisclosure.Discovered :
            revealed.Contains(recipe) ? RecipeDisclosure.Hint :
            RecipeDisclosure.Unknown;
        public bool IsCurrentOrder(DiscoveredRecipe recipe) =>
            currentOrder?.Requirements.Any(requirement =>
                requirement.Food.Equals(recipe.Output)) == true;
        public DiscoveredRecipe FindVisibleProducer(FoodItemData food) =>
            recipes.FirstOrDefault(recipe => recipe.Output.Equals(food) &&
                Disclosure(recipe) != RecipeDisclosure.Unknown);

        private void RevealPrerequisites(FoodItemData food)
        {
            foreach (DiscoveredRecipe recipe in recipes.Where(candidate =>
                candidate.Output.Equals(food)))
            {
                if (!revealed.Add(recipe)) continue;
                RevealPrerequisites(recipe.IngredientA);
                if (recipe.IngredientB != null)
                    RevealPrerequisites(recipe.IngredientB);
            }
        }
    }

    public sealed class RecipeDiscoveryPopupQueue
    {
        private readonly Queue<DiscoveredRecipe> pending = new();

        public DiscoveredRecipe Current { get; private set; }

        public void Show(DiscoveredRecipe recipe)
        {
            if (recipe == null)
            {
                throw new ArgumentNullException(nameof(recipe));
            }

            if (Current == null)
            {
                Current = recipe;
            }
            else
            {
                pending.Enqueue(recipe);
            }
        }

        public void Dismiss()
        {
            Current = pending.Count > 0 ? pending.Dequeue() : null;
        }
    }

    [RequireComponent(typeof(BuildingPlacementController))]
    public sealed class RecipeDiscoveryPanel : MonoBehaviour
    {
        private readonly RecipeDiscoveryPopupQueue popups = new();
        private BuildingPlacementController controller;
        private Vector2 bookScroll;
        private Vector2 popupScroll;
        private bool bookOpen;
        private DiscoveredRecipeKind? filter;
        private DiscoveredRecipe selectedRecipe;
        private FoodItemData selectedRawFood;
        private readonly Stack<(DiscoveredRecipe recipe, FoodItemData raw)> backStack = new();
        public bool HasModal => popups.Current != null;
        public void OpenAll()
        {
            if (controller == null || !controller.IsFoodDemo) return;
            filter = null;
            ClearDetail();
            controller.OpenPanel(BuildingPlacementController.DemoPanel.Recipe);
        }
        public void OpenForMachine(DiscoveredRecipeKind kind)
        {
            if (controller == null || !controller.IsFoodDemo) return;
            filter = kind;
            ClearDetail();
            controller.OpenPanel(BuildingPlacementController.DemoPanel.Recipe);
        }

        private void ClearDetail()
        {
            selectedRecipe = null;
            selectedRawFood = null;
            backStack.Clear();
            bookScroll = Vector2.zero;
        }
        public bool DismissModal()
        {
            if (popups.Current == null) return false;
            popups.Dismiss();
            return true;
        }

        public bool BlocksWorldInput
        {
            get
            {
                if (!isActiveAndEnabled || Mouse.current == null)
                {
                    return false;
                }

                if (popups.Current != null)
                {
                    return true;
                }

                Vector2 pointer = Mouse.current.position.ReadValue();
                pointer.y = Screen.height - pointer.y;
                return controller != null && controller.IsFoodDemo
                    ? controller.OpenDemoPanel == BuildingPlacementController.DemoPanel.Recipe &&
                      GetBookRect().Contains(pointer)
                    : GetBookButtonRect().Contains(pointer) ||
                      (bookOpen && GetBookRect().Contains(pointer));
            }
        }

        public static string FormatRequirements(DiscoveredRecipe recipe)
        {
            if (recipe == null)
            {
                throw new ArgumentNullException(nameof(recipe));
            }

            return recipe.Kind == DiscoveredRecipeKind.Cutting
                ? $"Ingredient: {recipe.IngredientA.Id}; yields " +
                    $"{CutterProcess.OutputQuantity} {recipe.Output.Id}"
                : recipe.Kind == DiscoveredRecipeKind.Processing
                ? $"Ingredient: {recipe.IngredientA.Id}; Property: {recipe.Property}"
                : $"Ingredients: {recipe.IngredientA.Id} + {recipe.IngredientB.Id}";
        }

        private void OnEnable()
        {
            controller = GetComponent<BuildingPlacementController>();
            controller.RecipeDiscovered += popups.Show;
        }

        private void OnDisable()
        {
            if (controller != null)
            {
                controller.RecipeDiscovered -= popups.Show;
            }
        }

        private void OnGUI()
        {
            if (controller == null)
            {
                return;
            }

            if (popups.Current != null)
            {
                DrawPopup(popups.Current);
                return;
            }

            if (controller.HasOrderCompletionCard) return;

            if (controller.IsFoodDemo)
            {
                if (controller.OpenDemoPanel == BuildingPlacementController.DemoPanel.Recipe)
                    DrawBook();
                return;
            }

            if (bookOpen)
            {
                DrawBook();
            }

            if (GUI.Button(GetBookButtonRect(),
                    $"Recipe Book ({controller.DiscoveredRecipes.Count})"))
            {
                bookOpen = !bookOpen;
            }
        }

        private void DrawPopup(DiscoveredRecipe recipe)
        {
            float width = Mathf.Min(400f, Screen.width - 16f);
            float height = Mathf.Min(320f, Screen.height - 16f);
            var rect = new Rect((Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f, width, height);
            GUI.Box(rect, GUIContent.none);
            GUILayout.BeginArea(new Rect(rect.x + 16f, rect.y + 12f,
                rect.width - 32f, rect.height - 24f));
            popupScroll = GUILayout.BeginScrollView(popupScroll);
            GUILayout.Label("NEW RECIPE!");
            DrawFoodIcon(recipe.Output);
            GUILayout.Label(recipe.Output.Id);
            DrawManufacturingSummary(recipe, false);
            GUILayout.Label("Added to Recipe Book");
            GUILayout.EndScrollView();
            if (GUILayout.Button("Continue")) popups.Dismiss();
            GUILayout.EndArea();
        }

        private void DrawBook()
        {
            Rect rect = GetBookRect();
            GUI.Box(rect, GUIContent.none);
            GUILayout.BeginArea(new Rect(rect.x + 12f, rect.y + 8f,
                rect.width - 24f, rect.height - 16f));
            RecipeBookCatalog catalog = CreateCatalog();
            GUILayout.Label($"Recipe Book  |  {catalog.DiscoveredCount} discovered");
            if (selectedRecipe != null || selectedRawFood != null)
            {
                DrawDetail(catalog);
                GUILayout.EndArea();
                return;
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("All")) filter = null;
            if (GUILayout.Button("Cutter")) filter = DiscoveredRecipeKind.Cutting;
            if (GUILayout.Button("Processor")) filter = DiscoveredRecipeKind.Processing;
            if (GUILayout.Button("Mixer")) filter = DiscoveredRecipeKind.Mixing;
            GUILayout.EndHorizontal();
            bookScroll = GUILayout.BeginScrollView(bookScroll);
            foreach (DiscoveredRecipe recipe in catalog.Recipes)
            {
                if (filter.HasValue && recipe.Kind != filter.Value) continue;
                RecipeDisclosure disclosure = catalog.Disclosure(recipe);
                if (disclosure == RecipeDisclosure.Unknown)
                {
                    GUILayout.Label("Unknown Recipe");
                    continue;
                }
                string flags = (catalog.IsCurrentOrder(recipe) ? "  ORDER" : "") +
                    (controller.RecipeDiscoveries.IsNew(recipe) ? "  NEW" : "");
                string name = recipe.Output.Id + flags;
                if (GUILayout.Button(name)) OpenRecipe(recipe);
                GUILayout.Label($"{MachineName(recipe.Kind)}: " +
                    (disclosure == RecipeDisclosure.Discovered
                        ? ShortSummary(recipe) : "Objective hint"));
            }
            if (!filter.HasValue && controller.Market?.ActiveOrder != null)
                foreach (FoodOrderRequirement requirement in
                    controller.Market.ActiveOrder.Order.Requirements)
                    if (requirement.Food.Kind == FoodItemKind.RawIngredient &&
                        catalog.FindVisibleProducer(requirement.Food) == null &&
                        GUILayout.Button($"{requirement.Food.Id}  ORDER"))
                        OpenRaw(requirement.Food);

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private RecipeBookCatalog CreateCatalog() => new(
            controller.ProcessorRecipes, controller.MixerRecipes,
            controller.CutterRecipes, controller.DiscoveredRecipes,
            controller.Market?.ActiveOrder?.Order);

        private void DrawDetail(RecipeBookCatalog catalog)
        {
            if (GUILayout.Button("Back"))
            {
                if (backStack.Count > 0)
                {
                    (selectedRecipe, selectedRawFood) = backStack.Pop();
                }
                else { selectedRecipe = null; selectedRawFood = null; }
                bookScroll = Vector2.zero;
                return;
            }
            bookScroll = GUILayout.BeginScrollView(bookScroll);
            if (selectedRawFood != null)
            {
                DrawFoodIcon(selectedRawFood);
                GUILayout.Label(selectedRawFood.Id);
                GUILayout.Label("Grow this crop in a Farm Plot, then collect it with a Harvester.");
                DrawCropAccess(selectedRawFood);
            }
            else if (selectedRecipe != null)
            {
                RecipeDisclosure disclosure = catalog.Disclosure(selectedRecipe);
                if (disclosure == RecipeDisclosure.Unknown)
                {
                    GUILayout.Label("Unknown Recipe");
                }
                else
                {
                    DrawFoodIcon(selectedRecipe.Output);
                    GUILayout.Label(selectedRecipe.Output.Id);
                    GUILayout.Label(disclosure == RecipeDisclosure.Discovered
                        ? "Discovered recipe" : "Current objective hint");
                    DrawManufacturingSummary(selectedRecipe,
                        disclosure == RecipeDisclosure.Hint);
                    DrawPrerequisite(catalog, selectedRecipe.IngredientA);
                    if (selectedRecipe.IngredientB != null)
                        DrawPrerequisite(catalog, selectedRecipe.IngredientB);
                    if (controller.CanBuildRecipeMachine(selectedRecipe.Kind,
                            out string requirement))
                    {
                        if (GUILayout.Button($"Build {MachineName(selectedRecipe.Kind)}"))
                        {
                            controller.SelectRecipeMachine(selectedRecipe.Kind);
                            selectedRecipe = null;
                            selectedRawFood = null;
                            backStack.Clear();
                        }
                    }
                    else GUILayout.Label($"Build locked: {requirement}");
                }
            }
            GUILayout.EndScrollView();
        }

        private void DrawPrerequisite(RecipeBookCatalog catalog, FoodItemData food)
        {
            DiscoveredRecipe source = catalog.FindVisibleProducer(food);
            if (source != null)
            {
                if (GUILayout.Button($"Prerequisite: {food.Id} >"))
                {
                    backStack.Push((selectedRecipe, selectedRawFood));
                    OpenRecipe(source);
                }
            }
            else if (food.Kind == FoodItemKind.RawIngredient &&
                GUILayout.Button($"Grow {food.Id} >"))
            {
                backStack.Push((selectedRecipe, selectedRawFood));
                OpenRaw(food);
            }
        }

        private void DrawManufacturingSummary(DiscoveredRecipe recipe, bool hint)
        {
            GUILayout.Label($"Machine: {MachineName(recipe.Kind)}");
            GUILayout.Label($"Food input: 1 {recipe.IngredientA.Id}");
            if (recipe.Kind == DiscoveredRecipeKind.Processing)
                GUILayout.Label($"Property input: {recipe.Property}");
            if (recipe.Kind == DiscoveredRecipeKind.Mixing)
                GUILayout.Label($"Second food input: 1 {recipe.IngredientB.Id}");
            if (!hint)
                GUILayout.Label($"Output: {OutputQuantity(recipe)} {recipe.Output.Id}");
            if (recipe.Kind == DiscoveredRecipeKind.Mixing)
                GUILayout.Label("Either ingredient can use either input.");
            if (recipe.Kind == DiscoveredRecipeKind.Cutting)
                GUILayout.Label("Both output belts must be available.");
        }

        private void DrawCropAccess(FoodItemData food)
        {
            Market market = controller.Market;
            if (market == null) return;
            if (market.Regions?.Regions != null)
                foreach (FarmableRegion region in market.Regions.Regions)
                    if (region.RestorationUnlocks.Any(key =>
                            key.Category == UnlockKey.CropCategory && key.Id == food.Id) &&
                        market.Regions.GetStatus(region.Id) != RegionStatus.Restored)
                        GUILayout.Label($"Restore {region.DisplayName} to grow {food.Id}.");
            foreach (FoodOrder order in market.Orders)
                if (order.Unlocks.Any(key =>
                        key.Category == UnlockKey.CropCategory && key.Id == food.Id) &&
                    !market.Unlocks.IsUnlocked(UnlockKey.CropCategory, food.Id))
                    GUILayout.Label($"Complete {order.DisplayName} to unlock {food.Id}.");
        }

        private void OpenRecipe(DiscoveredRecipe recipe)
        {
            selectedRecipe = recipe;
            selectedRawFood = null;
            bookScroll = Vector2.zero;
            controller.RecipeDiscoveries.MarkViewed(recipe);
        }

        private void OpenRaw(FoodItemData food)
        {
            selectedRawFood = food;
            selectedRecipe = null;
            bookScroll = Vector2.zero;
        }

        private static int OutputQuantity(DiscoveredRecipe recipe) =>
            recipe.Kind == DiscoveredRecipeKind.Cutting
                ? CutterProcess.OutputQuantity : 1;

        private static string ShortSummary(DiscoveredRecipe recipe) =>
            recipe.Kind == DiscoveredRecipeKind.Mixing
                ? $"{recipe.IngredientA.Id} + {recipe.IngredientB.Id}"
                : recipe.Kind == DiscoveredRecipeKind.Processing
                ? $"{recipe.IngredientA.Id} + {recipe.Property}"
                : $"{recipe.IngredientA.Id} to {OutputQuantity(recipe)} outputs";

        private static string MachineName(DiscoveredRecipeKind kind) => kind switch
        {
            DiscoveredRecipeKind.Processing => "Processor",
            DiscoveredRecipeKind.Mixing => "Basic Mixer",
            _ => "Cutter"
        };

        private static void DrawFoodIcon(FoodItemData food)
        {
            Rect rect = GUILayoutUtility.GetRect(42f, 42f, GUILayout.Width(42f));
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = Belt.FoodColor(food);
            GUI.Box(rect, food.Kind == FoodItemKind.ProcessedFood
                ? $"◆ {Belt.FoodLabel(food)}" : Belt.FoodLabel(food));
            GUI.backgroundColor = previous;
        }

        private static Rect GetBookButtonRect() =>
            new(Mathf.Max(8f, Screen.width - 188f),
                Mathf.Max(8f, Screen.height - 48f), 172f, 32f);

        private Rect GetBookRect()
        {
            if (controller == null || !controller.IsFoodDemo)
                return new Rect(Mathf.Max(8f, Screen.width - 340f),
                    Mathf.Max(8f, Screen.height - 408f), 324f,
                    Mathf.Min(344f, Mathf.Max(100f, Screen.height - 72f)));
            float height = Mathf.Min(344f, Mathf.Max(80f, Screen.height - 130f));
            return new Rect(Mathf.Max(8f, Screen.width - 340f),
                Mathf.Max(8f, Screen.height - height - 114f),
                Mathf.Min(324f, Screen.width - 16f), height);
        }
    }
}
