using System;
using System.Collections.Generic;
using System.Linq;
using CozyFoodFactory.Food;
using CozyFoodFactory.Logistics;

namespace CozyFoodFactory.Buildings
{
    // Construction only: runtime food, buffers, timers and progression never enter history.
    public sealed class ConstructionLayout
    {
        public SavedBuilding[] Buildings { get; private set; }
        public SavedPropertyConnection[] Connections { get; private set; }

        public static ConstructionLayout FromWorld(FactoryWorldData world) => new()
        {
            Buildings = world.buildings.Select(CopyBuilding).ToArray(),
            Connections = world.connections.Select(CopyConnection).ToArray()
        };

        private static SavedBuilding CopyBuilding(SavedBuilding source)
        {
            var result = new SavedBuilding
            {
                definitionId = source.definitionId, x = source.x, y = source.y,
                rotation = source.rotation
            };
            switch (source.definitionId)
            {
                case nameof(FarmPlot):
                    result.farmPlot = new SavedFarmPlot { cropId = source.farmPlot?.cropId };
                    break;
                case nameof(Harvester): result.harvester = new SavedHarvester
                    { outputs = Array.Empty<SavedFood>() }; break;
                case nameof(Belt): result.belt = new SavedBelt(); break;
                case nameof(Processor): result.processor = new SavedProcessor(); break;
                case nameof(BasicMixer): result.mixer = new SavedMixer(); break;
                case nameof(TradeBuilding):
                    result.tradeBuilding = new SavedTradeBuilding { tradeId = source.tradeBuilding?.tradeId };
                    break;
                case nameof(Cutter): result.cutter = new SavedCutter(); break;
                default: throw new ArgumentException("Unsupported construction type.");
            }
            return result;
        }

        private static SavedPropertyConnection CopyConnection(SavedPropertyConnection source) =>
            new()
            {
                x = source.x, y = source.y,
                sourceX = source.sourceX, sourceY = source.sourceY,
                property = source.property, kind = source.kind, units = source.units
            };

        public ConstructionChange Difference(ConstructionLayout after)
        {
            var beforeBuildings = Buildings.ToDictionary(BuildingKey);
            var afterBuildings = after.Buildings.ToDictionary(BuildingKey);
            var beforeConnections = Connections.ToDictionary(ConnectionKey);
            var afterConnections = after.Connections.ToDictionary(ConnectionKey);
            var before = new ConstructionLayout
            {
                Buildings = Buildings.Where(item =>
                    !afterBuildings.TryGetValue(BuildingKey(item), out SavedBuilding other) ||
                    !Same(item, other)).ToArray(),
                Connections = Connections.Where(item =>
                    !afterConnections.TryGetValue(ConnectionKey(item),
                        out SavedPropertyConnection other) || !Same(item, other)).ToArray()
            };
            var added = new ConstructionLayout
            {
                Buildings = after.Buildings.Where(item =>
                    !beforeBuildings.TryGetValue(BuildingKey(item), out SavedBuilding other) ||
                    !Same(item, other)).ToArray(),
                Connections = after.Connections.Where(item =>
                    !beforeConnections.TryGetValue(ConnectionKey(item),
                        out SavedPropertyConnection other) || !Same(item, other)).ToArray()
            };
            return new ConstructionChange(before, added);
        }

        public static (string, int, int) BuildingKey(SavedBuilding item) =>
            (item.definitionId, item.x, item.y);
        public static (int, int) ConnectionKey(SavedPropertyConnection item) =>
            (item.x, item.y);
        public static bool Same(SavedBuilding a, SavedBuilding b) =>
            a.definitionId == b.definitionId && a.x == b.x && a.y == b.y &&
            a.rotation == b.rotation && a.farmPlot?.cropId == b.farmPlot?.cropId;
        public static bool Same(SavedPropertyConnection a, SavedPropertyConnection b) =>
            a.x == b.x && a.y == b.y && a.sourceX == b.sourceX &&
            a.sourceY == b.sourceY && a.property == b.property &&
            a.kind == b.kind && a.units == b.units;
    }

    public sealed class ConstructionChange
    {
        public ConstructionChange(ConstructionLayout before, ConstructionLayout after)
        {
            Before = before;
            After = after;
        }
        public ConstructionLayout Before { get; }
        public ConstructionLayout After { get; }
        public bool IsEmpty => Before.Buildings.Length == 0 &&
            Before.Connections.Length == 0 && After.Buildings.Length == 0 &&
            After.Connections.Length == 0;
    }

    public sealed class ConstructionHistory
    {
        private const int Limit = 50;
        private readonly List<ConstructionChange> undo = new();
        private readonly List<ConstructionChange> redo = new();
        public int UndoCount => undo.Count;
        public int RedoCount => redo.Count;

        public void Record(ConstructionChange change)
        {
            if (change == null || change.IsEmpty) return;
            undo.Add(change);
            if (undo.Count > Limit) undo.RemoveAt(0);
            redo.Clear();
        }

        public bool TryUndo(Func<ConstructionLayout, ConstructionLayout, bool> apply)
        {
            if (undo.Count == 0) return false;
            ConstructionChange change = undo[^1];
            if (!apply(change.After, change.Before)) return false;
            undo.RemoveAt(undo.Count - 1);
            redo.Add(change);
            return true;
        }

        public bool TryRedo(Func<ConstructionLayout, ConstructionLayout, bool> apply)
        {
            if (redo.Count == 0) return false;
            ConstructionChange change = redo[^1];
            if (!apply(change.Before, change.After)) return false;
            redo.RemoveAt(redo.Count - 1);
            undo.Add(change);
            return true;
        }

        public void Clear() { undo.Clear(); redo.Clear(); }
    }
}
