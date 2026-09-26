using System;
using System.Collections.Generic;
using FantasyShapez.Buildings;
using UnityEngine;

namespace FantasyShapez.Food
{
    public sealed class FarmPlotPlacementBehavior : MonoBehaviour, IBuildingPlacementBehavior
    {
        private RegionState configuredRegions;

        public void Configure(RegionState regions)
        {
            configuredRegions = regions ?? throw new ArgumentNullException(nameof(regions));
        }

        public bool CanPlace(
            Vector2Int anchorCell,
            Vector2Int footprint,
            BuildingRotation rotation)
        {
            RegionState regions = configuredRegions ??
                GetComponent<BuildingPlacementController>()?.Market?.Regions;
            return footprint == Vector2Int.one && regions?.CanFarm(anchorCell) == true;
        }

        public void InitializePlacedBuilding(GameObject buildingObject, BuildingPlacement placement)
        {
            FarmPlot farmPlot = buildingObject.GetComponent<FarmPlot>();
            if (farmPlot == null)
            {
                throw new InvalidOperationException(
                    "The Farm Plot prefab must contain a FarmPlot component.");
            }

            farmPlot.Initialize(placement.AnchorCell,
                GetComponent<BuildingPlacementController>()?.Market?.Unlocks);
        }
    }

    [Serializable]
    public sealed class FarmableRegion
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private Vector2Int minimumCell;
        [SerializeField] private Vector2Int size;
        [SerializeField] private bool initiallyRestored;
        [SerializeField] private string requiredUnlockCategory;
        [SerializeField] private string requiredUnlockId;
        [SerializeField] private UnlockKey[] restorationUnlocks = Array.Empty<UnlockKey>();
        [SerializeField, Min(0)] private int price;

        public FarmableRegion(string id, string displayName, Vector2Int minimumCell,
            Vector2Int size, bool initiallyRestored,
            UnlockKey requiredUnlock = null, UnlockKey[] restorationUnlocks = null,
            int price = 0)
        {
            this.id = id;
            this.displayName = displayName;
            this.minimumCell = minimumCell;
            this.size = size;
            this.initiallyRestored = initiallyRestored;
            requiredUnlockCategory = requiredUnlock?.Category;
            requiredUnlockId = requiredUnlock?.Id;
            this.restorationUnlocks = restorationUnlocks ?? Array.Empty<UnlockKey>();
            this.price = price;
            Validate();
        }

        public string Id => id;
        public string DisplayName => displayName;
        public Vector2Int MinimumCell => minimumCell;
        public Vector2Int Size => size;
        public int Price => price;
        public int FarmableCellCount => size.x * size.y;
        public bool InitiallyRestored => initiallyRestored;
        public bool HasRequirement => !string.IsNullOrWhiteSpace(requiredUnlockCategory);
        public string RequiredUnlockCategory => requiredUnlockCategory;
        public string RequiredUnlockId => requiredUnlockId;
        public IReadOnlyList<UnlockKey> RestorationUnlocks =>
            restorationUnlocks ?? Array.Empty<UnlockKey>();

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(displayName) ||
                size.x <= 0 || size.y <= 0 || price < 0 ||
                string.IsNullOrWhiteSpace(requiredUnlockCategory) !=
                string.IsNullOrWhiteSpace(requiredUnlockId))
            {
                throw new InvalidOperationException("The farmable region is invalid.");
            }
            foreach (UnlockKey reward in RestorationUnlocks)
            {
                if (reward == null)
                    throw new InvalidOperationException("A restoration reward is missing.");
                reward.Validate();
            }
        }

        public bool Contains(Vector2Int cell) =>
            cell.x >= minimumCell.x && cell.y >= minimumCell.y &&
            cell.x - minimumCell.x < size.x && cell.y - minimumCell.y < size.y;

        public bool SharesEdge(FarmableRegion other)
        {
            int right = minimumCell.x + size.x;
            int top = minimumCell.y + size.y;
            int otherRight = other.minimumCell.x + other.size.x;
            int otherTop = other.minimumCell.y + other.size.y;
            return (right == other.minimumCell.x || otherRight == minimumCell.x) &&
                minimumCell.y < otherTop && other.minimumCell.y < top ||
                (top == other.minimumCell.y || otherTop == minimumCell.y) &&
                minimumCell.x < otherRight && other.minimumCell.x < right;
        }
    }

    public enum RegionStatus
    {
        Locked,
        Restorable,
        Restored
    }

    public enum RegionPurchaseStatus
    {
        ProgressionLocked,
        NotAdjacent,
        Unaffordable,
        Available,
        Restored
    }

    public sealed class RegionState
    {
        private readonly UnlockState unlocks;
        private readonly Dictionary<string, FarmableRegion> regionsById =
            new(StringComparer.Ordinal);
        private readonly IReadOnlyList<FarmableRegion> regions;

        public RegionState(IReadOnlyList<FarmableRegion> regions, UnlockState unlocks)
        {
            if (regions == null)
            {
                throw new ArgumentNullException(nameof(regions));
            }

            this.unlocks = unlocks ?? throw new ArgumentNullException(nameof(unlocks));
            var copiedRegions = new List<FarmableRegion>(regions.Count);
            foreach (FarmableRegion region in regions)
            {
                if (region == null)
                {
                    throw new ArgumentException("A region definition is missing.", nameof(regions));
                }

                region.Validate();
                if (!regionsById.TryAdd(region.Id, region))
                {
                    throw new ArgumentException("Region IDs must be unique.", nameof(regions));
                }

                foreach (FarmableRegion previous in copiedRegions)
                {
                    if (region.MinimumCell.x < previous.MinimumCell.x + previous.Size.x &&
                        previous.MinimumCell.x < region.MinimumCell.x + region.Size.x &&
                        region.MinimumCell.y < previous.MinimumCell.y + previous.Size.y &&
                        previous.MinimumCell.y < region.MinimumCell.y + region.Size.y)
                        throw new ArgumentException("Farmable regions overlap.", nameof(regions));
                }
                copiedRegions.Add(region);
            }

            this.regions = copiedRegions.AsReadOnly();
            foreach (FarmableRegion region in copiedRegions)
            {
                if (region.InitiallyRestored)
                {
                    unlocks.Grant(new UnlockKey(UnlockKey.RegionCategory, region.Id));
                    foreach (UnlockKey reward in region.RestorationUnlocks)
                        unlocks.Grant(reward);
                }

            }
        }

        public IReadOnlyList<FarmableRegion> Regions => regions;

        public FarmableRegion GetRegionAt(Vector2Int cell)
        {
            foreach (FarmableRegion region in regions)
                if (region.Contains(cell)) return region;
            return null;
        }

        public RegionPurchaseStatus GetPurchaseStatus(string regionId, long currency)
        {
            RegionStatus status = GetStatus(regionId);
            if (status == RegionStatus.Restored) return RegionPurchaseStatus.Restored;
            if (status == RegionStatus.Locked) return RegionPurchaseStatus.ProgressionLocked;
            FarmableRegion candidate = regionsById[regionId];
            bool adjacent = false;
            foreach (FarmableRegion region in regions)
            {
                if (GetStatus(region.Id) == RegionStatus.Restored &&
                    candidate.SharesEdge(region))
                {
                    adjacent = true;
                    break;
                }
            }
            if (!adjacent) return RegionPurchaseStatus.NotAdjacent;
            return currency < candidate.Price ? RegionPurchaseStatus.Unaffordable :
                RegionPurchaseStatus.Available;
        }

        public bool TryPurchase(string regionId, MarketInventory inventory)
        {
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            if (GetPurchaseStatus(regionId, inventory.Currency) != RegionPurchaseStatus.Available)
                return false;
            FarmableRegion region = regionsById[regionId];
            // All eligibility checks precede the currency mutation.
            if (region.Price > 0 && !inventory.TrySpendCurrency(region.Price)) return false;
            return TryRestore(regionId);
        }

        public RegionStatus GetStatus(string regionId)
        {
            if (regionId == null || !regionsById.TryGetValue(regionId,
                    out FarmableRegion region))
            {
                throw new ArgumentException("The region is not defined.", nameof(regionId));
            }

            if (unlocks.IsUnlocked(UnlockKey.RegionCategory, regionId))
            {
                return RegionStatus.Restored;
            }

            return !region.HasRequirement ||
                unlocks.IsUnlocked(region.RequiredUnlockCategory, region.RequiredUnlockId)
                ? RegionStatus.Restorable : RegionStatus.Locked;
        }

        public bool TryRestore(string regionId)
        {
            if (GetStatus(regionId) != RegionStatus.Restorable ||
                GetPurchaseStatus(regionId, long.MaxValue) != RegionPurchaseStatus.Available)
            {
                return false;
            }

            FarmableRegion region = regionsById[regionId];
            if (!unlocks.Grant(new UnlockKey(UnlockKey.RegionCategory, regionId)))
                return false;
            foreach (UnlockKey reward in region.RestorationUnlocks)
                unlocks.Grant(reward);
            return true;
        }

        public bool CanFarm(Vector2Int cell)
        {
            foreach (FarmableRegion region in regions)
            {
                if (region.Contains(cell) &&
                    GetStatus(region.Id) == RegionStatus.Restored)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
