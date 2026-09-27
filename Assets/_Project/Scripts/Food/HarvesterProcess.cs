using System;
using System.Collections.Generic;

namespace CozyFoodFactory.Food
{
    public sealed class HarvesterProcess
    {
        private readonly Queue<FoodItemData> outputs = new();
        private float elapsedTime;

        public HarvesterProcess(float harvestInterval, int outputCapacity)
        {
            if (harvestInterval <= 0f || float.IsNaN(harvestInterval) ||
                float.IsInfinity(harvestInterval))
            {
                throw new ArgumentOutOfRangeException(nameof(harvestInterval));
            }

            if (outputCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(outputCapacity));
            }

            HarvestInterval = harvestInterval;
            OutputCapacity = outputCapacity;
        }

        public float HarvestInterval { get; }

        public int OutputCapacity { get; }

        public int OutputCount => outputs.Count;
        public float ElapsedTime => elapsedTime;
        public IReadOnlyList<FoodItemData> Outputs => outputs.ToArray();

        public bool HasOutput => outputs.Count > 0;

        public void Restore(IReadOnlyList<FoodItemData> savedOutputs, float elapsed)
        {
            if (savedOutputs == null || savedOutputs.Count > OutputCapacity ||
                elapsed < 0f || float.IsNaN(elapsed) || float.IsInfinity(elapsed))
            {
                throw new ArgumentException("Invalid Harvester restore state.");
            }

            outputs.Clear();
            foreach (FoodItemData food in savedOutputs)
            {
                outputs.Enqueue(food ?? throw new ArgumentException(
                    "Harvester output is missing."));
            }

            elapsedTime = elapsed;
        }

        public int Advance(float deltaTime, FarmPlotProcess farmPlot)
        {
            if (deltaTime < 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            }

            if (farmPlot == null || !farmPlot.HasMatureCrop)
            {
                elapsedTime = 0f;
                return 0;
            }

            if (OutputCount >= OutputCapacity)
            {
                return 0;
            }

            elapsedTime += deltaTime;
            int harvested = 0;
            while (elapsedTime >= HarvestInterval &&
                   OutputCount < OutputCapacity &&
                   farmPlot.TryHarvest(out FoodItemData crop))
            {
                elapsedTime -= HarvestInterval;
                outputs.Enqueue(crop);
                harvested++;
            }

            if (OutputCount >= OutputCapacity || !farmPlot.HasMatureCrop)
            {
                elapsedTime = 0f;
            }

            return harvested;
        }

        public FoodItemData PeekOutput()
        {
            return HasOutput ? outputs.Peek() : null;
        }

        public bool TryTakeOutput(out FoodItemData item)
        {
            if (!HasOutput)
            {
                item = null;
                return false;
            }

            item = outputs.Dequeue();
            return true;
        }
    }
}
