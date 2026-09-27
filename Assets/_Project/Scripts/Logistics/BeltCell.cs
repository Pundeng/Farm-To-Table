using System;
using UnityEngine;

namespace CozyFoodFactory.Logistics
{
    public sealed class BeltCell
    {
        public BeltCell(Vector2Int cell, GridDirection direction)
        {
            Cell = cell;
            Direction = direction;
        }

        public Vector2Int Cell { get; }

        public GridDirection Direction { get; }

        public Vector2Int OutputCell => Cell + Direction.ToOffset();

        public TransportedItem Item { get; private set; }

        public bool HasItem => Item != null;

        public bool CanAccept => !HasItem;

        public bool TryAccept(ITransportItem item, GridDirection entryDirection)
        {
            if (!CanAccept)
            {
                return false;
            }

            Item = new TransportedItem(item, entryDirection);
            return true;
        }

        public void RestoreItem(ITransportItem item, GridDirection entryDirection,
            float progress)
        {
            if (HasItem || item == null)
            {
                throw new InvalidOperationException("Belt item cannot be restored.");
            }

            var transported = new TransportedItem(item, entryDirection);
            transported.RestoreProgress(progress);
            Item = transported;
        }

        internal bool TryAccept(TransportedItem item, GridDirection entryDirection)
        {
            if (!CanAccept)
            {
                return false;
            }

            Item = item ?? throw new ArgumentNullException(nameof(item));
            Item.EnterFrom(entryDirection);
            return true;
        }

        internal void Advance(float distance)
        {
            Item?.Advance(distance);
        }

        internal TransportedItem TakeItem()
        {
            TransportedItem item = Item;
            Item = null;
            return item;
        }
    }
}
