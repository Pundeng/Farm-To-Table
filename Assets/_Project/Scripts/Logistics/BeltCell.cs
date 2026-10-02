using System;
using UnityEngine;

namespace CozyFoodFactory.Logistics
{
    public static class BeltConnectionPlanner
    {
        public static GridDirection PreferStraight(int incomingMask,
            GridDirection fallback)
        {
            GridDirection? only = null;
            for (int index = 0; index < 4; index++)
            {
                GridDirection direction = (GridDirection)index;
                if ((incomingMask & BeltCell.Bit(direction)) == 0) continue;
                if (only.HasValue) return fallback;
                only = direction;
            }
            return only ?? fallback;
        }

        public static int ExtendExisting(int currentMask,
            GridDirection primaryDirection, GridDirection requestedDirection,
            bool explicitDrag)
        {
            int primary = BeltCell.Bit(primaryDirection);
            if ((currentMask & primary) == 0)
                throw new ArgumentException("Existing Belt must retain its primary output.");
            return explicitDrag
                ? currentMask | BeltCell.Bit(requestedDirection)
                : currentMask;
        }
    }

    public sealed class BeltCell
    {
        // Bit positions follow GridDirection (North, East, South, West).
        public const int AllDirections = 15;

        public static int Bit(GridDirection direction) => 1 << (int)direction;

        public BeltCell(Vector2Int cell, GridDirection direction)
        {
            Cell = cell;
            Direction = direction;
            OutputMask = Bit(direction);
            ConnectedOutputMask = OutputMask;
        }

        public Vector2Int Cell { get; }

        public GridDirection Direction { get; }

        public int OutputMask { get; private set; }
        public int ConnectedOutputMask { get; private set; }
        public int ConnectedInputMask { get; private set; }
        internal event Action TopologyChanged;

        public int NextOutputIndex { get; private set; }

        public int NextInputIndex { get; private set; }

        public bool HasOutput(GridDirection direction) =>
            (OutputMask & Bit(direction)) != 0;

        public GridDirection PreferredOutput
        {
            get
            {
                for (int offset = 0; offset < 4; offset++)
                {
                    GridDirection direction = (GridDirection)
                        ((NextOutputIndex + offset) & 3);
                    if (HasOutput(direction)) return direction;
                }
                return Direction;
            }
        }

        public void SetOutputs(int mask, int nextOutputIndex = 0)
        {
            if (mask < 0 || mask > AllDirections ||
                (mask & Bit(Direction)) == 0 || nextOutputIndex < 0 ||
                nextOutputIndex > 3)
                throw new ArgumentOutOfRangeException(nameof(mask));
            OutputMask = mask;
            NextOutputIndex = nextOutputIndex;
            TopologyChanged?.Invoke();
        }

        internal void SetConnectedTopology(int inputMask, int outputMask)
        {
            ConnectedInputMask = inputMask;
            ConnectedOutputMask = outputMask;
            if ((outputMask & (1 << NextOutputIndex)) != 0) return;
            for (int offset = 0; offset < 4; offset++)
            {
                int candidate = (NextOutputIndex + offset) & 3;
                if ((outputMask & (1 << candidate)) == 0) continue;
                NextOutputIndex = candidate;
                return;
            }
        }

        internal void AdvanceOutputCursor(GridDirection usedDirection) =>
            NextOutputIndex = ((int)usedDirection + 1) & 3;

        internal void AdvanceInputCursor(GridDirection usedDirection) =>
            NextInputIndex = ((int)usedDirection + 1) & 3;

        public void RestoreInputCursor(int index)
        {
            if (index < 0 || index > 3)
                throw new ArgumentOutOfRangeException(nameof(index));
            NextInputIndex = index;
        }

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
