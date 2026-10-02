using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CozyFoodFactory.Logistics
{
    public sealed class BeltTransportSystem
    {
        private readonly Dictionary<Vector2Int, BeltCell> beltsByCell = new();
        private readonly Dictionary<Vector2Int, IItemInputReceiver> receiversByCell = new();
        private readonly List<BeltCell> orderedBelts = new();
        private readonly List<IItemOutputSource> outputSources = new();
        private readonly List<IItemOutputPairSource> outputPairs = new();
        private readonly Dictionary<Vector2Int, int> nextMachineInput = new();
        private int topologyRevision;

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
            belt.TopologyChanged += RecomputeTopology;
            RecomputeTopology();
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
            nextMachineInput.Remove(belt.Cell);
            orderedBelts.Remove(belt);
            belt.TopologyChanged -= RecomputeTopology;
            RecomputeTopology();
            return true;
        }

        public void RefreshTopology() => RecomputeTopology();

        private void RecomputeTopology()
        {
            topologyRevision++;
            foreach (BeltCell source in orderedBelts)
            {
                int output = 0;
                for (int i = 0; i < 4; i++)
                {
                    GridDirection direction = (GridDirection)i;
                    Vector2Int target = source.Cell + direction.ToOffset();
                    if (source.HasOutput(direction) &&
                        ((beltsByCell.TryGetValue(target, out BeltCell downstream) &&
                          !downstream.HasOutput(Opposite(direction))) ||
                         receiversByCell.ContainsKey(target)))
                        output |= BeltCell.Bit(direction);
                }
                source.SetConnectedTopology(0, output);
            }
            foreach (BeltCell source in orderedBelts)
            {
                int input = 0;
                for (int i = 0; i < 4; i++)
                {
                    GridDirection direction = (GridDirection)i;
                    Vector2Int previous = source.Cell - direction.ToOffset();
                    if (beltsByCell.TryGetValue(previous, out BeltCell upstream) &&
                        (upstream.ConnectedOutputMask & BeltCell.Bit(direction)) != 0 &&
                        !source.HasOutput(Opposite(direction)))
                        input |= BeltCell.Bit(Opposite(direction));
                }
                source.SetConnectedTopology(input, source.ConnectedOutputMask);
            }
        }

        public bool TryGetBelt(Vector2Int cell, out BeltCell belt)
        {
            return beltsByCell.TryGetValue(cell, out belt);
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
            RecomputeTopology();
        }

        public void UnregisterInputReceiver(IItemInputReceiver receiver)
        {
            if (receiver != null &&
                receiversByCell.TryGetValue(receiver.InputCell, out IItemInputReceiver registered) &&
                ReferenceEquals(receiver, registered))
            {
                receiversByCell.Remove(receiver.InputCell);
                RecomputeTopology();
            }
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
            int observedRevision = topologyRevision;
            // All candidates observe pre-transfer occupancy. Contended belt inputs are
            // resolved by the destination cursor, then rejected splitters can retry.
            var beltTransfers = new List<(BeltCell Source, BeltCell Destination,
                GridDirection Direction)>();
            var receiverTransfers = new List<(BeltCell Source, IItemInputReceiver Destination,
                GridDirection Direction)>();
            var reservedDestinations = new HashSet<Vector2Int>();
            var reservedReceiverGroups = new HashSet<object>();
            var acceptedSources = new HashSet<BeltCell>();
            var attempted = new Dictionary<BeltCell, int>();

            for (int pass = 0; pass < 4; pass++)
            {
                var proposals = new Dictionary<BeltCell,
                    List<(BeltCell Source, GridDirection Direction)>>();
                bool anyProposal = false;
                foreach (BeltCell source in orderedBelts)
                {
                    if (acceptedSources.Contains(source) || !source.HasItem ||
                        source.Item.Progress < 1f) continue;

                    int tried = attempted.TryGetValue(source, out int mask) ? mask : 0;
                    for (int offset = 0; offset < 4; offset++)
                    {
                        GridDirection direction = (GridDirection)
                            ((source.NextOutputIndex + offset) & 3);
                        int bit = BeltCell.Bit(direction);
                        if ((source.ConnectedOutputMask & bit) == 0 || (tried & bit) != 0)
                            continue;
                        tried |= bit;
                        Vector2Int target = source.Cell + direction.ToOffset();
                        if (beltsByCell.TryGetValue(target, out BeltCell belt) &&
                            belt.CanAccept && !reservedDestinations.Contains(target) &&
                            !belt.HasOutput(Opposite(direction)))
                        {
                            if (!proposals.TryGetValue(belt, out var contenders))
                            {
                                contenders = new List<(BeltCell, GridDirection)>();
                                proposals.Add(belt, contenders);
                            }
                            contenders.Add((source, direction));
                            anyProposal = true;
                            break;
                        }
                        if (receiversByCell.TryGetValue(target,
                                out IItemInputReceiver receiver) &&
                            receiver.CanAcceptItem(source.Item.Item, direction) &&
                            (receiver.AllowsConcurrentInput ||
                                !reservedDestinations.Contains(receiver.InputCell)) &&
                            (receiver is not IItemInputReservationGroup group ||
                                !reservedReceiverGroups.Contains(group.InputReservationKey)))
                        {
                            if (!receiver.AllowsConcurrentInput)
                                reservedDestinations.Add(receiver.InputCell);
                            if (receiver is IItemInputReservationGroup reservedGroup)
                                reservedReceiverGroups.Add(reservedGroup.InputReservationKey);
                            receiverTransfers.Add((source, receiver, direction));
                            acceptedSources.Add(source);
                            anyProposal = true;
                            break;
                        }
                    }
                    attempted[source] = tried;
                }
                foreach (KeyValuePair<BeltCell,
                             List<(BeltCell Source, GridDirection Direction)>> proposal in proposals)
                {
                    BeltCell destination = proposal.Key;
                    (BeltCell Source, GridDirection Direction) winner = proposal.Value
                        .OrderBy(candidate =>
                            (((int)candidate.Direction - destination.NextInputIndex) + 4) & 3)
                        .ThenBy(candidate => candidate.Source.Cell.y)
                        .ThenBy(candidate => candidate.Source.Cell.x).First();
                    beltTransfers.Add((winner.Source, destination, winner.Direction));
                    acceptedSources.Add(winner.Source);
                    reservedDestinations.Add(destination.Cell);
                }
                if (!anyProposal) break;
            }

            if (observedRevision != topologyRevision) return;

            foreach ((BeltCell source, BeltCell destination, GridDirection direction)
                     in beltTransfers)
            {
                TransportedItem item = source.TakeItem();
                if (!destination.TryAccept(item, direction))
                    throw new InvalidOperationException("A reserved belt changed during transfer.");
                source.AdvanceOutputCursor(direction);
                destination.AdvanceInputCursor(direction);
            }

            foreach ((BeltCell source, IItemInputReceiver destination,
                     GridDirection direction) in receiverTransfers)
            {
                if (observedRevision != topologyRevision || !source.HasItem ||
                    (source.ConnectedOutputMask & BeltCell.Bit(direction)) == 0)
                    continue;
                ITransportItem item = source.Item.Item;
                if (!destination.TryAcceptItem(item, direction))
                {
                    throw new InvalidOperationException(
                        "An item input receiver changed during a deterministic transfer.");
                }

                source.TakeItem();
                source.AdvanceOutputCursor(direction);
            }
        }

        private static GridDirection Opposite(GridDirection direction) =>
            (GridDirection)(((int)direction + 2) & 3);

        private void TransferSourceOutputs()
        {
            foreach (BeltCell destination in orderedBelts)
            {
                if (!destination.CanAccept) continue;
                int cursor = nextMachineInput.TryGetValue(destination.Cell,
                    out int value) ? value : 0;
                IItemOutputSource source = outputSources
                    .Where(candidate => candidate.HasOutput &&
                        candidate.OutputCell == destination.Cell &&
                        candidate.PeekOutput() != null)
                    .OrderBy(candidate =>
                        (((int)candidate.OutputDirection - cursor) + 4) & 3)
                    .ThenBy(candidate => candidate.GetType().FullName,
                        StringComparer.Ordinal)
                    .FirstOrDefault();
                if (source == null) continue;
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
                nextMachineInput[destination.Cell] =
                    ((int)source.OutputDirection + 1) & 3;
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

        private static int CompareCells(BeltCell left, BeltCell right)
        {
            int yComparison = left.Cell.y.CompareTo(right.Cell.y);
            return yComparison != 0 ? yComparison : left.Cell.x.CompareTo(right.Cell.x);
        }
    }
}
