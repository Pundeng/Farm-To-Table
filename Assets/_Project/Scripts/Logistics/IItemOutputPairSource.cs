using UnityEngine;

namespace FantasyShapez.Logistics
{
    // Both items leave together. The transport system owns the two-belt preflight.
    public interface IItemOutputPairSource
    {
        Vector2Int OutputACell { get; }
        Vector2Int OutputBCell { get; }
        GridDirection OutputADirection { get; }
        GridDirection OutputBDirection { get; }
        bool HasOutputPair { get; }
        ITransportItem PeekOutputA();
        ITransportItem PeekOutputB();
        bool TryTakeOutputPair(out ITransportItem itemA, out ITransportItem itemB);
    }
}
