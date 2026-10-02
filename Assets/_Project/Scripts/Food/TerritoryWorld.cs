using System;
using System.Collections.Generic;
using System.Linq;
using CozyFoodFactory.Buildings;
using UnityEngine;

namespace CozyFoodFactory.Food
{
    public enum TerritoryPurchaseStatus
    {
        Owned,
        ReservedHub,
        NotAdjacent,
        Unaffordable,
        Available
    }

    public enum TerritoryParcelVisualState
    {
        ReservedHub,
        Owned,
        Purchasable,
        Locked
    }

    [Serializable]
    public sealed class PropertyDistanceWeights
    {
        [Min(0f)] public float heat;
        [Min(0f)] public float water;
        [Min(0f)] public float time;
        [Min(0f)] public float cold;

        public PropertyDistanceWeights(float heat, float water, float time, float cold)
        {
            this.heat = heat;
            this.water = water;
            this.time = time;
            this.cold = cold;
        }

        public float Total => heat + water + time + cold;
    }

    [Serializable]
    public sealed class TerritoryWorldSettings
    {
        public const int ParcelSize = 9;
        public const int GenerationVersion = 5;

        [SerializeField] private int worldSeed = 428173;
        [SerializeField] private Vector2Int hubParcelMinimumCell = Vector2Int.zero;
        [SerializeField] private Vector2Int hubTerritory = Vector2Int.zero;
        [SerializeField] private Vector2Int startingTerritory = new(0, -1);
        [SerializeField] private int[] expansionCosts =
            { 100, 180, 320, 550, 900, 1500 };
        [SerializeField] private PropertyDistanceWeights nearFrontier =
            new(0.45f, 0.45f, 0.05f, 0.05f);
        [SerializeField] private PropertyDistanceWeights middleDistance =
            new(0.35f, 0.35f, 0.15f, 0.15f);
        [SerializeField] private PropertyDistanceWeights farDistance =
            new(0.2f, 0.2f, 0.3f, 0.3f);

        public int WorldSeed => worldSeed;
        public Vector2Int HubTerritory => hubTerritory;
        public Vector2Int StartingTerritory => startingTerritory;
        public Vector2Int HubParcelMinimumCell => hubParcelMinimumCell;
        public IReadOnlyList<int> ExpansionCosts => expansionCosts;
        public int CostForExpansionCount(int expansionCount)
        {
            if (expansionCount < 0) throw new ArgumentOutOfRangeException(nameof(expansionCount));
            if (expansionCount < expansionCosts.Length) return expansionCosts[expansionCount];
            long previous = expansionCosts[Math.Max(0, expansionCosts.Length - 2)];
            long current = expansionCosts[expansionCosts.Length - 1];
            for (int tier = expansionCosts.Length; tier <= expansionCount; tier++)
            {
                long next = previous + current;
                if (next >= int.MaxValue) return int.MaxValue;
                previous = current;
                current = next;
            }
            return (int)current;
        }
        public PropertyDistanceWeights WeightsAtDistance(int distance) => distance switch
        {
            <= 1 => nearFrontier,
            2 => middleDistance,
            _ => farDistance
        };

        public void Validate(Vector2Int marketCell, Vector2Int marketFootprint)
        {
            if (expansionCosts == null || expansionCosts.Length == 0 ||
                expansionCosts.Any(cost => cost <= 0) ||
                nearFrontier == null || middleDistance == null || farDistance == null ||
                nearFrontier.Total <= 0f || middleDistance.Total <= 0f ||
                farDistance.Total <= 0f ||
                !ValidWeights(nearFrontier) || !ValidWeights(middleDistance) ||
                !ValidWeights(farDistance) ||
                startingTerritory != hubTerritory + Vector2Int.down ||
                !ContainsFootprint(hubTerritory, marketCell, marketFootprint))
                throw new InvalidOperationException("Territory world settings are invalid.");
        }

        private static bool ValidWeights(PropertyDistanceWeights weights) =>
            new[] { weights.heat, weights.water, weights.time, weights.cold }
                .All(value => value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value));

        private bool ContainsFootprint(Vector2Int territory, Vector2Int anchor,
            Vector2Int footprint)
        {
            Vector2Int minimum = hubParcelMinimumCell +
                (territory - hubTerritory) * ParcelSize;
            return anchor.x >= minimum.x && anchor.y >= minimum.y &&
                anchor.x + footprint.x <= minimum.x + ParcelSize &&
                anchor.y + footprint.y <= minimum.y + ParcelSize;
        }
    }

    [Serializable]
    public sealed class SavedTerritory
    {
        public int x;
        public int y;

        public SavedTerritory() { }
        public SavedTerritory(Vector2Int coordinate) { x = coordinate.x; y = coordinate.y; }
        public Vector2Int Coordinate => new(x, y);
    }

    [Serializable]
    public sealed class SavedPropertySource
    {
        public int x;
        public int y;
        public CookingProperty property;
        public int capacity;

        public SavedPropertySource() { }
        public SavedPropertySource(PropertySourceSetup source)
        {
            x = source.cell.x;
            y = source.cell.y;
            property = source.property;
            capacity = source.capacity;
        }

        public Vector2Int Cell => new(x, y);
        public PropertySourceSetup ToSetup() => new()
        {
            cell = Cell,
            property = property,
            capacity = capacity
        };
    }

    public sealed class TerritorySystem
    {
        private static readonly Vector2Int[] Neighbors =
        {
            Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left
        };

        private readonly TerritoryWorldSettings settings;
        private readonly HashSet<Vector2Int> purchased = new();
        private int expansionCount;

        public TerritorySystem(TerritoryWorldSettings settings) =>
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));

        public event Action<Vector2Int> Purchased;
        public int WorldSeed { get; private set; }
        public int ExpansionCount => expansionCount;
        public int NextPurchaseCost => settings.CostForExpansionCount(ExpansionCount);
        public IReadOnlyCollection<Vector2Int> PurchasedCoordinates => purchased;
        public IEnumerable<Vector2Int> RelevantCoordinates => purchased
            .SelectMany(cell => Neighbors.Select(offset => cell + offset).Append(cell))
            .Where(cell => !IsReservedHub(cell)).Distinct()
            .OrderBy(cell => cell.x).ThenBy(cell => cell.y);
        public Vector2Int ReservedHubTerritory => settings.HubTerritory;
        public Vector2Int StartingTerritory => settings.StartingTerritory;
        public TerritoryWorldSettings Settings => settings;

        public TerritorySystem Initialize()
        {
            WorldSeed = settings.WorldSeed;
            if (purchased.Count == 0)
                for (int x = -1; x <= 1; x++)
                    for (int y = -1; y <= 1; y++)
                    {
                        Vector2Int parcel = settings.HubTerritory + new Vector2Int(x, y);
                        if (parcel != settings.HubTerritory) purchased.Add(parcel);
                    }
            return this;
        }

        public Vector2Int ParcelMinimumCell(Vector2Int coordinate) =>
            settings.HubParcelMinimumCell +
            (coordinate - settings.HubTerritory) * TerritoryWorldSettings.ParcelSize;

        public Vector2Int CoordinateAtCell(Vector2Int cell) => new(
            settings.HubTerritory.x + FloorDivide(
                cell.x - settings.HubParcelMinimumCell.x, TerritoryWorldSettings.ParcelSize),
            settings.HubTerritory.y + FloorDivide(
                cell.y - settings.HubParcelMinimumCell.y, TerritoryWorldSettings.ParcelSize));

        public bool IsPurchased(Vector2Int coordinate) => purchased.Contains(coordinate);
        public bool IsReservedHub(Vector2Int coordinate) =>
            coordinate == settings.HubTerritory;

        public TerritoryParcelVisualState GetParcelVisualState(Vector2Int coordinate,
            long currency)
        {
            if (IsReservedHub(coordinate)) return TerritoryParcelVisualState.ReservedHub;
            return GetPurchaseStatus(coordinate, currency) switch
            {
                TerritoryPurchaseStatus.Owned => TerritoryParcelVisualState.Owned,
                TerritoryPurchaseStatus.Available => TerritoryParcelVisualState.Purchasable,
                TerritoryPurchaseStatus.Unaffordable => TerritoryParcelVisualState.Purchasable,
                _ => TerritoryParcelVisualState.Locked
            };
        }

        public bool IsOwnedCell(Vector2Int cell) => IsPurchased(CoordinateAtCell(cell));

        public bool IsBuildableCell(Vector2Int cell) => IsOwnedCell(cell) ||
            IsReservedHub(CoordinateAtCell(cell));

        public bool ContainsFootprint(Vector2Int anchor, Vector2Int footprint,
            BuildingRotation rotation)
        {
            Vector2Int size = ((int)rotation % 180 == 0)
                ? footprint : new Vector2Int(footprint.y, footprint.x);
            for (int x = anchor.x; x < anchor.x + size.x; x++)
                for (int y = anchor.y; y < anchor.y + size.y; y++)
                {
                    var cell = new Vector2Int(x, y);
                    if (!IsOwnedCell(cell))
                        return false;
                }
            return true;
        }

        public bool ContainsBuildableFootprint(Vector2Int anchor, Vector2Int footprint,
            BuildingRotation rotation)
        {
            Vector2Int size = ((int)rotation % 180 == 0) ? footprint :
                new Vector2Int(footprint.y, footprint.x);
            for (int x = anchor.x; x < anchor.x + size.x; x++)
                for (int y = anchor.y; y < anchor.y + size.y; y++)
                {
                    Vector2Int cell = new(x, y);
                    if (!IsBuildableCell(cell)) return false;
                }
            return true;
        }

        public TerritoryPurchaseStatus GetPurchaseStatus(Vector2Int coordinate,
            long currency)
        {
            if (IsReservedHub(coordinate)) return TerritoryPurchaseStatus.ReservedHub;
            if (purchased.Contains(coordinate)) return TerritoryPurchaseStatus.Owned;
            if (!HasPurchasedNeighbor(coordinate))
                return TerritoryPurchaseStatus.NotAdjacent;
            return currency < NextPurchaseCost
                ? TerritoryPurchaseStatus.Unaffordable
                : TerritoryPurchaseStatus.Available;
        }

        public bool HasPurchasedNeighbor(Vector2Int coordinate) =>
            TryGetExpansionSide(coordinate, out _);

        public bool TryPurchase(Vector2Int coordinate, MarketInventory inventory)
        {
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            int cost = NextPurchaseCost;
            if (GetPurchaseStatus(coordinate, inventory.Currency) !=
                    TerritoryPurchaseStatus.Available ||
                cost > 0 && !inventory.TrySpendCurrency(cost))
                return false;
            GetBounds(out int minX, out int maxX, out int minY, out int maxY);
            if (coordinate.y == maxY + 1)
                for (int x = minX; x <= maxX; x++) purchased.Add(new Vector2Int(x, maxY + 1));
            else if (coordinate.y == minY - 1)
                for (int x = minX; x <= maxX; x++) purchased.Add(new Vector2Int(x, minY - 1));
            else if (coordinate.x == maxX + 1)
                for (int y = minY; y <= maxY; y++) purchased.Add(new Vector2Int(maxX + 1, y));
            else if (coordinate.x == minX - 1)
                for (int y = minY; y <= maxY; y++) purchased.Add(new Vector2Int(minX - 1, y));
            expansionCount++;
            Purchased?.Invoke(coordinate);
            return true;
        }

        private bool TryGetExpansionSide(Vector2Int coordinate, out Vector2Int side)
        {
            side = default;
            if (purchased.Count == 0 || purchased.Contains(coordinate)) return false;
            GetBounds(out int minX, out int maxX, out int minY, out int maxY);
            if (coordinate.x >= minX && coordinate.x <= maxX && coordinate.y == maxY + 1)
                side = Vector2Int.up;
            else if (coordinate.x >= minX && coordinate.x <= maxX && coordinate.y == minY - 1)
                side = Vector2Int.down;
            else if (coordinate.y >= minY && coordinate.y <= maxY && coordinate.x == maxX + 1)
                side = Vector2Int.right;
            else if (coordinate.y >= minY && coordinate.y <= maxY && coordinate.x == minX - 1)
                side = Vector2Int.left;
            return side != default;
        }

        public string ExpansionSideName(Vector2Int coordinate) =>
            TryGetExpansionSide(coordinate, out Vector2Int side) ? side switch
            {
                { x: 0, y: 1 } => "North",
                { x: 0, y: -1 } => "South",
                { x: 1, y: 0 } => "East",
                _ => "West"
            } : string.Empty;

        public int ExpansionParcelCount(Vector2Int coordinate)
        {
            if (!TryGetExpansionSide(coordinate, out Vector2Int side)) return 0;
            GetBounds(out int minX, out int maxX, out int minY, out int maxY);
            return side.x == 0 ? maxX - minX + 1 : maxY - minY + 1;
        }

        private void GetBounds(out int minX, out int maxX, out int minY, out int maxY)
        {
            minX = purchased.Min(cell => cell.x);
            maxX = purchased.Max(cell => cell.x);
            minY = purchased.Min(cell => cell.y);
            maxY = purchased.Max(cell => cell.y);
        }

        public void Restore(int seed, IReadOnlyList<SavedTerritory> saved,
            int purchaseCount)
        {
            if (saved == null || saved.Count < 1)
                throw new ArgumentException("The save is missing its starting territory.");
            var restored = new HashSet<Vector2Int>();
            foreach (SavedTerritory item in saved)
                if (item == null || !restored.Add(item.Coordinate))
                    throw new ArgumentException("Saved territories contain duplicates.");
            if (restored.Contains(settings.HubTerritory) ||
                !restored.Contains(settings.StartingTerritory) ||
                Enumerable.Range(settings.HubTerritory.x - 1, 3)
                    .SelectMany(x => Enumerable.Range(settings.HubTerritory.y - 1, 3)
                    .Select(y => new Vector2Int(x, y)))
                    .Where(cell => cell != settings.HubTerritory)
                    .Any(cell => !restored.Contains(cell)))
                throw new ArgumentException("Saved territory ownership or purchase count is invalid.");
            int minX = restored.Min(cell => cell.x);
            int maxX = restored.Max(cell => cell.x);
            int minY = restored.Min(cell => cell.y);
            int maxY = restored.Max(cell => cell.y);
            int width = maxX - minX + 1;
            int height = maxY - minY + 1;
            if (minX > settings.HubTerritory.x - 1 || maxX < settings.HubTerritory.x + 1 ||
                minY > settings.HubTerritory.y - 1 || maxY < settings.HubTerritory.y + 1 ||
                restored.Count != width * height - 1 ||
                Enumerable.Range(minX, width).SelectMany(x => Enumerable.Range(minY, height)
                    .Select(y => new Vector2Int(x, y)))
                    .Where(cell => cell != settings.HubTerritory)
                    .Any(cell => !restored.Contains(cell)) ||
                purchaseCount != width - 3 + height - 3)
                throw new ArgumentException("Saved territories do not match side expansion bounds.");
            purchased.Clear();
            foreach (Vector2Int coordinate in restored) purchased.Add(coordinate);
            expansionCount = purchaseCount;
            WorldSeed = seed;
        }

        public SavedTerritory[] Capture() => purchased.OrderBy(item => item.x)
            .ThenBy(item => item.y).Select(item => new SavedTerritory(item)).ToArray();

        private static int FloorDivide(int value, int divisor) =>
            value >= 0 ? value / divisor : (value - divisor + 1) / divisor;
    }

    public sealed class PropertyWorldGenerator
    {
        private readonly TerritoryWorldSettings settings;

        public PropertyWorldGenerator(TerritoryWorldSettings settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public PropertySourceSetup[] Generate(int seed, Vector2Int territory)
        {
            int hubDistance = Math.Abs(territory.x - settings.HubTerritory.x) +
                Math.Abs(territory.y - settings.HubTerritory.y);
            if (hubDistance <= 1 || territory == settings.StartingTerritory)
                return Array.Empty<PropertySourceSetup>();

            var random = new System.Random(CombineSeed(seed, territory));
            int distance = Math.Abs(territory.x - settings.StartingTerritory.x) +
                Math.Abs(territory.y - settings.StartingTerritory.y);
            CookingProperty property = territory == settings.StartingTerritory + Vector2Int.right
                ? CookingProperty.Heat
                : territory == settings.StartingTerritory + Vector2Int.left
                    ? CookingProperty.Water
                    : ChooseProperty(settings.WeightsAtDistance(distance), random.NextDouble());
            Vector2Int minimum = settings.HubParcelMinimumCell +
                (territory - settings.HubTerritory) * TerritoryWorldSettings.ParcelSize;
            var available = new List<Vector2Int>();
            for (int x = 2; x <= 6; x++)
                for (int y = 2; y <= 6; y++) available.Add(minimum + new Vector2Int(x, y));
            var cluster = new List<Vector2Int> { available[random.Next(available.Count)] };
            available.Remove(cluster[0]);
            int size = random.Next(4, 7);
            while (cluster.Count < size)
            {
                Vector2Int[] frontier = available.Where(cell => cluster.Any(other =>
                    Math.Abs(cell.x - other.x) + Math.Abs(cell.y - other.y) == 1)).ToArray();
                Vector2Int next = frontier[random.Next(frontier.Length)];
                cluster.Add(next);
                available.Remove(next);
            }
            return cluster.OrderBy(cell => cell.x).ThenBy(cell => cell.y)
                .Select(cell => new PropertySourceSetup
                {
                    cell = cell, property = property, capacity = 4
                }).ToArray();
        }

        public static CookingProperty ChooseProperty(PropertyDistanceWeights weights,
            double roll)
        {
            if (weights == null || weights.Total <= 0f || roll < 0d || roll >= 1d)
                throw new ArgumentOutOfRangeException(nameof(roll));
            double value = roll * weights.Total;
            if (value < weights.heat) return CookingProperty.Heat;
            value -= weights.heat;
            if (value < weights.water) return CookingProperty.Water;
            value -= weights.water;
            if (value < weights.time) return CookingProperty.Time;
            return CookingProperty.Cold;
        }

        private static int CombineSeed(int seed, Vector2Int coordinate)
        {
            unchecked
            {
                int value = seed;
                value = value * 397 ^ coordinate.x;
                value = value * 397 ^ coordinate.y;
                value = value * 397 ^ TerritoryWorldSettings.GenerationVersion;
                return value;
            }
        }
    }
}
