using System;
using System.Collections.Generic;
using System.Linq;
using CozyFoodFactory.Buildings;
using CozyFoodFactory.CameraControl;
using CozyFoodFactory.Grid;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CozyFoodFactory.Food
{
    [Serializable]
    public sealed class PropertySourceSetup
    {
        public CookingProperty property;
        public Vector2Int cell;
        [Min(1)] public int capacity = 4;
    }

    [Serializable]
    public sealed class PropertyVisualDefinition
    {
        public Sprite source;
        public Sprite collector;
        public Sprite pipe;
    }

    // Temporary Play Mode controls for source, pipe, and test-demand placement.
    public sealed class PropertySupplyPlayMode
    {
        private enum Tool { None, Collector, Pipe, TestDemand, Remove }

        private readonly CookingPropertyNetwork network = new();
        private readonly GridSystem grid;
        private readonly GridHoverHighlight hover;
        private readonly GridOccupancy occupancy;
        private readonly Transform visualParent;
        private readonly PropertyVisualDefinition visualDefinition;
        private readonly FactoryCameraController cameraController;
        private readonly Dictionary<Vector2Int, BuildingPlacement> reservations = new();
        private readonly Dictionary<Vector2Int, GameObject> visuals = new();
        private readonly Dictionary<Vector2Int, GameObject> processorPortVisuals = new();
        private readonly List<Vector2Int> sources = new();
        private readonly Dictionary<Vector2Int, Vector2Int> processorPorts = new();
        private readonly GridDragTracker pipeDrag = new();
        private readonly List<Vector2Int> pipePath = new();
        private GameObject pipePreview;
        private string pipePreviewReason;
        private readonly bool debugTools;
        private readonly Func<CookingProperty, bool> sourceAvailable;
        private Tool tool;
        private bool isVisible;
        private int selectedSourceIndex;
        private string message = "Choose a tool, then click a grid cell.";
        private bool constructionBatch;
        public event Action ConstructionStarting;
        public event Action ConstructionFinished;

        public PropertySupplyPlayMode(GridSystem grid, GridHoverHighlight hover,
            GridOccupancy occupancy, Transform parent,
            IReadOnlyList<PropertySourceSetup> setups, bool debugTools = true,
            PropertyVisualDefinition visualDefinition = null,
            Func<CookingProperty, bool> sourceAvailable = null)
        {
            this.debugTools = debugTools;
            this.sourceAvailable = sourceAvailable;
            this.grid = grid;
            this.hover = hover;
            this.occupancy = occupancy;
            visualParent = parent;
            this.visualDefinition = visualDefinition;
            cameraController = Camera.main != null
                ? Camera.main.GetComponent<FactoryCameraController>() : null;
            if (setups == null)
            {
                return;
            }

            foreach (PropertySourceSetup setup in setups)
            {
                if (setup == null || !Reserve(setup.cell, "PropertySource",
                        out BuildingPlacement reservation))
                {
                    Debug.LogWarning("A property source could not reserve its grid cell.");
                    continue;
                }

                if (!network.TryAddSource(setup.cell, setup.property, setup.capacity))
                {
                    occupancy.Remove(reservation);
                    reservations.Remove(setup.cell);
                    Debug.LogWarning($"Invalid property source at {setup.cell}.");
                    continue;
                }

                sources.Add(setup.cell);
                CreateVisual(setup.cell);
            }
        }

        public bool IsActive => tool != Tool.None;
        public bool IsPipeDragging => tool == Tool.Pipe && pipeDrag.IsActive;

        public bool IsVisible => isVisible;

        public string Message => message;

        public SavedPropertyConnection[] CaptureWorldConnections() =>
            network.Connections
                .Where(connection => connection.Kind != PropertyConnectionKind.Source &&
                    !processorPorts.ContainsKey(connection.Cell))
                .OrderBy(connection => connection.Cell.x)
                .ThenBy(connection => connection.Cell.y)
                .Select(connection => new SavedPropertyConnection
                {
                    x = connection.Cell.x,
                    y = connection.Cell.y,
                    sourceX = connection.SourceCell.x,
                    sourceY = connection.SourceCell.y,
                    property = connection.Property,
                    kind = connection.Kind,
                    units = connection.Units
                }).ToArray();

        public void RestoreWorldConnections(IReadOnlyList<SavedPropertyConnection> saved)
        {
            foreach (SavedPropertyConnection connection in saved)
            {
                Vector2Int cell = new(connection.x, connection.y);
                Vector2Int source = new(connection.sourceX, connection.sourceY);
                if (!Reserve(cell, connection.kind.ToString(), out BuildingPlacement reservation))
                {
                    throw new InvalidOperationException($"Cannot reserve property cell {cell}.");
                }

                var restored = new PropertyConnection(cell, source,
                    connection.property, connection.kind, connection.units);
                if (!network.TryRestoreConnection(restored))
                {
                    occupancy.Remove(reservation);
                    reservations.Remove(cell);
                    throw new InvalidOperationException($"Cannot restore property connection at {cell}.");
                }

                CreateVisual(cell);
            }

            RefreshProcessorPorts();
        }

        public void TogglePanel()
        {
            isVisible = !isVisible;
            if (!isVisible)
            {
                tool = Tool.None;
                CancelPipeDrag();
            }
        }

        public void ExitTool()
        {
            tool = Tool.None;
            CancelPipeDrag();
        }

        public void CancelPipeDrag()
        {
            pipeDrag.Reset();
            pipePath.Clear();
            pipePreviewReason = null;
            if (pipePreview != null)
            {
                DestroyVisual(pipePreview);
                pipePreview = null;
            }
        }

        public IReadOnlyList<bool> PreviewPipePath(IReadOnlyList<Vector2Int> cells)
        {
            var valid = new bool[cells.Count];
            CookingPropertyNetwork preview = network.CopyForPreview();
            for (int index = 0; index < cells.Count; index++)
                valid[index] = occupancy.CanPlace(cells[index], Vector2Int.one,
                        BuildingRotation.Degrees0) &&
                        HasAvailableSelectedSource() &&
                        preview.TryAddPipe(cells[index], sources[selectedSourceIndex]);
            return valid;
        }

        private bool TryPlacePipe(Vector2Int cell, Vector2Int sourceCell) =>
            IsSourceAvailable(sourceCell) && TryPlaceConnection(cell, "Pipe",
                () => network.TryAddPipe(cell, sourceCell));

        public bool HasPipeAt(Vector2Int cell) =>
            network.TryGetConnection(cell, out PropertyConnection connection) &&
            connection.Kind is PropertyConnectionKind.Pipe or PropertyConnectionKind.Collector;

        public bool CanRemoveWithoutBreakingDependents(
            IReadOnlyCollection<Vector2Int> removedCells)
        {
            var removed = new HashSet<Vector2Int>(removedCells);
            PropertyConnection[] connected = network.Connections.Where(item =>
                !removed.Contains(item.Cell) && item.Kind != PropertyConnectionKind.Source &&
                network.IsConnectedToSource(item.Cell)).ToArray();
            CookingPropertyNetwork preview = network.CopyForPreview();
            foreach (Vector2Int cell in removed) preview.Remove(cell);
            return connected.All(item => preview.IsConnectedToSource(item.Cell));
        }

        public bool ContainsPlacement(BuildingPlacement placement) =>
            placement != null &&
            reservations.TryGetValue(placement.AnchorCell, out BuildingPlacement stored) &&
            ReferenceEquals(stored, placement);

        public bool TryGetClipboardConnection(BuildingPlacement placement,
            out PropertyConnection connection)
        {
            connection = default;
            return ContainsPlacement(placement) &&
                network.TryGetConnection(placement.AnchorCell, out connection);
        }

        public bool TryPlanClipboardConnections(
            IReadOnlyList<PropertyGroupCopyItem> items, Vector2Int anchor,
            ISet<Vector2Int> buildingCells,
            out IReadOnlyList<PropertyGroupCopyItem> ordered)
            => TryPlanClipboardConnectionsCore(items, anchor, buildingCells,
                null, out ordered);

        public bool TryPlanClipboardConnections(
            IReadOnlyList<PropertyGroupCopyItem> items, Vector2Int anchor,
            ISet<Vector2Int> buildingCells,
            IReadOnlyList<Vector2Int> removedAfterPaste,
            out IReadOnlyList<PropertyGroupCopyItem> ordered)
        {
            if (!TryPlanClipboardConnectionsCore(items, anchor, buildingCells,
                    null, out ordered)) return false;
            return removedAfterPaste == null || removedAfterPaste.Count == 0 ||
                TryPlanClipboardConnectionsCore(items, anchor, buildingCells,
                    removedAfterPaste, out _);
        }

        private bool TryPlanClipboardConnectionsCore(
            IReadOnlyList<PropertyGroupCopyItem> items, Vector2Int anchor,
            ISet<Vector2Int> buildingCells,
            IReadOnlyList<Vector2Int> removedAfterPaste,
            out IReadOnlyList<PropertyGroupCopyItem> ordered)
        {
            var plan = new List<PropertyGroupCopyItem>(items.Count);
            var remaining = new List<PropertyGroupCopyItem>(items);
            CookingPropertyNetwork preview = network.CopyForPreview();
            if (removedAfterPaste != null)
                foreach (Vector2Int cell in removedAfterPaste)
                    preview.Remove(cell);
            var targetCells = new HashSet<Vector2Int>();
            foreach (PropertyGroupCopyItem item in items)
            {
                Vector2Int cell = anchor + item.Offset;
                if (!targetCells.Add(cell) || buildingCells.Contains(cell) ||
                    !occupancy.CanPlace(cell, Vector2Int.one, BuildingRotation.Degrees0))
                {
                    ordered = null;
                    return false;
                }
            }
            while (remaining.Count > 0)
            {
                bool progressed = false;
                for (int index = 0; index < remaining.Count; index++)
                {
                    PropertyGroupCopyItem item = remaining[index];
                    Vector2Int cell = anchor + item.Offset;
                    bool added = false;
                    if (item.Connection.Kind == PropertyConnectionKind.Collector)
                    {
                        foreach (PropertyConnection source in preview.Connections)
                        {
                            if (source.Kind != PropertyConnectionKind.Source ||
                                source.Property != item.Connection.Property ||
                                Mathf.Abs(source.Cell.x - cell.x) +
                                Mathf.Abs(source.Cell.y - cell.y) != 1) continue;
                            if (preview.TryAddCollector(cell, source.Cell))
                            { added = true; break; }
                        }
                    }
                    else if (item.Connection.Kind == PropertyConnectionKind.Pipe)
                        added = preview.TryAddPipe(cell);
                    if (!added || !preview.TryGetConnection(cell,
                            out PropertyConnection placed) ||
                        placed.Property != item.Connection.Property)
                    {
                        if (added) preview.Remove(cell);
                        continue;
                    }
                    plan.Add(new PropertyGroupCopyItem(placed, item.Offset));
                    remaining.RemoveAt(index--);
                    progressed = true;
                }
                if (progressed) continue;
                ordered = null;
                return false;
            }
            ordered = plan;
            return true;
        }

        public bool TryPlaceClipboardConnection(PropertyGroupCopyItem item,
            Vector2Int anchor)
        {
            Vector2Int cell = anchor + item.Offset;
            return item.Connection.Kind switch
            {
                PropertyConnectionKind.Collector =>
                    TryPlaceCollector(cell, item.Connection.SourceCell),
                PropertyConnectionKind.Pipe => TryPlacePipe(cell),
                _ => false
            };
        }

        public void RegisterProcessorPort(Vector2Int cell, Vector2Int outsideCell)
        {
            if (!processorPorts.TryAdd(cell, outsideCell))
            {
                throw new InvalidOperationException($"Processor property port already registered at {cell}.");
            }

            RefreshProcessorPorts();
            processorPortVisuals.Add(cell, CreateStatusVisual(cell));
        }

        public void UnregisterProcessorPort(Vector2Int cell)
        {
            if (processorPorts.Remove(cell))
            {
                network.Remove(cell);
                DestroyVisual(processorPortVisuals[cell]);
                processorPortVisuals.Remove(cell);
                RefreshProcessorPorts();
            }
        }

        public bool TryGetProcessorSupply(Vector2Int cell, out CookingProperty property)
        {
            if (processorPorts.ContainsKey(cell) &&
                network.TryGetConnection(cell, out PropertyConnection connection) &&
                connection.Kind == PropertyConnectionKind.Demand &&
                network.IsSupplied(cell))
            {
                property = connection.Property;
                return true;
            }

            property = default;
            return false;
        }

        public bool TryGetSourceStatus(Vector2Int sourceCell,
            out PropertySupplyStatus status) => network.TryGetStatus(sourceCell, out status);

        public string GetProcessorSupplyMessage(Vector2Int cell)
        {
            if (!processorPorts.TryGetValue(cell, out Vector2Int outsideCell))
            {
                return "Property port is not registered.";
            }

            if (!network.TryGetConnection(outsideCell, out PropertyConnection outside) ||
                outside.Kind is not (PropertyConnectionKind.Pipe or PropertyConnectionKind.Collector))
            {
                return $"Connect a property pipe at {outsideCell}.";
            }

            if (!network.TryGetConnection(cell, out PropertyConnection demand))
            {
                return $"Property pipe at {outsideCell} cannot connect to this port.";
            }

            if (!network.IsConnectedToSource(cell))
            {
                return $"{demand.Property} pipe disconnected from source.";
            }

            network.TryGetStatus(demand.SourceCell, out PropertySupplyStatus status);
            if (!status.IsWithinCapacity)
            {
                return $"{demand.Property} over capacity: {status.ConnectedDemand}/{status.Capacity} demand.";
            }

            return $"{demand.Property} supplied: {status.AvailableCapacity}/{status.Capacity} capacity free.";
        }

        public bool TryPlaceCollector(Vector2Int cell, Vector2Int sourceCell) =>
            IsSourceAvailable(sourceCell) &&
            TryPlaceConnection(cell, "Collector", () => network.TryAddCollector(cell, sourceCell));

        public bool TryPlacePipe(Vector2Int cell) =>
            CanUsePipeSource(cell) &&
            TryPlaceConnection(cell, "Pipe", () => network.TryAddPipe(cell));

        public bool TryRemoveConnection(Vector2Int cell)
        {
            if (!constructionBatch) ConstructionStarting?.Invoke();
            try { return RemoveConnectionCore(cell); }
            finally { if (!constructionBatch) ConstructionFinished?.Invoke(); }
        }

        private bool RemoveConnectionCore(Vector2Int cell)
        {
            if (processorPorts.ContainsKey(cell))
            {
                message = "Processor demand is automatic. Remove its pipe or Processor.";
                return false;
            }

            if (!reservations.TryGetValue(cell, out BuildingPlacement reservation) ||
                !visuals.TryGetValue(cell, out GameObject visual) || !network.Remove(cell))
            {
                message = "Only placed collectors, pipes and test loads can be removed.";
                return false;
            }

            occupancy.Remove(reservation);
            reservations.Remove(cell);
            DestroyVisual(visual);
            visuals.Remove(cell);
            RefreshProcessorPorts();
            message = $"Removed connection at {cell}.";
            return true;
        }

        public void HandleInput()
        {
            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            {
                ExitTool();
                return;
            }

            if (tool == Tool.Pipe)
            {
                HandlePipeDrag();
                return;
            }
            CancelPipeDrag();

            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame ||
                IsPointerOverPanel())
            {
                return;
            }

            Vector2Int cell = hover.HoveredCell;
            if (tool == Tool.Remove)
            {
                TryRemoveConnection(cell);
                return;
            }

            bool added = tool switch
            {
                Tool.Collector => HasAvailableSelectedSource() &&
                    TryPlaceCollector(cell, sources[selectedSourceIndex]),
                Tool.Pipe => TryPlacePipe(cell),
                Tool.TestDemand => TryPlaceConnection(cell, "Test load",
                    () => network.TryAddDemand(cell)),
                _ => false
            };
            if (!added)
            {
                return;
            }
        }

        private void HandlePipeDrag()
        {
            if (Mouse.current == null) return;
            if (!Mouse.current.leftButton.isPressed)
            {
                if (pipeDrag.IsActive && !IsPointerOverPanel())
                {
                    IReadOnlyList<bool> valid = PreviewPipePath(pipePath);
                    ConstructionStarting?.Invoke();
                    constructionBatch = true;
                    try
                    {
                        for (int index = 0; index < pipePath.Count; index++)
                            if (valid[index] && HasAvailableSelectedSource())
                                TryPlacePipe(pipePath[index], sources[selectedSourceIndex]);
                    }
                    finally
                    {
                        constructionBatch = false;
                        ConstructionFinished?.Invoke();
                    }
                }
                CancelPipeDrag();
                return;
            }
            if (IsPointerOverPanel() ||
                !Mouse.current.leftButton.wasPressedThisFrame && !pipeDrag.IsActive)
                return;
            foreach (Vector2Int cell in pipeDrag.Continue(hover.HoveredCell))
                pipePath.Add(cell);
            RefreshPipePreview();
        }

        private void RefreshPipePreview()
        {
            if (pipePreview != null) DestroyVisual(pipePreview);
            pipePreview = new GameObject("Pipe drag preview");
            pipePreview.transform.SetParent(visualParent, false);
            IReadOnlyList<bool> valid = PreviewPipePath(pipePath);
            pipePreviewReason = null;
            for (int index = 0; index < pipePath.Count; index++)
            {
                Vector2Int cell = pipePath[index];
                var marker = new GameObject($"Pipe {cell}");
                marker.transform.SetParent(pipePreview.transform, false);
                marker.transform.position = grid.GridToWorld(cell) +
                    new Vector3(0f, 0f, -0.08f);
                SpriteRenderer renderer = marker.AddComponent<SpriteRenderer>();
                Sprite artwork = visualDefinition?.pipe;
                renderer.sprite = artwork != null ? artwork :
                    BuildingVisualFactory.PlaceholderSprite;
                Vector2 previewSize = artwork != null ? artwork.bounds.size :
                    Vector2.one;
                marker.transform.localScale = new Vector3(
                    grid.CellSize * 0.55f / Mathf.Max(0.001f, previewSize.x),
                    grid.CellSize * 0.55f / Mathf.Max(0.001f, previewSize.y), 1f);
                renderer.color = valid[index]
                    ? new Color(0.3f, 0.95f, 0.5f, 0.65f)
                    : new Color(1f, 0.25f, 0.2f, 0.7f);
                renderer.sortingOrder = 70;
                if (!valid[index])
                    pipePreviewReason = occupancy.CanPlace(cell, Vector2Int.one,
                            BuildingRotation.Degrees0)
                        ? "Start at a Collector or connected Pipe; keep one Property Source."
                        : "Cannot overlap another building.";
            }
        }

        private bool TryPlaceConnection(Vector2Int cell, string kind, Func<bool> add)
        {
            if (!constructionBatch) ConstructionStarting?.Invoke();
            try { return PlaceConnectionCore(cell, kind, add); }
            finally { if (!constructionBatch) ConstructionFinished?.Invoke(); }
        }

        private bool PlaceConnectionCore(Vector2Int cell, string kind, Func<bool> add)
        {
            if (!Reserve(cell, kind, out BuildingPlacement reservation))
            {
                message = $"Cell {cell} is occupied.";
                return false;
            }

            if (!add())
            {
                occupancy.Remove(reservation);
                reservations.Remove(cell);
                message = kind switch
                {
                    "Collector" => "Collector must touch its selected source without joining another source.",
                    "Pipe" => "Pipe must touch a Collector or Pipe from one source; sources cannot merge.",
                    _ => "Test load must touch a Collector or Pipe from one source."
                };
                return false;
            }

            CreateVisual(cell);
            RefreshProcessorPorts();
            message = $"Placed {kind} at {cell}.";
            return true;
        }

        public void DrawGUI()
        {
            RefreshConnectionVisuals();
            if (!isVisible)
            {
                if (debugTools)
                    GUI.Label(new Rect(Screen.width - 150f, 12f, 138f, 22f),
                        "F8: Property Debug");
                return;
            }

            const float width = 310f;
            var panel = new Rect(Screen.width - width - 12f, 12f, width, 350f);
            GUILayout.BeginArea(panel, GUI.skin.box);
            GUILayout.Label(debugTools ? "Property supply prototype" :
                "Property connections");
            GUILayout.Label(debugTools ? $"Tool: {tool}  |  Hover: {hover.HoveredCell}" :
                $"Tool: {tool}");
            if (HasAvailableSelectedSource())
            {
                Vector2Int selected = sources[selectedSourceIndex];
                PropertyConnection source = GetConnection(selected);
                if (GUILayout.Button(debugTools
                        ? $"Collector source: {source.Property} {selected}"
                        : $"Collector source: {source.Property}"))
                {
                    do { selectedSourceIndex = (selectedSourceIndex + 1) % sources.Count; }
                    while (!IsSourceAvailable(sources[selectedSourceIndex]));
                }
            }
            else GUILayout.Label("Complete the Carrot order to use Heat.");
            if (!debugTools)
                GUILayout.Label("Water follows French Fries; Time and Cold are for later chapters.");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Collector")) SelectTool(Tool.Collector);
            if (GUILayout.Button("Pipe")) SelectTool(Tool.Pipe);
            if (debugTools && GUILayout.Button("Test load")) SelectTool(Tool.TestDemand);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Remove")) SelectTool(Tool.Remove);
            if (GUILayout.Button("Exit (Esc)")) ExitTool();
            GUILayout.EndHorizontal();
            GUILayout.Label(debugTools ?
                "Select a tool, then click the map. Test loads use 1 capacity." :
                "Select a source and tool, then click the map.");
            GUILayout.Label("Processor property port connects through its open corner.");
            GUILayout.Label("Property color: connected  |  Gray: disconnected");
            GUILayout.Label("Amber: over capacity  |  Green/red: demand supplied/not");
            if (debugTools) foreach (Vector2Int cell in sources)
            {
                PropertyConnection source = GetConnection(cell);
                network.TryGetStatus(cell, out PropertySupplyStatus status);
                string state = status.IsWithinCapacity ? "available" : "over capacity";
                GUILayout.Label($"{source.Property} {cell}: {status.ConnectedConsumers} consumers, " +
                    $"{status.ConnectedDemand}/{status.Capacity} demand, {state}");
            }

            GUILayout.Label(message);
            GUILayout.EndArea();
            if (!string.IsNullOrEmpty(pipePreviewReason))
            {
                Vector2 pointer = Mouse.current?.position.ReadValue() ?? Vector2.zero;
                const float tooltipWidth = 250f;
                GUIStyle style = new(GUI.skin.box) { wordWrap = true };
                float height = Mathf.Max(30f,
                    style.CalcHeight(new GUIContent(pipePreviewReason), tooltipWidth));
                float x = Mathf.Clamp(pointer.x + 16f, 8f,
                    Mathf.Max(8f, Screen.width - tooltipWidth - 8f));
                float y = Mathf.Clamp(Screen.height - pointer.y + 16f, 8f,
                    Mathf.Max(8f, Screen.height - height - 8f));
                GUI.Box(new Rect(x, y, tooltipWidth, height), pipePreviewReason, style);
            }
        }

        private void SelectTool(Tool selected)
        {
            CancelPipeDrag();
            tool = selected;
        }

        private PropertyConnection GetConnection(Vector2Int cell)
        {
            network.TryGetConnection(cell, out PropertyConnection connection);
            return connection;
        }

        private void RefreshProcessorPorts()
        {
            foreach (Vector2Int cell in processorPorts.Keys)
            {
                network.Remove(cell);
            }

            foreach (KeyValuePair<Vector2Int, Vector2Int> port in processorPorts)
            {
                network.TryAddDemand(port.Key, 1, port.Value);
            }
        }

        private bool IsSourceAvailable(Vector2Int cell) =>
            network.TryGetConnection(cell, out PropertyConnection connection) &&
            connection.Kind == PropertyConnectionKind.Source &&
            (sourceAvailable?.Invoke(connection.Property) ?? true);

        private bool CanUsePipeSource(Vector2Int cell)
        {
            CookingPropertyNetwork preview = network.CopyForPreview();
            return preview.TryAddPipe(cell) &&
                preview.TryGetConnection(cell, out PropertyConnection connection) &&
                IsSourceAvailable(connection.SourceCell);
        }

        private bool HasAvailableSelectedSource()
        {
            if (sources.Count == 0) return false;
            if (IsSourceAvailable(sources[selectedSourceIndex])) return true;
            for (int index = 0; index < sources.Count; index++)
                if (IsSourceAvailable(sources[index]))
                {
                    selectedSourceIndex = index;
                    return true;
                }
            return false;
        }

        private bool Reserve(Vector2Int cell, string id,
            out BuildingPlacement reservation)
        {
            if (!occupancy.TryRegister(id, cell, Vector2Int.one,
                    BuildingRotation.Degrees0, out reservation))
            {
                return false;
            }

            reservations.Add(cell, reservation);
            return true;
        }

        private void CreateVisual(Vector2Int cell)
        {
            PropertyConnection connection = GetConnection(cell);
            var visual = new GameObject($"{connection.Property} {connection.Kind} {cell}");
            visual.transform.SetParent(visualParent, false);
            visual.transform.position = grid.GridToWorld(cell) + new Vector3(0f, 0f, -0.02f);
            float size = connection.Kind switch
            {
                PropertyConnectionKind.Source => 0.85f,
                PropertyConnectionKind.Collector => 0.65f,
                PropertyConnectionKind.Pipe => 0.42f,
                _ => 0.68f
            };
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            Sprite artwork = GetVisualSprite(connection.Kind);
            renderer.sprite = artwork != null ? artwork :
                BuildingVisualFactory.PlaceholderSprite;
            Vector2 artSize = artwork != null ? artwork.bounds.size :
                Vector2.one;
            visual.transform.localScale = new Vector3(
                grid.CellSize * size / Mathf.Max(0.001f, artSize.x),
                grid.CellSize * size / Mathf.Max(0.001f, artSize.y), 1f);
            renderer.sortingOrder = 12;
            renderer.color = GetColor(connection.Property);
            if (connection.Kind == PropertyConnectionKind.Collector)
                visual.AddComponent<PropertyActivityVisual>().Initialize(grid.CellSize);
            visuals.Add(cell, visual);
        }

        private Sprite GetVisualSprite(PropertyConnectionKind kind) => kind switch
        {
            PropertyConnectionKind.Source => visualDefinition?.source,
            PropertyConnectionKind.Collector => visualDefinition?.collector,
            PropertyConnectionKind.Pipe => visualDefinition?.pipe,
            _ => null
        };

        private GameObject CreateStatusVisual(Vector2Int cell)
        {
            var visual = new GameObject($"Processor property status {cell}");
            visual.transform.SetParent(visualParent, false);
            visual.transform.position = grid.GridToWorld(cell) + new Vector3(0f, 0f, -0.05f);
            visual.transform.localScale = Vector3.one * (grid.CellSize * 0.25f);
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = BuildingVisualFactory.PlaceholderSprite;
            renderer.sortingOrder = 20;
            return visual;
        }

        private void RefreshConnectionVisuals()
        {
            foreach (PropertyConnection connection in network.Connections)
            {
                if (!visuals.TryGetValue(connection.Cell, out GameObject visual))
                {
                    continue;
                }

                Color color;
                if (connection.Kind == PropertyConnectionKind.Demand)
                {
                    color = network.IsSupplied(connection.Cell)
                        ? new Color(0.25f, 0.9f, 0.4f)
                        : new Color(0.95f, 0.2f, 0.2f);
                }
                else if (!network.IsConnectedToSource(connection.Cell))
                {
                    color = Color.gray;
                }
                else if (network.TryGetStatus(connection.SourceCell,
                             out PropertySupplyStatus status) && !status.IsWithinCapacity)
                {
                    color = new Color(1f, 0.65f, 0.15f);
                }
                else
                {
                    color = GetColor(connection.Property);
                }

                visual.GetComponent<SpriteRenderer>().color = color;
                if (connection.Kind == PropertyConnectionKind.Collector &&
                    visual.TryGetComponent(out PropertyActivityVisual activity))
                    activity.SetState(network.IsConnectedToSource(connection.Cell) &&
                        network.TryGetStatus(connection.SourceCell,
                            out PropertySupplyStatus collectorStatus) &&
                        collectorStatus.IsWithinCapacity &&
                        collectorStatus.ConnectedConsumers > 0,
                        cameraController?.InformationLevel ?? WorldInformationLevel.Close,
                        hover.HoveredCell == connection.Cell && !IsPointerOverPanel());
            }

            foreach (KeyValuePair<Vector2Int, GameObject> port in processorPortVisuals)
            {
                port.Value.GetComponent<SpriteRenderer>().color =
                    TryGetProcessorSupply(port.Key, out _)
                        ? new Color(0.25f, 0.9f, 0.4f)
                        : new Color(0.95f, 0.2f, 0.2f);
            }
        }

        private static void DestroyVisual(GameObject visual)
        {
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(visual);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(visual);
            }
        }

        private static Color GetColor(CookingProperty property)
        {
            return property switch
            {
                CookingProperty.Heat => new Color(1f, 0.42f, 0.2f),
                CookingProperty.Water => new Color(0.2f, 0.65f, 1f),
                CookingProperty.Time => new Color(0.75f, 0.65f, 0.4f),
                CookingProperty.Cold => new Color(0.7f, 0.9f, 0.95f),
                _ => Color.white
            };
        }

        public bool IsPointerOverPanel()
        {
            if (!isVisible || Mouse.current == null)
            {
                return false;
            }

            Vector2 pointer = Mouse.current.position.ReadValue();
            pointer.y = Screen.height - pointer.y;
            return new Rect(Screen.width - 322f, 12f, 310f, 350f)
                .Contains(pointer);
        }
    }
}
