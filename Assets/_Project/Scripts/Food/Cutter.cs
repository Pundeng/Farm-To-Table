using System;
using FantasyShapez.Buildings;
using FantasyShapez.Logistics;
using UnityEngine;

namespace FantasyShapez.Food
{
    public sealed class Cutter : MonoBehaviour, IItemInputReceiver,
        IItemOutputPairSource
    {
        private CutterProcess process;
        private BeltTransportCoordinator coordinator;
        private bool inputRegistered;
        private bool outputRegistered;
        private float invalidRecipeUntil;

        public Vector2Int InputCell { get; private set; }
        public Vector2Int OutputACell { get; private set; }
        public Vector2Int OutputBCell { get; private set; }
        public GridDirection RequiredIncomingDirection { get; private set; }
        public GridDirection OutputADirection { get; private set; }
        public GridDirection OutputBDirection { get; private set; }
        public bool AllowsConcurrentInput => false;
        public CutterState State => process?.State ?? CutterState.Idle;
        public bool HasOutputPair => process?.HasOutputPair ?? false;
        public bool HasRecentInvalidRecipe => Time.time < invalidRecipeUntil;
        public string LastEvent { get; private set; } = "Waiting for food.";

        public void Initialize(BuildingPlacement placement,
            BeltTransportCoordinator transport, CuttingRecipeCatalog catalog,
            float duration, RecipeDiscoveryRegistry discoveries = null)
        {
            if (placement == null || placement.Footprint != CutterPortLayout.Footprint)
                throw new ArgumentException("A Cutter requires a 1x2 footprint.",
                    nameof(placement));
            coordinator = transport ?? throw new ArgumentNullException(nameof(transport));
            process = new CutterProcess(catalog, duration, discoveries);
            InputCell = CutterPortLayout.InputCell(placement.AnchorCell,
                placement.Rotation);
            RequiredIncomingDirection = CutterPortLayout.IncomingDirection(
                placement.Rotation);
            OutputACell = CutterPortLayout.OutputCell(placement.AnchorCell,
                placement.Rotation, 0);
            OutputBCell = CutterPortLayout.OutputCell(placement.AnchorCell,
                placement.Rotation, 1);
            OutputADirection = CutterPortLayout.OutputDirection(placement.Rotation, 0);
            OutputBDirection = CutterPortLayout.OutputDirection(placement.Rotation, 1);
            coordinator.RegisterInputReceiver(this);
            inputRegistered = true;
            coordinator.RegisterOutputPair(this);
            outputRegistered = true;
        }

        public bool CanAcceptItem(ITransportItem item, GridDirection direction)
        {
            if (direction != RequiredIncomingDirection || item is not FoodItemData food ||
                process == null) return false;
            bool accepted = process.CanAccept(food);
            if (!accepted && State == CutterState.Idle)
            {
                LastEvent = $"No unique cutting recipe for {food.Id}.";
                invalidRecipeUntil = Time.time + 1.5f;
            }
            return accepted;
        }

        public bool TryAcceptItem(ITransportItem item, GridDirection direction)
        {
            if (!CanAcceptItem(item, direction) ||
                !process.TryAccept((FoodItemData)item)) return false;
            LastEvent = $"Buffered {((FoodItemData)item).Id}; waiting for both outputs.";
            return true;
        }

        public ITransportItem PeekOutputA() => HasOutputPair ? process.Output : null;
        public ITransportItem PeekOutputB() => HasOutputPair ? process.Output : null;

        public bool TryTakeOutputPair(out ITransportItem itemA,
            out ITransportItem itemB)
        {
            if (process.TryTakePair(out FoodItemData a, out FoodItemData b))
            {
                itemA = a;
                itemB = b;
                LastEvent = $"Output two {a.Id}.";
                return true;
            }
            itemA = itemB = null;
            return false;
        }

        public SavedCutter CaptureWorldState() => process == null
            ? throw new InvalidOperationException("Cutter is not initialized.")
            : new SavedCutter
            {
                state = process.State,
                input = SavedFood.From(process.Input),
                output = SavedFood.From(process.Output),
                elapsedSeconds = process.Elapsed
            };

        public void RestoreWorldState(SavedCutter saved)
        {
            process.Restore(saved.state, saved.input?.ToFood(),
                saved.output?.ToFood(), saved.elapsedSeconds);
            LastEvent = "Cutter state restored.";
        }

        private bool OutputsAvailable => coordinator != null &&
            coordinator.CanAcceptOutputPair(OutputACell, OutputBCell);

        private void Update()
        {
            if (process == null || FactoryWorldLoadSession.IsReconstructing) return;
            if (process.Advance(Time.deltaTime, OutputsAvailable))
                LastEvent = $"Two {process.Output.Id} ready.";
        }

        private void OnDestroy()
        {
            if (inputRegistered) coordinator?.UnregisterInputReceiver(this);
            if (outputRegistered) coordinator?.UnregisterOutputPair(this);
        }

    }
}
