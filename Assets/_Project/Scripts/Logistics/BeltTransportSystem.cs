using System;
using System.Collections.Generic;
using FantasyShapez.Runes;
using UnityEngine;

namespace FantasyShapez.Logistics
{
    public sealed class BeltTransportSystem
    {
        private readonly Dictionary<Vector2Int, BeltCell> beltsByCell = new();
        private readonly Dictionary<Vector2Int, IItemInputReceiver> receiversByCell = new();
        private readonly List<BeltCell> orderedBelts = new();
        private readonly List<IItemOutputSource> outputSources = new();
        private readonly List<IItemOutputPairSource> outputPairs = new();
        private readonly Dictionary<IRuneInputReceiver, IItemInputReceiver> runeReceivers = new();
        private readonly Dictionary<IRuneOutputSource, IItemOutputSource> runeSources = new();

        public BeltTransportSystem(float movementSpeed)
        {
            if (movementSpeed <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(movementSpeed),
                    "Movement speed must be positive.");
            }

            MovementSpeed = movementSpeed;
        }

        public float MovementSpeed { get; }

        public BeltCell AddBelt(Vector2Int cell, GridDirection direction)
        {
            if (beltsByCell.ContainsKey(cell) || receiversByCell.ContainsKey(cell))
            {
                throw new InvalidOperationException($"A logistics carrier already exists at {cell}.");
            }

            var belt = new BeltCell(cell, direction);
            beltsByCell.Add(cell, belt);
            orderedBelts.Add(belt);
            orderedBelts.Sort(CompareCells);
            return belt;
        }

        public bool RemoveBelt(BeltCell belt)
        {
            if (belt == null ||
                !beltsByCell.TryGetValue(belt.Cell, out BeltCell registered) ||
                !ReferenceEquals(registered, belt))
            {
                return false;
            }

            // Rebuilding intentionally discards the item on this belt.
            belt.TakeItem();
            beltsByCell.Remove(belt.Cell);
            orderedBelts.Remove(belt);
            return true;
        }

        public bool TryGetBelt(Vector2Int cell, out BeltCell belt)
        {
            return beltsByCell.TryGetValue(cell, out belt);
        }

        public void RegisterInputReceiver(IRuneInputReceiver receiver)
        {
            if (receiver == null)
            {
                throw new ArgumentNullException(nameof(receiver));
            }

            var adapter = new RuneInputReceiverAdapter(receiver);
            RegisterInputReceiver(adapter);
            runeReceivers.Add(receiver, adapter);
        }

        public void RegisterInputReceiver(IItemInputReceiver receiver)
        {
            if (receiver == null)
            {
                throw new ArgumentNullException(nameof(receiver));
            }

            if (beltsByCell.ContainsKey(receiver.InputCell) ||
                receiversByCell.ContainsKey(receiver.InputCell))
            {
                throw new InvalidOperationException(
                    $"A logistics carrier already exists at {receiver.InputCell}.");
            }

            receiversByCell.Add(receiver.InputCell, receiver);
        }

        public void UnregisterInputReceiver(IRuneInputReceiver receiver)
        {
            if (receiver != null && runeReceivers.TryGetValue(receiver, out IItemInputReceiver adapter))
            {
                UnregisterInputReceiver(adapter);
                runeReceivers.Remove(receiver);
            }
        }

        public void UnregisterInputReceiver(IItemInputReceiver receiver)
        {
            if (receiver != null &&
                receiversByCell.TryGetValue(receiver.InputCell, out IItemInputReceiver registered) &&
                ReferenceEquals(receiver, registered))
            {
                receiversByCell.Remove(receiver.InputCell);
            }
        }

        public void RegisterOutputSource(IRuneOutputSource source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (runeSources.ContainsKey(source))
            {
                return;
            }

            var adapter = new RuneOutputSourceAdapter(source);
            RegisterOutputSource(adapter);
            runeSources.Add(source, adapter);
        }

        public void RegisterOutputSource(IItemOutputSource source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (!outputSources.Contains(source))
            {
                outputSources.Add(source);
            }
        }

        public void RegisterOutputPair(IItemOutputPairSource source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (!outputPairs.Contains(source)) outputPairs.Add(source);
        }

        public void UnregisterOutputPair(IItemOutputPairSource source) =>
            outputPairs.Remove(source);

        public bool CanAcceptOutputPair(Vector2Int cellA, Vector2Int cellB) =>
            cellA != cellB &&
            beltsByCell.TryGetValue(cellA, out BeltCell beltA) && beltA.CanAccept &&
            beltsByCell.TryGetValue(cellB, out BeltCell beltB) && beltB.CanAccept;

        public bool CanAcceptOutput(Vector2Int cell) =>
            beltsByCell.TryGetValue(cell, out BeltCell belt) && belt.CanAccept;

        public void UnregisterOutputSource(IRuneOutputSource source)
        {
            if (source != null && runeSources.TryGetValue(source, out IItemOutputSource adapter))
            {
                UnregisterOutputSource(adapter);
                runeSources.Remove(source);
            }
        }

        public void UnregisterOutputSource(IItemOutputSource source)
        {
            outputSources.Remove(source);
        }

        public void Advance(float deltaTime)
        {
            if (deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime), "Delta time cannot be negative.");
            }

            float movementDistance = deltaTime * MovementSpeed;
            foreach (BeltCell belt in orderedBelts)
            {
                belt.Advance(movementDistance);
            }

            TransferReadyItems();
            TransferSourceOutputs();
            TransferOutputPairs();
        }

        private void TransferReadyItems()
        {
            // Decisions use the pre-transfer occupancy snapshot so a chain cannot cascade
            // differently based on component or dictionary iteration order.
            var beltTransfers = new List<(BeltCell Source, BeltCell Destination)>();
            var receiverTransfers = new List<(BeltCell Source, IItemInputReceiver Destination)>();
            var reservedDestinations = new HashSet<Vector2Int>();
            var reservedReceiverGroups = new HashSet<object>();

            foreach (BeltCell source in orderedBelts)
            {
                if (!source.HasItem || source.Item.Progress < 1f)
                {
                    continue;
                }

                if (beltsByCell.TryGetValue(source.OutputCell, out BeltCell beltDestination) &&
                    beltDestination.OutputCell != source.Cell &&
                    beltDestination.CanAccept &&
                    reservedDestinations.Add(beltDestination.Cell))
                {
                    beltTransfers.Add((source, beltDestination));
                    continue;
                }

                if (receiversByCell.TryGetValue(
                        source.OutputCell,
                        out IItemInputReceiver receiverDestination) &&
                    receiverDestination.CanAcceptItem(source.Item.Item, source.Direction) &&
                    (receiverDestination.AllowsConcurrentInput ||
                        reservedDestinations.Add(receiverDestination.InputCell)) &&
                    (receiverDestination is not IItemInputReservationGroup group ||
                        reservedReceiverGroups.Add(group.InputReservationKey)))
                {
                    receiverTransfers.Add((source, receiverDestination));
                }
            }

            foreach ((BeltCell source, BeltCell destination) in beltTransfers)
            {
                TransportedRune item = source.TakeItem();
                destination.TryAccept(item, source.Direction);
            }

            foreach ((BeltCell source, IItemInputReceiver destination) in receiverTransfers)
            {
                ITransportItem item = source.Item.Item;
                if (!destination.TryAcceptItem(item, source.Direction))
                {
                    throw new InvalidOperationException(
                        "An item input receiver changed during a deterministic transfer.");
                }

                source.TakeItem();
            }
        }

        private void TransferSourceOutputs()
        {
            foreach (IItemOutputSource source in outputSources)
            {
                if (!source.HasOutput ||
                    !beltsByCell.TryGetValue(source.OutputCell, out BeltCell destination) ||
                    !destination.CanAccept)
                {
                    continue;
                }

                ITransportItem pendingItem = source.PeekOutput();
                if (pendingItem == null || !source.TryTakeOutput(out ITransportItem takenItem))
                {
                    continue;
                }

                if (!ReferenceEquals(pendingItem, takenItem) ||
                    !destination.TryAccept(takenItem, source.OutputDirection))
                {
                    throw new InvalidOperationException(
                        "An item output source changed during a deterministic transfer.");
                }
            }
        }

        private void TransferOutputPairs()
        {
            foreach (IItemOutputPairSource source in outputPairs)
            {
                if (!source.HasOutputPair ||
                    !CanAcceptOutputPair(source.OutputACell, source.OutputBCell))
                    continue;

                ITransportItem pendingA = source.PeekOutputA();
                ITransportItem pendingB = source.PeekOutputB();
                if (pendingA == null || pendingB == null ||
                    !source.TryTakeOutputPair(out ITransportItem itemA,
                        out ITransportItem itemB) ||
                    !ReferenceEquals(pendingA, itemA) ||
                    !ReferenceEquals(pendingB, itemB))
                    throw new InvalidOperationException("A paired output changed during transfer.");

                if (!beltsByCell[source.OutputACell].TryAccept(itemA,
                        source.OutputADirection) ||
                    !beltsByCell[source.OutputBCell].TryAccept(itemB,
                        source.OutputBDirection))
                    throw new InvalidOperationException("A paired output belt changed during transfer.");
            }
        }

        private sealed class RuneInputReceiverAdapter : IItemInputReceiver
        {
            private readonly IRuneInputReceiver receiver;

            public RuneInputReceiverAdapter(IRuneInputReceiver receiver)
            {
                this.receiver = receiver;
            }

            public Vector2Int InputCell => receiver.InputCell;

            public bool AllowsConcurrentInput => receiver.AllowsConcurrentInput;

            public bool CanAcceptItem(ITransportItem item, GridDirection direction)
            {
                return item is RuneData && receiver.CanAcceptInput &&
                    receiver.CanAcceptInputFrom(direction);
            }

            public bool TryAcceptItem(ITransportItem item, GridDirection direction)
            {
                return item is RuneData rune && receiver.TryAcceptInput(rune, direction);
            }
        }

        private sealed class RuneOutputSourceAdapter : IItemOutputSource
        {
            private readonly IRuneOutputSource source;

            public RuneOutputSourceAdapter(IRuneOutputSource source)
            {
                this.source = source;
            }

            public Vector2Int OutputCell => source.OutputCell;

            public GridDirection OutputDirection => source.OutputDirection;

            public bool HasOutput => source.HasOutput;

            public ITransportItem PeekOutput() => source.PeekOutput();

            public bool TryTakeOutput(out ITransportItem item)
            {
                bool succeeded = source.TryTakeOutput(out RuneData rune);
                item = rune;
                return succeeded;
            }
        }

        private static int CompareCells(BeltCell left, BeltCell right)
        {
            int yComparison = left.Cell.y.CompareTo(right.Cell.y);
            return yComparison != 0 ? yComparison : left.Cell.x.CompareTo(right.Cell.x);
        }
    }
}
