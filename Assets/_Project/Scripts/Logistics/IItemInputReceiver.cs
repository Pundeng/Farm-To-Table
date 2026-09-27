using UnityEngine;

namespace CozyFoodFactory.Logistics
{
    public interface IItemInputReceiver
    {
        Vector2Int InputCell { get; }

        bool AllowsConcurrentInput { get; }

        bool CanAcceptItem(ITransportItem item, GridDirection incomingDirection);

        bool TryAcceptItem(ITransportItem item, GridDirection incomingDirection);
    }

    // Multiple ports of one machine can accept at most one belt transfer per step.
    public interface IItemInputReservationGroup
    {
        object InputReservationKey { get; }
    }
}
