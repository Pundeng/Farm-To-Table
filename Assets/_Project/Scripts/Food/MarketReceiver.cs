using System;
using System.Collections.Generic;
using System.Linq;
using CozyFoodFactory.Logistics;
using UnityEngine;

namespace CozyFoodFactory.Food
{
    public readonly struct MarketInputPort
    {
        public MarketInputPort(Vector2Int edgeCell, GridDirection side)
        {
            EdgeCell = edgeCell;
            Side = side;
        }

        public Vector2Int EdgeCell { get; }
        public GridDirection Side { get; }
        public Vector2Int ExternalCell => EdgeCell + Side.ToOffset();
        public GridDirection IncomingDirection => (GridDirection)(((int)Side + 2) & 3);
    }

    public static class MarketPortLayout
    {
        public static readonly Vector2Int Footprint = new(5, 5);

        public static MarketInputPort[] Generate(Vector2Int anchor)
        {
            var ports = new List<MarketInputPort>(12);
            foreach (GridDirection side in new[]
                { GridDirection.North, GridDirection.South, GridDirection.West, GridDirection.East })
                for (int index = 1; index <= 3; index++)
                {
                    Vector2Int local = side switch
                    {
                        GridDirection.North => new Vector2Int(index, Footprint.y - 1),
                        GridDirection.South => new Vector2Int(index, 0),
                        GridDirection.West => new Vector2Int(0, index),
                        _ => new Vector2Int(Footprint.x - 1, index)
                    };
                    ports.Add(new MarketInputPort(anchor + local, side));
                }
            return ports.ToArray();
        }

        public static bool IsExternalLane(Vector2Int cell, Vector2Int anchor) =>
            Generate(anchor).Any(port => port.ExternalCell == cell);
    }

    // One delivery pipeline with twelve independent perimeter receivers.
    public sealed class MarketReceiver
    {
        public MarketReceiver(Vector2Int anchorCell, MarketInventory inventory)
        {
            Ports = Array.AsReadOnly(MarketPortLayout.Generate(anchorCell));
            Inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            InputReceivers = Array.AsReadOnly(Ports.GroupBy(port => port.EdgeCell)
                .Select(group => (IItemInputReceiver)new EdgeReceiver(this, group.Key,
                    group.Aggregate(0, (mask, port) => mask | BeltCell.Bit(port.IncomingDirection))))
                .ToArray());
        }

        public event Action<FoodItemData, int> FoodDelivered;

        public IReadOnlyList<MarketInputPort> Ports { get; }
        public IReadOnlyList<IItemInputReceiver> InputReceivers { get; }

        private sealed class EdgeReceiver : IItemInputReceiver
        {
            private readonly MarketReceiver owner;
            private readonly int incomingMask;

            public EdgeReceiver(MarketReceiver owner, Vector2Int cell, int incomingMask)
            {
                this.owner = owner;
                InputCell = cell;
                this.incomingMask = incomingMask;
            }

            public Vector2Int InputCell { get; }
            public bool AllowsConcurrentInput => true;
            public bool CanAcceptItem(ITransportItem item, GridDirection incomingDirection) =>
                owner.CanAcceptItem(item, incomingDirection) &&
                (incomingMask & BeltCell.Bit(incomingDirection)) != 0;
            public bool TryAcceptItem(ITransportItem item, GridDirection incomingDirection) =>
                CanAcceptItem(item, incomingDirection) && owner.TryAcceptItem(item, incomingDirection);
        }

        public MarketInventory Inventory { get; }

        public bool CanAcceptItem(ITransportItem item, GridDirection incomingDirection)
        {
            return item is FoodItemData food && food.IsValid &&
                Enum.IsDefined(typeof(GridDirection), incomingDirection);
        }

        public bool TryAcceptItem(ITransportItem item, GridDirection incomingDirection)
        {
            if (!CanAcceptItem(item, incomingDirection))
            {
                return false;
            }

            var food = (FoodItemData)item;
            int count = Inventory.RecordDelivery(food);
            FoodDelivered?.Invoke(food, count);
            return true;
        }
    }
}
