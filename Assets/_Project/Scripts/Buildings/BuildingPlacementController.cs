using System;
using System.Collections.Generic;
using System.Linq;
using CozyFoodFactory.Food;
using CozyFoodFactory.CameraControl;
using CozyFoodFactory.Grid;
using CozyFoodFactory.Logistics;
using CozyFoodFactory.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace CozyFoodFactory.Buildings
{
    public enum DemoEscapeAction
    {
        DismissModal, CloseSystem, ClosePanel, CancelTool, ClearSelection, OpenSystem
    }

    public static class DemoEscapePriority
    {
        public static DemoEscapeAction Choose(bool modal, bool system,
            bool panel, bool tool, bool selection)
        {
            if (modal) return DemoEscapeAction.DismissModal;
            if (system) return DemoEscapeAction.CloseSystem;
            if (panel) return DemoEscapeAction.ClosePanel;
            if (tool) return DemoEscapeAction.CancelTool;
            if (selection) return DemoEscapeAction.ClearSelection;
            return DemoEscapeAction.OpenSystem;
        }
    }

    public sealed class BuildingToolRotationMemory
    {
        private readonly Dictionary<string, BuildingRotation> rotations = new(
            StringComparer.Ordinal);

        public BuildingRotation Get(string buildingId) => buildingId != null &&
            rotations.TryGetValue(buildingId, out BuildingRotation rotation)
                ? rotation : BuildingRotation.Degrees0;

        public void Set(string buildingId, BuildingRotation rotation)
        {
            if (string.IsNullOrWhiteSpace(buildingId))
                throw new ArgumentException("A building ID is required.", nameof(buildingId));
            rotations[buildingId] = rotation;
        }
    }

    public sealed class BuildingPlacementController : MonoBehaviour
    {
        public enum DemoPanel { None, Build, Recipe, Help, Market, Region, Machine, Property, Issues }
        [SerializeField] private GridSystem gridSystem = null;
        [SerializeField] private GridHoverHighlight hoverHighlight = null;
        [SerializeField] private BuildingPreview placementPreview = null;
        [SerializeField] private ObjectivePanel engraverUpgradePanel = null;
        [SerializeField] private Market market = null;
        [SerializeField] private bool foodDemoControls;
        [SerializeField, Tooltip("Second HUD resource line; gameplay source is not finalized.")]
        private string secondaryResourceText = "0";
        [SerializeField] private BuildingPlacementOption[] buildingOptions =
            Array.Empty<BuildingPlacementOption>();
        [SerializeField] private PropertySourceSetup[] propertySources =
            Array.Empty<PropertySourceSetup>();
        [SerializeField] private PropertyVisualDefinition propertyVisuals = new();
        [SerializeField] private BeltTransportCoordinator processorTransportCoordinator = null;
        [SerializeField, Min(0.01f)] private float processorDuration = 1f;
        [SerializeField] private ProcessingRecipe[] processorRecipes =
            Array.Empty<ProcessingRecipe>();
        [SerializeField] private MixingRecipe[] mixerRecipes =
            Array.Empty<MixingRecipe>();
        [SerializeField] private TradeRecipe[] tradeRecipes = Array.Empty<TradeRecipe>();
        [SerializeField] private string tradeVillageId = "demo-village";
        private TradeRecipe[] VillageTrades => tradeRecipes.Where(recipe =>
            recipe != null && recipe.VillageId == tradeVillageId).ToArray();
        [SerializeField] private CuttingRecipe[] cutterRecipes =
            Array.Empty<CuttingRecipe>();
        [SerializeField, Min(0.01f)] private float cutterDuration = 1f;
        [SerializeField, Min(0f)] private float propertyIssueDelay = 1.5f;
        [SerializeField, Min(0f)] private float invalidIssueDelay = 0.75f;
        [SerializeField, Min(0f)] private float blockedIssueDelay = 0.75f;

        private readonly GridOccupancy occupancy = new();
        private readonly RecipeDiscoveryRegistry recipeDiscoveries = new();
        private readonly Dictionary<BuildingPlacement, PlacedBuilding> buildingInstances = new();
        private readonly Dictionary<BuildingPlacement, MachineFeedbackView> feedbackViews = new();
        private readonly FactoryIssueTracker<BuildingPlacement> issueTracker = new();
        private readonly EventToastQueue toasts = new();
        private readonly Queue<OrderCompletionPresentation> completionCards = new();
        private OrderCompletionPresentation activeCompletionCard;
        private string contextualGuidance;
        private float guidanceUntil;
        private bool loadConfirmationOpen;
        private bool quitConfirmationOpen;
        private readonly BeltDragPlacementPlanner beltDragPlanner = new();
        private bool beltRotationExplicit;
        private readonly GridDragTracker placementDrag = new();
        private readonly GridDragTracker removalDrag = new();
        private BuildingPlacement outlinedRemoval;
        private GameObject removalOutline;
        private Material removalOutlineMaterial;
        private readonly BuildingSelection selection = new();
        private readonly Dictionary<BuildingPlacement, GameObject> selectionHighlights = new();
        private Vector2Int? selectionStartCell;
        private readonly List<BuildingPlacement> selectionBeforeDrag = new();
        private GameObject selectionArea;
        private BuildingRotation selectedRotation;
        private readonly BuildingToolRotationMemory rememberedRotations = new();
        private int constructionCategory;
        private bool suppressRightRemovalUntilRelease;
        private bool systemMenuOpen;
        private bool devInspectorOpen;
        private DemoPanel demoPanel;
        private FarmPlot cropPickerPlot;
        private TradeBuilding tradePickerBuilding;
        private string rememberedFarmCropId;
        private Vector2 buildMenuScroll;
        private BlueprintLibrary blueprintLibrary;
        private readonly ConstructionHistory constructionHistory = new();
        private ConstructionLayout placementHistoryStart;
        private ConstructionLayout removalHistoryStart;
        private ConstructionLayout cropHistoryStart;
        private ConstructionLayout propertyHistoryStart;
        private bool suppressConstructionHistory;
        private bool blueprintLibraryOpen;
        private bool blueprintSaveFormOpen;
        private bool blueprintNameFocused;
        private BuildingGroupCopy pendingBlueprintGroup;
        private const string BlueprintNameControl = "BlueprintName";
        private string blueprintName = "Production Line";
        private string selectedBlueprintId;
        private string activeBlueprintName;
        private Vector2 helpScroll;
        private static readonly string[] HotbarBuildingIds =
        {
            nameof(FarmPlot), nameof(Belt), nameof(Harvester),
            nameof(Processor), nameof(BasicMixer), nameof(Cutter), nameof(TradeBuilding)
        };
        private int selectedBuildingIndex;
        private bool isPlacementModeActive;
        private string constructionMessage;
        private BuildingGroupCopy copiedGroup;
        private BuildingGroupCopy activeGroup;
        private readonly List<BuildingPlacement> moveSources = new();
        private readonly List<Vector2Int> movePropertySources = new();
        private readonly List<BuildingPreview> groupPreviews = new();
        private readonly List<GameObject> propertyGroupPreviews = new();
        private bool isGroupPasteModeActive;
        private bool pasteAwaitingMouseRelease;
        private PropertySupplyPlayMode propertySupply;
        private RecipeDiscoveryPanel recipeDiscoveryPanel;
        private DiscoveredRecipeKind? recipeShortcutKind;
        private Rect recipeShortcutRect;
        private BuildingPlacement recipeShortcutPlacement;
        private float recipeShortcutKeepUntil;
        private MarketPanel marketPanel;
        private FactoryCameraController factoryCamera;

        public PropertySupplyPlayMode PropertySupply => propertySupply;
        public Market Market => market;
        public GridSystem GridSystem => gridSystem;
        public int CountFarmPlots(FarmableRegion region) => buildingInstances.Keys.Count(
            placement => placement.DefinitionId == nameof(FarmPlot) &&
                region.Contains(placement.AnchorCell));
        public int CountFreeFarmCells(FarmableRegion region)
        {
            int free = 0;
            for (int x = region.MinimumCell.x; x < region.MinimumCell.x + region.Size.x; x++)
                for (int y = region.MinimumCell.y; y < region.MinimumCell.y + region.Size.y; y++)
                    if (occupancy.CanPlace(new Vector2Int(x, y), Vector2Int.one,
                            BuildingRotation.Degrees0)) free++;
            return free;
        }
        public bool IsFoodDemo => foodDemoControls;
        public DemoPanel OpenDemoPanel => demoPanel;
        public bool IsSystemMenuOpen => systemMenuOpen;
        public bool HasOrderCompletionCard => activeCompletionCard != null;
        public bool IsDevInspectorOpen => devInspectorOpen;
        public bool BlocksAllWorldInput => foodDemoControls &&
            (systemMenuOpen || activeCompletionCard != null ||
             recipeDiscoveryPanel?.HasModal == true);
        public bool BlocksGameplayKeyboardInput => ShouldBlockGameplayKeyboardInput(
            foodDemoControls, blueprintLibraryOpen, demoPanel, blueprintNameFocused);
        public static bool ShouldBlockGameplayKeyboardInput(bool foodDemo,
            bool libraryOpen, DemoPanel panel, bool textFocused) =>
            foodDemo && libraryOpen && panel == DemoPanel.Build && textFocused;
        public void OpenPanel(DemoPanel panel)
        {
            if (!foodDemoControls) return;
            cropPickerPlot = null; tradePickerBuilding = null;
            if (panel == DemoPanel.None) { ClosePanel(); return; }
            if (demoPanel == panel && panel == DemoPanel.Region) return;
            if (demoPanel == panel && panel != DemoPanel.Machine)
            { ClosePanel(); return; }
            ClosePanel();
            demoPanel = panel;
            if (panel == DemoPanel.Market) marketPanel?.OpenCurrentOrder();
            if (panel == DemoPanel.Property && propertySupply?.IsVisible == false)
                propertySupply.TogglePanel();
        }
        public void ClosePanel()
        {
            tradePickerBuilding = null;
            if (demoPanel == DemoPanel.Property && propertySupply?.IsVisible == true)
                propertySupply.TogglePanel();
            if (demoPanel == DemoPanel.Machine) engraverUpgradePanel?.CloseConfiguration();
            if (demoPanel == DemoPanel.Region) marketPanel?.CloseRegion();
            demoPanel = DemoPanel.None;
            blueprintNameFocused = false;
        }
        public bool IsPointerOverInterface => Mouse.current != null &&
            (BlocksAllWorldInput ||
             foodDemoControls && IsPointerOverDemoHud() ||
             recipeDiscoveryPanel != null && recipeDiscoveryPanel.BlocksWorldInput ||
             marketPanel != null && marketPanel.IsPointerOverPanel ||
             foodDemoControls && marketPanel?.IsPointerOverWorldObjective == true ||
             marketPanel != null && marketPanel.IsPointerOverRegionPanel ||
             engraverUpgradePanel != null && engraverUpgradePanel.IsPointerOverPanel ||
             propertySupply != null && propertySupply.IsPointerOverPanel());
        public IReadOnlyList<DiscoveredRecipe> DiscoveredRecipes =>
            recipeDiscoveries.DiscoveredRecipes;
        public RecipeDiscoveryRegistry RecipeDiscoveries => recipeDiscoveries;
        public IReadOnlyList<ProcessingRecipe> ProcessorRecipes => processorRecipes;
        public IReadOnlyList<MixingRecipe> MixerRecipes => mixerRecipes;
        public IReadOnlyList<CuttingRecipe> CutterRecipes => cutterRecipes;

        public FactoryWorldData CaptureWorldSnapshot()
        {
            if (propertySupply == null)
            {
                throw new InvalidOperationException("Factory world is not initialized.");
            }

            var saved = new List<SavedBuilding>(buildingInstances.Count);
            foreach (KeyValuePair<BuildingPlacement, PlacedBuilding> entry in buildingInstances)
            {
                BuildingPlacement placement = entry.Key;
                PlacedBuilding instance = entry.Value;
                var building = new SavedBuilding
                {
                    definitionId = placement.DefinitionId,
                    x = placement.AnchorCell.x,
                    y = placement.AnchorCell.y,
                    rotation = placement.Rotation
                };
                switch (placement.DefinitionId)
                {
                    case nameof(FarmPlot):
                        building.farmPlot = instance.GetComponent<FarmPlot>()?.CaptureWorldState();
                        break;
                    case nameof(Harvester):
                        building.harvester = instance.GetComponent<Harvester>()?.CaptureWorldState();
                        break;
                    case nameof(Belt):
                        building.belt = instance.GetComponent<Belt>()?.CaptureWorldState();
                        break;
                    case nameof(Processor):
                        building.processor = instance.GetComponent<Processor>()?.CaptureWorldState();
                        break;
                    case nameof(BasicMixer):
                        building.mixer = instance.GetComponent<BasicMixer>()?.CaptureWorldState();
                        break;
                    case nameof(TradeBuilding):
                        building.tradeBuilding = instance.GetComponent<TradeBuilding>()?.CaptureWorldState();
                        break;
                    case nameof(Cutter):
                        building.cutter = instance.GetComponent<Cutter>()?.CaptureWorldState();
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Factory snapshot cannot save legacy building {placement.DefinitionId} " +
                            "or its active RuneData state.");
                }

                saved.Add(building);
            }

            return new FactoryWorldData
            {
                buildings = saved.OrderBy(item => item.definitionId, StringComparer.Ordinal)
                    .ThenBy(item => item.x).ThenBy(item => item.y).ToArray(),
                connections = propertySupply.CaptureWorldConnections()
            };
        }

        public void ValidateWorldSnapshot(FactoryWorldData world,
            IReadOnlyList<SavedUnlock> savedUnlocks)
        {
            if (foodDemoControls && world?.connections != null &&
                world.connections.Any(connection =>
                    connection?.kind == PropertyConnectionKind.Demand))
            {
                throw new ArgumentException(
                    "Demo cannot load developer test demands.");
            }

            FactoryWorldSnapshotValidator.ValidateAgainstScene(world, buildingOptions,
                propertySources, market.Regions, savedUnlocks, processorRecipes,
                mixerRecipes, market.InputCell, cutterRecipes, VillageTrades);
        }

        public void RestoreWorldSnapshot(FactoryWorldData world,
            IReadOnlyList<SavedUnlock> savedUnlocks)
        {
            ValidateWorldSnapshot(world, savedUnlocks);
            if (buildingInstances.Count != 0)
            {
                throw new InvalidOperationException("Factory reconstruction requires a fresh scene.");
            }

            // Overlay placement requires the underlying Farm Plot to exist first.
            IEnumerable<SavedBuilding> ordered =
                FactoryWorldSnapshotValidator.ReconstructionOrder(world);
            foreach (SavedBuilding saved in ordered)
            {
                BuildingPlacementOption option = buildingOptions.FirstOrDefault(candidate =>
                    candidate?.Definition?.Id == saved.definitionId);
                Vector2Int anchor = new(saved.x, saved.y);
                if (option == null || !CanPlaceBuilding(option, anchor, saved.rotation) ||
                    !PlaceBuilding(option, anchor, saved.rotation,
                        out BuildingPlacement placement))
                {
                    throw new InvalidOperationException(
                        $"Cannot restore {saved.definitionId} at {anchor}.");
                }

                PlacedBuilding instance = buildingInstances[placement];
                if (saved.farmPlot != null)
                    instance.GetComponent<FarmPlot>().RestoreWorldState(saved.farmPlot);
                else if (saved.harvester != null)
                    instance.GetComponent<Harvester>().RestoreWorldState(saved.harvester);
                else if (saved.processor != null)
                    instance.GetComponent<Processor>().RestoreWorldState(saved.processor);
                else if (saved.mixer != null)
                    instance.GetComponent<BasicMixer>().RestoreWorldState(saved.mixer);
                else if (saved.tradeBuilding != null)
                    instance.GetComponent<TradeBuilding>().RestoreWorldState(saved.tradeBuilding);
                else if (saved.cutter != null)
                    instance.GetComponent<Cutter>().RestoreWorldState(saved.cutter);
                else if (saved.belt != null)
                    instance.GetComponent<Belt>().RestoreWorldState(saved.belt);
            }

            propertySupply.RestoreWorldConnections(world.connections);
        }
        public event Action<DiscoveredRecipe> RecipeDiscovered
        {
            add => recipeDiscoveries.Discovered += value;
            remove => recipeDiscoveries.Discovered -= value;
        }

        private bool IsChapterOnePropertyAvailable(CookingProperty property)
        {
            int completed = market?.CompletedOrders.Count ?? 0;
            return property switch
            {
                CookingProperty.Heat => completed >= 1,
                CookingProperty.Water => completed >= 5,
                _ => false
            };
        }

        private void Awake()
        {
            recipeDiscoveryPanel = GetComponent<RecipeDiscoveryPanel>();
            marketPanel = market?.GetComponent<MarketPanel>();
            if (foodDemoControls)
                blueprintLibrary = new BlueprintLibrary(System.IO.Path.Combine(
                    Application.persistentDataPath, "cozy-food-factory-chapter-1-blueprints.json"));
            if (market != null && !occupancy.TryRegister(
                    nameof(Market),
                    market.InputCell,
                    market.Footprint,
                    BuildingRotation.Degrees0,
                    out _))
            {
                throw new InvalidOperationException(
                    $"The Market footprint at {market.InputCell} could not be reserved.");
            }

            propertySupply = new PropertySupplyPlayMode(gridSystem, hoverHighlight,
                occupancy, transform, propertySources, !foodDemoControls,
                propertyVisuals, foodDemoControls
                    ? new Func<CookingProperty, bool>(IsChapterOnePropertyAvailable)
                    : null);
            if (foodDemoControls)
            {
                propertySupply.ConstructionStarting += OnPropertyConstructionStarting;
                propertySupply.ConstructionFinished += OnPropertyConstructionFinished;
            }
            foreach (BuildingPlacementOption option in buildingOptions)
            {
                if (option?.Definition?.Id == nameof(Processor))
                {
                    var processorBehavior = gameObject.AddComponent<ProcessorPlacementBehavior>();
                    processorBehavior.Configure(processorTransportCoordinator, processorRecipes,
                        processorDuration, recipeDiscoveries);
                    option.SetRuntimePlacementBehavior(processorBehavior);
                }
                else if (option?.Definition?.Id == nameof(BasicMixer))
                {
                    var mixerBehavior = gameObject.AddComponent<BasicMixerPlacementBehavior>();
                    mixerBehavior.Configure(processorTransportCoordinator, mixerRecipes,
                        recipeDiscoveries);
                    option.SetRuntimePlacementBehavior(mixerBehavior);
                }
                else if (option?.Definition?.Id == nameof(TradeBuilding))
                {
                    var tradeBehavior = gameObject.AddComponent<TradeBuildingPlacementBehavior>();
                    tradeBehavior.Configure(processorTransportCoordinator, VillageTrades);
                    option.SetRuntimePlacementBehavior(tradeBehavior);
                }
                else if (option?.Definition?.Id == nameof(Cutter))
                {
                    var cutterBehavior = gameObject.AddComponent<CutterPlacementBehavior>();
                    cutterBehavior.Configure(processorTransportCoordinator, cutterRecipes,
                        cutterDuration, recipeDiscoveries);
                    option.SetRuntimePlacementBehavior(cutterBehavior);
                }
            }
        }

        private void OnGUI()
        {
            bool originalEnabled = GUI.enabled;
            if (BlocksAllWorldInput) GUI.enabled = false;
            propertySupply?.DrawGUI();
            GUI.enabled = originalEnabled;
            if (foodDemoControls)
            {
                DrawDemoHud();
                DrawToasts();
            }
            if (propertySupply?.IsActive == true &&
                (isPlacementModeActive || isGroupPasteModeActive))
            {
                isPlacementModeActive = false;
                placementPreview.Hide();
                ExitGroupPasteMode();
            }
        }

        private void Start()
        {
            placementPreview.Hide();
            if (!foodDemoControls || market == null) return;
            market.OrderSequence.CompletedWithRewards += OnOrderCompleted;
            market.Unlocks.UnlockedContent += OnContentUnlocked;
            market.FoodDelivered += OnFoodDelivered;
            if (marketPanel != null) marketPanel.GameSaved += OnGameSaved;
            string loadFeedback = FactoryWorldLoadSession.ConsumeFeedback();
            if (!string.IsNullOrEmpty(loadFeedback))
                toasts.Enqueue(loadFeedback, Time.time);
        }

        private void OnOrderCompleted(FoodOrder order,
            IReadOnlyList<UnlockKey> granted)
        {
            completionCards.Enqueue(new OrderCompletionPresentation(order, granted));
            if (activeCompletionCard == null) activeCompletionCard = completionCards.Dequeue();
            marketPanel?.ShowOrderCompletion();
        }

        private void OnContentUnlocked(UnlockKey unlock)
        {
            string name = unlock.Id switch
            {
                nameof(BasicMixer) => "Basic Mixer",
                _ => unlock.Id.Replace('_', ' ')
            };
            string message = unlock.Category switch
            {
                UnlockKey.MachineCategory => $"New machine: {name}",
                UnlockKey.CropCategory => $"New crop: {name}",
                UnlockKey.SeedShopCategory => $"New seed shop item: {name}",
                UnlockKey.RegionAccessCategory => $"{name} is now available",
                UnlockKey.RegionCategory => $"{name} restored",
                _ => null
            };
            if (message != null && market.OrderSequence?.IsCompleting != true)
                toasts.Enqueue(message, Time.time);
        }

        private void OnFoodDelivered(FoodItemData food, int count) =>
            toasts.AddCurrency(food.SellValue, Time.time);

        private void OnGameSaved() => toasts.Enqueue("Game saved", Time.time);

        private void DrawToasts()
        {
            toasts.Prune(Time.time);
            float top = devInspectorOpen ? 132f : 8f;
            for (int index = 0; index < toasts.Active.Count; index++)
                GUI.Box(new Rect(Mathf.Max(8f, Screen.width - 225f),
                    top + index * 42f, 217f, 36f), toasts.Active[index].Text);
        }

        private void Update()
        {
            if (Keyboard.current == null || Mouse.current == null)
            {
                ClearRemovalOutline();
                return;
            }

            // IMGUI text fields receive the key event later in the frame. Keep
            // world shortcuts from acting on it first, including Enter and Esc.
            if (BlocksGameplayKeyboardInput)
            {
                ClearRightClickRemoval();
                return;
            }


            if (foodDemoControls)
            {
                RefreshRecipeShortcut();
                if (Keyboard.current.f3Key.wasPressedThisFrame)
                    devInspectorOpen = !devInspectorOpen;
                if (Keyboard.current.escapeKey.wasPressedThisFrame && HandleDemoEscape())
                    return;
                if (Mouse.current.rightButton.wasPressedThisFrame)
                {
                    if (isGroupPasteModeActive ||
                        isPlacementModeActive || propertySupply?.IsActive == true)
                    {
                        if (isGroupPasteModeActive) ExitGroupPasteMode();
                        else CancelDemoTool();
                        suppressRightRemovalUntilRelease = true;
                        ClearRightClickRemoval();
                        return;
                    }
                }
                if (BlocksAllWorldInput)
                {
                    recipeShortcutKind = null;
                    ClearRightClickRemoval();
                    return;
                }
                if (HandleConstructionHistoryShortcut()) return;
                if (recipeShortcutKind.HasValue &&
                    Mouse.current.leftButton.wasPressedThisFrame &&
                    recipeShortcutRect.Contains(GetGuiPointer()))
                {
                    recipeDiscoveryPanel?.OpenForMachine(recipeShortcutKind.Value);
                    recipeShortcutKind = null;
                    return;
                }
                HandleDemoHotbarShortcuts();
                if (tradePickerBuilding != null && Mouse.current.leftButton.wasPressedThisFrame &&
                    !TradePickerRect.Contains(GetGuiPointer()))
                { tradePickerBuilding = null; return; }
                if (cropPickerPlot != null &&
                    Mouse.current.leftButton.wasPressedThisFrame &&
                    !CropPickerRect.Contains(GetGuiPointer()))
                {
                    cropPickerPlot = null; tradePickerBuilding = null;
                    return;
                }
                if (demoPanel != DemoPanel.None &&
                    (demoPanel != DemoPanel.Property ||
                     propertySupply?.IsActive != true) &&
                    Mouse.current.leftButton.wasPressedThisFrame &&
                    !IsPointerOverDemoHud() &&
                    !(demoPanel == DemoPanel.Region && marketPanel?.IsPointerOverRegionPanel == true))
                {
                    ClosePanel();
                    return;
                }
                if (Mouse.current.leftButton.wasPressedThisFrame &&
                    !isPlacementModeActive &&
                    propertySupply?.IsActive != true &&
                    propertySupply?.IsPointerOverPanel() != true &&
                    marketPanel?.TryOpenAt(hoverHighlight.HoveredCell) == true)
                {
                    OpenPanel(DemoPanel.Market);
                    return;
                }
            }

            if (isPlacementModeActive && Mouse.current.rightButton.wasPressedThisFrame &&
                IsPointerOverInterface)
            {
                isPlacementModeActive = false;
                placementPreview.Hide();
                placementDrag.Reset();
                beltDragPlanner.Reset();
                return;
            }

            if (IsPointerOverInterface)
            {
                RecordConstruction(placementHistoryStart);
                placementHistoryStart = null;
                RecordConstruction(removalHistoryStart);
                removalHistoryStart = null;
                if (Mouse.current.rightButton.wasPressedThisFrame)
                    suppressRightRemovalUntilRelease = true;
                ClearRightClickRemoval();
                if (propertySupply?.IsPipeDragging == true &&
                    !Mouse.current.leftButton.isPressed)
                    propertySupply.CancelPipeDrag();
                if (isPlacementModeActive && Keyboard.current.escapeKey.wasPressedThisFrame)
                    isPlacementModeActive = false;
                else if (isPlacementModeActive && Keyboard.current.rKey.wasPressedThisFrame)
                {
                    selectedRotation = selectedRotation.RotateClockwise();
                    RememberRotation(GetSelectedOption(), selectedRotation);
                    beltRotationExplicit = true;
                }
                placementPreview.Hide();
                placementDrag.Reset();
                removalDrag.Reset();
                return;
            }

            if (propertySupply != null &&
                (propertySupply.IsActive || propertySupply.IsPointerOverPanel()))
            {
                ClearRightClickRemoval();
                if (propertySupply.IsActive)
                {
                    propertySupply.HandleInput();
                }

                return;
            }

            bool wasGroupPasteModeActive = isGroupPasteModeActive;
            HandleModeInput();
            if (isGroupPasteModeActive)
            {
                HandleGroupPasteInput();
                return;
            }

            if (wasGroupPasteModeActive)
            {
                return;
            }

            if (Mouse.current.leftButton.wasPressedThisFrame && marketPanel != null &&
                (!isPlacementModeActive ||
                    GetSelectedOption()?.Definition?.Id == nameof(FarmPlot)) &&
                marketPanel.TrySelectRegionAt(hoverHighlight.HoveredCell,
                    isPlacementModeActive, occupancy.TryGetBuilding(
                        hoverHighlight.HoveredCell, out _)))
            {
                if (foodDemoControls) OpenPanel(DemoPanel.Region);
                return;
            }

            if (HandleSelectionInput())
            {
                return;
            }

            HandleRemovalInput();
            HandleInteractionInput();

            if (!isPlacementModeActive)
            {
                beltDragPlanner.Reset();
                placementDrag.Reset();
                return;
            }

            Vector2Int anchorCell = hoverHighlight.HoveredCell;
            BuildingPlacementOption selectedOption = GetSelectedOption();
            if (selectedOption == null)
            {
                placementPreview.Hide();
                return;
            }

            BuildingRotation previewRotation = GetPreviewRotation(
                selectedOption,
                anchorCell);
            if (selectedOption.PlacementBehavior is HarvesterPlacementBehavior)
            {
                anchorCell = HarvesterPlacementBehavior.GetAnchorForFarmCell(
                    anchorCell, selectedOption.Definition.Footprint, previewRotation);
            }

            bool canPlace = CanPlaceBuilding(
                selectedOption,
                anchorCell,
                previewRotation) ||
                CanExtendBelt(selectedOption, anchorCell);
            placementPreview.Show(
                selectedOption,
                gridSystem,
                anchorCell,
                previewRotation,
                canPlace);
            placementPreview.SetPortConnectionFeedback(
                GetPortConnectionFeedback(selectedOption, previewRotation));
            placementPreview.SetDirectionConnectionFeedback(
                GetBeltConnectionFeedback(selectedOption, anchorCell,
                    previewRotation));
            if (selectedOption.Definition.Id == nameof(Belt))
            {
                int outgoing = BeltCell.Bit(previewRotation.ToGridDirection());
                if (occupancy.TryGetBuilding(anchorCell, out BuildingPlacement existing) &&
                    existing.DefinitionId == nameof(Belt) &&
                    buildingInstances.TryGetValue(existing, out PlacedBuilding instance) &&
                    instance.TryGetComponent(out Belt existingBelt))
                    outgoing |= existingBelt.OutputMask;
                int incoming = 0;
                for (int index = 0; index < 4; index++)
                {
                    GridDirection direction = (GridDirection)index;
                    Vector2Int neighborCell = anchorCell - direction.ToOffset();
                    if (occupancy.TryGetBuilding(neighborCell,
                            out BuildingPlacement neighbor) &&
                        buildingInstances.TryGetValue(neighbor,
                            out PlacedBuilding neighborInstance) &&
                        neighborInstance.TryGetComponent(out Belt neighborBelt) &&
                        (neighborBelt.OutputMask & BeltCell.Bit(direction)) != 0 &&
                        (outgoing & BeltCell.Bit(
                            (GridDirection)((index + 2) & 3))) == 0)
                        incoming |= BeltCell.Bit((GridDirection)((index + 2) & 3));
                }
                placementPreview.SetBeltConnections(outgoing, incoming,
                    gridSystem.CellSize);
            }
            else placementPreview.HideBeltConnections();
            placementPreview.SetReason(canPlace ? null :
                GetPlacementFailureReason(selectedOption, anchorCell, previewRotation));

            HandlePlacementInput(selectedOption, anchorCell, canPlace);
        }

        private void LateUpdate()
        {
            issueTracker.NeedsPropertyDelay = propertyIssueDelay;
            issueTracker.InvalidRecipeDelay = invalidIssueDelay;
            issueTracker.OutputBlockedDelay = blockedIssueDelay;
            foreach (KeyValuePair<BuildingPlacement, MachineFeedbackView> entry in feedbackViews)
            {
                if (!buildingInstances.TryGetValue(entry.Key, out PlacedBuilding building))
                    continue;
                MachineFeedback feedback;
                if (building.TryGetComponent(out Processor processor))
                {
                    feedback = MachineFeedbackResolver.Processor(
                        processor.HasOutput &&
                        processorTransportCoordinator?.CanAcceptOutput(processor.OutputCell) != true,
                        processor.HasRecentInvalidRecipe,
                        processor.ProcessingStateMessage.Contains("property supply") ||
                        processor.ProcessingStateMessage.Contains("supplied property changed"),
                        processor.State == ProcessorState.Idle);
                }
                else if (building.TryGetComponent(out BasicMixer mixer))
                {
                    feedback = MachineFeedbackResolver.Mixer(
                        mixer.HasOutput &&
                        processorTransportCoordinator?.CanAcceptOutput(mixer.OutputCell) != true,
                        mixer.HasRecentInvalidRecipe,
                        !mixer.HasOutput && mixer.SlotA == null,
                        !mixer.HasOutput && mixer.SlotB == null);
                }
                else if (building.TryGetComponent(out Cutter cutter))
                {
                    bool outputNeeded = cutter.State != CutterState.Idle;
                    feedback = MachineFeedbackResolver.Cutter(
                        outputNeeded && processorTransportCoordinator?.CanAcceptOutput(
                            cutter.OutputACell) != true,
                        outputNeeded && processorTransportCoordinator?.CanAcceptOutput(
                            cutter.OutputBCell) != true,
                        cutter.HasRecentInvalidRecipe, cutter.State == CutterState.Idle);
                }
                else if (building.TryGetComponent(out TradeBuilding trade))
                {
                    feedback = trade.OutputBlocked
                        ? new MachineFeedback(MachineFeedbackState.OutputBlocked,
                            MachineFeedbackPort.OutputA | MachineFeedbackPort.OutputB | MachineFeedbackPort.OutputC,
                            "Trade output blocked", "Connect or clear any output Belt.")
                        : trade.HasRecentInvalidRecipe
                        ? new MachineFeedback(MachineFeedbackState.InvalidRecipe,
                            MachineFeedbackPort.InputA | MachineFeedbackPort.InputB | MachineFeedbackPort.InputC,
                            "Wrong trade input", "Feed the food shown in the selected trade.")
                        : trade.Process.SelectedTrade == null
                        ? new MachineFeedback(MachineFeedbackState.NeedsInput, MachineFeedbackPort.InputA,
                            "Choose a trade", "Click the Trade Building to select Egg or Milk.")
                        : MachineFeedbackResolver.Processor(false, false, false,
                            trade.Process.PendingOutput == 0);
                }
                else if (building.TryGetComponent(out Harvester harvester))
                {
                    feedback = MachineFeedbackResolver.Harvester(
                        harvester.HasOutput &&
                        processorTransportCoordinator?.CanAcceptOutput(harvester.OutputCell) != true,
                        harvester.ConnectedFarmPlot?.SelectedCrop == null);
                }
                else continue;
                bool hovered = occupancy.TryGetBuilding(hoverHighlight.HoveredCell,
                    out BuildingPlacement over) && over == entry.Key &&
                    !IsPointerOverInterface;
                entry.Value.SetFeedback(feedback, hovered, BlocksAllWorldInput);
                if (foodDemoControls)
                    issueTracker.Observe(entry.Key, feedback, Time.time);
            }

            if (foodDemoControls)
            {
                issueTracker.EndFrame(Time.time);
                UpdateWorldInformationLod();
            }
        }

        private void UpdateWorldInformationLod()
        {
            if (factoryCamera == null && Camera.main != null)
                factoryCamera = Camera.main.GetComponent<FactoryCameraController>();
            WorldInformationLevel level = factoryCamera?.InformationLevel ??
                WorldInformationLevel.Close;
            if (market != null && market.TryGetComponent(
                    out MachineVisualAnimator marketAnimation))
                marketAnimation.SetInformationLevel(level,
                    hoverHighlight.HoveredCell == market.InputCell &&
                    !IsPointerOverInterface);
            occupancy.TryGetBuilding(hoverHighlight.HoveredCell,
                out BuildingPlacement hoveredPlacement);
            foreach (KeyValuePair<BuildingPlacement, PlacedBuilding> entry in buildingInstances)
            {
                bool detail = entry.Key == hoveredPlacement && !IsPointerOverInterface ||
                    selection.Contains(entry.Key) ||
                    feedbackViews.TryGetValue(entry.Key, out MachineFeedbackView view) &&
                    view.IsEmphasized;
                if (entry.Value.TryGetComponent(out Belt belt))
                    belt.SetInformationLevel(level, detail);
                if (entry.Value.TryGetComponent(out MachineVisualAnimator animation))
                    animation.SetInformationLevel(level, detail);
                foreach (TextMesh label in entry.Value.GetComponentsInChildren<TextMesh>(true))
                {
                    if (!label.gameObject.name.EndsWith(" label", StringComparison.Ordinal))
                        continue;
                    string original = label.gameObject.name[..^6];
                    label.text = level == WorldInformationLevel.Medium && !detail
                        ? original switch
                        {
                            "IN" => "I", "OUT" => "O", "PROP" => "P",
                            "IN A" or "OUT A" => "A",
                            "IN B" or "OUT B" => "B", _ => original
                        } : original;
                    label.gameObject.SetActive(level != WorldInformationLevel.Far || detail);
                }
            }
        }

        private bool HandleConstructionHistoryShortcut()
        {
            if (demoPanel != DemoPanel.None || cropPickerPlot != null ||
                isGroupPasteModeActive || placementDrag.IsActive ||
                removalDrag.IsActive || propertySupply?.IsPipeDragging == true ||
                !Keyboard.current.ctrlKey.isPressed) return false;
            if (Keyboard.current.zKey.wasPressedThisFrame)
            {
                if (constructionHistory.UndoCount == 0)
                    constructionMessage = "Nothing to undo.";
                else constructionHistory.TryUndo(ApplyConstructionHistory);
                return true;
            }
            if (Keyboard.current.yKey.wasPressedThisFrame)
            {
                if (constructionHistory.RedoCount == 0)
                    constructionMessage = "Nothing to redo.";
                else constructionHistory.TryRedo(ApplyConstructionHistory);
                return true;
            }
            return false;
        }

        private void HandleModeInput()
        {
            if (foodDemoControls && !Keyboard.current.ctrlKey.isPressed &&
                Keyboard.current.mKey.wasPressedThisFrame && !isPlacementModeActive &&
                !isGroupPasteModeActive && selection.SelectedPlacements.Count > 0)
            {
                var sources = new List<BuildingPlacement>(selection.SelectedPlacements);
                if (CanCutDemoSources(sources) &&
                    TryCaptureSelection(out BuildingGroupCopy moving))
                {
                    EnterGroupPasteMode(moving, sources);
                    constructionMessage = "Move selection: R rotates, Click places, Esc cancels.";
                }
                return;
            }
            if (Keyboard.current.ctrlKey.isPressed &&
                Keyboard.current.cKey.wasPressedThisFrame)
            {
                CopySelection();
            }

            if (Keyboard.current.ctrlKey.isPressed &&
                Keyboard.current.xKey.wasPressedThisFrame)
            {
                CutSelection();
            }

            if (Keyboard.current.ctrlKey.isPressed &&
                Keyboard.current.vKey.wasPressedThisFrame)
            {
                EnterGroupPasteMode(copiedGroup);
            }

            if (isGroupPasteModeActive)
            {
                if (Keyboard.current.escapeKey.wasPressedThisFrame)
                {
                    ExitGroupPasteMode();
                }

                if (isGroupPasteModeActive && Keyboard.current.rKey.wasPressedThisFrame)
                {
                    activeGroup = activeGroup.RotateClockwise();
                }

                if (isGroupPasteModeActive && !Keyboard.current.ctrlKey.isPressed &&
                    Keyboard.current.hKey.wasPressedThisFrame)
                {
                    if (activeGroup.TryMirrorHorizontal(out BuildingGroupCopy mirrored))
                    {
                        activeGroup = mirrored;
                    }
                    else
                    {
                        Debug.LogWarning("This group has a building that cannot be mirrored horizontally.", this);
                    }
                }

                if (isGroupPasteModeActive && !Keyboard.current.ctrlKey.isPressed &&
                    Keyboard.current.vKey.wasPressedThisFrame)
                {
                    if (activeGroup.TryMirrorVertical(out BuildingGroupCopy mirrored))
                    {
                        activeGroup = mirrored;
                    }
                    else
                    {
                        Debug.LogWarning("This group has a building that cannot be mirrored vertically.", this);
                    }
                }

                return;
            }

            if (Keyboard.current.bKey.wasPressedThisFrame)
            {
                isPlacementModeActive = true;
                selectedRotation = GetRememberedRotation(GetSelectedOption());
                beltDragPlanner.Reset();
                placementDrag.Reset();
            }

            if (!isPlacementModeActive)
            {
                return;
            }

            if (Keyboard.current.rKey.wasPressedThisFrame)
            {
                selectedRotation = selectedRotation.RotateClockwise();
                RememberRotation(GetSelectedOption(), selectedRotation);
                beltRotationExplicit = true;
            }
        }

        private void CopySelection()
        {
            if (!TryCaptureSelection(out BuildingGroupCopy group))
            {
                return;
            }

            copiedGroup = group;
            constructionMessage = $"Copied {group.Items.Count + group.PropertyItems.Count} selected parts.";
            ExitGroupPasteMode();
        }

        private void CutSelection()
        {
            var sources = new List<BuildingPlacement>(selection.SelectedPlacements);
            if (sources.Count == 0 ||
                !CanCutDemoSources(sources) ||
                !TryCaptureSelection(out BuildingGroupCopy group))
            {
                return;
            }

            copiedGroup = group;
            EnterGroupPasteMode(group, sources);
            constructionMessage = "Choose a destination for the cut selection.";
        }

        private bool CanCutDemoSources(IReadOnlyList<BuildingPlacement> sources)
        {
            var removedPropertyCells = new List<Vector2Int>();
            foreach (BuildingPlacement source in sources)
            {
                if (buildingInstances.TryGetValue(source, out PlacedBuilding building))
                {
                    if (building.TryGetComponent(out Processor processor))
                        removedPropertyCells.Add(processor.PropertyCell);
                    if (source.DefinitionId == nameof(FarmPlot) &&
                        occupancy.TryGetBuilding(source.AnchorCell,
                            out BuildingPlacement covering) && covering != source &&
                        !sources.Contains(covering))
                    {
                        constructionMessage = "Select the covering Harvester with its Farm Plot.";
                        return false;
                    }
                    if (HasActiveDemoItems(building.gameObject))
                    {
                        constructionMessage = $"Empty {source.DefinitionId} before cutting it.";
                        return false;
                    }
                    if (CanRemove(building.gameObject)) continue;
                    constructionMessage = $"{source.DefinitionId} cannot be cut now.";
                    return false;
                }
                if (propertySupply?.TryGetClipboardConnection(source,
                        out PropertyConnection connection) == true &&
                    connection.Kind is PropertyConnectionKind.Collector or
                        PropertyConnectionKind.Pipe)
                {
                    removedPropertyCells.Add(connection.Cell);
                    continue;
                }
                constructionMessage = "This Property connection cannot be cut.";
                return false;
            }
            if (removedPropertyCells.Count > 0 &&
                propertySupply?.CanRemoveWithoutBreakingDependents(
                    removedPropertyCells) != true)
            {
                constructionMessage = "Other Property connections depend on this selection.";
                return false;
            }
            return true;
        }

        private static bool HasActiveDemoItems(GameObject building)
        {
            return building.TryGetComponent(out FarmPlot plot) &&
                    (plot.MatureCount > 0 ||
                     plot.CaptureWorldState().elapsedSeconds > 0f) ||
                building.TryGetComponent(out Harvester harvester) &&
                    (harvester.OutputCount > 0 ||
                     harvester.CaptureWorldState().elapsedSeconds > 0f) ||
                building.TryGetComponent(out Belt belt) && !belt.CanMove ||
                building.TryGetComponent(out Processor processor) &&
                    processor.State != ProcessorState.Idle ||
                building.TryGetComponent(out BasicMixer mixer) &&
                    (mixer.SlotA != null || mixer.SlotB != null || mixer.HasOutput) ||
                building.TryGetComponent(out Cutter cutter) &&
                    cutter.State != CutterState.Idle ||
                building.TryGetComponent(out TradeBuilding trade) && !trade.Process.CanChangeTrade;
        }

        private bool TryCaptureSelection(out BuildingGroupCopy group)
        {
            group = null;
            if (selection.SelectedPlacements.Count == 0)
            {
                return false;
            }

            var sourceItems = new List<BuildingGroupCopyItem>();
            var propertyItems = new List<PropertyConnection>();
            foreach (BuildingPlacement placement in selection.SelectedPlacements)
            {
                if (!buildingInstances.TryGetValue(placement, out PlacedBuilding instance))
                {
                    if (foodDemoControls &&
                        propertySupply?.TryGetClipboardConnection(placement,
                            out PropertyConnection connection) == true &&
                        connection.Kind is PropertyConnectionKind.Collector or
                            PropertyConnectionKind.Pipe)
                    {
                        propertyItems.Add(connection);
                        continue;
                    }
                    constructionMessage = $"Cannot copy {placement.DefinitionId}.";
                    return false;
                }

                if (!TryGetCopyOption(placement, out BuildingPlacementOption option))
                {
                    constructionMessage = $"Cannot copy {placement.DefinitionId}.";
                    return false;
                }
                sourceItems.Add(new BuildingGroupCopyItem(
                    option,
                    placement.AnchorCell,
                    placement.Rotation,
                    instance.GetComponent<FarmPlot>()?.SelectedCrop?.Id,
                    instance.GetComponent<Belt>()?.OutputMask ?? 0));
            }

            group = new BuildingGroupCopy(sourceItems, propertyItems);
            return true;
        }

        private bool TryGetCopyOption(
            BuildingPlacement placement,
            out BuildingPlacementOption option)
        {
            foreach (BuildingPlacementOption candidate in buildingOptions)
            {
                if (candidate?.Definition?.Id == placement.DefinitionId &&
                    candidate.Definition.InstancePrefab != null)
                {
                    option = candidate;
                    return true;
                }
            }

            option = null;
            return false;
        }

        private static IBuildingMoveState GetMoveState(GameObject buildingObject)
        {
            foreach (MonoBehaviour component in buildingObject.GetComponents<MonoBehaviour>())
            {
                if (component is IBuildingMoveState moveState)
                {
                    return moveState;
                }
            }

            return null;
        }

        private void EnterGroupPasteMode(
            BuildingGroupCopy group,
            IReadOnlyList<BuildingPlacement> sources = null)
        {
            if (group == null)
            {
                return;
            }

            ExitGroupPasteMode();
            activeGroup = group;
            if (sources != null)
            {
                foreach (BuildingPlacement source in sources)
                {
                    if (buildingInstances.ContainsKey(source))
                    {
                        moveSources.Add(source);
                    }
                    else if (propertySupply?.ContainsPlacement(source) == true)
                        movePropertySources.Add(source.AnchorCell);
                }
            }
            isPlacementModeActive = false;
            selectionStartCell = null;
            HideSelectionArea();
            ClearRightClickRemoval();
            beltDragPlanner.Reset();
            placementDrag.Reset();
            placementPreview.Hide();
            foreach (BuildingGroupCopyItem item in activeGroup.Items)
            {
                var previewObject = new GameObject("Group Paste Preview");
                previewObject.transform.SetParent(transform, false);
                groupPreviews.Add(previewObject.AddComponent<BuildingPreview>());
            }
            foreach (PropertyGroupCopyItem item in activeGroup.PropertyItems)
            {
                var previewObject = new GameObject("Property Paste Preview");
                previewObject.transform.SetParent(transform, false);
                SpriteRenderer renderer = previewObject.AddComponent<SpriteRenderer>();
                renderer.sprite = BuildingVisualFactory.PlaceholderSprite;
                renderer.sortingOrder = 75;
                propertyGroupPreviews.Add(previewObject);
            }

            isGroupPasteModeActive = true;
            pasteAwaitingMouseRelease = true;
        }

        private void ExitGroupPasteMode()
        {
            isGroupPasteModeActive = false;
            pasteAwaitingMouseRelease = false;
            activeGroup = null;
            activeBlueprintName = null;
            moveSources.Clear();
            movePropertySources.Clear();
            foreach (BuildingPreview preview in groupPreviews)
            {
                if (preview != null)
                {
                    Destroy(preview.gameObject);
                }
            }

            groupPreviews.Clear();
            foreach (GameObject preview in propertyGroupPreviews)
                if (preview != null) Destroy(preview);
            propertyGroupPreviews.Clear();
        }

        private void HandleGroupPasteInput()
        {
            Vector2Int anchorCell = hoverHighlight.HoveredCell;
            bool canPlaceGroup = CanPlaceDemoGroup(anchorCell,
                out IReadOnlyList<PropertyGroupCopyItem> _);
            for (int index = 0; index < activeGroup.Items.Count; index++)
            {
                BuildingGroupCopyItem item = activeGroup.Items[index];
                groupPreviews[index].Show(
                    item.Option,
                    gridSystem,
                    anchorCell + item.Offset,
                    item.Rotation,
                    canPlaceGroup);
            }
            for (int index = 0; index < activeGroup.PropertyItems.Count; index++)
            {
                PropertyGroupCopyItem item = activeGroup.PropertyItems[index];
                GameObject preview = propertyGroupPreviews[index];
                preview.transform.position = gridSystem.GridToWorld(
                    anchorCell + item.Offset) + new Vector3(0f, 0f, -0.07f);
                preview.transform.localScale = Vector3.one * gridSystem.CellSize * 0.5f;
                preview.GetComponent<SpriteRenderer>().color = canPlaceGroup
                    ? new Color(0.25f, 0.95f, 0.45f, 0.7f)
                    : new Color(0.95f, 0.25f, 0.2f, 0.7f);
            }

            if (pasteAwaitingMouseRelease)
            {
                pasteAwaitingMouseRelease = Mouse.current.leftButton.isPressed;
                return;
            }

            if (!canPlaceGroup || !Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (!canPlaceGroup && Mouse.current.leftButton.wasPressedThisFrame)
                    constructionMessage = GetGroupFailureReason(anchorCell);
                return;
            }

            PlaceDemoGroup(anchorCell);
        }

        private bool CanPlaceDemoGroup(Vector2Int anchor,
            out IReadOnlyList<PropertyGroupCopyItem> propertyPlan)
        {
            propertyPlan = null;
            if ((moveSources.Count > 0 || movePropertySources.Count > 0) &&
                !CanCutDemoSources(GetCurrentMovePlacements()))
                return false;
            var occupied = new HashSet<Vector2Int>();
            var overlay = new HashSet<Vector2Int>();
            foreach (BuildingGroupCopyItem item in activeGroup.Items)
            {
                if (item.Option.Definition.Id == nameof(Harvester)) continue;
                Vector2Int cell = anchor + item.Offset;
                BuildingDefinition definition = item.Option.Definition;
                if (IsMachineLocked(item.Option) ||
                    !occupancy.CanPlace(definition, cell, item.Rotation) ||
                    !CanSatisfyPlacementBehavior(item.Option, cell, item.Rotation))
                    return false;
                if (item.CropId != null)
                {
                    FarmPlot prefab = definition.InstancePrefab?.GetComponent<FarmPlot>();
                    CropDefinition crop = prefab?.AvailableCrops.FirstOrDefault(candidate =>
                        candidate.Id == item.CropId);
                    if (crop == null || !string.IsNullOrEmpty(crop.RequiredUnlockId) &&
                        market?.Unlocks.IsUnlocked(UnlockKey.CropCategory,
                            crop.RequiredUnlockId) != true) return false;
                }
                var candidate = new BuildingPlacement(definition.Id, cell,
                    definition.Footprint, item.Rotation, definition.OccupiedCells);
                foreach (Vector2Int occupiedCell in candidate.OccupiedCells)
                    if (!occupied.Add(occupiedCell)) return false;
            }
            foreach (BuildingGroupCopyItem item in activeGroup.Items)
            {
                if (item.Option.Definition.Id != nameof(Harvester)) continue;
                Vector2Int cell = anchor + item.Offset;
                Vector2Int farm = HarvesterPlacementBehavior.GetFarmCell(cell,
                    item.Option.Definition.Footprint, item.Rotation);
                bool copiedPlot = activeGroup.Items.Any(other =>
                    other.Option.Definition.Id == nameof(FarmPlot) &&
                    anchor + other.Offset == farm);
                bool existingPlot = occupancy.TryGetUnderlyingBuilding(farm,
                        out BuildingPlacement plot) &&
                    plot.DefinitionId == nameof(FarmPlot) &&
                    FarmPlot.GetAt(farm) != null &&
                    occupancy.TryGetBuilding(farm, out BuildingPlacement top) && top == plot;
                if (!copiedPlot && !existingPlot ||
                    !copiedPlot && !occupancy.CanPlaceOver(cell,
                        item.Option.Definition.Footprint, item.Rotation, farm, plot))
                    return false;
                var candidate = new BuildingPlacement(nameof(Harvester), cell,
                    item.Option.Definition.Footprint, item.Rotation);
                foreach (Vector2Int candidateCell in candidate.OccupiedCells)
                {
                    if (!overlay.Add(candidateCell)) return false;
                    if (candidateCell == farm) continue;
                    if (!occupancy.CanPlace(candidateCell, Vector2Int.one,
                            BuildingRotation.Degrees0) || !occupied.Add(candidateCell))
                        return false;
                }
            }
            return activeGroup.PropertyItems.Count == 0 ||
                propertySupply.TryPlanClipboardConnections(activeGroup.PropertyItems,
                    anchor, occupied, movePropertySources, out propertyPlan);
        }

        private IReadOnlyList<BuildingPlacement> GetCurrentMovePlacements()
        {
            var placements = new List<BuildingPlacement>(moveSources);
            foreach (Vector2Int cell in movePropertySources)
                if (occupancy.TryGetBuilding(cell, out BuildingPlacement placement))
                    placements.Add(placement);
            return placements;
        }

        private bool PlaceDemoGroup(Vector2Int anchor)
        {
            if (!CanPlaceDemoGroup(anchor,
                    out IReadOnlyList<PropertyGroupCopyItem> propertyPlan)) return false;
            ConstructionLayout historyBefore = !suppressConstructionHistory
                ? CaptureConstructionLayout() : null;
            bool priorSuppression = suppressConstructionHistory;
            suppressConstructionHistory = true;
            var placedBuildings = new List<BuildingPlacement>();
            var placedProperties = new List<Vector2Int>();
            bool failed = false;
            try
            {
                IEnumerable<BuildingGroupCopyItem> ordered = activeGroup.Items
                    .OrderBy(item => item.Option.Definition.Id == nameof(FarmPlot) ? 0 :
                        item.Option.Definition.Id == nameof(Harvester) ? 2 : 1);
                foreach (BuildingGroupCopyItem item in ordered)
                {
                    if (!PlaceBuilding(item.Option, anchor + item.Offset, item.Rotation,
                             out BuildingPlacement placed))
                    {
                        failed = true;
                        break;
                    }
                    placedBuildings.Add(placed);
                    if (item.OutputMask != 0 &&
                        buildingInstances[placed].TryGetComponent(out Belt placedBelt))
                        placedBelt.SetOutputs(item.OutputMask);
                    if (item.CropId == null) continue;
                    FarmPlot plot = buildingInstances[placed].GetComponent<FarmPlot>();
                    CropDefinition crop = plot.AvailableCrops.FirstOrDefault(candidate =>
                        candidate.Id == item.CropId && plot.IsCropUnlocked(candidate));
                    if (crop == null) { failed = true; break; }
                    plot.SelectCrop(crop);
                }
                if (!failed && propertyPlan != null)
                    foreach (PropertyGroupCopyItem item in propertyPlan)
                    {
                        if (!propertySupply.TryPlaceClipboardConnection(item, anchor))
                        {
                            failed = true;
                            break;
                        }
                        placedProperties.Add(anchor + item.Offset);
                    }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                failed = true;
            }
            if (failed)
            {
                for (int index = placedProperties.Count - 1; index >= 0; index--)
                    propertySupply.TryRemoveConnection(placedProperties[index]);
                foreach (BuildingPlacement placement in placedBuildings
                             .OrderByDescending(item => item.DefinitionId == nameof(Harvester)))
                    TryRemovePlacement(placement);
                constructionMessage = "Paste failed; the original selection was kept.";
                suppressConstructionHistory = priorSuppression;
                return false;
            }
            foreach (BuildingPlacement source in moveSources
                         .OrderByDescending(item => item.DefinitionId == nameof(Harvester)))
                TryRemovePlacement(source);
            foreach (Vector2Int cell in movePropertySources)
                propertySupply.TryRemoveConnection(cell);
            constructionMessage = $"Placed {placedBuildings.Count + placedProperties.Count} parts.";
            if (activeBlueprintName != null)
                toasts.Enqueue($"Placed {activeBlueprintName}", Time.time);
            ExitGroupPasteMode();
            suppressConstructionHistory = priorSuppression;
            RecordConstruction(historyBefore);
            return true;
        }

        private string GetGroupFailureReason(Vector2Int anchor)
        {
            foreach (BuildingGroupCopyItem item in activeGroup.Items)
            {
                string id = item.Option.Definition.Id;
                if (IsMachineLocked(item.Option))
                    return $"{id} requires {GetMachineUnlockRequirement(item.Option)}.";
                if (id == nameof(FarmPlot) &&
                    !CanSatisfyPlacementBehavior(item.Option,
                        anchor + item.Offset, item.Rotation))
                    return "Farm Plot requires unlocked farmable land.";
            }
            return activeGroup.PropertyItems.Count > 0
                ? "Clear occupied cells and check Property source connections."
                : "Clear occupied cells and check building dependencies.";
        }

        private ConstructionLayout CaptureConstructionLayout() =>
            ConstructionLayout.FromWorld(CaptureWorldSnapshot());

        private void OnPropertyConstructionStarting()
        {
            if (!suppressConstructionHistory)
                propertyHistoryStart = CaptureConstructionLayout();
        }

        private void OnPropertyConstructionFinished()
        {
            RecordConstruction(propertyHistoryStart);
            propertyHistoryStart = null;
        }

        private void OnCropChanging(FarmPlot plot)
        {
            if (!suppressConstructionHistory)
                cropHistoryStart = CaptureConstructionLayout();
        }

        private void OnCropChanged(FarmPlot plot)
        {
            RecordConstruction(cropHistoryStart);
            cropHistoryStart = null;
        }

        private void RecordConstruction(ConstructionLayout before)
        {
            if (before == null || suppressConstructionHistory || !foodDemoControls) return;
            constructionHistory.Record(before.Difference(CaptureConstructionLayout()));
        }

        private bool ApplyConstructionHistory(ConstructionLayout expected,
            ConstructionLayout desired)
        {
            constructionMessage = null;
            if (!foodDemoControls || activeGroup != null ||
                placementDrag.IsActive || removalDrag.IsActive ||
                propertySupply?.IsPipeDragging == true)
                return HistoryRejected("Finish the current construction tool first.");
            FactoryWorldData live = CaptureWorldSnapshot();
            foreach (SavedBuilding old in expected.Buildings)
                if (!live.buildings.Any(item => ConstructionLayout.Same(old, item)))
                    return HistoryRejected("A building changed since this action.");
            foreach (SavedPropertyConnection old in expected.Connections)
                if (!live.connections.Any(item => ConstructionLayout.Same(old, item)))
                    return HistoryRejected("A Property connection changed since this action.");

            var beltEdits = expected.Buildings
                .Where(old => old.definitionId == nameof(Belt))
                .Select(old => (Old: old, New: desired.Buildings.FirstOrDefault(
                    target => ConstructionLayout.BuildingKey(target) ==
                        ConstructionLayout.BuildingKey(old) &&
                        target.rotation == old.rotation)))
                .Where(pair => pair.New != null &&
                    pair.Old.belt?.outputMask != pair.New.belt?.outputMask)
                .ToArray();
            if (beltEdits.Length > 0)
            {
                var editedKeys = new HashSet<(string, int, int)>(
                    beltEdits.Select(pair => ConstructionLayout.BuildingKey(pair.Old)));
                expected = ConstructionLayout.FromWorld(new FactoryWorldData
                {
                    buildings = expected.Buildings.Where(item =>
                        !editedKeys.Contains(ConstructionLayout.BuildingKey(item))).ToArray(),
                    connections = expected.Connections
                });
                desired = ConstructionLayout.FromWorld(new FactoryWorldData
                {
                    buildings = desired.Buildings.Where(item =>
                        !editedKeys.Contains(ConstructionLayout.BuildingKey(item))).ToArray(),
                    connections = desired.Connections
                });
                foreach (SavedBuilding building in live.buildings)
                {
                    SavedBuilding edit = beltEdits.FirstOrDefault(pair =>
                        ConstructionLayout.BuildingKey(pair.Old) ==
                        ConstructionLayout.BuildingKey(building)).New;
                    if (edit != null) building.belt.outputMask = edit.belt.outputMask;
                }
            }

            // Crop configuration changes keep their existing Farm Plot and do not touch time.
            if (expected.Buildings.Length == 1 && desired.Buildings.Length == 1 &&
                expected.Connections.Length == 0 && desired.Connections.Length == 0 &&
                ConstructionLayout.BuildingKey(expected.Buildings[0]) ==
                    ConstructionLayout.BuildingKey(desired.Buildings[0]) &&
                expected.Buildings[0].rotation == desired.Buildings[0].rotation &&
                expected.Buildings[0].definitionId == nameof(FarmPlot))
            {
                SavedBuilding old = expected.Buildings[0];
                FarmPlot plot = buildingInstances.FirstOrDefault(entry =>
                    entry.Key.DefinitionId == nameof(FarmPlot) &&
                    entry.Key.AnchorCell == new Vector2Int(old.x, old.y))
                    .Value?.GetComponent<FarmPlot>();
                SavedFarmPlot state = plot?.CaptureWorldState();
                if (state == null || state.matureCount > 0 || state.elapsedSeconds > 0f)
                    return HistoryRejected("Wait until the Farm Plot is empty and idle.");
                CropDefinition crop = desired.Buildings[0].farmPlot?.cropId == null
                    ? null : plot.AvailableCrops.FirstOrDefault(item =>
                        item.Id == desired.Buildings[0].farmPlot.cropId &&
                        plot.IsCropUnlocked(item));
                if (desired.Buildings[0].farmPlot?.cropId != null && crop == null)
                    return HistoryRejected("The crop is no longer unlocked.");
                suppressConstructionHistory = true;
                try { plot.SelectCrop(crop); }
                finally { suppressConstructionHistory = false; }
                return true;
            }

            var sourceBuildings = new List<BuildingPlacement>();
            foreach (SavedBuilding old in expected.Buildings)
            {
                KeyValuePair<BuildingPlacement, PlacedBuilding> entry =
                    buildingInstances.FirstOrDefault(candidate =>
                        candidate.Key.DefinitionId == old.definitionId &&
                        candidate.Key.AnchorCell == new Vector2Int(old.x, old.y));
                if (entry.Key == null || !CanRemove(entry.Value.gameObject) ||
                    HasActiveDemoItems(entry.Value.gameObject))
                    return HistoryRejected($"Empty {old.definitionId} before changing it.");
                SavedBuilding current = live.buildings.First(item =>
                    ConstructionLayout.BuildingKey(item) ==
                    ConstructionLayout.BuildingKey(old));
                if (current.farmPlot?.elapsedSeconds > 0f ||
                    current.harvester?.elapsedSeconds > 0f)
                    return HistoryRejected("Wait for active crop work to finish.");
                sourceBuildings.Add(entry.Key);
            }
            var sourceProperties = new List<Vector2Int>();
            foreach (SavedPropertyConnection old in expected.Connections)
                sourceProperties.Add(new Vector2Int(old.x, old.y));
            var removedNetworkCells = new List<Vector2Int>(sourceProperties);
            foreach (BuildingPlacement source in sourceBuildings)
                if (buildingInstances[source].TryGetComponent(out Processor processor))
                    removedNetworkCells.Add(processor.PropertyCell);
            if (removedNetworkCells.Count > 0 &&
                !propertySupply.CanRemoveWithoutBreakingDependents(removedNetworkCells))
                return HistoryRejected("Other Property connections still depend on this layout.");
            if (sourceProperties.Count > 0 && buildingInstances.Values.Any(item =>
                    item.TryGetComponent(out Processor processor) &&
                    processor.State != ProcessorState.Idle))
                return HistoryRejected("Stop active Processors before changing Property pipes.");

            var expectedBuildingKeys = new HashSet<(string, int, int)>(
                expected.Buildings.Select(ConstructionLayout.BuildingKey));
            var expectedConnectionKeys = new HashSet<(int, int)>(
                expected.Connections.Select(ConstructionLayout.ConnectionKey));
            var candidate = new FactoryWorldData
            {
                buildings = live.buildings.Where(item =>
                    !expectedBuildingKeys.Contains(ConstructionLayout.BuildingKey(item)))
                    .Concat(desired.Buildings).ToArray(),
                connections = live.connections.Where(item =>
                    !expectedConnectionKeys.Contains(ConstructionLayout.ConnectionKey(item)))
                    .Concat(desired.Connections).ToArray()
            };
            try
            {
                ValidateWorldSnapshot(candidate, market.Unlocks.Unlocked.Select(key =>
                    new SavedUnlock { category = key.Category, id = key.Id }).ToArray());
            }
            catch (ArgumentException exception)
            {
                return HistoryRejected(exception.Message);
            }

            if (desired.Buildings.Length == 0 && desired.Connections.Length == 0)
            {
                suppressConstructionHistory = true;
                try
                {
                    foreach (BuildingPlacement placement in sourceBuildings.OrderByDescending(
                        item => item.DefinitionId == nameof(Harvester)))
                        TryRemovePlacement(placement);
                    foreach (Vector2Int cell in sourceProperties)
                        propertySupply.TryRemoveConnection(cell);
                }
                finally { suppressConstructionHistory = false; }
                ApplyBeltHistoryEdits(beltEdits);
                return true;
            }

            var items = new List<BuildingGroupCopyItem>();
            foreach (SavedBuilding target in desired.Buildings)
            {
                BuildingPlacementOption option = buildingOptions.FirstOrDefault(item =>
                    item?.Definition?.Id == target.definitionId);
                if (option == null || IsMachineLocked(option))
                    return HistoryRejected($"{target.definitionId} is locked or unavailable.");
                items.Add(new BuildingGroupCopyItem(option,
                    new Vector2Int(target.x, target.y), target.rotation,
                    cropId: target.farmPlot?.cropId,
                    outputMask: target.belt?.outputMask ?? 0));
            }
            var properties = desired.Connections.Select(item => new PropertyConnection(
                new Vector2Int(item.x, item.y),
                new Vector2Int(item.sourceX, item.sourceY), item.property,
                item.kind)).ToArray();
            var group = new BuildingGroupCopy(items, properties);
            int anchorX = items.Count > 0 ? items.Min(item => item.Offset.x) : int.MaxValue;
            int anchorY = items.Count > 0 ? items.Min(item => item.Offset.y) : int.MaxValue;
            foreach (PropertyConnection connection in properties)
            {
                anchorX = Math.Min(anchorX, connection.Cell.x);
                anchorY = Math.Min(anchorY, connection.Cell.y);
            }
            activeGroup = group;
            moveSources.AddRange(sourceBuildings);
            movePropertySources.AddRange(sourceProperties);
            Vector2Int destinationAnchor = new(anchorX, anchorY);
            if (!CanPlaceDemoGroup(destinationAnchor,
                    out IReadOnlyList<PropertyGroupCopyItem> planned) ||
                desired.Connections.Any(saved => planned == null ||
                    !planned.Any(item => destinationAnchor + item.Offset ==
                            new Vector2Int(saved.x, saved.y) &&
                        item.Connection.SourceCell ==
                            new Vector2Int(saved.sourceX, saved.sourceY))))
            {
                ExitGroupPasteMode();
                return HistoryRejected("The original Property ownership or placement is unavailable.");
            }
            suppressConstructionHistory = true;
            try
            {
                bool placed = PlaceDemoGroup(destinationAnchor);
                if (!placed)
                {
                    ExitGroupPasteMode();
                    return HistoryRejected("The construction area or connection changed.");
                }
                ApplyBeltHistoryEdits(beltEdits);
                return true;
            }
            finally { suppressConstructionHistory = false; }
        }

        private bool HistoryRejected(string reason)
        {
            constructionMessage = reason;
            return false;
        }

        private void HandleRemovalInput()
        {
            if (!foodDemoControls || isPlacementModeActive || isGroupPasteModeActive ||
                propertySupply?.IsActive == true)
            {
                RecordConstruction(removalHistoryStart);
                removalHistoryStart = null;
                ClearRightClickRemoval();
                return;
            }
            if (!Mouse.current.rightButton.isPressed && removalHistoryStart != null)
            {
                RecordConstruction(removalHistoryStart);
                removalHistoryStart = null;
            }
            if (!Mouse.current.rightButton.isPressed)
                suppressRightRemovalUntilRelease = false;
            if (occupancy.TryGetBuilding(hoverHighlight.HoveredCell,
                    out BuildingPlacement hovered) &&
                CanRemovePlacement(hovered))
            {
                if (outlinedRemoval != hovered) ShowRemovalOutline(hovered);
            }
            else ClearRemovalOutline();
            if (!Mouse.current.rightButton.isPressed || suppressRightRemovalUntilRelease)
                removalDrag.Reset();
            else if (Mouse.current.rightButton.wasPressedThisFrame || removalDrag.IsActive)
            {
                removalHistoryStart ??= CaptureConstructionLayout();
                bool previous = suppressConstructionHistory;
                suppressConstructionHistory = true;
                try
                {
                    foreach (Vector2Int cell in removalDrag.Continue(hoverHighlight.HoveredCell))
                        TryRemoveBuilding(cell);
                }
                finally { suppressConstructionHistory = previous; }
            }
        }

        private void ClearRightClickRemoval()
        {
            removalDrag.Reset();
            ClearRemovalOutline();
        }

        private void ClearRemovalOutline()
        {
            if (removalOutline != null)
            {
                Destroy(removalOutline);
                removalOutline = null;
            }
            outlinedRemoval = null;
        }

        private void OnDestroy()
        {
            if (propertySupply != null)
            {
                propertySupply.ConstructionStarting -= OnPropertyConstructionStarting;
                propertySupply.ConstructionFinished -= OnPropertyConstructionFinished;
            }
            if (foodDemoControls && market != null)
            {
                if (market.OrderSequence != null)
                    market.OrderSequence.CompletedWithRewards -= OnOrderCompleted;
                market.Unlocks.UnlockedContent -= OnContentUnlocked;
                market.FoodDelivered -= OnFoodDelivered;
                if (marketPanel != null) marketPanel.GameSaved -= OnGameSaved;
            }
            ClearRemovalOutline();
            if (removalOutlineMaterial != null)
                Destroy(removalOutlineMaterial);
        }

        private void ShowRemovalOutline(BuildingPlacement placement)
        {
            ClearRemovalOutline();
            outlinedRemoval = placement;
            removalOutline = new GameObject("Right-click removal outline");
            removalOutline.transform.SetParent(transform, false);
            if (removalOutlineMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader != null)
                    removalOutlineMaterial = new Material(shader);
            }
            var occupied = new HashSet<Vector2Int>(placement.OccupiedCells);
            Vector2Int[] directions =
            {
                Vector2Int.left, Vector2Int.up, Vector2Int.right, Vector2Int.down
            };
            foreach (Vector2Int cell in placement.OccupiedCells)
            {
                Vector3 center = gridSystem.GridToWorld(cell);
                float half = gridSystem.CellSize * 0.5f;
                for (int edge = 0; edge < directions.Length; edge++)
                {
                    if (occupied.Contains(cell + directions[edge])) continue;
                    var lineObject = new GameObject("Outline edge");
                    lineObject.transform.SetParent(removalOutline.transform, false);
                    LineRenderer line = lineObject.AddComponent<LineRenderer>();
                    line.sharedMaterial = removalOutlineMaterial;
                    line.useWorldSpace = true;
                    line.positionCount = 2;
                    line.startWidth = 0.07f * gridSystem.CellSize;
                    line.endWidth = line.startWidth;
                    line.startColor = Color.red;
                    line.endColor = Color.red;
                    line.sortingOrder = 110;
                    line.shadowCastingMode = ShadowCastingMode.Off;
                    line.receiveShadows = false;
                    Vector3 a = edge switch
                    {
                        0 => new Vector3(-half, -half, -0.08f),
                        1 => new Vector3(-half, half, -0.08f),
                        2 => new Vector3(half, half, -0.08f),
                        _ => new Vector3(half, -half, -0.08f)
                    };
                    Vector3 b = edge switch
                    {
                        0 => new Vector3(-half, half, -0.08f),
                        1 => new Vector3(half, half, -0.08f),
                        2 => new Vector3(half, -half, -0.08f),
                        _ => new Vector3(-half, -half, -0.08f)
                    };
                    line.SetPosition(0, center + a);
                    line.SetPosition(1, center + b);
                }
            }
        }

        private bool HandleSelectionInput()
        {
            if (isPlacementModeActive || isGroupPasteModeActive) return false;
            if (!selectionStartCell.HasValue &&
                Keyboard.current.shiftKey.isPressed &&
                Mouse.current.leftButton.wasPressedThisFrame)
            {
                selectionStartCell = hoverHighlight.HoveredCell;
                selectionBeforeDrag.Clear();
                selectionBeforeDrag.AddRange(selection.SelectedPlacements);
                beltDragPlanner.Reset();
                placementDrag.Reset();
                placementPreview.Hide();
            }

            if (selectionStartCell.HasValue)
            {
                Vector2Int endCell = hoverHighlight.HoveredCell;
                ShowSelectionArea(selectionStartCell.Value, endCell);
                if (!Mouse.current.leftButton.isPressed)
                {
                    if (endCell == selectionStartCell.Value)
                    {
                        selection.Clear();
                        foreach (BuildingPlacement previous in selectionBeforeDrag)
                            selection.Add(previous);
                        if (occupancy.TryGetBuilding(endCell,
                                out BuildingPlacement clicked) &&
                            (buildingInstances.ContainsKey(clicked) ||
                             propertySupply?.ContainsPlacement(clicked) == true))
                            selection.Toggle(clicked);
                    }
                    else
                    {
                        selection.SelectRectangle(occupancy, selectionStartCell.Value,
                            endCell, placement => buildingInstances.ContainsKey(placement) ||
                                foodDemoControls &&
                                propertySupply?.ContainsPlacement(placement) == true,
                            foodDemoControls);
                        foreach (BuildingPlacement previous in selectionBeforeDrag)
                            selection.Add(previous);
                    }
                    selectionBeforeDrag.Clear();
                    RefreshSelectionHighlights();
                    selectionStartCell = null;
                    HideSelectionArea();
                }

                return true;
            }

            if (Keyboard.current.deleteKey.wasPressedThisFrame)
            {
                var selected = new List<BuildingPlacement>(selection.SelectedPlacements);
                if (foodDemoControls && !CanCutDemoSources(selected)) return true;
                ConstructionLayout before = foodDemoControls
                    ? CaptureConstructionLayout() : null;
                bool previous = suppressConstructionHistory;
                suppressConstructionHistory = true;
                try
                {
                    foreach (BuildingPlacement placement in selected.OrderByDescending(
                        item => item.DefinitionId == nameof(Harvester)))
                        TryRemovePlacement(placement);
                }
                finally { suppressConstructionHistory = previous; }
                RecordConstruction(before);
            }

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                selection.Clear();
                if (occupancy.TryGetBuilding(hoverHighlight.HoveredCell,
                        out BuildingPlacement clicked) &&
                    (buildingInstances.ContainsKey(clicked) ||
                     foodDemoControls &&
                     propertySupply?.ContainsPlacement(clicked) == true))
                    selection.Add(clicked);
                RefreshSelectionHighlights();
            }

            return false;
        }

        private void ShowSelectionArea(Vector2Int firstCell, Vector2Int lastCell)
        {
            selectionArea ??= CreateSelectionVisual(
                "Selection Area", new Color(0.25f, 0.8f, 1f, 0.16f), 60);
            PositionSelectionVisual(selectionArea, firstCell, lastCell);
            selectionArea.SetActive(true);
        }

        private void HideSelectionArea()
        {
            if (selectionArea != null)
            {
                selectionArea.SetActive(false);
            }
        }

        private void RefreshSelectionHighlights()
        {
            foreach (BuildingPlacement placement in
                new List<BuildingPlacement>(selectionHighlights.Keys))
            {
                if (selection.Contains(placement))
                {
                    continue;
                }

                Destroy(selectionHighlights[placement]);
                selectionHighlights.Remove(placement);
            }

            foreach (BuildingPlacement placement in selection.SelectedPlacements)
            {
                if (selectionHighlights.ContainsKey(placement))
                {
                    continue;
                }

                GameObject highlight = CreateSelectionVisual(
                    "Selected Building", new Color(0.2f, 0.85f, 1f, 0.38f), 70);
                foreach (Vector2Int cell in placement.OccupiedCells)
                {
                    GameObject cellHighlight = CreateSelectionVisual(
                        "Occupied Cell", new Color(0.2f, 0.85f, 1f, 0.38f), 70);
                    cellHighlight.transform.SetParent(highlight.transform, true);
                    PositionSelectionVisual(cellHighlight, cell, cell);
                }
                highlight.GetComponent<SpriteRenderer>().enabled = false;
                selectionHighlights.Add(placement, highlight);
            }
        }

        private GameObject CreateSelectionVisual(string name, Color color, int sortingOrder)
        {
            var visual = new GameObject(name);
            visual.transform.SetParent(transform, false);
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = BuildingVisualFactory.PlaceholderSprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return visual;
        }

        private void PositionSelectionVisual(
            GameObject visual, Vector2Int firstCell, Vector2Int lastCell)
        {
            int minX = Math.Min(firstCell.x, lastCell.x);
            int maxX = Math.Max(firstCell.x, lastCell.x);
            int minY = Math.Min(firstCell.y, lastCell.y);
            int maxY = Math.Max(firstCell.y, lastCell.y);
            Vector3 firstCenter = gridSystem.GridToWorld(new Vector2Int(minX, minY));
            Vector3 lastCenter = gridSystem.GridToWorld(new Vector2Int(maxX, maxY));
            visual.transform.position = (firstCenter + lastCenter) * 0.5f +
                new Vector3(0f, 0f, -0.05f);
            visual.transform.localScale = new Vector3(
                (maxX - minX + 1) * gridSystem.CellSize,
                (maxY - minY + 1) * gridSystem.CellSize,
                1f);
        }

        private void HandlePlacementInput(
            BuildingPlacementOption option,
            Vector2Int anchorCell,
            bool canPlace)
        {
            if (!Mouse.current.leftButton.isPressed)
            {
                bool hadBeltPath = beltDragPlanner.HasPathRotation;
                BuildingRotation finalRotation = hadBeltPath ||
                    beltRotationExplicit ? selectedRotation :
                    GetSmartSingleBeltRotation(beltDragPlanner.PendingCell,
                        selectedRotation);
                if (option.SupportsContinuousPlacement &&
                    beltDragPlanner.TryComplete(finalRotation, out BeltPlacementStep finalStep))
                {
                    TryPlaceBeltStep(option, finalStep,
                        hadBeltPath || beltRotationExplicit);
                    beltRotationExplicit = false;
                }

                RecordConstruction(placementHistoryStart);
                placementHistoryStart = null;

                placementDrag.Reset();
                return;
            }

            if (!option.SupportsContinuousPlacement)
            {
                beltDragPlanner.Reset();
                placementDrag.Reset();
                if (Mouse.current.leftButton.wasPressedThisFrame && canPlace)
                {
                    ConstructionLayout before = foodDemoControls
                        ? CaptureConstructionLayout() : null;
                    if (PlaceBuilding(option, anchorCell, selectedRotation,
                            out BuildingPlacement placed) && foodDemoControls)
                    {
                        if (buildingInstances[placed].TryGetComponent(out FarmPlot plot))
                            OpenCropPicker(plot);
                        else if (buildingInstances[placed].TryGetComponent(out TradeBuilding trade))
                        { ClosePanel(); tradePickerBuilding = trade; }
                    }
                    RecordConstruction(before);
                }

                return;
            }

            if (!Mouse.current.leftButton.wasPressedThisFrame && !placementDrag.IsActive)
            {
                return;
            }

            if (foodDemoControls && placementHistoryStart == null)
                placementHistoryStart = CaptureConstructionLayout();

            IReadOnlyList<Vector2Int> newCells = placementDrag.Continue(anchorCell);
            foreach (BeltPlacementStep step in beltDragPlanner.Continue(newCells))
            {
                TryPlaceBeltStep(option, step, true);
            }
        }

        private void ApplyBeltHistoryEdits(
            (SavedBuilding Old, SavedBuilding New)[] edits)
        {
            foreach ((SavedBuilding old, SavedBuilding target) in edits)
            {
                KeyValuePair<BuildingPlacement, PlacedBuilding> entry =
                    buildingInstances.First(item =>
                        item.Key.DefinitionId == nameof(Belt) &&
                        item.Key.AnchorCell == new Vector2Int(old.x, old.y));
                entry.Value.GetComponent<Belt>().SetOutputs(target.belt.outputMask);
            }
        }

        private bool CanExtendBelt(BuildingPlacementOption option,
            Vector2Int cell)
        {
            if (option.Definition.Id != nameof(Belt) ||
                (!beltDragPlanner.HasPathRotation && !beltRotationExplicit) ||
                !occupancy.TryGetBuilding(cell, out BuildingPlacement existing) ||
                existing.DefinitionId != nameof(Belt)) return false;
            return buildingInstances.TryGetValue(existing,
                out PlacedBuilding instance) && instance.GetComponent<Belt>() != null;
        }

        private void TryPlaceBeltStep(BuildingPlacementOption option,
            BeltPlacementStep step, bool explicitDrag)
        {
            if (option.Definition.Id == nameof(Belt) && explicitDrag &&
                occupancy.TryGetBuilding(step.Cell, out BuildingPlacement existing) &&
                existing.DefinitionId == nameof(Belt) &&
                buildingInstances.TryGetValue(existing, out PlacedBuilding instance) &&
                instance.TryGetComponent(out Belt belt))
            {
                belt.SetOutputs(BeltConnectionPlanner.ExtendExisting(
                    belt.OutputMask, existing.Rotation.ToGridDirection(),
                    step.Rotation.ToGridDirection(), explicitDrag));
                return;
            }
            TryPlaceBuilding(option, step.Cell, step.Rotation);
        }

        private void TryRemoveBuilding(Vector2Int cell)
        {
            if (occupancy.TryGetBuilding(cell, out BuildingPlacement placement))
            {
                TryRemovePlacement(placement);
            }
        }

        private void TryRemovePlacement(BuildingPlacement placement)
        {
            if (!buildingInstances.TryGetValue(placement, out PlacedBuilding instance))
            {
                if (foodDemoControls &&
                    propertySupply?.TryGetClipboardConnection(placement,
                        out PropertyConnection connection) == true &&
                    connection.Kind is PropertyConnectionKind.Collector or
                        PropertyConnectionKind.Pipe &&
                    propertySupply.TryRemoveConnection(placement.AnchorCell))
                {
                    selection.Remove(placement);
                    RefreshSelectionHighlights();
                }
                return;
            }
            if (!CanRemove(instance.gameObject))
            {
                return;
            }

            if (occupancy.Remove(placement) && buildingInstances.Remove(placement))
            {
                feedbackViews.Remove(placement);
                selection.Remove(placement);
                RefreshSelectionHighlights();
                if (instance.TryGetComponent(out Belt removedBelt))
                    removedBelt.DetachForMove();
                Destroy(instance.gameObject);
            }
        }

        private void HandleInteractionInput()
        {
            if (isPlacementModeActive ||
                !Mouse.current.leftButton.wasPressedThisFrame ||
                !occupancy.TryGetBuilding(hoverHighlight.HoveredCell, out BuildingPlacement placement) ||
                !buildingInstances.TryGetValue(placement, out PlacedBuilding instance))
            {
                return;
            }

            if (instance.TryGetComponent(out TradeBuilding selectedTrade))
            {
                ClosePanel(); cropPickerPlot = null; tradePickerBuilding = null; tradePickerBuilding = selectedTrade;
                return;
            }
            if (foodDemoControls && feedbackViews.TryGetValue(placement,
                    out MachineFeedbackView feedbackView) &&
                feedbackView.Feedback.HasProblem)
            {
                Vector2Int? traceCell = GetFeedbackTraceCell(placement,
                    instance, feedbackView.Feedback.Ports);
                Vector3? tracePosition = traceCell.HasValue &&
                    occupancy.TryGetBuilding(traceCell.Value, out BuildingPlacement connected) &&
                    connected != placement
                    ? gridSystem.GridToWorld(traceCell.Value) : null;
                feedbackView.Emphasize(tracePosition);
                if (instance.GetComponent<Harvester>() == null ||
                    feedbackView.Feedback.Ports != MachineFeedbackPort.Crop)
                    return;
            }

            foreach (MonoBehaviour component in instance.GetComponents<MonoBehaviour>())
            {
                if (component is CozyFoodFactory.Food.FarmPlot farmPlot)
                {
                    if (foodDemoControls) OpenPanel(DemoPanel.Machine);
                    engraverUpgradePanel?.ShowFarmPlot(farmPlot);
                    return;
                }

                if (component is CozyFoodFactory.Food.Harvester harvester)
                {
                    if (foodDemoControls) OpenPanel(DemoPanel.Machine);
                    engraverUpgradePanel?.ShowHarvester(harvester);
                    return;
                }

                if (component is Processor processor)
                {
                    if (foodDemoControls)
                    {
                        recipeDiscoveryPanel?.OpenForMachine(DiscoveredRecipeKind.Processing);
                        return;
                    }
                    engraverUpgradePanel?.ShowProcessor(processor);
                    return;
                }

                if (component is BasicMixer mixer)
                {
                    if (foodDemoControls)
                    {
                        recipeDiscoveryPanel?.OpenForMachine(DiscoveredRecipeKind.Mixing);
                        return;
                    }
                    engraverUpgradePanel?.ShowMixer(mixer);
                    return;
                }
                if (component is Cutter cutter)
                {
                    if (foodDemoControls)
                    {
                        recipeDiscoveryPanel?.OpenForMachine(DiscoveredRecipeKind.Cutting);
                        return;
                    }
                    engraverUpgradePanel?.ShowCutter(cutter);
                    return;
                }
            }
        }

        private bool TryPlaceBuilding(
            BuildingPlacementOption option,
            Vector2Int anchorCell,
            BuildingRotation rotation)
        {
            return CanPlaceBuilding(option, anchorCell, rotation) &&
                PlaceBuilding(option, anchorCell, rotation);
        }

        private bool PlaceBuilding(
            BuildingPlacementOption option,
            Vector2Int anchorCell,
            BuildingRotation rotation)
        {
            return PlaceBuilding(option, anchorCell, rotation, out _);
        }

        private bool PlaceBuilding(
            BuildingPlacementOption option,
            Vector2Int anchorCell,
            BuildingRotation rotation,
            out BuildingPlacement placement)
        {
            BuildingDefinition definition = option.Definition;
            placement = null;
            bool registered;
            if (option.PlacementBehavior is HarvesterPlacementBehavior)
            {
                registered = TryGetHarvesterFarmPlacement(option, anchorCell, rotation,
                        out Vector2Int farmCell, out BuildingPlacement farmPlacement) &&
                    occupancy.TryRegisterOver(definition.Id, anchorCell, definition.Footprint,
                        rotation, farmCell, farmPlacement, out placement);
            }
            else
            {
                registered = occupancy.TryRegister(definition, anchorCell, rotation,
                    out placement);
            }

            if (!registered)
            {
                return false;
            }

            try
            {
                PlacedBuilding instance = CreateBuildingInstance(option, placement);
                buildingInstances.Add(placement, instance);
                MachineFeedbackView view = instance.GetComponent<MachineFeedbackView>();
                if (view != null) feedbackViews.Add(placement, view);
                return true;
            }
            catch
            {
                occupancy.Remove(placement);
                throw;
            }
        }

        private PlacedBuilding CreateBuildingInstance(
            BuildingPlacementOption option,
            BuildingPlacement placement)
        {
            BuildingDefinition definition = option.Definition;
            GameObject buildingObject = definition.InstancePrefab != null
                ? Instantiate(definition.InstancePrefab)
                : new GameObject();
            try
            {
                buildingObject.name = $"{placement.DefinitionId} {placement.AnchorCell}";
                Vector3 firstCellCenter = gridSystem.GridToWorld(placement.AnchorCell);
                buildingObject.transform.position = firstCellCenter + new Vector3(
                    (placement.RotatedFootprint.x - 1) * gridSystem.CellSize * 0.5f,
                    (placement.RotatedFootprint.y - 1) * gridSystem.CellSize * 0.5f,
                    0f);
                buildingObject.transform.rotation = Quaternion.Euler(0f, 0f,
                    -(int)placement.Rotation);

                PlacedBuilding instance = buildingObject.GetComponent<PlacedBuilding>();
                if (instance == null)
                {
                    instance = buildingObject.AddComponent<PlacedBuilding>();
                }

                instance.Initialize(placement);
                GameObject visual = BuildingVisualFactory.Create(
                    definition,
                    buildingObject.transform,
                    gridSystem.CellSize,
                    buildingObject.GetComponent<Harvester>() != null ? 11 : 10);
                BuildingVisualFactory.ApplyRotation(visual, definition,
                    placement.Rotation);
                if (definition.UsesPlaceholderVisual)
                    BuildingVisualFactory.Tint(visual, definition.PlacedColor);
                else BuildingVisualFactory.MultiplyAlpha(visual,
                    definition.PlacedColor.a);
                BuildingVisualFactory.CreatePortMarkers(buildingObject.transform,
                    option.PortPreviews, gridSystem.CellSize, placement.Rotation,
                    definition.Id == nameof(TradeBuilding));
                option.PlacementBehavior?.InitializePlacedBuilding(buildingObject, placement);
                if (foodDemoControls && buildingObject.TryGetComponent(out FarmPlot farmPlot))
                {
                    farmPlot.CropChanging += OnCropChanging;
                    farmPlot.CropChanged += OnCropChanged;
                }
                if (buildingObject.GetComponent<Processor>() != null ||
                    buildingObject.GetComponent<BasicMixer>() != null ||
                    buildingObject.GetComponent<Cutter>() != null ||
                    buildingObject.GetComponent<TradeBuilding>() != null ||
                    buildingObject.GetComponent<Harvester>() != null)
                    BuildingVisualFactory.CreateFeedbackView(buildingObject.transform,
                        gridSystem.CellSize);
                if (buildingObject.GetComponent<FarmPlot>() != null ||
                    buildingObject.GetComponent<Harvester>() != null ||
                    buildingObject.GetComponent<Processor>() != null ||
                    buildingObject.GetComponent<BasicMixer>() != null ||
                    buildingObject.GetComponent<Cutter>() != null ||
                    buildingObject.GetComponent<TradeBuilding>() != null)
                    buildingObject.AddComponent<MachineVisualAnimator>().Initialize(
                        visual, gridSystem.CellSize, processorTransportCoordinator);
                return instance;
            }
            catch
            {
                GetMoveState(buildingObject)?.DetachForMove();
                Destroy(buildingObject);
                throw;
            }
        }

        private bool CanSatisfyPlacementBehavior(
            BuildingPlacementOption option,
            Vector2Int anchorCell,
            BuildingRotation rotation)
        {
            IBuildingPlacementBehavior behavior = option.PlacementBehavior;
            return behavior == null || behavior.CanPlace(
                anchorCell,
                option.Definition.Footprint,
                rotation);
        }

        private bool CanPlaceBuilding(
            BuildingPlacementOption option,
            Vector2Int anchorCell,
            BuildingRotation rotation)
        {
            BuildingDefinition definition = option.Definition;
            if (option.PlacementBehavior is HarvesterPlacementBehavior)
            {
                return CanSatisfyPlacementBehavior(option, anchorCell, rotation) &&
                    TryGetHarvesterFarmPlacement(option, anchorCell, rotation,
                        out Vector2Int farmCell, out BuildingPlacement farmPlacement) &&
                    occupancy.CanPlaceOver(anchorCell, definition.Footprint, rotation,
                        farmCell, farmPlacement);
            }

            return occupancy.CanPlace(definition, anchorCell, rotation) &&
                CanSatisfyPlacementBehavior(option, anchorCell, rotation);
        }

        private bool TryGetHarvesterFarmPlacement(
            BuildingPlacementOption option,
            Vector2Int anchorCell,
            BuildingRotation rotation,
            out Vector2Int farmCell,
            out BuildingPlacement farmPlacement)
        {
            farmCell = HarvesterPlacementBehavior.GetFarmCell(
                anchorCell, option.Definition.Footprint, rotation);
            farmPlacement = null;
            return FarmPlot.GetAt(farmCell) != null &&
                occupancy.TryGetUnderlyingBuilding(farmCell, out farmPlacement) &&
                farmPlacement.DefinitionId == nameof(FarmPlot);
        }

        private BuildingRotation GetPreviewRotation(
            BuildingPlacementOption option,
            Vector2Int anchorCell)
        {
            if (!option.SupportsContinuousPlacement)
            {
                return selectedRotation;
            }

            if (placementDrag.LastCell.HasValue &&
                BeltDragPlacementPlanner.TryGetPathRotation(
                    placementDrag.LastCell.Value,
                    anchorCell,
                    out BuildingRotation pathRotation))
            {
                return pathRotation;
            }

            return beltDragPlanner.HasPathRotation
                ? beltDragPlanner.GetPreviewRotation(selectedRotation)
                : beltRotationExplicit ? selectedRotation :
                    GetSmartSingleBeltRotation(anchorCell, selectedRotation);
        }

        private BuildingRotation GetSmartSingleBeltRotation(Vector2Int? cell,
            BuildingRotation fallback)
        {
            if (!cell.HasValue) return fallback;
            int incomingMask = 0;
            for (int index = 0; index < 4; index++)
            {
                GridDirection direction = (GridDirection)index;
                if (!occupancy.TryGetBuilding(cell.Value - direction.ToOffset(),
                        out BuildingPlacement neighbor) ||
                    !buildingInstances.TryGetValue(neighbor,
                        out PlacedBuilding instance) ||
                    !instance.TryGetComponent(out Belt belt) ||
                    (belt.OutputMask & BeltCell.Bit(direction)) == 0)
                    continue;
                incomingMask |= BeltCell.Bit(direction);
            }
            GridDirection chosen = BeltConnectionPlanner.PreferStraight(
                incomingMask, fallback.ToGridDirection());
            return (BuildingRotation)((int)chosen * 90);
        }

        private void SelectBuilding(int index)
        {
            if (index < 0 || index >= buildingOptions.Length || buildingOptions[index] == null)
            {
                return;
            }

            if (IsMachineLocked(buildingOptions[index]))
            {
                constructionMessage = $"{buildingOptions[index].Definition.Id} " +
                    $"requires {GetMachineUnlockRequirement(buildingOptions[index])}.";
                return;
            }

            constructionMessage = null;
            string buildingId = buildingOptions[index].Definition.Id;
            if (foodDemoControls && market?.Unlocks.MarkSeen(
                    UnlockKey.MachineCategory, buildingId) == true)
                ShowContextualGuidance(GetBuildingGuidance(buildingId));

            selectedBuildingIndex = index;
            beltRotationExplicit = false;
            if (foodDemoControls)
            {
                cropPickerPlot = null; tradePickerBuilding = null;
                ClosePanel();
                if (isGroupPasteModeActive) ExitGroupPasteMode();
                propertySupply?.ExitTool();
            }
            selectedRotation = GetRememberedRotation(buildingOptions[index]);
            isPlacementModeActive = true;
            beltDragPlanner.Reset();
            placementDrag.Reset();
        }

        private bool HandleDemoEscape()
        {
            if (recipeDiscoveryPanel?.HasModal == true)
            {
                recipeDiscoveryPanel.DismissModal();
                return true;
            }
            if (activeCompletionCard != null)
            {
                ContinueAfterOrder();
                return true;
            }
            if (loadConfirmationOpen || quitConfirmationOpen)
            {
                loadConfirmationOpen = quitConfirmationOpen = false;
                return true;
            }
            DemoEscapeAction action = DemoEscapePriority.Choose(
                recipeDiscoveryPanel?.HasModal == true, systemMenuOpen,
                demoPanel != DemoPanel.None || cropPickerPlot != null || tradePickerBuilding != null,
                isGroupPasteModeActive ||
                isPlacementModeActive || propertySupply?.IsActive == true,
                selectionStartCell.HasValue || selection.SelectedPlacements.Count > 0);
            switch (action)
            {
                case DemoEscapeAction.DismissModal:
                    recipeDiscoveryPanel.DismissModal();
                    break;
                case DemoEscapeAction.CloseSystem:
                    systemMenuOpen = false;
                    break;
                case DemoEscapeAction.ClosePanel:
                    if (tradePickerBuilding != null) tradePickerBuilding = null;
                    else if (cropPickerPlot != null) cropPickerPlot = null;
                    else ClosePanel();
                    break;
                case DemoEscapeAction.CancelTool:
                    if (isGroupPasteModeActive) ExitGroupPasteMode();
                    else CancelDemoTool();
                    break;
                case DemoEscapeAction.ClearSelection:
                    selectionStartCell = null;
                    selection.Clear();
                    RefreshSelectionHighlights();
                    HideSelectionArea();
                    break;
                default:
                    systemMenuOpen = true;
                    break;
            }
            return true;
        }

        private void CancelDemoTool()
        {
            RecordConstruction(placementHistoryStart);
            placementHistoryStart = null;
            RecordConstruction(removalHistoryStart);
            removalHistoryStart = null;
            cropPickerPlot = null; tradePickerBuilding = null;
            isPlacementModeActive = false;
            propertySupply?.ExitTool();
            placementPreview.Hide();
            placementDrag.Reset();
            removalDrag.Reset();
            beltDragPlanner.Reset();
        }

        private void HandleDemoHotbarShortcuts()
        {
            if (Keyboard.current.ctrlKey.isPressed || isGroupPasteModeActive) return;
            int slot = Keyboard.current.digit1Key.wasPressedThisFrame ? 0 :
                Keyboard.current.digit2Key.wasPressedThisFrame ? 1 :
                Keyboard.current.digit3Key.wasPressedThisFrame ? 2 :
                Keyboard.current.digit4Key.wasPressedThisFrame ? 3 :
                Keyboard.current.digit5Key.wasPressedThisFrame ? 4 :
                Keyboard.current.digit6Key.wasPressedThisFrame ? 5 :
                Keyboard.current.digit7Key.wasPressedThisFrame ? 6 : -1;
            if (slot >= 0) SelectBuilding(FindBuildingOption(HotbarBuildingIds[slot]));
        }

        private Vector2Int? GetFeedbackTraceCell(BuildingPlacement placement,
            PlacedBuilding building, MachineFeedbackPort ports)
        {
            Vector2Int anchor = placement.AnchorCell;
            BuildingRotation rotation = placement.Rotation;
            if (building.TryGetComponent(out Processor processor))
            {
                if ((ports & MachineFeedbackPort.OutputA) != 0) return processor.OutputCell;
                if ((ports & MachineFeedbackPort.Property) != 0)
                    return ProcessorPortLayout.GetPropertyOutsideCell(anchor, rotation);
                if ((ports & MachineFeedbackPort.InputA) != 0)
                    return ProcessorPortLayout.GetFoodInputOutsideCell(anchor, rotation);
            }
            if (building.TryGetComponent(out BasicMixer mixer))
            {
                if ((ports & MachineFeedbackPort.OutputA) != 0) return mixer.OutputCell;
                if ((ports & MachineFeedbackPort.Combination) != 0)
                {
                    Vector2Int first = BasicMixerPortLayout.GetInputOutsideCell(
                        anchor, rotation, 0);
                    return occupancy.TryGetBuilding(first, out _) ? first :
                        BasicMixerPortLayout.GetInputOutsideCell(anchor, rotation, 1);
                }
                if ((ports & MachineFeedbackPort.InputA) != 0)
                    return BasicMixerPortLayout.GetInputOutsideCell(anchor, rotation, 0);
                if ((ports & MachineFeedbackPort.InputB) != 0)
                    return BasicMixerPortLayout.GetInputOutsideCell(anchor, rotation, 1);
            }
            if (building.TryGetComponent(out Cutter cutter))
            {
                if ((ports & MachineFeedbackPort.OutputA) != 0) return cutter.OutputACell;
                if ((ports & MachineFeedbackPort.OutputB) != 0) return cutter.OutputBCell;
                if ((ports & MachineFeedbackPort.InputA) != 0)
                    return cutter.InputCell + CutterPortLayout.InputFacing(rotation).ToOffset();
            }
            if (building.TryGetComponent(out Harvester harvester))
            {
                if ((ports & MachineFeedbackPort.OutputA) != 0) return harvester.OutputCell;
                if ((ports & MachineFeedbackPort.Crop) != 0) return harvester.FarmCell;
            }
            return null;
        }

        private int FindBuildingOption(string id)
        {
            for (int index = 0; index < buildingOptions.Length; index++)
                if (buildingOptions[index]?.Definition?.Id == id) return index;
            return -1;
        }

        public bool CanBuildRecipeMachine(DiscoveredRecipeKind kind,
            out string requirement)
        {
            string id = kind switch
            {
                DiscoveredRecipeKind.Processing => nameof(Processor),
                DiscoveredRecipeKind.Mixing => nameof(BasicMixer),
                DiscoveredRecipeKind.Cutting => nameof(Cutter),
                _ => null
            };
            int index = id == null ? -1 : FindBuildingOption(id);
            if (index < 0 || buildingOptions[index]?.Definition == null)
            {
                requirement = "Machine unavailable in this scene.";
                return false;
            }
            if (IsMachineLocked(buildingOptions[index]))
            {
                requirement = GetMachineUnlockRequirement(buildingOptions[index]);
                return false;
            }
            requirement = null;
            return true;
        }

        public bool SelectRecipeMachine(DiscoveredRecipeKind kind)
        {
            if (!CanBuildRecipeMachine(kind, out _)) return false;
            string id = kind switch
            {
                DiscoveredRecipeKind.Processing => nameof(Processor),
                DiscoveredRecipeKind.Mixing => nameof(BasicMixer),
                _ => nameof(Cutter)
            };
            SelectBuilding(FindBuildingOption(id));
            return true;
        }

        private Rect HotbarRect => new(
            Mathf.Max(8f, (Screen.width - Mathf.Min(424f, Screen.width - 16f)) * 0.5f),
            Mathf.Max(8f, Screen.height - 56f),
            Mathf.Min(424f, Screen.width - 16f), 44f);
        private Rect UtilityRect => new(
            Mathf.Max(8f, Screen.width - 156f),
            Mathf.Max(8f, Screen.height - 106f), 148f, 42f);
        private Rect DemoPanelRect
        {
            get
            {
                float height = Mathf.Max(80f,
                    Mathf.Min(300f, Screen.height - 164f));
                return new Rect(
                    Mathf.Max(8f, (Screen.width - Mathf.Min(460f, Screen.width - 16f)) * 0.5f),
                    Mathf.Max(8f, Screen.height - height - 114f),
                    Mathf.Min(460f, Screen.width - 16f), height);
            }
        }
        private Rect SystemRect => new(
            (Screen.width - Mathf.Min(280f, Screen.width - 16f)) * 0.5f,
            (Screen.height - Mathf.Min(300f, Screen.height - 16f)) * 0.5f,
            Mathf.Min(280f, Screen.width - 16f),
            Mathf.Min(300f, Screen.height - 16f));
        private Vector2 GetGuiPointer()
        {
            Vector2 pointer = Mouse.current?.position.ReadValue() ?? Vector2.zero;
            pointer.y = Screen.height - pointer.y;
            return pointer;
        }

        private Rect CropPickerRect
        {
            get
            {
                if (cropPickerPlot == null || Camera.main == null) return Rect.zero;
                Vector3 screen = Camera.main.WorldToScreenPoint(
                    cropPickerPlot.transform.position);
                float height = 42f + cropPickerPlot.AvailableCrops.Count * 27f;
                return new Rect(
                    Mathf.Clamp(screen.x + 18f, 8f,
                        Mathf.Max(8f, Screen.width - 184f)),
                    Mathf.Clamp(Screen.height - screen.y - height * 0.5f, 8f,
                        Mathf.Max(8f, Screen.height - height - 8f)),
                    176f, height);
            }
        }

        private void OpenCropPicker(FarmPlot plot)
        {
            tradePickerBuilding = null;
            ClosePanel();
            CropDefinition remembered = plot.AvailableCrops.FirstOrDefault(crop =>
                crop != null && crop.Id == rememberedFarmCropId &&
                plot.IsCropUnlocked(crop));
            CropDefinition defaultCrop = remembered ??
                plot.AvailableCrops.FirstOrDefault(plot.IsCropUnlocked);
            if (defaultCrop != null) plot.SelectCrop(defaultCrop);
            cropPickerPlot = plot;
        }

        private Rect TradePickerRect
        {
            get
            {
                Vector3 screen = UnityEngine.Camera.main.WorldToScreenPoint(tradePickerBuilding.transform.position);
                float height = 42f + tradePickerBuilding.Process.Recipes.Count * 27f +
                    (tradePickerBuilding.Process.CanChangeTrade ? 0f : 44f);
                return new Rect(Mathf.Clamp(screen.x + 18f, 8f, Mathf.Max(8f, Screen.width - 184f)),
                    Mathf.Clamp(Screen.height - screen.y - height * .5f, 8f,
                        Mathf.Max(8f, Screen.height - height - 8f)), 176f, height);
            }
        }
        private void DrawTradePicker()
        {
            if (tradePickerBuilding == null) return;
            var process = tradePickerBuilding.Process;
            Rect rect = TradePickerRect;
            GUI.Box(rect, GUIContent.none);
            GUI.Label(new Rect(rect.x + 8f, rect.y + 5f, rect.width - 16f, 22f), "Choose Trade");
            for (int i = 0; i < process.Recipes.Count; i++)
            {
                TradeRecipe recipe = process.Recipes[i];
                bool enabled = GUI.enabled;
                GUI.enabled = enabled && process.CanChangeTrade;
                if (GUI.Button(new Rect(rect.x + 8f, rect.y + 30f + i * 27f,
                    rect.width - 16f, 24f), new GUIContent(recipe.Output.Id, recipe.Label)))
                {
                    ConstructionLayout before = CaptureConstructionLayout();
                    process.Select(recipe.Id);
                    RecordConstruction(before);
                    tradePickerBuilding = null;
                    GUI.enabled = enabled;
                    return;
                }
                GUI.enabled = enabled;
            }
            if (!process.CanChangeTrade)
                GUI.Label(new Rect(rect.x + 8f, rect.y + 30f + process.Recipes.Count * 27f,
                    rect.width - 16f, 44f), "Drain items first.\n" +
                    $"In: {process.BufferedInput}  Out: {process.PendingOutput}");
            if (!string.IsNullOrEmpty(GUI.tooltip))
                GUI.Box(new Rect(rect.x, rect.yMax + 4f, 250f, 26f), GUI.tooltip);
        }

        private void DrawCropPicker()
        {
            if (cropPickerPlot == null) return;
            Rect rect = CropPickerRect;
            GUI.Box(rect, GUIContent.none);
            GUI.Label(new Rect(rect.x + 8f, rect.y + 5f, rect.width - 16f, 22f),
                "Choose Crop");
            for (int index = 0; index < cropPickerPlot.AvailableCrops.Count; index++)
            {
                CropDefinition crop = cropPickerPlot.AvailableCrops[index];
                if (crop == null) continue;
                bool wasEnabled = GUI.enabled;
                GUI.enabled = wasEnabled && cropPickerPlot.IsCropUnlocked(crop);
                string cropLabel = crop.Id +
                    (market?.Unlocks.IsNew(UnlockKey.CropCategory, crop.Id) == true
                        ? "  NEW" : "");
                if (GUI.Button(new Rect(rect.x + 8f, rect.y + 30f + index * 27f,
                        rect.width - 16f, 24f), cropLabel))
                {
                    cropPickerPlot.SelectCrop(crop);
                    rememberedFarmCropId = crop.Id;
                    if (market?.Unlocks.MarkSeen(UnlockKey.CropCategory, crop.Id) == true)
                        ShowContextualGuidance($"Grow {crop.Id} in a Farm Plot, then collect it with a Harvester.");
                    cropPickerPlot = null; tradePickerBuilding = null;
                    GUI.enabled = wasEnabled;
                    break;
                }
                GUI.enabled = wasEnabled;
            }
        }

        private bool IsPointerOverDemoHud()
        {
            if (!foodDemoControls || Mouse.current == null) return false;
            return systemMenuOpen ||
                issueTracker.Issues.Count > 0 && IssuesButtonRect.Contains(GetGuiPointer()) ||
                selection.SelectedPlacements.Count > 0 &&
                    SaveBlueprintButtonRect.Contains(GetGuiPointer()) ||
                recipeShortcutKind.HasValue &&
                    recipeShortcutRect.Contains(GetGuiPointer()) ||
                IsPointerOverForegroundPanel() ||
                HotbarRect.Contains(GetGuiPointer()) ||
                UtilityRect.Contains(GetGuiPointer());
        }

        private bool IsPointerOverForegroundPanel()
        {
            if (!foodDemoControls || Mouse.current == null) return false;
            Vector2 pointer = GetGuiPointer();
            return tradePickerBuilding != null && TradePickerRect.Contains(pointer) ||
                cropPickerPlot != null && CropPickerRect.Contains(pointer) ||
                (demoPanel is DemoPanel.Build or DemoPanel.Help or DemoPanel.Issues) &&
                    DemoPanelRect.Contains(pointer) ||
                demoPanel == DemoPanel.Recipe &&
                    recipeDiscoveryPanel?.BlocksWorldInput == true ||
                demoPanel == DemoPanel.Market &&
                    marketPanel?.IsPointerOverPanel == true ||
                demoPanel == DemoPanel.Machine &&
                    engraverUpgradePanel?.IsPointerOverPanel == true ||
                demoPanel == DemoPanel.Property &&
                    propertySupply?.IsPointerOverPanel() == true ||
                demoPanel == DemoPanel.Region &&
                    marketPanel?.IsPointerOverRegionPanel == true;
        }

        private void DrawDemoHud()
        {
            if (market == null) return;
            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && !BlocksAllWorldInput &&
                !IsPointerOverForegroundPanel();
            GUI.Label(new Rect(12f, 8f, 180f, 22f), $"$ {market.Currency}");
            GUI.Label(new Rect(12f, 28f, 180f, 22f), secondaryResourceText);
            Vector2Int regionCell = Camera.main != null
                ? gridSystem.WorldToGrid(Camera.main.transform.position)
                : hoverHighlight.HoveredCell;
            FarmableRegion region = market.Regions?.GetRegionAt(regionCell);
            GUI.Label(new Rect(12f, 48f, 180f, 22f),
                region?.DisplayName ?? market.Regions?.Regions.FirstOrDefault()?.DisplayName ?? "");
            if (selection.SelectedPlacements.Count > 0)
            {
                GUI.Box(new Rect(12f, 72f, 144f, 25f),
                    $"{selection.SelectedPlacements.Count} selected  |  M Move");
                if (GUI.Button(SaveBlueprintButtonRect, "Save Blueprint"))
                    OpenBlueprintLibraryForSave();
            }
            if (issueTracker.Issues.Count > 0 &&
                GUI.Button(IssuesButtonRect,
                    $"Factory Issues: {issueTracker.Issues.Count}"))
                OpenPanel(DemoPanel.Issues);

            Rect hotbar = HotbarRect;
            float slotWidth = (hotbar.width - 48f) / HotbarBuildingIds.Length;
            string hoveredName = null;
            Vector2 hotbarPointer = Mouse.current?.position.ReadValue() ?? Vector2.zero;
            hotbarPointer.y = Screen.height - hotbarPointer.y;
            for (int slot = 0; slot < HotbarBuildingIds.Length; slot++)
            {
                int optionIndex = FindBuildingOption(HotbarBuildingIds[slot]);
                BuildingPlacementOption option = optionIndex >= 0
                    ? buildingOptions[optionIndex] : null;
                if (option?.Definition == null) continue;
                Rect cell = new(hotbar.x + slot * slotWidth, hotbar.y,
                    slotWidth - 2f, hotbar.height);
                if (cell.Contains(hotbarPointer)) hoveredName = option.Definition.Id;
                bool slotEnabled = GUI.enabled;
                GUI.enabled = slotEnabled && !IsMachineLocked(option);
                if (GUI.Button(cell, new GUIContent("", option.Definition.Id)))
                    SelectBuilding(optionIndex);
                GUI.enabled = slotEnabled;
                GUI.Label(new Rect(cell.x + 3f, cell.y + 2f, 18f, 18f),
                    (slot + 1).ToString());
                Color originalColor = GUI.backgroundColor;
                GUI.backgroundColor = option.Definition.PlacedColor;
                GUI.Box(new Rect(cell.center.x - 13f, cell.y + 17f, 26f, 22f),
                    option.Definition.Id.Substring(0, 2).ToUpperInvariant());
                GUI.backgroundColor = originalColor;
                if (market.Unlocks.IsNew(UnlockKey.MachineCategory,
                        option.Definition.Id))
                    GUI.Label(new Rect(cell.x + 17f, cell.y, cell.width - 18f, 18f),
                        "NEW");
                if (isPlacementModeActive && selectedBuildingIndex == optionIndex)
                    GUI.Box(new Rect(cell.x, cell.y, cell.width, 3f), GUIContent.none);
            }
            if (GUI.Button(new Rect(hotbar.xMax - 46f, hotbar.y, 44f, hotbar.height), "Build"))
                OpenPanel(DemoPanel.Build);
            if (!string.IsNullOrEmpty(hoveredName))
                GUI.Label(new Rect(hotbar.x, hotbar.y - 62f, hotbar.width, 20f),
                    hoveredName);
            if (isPlacementModeActive)
            {
                string name = GetSelectedOption()?.Definition?.Id ?? "Build";
                GUI.Label(new Rect(hotbar.x, hotbar.y - 42f, hotbar.width, 40f),
                    $"{name}  |  {(isPlacementModeActive ? "R Rotate  ·  " : "")}Esc / Right Click Cancel");
            }
            else if (!string.IsNullOrEmpty(constructionMessage))
                GUI.Label(new Rect(hotbar.x, hotbar.y - 42f, hotbar.width, 40f),
                    constructionMessage);
            if (Time.time < guidanceUntil && !string.IsNullOrEmpty(contextualGuidance))
                GUI.Box(new Rect(hotbar.x, hotbar.y - 96f, hotbar.width, 48f),
                    contextualGuidance);
            if (recipeShortcutKind.HasValue && !BlocksAllWorldInput)
                GUI.Box(recipeShortcutRect, "Recipes");

            Rect utility = UtilityRect;
            if (GUI.Button(new Rect(utility.x, utility.y, 46f, 38f), "Recipe"))
            {
                if (recipeDiscoveryPanel != null) recipeDiscoveryPanel.OpenAll();
                else OpenPanel(DemoPanel.Recipe);
            }
            if (GUI.Button(new Rect(utility.x + 49f, utility.y, 46f, 38f), "Help"))
                OpenPanel(DemoPanel.Help);
            if (GUI.Button(new Rect(utility.x + 98f, utility.y, 46f, 38f), "System"))
            {
                cropPickerPlot = null; tradePickerBuilding = null;
                ClosePanel();
                systemMenuOpen = true;
            }

            GUI.enabled = previousEnabled && !BlocksAllWorldInput;
            if (demoPanel == DemoPanel.Build) DrawBuildMenu();
            if (demoPanel == DemoPanel.Help) DrawHelpPanel();
            if (demoPanel == DemoPanel.Issues) DrawIssuesPanel();
            GUI.enabled = previousEnabled;
            if (systemMenuOpen)
            {
                GUI.enabled = previousEnabled && recipeDiscoveryPanel?.HasModal != true &&
                    activeCompletionCard == null;
                DrawSystemMenu();
            }
            if (activeCompletionCard != null && recipeDiscoveryPanel?.HasModal != true)
            {
                GUI.enabled = previousEnabled;
                DrawOrderCompletionCard();
            }
            if (devInspectorOpen)
                GUI.Box(new Rect(Mathf.Max(8f, Screen.width - 224f), 8f, 216f, 116f),
                    $"DEV INSPECTOR\nGrid: {hoverHighlight.HoveredCell}\nTool: " +
                    (isPlacementModeActive ?
                        GetSelectedOption()?.Definition?.Id : "None"));
            if (!systemMenuOpen)
            {
                GUI.enabled = previousEnabled && !BlocksAllWorldInput;
                DrawCropPicker();
                DrawTradePicker();
            }
            GUI.enabled = previousEnabled;
        }

        private string GetHoveredMachineDebug()
        {
            if (!occupancy.TryGetBuilding(hoverHighlight.HoveredCell,
                    out BuildingPlacement placement) ||
                !buildingInstances.TryGetValue(placement, out PlacedBuilding building))
                return "Machine: none";
            if (building.TryGetComponent(out Processor processor))
                return $"Processor: {processor.State}\n{processor.ProcessingStateMessage}";
            if (building.TryGetComponent(out BasicMixer mixer))
                return $"Mixer A: {mixer.SlotA?.Id ?? "empty"}\n" +
                    $"Mixer B: {mixer.SlotB?.Id ?? "empty"}";
            if (building.TryGetComponent(out Cutter cutter))
                return $"Cutter: {cutter.State}";
            if (building.TryGetComponent(out Harvester harvester))
                return $"Harvester buffer: {harvester.OutputCount}/{harvester.OutputCapacity}";
            return "Machine: none";
        }

        private void DrawBuildMenu()
        {
            Rect rect = DemoPanelRect;
            GUI.Box(rect, GUIContent.none);
            GUILayout.BeginArea(new Rect(rect.x + 10f, rect.y + 8f,
                rect.width - 20f, rect.height - 16f));
            GUILayout.Label("Build Menu");
            if (blueprintLibrary != null && GUILayout.Button(
                    blueprintLibraryOpen ? "Buildings" : "Blueprint Library"))
            {
                blueprintLibraryOpen = !blueprintLibraryOpen;
                blueprintNameFocused = false;
            }
            if (blueprintLibraryOpen)
            {
                DrawBlueprintLibrary();
                GUILayout.EndArea();
                return;
            }
            GUILayout.BeginHorizontal();
            string[] categories = { "Farming", "Logistics", "Production", "Utility" };
            for (int category = 0; category < categories.Length; category++)
                if (GUILayout.Button(categories[category])) constructionCategory = category;
            GUILayout.EndHorizontal();
            buildMenuScroll = GUILayout.BeginScrollView(buildMenuScroll);
            for (int index = 0; index < buildingOptions.Length; index++)
            {
                BuildingPlacementOption option = buildingOptions[index];
                if (option?.Definition == null ||
                    GetConstructionCategory(option.Definition.Id) != constructionCategory) continue;
                bool locked = IsMachineLocked(option);
                bool enabled = GUI.enabled;
                GUI.enabled = enabled && !locked;
                string label = option.Definition.Id +
                    (market.Unlocks.IsNew(UnlockKey.MachineCategory,
                        option.Definition.Id) ? "  NEW" : "");
                if (GUILayout.Button(label)) SelectBuilding(index);
                GUI.enabled = enabled;
                if (locked) GUILayout.Label($"Requires {GetMachineUnlockRequirement(option)}");
            }
            if (constructionCategory == 3 && GUILayout.Button("Property connections"))
                OpenPanel(DemoPanel.Property);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private Rect IssuesButtonRect => new(12f,
            selection.SelectedPlacements.Count > 0 ? 102f : 74f, 160f, 28f);

        private Rect SaveBlueprintButtonRect => new(160f, 72f, 112f, 25f);

        private void OpenBlueprintLibraryForSave()
        {
            if (blueprintLibrary == null) return;
            blueprintLibraryOpen = true;
            if (demoPanel != DemoPanel.Build) OpenPanel(DemoPanel.Build);
            OpenBlueprintSaveForm();
        }

        private void OpenBlueprintSaveForm()
        {
            constructionMessage = null;
            if (!TryCaptureSelection(out pendingBlueprintGroup))
            {
                if (string.IsNullOrEmpty(constructionMessage))
                    constructionMessage = "Select buildings before saving a Blueprint.";
                return;
            }
            blueprintSaveFormOpen = true;
            constructionMessage = null;
        }

        private void SavePendingBlueprint()
        {
            if (pendingBlueprintGroup == null)
            {
                constructionMessage = "Select buildings before saving a Blueprint.";
                return;
            }
            if (!blueprintLibrary.TryAdd(blueprintName, pendingBlueprintGroup,
                    out string error))
            {
                constructionMessage = error;
                return;
            }
            selectedBlueprintId = blueprintLibrary.Records.Last().id;
            blueprintSaveFormOpen = false;
            pendingBlueprintGroup = null;
            blueprintNameFocused = false;
            GUI.FocusControl(null);
            constructionMessage = null;
            toasts.Enqueue($"Blueprint saved: {blueprintName.Trim()}", Time.time);
        }

        private void DrawIssuesPanel()
        {
            Rect rect = DemoPanelRect;
            GUI.Box(rect, GUIContent.none);
            GUILayout.BeginArea(new Rect(rect.x + 10f, rect.y + 8f,
                rect.width - 20f, rect.height - 16f));
            GUILayout.Label($"Factory Issues ({issueTracker.Issues.Count})");
            if (GUILayout.Button("Close")) ClosePanel();
            buildMenuScroll = GUILayout.BeginScrollView(buildMenuScroll);
            foreach (FactoryIssue<BuildingPlacement> issue in issueTracker.Issues)
            {
                if (!buildingInstances.ContainsKey(issue.Machine)) continue;
                GUILayout.Label($"{issue.Machine.DefinitionId}  |  " +
                    $"{issue.Feedback.Problem}");
                GUILayout.Label($"Port: {IssuePortName(issue.Feedback.Ports)}");
                GUILayout.Label(issue.Feedback.Action);
                if (GUILayout.Button("Locate and diagnose"))
                {
                    FocusFactoryIssue(issue.Machine);
                    break;
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private static string IssuePortName(MachineFeedbackPort ports) =>
            ports switch
            {
                MachineFeedbackPort.OutputA => "Output",
                MachineFeedbackPort.OutputB => "Output B",
                MachineFeedbackPort.OutputA | MachineFeedbackPort.OutputB =>
                    "Both outputs",
                MachineFeedbackPort.InputA => "Food input",
                MachineFeedbackPort.InputB => "Food input B",
                MachineFeedbackPort.Combination => "Ingredient combination",
                MachineFeedbackPort.Property => "Property input",
                MachineFeedbackPort.Crop => "Farm Plot crop",
                _ => "Affected ports"
            };

        private void FocusFactoryIssue(BuildingPlacement placement)
        {
            if (!buildingInstances.TryGetValue(placement, out PlacedBuilding building) ||
                !feedbackViews.TryGetValue(placement, out MachineFeedbackView view))
                return;
            ClosePanel();
            if (factoryCamera == null && Camera.main != null)
                factoryCamera = Camera.main.GetComponent<FactoryCameraController>();
            Vector2Int? traceCell = GetFeedbackTraceCell(placement,
                building, view.Feedback.Ports);
            Vector3? tracePosition = traceCell.HasValue &&
                occupancy.TryGetBuilding(traceCell.Value,
                    out BuildingPlacement connected) && connected != placement
                ? gridSystem.GridToWorld(traceCell.Value) : null;
            view.Emphasize(tracePosition);
            factoryCamera?.JumpTo(building.transform.position, () =>
            {
                if (view != null) view.Emphasize(tracePosition);
            });
        }

        private void DrawBlueprintLibrary()
        {
            if (blueprintNameFocused && Event.current.type == EventType.MouseDown &&
                !DemoPanelRect.Contains(Event.current.mousePosition))
            {
                GUI.FocusControl(null);
                blueprintNameFocused = false;
            }
            if (blueprintLibrary.LoadError != null)
                GUILayout.Label($"Library unavailable: {blueprintLibrary.LoadError}");
            int selectedBuildings = selection.SelectedPlacements.Count(
                buildingInstances.ContainsKey);
            GUILayout.Label($"Selected buildings: {selectedBuildings}");
            if (selectedBuildings == 0)
                GUILayout.Label("Select one or more buildings in the world to save a layout.");
            if (!blueprintSaveFormOpen && GUILayout.Button("Save Selection"))
                OpenBlueprintSaveForm();
            if (blueprintSaveFormOpen)
            {
                GUILayout.Label("Blueprint name");
                GUI.SetNextControlName(BlueprintNameControl);
                bool nameHasFocus = GUI.GetNameOfFocusedControl() ==
                    BlueprintNameControl;
                if (nameHasFocus && Event.current.type == EventType.KeyDown)
                {
                    if (Event.current.keyCode is KeyCode.Return or KeyCode.KeypadEnter)
                    {
                        SavePendingBlueprint();
                        Event.current.Use();
                    }
                    else if (Event.current.keyCode == KeyCode.Escape)
                    {
                        blueprintSaveFormOpen = false;
                        pendingBlueprintGroup = null;
                        blueprintNameFocused = false;
                        GUI.FocusControl(null);
                        Event.current.Use();
                    }
                }
                blueprintName = GUILayout.TextField(blueprintName, 64);
                blueprintNameFocused = GUI.GetNameOfFocusedControl() ==
                    BlueprintNameControl;
                if (GUILayout.Button("Save Blueprint"))
                    SavePendingBlueprint();
                if (GUILayout.Button("Cancel Save"))
                {
                    blueprintSaveFormOpen = false;
                    pendingBlueprintGroup = null;
                    blueprintNameFocused = false;
                    GUI.FocusControl(null);
                }
            }
            if (!string.IsNullOrEmpty(constructionMessage))
                GUILayout.Label(constructionMessage);
            GUILayout.Label("Saved Blueprints");
            if (blueprintLibrary.Records.Count == 0)
                GUILayout.Label("No Blueprints yet. Select buildings, click Save Selection, name the layout, and save it here.");
            buildMenuScroll = GUILayout.BeginScrollView(buildMenuScroll);
            foreach (BlueprintRecord record in blueprintLibrary.Records)
            {
                if (GUILayout.Button($"{record.name}  ({record.PartCount} parts)"))
                {
                    selectedBlueprintId = record.id;
                    blueprintName = record.name;
                }
            }
            BlueprintRecord selected = blueprintLibrary.Records.FirstOrDefault(
                item => item.id == selectedBlueprintId);
            if (selected != null)
            {
                GUILayout.Label($"{selected.name}: {selected.PartCount} parts");
                if (!blueprintSaveFormOpen)
                {
                    GUILayout.Label("Rename selected Blueprint");
                    GUI.SetNextControlName(BlueprintNameControl);
                    bool renameHasFocus = GUI.GetNameOfFocusedControl() ==
                        BlueprintNameControl;
                    if (renameHasFocus && Event.current.type == EventType.KeyDown)
                    {
                        if (Event.current.keyCode is KeyCode.Return or KeyCode.KeypadEnter)
                        {
                            if (blueprintLibrary.TryRename(selected.id, blueprintName,
                                    out string renameError))
                            {
                                constructionMessage = null;
                                toasts.Enqueue("Blueprint renamed", Time.time);
                            }
                            else constructionMessage = renameError;
                            Event.current.Use();
                        }
                        else if (Event.current.keyCode == KeyCode.Escape)
                        {
                            blueprintNameFocused = false;
                            GUI.FocusControl(null);
                            Event.current.Use();
                        }
                    }
                    blueprintName = GUILayout.TextField(blueprintName, 64);
                    blueprintNameFocused = GUI.GetNameOfFocusedControl() ==
                        BlueprintNameControl;
                }
                if (BlueprintLibrary.TryResolve(selected, buildingOptions,
                        out BuildingGroupCopy group, out string reason))
                {
                    BuildingGroupCopyItem locked = group.Items.FirstOrDefault(item =>
                        IsMachineLocked(item.Option));
                    if (locked.Option != null)
                        GUILayout.Label($"{locked.Option.Definition.Id} requires " +
                            GetMachineUnlockRequirement(locked.Option));
                    else if (GUILayout.Button("Place Blueprint"))
                    {
                        ClosePanel();
                        EnterGroupPasteMode(group);
                        activeBlueprintName = selected.name;
                    }
                }
                else GUILayout.Label(reason);
                if (GUILayout.Button("Rename"))
                {
                    if (blueprintLibrary.TryRename(selected.id, blueprintName,
                            out string error)) toasts.Enqueue("Blueprint renamed", Time.time);
                    else constructionMessage = error;
                }
                if (GUILayout.Button("Duplicate"))
                {
                    if (blueprintLibrary.TryDuplicate(selected.id, out string error))
                        toasts.Enqueue("Blueprint duplicated", Time.time);
                    else constructionMessage = error;
                }
                if (GUILayout.Button("Delete"))
                {
                    if (blueprintLibrary.TryDelete(selected.id, out string error))
                    {
                        selectedBlueprintId = null;
                        toasts.Enqueue("Blueprint deleted", Time.time);
                    }
                    else constructionMessage = error;
                }
            }
            GUILayout.EndScrollView();
        }

        private void DrawHelpPanel()
        {
            Rect rect = DemoPanelRect;
            GUI.Box(rect, GUIContent.none);
            GUILayout.BeginArea(new Rect(rect.x + 10f, rect.y + 8f,
                rect.width - 20f, rect.height - 16f));
            helpScroll = GUILayout.BeginScrollView(helpScroll);
            GUILayout.Label("Help");
            GUILayout.Label("Choose a building from the hotbar or Build Menu.");
            GUILayout.Label("R rotates. Esc or Right Click cancels the current tool.");
            GUILayout.Label("Right Click removes a building; hold and drag to remove more.");
            GUILayout.Label("Click selects; Shift click toggles; Shift drag adds a rectangle.");
            GUILayout.Label("M moves selection. Ctrl+C copies, Ctrl+V pastes, Ctrl+X cuts.");
            GUILayout.Label("Ctrl+Z undoes construction; Ctrl+Y redoes it.");
            GUILayout.Label("Save selected layouts in the Build Menu Blueprint Library.");
            GUILayout.Label("Click Market to view the current order.");
            GUILayout.Label("NEW clears when you select that machine or crop.");
            GUILayout.Label("Select a new crop in a Farm Plot, then collect it with a Harvester.");
            GUILayout.Label(GetBuildingGuidance(nameof(Processor)));
            GUILayout.Label(GetBuildingGuidance(nameof(BasicMixer)));
            GUILayout.Label(GetBuildingGuidance(nameof(Cutter)));
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawSystemMenu()
        {
            Rect rect = SystemRect;
            GUI.Box(rect, GUIContent.none);
            GUILayout.BeginArea(new Rect(rect.x + 12f, rect.y + 10f,
                rect.width - 24f, rect.height - 20f));
            GUILayout.Label("SYSTEM");
            if (loadConfirmationOpen || quitConfirmationOpen)
            {
                GUILayout.Label(loadConfirmationOpen
                    ? "Load saved game? Unsaved progress may be lost."
                    : "Quit? Unsaved progress may be lost.");
                if (GUILayout.Button("Cancel"))
                    loadConfirmationOpen = quitConfirmationOpen = false;
                if (GUILayout.Button(loadConfirmationOpen ? "Confirm Load" : "Confirm Quit"))
                {
                    if (loadConfirmationOpen)
                    {
                        if (marketPanel?.LoadGame() == true) systemMenuOpen = false;
                    }
                    else Application.Quit();
                    loadConfirmationOpen = quitConfirmationOpen = false;
                }
            }
            else
            {
                if (GUILayout.Button("Resume")) systemMenuOpen = false;
                if (GUILayout.Button("Save Game") && marketPanel?.SaveGame() == true)
                    systemMenuOpen = false;
                if (GUILayout.Button("Load Game")) loadConfirmationOpen = true;
                GUILayout.Label("Settings: no configurable options yet.");
                if (GUILayout.Button("Quit")) quitConfirmationOpen = true;
                GUILayout.Label("SAVE DATA");
                GUILayout.Label(marketPanel?.GetSaveSummary() ?? "No save data available.");
            }
            if (!string.IsNullOrEmpty(marketPanel?.SaveMessage))
                GUILayout.Label(marketPanel.SaveMessage);
            GUILayout.EndArea();
        }

        private void RefreshRecipeShortcut()
        {
            if (BlocksAllWorldInput || isPlacementModeActive ||
                isGroupPasteModeActive || demoPanel != DemoPanel.None ||
                cropPickerPlot != null || Camera.main == null)
            {
                recipeShortcutKind = null;
                recipeShortcutPlacement = null;
                return;
            }
            if (recipeShortcutKind.HasValue &&
                recipeShortcutPlacement != null &&
                buildingInstances.ContainsKey(recipeShortcutPlacement))
            {
                if (occupancy.TryGetBuilding(hoverHighlight.HoveredCell,
                        out BuildingPlacement hovered) &&
                    hovered == recipeShortcutPlacement)
                    recipeShortcutKeepUntil = Time.time + 0.5f;
                if (recipeShortcutRect.Contains(GetGuiPointer()) ||
                    Time.time < recipeShortcutKeepUntil) return;
            }
            recipeShortcutKind = null;
            recipeShortcutPlacement = null;
            if (!occupancy.TryGetBuilding(hoverHighlight.HoveredCell,
                    out BuildingPlacement placement) ||
                !buildingInstances.TryGetValue(placement, out PlacedBuilding building))
                return;
            if (building.TryGetComponent(out Processor _))
                recipeShortcutKind = DiscoveredRecipeKind.Processing;
            else if (building.TryGetComponent(out BasicMixer _))
                recipeShortcutKind = DiscoveredRecipeKind.Mixing;
            else if (building.TryGetComponent(out Cutter _))
                recipeShortcutKind = DiscoveredRecipeKind.Cutting;
            if (!recipeShortcutKind.HasValue) return;
            Vector3 screen = Camera.main.WorldToScreenPoint(building.transform.position);
            if (screen.z <= 0f)
            {
                recipeShortcutKind = null;
                return;
            }
            recipeShortcutPlacement = placement;
            recipeShortcutKeepUntil = Time.time + 0.5f;
            recipeShortcutRect = new Rect(
                Mathf.Clamp(screen.x - 35f, 8f, Screen.width - 78f),
                Mathf.Clamp(Screen.height - screen.y + 24f, 8f,
                    Screen.height - 32f), 70f, 24f);
        }

        private void DrawOrderCompletionCard()
        {
            Rect rect = new((Screen.width - 360f) * 0.5f,
                (Screen.height - 250f) * 0.5f, 360f, 250f);
            GUI.Box(rect, GUIContent.none);
            GUILayout.BeginArea(new Rect(rect.x + 14f, rect.y + 12f,
                rect.width - 28f, rect.height - 24f));
            GUILayout.Label($"ORDER COMPLETE: {activeCompletionCard.Order.DisplayName}");
            foreach (FoodOrderRequirement requirement in activeCompletionCard.Order.Requirements)
                GUILayout.Label($"{requirement.Quantity} {requirement.Food.Id}");
            GUILayout.Label("Order bonus: 0 currency (delivery sales credited separately)");
            foreach (UnlockKey unlock in activeCompletionCard.GrantedUnlocks)
                GUILayout.Label($"Unlocked {unlock.Category}: {unlock.Id}");
            if (activeCompletionCard.GrantedUnlocks.Count == 0)
                GUILayout.Label("No new unlocks.");
            if (GUILayout.Button("Continue")) ContinueAfterOrder();
            GUILayout.EndArea();
        }

        private void ContinueAfterOrder() => activeCompletionCard =
            completionCards.Count > 0 ? completionCards.Dequeue() : null;

        private void ShowContextualGuidance(string message)
        {
            contextualGuidance = message;
            guidanceUntil = Time.time + 6f;
        }

        private static string GetBuildingGuidance(string id) => id switch
        {
            nameof(Processor) => "Processor: feed food from the west and pipe a property to the south port.",
            nameof(BasicMixer) => "Mixer: feed its two west inputs on separate belts and collect the east output.",
            nameof(Cutter) => "Cutter: feed its rear input and connect both output belts.",
            nameof(TradeBuilding) => "Trade Building: choose a trade, feed any left input, collect from any bottom output.",
            _ => $"Select {id}, choose a valid cell, and place it on the map."
        };

        private static int GetConstructionCategory(string id) => id switch
        {
            nameof(FarmPlot) or nameof(Harvester) => 0,
            nameof(Belt) => 1,
            nameof(Processor) or nameof(BasicMixer) or nameof(Cutter) => 2,
            _ => 2
        };

        private BuildingRotation GetRememberedRotation(BuildingPlacementOption option) =>
            rememberedRotations.Get(option?.Definition?.Id);

        private void RememberRotation(BuildingPlacementOption option,
            BuildingRotation rotation)
        {
            if (option?.Definition != null)
                rememberedRotations.Set(option.Definition.Id, rotation);
        }

        private string GetPlacementFailureReason(BuildingPlacementOption option,
            Vector2Int anchor, BuildingRotation rotation)
        {
            BuildingDefinition definition = option.Definition;
            if (foodDemoControls && definition.Id == nameof(FarmPlot))
            {
                FarmableRegion region = market?.Regions?.GetRegionAt(anchor);
                if (region == null) return "Place Farm Plots on farmable land.";
                if (market.Regions.GetStatus(region.Id) != RegionStatus.Restored)
                    return "Restore this region before placing a Farm Plot.";
            }
            if (option.PlacementBehavior is HarvesterPlacementBehavior)
            {
                if (!TryGetHarvesterFarmPlacement(option, anchor, rotation,
                        out Vector2Int farmCell, out BuildingPlacement farmPlacement))
                    return "Place a Farm Plot under the Harvester.";
                return occupancy.CanPlaceOver(anchor, definition.Footprint, rotation,
                    farmCell, farmPlacement) ? "Check the Harvester setup." :
                    "Clear the Harvester's output cell.";
            }
            if (!occupancy.CanPlace(definition, anchor, rotation))
                return "Move the preview off another building.";
            return "Check this building's required setup.";
        }

        private IReadOnlyList<bool?> GetPortConnectionFeedback(
            BuildingPlacementOption option, BuildingRotation rotation)
        {
            var feedback = new bool?[option.PortPreviews.Count];
            for (int index = 0; index < feedback.Length; index++)
            {
                BuildingPortPreview port = option.PortPreviews[index];
                Vector2Int outward = port.ResolveDirection(rotation)
                    .ToGridDirection().ToOffset();
                Vector3 local = new(port.LocalPosition.x * gridSystem.CellSize,
                    port.LocalPosition.y * gridSystem.CellSize, 0f);
                Vector2Int outside = gridSystem.WorldToGrid(
                    placementPreview.transform.TransformPoint(local) +
                    (Vector3)(Vector2)outward * gridSystem.CellSize * 0.6f);
                if (port.Kind == BuildingPortKind.PropertyInput)
                {
                    if (propertySupply?.HasPipeAt(outside) == true)
                        feedback[index] = true;
                    else if (occupancy.TryGetBuilding(outside, out _))
                        feedback[index] = false;
                    continue;
                }
                if (!occupancy.TryGetBuilding(outside,
                        out BuildingPlacement neighbor)) continue;
                if (neighbor.DefinitionId != nameof(Belt))
                {
                    feedback[index] = HasMatchingNeighborPort(neighbor, port,
                        local, outward);
                    continue;
                }
                int beltOutputs = buildingInstances.TryGetValue(neighbor,
                    out PlacedBuilding beltInstance) &&
                    beltInstance.TryGetComponent(out Belt connectedBelt)
                    ? connectedBelt.OutputMask
                    : BeltCell.Bit(neighbor.Rotation.ToGridDirection());
                feedback[index] = port.Kind == BuildingPortKind.Input
                    ? (beltOutputs & BeltCell.Bit(
                        DirectionForOffset(-outward))) != 0
                    : (beltOutputs & BeltCell.Bit(
                        DirectionForOffset(-outward))) == 0;
            }
            return feedback;
        }

        private bool HasMatchingNeighborPort(BuildingPlacement neighbor,
            BuildingPortPreview currentPort, Vector3 currentLocal,
            Vector2Int outward)
        {
            if (!buildingInstances.TryGetValue(neighbor, out PlacedBuilding instance))
                return false;
            BuildingPlacementOption option = buildingOptions.FirstOrDefault(candidate =>
                candidate?.Definition?.Id == neighbor.DefinitionId);
            if (option == null) return false;
            Vector2Int inside = gridSystem.WorldToGrid(
                placementPreview.transform.TransformPoint(currentLocal) -
                (Vector3)(Vector2)outward * gridSystem.CellSize * 0.6f);
            BuildingPortKind required = currentPort.Kind == BuildingPortKind.Input
                ? BuildingPortKind.Output : BuildingPortKind.Input;
            foreach (BuildingPortPreview port in option.PortPreviews)
            {
                if (port.Kind != required ||
                    port.ResolveDirection(neighbor.Rotation).ToGridDirection().ToOffset()
                    != -outward) continue;
                Vector3 local = new(port.LocalPosition.x * gridSystem.CellSize,
                    port.LocalPosition.y * gridSystem.CellSize, 0f);
                Vector2Int outside = gridSystem.WorldToGrid(
                    instance.transform.TransformPoint(local) +
                    (Vector3)(Vector2)(-outward) * gridSystem.CellSize * 0.6f);
                if (outside == inside) return true;
            }
            return false;
        }

        private bool? GetBeltConnectionFeedback(BuildingPlacementOption option,
            Vector2Int cell, BuildingRotation rotation)
        {
            if (option.Definition.Id != nameof(Belt)) return null;
            Vector2Int forward = rotation.ToGridDirection().ToOffset();
            if (!occupancy.TryGetBuilding(cell + forward,
                    out BuildingPlacement neighbor)) return null;
            if (neighbor.DefinitionId == nameof(Belt))
            {
                int outputs = buildingInstances.TryGetValue(neighbor,
                    out PlacedBuilding beltInstance) &&
                    beltInstance.TryGetComponent(out Belt belt)
                    ? belt.OutputMask
                    : BeltCell.Bit(neighbor.Rotation.ToGridDirection());
                return (outputs & BeltCell.Bit(
                    DirectionForOffset(-forward))) == 0;
            }
            if (!buildingInstances.TryGetValue(neighbor, out PlacedBuilding instance))
                return false;
            BuildingPlacementOption target = buildingOptions.FirstOrDefault(candidate =>
                candidate?.Definition?.Id == neighbor.DefinitionId);
            if (target == null) return false;
            foreach (BuildingPortPreview port in target.PortPreviews)
            {
                if (port.Kind != BuildingPortKind.Input ||
                    port.ResolveDirection(neighbor.Rotation).ToGridDirection().ToOffset()
                    != -forward) continue;
                Vector3 local = new(port.LocalPosition.x * gridSystem.CellSize,
                    port.LocalPosition.y * gridSystem.CellSize, 0f);
                Vector2Int outside = gridSystem.WorldToGrid(
                    instance.transform.TransformPoint(local) -
                    (Vector3)(Vector2)forward * gridSystem.CellSize * 0.6f);
                if (outside == cell) return true;
            }
            return false;
        }

        private static GridDirection DirectionForOffset(Vector2Int offset)
        {
            for (int index = 0; index < 4; index++)
            {
                GridDirection direction = (GridDirection)index;
                if (direction.ToOffset() == offset) return direction;
            }
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        private bool IsMachineLocked(BuildingPlacementOption option) =>
            foodDemoControls && option?.Definition != null &&
            option.Definition.Id is nameof(Processor) or nameof(BasicMixer) or
                nameof(Cutter) &&
            market?.Unlocks.IsUnlocked(UnlockKey.MachineCategory,
                option.Definition.Id) != true;

        private string GetMachineUnlockRequirement(BuildingPlacementOption option)
        {
            string id = option?.Definition?.Id;
            foreach (FoodOrder order in market?.Orders ?? Array.Empty<FoodOrder>())
                if (order.Unlocks.Any(unlock =>
                        unlock.Category == UnlockKey.MachineCategory && unlock.Id == id))
                    return order.DisplayName;
            return "Market progression";
        }

        private BuildingPlacementOption GetSelectedOption()
        {
            return selectedBuildingIndex >= 0 && selectedBuildingIndex < buildingOptions.Length
                ? buildingOptions[selectedBuildingIndex]
                : null;
        }

        private static bool CanRemove(GameObject buildingObject)
        {
            foreach (MonoBehaviour component in buildingObject.GetComponents<MonoBehaviour>())
            {
                if (component is IBuildingRemovalRule removalRule && !removalRule.CanRemove)
                {
                    return false;
                }
            }

            return true;
        }

        private bool CanRemovePlacement(BuildingPlacement placement)
        {
            if (buildingInstances.TryGetValue(placement, out PlacedBuilding instance))
                return CanRemove(instance.gameObject);
            return foodDemoControls &&
                propertySupply?.TryGetClipboardConnection(placement,
                    out PropertyConnection connection) == true &&
                connection.Kind is PropertyConnectionKind.Collector or
                    PropertyConnectionKind.Pipe;
        }

        private void OnValidate()
        {
            if (buildingOptions == null)
            {
                buildingOptions = Array.Empty<BuildingPlacementOption>();
                return;
            }

            foreach (BuildingPlacementOption option in buildingOptions)
            {
                option?.Validate();
            }
        }
    }

    public sealed class BuildingSelection
    {
        private readonly List<BuildingPlacement> selectedPlacements = new();
        private readonly HashSet<BuildingPlacement> selectedSet = new();

        public IReadOnlyList<BuildingPlacement> SelectedPlacements => selectedPlacements;

        public bool Contains(BuildingPlacement placement) => selectedSet.Contains(placement);

        public void SelectRectangle(
            GridOccupancy occupancy,
            Vector2Int firstCell,
            Vector2Int lastCell,
            Func<BuildingPlacement, bool> canSelect,
            bool includeUnderlying = false)
        {
            if (occupancy == null)
            {
                throw new ArgumentNullException(nameof(occupancy));
            }

            if (canSelect == null)
            {
                throw new ArgumentNullException(nameof(canSelect));
            }

            Clear();
            int minX = Math.Min(firstCell.x, lastCell.x);
            int maxX = Math.Max(firstCell.x, lastCell.x);
            int minY = Math.Min(firstCell.y, lastCell.y);
            int maxY = Math.Max(firstCell.y, lastCell.y);
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (occupancy.TryGetBuilding(
                            new Vector2Int(x, y), out BuildingPlacement placement) &&
                        !selectedSet.Contains(placement) && canSelect(placement))
                    {
                        selectedSet.Add(placement);
                        selectedPlacements.Add(placement);
                    }
                    if (includeUnderlying && occupancy.TryGetUnderlyingBuilding(
                            new Vector2Int(x, y), out BuildingPlacement underlying) &&
                        !selectedSet.Contains(underlying) && canSelect(underlying))
                    {
                        selectedSet.Add(underlying);
                        selectedPlacements.Add(underlying);
                    }
                }
            }
        }

        public bool Remove(BuildingPlacement placement)
        {
            return selectedSet.Remove(placement) && selectedPlacements.Remove(placement);
        }

        public bool Add(BuildingPlacement placement)
        {
            if (placement == null || !selectedSet.Add(placement)) return false;
            selectedPlacements.Add(placement);
            return true;
        }

        public void Toggle(BuildingPlacement placement)
        {
            if (!Remove(placement)) Add(placement);
        }

        public void Clear()
        {
            selectedSet.Clear();
            selectedPlacements.Clear();
        }
    }

    public readonly struct BuildingGroupCopyItem
    {
        public BuildingGroupCopyItem(
            BuildingPlacementOption option,
            Vector2Int cell,
            BuildingRotation rotation,
            string cropId = null,
            int outputMask = 0)
        {
            Option = option ?? throw new ArgumentNullException(nameof(option));
            Offset = cell;
            Rotation = rotation;
            CropId = cropId;
            OutputMask = outputMask;
        }

        public BuildingPlacementOption Option { get; }
        public Vector2Int Offset { get; }
        public BuildingRotation Rotation { get; }
        public string CropId { get; }
        public int OutputMask { get; }
    }

    public readonly struct PropertyGroupCopyItem
    {
        public PropertyGroupCopyItem(PropertyConnection connection, Vector2Int offset)
        {
            Connection = connection;
            Offset = offset;
        }

        public PropertyConnection Connection { get; }
        public Vector2Int Offset { get; }
    }

    public sealed class BuildingGroupCopy
    {
        private readonly BuildingGroupCopyItem[] items;
        private readonly PropertyGroupCopyItem[] propertyItems;

        public BuildingGroupCopy(IReadOnlyList<BuildingGroupCopyItem> sourceItems,
            IReadOnlyList<PropertyConnection> sourceConnections = null)
        {
            if (sourceItems == null || sourceItems.Count == 0 &&
                (sourceConnections == null || sourceConnections.Count == 0))
            {
                throw new ArgumentException("A group requires at least one building.",
                    nameof(sourceItems));
            }

            int minX = int.MaxValue;
            int minY = int.MaxValue;
            foreach (BuildingGroupCopyItem item in sourceItems)
            {
                minX = Math.Min(minX, item.Offset.x);
                minY = Math.Min(minY, item.Offset.y);
            }
            if (sourceConnections != null)
                foreach (PropertyConnection connection in sourceConnections)
                {
                    minX = Math.Min(minX, connection.Cell.x);
                    minY = Math.Min(minY, connection.Cell.y);
                }

            Vector2Int origin = new(minX, minY);
            items = new BuildingGroupCopyItem[sourceItems.Count];
            for (int index = 0; index < sourceItems.Count; index++)
            {
                BuildingGroupCopyItem source = sourceItems[index];
                items[index] = new BuildingGroupCopyItem(
                    source.Option,
                    source.Offset - origin,
                    source.Rotation,
                    source.CropId,
                    source.OutputMask);
            }
            propertyItems = sourceConnections == null
                ? Array.Empty<PropertyGroupCopyItem>()
                : sourceConnections.Select(connection =>
                    new PropertyGroupCopyItem(connection, connection.Cell - origin)).ToArray();
        }

        public IReadOnlyList<BuildingGroupCopyItem> Items => items;
        public IReadOnlyList<PropertyGroupCopyItem> PropertyItems => propertyItems;

        private BuildingGroupCopy(BuildingGroupCopyItem[] items,
            PropertyGroupCopyItem[] propertyItems)
        {
            this.items = items;
            this.propertyItems = propertyItems;
        }

        public BuildingGroupCopy RotateClockwise()
        {
            int groupWidth = 0;
            foreach (BuildingGroupCopyItem item in items)
            {
                Vector2Int footprint = item.Rotation.GetRotatedFootprint(
                    item.Option.Definition.Footprint);
                groupWidth = Math.Max(groupWidth, item.Offset.x + footprint.x);
            }
            foreach (PropertyGroupCopyItem item in propertyItems)
                groupWidth = Math.Max(groupWidth, item.Offset.x + 1);

            var rotatedItems = new BuildingGroupCopyItem[items.Length];
            for (int index = 0; index < items.Length; index++)
            {
                BuildingGroupCopyItem item = items[index];
                Vector2Int footprint = item.Rotation.GetRotatedFootprint(
                    item.Option.Definition.Footprint);
                Vector2Int rotatedOffset = new(
                    item.Offset.y, groupWidth - item.Offset.x - footprint.x);
                rotatedItems[index] = new BuildingGroupCopyItem(
                    item.Option,
                    rotatedOffset,
                    item.Rotation.RotateClockwise(),
                    item.CropId,
                    RotateBeltMask(item.OutputMask));
            }
            var rotatedProperties = propertyItems.Select(item =>
                new PropertyGroupCopyItem(item.Connection,
                    new Vector2Int(item.Offset.y, groupWidth - item.Offset.x - 1)))
                .ToArray();
            return new BuildingGroupCopy(rotatedItems, rotatedProperties);
        }

        public bool TryMirrorHorizontal(out BuildingGroupCopy mirrored)
        {
            return TryMirror(true, out mirrored);
        }

        public bool TryMirrorVertical(out BuildingGroupCopy mirrored)
        {
            return TryMirror(false, out mirrored);
        }

        private bool TryMirror(bool horizontal, out BuildingGroupCopy mirrored)
        {
            int groupExtent = 0;
            foreach (BuildingGroupCopyItem item in items)
            {
                Vector2Int footprint = item.Rotation.GetRotatedFootprint(
                    item.Option.Definition.Footprint);
                groupExtent = Math.Max(groupExtent,
                    horizontal ? item.Offset.x + footprint.x : item.Offset.y + footprint.y);

                if (item.Option.Definition.HasExplicitFootprint)
                {
                    mirrored = null;
                    return false;
                }

                BuildingRotation mirroredRotation = MirrorDirection(item.Rotation, horizontal);
                if (!CanMirrorPorts(item.Option, item.Rotation, mirroredRotation, horizontal))
                {
                    mirrored = null;
                    return false;
                }
            }
            foreach (PropertyGroupCopyItem item in propertyItems)
                groupExtent = Math.Max(groupExtent,
                    (horizontal ? item.Offset.x : item.Offset.y) + 1);

            var mirroredItems = new BuildingGroupCopyItem[items.Length];
            for (int index = 0; index < items.Length; index++)
            {
                BuildingGroupCopyItem item = items[index];
                Vector2Int footprint = item.Rotation.GetRotatedFootprint(
                    item.Option.Definition.Footprint);
                Vector2Int mirroredOffset = horizontal
                    ? new Vector2Int(groupExtent - item.Offset.x - footprint.x, item.Offset.y)
                    : new Vector2Int(item.Offset.x,
                        groupExtent - item.Offset.y - footprint.y);
                mirroredItems[index] = new BuildingGroupCopyItem(
                    item.Option,
                    mirroredOffset,
                    MirrorDirection(item.Rotation, horizontal),
                    item.CropId,
                    MirrorBeltMask(item.OutputMask, horizontal));
            }
            var mirroredProperties = propertyItems.Select(item =>
                new PropertyGroupCopyItem(item.Connection,
                    horizontal
                        ? new Vector2Int(groupExtent - item.Offset.x - 1, item.Offset.y)
                        : new Vector2Int(item.Offset.x, groupExtent - item.Offset.y - 1)))
                .ToArray();
            mirrored = new BuildingGroupCopy(mirroredItems, mirroredProperties);
            return true;
        }

        private static int RotateBeltMask(int mask)
        {
            int rotated = 0;
            for (int index = 0; index < 4; index++)
                if ((mask & (1 << index)) != 0)
                    rotated |= 1 << ((index + 1) & 3);
            return rotated;
        }

        private static int MirrorBeltMask(int mask, bool horizontal)
        {
            int mirrored = 0;
            for (int index = 0; index < 4; index++)
                if ((mask & (1 << index)) != 0)
                    mirrored |= 1 << (horizontal
                        ? (index is 1 or 3 ? (index + 2) & 3 : index)
                        : (index is 0 or 2 ? (index + 2) & 3 : index));
            return mirrored;
        }

        private static bool CanMirrorPorts(
            BuildingPlacementOption option,
            BuildingRotation rotation,
            BuildingRotation mirroredRotation,
            bool horizontal)
        {
            IReadOnlyList<BuildingPortPreview> ports = option.PortPreviews;
            if (ports.Count == 0)
            {
                return option.SupportsContinuousPlacement;
            }

            var matchedPorts = new bool[ports.Count];
            foreach (BuildingPortPreview port in ports)
            {
                Vector2 position = RotatePosition(port.LocalPosition, rotation);
                Vector2 mirroredPosition = horizontal
                    ? new Vector2(-position.x, position.y)
                    : new Vector2(position.x, -position.y);
                BuildingRotation mirroredDirection = MirrorDirection(
                    port.ResolveDirection(rotation), horizontal);
                bool found = false;
                for (int index = 0; index < ports.Count; index++)
                {
                    BuildingPortPreview candidate = ports[index];
                    if (matchedPorts[index] || candidate.Kind != port.Kind ||
                        candidate.ResolveDirection(mirroredRotation) != mirroredDirection ||
                        (RotatePosition(candidate.LocalPosition, mirroredRotation) -
                            mirroredPosition).sqrMagnitude > 0.000001f)
                    {
                        continue;
                    }

                    matchedPorts[index] = true;
                    found = true;
                    break;
                }

                if (!found)
                {
                    return false;
                }
            }

            return true;
        }

        private static Vector2 RotatePosition(Vector2 position, BuildingRotation rotation)
        {
            return rotation switch
            {
                BuildingRotation.Degrees0 => position,
                BuildingRotation.Degrees90 => new Vector2(position.y, -position.x),
                BuildingRotation.Degrees180 => -position,
                BuildingRotation.Degrees270 => new Vector2(-position.y, position.x),
                _ => throw new ArgumentOutOfRangeException(nameof(rotation), rotation, null)
            };
        }

        private static BuildingRotation MirrorDirection(BuildingRotation rotation, bool horizontal)
        {
            return rotation switch
            {
                BuildingRotation.Degrees0 => horizontal
                    ? BuildingRotation.Degrees0 : BuildingRotation.Degrees180,
                BuildingRotation.Degrees90 => horizontal
                    ? BuildingRotation.Degrees270 : BuildingRotation.Degrees90,
                BuildingRotation.Degrees180 => horizontal
                    ? BuildingRotation.Degrees180 : BuildingRotation.Degrees0,
                BuildingRotation.Degrees270 => horizontal
                    ? BuildingRotation.Degrees90 : BuildingRotation.Degrees270,
                _ => throw new ArgumentOutOfRangeException(nameof(rotation), rotation, null)
            };
        }

        public bool CanPlace(Vector2Int anchorCell, GridOccupancy occupancy)
        {
            return CanPlace(anchorCell, occupancy, null);
        }

        public bool CanPlace(
            Vector2Int anchorCell,
            GridOccupancy occupancy,
            ISet<BuildingPlacement> ignoredPlacements)
        {
            if (occupancy == null)
            {
                throw new ArgumentNullException(nameof(occupancy));
            }

            var groupCells = new HashSet<Vector2Int>();
            foreach (BuildingGroupCopyItem item in items)
            {
                BuildingDefinition definition = item.Option.Definition;
                Vector2Int itemCell = anchorCell + item.Offset;
                if (!occupancy.CanPlace(definition, itemCell, item.Rotation,
                        ignoredPlacements) ||
                    (item.Option.PlacementBehavior != null &&
                        !item.Option.PlacementBehavior.CanPlace(
                            itemCell, definition.Footprint, item.Rotation)))
                {
                    return false;
                }

                var candidate = new BuildingPlacement(definition.Id, itemCell,
                    definition.Footprint, item.Rotation, definition.OccupiedCells);
                foreach (Vector2Int cell in candidate.OccupiedCells)
                {
                    if (!groupCells.Add(cell))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }

    public sealed class GridDragTracker
    {
        private readonly List<Vector2Int> newCells = new();
        private readonly HashSet<Vector2Int> visitedCells = new();
        private Vector2Int? lastCell;

        public bool IsActive => lastCell.HasValue;

        public Vector2Int? LastCell => lastCell;

        public IReadOnlyList<Vector2Int> Continue(Vector2Int currentCell)
        {
            newCells.Clear();
            if (!lastCell.HasValue)
            {
                AddIfNew(currentCell);
                lastCell = currentCell;
                return newCells;
            }

            Vector2Int nextCell = lastCell.Value;
            while (nextCell.x != currentCell.x)
            {
                nextCell.x += Math.Sign(currentCell.x - nextCell.x);
                AddIfNew(nextCell);
            }

            while (nextCell.y != currentCell.y)
            {
                nextCell.y += Math.Sign(currentCell.y - nextCell.y);
                AddIfNew(nextCell);
            }

            lastCell = currentCell;
            return newCells;
        }

        public void Reset()
        {
            lastCell = null;
            visitedCells.Clear();
            newCells.Clear();
        }

        private void AddIfNew(Vector2Int cell)
        {
            if (visitedCells.Add(cell))
            {
                newCells.Add(cell);
            }
        }
    }

    public readonly struct BeltPlacementStep
    {
        public BeltPlacementStep(Vector2Int cell, BuildingRotation rotation)
        {
            Cell = cell;
            Rotation = rotation;
        }

        public Vector2Int Cell { get; }

        public BuildingRotation Rotation { get; }
    }

    public sealed class BeltDragPlacementPlanner
    {
        private readonly List<BeltPlacementStep> completedSteps = new();
        private bool hasPathRotation;
        private Vector2Int? pendingCell;

        public bool IsActive => pendingCell.HasValue;

        public bool HasPathRotation => hasPathRotation;

        public Vector2Int? PendingCell => pendingCell;

        public BuildingRotation LastPathRotation { get; private set; }

        public BuildingRotation GetPreviewRotation(BuildingRotation fallbackRotation)
        {
            return hasPathRotation ? LastPathRotation : fallbackRotation;
        }

        public IReadOnlyList<BeltPlacementStep> Continue(IReadOnlyList<Vector2Int> pathCells)
        {
            completedSteps.Clear();
            foreach (Vector2Int cell in pathCells)
            {
                if (!pendingCell.HasValue)
                {
                    pendingCell = cell;
                    continue;
                }

                if (!TryGetPathRotation(
                        pendingCell.Value,
                        cell,
                        out BuildingRotation rotation))
                {
                    continue;
                }

                completedSteps.Add(new BeltPlacementStep(pendingCell.Value, rotation));
                pendingCell = cell;
                LastPathRotation = rotation;
                hasPathRotation = true;
            }

            return completedSteps;
        }

        public bool TryComplete(
            BuildingRotation singlePlacementRotation,
            out BeltPlacementStep finalStep)
        {
            if (!pendingCell.HasValue)
            {
                finalStep = default;
                return false;
            }

            BuildingRotation rotation = hasPathRotation
                ? LastPathRotation
                : singlePlacementRotation;
            finalStep = new BeltPlacementStep(pendingCell.Value, rotation);
            Reset();
            return true;
        }

        public void Reset()
        {
            completedSteps.Clear();
            hasPathRotation = false;
            pendingCell = null;
            LastPathRotation = BuildingRotation.Degrees0;
        }

        public static bool TryGetPathRotation(
            Vector2Int source,
            Vector2Int destination,
            out BuildingRotation rotation)
        {
            Vector2Int offset = destination - source;
            if (offset == Vector2Int.up)
            {
                rotation = BuildingRotation.Degrees0;
                return true;
            }

            if (offset == Vector2Int.right)
            {
                rotation = BuildingRotation.Degrees90;
                return true;
            }

            if (offset == Vector2Int.down)
            {
                rotation = BuildingRotation.Degrees180;
                return true;
            }

            if (offset == Vector2Int.left)
            {
                rotation = BuildingRotation.Degrees270;
                return true;
            }

            rotation = default;
            return false;
        }
    }
}
