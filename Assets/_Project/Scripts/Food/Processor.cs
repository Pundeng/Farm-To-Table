using System;
using FantasyShapez.Buildings;
using FantasyShapez.Logistics;
using UnityEngine;

namespace FantasyShapez.Food
{
    public sealed class Processor : MonoBehaviour, IItemInputReceiver, IItemOutputSource
    {
        private ProcessorProcess process;
        private BeltTransportCoordinator transportCoordinator;
        private PropertySupplyPlayMode propertySupply;
        private bool propertyPortRegistered;
        private bool inputRegistered;
        private bool outputRegistered;
        private float invalidRecipeUntil;

        public Vector2Int InputCell { get; private set; }
        public Vector2Int OutputCell { get; private set; }
        public Vector2Int PropertyCell { get; private set; }
        public GridDirection RequiredIncomingDirection { get; private set; }
        public GridDirection OutputDirection { get; private set; }
        public bool AllowsConcurrentInput => false;
        public bool HasOutput => process?.HasOutput ?? false;
        public bool HasRecentInvalidRecipe => Time.time < invalidRecipeUntil;
        public ProcessorState State => process?.State ?? ProcessorState.Idle;
        public string LastRecipeMessage { get; private set; } = "No food received yet.";
        public string SupplyMessage => propertySupply?.GetProcessorSupplyMessage(PropertyCell) ??
            "Property supply is unavailable.";

        public SavedProcessor CaptureWorldState()
        {
            if (process == null)
            {
                throw new InvalidOperationException("Processor is not initialized.");
            }

            return new SavedProcessor
            {
                state = process.State,
                input = SavedFood.From(process.ActiveInput),
                activeProperty = process.ActiveProperty,
                elapsedSeconds = process.ElapsedTime,
                output = SavedFood.From(process.PendingOutput)
            };
        }
        public void RestoreWorldState(SavedProcessor saved)
        {
            process.Restore(saved.state, saved.input?.ToFood(), saved.activeProperty,
                saved.output?.ToFood(), saved.elapsedSeconds);
            LastRecipeMessage = saved.state == ProcessorState.Idle ?
                "No food received yet." : "Processor state restored.";
        }
        public string ProcessingStateMessage
        {
            get
            {
                if (State != ProcessorState.Processing)
                {
                    return State == ProcessorState.Idle
                        ? propertySupply != null &&
                          propertySupply.TryGetProcessorSupply(PropertyCell, out _)
                            ? "Idle: ready for food."
                            : "Idle: waiting for property supply."
                        : "Waiting for product output.";
                }

                if (!propertySupply.TryGetProcessorSupply(PropertyCell,
                        out CookingProperty property))
                {
                    return "Processing paused: property supply unavailable.";
                }

                return property == process.ActiveProperty
                    ? State.ToString()
                    : "Processing paused: supplied property changed.";
            }
        }

        public void Initialize(BuildingPlacement placement,
            BeltTransportCoordinator coordinator, PropertySupplyPlayMode supply,
            ProcessingRecipeCatalog catalog, float processingDuration,
            RecipeDiscoveryRegistry discoveries = null)
        {
            if (placement == null || placement.Footprint != ProcessorPortLayout.Footprint)
            {
                throw new ArgumentException("A Processor requires the 2x2 layout.",
                    nameof(placement));
            }

            transportCoordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            propertySupply = supply ?? throw new ArgumentNullException(nameof(supply));
            process = new ProcessorProcess(catalog, processingDuration, discoveries);
            InputCell = ProcessorPortLayout.GetChamberCell(placement.AnchorCell,
                placement.Rotation);
            PropertyCell = ProcessorPortLayout.GetPropertyCell(placement.AnchorCell,
                placement.Rotation);
            OutputCell = ProcessorPortLayout.GetFoodOutputOutsideCell(placement.AnchorCell,
                placement.Rotation);
            OutputDirection = ProcessorPortLayout.GetFoodOutputFacing(placement.Rotation);
            RequiredIncomingDirection =
                ProcessorPortLayout.GetFoodIncomingDirection(placement.Rotation);
            propertySupply.RegisterProcessorPort(PropertyCell,
                ProcessorPortLayout.GetPropertyOutsideCell(placement.AnchorCell,
                    placement.Rotation));
            propertyPortRegistered = true;
            transportCoordinator.RegisterInputReceiver(this);
            inputRegistered = true;
            transportCoordinator.RegisterOutputSource(this);
            outputRegistered = true;
        }

        public bool CanAcceptItem(ITransportItem item, GridDirection incomingDirection)
        {
            if (incomingDirection != RequiredIncomingDirection ||
                item is not FoodItemData food)
            {
                LastRecipeMessage = "Food must enter the food input port.";
                return false;
            }

            if (process.State != ProcessorState.Idle)
            {
                return false;
            }

            if (!propertySupply.TryGetProcessorSupply(PropertyCell,
                    out CookingProperty property))
            {
                LastRecipeMessage = "Food blocked: property supply was unavailable.";
                return false;
            }

            ProcessingRecipeMatch match = process.Evaluate(food, property, true);
            LastRecipeMessage = match switch
            {
                ProcessingRecipeMatch.None => $"No recipe for {food.Id} + {property}.",
                ProcessingRecipeMatch.Ambiguous => $"Ambiguous recipe for {food.Id} + {property}.",
                _ => $"Recipe found: {food.Id} + {property}."
            };
            if (match != ProcessingRecipeMatch.Unique)
                invalidRecipeUntil = Time.time + 1.5f;
            return match == ProcessingRecipeMatch.Unique;
        }

        public bool TryAcceptItem(ITransportItem item, GridDirection incomingDirection)
        {
            if (!CanAcceptItem(item, incomingDirection) ||
                !propertySupply.TryGetProcessorSupply(PropertyCell,
                    out CookingProperty property) ||
                !process.TryAccept((FoodItemData)item, property, true))
            {
                return false;
            }

            LastRecipeMessage = $"Processing {((FoodItemData)item).Id} with {property}.";
            return true;
        }

        public ITransportItem PeekOutput() => process?.PeekOutput();

        public bool TryTakeOutput(out ITransportItem item)
        {
            if (process != null && process.TryTakeOutput(out FoodItemData food))
            {
                item = food;
                LastRecipeMessage = $"Output {food.Id}.";
                return true;
            }

            item = null;
            return false;
        }

        private void Update()
        {
            if (process == null || FactoryWorldLoadSession.IsReconstructing)
            {
                return;
            }

            bool supplied = propertySupply.TryGetProcessorSupply(PropertyCell,
                out CookingProperty property) && property == process.ActiveProperty;
            if (process.Advance(Time.deltaTime, supplied))
            {
                LastRecipeMessage = $"{process.PeekOutput().Id} ready for output.";
            }

        }

        private void OnDestroy()
        {
            if (inputRegistered)
            {
                transportCoordinator?.UnregisterInputReceiver(this);
            }

            if (outputRegistered)
            {
                transportCoordinator?.UnregisterOutputSource(this);
            }

            if (propertyPortRegistered)
            {
                propertySupply?.UnregisterProcessorPort(PropertyCell);
            }
        }

    }
}
