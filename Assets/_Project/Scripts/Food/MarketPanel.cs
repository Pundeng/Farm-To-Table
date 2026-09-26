using System;
using System.Collections.Generic;
using System.Linq;
using FantasyShapez.Buildings;
using FantasyShapez.Grid;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FantasyShapez.Food
{
    public sealed class MarketPanel : MonoBehaviour
    {
        [SerializeField] private Market market = null;
        [SerializeField] private BuildingPlacementController buildings = null;
        private Vector2 scrollPosition;
        private Vector2 objectiveScroll;
        private Vector2 regionScroll;
        private ProgressionSaveService saves;
        private string saveMessage;
        private string selectedRegionId;
        private string regionMessage;
        private Vector2 objectiveAnchor;
        private bool objectiveAnchorValid;
        private GameObject regionVisualRoot;
        private readonly Dictionary<string, SpriteRenderer> regionVisuals = new();
        public event Action GameSaved;
        private string SavePath => buildings != null && buildings.IsFoodDemo
            ? ProgressionSaveService.DemoPath : ProgressionSaveService.DefaultPath;

        public bool IsPointerOverPanel
        {
            get
            {
                if (!isActiveAndEnabled || market == null || market.Inventory == null ||
                    Mouse.current == null)
                {
                    return false;
                }

                Vector2 pointer = Mouse.current.position.ReadValue();
                pointer.y = Screen.height - pointer.y;
                return buildings != null && buildings.IsFoodDemo
                    ? buildings.OpenDemoPanel == BuildingPlacementController.DemoPanel.Market &&
                      GetObjectivePopoverRect().Contains(pointer)
                    : GetPanelRect().Contains(pointer);
            }
        }

        private void OnGUI()
        {
            if (market == null || market.Inventory == null)
            {
                return;
            }

            if (buildings != null && buildings.IsFoodDemo)
            {
                DrawWorldObjective();
                bool originalEnabled = GUI.enabled;
                if (buildings.BlocksAllWorldInput) GUI.enabled = false;
                if (buildings.OpenDemoPanel == BuildingPlacementController.DemoPanel.Market)
                    DrawObjectivePopover();
                if (buildings.OpenDemoPanel == BuildingPlacementController.DemoPanel.Region)
                    DrawRegionPanel();
                GUI.enabled = originalEnabled;
                return;
            }

            DrawRegionPanel();

            Rect rect = GetPanelRect();
            GUI.Box(rect, GUIContent.none);
            GUILayout.BeginArea(new Rect(rect.x + 12f, rect.y + 8f,
                rect.width - 24f, rect.height - 16f));
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            GUILayout.Label("Market");
            if (buildings != null && buildings.IsFoodDemo)
            {
                var guidanceStyle = new GUIStyle(GUI.skin.label) { wordWrap = true };
                GUILayout.Label(market.Unlocks.IsUnlocked("demo", "complete")
                    ? "DEMO COMPLETE — your food factory is running!"
                    : GetDemoGuidance(), guidanceStyle);
            }
            GUILayout.Label($"Total delivered: {market.Inventory.TotalDelivered}");
            GUILayout.Label($"Regular currency: {market.Currency}");
            if (!string.IsNullOrEmpty(market.LastDeliveryMessage))
            {
                GUILayout.Label(market.LastDeliveryMessage);
            }

            foreach (var delivered in market.Inventory.DeliveredCounts
                .OrderBy(entry => entry.Key.Kind)
                .ThenBy(entry => entry.Key.Id, StringComparer.Ordinal))
            {
                string kind = delivered.Key.Kind == FoodItemKind.RawIngredient
                    ? "raw" : "processed";
                GUILayout.Label($"{delivered.Key.Id} ({kind}): {delivered.Value}");
            }

            FoodOrderProgress order = market.ActiveOrder;
            if (market.Orders.Count > 0)
            {
                GUILayout.Space(6f);
                GUILayout.Label($"Orders complete: {market.CompletedOrders.Count} / {market.Orders.Count}");
            }

            foreach (FoodOrder completed in market.CompletedOrders)
            {
                GUILayout.Label($"Completed: {completed.DisplayName}");
                foreach (UnlockKey unlock in completed.Unlocks)
                {
                    GUILayout.Label($"Unlocked {unlock.Category}: {unlock.Id}");
                }
            }

            if (order != null)
            {
                GUILayout.Label($"Order: {order.Order.DisplayName}");
                foreach (FoodOrderRequirement requirement in order.Order.Requirements)
                {
                    GUILayout.Label($"{requirement.Food.Id}: " +
                        $"{order.GetDeliveredCount(requirement)} / {requirement.Quantity}");
                }
            }
            else if (market.Orders.Count > 0)
            {
                GUILayout.Label("All orders complete.");
            }

            SeedShop shop = market.SeedShop;
            if (shop != null && shop.Offers.Count > 0)
            {
                GUILayout.Space(6f);
                GUILayout.Label("Seed Shop");
                foreach (SeedShopOffer offer in shop.Offers)
                {
                    SeedShopOfferState state = shop.GetState(offer.CropId);
                    switch (state)
                    {
                        case SeedShopOfferState.Locked:
                            GUILayout.Label($"{offer.DisplayName}: locked " +
                                $"(requires {offer.RequiredUnlock.Category}: {offer.RequiredUnlock.Id})");
                            break;
                        case SeedShopOfferState.Available:
                            GUILayout.Label($"{offer.DisplayName}: available, " +
                                $"need {offer.Price} currency");
                            break;
                        case SeedShopOfferState.Affordable:
                            if (GUILayout.Button($"Affordable: buy {offer.DisplayName} " +
                                    $"({offer.Price} currency)"))
                            {
                                shop.TryPurchase(offer.CropId);
                            }
                            break;
                        case SeedShopOfferState.Purchased:
                            GUILayout.Label($"{offer.DisplayName}: purchased");
                            break;
                        case SeedShopOfferState.AlreadyUnlocked:
                            GUILayout.Label($"{offer.DisplayName}: already unlocked");
                            break;
                    }
                }
            }

            RegionState regions = market.Regions;
            if (regions != null && regions.Regions.Count > 0)
            {
                GUILayout.Space(6f);
                GUILayout.Label("Farmable Regions");
                GUILayout.Label("Green restored, olive available, blue needs currency, " +
                    "gray needs progression; dark ground is not farmable.");
                foreach (FarmableRegion region in regions.Regions)
                {
                    string area = $"({region.MinimumCell.x}.." +
                        $"{region.MinimumCell.x + region.Size.x - 1}, " +
                        $"{region.MinimumCell.y}.." +
                        $"{region.MinimumCell.y + region.Size.y - 1})";
                    if (GUILayout.Button($"Inspect {region.DisplayName} {area}: " +
                            regions.GetPurchaseStatus(region.Id, market.Currency)))
                        selectedRegionId = region.Id;
                }
            }

            if (buildings != null)
            {
                saves ??= new ProgressionSaveService(market, buildings);
                GUILayout.Space(6f);
                GUILayout.Label("Factory Save");
                GUILayout.Label(SavePath);
                if (GUILayout.Button(buildings.IsFoodDemo ? "Save factory" :
                        "Save progression"))
                    SaveGame();

                if (GUILayout.Button(buildings.IsFoodDemo ? "Load factory" :
                        "Load progression"))
                {
                    if (!saves.TryReadValidated(SavePath,
                            out ProgressionSaveData data, out string error))
                    {
                        saveMessage = $"Load failed: {error}";
                    }
                    else if (data.version == 1)
                    {
                        saveMessage = saves.TryLoad(SavePath,
                            out error) ? "Version 1 progression loaded." :
                            $"Load failed: {error}";
                    }
                    else
                    {
                        saveMessage = FactoryWorldLoadSession.TryBegin(data, out error) ?
                            "Reconstructing factory..." : $"Load failed: {error}";
                    }
                }

                string displayedMessage = saveMessage ?? FactoryWorldLoadSession.LastMessage;
                if (!string.IsNullOrEmpty(displayedMessage))
                {
                    GUILayout.Label(displayedMessage);
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        public bool IsPointerOverRegionPanel => selectedRegionId != null &&
            Mouse.current != null && GetRegionPanelRect().Contains(
                new Vector2(Mouse.current.position.ReadValue().x,
                    Screen.height - Mouse.current.position.ReadValue().y));

        public static bool ShouldConsumeRegionClick(RegionStatus status,
            bool placementMode, bool occupied, bool inspectRestored = false) =>
            !occupied && (status != RegionStatus.Restored ||
                inspectRestored && !placementMode);

        public bool TrySelectRegionAt(Vector2Int cell, bool placementMode,
            bool occupied)
        {
            FarmableRegion region = market?.Regions?.GetRegionAt(cell);
            if (region == null || !ShouldConsumeRegionClick(
                    market.Regions.GetStatus(region.Id), placementMode, occupied,
                    Keyboard.current?.altKey.isPressed == true))
                return false;
            selectedRegionId = region.Id;
            regionMessage = null;
            return true;
        }

        public bool TryOpenAt(Vector2Int cell)
        {
            if (market == null) return false;
            Vector2Int origin = market.InputCell;
            Vector2Int size = market.Footprint;
            return IsPointerOverWorldObjective ||
                cell.x >= origin.x && cell.y >= origin.y &&
                cell.x < origin.x + size.x && cell.y < origin.y + size.y;
        }

        public bool IsPointerOverWorldObjective
        {
            get
            {
                if (!objectiveAnchorValid || Mouse.current == null || Camera.main == null ||
                    Camera.main.orthographicSize > 15f) return false;
                Vector2 pointer = Mouse.current.position.ReadValue();
                pointer.y = Screen.height - pointer.y;
                return new Rect(objectiveAnchor.x - 85f,
                    objectiveAnchor.y - 44f, 170f, 48f).Contains(pointer);
            }
        }

        public void CloseRegion() => selectedRegionId = null;
        public string SaveMessage => saveMessage ?? FactoryWorldLoadSession.LastMessage;

        public void SaveGame()
        {
            if (market == null || buildings == null) return;
            saves ??= new ProgressionSaveService(market, buildings);
            bool saved = saves.TrySave(SavePath, out string error);
            saveMessage = saved ?
                "Factory saved." : $"Save failed: {error}";
            if (saved) GameSaved?.Invoke();
        }

        public void LoadGame()
        {
            if (market == null || buildings == null) return;
            saves ??= new ProgressionSaveService(market, buildings);
            if (!saves.TryReadValidated(SavePath,
                    out ProgressionSaveData data, out string error))
                saveMessage = $"Load failed: {error}";
            else if (data.version == 1)
                saveMessage = saves.TryLoad(SavePath, out error) ?
                    "Version 1 progression loaded." : $"Load failed: {error}";
            else
                saveMessage = FactoryWorldLoadSession.TryBegin(data, out error) ?
                    "Reconstructing factory..." : $"Load failed: {error}";
        }

        private void DrawWorldObjective()
        {
            objectiveAnchorValid = false;
            Camera camera = Camera.main;
            if (camera == null || camera.orthographicSize > 15f)
            {
                if (buildings.OpenDemoPanel == BuildingPlacementController.DemoPanel.Market)
                    buildings.ClosePanel();
                return;
            }
            Vector3 screen = camera.WorldToScreenPoint(market.transform.position +
                new Vector3(0f, 1.25f, 0f));
            if (screen.z <= 0f) return;
            objectiveAnchor = new Vector2(screen.x, Screen.height - screen.y);
            if (objectiveAnchor.x < -140f || objectiveAnchor.x > Screen.width + 140f ||
                objectiveAnchor.y < -80f || objectiveAnchor.y > Screen.height + 80f)
                return;
            objectiveAnchorValid = true;
            FoodOrderProgress order = market.ActiveOrder;
            string progress = order == null ? "Orders complete" :
                string.Join("  ", order.Order.Requirements.Select(requirement =>
                    $"{order.GetDeliveredCount(requirement)} / {requirement.Quantity}"));
            Vector2 pointer = Mouse.current?.position.ReadValue() ?? Vector2.zero;
            pointer.y = Screen.height - pointer.y;
            bool hovered = new Rect(objectiveAnchor.x - 80f,
                objectiveAnchor.y - 18f, 160f, 38f).Contains(pointer);
            string label = hovered && order != null
                ? $"{order.Order.DisplayName}\n{progress}" : progress;
            GUI.Box(new Rect(objectiveAnchor.x - 85f,
                objectiveAnchor.y - (hovered ? 42f : 20f), 170f,
                hovered ? 44f : 24f), label);
        }

        private Rect GetObjectivePopoverRect()
        {
            float width = Mathf.Min(260f, Screen.width - 16f);
            float minY = Screen.width < 500f ? 76f : 8f;
            float height = Mathf.Max(60f,
                Mathf.Min(220f, Screen.height - minY - 114f));
            float x = objectiveAnchor.x + 24f;
            if (x + width > Screen.width - 8f) x = objectiveAnchor.x - width - 24f;
            return new Rect(Mathf.Clamp(x, 8f, Screen.width - width - 8f),
                Mathf.Clamp(objectiveAnchor.y - height * 0.5f, minY,
                    Screen.height - height - 114f), width, height);
        }

        private void DrawObjectivePopover()
        {
            Rect rect = GetObjectivePopoverRect();
            GUI.Box(rect, GUIContent.none);
            GUILayout.BeginArea(new Rect(rect.x + 10f, rect.y + 8f,
                rect.width - 20f, rect.height - 16f));
            objectiveScroll = GUILayout.BeginScrollView(objectiveScroll);
            FoodOrderProgress order = market.ActiveOrder;
            GUILayout.Label(order?.Order.DisplayName ?? "All orders complete");
            if (order != null)
            {
                foreach (FoodOrderRequirement requirement in order.Order.Requirements)
                    GUILayout.Label($"{requirement.Food.Id}: " +
                        $"{order.GetDeliveredCount(requirement)} / {requirement.Quantity}");
                if (order.Order.Unlocks.Count > 0) GUILayout.Label("Reward");
                foreach (UnlockKey unlock in order.Order.Unlocks)
                    GUILayout.Label($"Unlock: {unlock.Id}");
                GUILayout.Label(GetDemoGuidance());
            }
            if (market.SeedShop?.Offers.Count > 0)
            {
                foreach (SeedShopOffer offer in market.SeedShop.Offers)
                    if (market.SeedShop.GetState(offer.CropId) == SeedShopOfferState.Affordable &&
                        GUILayout.Button($"Buy {offer.DisplayName} ({offer.Price})"))
                        market.SeedShop.TryPurchase(offer.CropId);
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void Start()
        {
            if (market?.Regions == null || buildings?.GridSystem == null) return;
            regionVisualRoot = new GameObject("Farmable region background");
            regionVisualRoot.transform.SetParent(transform, false);
            GridSystem grid = buildings.GridSystem;
            foreach (FarmableRegion region in market.Regions.Regions)
            {
                var area = new GameObject(region.DisplayName);
                area.transform.SetParent(regionVisualRoot.transform, false);
                Vector3 min = grid.GridToWorld(region.MinimumCell);
                Vector3 max = grid.GridToWorld(region.MinimumCell + region.Size - Vector2Int.one);
                area.transform.position = (min + max) * 0.5f;
                area.transform.localScale = new Vector3(region.Size.x * grid.CellSize,
                    region.Size.y * grid.CellSize, 1f);
                SpriteRenderer renderer = area.AddComponent<SpriteRenderer>();
                renderer.sprite = BuildingVisualFactory.PlaceholderSprite;
                renderer.sortingOrder = -110;
                regionVisuals.Add(region.Id, renderer);
            }
        }

        private void LateUpdate()
        {
            if (market?.Regions == null) return;
            foreach (FarmableRegion region in market.Regions.Regions)
            {
                if (!regionVisuals.TryGetValue(region.Id, out SpriteRenderer renderer))
                    continue;
                RegionPurchaseStatus status = market.Regions.GetPurchaseStatus(
                    region.Id, market.Currency);
                Color color = status switch
                {
                    RegionPurchaseStatus.Restored => new Color(0.34f, 0.62f, 0.31f, 0.55f),
                    RegionPurchaseStatus.Available => new Color(0.55f, 0.64f, 0.32f, 0.45f),
                    RegionPurchaseStatus.Unaffordable => new Color(0.43f, 0.49f, 0.66f, 0.48f),
                    RegionPurchaseStatus.NotAdjacent => new Color(0.48f, 0.38f, 0.56f, 0.48f),
                    _ => new Color(0.37f, 0.40f, 0.53f, 0.48f)
                };
                if (region.Id == selectedRegionId) color.a = 0.75f;
                renderer.color = color;
            }
        }

        private void OnDestroy()
        {
            if (regionVisualRoot != null) Destroy(regionVisualRoot);
        }

        private Rect GetRegionPanelRect()
        {
            if (buildings == null || !buildings.IsFoodDemo)
                return new Rect(Mathf.Max(16f, Screen.width - 320f),
                    16f, 304f, 242f);
            float y = Screen.width < 500f ? 76f : 8f;
            return new Rect(
                Mathf.Max(8f, Screen.width - Mathf.Min(312f, Screen.width - 16f)),
                y, Mathf.Min(304f, Screen.width - 16f),
                Mathf.Max(60f, Mathf.Min(242f, Screen.height - y - 114f)));
        }

        private void DrawRegionPanel()
        {
            if (selectedRegionId == null || market.Regions == null) return;
            FarmableRegion region = market.Regions.Regions.FirstOrDefault(candidate =>
                candidate.Id == selectedRegionId);
            if (region == null) return;

            Rect rect = GetRegionPanelRect();
            GUI.Box(rect, GUIContent.none);
            GUILayout.BeginArea(new Rect(rect.x + 10f, rect.y + 8f,
                rect.width - 20f, rect.height - 16f));
            if (buildings != null && buildings.IsFoodDemo)
                regionScroll = GUILayout.BeginScrollView(regionScroll);
            GUILayout.Label(region.DisplayName);
            if (buildings == null || !buildings.IsFoodDemo || buildings.IsDevInspectorOpen)
                GUILayout.Label($"Bounds: x {region.MinimumCell.x}.." +
                    $"{region.MinimumCell.x + region.Size.x - 1}, y " +
                    $"{region.MinimumCell.y}..{region.MinimumCell.y + region.Size.y - 1}");
            int used = buildings?.CountFarmPlots(region) ?? 0;
            int free = buildings?.CountFreeFarmCells(region) ??
                region.FarmableCellCount - used;
            GUILayout.Label($"Farmable cells: {region.FarmableCellCount} " +
                $"({used} plots, {free} free)");
            GUILayout.Label($"Price: {region.Price} currency");
            GUILayout.Label(region.HasRequirement
                ? $"Requires {region.RequiredUnlockCategory}: {region.RequiredUnlockId}"
                : "Progression: none");
            RegionPurchaseStatus status = market.Regions.GetPurchaseStatus(
                region.Id, market.Currency);
            GUILayout.Label($"Status: {status}");
            if (status == RegionPurchaseStatus.Available &&
                GUILayout.Button($"Purchase {region.DisplayName}"))
            {
                regionMessage = market.Regions.TryPurchase(region.Id, market.Inventory)
                    ? $"{region.DisplayName} restored!"
                    : "Purchase failed.";
            }
            if (!string.IsNullOrEmpty(regionMessage)) GUILayout.Label(regionMessage);
            if (GUILayout.Button("Close"))
            {
                if (buildings != null && buildings.IsFoodDemo) buildings.ClosePanel();
                else selectedRegionId = null;
            }
            if (buildings != null && buildings.IsFoodDemo)
                GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private string GetDemoGuidance()
        {
            string orderId = market.ActiveOrder?.Order.Id;
            return orderId switch
            {
                "First Harvest" => "Start: place a Farm Plot on Starter Fields, " +
                    "choose Apple, cover it with a Harvester, and belt apples to Market. " +
                    "Use the Hotbar or Build Menu, R to rotate, Esc to exit build mode.",
                "Dried Apples" => "Build a Processor. In its default rotation, " +
                    "belt apples in from the west and collect Air with a Collector " +
                    "and Pipe to the south property port. Belt its north output to Market.",
                "Vegetable Base" => "Grow Tomato and Onion. Feed separate belts " +
                    "to the Mixer's two west inputs in default rotation, then belt " +
                    "its east output to Market.",
                "Cut Potatoes" when market.Regions.GetStatus("East Field") !=
                    RegionStatus.Restored => "Select East Field on the map and purchase " +
                        "it to unlock Potato.",
                "Cut Potatoes" => "Grow Potato in East Field. In default Cutter " +
                    "rotation, feed its rear from the south and connect a westbound " +
                    "and eastbound belt to its front sides. One potato makes two cuts.",
                "French Fries" => "Pipe Heat into a Processor and feed it Cut Potato. " +
                    "Belt French Fries to Market to complete the demo.",
                _ => "Build a food production line and deliver its output to Market."
            };
        }

        private Rect GetPanelRect()
        {
            int rowCount = market.Inventory.DeliveredCounts.Count;
            FoodOrderProgress order = market.ActiveOrder;
            int completedLineCount = market.CompletedOrders.Sum(completed =>
                1 + completed.Unlocks.Count);
            float contentHeight = 136f + rowCount * 22f + completedLineCount * 22f +
                (order == null ? 22f : 22f + order.Order.Requirements.Count * 22f);
            if (market.SeedShop?.Offers.Count > 0)
            {
                contentHeight += 28f + market.SeedShop.Offers.Count * 28f;
            }
            if (market.Regions?.Regions.Count > 0)
            {
                contentHeight += 28f + market.Regions.Regions.Count * 28f;
            }
            if (buildings != null)
            {
                contentHeight += 130f;
            }
            float height = Mathf.Min(contentHeight, Mathf.Max(16f, Screen.height * 0.5f));
            return new Rect(16f, Mathf.Max(16f, Screen.height - height - 16f),
                300f, height);
        }
    }
}
