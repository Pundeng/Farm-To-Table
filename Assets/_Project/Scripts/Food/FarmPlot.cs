using System;
using System.Collections.Generic;
using UnityEngine;

namespace FantasyShapez.Food
{
    public sealed class FarmPlot : MonoBehaviour
    {
        public event Action<FarmPlot> CropChanging;
        public event Action<FarmPlot> CropChanged;
        [SerializeField] private CropDefinition[] availableCrops =
        {
            new CropDefinition("Apple",
                new FoodItemData("apple", FoodItemKind.RawIngredient), 2f)
        };
        [SerializeField, Min(1)] private int matureCapacity = 4;

        private static readonly Dictionary<Vector2Int, FarmPlot> PlotsByCell = new();
        private FarmPlotProcess process;
        private UnlockState unlocks;
        private Vector2Int cell;
        private bool initialized;

        public IReadOnlyList<CropDefinition> AvailableCrops => availableCrops;

        public CropDefinition SelectedCrop => process?.SelectedCrop;

        public int MatureCount => process?.MatureCount ?? 0;

        public int MatureCapacity => matureCapacity;

        internal FarmPlotProcess Process => process;

        public SavedFarmPlot CaptureWorldState()
        {
            if (process == null)
            {
                throw new InvalidOperationException("Farm Plot is not initialized.");
            }

            return new SavedFarmPlot
            {
                cropId = SelectedCrop?.Id,
                matureCount = process.MatureCount,
                elapsedSeconds = process.ElapsedTime
            };
        }

        public void RestoreWorldState(SavedFarmPlot saved)
        {
            CropDefinition crop = string.IsNullOrEmpty(saved.cropId) ? null :
                Array.Find(availableCrops, candidate => candidate.Id == saved.cropId);
            if (!string.IsNullOrEmpty(saved.cropId) &&
                (crop == null || !IsCropUnlocked(crop)))
            {
                throw new ArgumentException("Saved Farm Plot crop is unavailable.");
            }

            process.Restore(crop, saved.matureCount, saved.elapsedSeconds);
        }

        public void Initialize(Vector2Int anchorCell,
            UnlockState unlocks = null)
        {
            if (initialized)
            {
                throw new InvalidOperationException("The Farm Plot is already initialized.");
            }

            if (PlotsByCell.TryGetValue(anchorCell, out FarmPlot existing) && existing != null)
            {
                throw new InvalidOperationException($"A Farm Plot already exists at {anchorCell}.");
            }

            process = new FarmPlotProcess(matureCapacity);
            this.unlocks = unlocks;
            if (unlocks != null)
            {
                unlocks.Restored += ReconcileSelectedCrop;
            }
            cell = anchorCell;
            initialized = true;
            PlotsByCell[anchorCell] = this;
        }

        public static FarmPlot GetAt(Vector2Int cell)
        {
            return PlotsByCell.TryGetValue(cell, out FarmPlot plot) && plot != null
                ? plot : null;
        }

        public void SelectCrop(CropDefinition crop)
        {
            if (process == null)
            {
                throw new InvalidOperationException("The Farm Plot is not initialized.");
            }

            if (crop != null && Array.IndexOf(availableCrops, crop) < 0)
            {
                throw new ArgumentException("The crop is not available on this Farm Plot.", nameof(crop));
            }

            if (crop != null && !IsCropUnlocked(crop))
            {
                throw new InvalidOperationException("The crop is not unlocked.");
            }

            CropChanging?.Invoke(this);
            process.SelectCrop(crop);
            CropChanged?.Invoke(this);
        }

        public bool IsCropUnlocked(CropDefinition crop) =>
            crop != null &&
            (string.IsNullOrEmpty(crop.RequiredUnlockId) ||
             unlocks?.IsUnlocked(UnlockKey.CropCategory, crop.RequiredUnlockId) == true);

        private void Update()
        {
            if (!FactoryWorldLoadSession.IsReconstructing)
            {
                process?.Advance(Time.deltaTime);
            }
        }

        private void OnValidate()
        {
            matureCapacity = Mathf.Max(1, matureCapacity);
        }

        private void ReconcileSelectedCrop()
        {
            if (SelectedCrop != null && !IsCropUnlocked(SelectedCrop))
            {
                process.SelectCrop(null);
            }
        }

        private void OnDestroy()
        {
            if (unlocks != null)
            {
                unlocks.Restored -= ReconcileSelectedCrop;
            }

            if (initialized && PlotsByCell.TryGetValue(cell, out FarmPlot registered) &&
                ReferenceEquals(registered, this))
            {
                PlotsByCell.Remove(cell);
            }
        }
    }
}
