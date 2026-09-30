using System;
using System.Collections.Generic;
using CozyFoodFactory.Buildings;
using CozyFoodFactory.Logistics;
using UnityEngine;

namespace CozyFoodFactory.Food
{
    public sealed class TradeBuilding : MonoBehaviour, IBuildingRemovalRule
    {
        private readonly List<InputPort> inputs = new();
        private readonly List<OutputPort> outputs = new();
        private BeltTransportCoordinator coordinator;
        public TradeProcess Process { get; private set; }
        public bool HasRecentInvalidRecipe => Time.time < invalidUntil;
        private float invalidUntil;
        public bool OutputBlocked => Process?.PendingOutput > 0 &&
            !outputs.Exists(port => coordinator.CanAcceptOutput(port.OutputCell));
        public bool CanRemove => Process == null || Process.CanChangeTrade;
        public string RemovalBlockedReason => "Drain the Trade Building before removing it.";
        public void Initialize(BuildingPlacement placement, BeltTransportCoordinator transport,
            IReadOnlyList<TradeRecipe> recipes)
        {
            if (placement == null || placement.Footprint != TradePortLayout.Footprint)
                throw new ArgumentException("Trade Building requires a 3x3 footprint.");
            coordinator = transport ?? throw new ArgumentNullException(nameof(transport));
            Process = new TradeProcess(recipes);
            for (int i = 0; i < 3; i++)
            {
                var input = new InputPort(this,
                    TradePortLayout.InputCell(placement.AnchorCell, placement.Rotation, i),
                    TradePortLayout.Rotate(GridDirection.East, placement.Rotation));
                var output = new OutputPort(this,
                    TradePortLayout.OutputCell(placement.AnchorCell, placement.Rotation, i),
                    TradePortLayout.Rotate(GridDirection.South, placement.Rotation));
                coordinator.RegisterInputReceiver(input); inputs.Add(input);
                coordinator.RegisterOutputSource(output); outputs.Add(output);
            }
        }
        public SavedTradeBuilding CaptureWorldState() => Process.Capture();
        public void RestoreWorldState(SavedTradeBuilding saved) => Process.Restore(saved);
        private void OnDestroy()
        {
            foreach (var input in inputs) coordinator?.UnregisterInputReceiver(input);
            foreach (var output in outputs) coordinator?.UnregisterOutputSource(output);
        }
        private sealed class InputPort : IItemInputReceiver, IItemInputReservationGroup
        {
            private readonly TradeBuilding owner;
            private readonly GridDirection direction;
            public Vector2Int InputCell { get; }
            public bool AllowsConcurrentInput => false;
            public object InputReservationKey => owner;
            public InputPort(TradeBuilding owner, Vector2Int cell, GridDirection direction)
            { this.owner = owner; InputCell = cell; this.direction = direction; }
            public bool CanAcceptItem(ITransportItem item, GridDirection incomingDirection)
            {
                if (incomingDirection != direction || item is not FoodItemData food) return false;
                bool accepted = owner.Process.CanAccept(food);
                if (!accepted && owner.Process.SelectedTrade != null &&
                    !owner.Process.SelectedTrade.Input.Equals(food))
                    owner.invalidUntil = Time.time + 1.5f;
                return accepted;
            }
            public bool TryAcceptItem(ITransportItem item, GridDirection incomingDirection) =>
                CanAcceptItem(item, incomingDirection) && owner.Process.TryAccept((FoodItemData)item);
        }
        private sealed class OutputPort : IItemOutputSource
        {
            private readonly TradeBuilding owner;
            public Vector2Int OutputCell { get; }
            public GridDirection OutputDirection { get; }
            public bool HasOutput => owner.Process.PendingOutput > 0;
            public OutputPort(TradeBuilding owner, Vector2Int cell, GridDirection direction)
            { this.owner = owner; OutputCell = cell; OutputDirection = direction; }
            public ITransportItem PeekOutput() => owner.Process.PeekOutput();
            public bool TryTakeOutput(out ITransportItem item)
            {
                bool result = owner.Process.TryTakeOutput(out FoodItemData food);
                item = food; return result;
            }
        }
    }
}
