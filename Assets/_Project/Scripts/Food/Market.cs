using System;
using System.Collections.Generic;
using FantasyShapez.Buildings;
using FantasyShapez.Logistics;
using UnityEngine;

namespace FantasyShapez.Food
{
    public sealed class Market : MonoBehaviour
    {
        [SerializeField] private BeltTransportCoordinator transportCoordinator = null;
        [SerializeField] private Vector2Int inputCell = new(10, 4);
        [SerializeField] private string lastDeliveryDebug = string.Empty;
        [SerializeField] private FoodOrder[] orders = Array.Empty<FoodOrder>();
        [SerializeField] private SeedShopOffer[] seedOffers = Array.Empty<SeedShopOffer>();
        [SerializeField] private FarmableRegion[] farmableRegions =
            Array.Empty<FarmableRegion>();

        private MarketReceiver receiver;
        private FoodOrderSequence orderSequence;
        private SeedShop seedShop;
        private RegionState regions;
        private readonly UnlockState unlocks = new();

        public Vector2Int InputCell => inputCell;

        public Vector2Int Footprint => Vector2Int.one;

        public MarketInventory Inventory => receiver?.Inventory;

        public long Currency => Inventory?.Currency ?? 0;

        public IReadOnlyList<FoodOrder> Orders => orderSequence?.Orders ?? orders;
        public IReadOnlyList<FoodOrder> CompletedOrders =>
            orderSequence?.CompletedOrders ?? Array.Empty<FoodOrder>();
        public FoodOrderProgress ActiveOrder => orderSequence?.ActiveOrder;
        public UnlockState Unlocks => unlocks;
        public SeedShop SeedShop => seedShop;
        public RegionState Regions => regions;
        public FoodOrderSequence OrderSequence => orderSequence;

        public string LastDeliveryMessage => lastDeliveryDebug;
        public event Action<FoodItemData, int> FoodDelivered;

        private void Awake()
        {
            if (transportCoordinator == null)
            {
                throw new MissingReferenceException("The Market requires a Belt Transport Coordinator.");
            }

            receiver = new MarketReceiver(inputCell, new MarketInventory());
            receiver.FoodDelivered += HandleFoodDelivered;
            orderSequence = new FoodOrderSequence(orders, receiver, unlocks);
            seedShop = new SeedShop(seedOffers, receiver.Inventory, unlocks);
            regions = new RegionState(farmableRegions, unlocks);
            transportCoordinator.RegisterInputReceiver(receiver);
            CreatePlaceholderVisual();
        }

        private void HandleFoodDelivered(FoodItemData food, int count)
        {
            string kind = food.Kind == FoodItemKind.RawIngredient ? "raw" : "processed";
            lastDeliveryDebug =
                $"Delivered {food.Id} ({kind}): {count} (+{food.SellValue} currency)";
            FoodDelivered?.Invoke(food, count);
        }

        private void OnDestroy()
        {
            if (receiver == null)
            {
                return;
            }

            receiver.FoodDelivered -= HandleFoodDelivered;
            orderSequence?.Dispose();
            transportCoordinator?.UnregisterInputReceiver(receiver);
        }

        private void CreatePlaceholderVisual()
        {
            CreateVisualPart("Market Body", Vector2.zero, new Vector2(0.85f, 0.85f),
                new Color(0.2f, 0.7f, 0.55f, 1f), 8);
            CreateVisualPart("Market Core", Vector2.zero, new Vector2(0.38f, 0.38f),
                new Color(1f, 0.85f, 0.4f, 1f), 9);
            foreach (GridDirection direction in new[]
                { GridDirection.North, GridDirection.East, GridDirection.South, GridDirection.West })
            {
                Vector2 position = -(Vector2)direction.ToOffset() * 0.42f;
                Vector2 scale = direction is GridDirection.East or GridDirection.West
                    ? new Vector2(0.12f, 0.3f)
                    : new Vector2(0.3f, 0.12f);
                CreateVisualPart($"Input {direction}", position, scale,
                    BuildingPortPreviewLayouts.InputColor, 10);
            }
        }

        private void CreateVisualPart(
            string name, Vector2 position, Vector2 scale, Color color, int sortingOrder)
        {
            var part = new GameObject(name);
            part.transform.SetParent(transform, false);
            part.transform.localPosition = new Vector3(position.x, position.y, 0f);
            part.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            SpriteRenderer renderer = part.AddComponent<SpriteRenderer>();
            renderer.sprite = BuildingVisualFactory.PlaceholderSprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
        }
    }

    [Serializable]
    public sealed class SeedShopOffer
    {
        [SerializeField] private string cropId;
        [SerializeField] private string displayName;
        [SerializeField, Min(1)] private int price;
        [SerializeField] private UnlockKey requiredUnlock;

        public SeedShopOffer(string cropId, string displayName, int price,
            UnlockKey requiredUnlock = null)
        {
            this.cropId = cropId;
            this.displayName = displayName;
            this.price = price;
            this.requiredUnlock = requiredUnlock;
            Validate();
        }

        public string CropId => cropId;
        public string DisplayName => displayName;
        public int Price => price;
        public UnlockKey RequiredUnlock => requiredUnlock;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(cropId) ||
                string.IsNullOrWhiteSpace(displayName) || price <= 0)
            {
                throw new InvalidOperationException("The seed shop offer is incomplete.");
            }

            requiredUnlock?.Validate();
        }
    }

    public enum SeedShopOfferState
    {
        Locked,
        Available,
        Affordable,
        Purchased,
        AlreadyUnlocked
    }

    public sealed class SeedShop
    {
        private readonly MarketInventory inventory;
        private readonly UnlockState unlocks;
        private readonly Dictionary<string, SeedShopOffer> offersByCrop = new(StringComparer.Ordinal);
        private readonly HashSet<string> purchased = new(StringComparer.Ordinal);
        private readonly IReadOnlyList<SeedShopOffer> offers;

        public SeedShop(IReadOnlyList<SeedShopOffer> offers,
            MarketInventory inventory, UnlockState unlocks)
        {
            if (offers == null)
            {
                throw new ArgumentNullException(nameof(offers));
            }

            this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            this.unlocks = unlocks ?? throw new ArgumentNullException(nameof(unlocks));
            var copiedOffers = new List<SeedShopOffer>(offers.Count);
            foreach (SeedShopOffer offer in offers)
            {
                if (offer == null)
                {
                    throw new ArgumentException("A seed shop offer is missing.", nameof(offers));
                }

                offer.Validate();
                if (!offersByCrop.TryAdd(offer.CropId, offer))
                {
                    throw new ArgumentException("Seed shop crop IDs must be unique.",
                        nameof(offers));
                }

                copiedOffers.Add(offer);
            }

            this.offers = copiedOffers.AsReadOnly();
        }

        public IReadOnlyList<SeedShopOffer> Offers => offers;
        public IReadOnlyCollection<string> PurchasedCropIds => purchased;

        public void RestorePurchases(IReadOnlyCollection<string> cropIds)
        {
            if (cropIds == null)
            {
                throw new ArgumentNullException(nameof(cropIds));
            }

            foreach (string cropId in cropIds)
            {
                if (!offersByCrop.ContainsKey(cropId))
                {
                    throw new ArgumentException("Unknown purchased crop.", nameof(cropIds));
                }
            }

            purchased.Clear();
            foreach (string cropId in cropIds)
            {
                purchased.Add(cropId);
            }
        }

        public SeedShopOfferState GetState(string cropId)
        {
            if (cropId == null || !offersByCrop.TryGetValue(cropId, out SeedShopOffer offer))
            {
                throw new ArgumentException("The crop is not sold in this shop.",
                    nameof(cropId));
            }

            if (purchased.Contains(cropId))
            {
                return SeedShopOfferState.Purchased;
            }

            if (unlocks.IsUnlocked(UnlockKey.CropCategory, cropId))
            {
                return SeedShopOfferState.AlreadyUnlocked;
            }

            if (offer.RequiredUnlock != null &&
                !unlocks.IsUnlocked(offer.RequiredUnlock.Category, offer.RequiredUnlock.Id))
            {
                return SeedShopOfferState.Locked;
            }

            return inventory.Currency >= offer.Price
                ? SeedShopOfferState.Affordable
                : SeedShopOfferState.Available;
        }

        public bool TryPurchase(string cropId)
        {
            if (GetState(cropId) != SeedShopOfferState.Affordable)
            {
                return false;
            }

            SeedShopOffer offer = offersByCrop[cropId];
            if (!inventory.TrySpendCurrency(offer.Price))
            {
                return false;
            }

            unlocks.Grant(new UnlockKey(UnlockKey.CropCategory, cropId));
            purchased.Add(cropId);
            return true;
        }
    }
}
