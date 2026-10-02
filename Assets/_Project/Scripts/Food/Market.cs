using System;
using System.Collections.Generic;
using CozyFoodFactory.Buildings;
using CozyFoodFactory.CameraControl;
using CozyFoodFactory.Logistics;
using UnityEngine;
using UnityEngine.Serialization;

namespace CozyFoodFactory.Food
{
    public sealed class Market : MonoBehaviour
    {
        private readonly List<GameObject> inputMarks = new();
        [SerializeField] private BeltTransportCoordinator transportCoordinator = null;
        [SerializeField] private Sprite visualSprite;
        [SerializeField] private GameObject visualPrefab;
        [FormerlySerializedAs("inputCell")]
        [SerializeField] private Vector2Int anchorCell = new(2, 2);
        [SerializeField] private string lastDeliveryDebug = string.Empty;
        [Header("Main Campaign Objectives (in sequence order)")]
        [InspectorName("Main Campaign Objectives")]
        [Tooltip("Edit each objective's ID, required Food and Quantity, and Unlocks here. Reorder the array to change the campaign sequence. Keep existing IDs and completed objectives in place for save compatibility.")]
        [SerializeField] private FoodOrder[] orders = Array.Empty<FoodOrder>();
        [SerializeField] private SeedShopOffer[] seedOffers = Array.Empty<SeedShopOffer>();
        [SerializeField] private TerritoryWorldSettings territorySettings = new();

        private MarketReceiver receiver;
        private FoodOrderSequence orderSequence;
        private SeedShop seedShop;
        private TerritorySystem territories;
        private readonly UnlockState unlocks = new();

        public Vector2Int AnchorCell => anchorCell;

        public IReadOnlyList<MarketInputPort> InputPorts => receiver?.Ports ??
            MarketPortLayout.Generate(anchorCell);

        public Vector2Int Footprint => MarketPortLayout.Footprint;

        public MarketInventory Inventory => receiver?.Inventory;

        public long Currency => Inventory?.Currency ?? 0;

        public IReadOnlyList<FoodOrder> Orders => orderSequence?.Orders ?? orders;
        public string CampaignObjectiveError =>
            CampaignObjectiveValidation.GetError(orders);
        public IReadOnlyList<FoodOrder> CompletedOrders =>
            orderSequence?.CompletedOrders ?? Array.Empty<FoodOrder>();
        public FoodOrderProgress ActiveOrder => orderSequence?.ActiveOrder;
        public UnlockState Unlocks => unlocks;
        public SeedShop SeedShop => seedShop;
        public TerritorySystem Territories => territories ??=
            new TerritorySystem(territorySettings).Initialize();
        public FoodOrderSequence OrderSequence => orderSequence;

        public string LastDeliveryMessage => lastDeliveryDebug;
        public event Action<FoodItemData, int> FoodDelivered;

        private void Awake()
        {
            if (transportCoordinator == null)
            {
                throw new MissingReferenceException("The Market requires a Belt Transport Coordinator.");
            }

            receiver = new MarketReceiver(anchorCell, new MarketInventory());
            receiver.FoodDelivered += HandleFoodDelivered;
            orderSequence = new FoodOrderSequence(orders, receiver, unlocks);
            seedShop = new SeedShop(seedOffers, receiver.Inventory, unlocks);
            territorySettings.Validate(anchorCell, Footprint);
            Territories.Initialize();
            unlocks.RestoreUnseen(Array.Empty<UnlockKey>());
            foreach (IItemInputReceiver input in receiver.InputReceivers)
                transportCoordinator.RegisterInputReceiver(input);
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
            foreach (IItemInputReceiver input in receiver.InputReceivers)
                transportCoordinator?.UnregisterInputReceiver(input);
        }

        private void CreatePlaceholderVisual()
        {
            if (visualPrefab != null)
            {
                GameObject visual = Instantiate(visualPrefab, transform, false);
                visual.name = "Market Visual";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
            }
            else if (visualSprite != null)
            {
                CreateVisualPart("Market Body", Vector2.zero,
                    FootprintVisualSize(), Color.white, 8, visualSprite);
            }
            else
            {
                CreateVisualPart("Market Body", Vector2.zero,
                    FootprintVisualSize(),
                    new Color(0.2f, 0.7f, 0.55f, 1f), 8);
                CreateVisualPart("Market Core", Vector2.zero,
                    new Vector2(0.7f, 0.7f),
                    new Color(1f, 0.85f, 0.4f, 1f), 9);
            }
            foreach (MarketInputPort port in InputPorts)
            {
                Vector2 position = (Vector2)(port.EdgeCell - AnchorCell) -
                    (Vector2)(Footprint - Vector2Int.one) * 0.5f +
                    (Vector2)port.Side.ToOffset() * 0.44f;
                Vector2 scale = port.Side is GridDirection.East or GridDirection.West
                    ? new Vector2(0.10f, 0.32f) : new Vector2(0.32f, 0.10f);
                inputMarks.Add(CreateVisualPart($"Market Input {port.Side} {port.EdgeCell}",
                    position, scale, BuildingPortPreviewLayouts.InputColor, 10));
            }
            gameObject.AddComponent<MachineVisualAnimator>().InitializeMarket(this);
        }

        private Vector2 FootprintVisualSize() =>
            new(Footprint.x * 0.93f, Footprint.y * 0.93f);

        public void SetInformationLevel(WorldInformationLevel level, bool detail)
        {
            foreach (GameObject mark in inputMarks)
                mark.SetActive(MachineAnimationDecisions.ShowMotion(level, detail));
        }

        private GameObject CreateVisualPart(
            string name, Vector2 position, Vector2 scale, Color color,
            int sortingOrder, Sprite sprite = null)
        {
            var part = new GameObject(name);
            part.transform.SetParent(transform, false);
            part.transform.localPosition = new Vector3(position.x, position.y, 0f);
            Vector2 artSize = sprite != null ? sprite.bounds.size : Vector2.one;
            part.transform.localScale = new Vector3(
                scale.x / Mathf.Max(0.001f, artSize.x),
                scale.y / Mathf.Max(0.001f, artSize.y), 1f);
            SpriteRenderer renderer = part.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite != null ? sprite :
                BuildingVisualFactory.PlaceholderSprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return part;
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
