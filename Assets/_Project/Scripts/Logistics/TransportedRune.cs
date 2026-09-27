using System;
using UnityEngine;

namespace FantasyShapez.Logistics
{
    public sealed class TransportedRune
    {
        public TransportedRune(ITransportItem item, GridDirection entryDirection)
        {
            Item = item ?? throw new ArgumentNullException(nameof(item));
            EnterFrom(entryDirection);
        }

        public ITransportItem Item { get; }

        public GridDirection EntryDirection { get; private set; }

        public float Progress { get; private set; }

        internal void Advance(float distance)
        {
            Progress = Mathf.Clamp01(Progress + distance);
        }

        internal void RestoreProgress(float progress)
        {
            if (progress < 0f || progress > 1f || float.IsNaN(progress))
            {
                throw new ArgumentOutOfRangeException(nameof(progress));
            }

            Progress = progress;
        }

        internal void EnterFrom(GridDirection entryDirection)
        {
            EntryDirection = entryDirection;
            Progress = 0f;
        }
    }
}
