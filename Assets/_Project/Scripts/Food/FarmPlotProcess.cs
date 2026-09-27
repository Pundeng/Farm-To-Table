using System;

namespace CozyFoodFactory.Food
{
    public sealed class FarmPlotProcess
    {
        private int matureCount;
        private float elapsedTime;

        public FarmPlotProcess(int matureCapacity)
        {
            if (matureCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(matureCapacity));
            }

            MatureCapacity = matureCapacity;
        }

        public CropDefinition SelectedCrop { get; private set; }

        public int MatureCapacity { get; }

        public int MatureCount => matureCount;

        public bool HasMatureCrop => matureCount > 0;

        public float ElapsedTime => elapsedTime;

        public void SelectCrop(CropDefinition crop)
        {
            crop?.Validate();
            if (ReferenceEquals(SelectedCrop, crop))
            {
                return;
            }

            SelectedCrop = crop;
            elapsedTime = 0f;
            matureCount = 0;
        }

        public void Restore(CropDefinition crop, int mature, float elapsed)
        {
            if (mature < 0 || mature > MatureCapacity || elapsed < 0f ||
                float.IsNaN(elapsed) || float.IsInfinity(elapsed) ||
                (crop == null && (mature != 0 || elapsed != 0f)))
            {
                throw new ArgumentException("Invalid Farm Plot restore state.");
            }

            crop?.Validate();
            SelectedCrop = crop;
            matureCount = mature;
            elapsedTime = elapsed;
        }

        public int Advance(float deltaTime)
        {
            if (deltaTime < 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            }

            if (SelectedCrop == null || matureCount >= MatureCapacity)
            {
                return 0;
            }

            elapsedTime += deltaTime;
            int produced = 0;
            while (elapsedTime >= SelectedCrop.ProductionDuration &&
                   matureCount < MatureCapacity)
            {
                elapsedTime -= SelectedCrop.ProductionDuration;
                matureCount++;
                produced++;
            }

            if (matureCount >= MatureCapacity)
            {
                elapsedTime = 0f;
            }

            return produced;
        }

        public bool TryHarvest(out FoodItemData item)
        {
            if (!HasMatureCrop || SelectedCrop == null)
            {
                item = null;
                return false;
            }

            matureCount--;
            item = new FoodItemData(SelectedCrop.Output.Id,
                SelectedCrop.Output.Kind, SelectedCrop.Output.SellValue);
            return true;
        }
    }
}
