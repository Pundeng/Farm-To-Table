using System;
using System.Collections.Generic;
using FantasyShapez.Grid;
using FantasyShapez.Food;
using UnityEngine;

namespace FantasyShapez.Logistics
{
    [DefaultExecutionOrder(100)]
    public sealed class BeltTransportCoordinator : MonoBehaviour
    {
        [SerializeField] private GridSystem gridSystem = null;
        [SerializeField, Min(0.01f)] private float movementSpeed = 1f;

        private readonly Dictionary<BeltCell, Belt> beltViews = new();
        private BeltTransportSystem transportSystem;

        public BeltCell RegisterBelt(Belt belt, Vector2Int cell, GridDirection direction)
        {
            BeltCell beltCell = GetSystem().AddBelt(cell, direction);
            beltViews.Add(beltCell, belt);
            return beltCell;
        }

        public void UnregisterBelt(BeltCell beltCell)
        {
            if (beltCell != null && GetSystem().RemoveBelt(beltCell))
            {
                beltViews.Remove(beltCell);
            }
        }

        public void RegisterOutputSource(IRuneOutputSource source)
        {
            GetSystem().RegisterOutputSource(source);
        }

        public void RegisterOutputSource(IItemOutputSource source)
        {
            GetSystem().RegisterOutputSource(source);
        }

        public void RegisterOutputPair(IItemOutputPairSource source) =>
            GetSystem().RegisterOutputPair(source);

        public void UnregisterOutputPair(IItemOutputPairSource source) =>
            GetSystem().UnregisterOutputPair(source);

        public bool CanAcceptOutputPair(Vector2Int cellA, Vector2Int cellB) =>
            GetSystem().CanAcceptOutputPair(cellA, cellB);

        public bool CanAcceptOutput(Vector2Int cell) =>
            GetSystem().CanAcceptOutput(cell);

        public void RegisterInputReceiver(IRuneInputReceiver receiver)
        {
            GetSystem().RegisterInputReceiver(receiver);
        }

        public void RegisterInputReceiver(IItemInputReceiver receiver)
        {
            GetSystem().RegisterInputReceiver(receiver);
        }

        public void UnregisterInputReceiver(IRuneInputReceiver receiver)
        {
            GetSystem().UnregisterInputReceiver(receiver);
        }

        public void UnregisterInputReceiver(IItemInputReceiver receiver)
        {
            GetSystem().UnregisterInputReceiver(receiver);
        }

        public void UnregisterOutputSource(IRuneOutputSource source)
        {
            GetSystem().UnregisterOutputSource(source);
        }

        public void UnregisterOutputSource(IItemOutputSource source)
        {
            GetSystem().UnregisterOutputSource(source);
        }

        private void LateUpdate()
        {
            // Extractors produce in Update; one coordinated LateUpdate owns every belt move.
            if (!FactoryWorldLoadSession.IsReconstructing)
            {
                GetSystem().Advance(Time.deltaTime);
            }

            foreach (KeyValuePair<BeltCell, Belt> beltView in beltViews)
            {
                beltView.Value.RefreshItemVisual(gridSystem, beltView.Key);
            }
        }

        private BeltTransportSystem GetSystem()
        {
            return transportSystem ??= new BeltTransportSystem(movementSpeed);
        }

        private void OnValidate()
        {
            movementSpeed = Mathf.Max(0.01f, movementSpeed);
            transportSystem = null;
        }
    }
}
