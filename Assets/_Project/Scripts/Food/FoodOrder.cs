using System;
using System.Collections.Generic;
using UnityEngine;

namespace FantasyShapez.Food
{
    [Serializable]
    public sealed class UnlockKey : IEquatable<UnlockKey>
    {
        public const string CropCategory = "crop";
        public const string SeedShopCategory = "seed_shop";
        public const string RegionCategory = "region";
        public const string RegionAccessCategory = "region_access";
        public const string MachineCategory = "machine";

        [SerializeField] private string category;
        [SerializeField] private string id;

        public UnlockKey(string category, string id)
        {
            this.category = category;
            this.id = id;
            Validate();
        }

        public string Category => category;
        public string Id => id;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(id))
            {
                throw new InvalidOperationException("An unlock needs a category and ID.");
            }
        }

        public bool Equals(UnlockKey other) => other != null &&
            category == other.category && id == other.id;

        public override bool Equals(object obj) => obj is UnlockKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(category, id);
    }

    public sealed class UnlockState
    {
        private readonly HashSet<UnlockKey> unlocked = new();
        private readonly List<UnlockKey> ordered = new();
        private readonly IReadOnlyList<UnlockKey> readOnlyOrdered;

        public UnlockState()
        {
            readOnlyOrdered = ordered.AsReadOnly();
        }

        public IReadOnlyList<UnlockKey> Unlocked => readOnlyOrdered;
        public event Action<UnlockKey> UnlockedContent;
        public event Action Restored;

        public bool IsUnlocked(string category, string id) =>
            !string.IsNullOrWhiteSpace(category) && !string.IsNullOrWhiteSpace(id) &&
            unlocked.Contains(new UnlockKey(category, id));

        public bool Grant(UnlockKey key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            key.Validate();
            if (!unlocked.Add(key))
            {
                return false;
            }

            ordered.Add(key);
            UnlockedContent?.Invoke(key);
            return true;
        }

        public void Restore(IReadOnlyList<UnlockKey> keys)
        {
            if (keys == null)
            {
                throw new ArgumentNullException(nameof(keys));
            }

            var unique = new HashSet<UnlockKey>();
            foreach (UnlockKey key in keys)
            {
                if (key == null || !unique.Add(key))
                {
                    throw new ArgumentException("Invalid saved unlocks.", nameof(keys));
                }

                key.Validate();
            }

            unlocked.Clear();
            ordered.Clear();
            foreach (UnlockKey key in keys)
            {
                unlocked.Add(key);
                ordered.Add(key);
            }

            Restored?.Invoke();
        }
    }

    [Serializable]
    public sealed class FoodOrderRequirement
    {
        [SerializeField] private FoodItemData food;
        [SerializeField, Min(1), Tooltip("Editable delivery objective for this order.")]
        private int quantity = 1;

        public FoodOrderRequirement(FoodItemData food, int quantity)
        {
            this.food = food ?? throw new ArgumentNullException(nameof(food));
            if (quantity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(quantity));
            }

            this.quantity = quantity;
        }

        public FoodItemData Food => food;
        public int Quantity => quantity;
    }

    [Serializable]
    public sealed class FoodOrder
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private FoodOrderRequirement[] requirements;
        [SerializeField] private UnlockKey[] unlocks;

        public FoodOrder(string id, string displayName,
            FoodOrderRequirement[] requirements, UnlockKey[] unlocks)
        {
            this.id = id;
            this.displayName = displayName;
            this.requirements = requirements;
            this.unlocks = unlocks;
            Validate();
        }

        public string Id => id;
        public string DisplayName => displayName;
        public IReadOnlyList<FoodOrderRequirement> Requirements => requirements;
        public IReadOnlyList<UnlockKey> Unlocks => unlocks;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(displayName) ||
                requirements == null || requirements.Length == 0 || unlocks == null)
            {
                throw new InvalidOperationException("The food order is incomplete.");
            }

            var unique = new HashSet<FoodItemData>();
            foreach (FoodOrderRequirement requirement in requirements)
            {
                if (requirement?.Food == null || !requirement.Food.IsValid ||
                    requirement.Quantity <= 0 || !unique.Add(requirement.Food))
                {
                    throw new InvalidOperationException("The food order has an invalid requirement.");
                }
            }

            var uniqueUnlocks = new HashSet<UnlockKey>();
            foreach (UnlockKey unlock in unlocks)
            {
                if (unlock == null || !uniqueUnlocks.Add(unlock))
                {
                    throw new InvalidOperationException("The food order has an invalid unlock.");
                }

                unlock.Validate();
            }
        }
    }

    public sealed class FoodOrderProgress : IDisposable
    {
        private readonly MarketReceiver receiver;
        private readonly Dictionary<FoodItemData, int> startingCounts = new();

        public FoodOrderProgress(FoodOrder order, MarketReceiver receiver)
            : this(order, receiver, null)
        {
        }

        public FoodOrderProgress(FoodOrder order, MarketReceiver receiver,
            IReadOnlyDictionary<FoodItemData, int> savedProgress)
        {
            Order = order ?? throw new ArgumentNullException(nameof(order));
            Order.Validate();
            this.receiver = receiver ?? throw new ArgumentNullException(nameof(receiver));
            foreach (FoodOrderRequirement requirement in Order.Requirements)
            {
                int current = receiver.Inventory.GetDeliveredCount(requirement.Food);
                int progress = 0;
                if (savedProgress != null &&
                    (!savedProgress.TryGetValue(requirement.Food, out progress) ||
                     progress < 0 || progress >= requirement.Quantity || progress > current))
                {
                    throw new ArgumentException("Invalid saved order progress.",
                        nameof(savedProgress));
                }

                startingCounts.Add(requirement.Food, current - progress);
            }

            receiver.FoodDelivered += OnFoodDelivered;
        }

        public FoodOrder Order { get; }
        public bool IsComplete { get; private set; }
        public event Action<FoodOrder> Completed;

        public int GetDeliveredCount(FoodOrderRequirement requirement)
        {
            if (requirement == null ||
                !startingCounts.TryGetValue(requirement.Food, out int startingCount))
            {
                throw new ArgumentException("The requirement is not in this order.",
                    nameof(requirement));
            }

            int delivered = receiver.Inventory.GetDeliveredCount(requirement.Food) -
                startingCount;
            return Math.Min(requirement.Quantity, Math.Max(0, delivered));
        }

        public void Dispose()
        {
            receiver.FoodDelivered -= OnFoodDelivered;
        }

        private void OnFoodDelivered(FoodItemData food, int count)
        {
            if (IsComplete)
            {
                return;
            }

            foreach (FoodOrderRequirement requirement in Order.Requirements)
            {
                if (GetDeliveredCount(requirement) < requirement.Quantity)
                {
                    return;
                }
            }

            IsComplete = true;
            Completed?.Invoke(Order);
        }
    }

    public sealed class FoodOrderSequence : IDisposable
    {
        private readonly IReadOnlyList<FoodOrder> orders;
        private readonly MarketReceiver receiver;
        private readonly UnlockState unlocks;
        private readonly List<FoodOrder> completed = new();
        private readonly IReadOnlyList<FoodOrder> readOnlyCompleted;
        private int activeIndex;

        public FoodOrderSequence(IReadOnlyList<FoodOrder> orders,
            MarketReceiver receiver, UnlockState unlocks)
        {
            this.orders = orders ?? throw new ArgumentNullException(nameof(orders));
            this.receiver = receiver ?? throw new ArgumentNullException(nameof(receiver));
            this.unlocks = unlocks ?? throw new ArgumentNullException(nameof(unlocks));
            readOnlyCompleted = completed.AsReadOnly();

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (FoodOrder order in orders)
            {
                if (order == null)
                {
                    throw new ArgumentException("An order definition is missing.", nameof(orders));
                }

                order.Validate();
                if (!ids.Add(order.Id))
                {
                    throw new ArgumentException("Order IDs must be unique.", nameof(orders));
                }
            }

            ActivateNext();
        }

        public IReadOnlyList<FoodOrder> Orders => orders;
        public IReadOnlyList<FoodOrder> CompletedOrders => readOnlyCompleted;
        public FoodOrderProgress ActiveOrder { get; private set; }
        public event Action<FoodOrder> Completed;

        public void Dispose()
        {
            if (ActiveOrder == null)
            {
                return;
            }

            ActiveOrder.Completed -= OnOrderCompleted;
            ActiveOrder.Dispose();
            ActiveOrder = null;
        }

        public void Restore(int completedCount,
            IReadOnlyDictionary<FoodItemData, int> activeProgress)
        {
            if (completedCount < 0 || completedCount > orders.Count ||
                (completedCount == orders.Count) != (activeProgress == null))
            {
                throw new ArgumentException("Invalid saved order position.");
            }

            if (ActiveOrder != null)
            {
                ActiveOrder.Completed -= OnOrderCompleted;
                ActiveOrder.Dispose();
            }
            ActiveOrder = null;
            completed.Clear();
            for (int index = 0; index < completedCount; index++)
            {
                completed.Add(orders[index]);
            }

            activeIndex = completedCount;
            if (activeProgress != null)
            {
                ActiveOrder = new FoodOrderProgress(orders[activeIndex], receiver,
                    activeProgress);
                ActiveOrder.Completed += OnOrderCompleted;
            }
        }

        private void OnOrderCompleted(FoodOrder order)
        {
            ActiveOrder.Completed -= OnOrderCompleted;
            ActiveOrder.Dispose();
            ActiveOrder = null;
            completed.Add(order);
            foreach (UnlockKey unlock in order.Unlocks)
            {
                unlocks.Grant(unlock);
            }

            activeIndex++;
            ActivateNext();
            Completed?.Invoke(order);
        }

        private void ActivateNext()
        {
            if (activeIndex >= orders.Count)
            {
                return;
            }

            ActiveOrder = new FoodOrderProgress(orders[activeIndex], receiver);
            ActiveOrder.Completed += OnOrderCompleted;
        }
    }
}
