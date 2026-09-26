using System;
using FantasyShapez.Buildings;
using FantasyShapez.Logistics;
using UnityEngine;

namespace FantasyShapez.Food
{
    public sealed class BasicMixer : MonoBehaviour, IItemOutputSource
    {
        private sealed class IngredientPort : IItemInputReceiver, IItemInputReservationGroup
        {
            private readonly BasicMixer mixer;
            private readonly int slot;

            public IngredientPort(BasicMixer mixer, int slot, Vector2Int cell)
            {
                this.mixer = mixer;
                this.slot = slot;
                InputCell = cell;
            }

            public Vector2Int InputCell { get; }
            public bool AllowsConcurrentInput => false;
            public object InputReservationKey => mixer;
            public bool CanAcceptItem(ITransportItem item, GridDirection direction) =>
                mixer.CanAccept(slot, item, direction);
            public bool TryAcceptItem(ITransportItem item, GridDirection direction) =>
                mixer.TryAccept(slot, item, direction);
        }

        private BasicMixerProcess process;
        private BeltTransportCoordinator transportCoordinator;
        private IngredientPort inputA;
        private IngredientPort inputB;
        private bool inputARegistered;
        private bool inputBRegistered;
        private bool outputRegistered;
        private float invalidRecipeUntil;

        public Vector2Int InputACell => inputA.InputCell;
        public Vector2Int InputBCell => inputB.InputCell;
        public Vector2Int OutputCell { get; private set; }
        public GridDirection RequiredIncomingDirection { get; private set; }
        public GridDirection OutputDirection { get; private set; }
        public FoodItemData SlotA => process?.InputA;
        public FoodItemData SlotB => process?.InputB;
        public bool HasOutput => process?.HasOutput ?? false;
        public bool HasRecentInvalidRecipe => Time.time < invalidRecipeUntil;
        public string LastEvent { get; private set; } = "Waiting for ingredients.";

        public SavedMixer CaptureWorldState()
        {
            if (process == null)
            {
                throw new InvalidOperationException("Basic Mixer is not initialized.");
            }

            return new SavedMixer
            {
                slotA = SavedFood.From(process.InputA),
                slotB = SavedFood.From(process.InputB),
                output = SavedFood.From(process.PeekOutput())
            };
        }

        public void RestoreWorldState(SavedMixer saved)
        {
            process.Restore(saved.slotA?.ToFood(), saved.slotB?.ToFood(),
                saved.output?.ToFood());
            LastEvent = "Mixer state restored.";
        }

        public void Initialize(BuildingPlacement placement,
            BeltTransportCoordinator coordinator, MixingRecipeCatalog catalog,
            RecipeDiscoveryRegistry discoveries = null)
        {
            if (placement == null || placement.Footprint != BasicMixerPortLayout.Footprint)
            {
                throw new ArgumentException("A Basic Mixer requires a 2x2 footprint.",
                    nameof(placement));
            }

            transportCoordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            process = new BasicMixerProcess(catalog, discoveries);
            RequiredIncomingDirection = BasicMixerPortLayout.GetIncomingDirection(
                placement.Rotation);
            OutputDirection = BasicMixerPortLayout.GetOutputFacing(placement.Rotation);
            OutputCell = BasicMixerPortLayout.GetOutputOutsideCell(placement.AnchorCell,
                placement.Rotation);
            inputA = new IngredientPort(this, 0, BasicMixerPortLayout.GetInputCell(
                placement.AnchorCell, placement.Rotation, 0));
            inputB = new IngredientPort(this, 1, BasicMixerPortLayout.GetInputCell(
                placement.AnchorCell, placement.Rotation, 1));
            transportCoordinator.RegisterInputReceiver(inputA);
            inputARegistered = true;
            transportCoordinator.RegisterInputReceiver(inputB);
            inputBRegistered = true;
            transportCoordinator.RegisterOutputSource(this);
            outputRegistered = true;
        }

        public IItemInputReceiver InputAReceiver => inputA;
        public IItemInputReceiver InputBReceiver => inputB;

        public ITransportItem PeekOutput() => process?.PeekOutput();

        public bool TryTakeOutput(out ITransportItem item)
        {
            if (process != null && process.TryTakeOutput(out FoodItemData food))
            {
                item = food;
                LastEvent = $"Output {food.Id}.";
                return true;
            }

            item = null;
            return false;
        }

        private bool CanAccept(int slot, ITransportItem item, GridDirection direction)
        {
            if (direction != RequiredIncomingDirection || item is not FoodItemData food)
            {
                LastEvent = "Food must enter an ingredient input port.";
                return false;
            }

            if (!process.CanAccept(slot, food))
            {
                LastEvent = $"No unique mixing recipe for {food.Id} in slot {(slot == 0 ? "A" : "B")}.";
                if (!HasOutput && (slot == 0 ? SlotA : SlotB) == null)
                    invalidRecipeUntil = Time.time + 1.5f;
                return false;
            }

            return true;
        }

        private bool TryAccept(int slot, ITransportItem item, GridDirection direction)
        {
            if (!CanAccept(slot, item, direction) ||
                !process.TryAccept(slot, (FoodItemData)item))
            {
                return false;
            }

            LastEvent = HasOutput
                ? $"{process.PeekOutput().Id} ready for output."
                : $"Received {((FoodItemData)item).Id} in slot {(slot == 0 ? "A" : "B")}.";
            return true;
        }

        private void OnDestroy()
        {
            if (inputARegistered)
            {
                transportCoordinator?.UnregisterInputReceiver(inputA);
            }

            if (inputBRegistered)
            {
                transportCoordinator?.UnregisterInputReceiver(inputB);
            }

            if (outputRegistered)
            {
                transportCoordinator?.UnregisterOutputSource(this);
            }
        }

    }
}
