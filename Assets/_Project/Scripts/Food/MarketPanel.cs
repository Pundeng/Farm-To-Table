using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CozyFoodFactory.Buildings;
using CozyFoodFactory.Grid;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CozyFoodFactory.Food
{
    public sealed class MarketPanel : MonoBehaviour
    {
        [SerializeField] private Market market = null;
        [SerializeField] private BuildingPlacementController buildings = null;
        private readonly List<SpriteRenderer> territoryFills = new();
        private Vector2 scrollPosition;
        private Vector2 objectiveScroll;
        private Vector2 detailScroll;
        private Vector2 territoryScroll;
        private enum MarketSection { Current, Seeds, Sales, History }
        private MarketSection section;
        private float orderFeedbackUntil;
        private ProgressionSaveService saves;
        private string saveMessage;
        private DateTime cachedSaveWriteUtc;
        private string cachedSaveSummary;
        private string cachedSavePath;
        private Vector2Int? selectedTerritory;
        private int selectedSaveSlot = 1;
        private string regionMessage;
        private Vector2 objectiveAnchor;
        private bool objectiveAnchorValid;
        private Rect objectiveWorldRect;
        public event Action GameSaved;
        private string ActiveSavePath => ProgressionSaveService.SlotPath(
            ProgressionSaveSlotSession.ActiveSlot);
        private string SelectedSavePath => ProgressionSaveService.SlotPath(selectedSaveSlot);

        private void OnEnable() => selectedSaveSlot = ProgressionSaveSlotSession.ActiveSlot;

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
                      (section == MarketSection.Current ?
                          GetObjectivePopoverRect() : GetDetailPanelRect()).Contains(pointer)
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
                DrawTerritoryOverlays();
                DrawWorldObjective();
                bool originalEnabled = GUI.enabled;
                if (buildings.BlocksAllWorldInput) GUI.enabled = false;
                if (buildings.OpenDemoPanel == BuildingPlacementController.DemoPanel.Market)
                {
                    if (section == MarketSection.Current) DrawObjectivePopover();
                    else DrawMarketDetail();
                }
                DrawWorldFeedback();
                if (buildings.OpenDemoPanel == BuildingPlacementController.DemoPanel.Region)
                    DrawTerritoryPanel();
                GUI.enabled = originalEnabled;
                return;
            }

            DrawTerritoryOverlays();
            DrawTerritoryPanel();

            Rect rect = GetPanelRect();
            GUI.Box(rect, GUIContent.none);
            GUILayout.BeginArea(new Rect(rect.x + 12f, rect.y + 8f,
                rect.width - 24f, rect.height - 16f));
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            GUILayout.Label("Market");
            if (buildings != null && buildings.IsFoodDemo)
            {
                var guidanceStyle = new GUIStyle(GUI.skin.label) { wordWrap = true };
                GUILayout.Label(market.Unlocks.IsUnlocked("chapter", "chapter_1_complete")
                    ? "CHAPTER 1 COMPLETE — Vegetable Automation!"
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
                    GUILayout.Label($"Unlocked {unlock.Category}: {UnlockDisplayName(unlock)}");
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

            GUILayout.Space(6f);
            GUILayout.Label($"Territories: {market.Territories.PurchasedCoordinates.Count} " +
                $"owned  |  Next expansion: {market.Territories.NextPurchaseCost}");

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        public bool IsPointerOverRegionPanel => selectedTerritory.HasValue &&
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
            if (market == null || occupied) return false;
            Vector2Int coordinate = market.Territories.CoordinateAtCell(cell);
            if (market.Territories.IsPurchased(coordinate) ||
                market.Territories.IsReservedHub(coordinate)) return false;
            if (market.Territories.GetPurchaseStatus(coordinate,
                    market.Currency) == TerritoryPurchaseStatus.NotAdjacent)
                return false;
            selectedTerritory = coordinate;
            regionMessage = null;
            return true;
        }

        public bool IsPointerOverTerritoryUi => Mouse.current != null &&
            GetTerritoryActionRectAtPointer(GetGuiPointer(), out _);

        private static Vector2 GetGuiPointer()
        {
            Vector2 pointer = Mouse.current?.position.ReadValue() ?? Vector2.zero;
            pointer.y = Screen.height - pointer.y;
            return pointer;
        }

        public bool TryOpenAt(Vector2Int cell)
        {
            if (market == null) return false;
            Vector2Int origin = market.AnchorCell;
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
                return objectiveWorldRect.Contains(pointer);
            }
        }

        public void CloseRegion() => selectedTerritory = null;
        public void OpenCurrentOrder() => section = MarketSection.Current;
        public void ShowOrderCompletion() => orderFeedbackUntil = Time.time + 3f;
        public string SaveMessage => saveMessage ?? FactoryWorldLoadSession.LastMessage;

        public bool SaveGame()
        {
            if (market == null || buildings == null) return false;
            saves ??= new ProgressionSaveService(market, buildings);
            bool saved = saves.TrySave(ActiveSavePath, out string error);
            saveMessage = saved ?
                "Factory saved." : $"Save failed: {error}";
            if (saved) cachedSaveSummary = null;
            if (saved) GameSaved?.Invoke();
            return saved;
        }

        public string GetSaveSummary()
        {
            if (market == null || buildings == null) return "Save unavailable.";
            return GetSaveSlotSummary(selectedSaveSlot);
        }

        public bool IsSaveSlotOccupied(int slot) =>
            File.Exists(ProgressionSaveService.SlotPath(slot));

        public int SelectedSaveSlot => selectedSaveSlot;
        public int ActiveSaveSlot => ProgressionSaveSlotSession.ActiveSlot;
        public void SelectSaveSlot(int slot)
        {
            if (slot < 1 || slot > ProgressionSaveService.SlotCount)
                throw new ArgumentOutOfRangeException(nameof(slot));
            selectedSaveSlot = slot;
            cachedSavePath = null;
        }

        public bool StartNewGameInSelectedSlot()
        {
            int previousSlot = ProgressionSaveSlotSession.ActiveSlot;
            ProgressionSaveSlotSession.SetActiveSlot(selectedSaveSlot);
            if (!FactoryWorldLoadSession.TryBeginNewGame(out string error))
            {
                ProgressionSaveSlotSession.SetActiveSlot(previousSlot);
                saveMessage = error;
                return false;
            }
            saveMessage = "Starting a new game...";
            return true;
        }

        public string GetSaveSlotSummary(int slot)
        {
            string path = ProgressionSaveService.SlotPath(slot);
            if (!File.Exists(path))
                return slot == 1 && HasLegacySingleSlotSave()
                    ? "Empty. A previous single-slot save is incompatible with the 9x9 map and remains preserved."
                    : "Empty slot.";
            DateTime writeUtc = File.GetLastWriteTimeUtc(path);
            if (cachedSaveSummary != null && cachedSaveWriteUtc == writeUtc &&
                cachedSavePath == path)
                return cachedSaveSummary;
            saves ??= new ProgressionSaveService(market, buildings);
            if (!saves.TryReadValidated(path, out ProgressionSaveData data,
                    out string error)) return $"Save unavailable: {error}";
            string timestamp = File.GetLastWriteTime(path).ToString("g");
            if (data.version == 1)
                return $"Legacy progression save: {timestamp}\n" +
                    $"Currency: {data.currency}\nObjective: {data.activeOrderId ?? "All complete"}";
            int savedNextExpansionCost = market.Territories.Settings
                .CostForExpansionCount(data.world.territoryPurchaseCount);
            cachedSaveWriteUtc = writeUtc;
            cachedSavePath = path;
            cachedSaveSummary = $"Last saved: {timestamp}\n" +
                $"Chapter objective: {data.activeOrderId ?? "All complete"}\n" +
                $"Currency: {data.currency}\nWorld seed: {data.world.worldSeed}\n" +
                $"Owned parcels: {data.world.purchasedTerritories.Length}\n" +
                $"Next side: {savedNextExpansionCost}";
            return cachedSaveSummary;
        }

        private static bool HasLegacySingleSlotSave() =>
            File.Exists(ProgressionSaveService.DemoPath) ||
            File.Exists(ProgressionSaveService.PreviousCampaignPath) ||
            File.Exists(ProgressionSaveService.PreviousReservedTerritoryPath) ||
            File.Exists(ProgressionSaveService.PreviousTerritoryPath) ||
            File.Exists(ProgressionSaveService.PreviousChapterPath) ||
            File.Exists(ProgressionSaveService.LegacyDemoPath);

        public bool LoadGame()
        {
            if (market == null || buildings == null) return false;
            saves ??= new ProgressionSaveService(market, buildings);
            if (!saves.TryReadValidated(SelectedSavePath,
                    out ProgressionSaveData data, out string error))
            {
                saveMessage = !File.Exists(SelectedSavePath) && HasLegacySingleSlotSave()
                    ? "Legacy single-slot saves are preserved but incompatible with this map. Start a new game in an empty slot."
                    : $"Load failed: {error}";
                return false;
            }
            else
            {
                int previousSlot = ProgressionSaveSlotSession.ActiveSlot;
                ProgressionSaveSlotSession.SetActiveSlot(selectedSaveSlot);
                bool started = FactoryWorldLoadSession.TryBegin(data, out error);
                if (!started) ProgressionSaveSlotSession.SetActiveSlot(previousSlot);
                saveMessage = started ? "Reconstructing factory..." :
                    $"Load failed: {error}";
                return started;
            }
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
                new Vector3(0f, market.Footprint.y * 0.3f, 0f));
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
            float projectedCellSize = buildings.GridSystem.CellSize *
                Screen.height / (2f * camera.orthographicSize);
            float boxWidth = Mathf.Min(210f,
                market.Footprint.x * projectedCellSize * 0.86f);
            float boxHeight = Mathf.Min(52f, projectedCellSize * 0.62f);
            Rect objectiveRect = new(objectiveAnchor.x - boxWidth * 0.5f,
                objectiveAnchor.y - boxHeight * 0.5f, boxWidth, boxHeight);
            objectiveWorldRect = objectiveRect;
            bool hovered = objectiveRect.Contains(pointer);
            string label = hovered && order != null
                ? $"{order.Order.DisplayName}\n{progress}" : progress;
            GUIStyle style = new(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                fontSize = Mathf.Clamp(Mathf.RoundToInt(projectedCellSize * 0.32f), 8, 14)
            };
            GUI.Box(objectiveRect, label, style);
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
                GUILayout.Label("Order bonus: 0 currency; deliveries earn sale income.");
                GUILayout.Label(GetDemoGuidance());
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Seeds")) section = MarketSection.Seeds;
            if (GUILayout.Button("Sales")) section = MarketSection.Sales;
            if (GUILayout.Button("History")) section = MarketSection.History;
            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private Rect GetDetailPanelRect() => new(16f,
            Mathf.Max(16f, Screen.height - Mathf.Min(320f, Screen.height * 0.5f) - 72f),
            Mathf.Min(310f, Screen.width - 32f),
            Mathf.Min(320f, Screen.height * 0.5f));

        private void DrawMarketDetail()
        {
            Rect rect = GetDetailPanelRect();
            GUI.Box(rect, GUIContent.none);
            GUILayout.BeginArea(new Rect(rect.x + 10f, rect.y + 8f,
                rect.width - 20f, rect.height - 16f));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Order")) section = MarketSection.Current;
            if (GUILayout.Button("Seeds")) section = MarketSection.Seeds;
            if (GUILayout.Button("Sales")) section = MarketSection.Sales;
            if (GUILayout.Button("History")) section = MarketSection.History;
            GUILayout.EndHorizontal();
            detailScroll = GUILayout.BeginScrollView(detailScroll);
            switch (section)
            {
                case MarketSection.Seeds:
                    GUILayout.Label($"Seeds  |  {market.Currency} currency");
                    foreach (SeedShopOffer offer in market.SeedShop.Offers)
                    {
                        SeedShopOfferState state = market.SeedShop.GetState(offer.CropId);
                        string label = offer.DisplayName +
                            (market.Unlocks.IsNew(UnlockKey.CropCategory, offer.CropId)
                                ? "  NEW" : "");
                        if (state == SeedShopOfferState.Affordable)
                        {
                            if (GUILayout.Button($"Buy {label} ({offer.Price})"))
                                market.SeedShop.TryPurchase(offer.CropId);
                        }
                        else if (state == SeedShopOfferState.Locked)
                            GUILayout.Label($"{label}: requires " +
                                $"{offer.RequiredUnlock?.Id ?? "an unlock"}");
                        else if (state == SeedShopOfferState.Available)
                            GUILayout.Label($"{label}: need " +
                                $"{Math.Max(0, offer.Price - market.Currency)} more coins");
                        else GUILayout.Label($"{label}: {state}");
                    }
                    break;
                case MarketSection.Sales:
                    GUILayout.Label($"Currency: {market.Currency}");
                    GUILayout.Label($"Total delivered: {market.Inventory.TotalDelivered}");
                    foreach (var entry in market.Inventory.DeliveredCounts)
                        GUILayout.Label($"{entry.Key.Id}: {entry.Value}");
                    break;
                case MarketSection.History:
                    GUILayout.Label($"Completed: {market.CompletedOrders.Count} / {market.Orders.Count}");
                    foreach (FoodOrder completed in market.CompletedOrders)
                    {
                        GUILayout.Label(completed.DisplayName);
                        foreach (UnlockKey unlock in completed.Unlocks)
                            GUILayout.Label($"  {unlock.Category}: {UnlockDisplayName(unlock)}");
                    }
                    GUILayout.Label($"Purchased territories: " +
                        market.Territories.PurchasedCoordinates.Count);
                    break;
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawWorldFeedback()
        {
            if (Camera.main == null) return;
            if (Time.time < orderFeedbackUntil && objectiveAnchorValid)
            {
                float projectedCellSize = buildings.GridSystem.CellSize *
                    Screen.height / (2f * Camera.main.orthographicSize);
                float width = Mathf.Min(210f,
                    market.Footprint.x * projectedCellSize * 0.86f);
                var style = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = Mathf.Clamp(Mathf.RoundToInt(projectedCellSize * 0.32f), 8, 14)
                };
                GUI.Label(new Rect(objectiveAnchor.x - width * 0.5f,
                    objectiveAnchor.y + 2f, width, 20f), "Order complete!", style);
            }
        }

        private void DrawTerritoryOverlays()
        {
            Camera camera = Camera.main;
            if (camera == null || buildings?.GridSystem == null || market == null) return;
            TerritorySystem territories = market.Territories;
            GetVisibleParcels(camera, out Vector2Int first, out Vector2Int last);
            for (int x = first.x; x <= last.x; x++)
                for (int y = first.y; y <= last.y; y++)
                    DrawTerritory(new Vector2Int(x, y), camera, territories);
        }

        private void DrawTerritory(Vector2Int coordinate, Camera camera,
            TerritorySystem territories)
        {
            TerritoryParcelVisualState state = territories.GetParcelVisualState(
                coordinate, market.Currency);
            if (state == TerritoryParcelVisualState.ReservedHub) return;
            Rect rect = GetTerritoryRect(coordinate, camera);
            if (rect.width < 28f || rect.height < 28f) return;
            if (state != TerritoryParcelVisualState.Purchasable) return;
            Rect action = GetTerritoryActionRect(coordinate, camera);
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && !buildings.BlocksAllWorldInput;
            string side = territories.ExpansionSideName(coordinate).ToUpperInvariant();
            if (GUI.Button(action, $"EXPAND {side}  ${territories.NextPurchaseCost}"))
            {
                selectedTerritory = coordinate;
                buildings.OpenPanel(BuildingPlacementController.DemoPanel.Region);
            }
            GUI.enabled = enabled;
        }

        private Rect GetTerritoryActionRect(Vector2Int coordinate, Camera camera)
        {
            Rect parcel = GetTerritoryRect(coordinate, camera);
            float width = Mathf.Min(140f, parcel.width - 4f);
            return new Rect(parcel.center.x - width * 0.5f,
                parcel.y + 4f, width, 30f);
        }

        private void GetVisibleParcels(Camera camera, out Vector2Int first, out Vector2Int last)
        {
            var grid = buildings.GridSystem;
            var territories = market.Territories;
            float depth = -camera.transform.position.z;
            first = territories.CoordinateAtCell(grid.WorldToGrid(
                camera.ViewportToWorldPoint(new Vector3(0f, 0f, depth))));
            last = territories.CoordinateAtCell(grid.WorldToGrid(
                camera.ViewportToWorldPoint(new Vector3(1f, 1f, depth))));
        }

        private void LateUpdate()
        {
            int used = 0;
            Camera camera = Camera.main;
            if (camera != null && market != null && buildings?.GridSystem != null)
            {
                var grid = buildings.GridSystem;
                var territories = market.Territories;
                GetVisibleParcels(camera, out Vector2Int first, out Vector2Int last);
                for (int x = first.x; x <= last.x; x++)
                    for (int y = first.y; y <= last.y; y++)
                    {
                        Vector2Int parcel = new(x, y);
                        TerritoryParcelVisualState state = territories.GetParcelVisualState(parcel, market.Currency);
                        if (state == TerritoryParcelVisualState.ReservedHub) continue;
                        if (used == territoryFills.Count)
                        {
                            var fill = new GameObject("Territory tint");
                            fill.transform.SetParent(transform, false);
                            var renderer = fill.AddComponent<SpriteRenderer>();
                            renderer.sprite = BuildingVisualFactory.PlaceholderSprite;
                            renderer.sortingOrder = -110;
                            territoryFills.Add(renderer);
                        }
                        SpriteRenderer view = territoryFills[used++];
                        view.gameObject.SetActive(true);
                        float centerOffset = (TerritoryWorldSettings.ParcelSize - 1) * 0.5f;
                        Vector3 center = grid.GridToWorld(territories.ParcelMinimumCell(parcel)) +
                            new Vector3(centerOffset, centerOffset, 0f) * grid.CellSize;
                        view.transform.position = center;
                        view.transform.localScale = Vector3.one *
                            (TerritoryWorldSettings.ParcelSize * grid.CellSize);
                        view.color = ParcelColor(state);
                    }
            }
            for (int index = used; index < territoryFills.Count; index++)
                territoryFills[index].gameObject.SetActive(false);
        }

        public static Color ParcelColor(TerritoryParcelVisualState state) => state switch
        {
            TerritoryParcelVisualState.Owned => new Color(0.48f, 0.82f, 0.43f, 0.16f),
            TerritoryParcelVisualState.Purchasable => new Color(0.95f, 0.80f, 0.25f, 0.20f),
            TerritoryParcelVisualState.Locked => new Color(0.43f, 0.47f, 0.52f, 0.14f),
            _ => Color.clear
        };

        private void OnDisable()
        {
            foreach (SpriteRenderer fill in territoryFills)
                if (fill != null) fill.gameObject.SetActive(false);
        }

        private Rect GetTerritoryRect(Vector2Int coordinate, Camera camera)
        {
            Vector2Int minimum = market.Territories.ParcelMinimumCell(coordinate);
            Vector2Int maximum = minimum + Vector2Int.one *
                (TerritoryWorldSettings.ParcelSize - 1);
            float half = buildings.GridSystem.CellSize * 0.5f;
            Vector3 lowerLeft = buildings.GridSystem.GridToWorld(minimum) +
                new Vector3(-half, -half, 0f);
            Vector3 upperRight = buildings.GridSystem.GridToWorld(maximum) +
                new Vector3(half, half, 0f);
            Vector3 a = camera.WorldToScreenPoint(lowerLeft);
            Vector3 b = camera.WorldToScreenPoint(upperRight);
            float x = Mathf.Min(a.x, b.x);
            float y = Screen.height - Mathf.Max(a.y, b.y);
            return new Rect(x, y, Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));
        }

        private bool GetTerritoryActionRectAtPointer(Vector2 pointer,
            out Vector2Int coordinate)
        {
            coordinate = default;
            if (Camera.main == null || market == null || buildings?.GridSystem == null)
                return false;
            TerritorySystem territories = market.Territories;
            GetVisibleParcels(Camera.main, out Vector2Int first, out Vector2Int last);
            for (int x = first.x; x <= last.x; x++)
                for (int y = first.y; y <= last.y; y++)
                {
                    var candidate = new Vector2Int(x, y);
                    if (territories.GetParcelVisualState(candidate, market.Currency) ==
                            TerritoryParcelVisualState.Purchasable &&
                        GetTerritoryActionRect(candidate, Camera.main).Contains(pointer))
                    {
                        coordinate = candidate;
                        return true;
                    }
                }
            return false;
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

        private void DrawTerritoryPanel()
        {
            if (!selectedTerritory.HasValue || market == null) return;
            Vector2Int coordinate = selectedTerritory.Value;
            TerritorySystem territories = market.Territories;
            TerritoryPurchaseStatus status = territories.GetPurchaseStatus(
                coordinate, market.Currency);

            Rect rect = GetRegionPanelRect();
            GUI.Box(rect, GUIContent.none);
            GUILayout.BeginArea(new Rect(rect.x + 10f, rect.y + 8f,
                rect.width - 20f, rect.height - 16f));
            if (buildings != null && buildings.IsFoodDemo)
                territoryScroll = GUILayout.BeginScrollView(territoryScroll);
            Vector2Int minimum = territories.ParcelMinimumCell(coordinate);
            int distance = Mathf.Abs(coordinate.x - territories.StartingTerritory.x) +
                Mathf.Abs(coordinate.y - territories.StartingTerritory.y);
            GUILayout.Label($"Territory {coordinate.x}, {coordinate.y}");
            GUILayout.Label($"Cells: {minimum.x}..{minimum.x + TerritoryWorldSettings.ParcelSize - 1}, " +
                $"{minimum.y}..{minimum.y + TerritoryWorldSettings.ParcelSize - 1}");
            if (status == TerritoryPurchaseStatus.Owned)
                GUILayout.Label("Owned · all normal construction is allowed inside.");
            else
            {
                GUILayout.Label($"Distance from start: {distance} parcels");
                GUILayout.Label(status == TerritoryPurchaseStatus.NotAdjacent
                    ? "Unavailable: purchase a cardinally adjacent territory first."
                    : $"Next expansion price: {territories.NextPurchaseCost} " +
                        $"(currency: {market.Currency}).");
                if (status == TerritoryPurchaseStatus.Unaffordable)
                    GUILayout.Label("Not enough currency. This parcel remains frontier territory.");
                bool originalEnabled = GUI.enabled;
                GUI.enabled = originalEnabled && status == TerritoryPurchaseStatus.Available;
                if (GUILayout.Button($"Purchase {territories.ExpansionSideName(coordinate)} side " +
                        $"({territories.ExpansionParcelCount(coordinate)} parcels) " +
                        $"for {territories.NextPurchaseCost}"))
                {
                    if (buildings != null && buildings.TryPurchaseTerritory(coordinate))
                    {
                        regionMessage = null;
                    }
                    else regionMessage = "Purchase failed.";
                }
                GUI.enabled = originalEnabled;
            }
            if (!string.IsNullOrEmpty(regionMessage)) GUILayout.Label(regionMessage);
            if (GUILayout.Button("Close"))
            {
                if (buildings != null && buildings.IsFoodDemo) buildings.ClosePanel();
                else selectedTerritory = null;
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
                "O1" => "Start: place a Farm Plot inside your owned 9x9 territory, " +
                    "choose Carrot, cover it with a Harvester, and belt carrots to Market. " +
                    "Use the Hotbar or Build Menu, R to rotate, Esc to exit build mode.",
                "O2" => "Build a Processor. In its default rotation, " +
                    "belt carrots in from the west and collect Heat with a Collector " +
                    "and Pipe to the south property port. Belt its north output to Market.",
                "O3" => "Grow Tomato and Onion. Feed separate belts " +
                    "to the Mixer's two west inputs in default rotation, then belt " +
                    "its east output to Market.",
                "O4" => "Grow Potato in a purchased territory. In default Cutter " +
                    "rotation, feed its rear from the south and connect a westbound " +
                    "and eastbound belt to its front sides. One potato makes two slices.",
                "O5" => "Pipe Heat into a Processor and feed it Potato Slice. " +
                    "Belt French Fries to Market; the next order reuses Tomato Sauce.",
                "O6" => "Feed Tomato Sauce into a Processor supplied with Water. " +
                    "Belt Tomato Soup to Market.",
                "O7" => "Feed French Fries and Tomato Sauce into a Mixer. " +
                    "Keep Sauce available for Soup as well as Loaded Fries.",
                "O8" => "Feed Loaded Fries and Tomato Soup into a Mixer. " +
                    "Deliver Garden Lunch to unlock Chicken Village trading.",
                "O9" => "Build the Chicken Trading Center, choose Garden Lunch for Egg, " +
                    "and connect a Garden Lunch belt to a left input port. Connect a bottom " +
                    "output port to Market and deliver 500 Eggs.",
                "O10" => "Feed Egg and Tomato Sauce to the Mixer's two inputs. " +
                    "Route Tomato Omelette to Market to complete Chapter 1.",
                _ => market.ActiveOrder == null
                    ? "Chapter 1 Complete. Your factory has completed the vegetable campaign."
                    : "Build a food production line and deliver its output to Market."
            };
        }

        private static string UnlockDisplayName(UnlockKey unlock) =>
            unlock?.Category == UnlockKey.MachineCategory &&
            unlock.Id == nameof(TradeBuilding) ? "Chicken Trading Center" : unlock?.Id;

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
            contentHeight += 28f;
            if (buildings != null)
            {
                contentHeight += buildings.IsFoodDemo ? 0f : 94f;
            }
            float height = Mathf.Min(contentHeight, Mathf.Max(16f, Screen.height * 0.5f));
            return new Rect(16f, Mathf.Max(16f, Screen.height - height - 16f),
                300f, height);
        }
    }
}
