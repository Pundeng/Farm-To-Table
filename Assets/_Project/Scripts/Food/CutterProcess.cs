using System;

namespace FantasyShapez.Food
{
    public enum CutterState { Idle, Processing, WaitingForOutput, WaitingForOutputs }

    public sealed class CutterProcess
    {
        private readonly CuttingRecipeCatalog catalog;
        private readonly RecipeDiscoveryRegistry discoveries;
        private readonly float duration;
        private CuttingRecipe activeRecipe;
        private float elapsed;

        public CutterProcess(CuttingRecipeCatalog catalog, float duration,
            RecipeDiscoveryRegistry discoveries = null)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            if (duration <= 0f || float.IsNaN(duration) || float.IsInfinity(duration))
                throw new ArgumentOutOfRangeException(nameof(duration));
            this.duration = duration;
            this.discoveries = discoveries;
        }

        public CutterState State { get; private set; }
        public FoodItemData Input => activeRecipe?.Input;
        public FoodItemData Output => activeRecipe?.Output;
        public float Elapsed => elapsed;
        public bool HasOutputPair => State == CutterState.WaitingForOutput;

        public bool CanAccept(FoodItemData food) =>
            State == CutterState.Idle &&
            catalog.Find(food, out _) == ProcessingRecipeMatch.Unique;

        public bool TryAccept(FoodItemData food)
        {
            if (State != CutterState.Idle ||
                catalog.Find(food, out CuttingRecipe recipe) !=
                ProcessingRecipeMatch.Unique) return false;
            activeRecipe = recipe;
            elapsed = 0f;
            State = CutterState.WaitingForOutputs;
            return true;
        }

        public bool Advance(float deltaTime, bool outputsAvailable)
        {
            if (deltaTime < 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (State == CutterState.WaitingForOutputs && outputsAvailable)
                State = CutterState.Processing;
            if (State != CutterState.Processing || !outputsAvailable) return false;
            elapsed = Math.Min(duration, elapsed + deltaTime);
            if (elapsed < duration || !outputsAvailable) return false;
            State = CutterState.WaitingForOutput;
            discoveries?.Record(activeRecipe);
            return true;
        }

        public bool TryTakePair(out FoodItemData a, out FoodItemData b)
        {
            a = b = HasOutputPair ? activeRecipe.Output : null;
            if (a == null) return false;
            activeRecipe = null;
            elapsed = 0f;
            State = CutterState.Idle;
            return true;
        }

        public void Restore(CutterState state, FoodItemData input,
            FoodItemData output, float elapsedSeconds)
        {
            if (!Enum.IsDefined(typeof(CutterState), state) ||
                elapsedSeconds < 0f || elapsedSeconds > duration ||
                float.IsNaN(elapsedSeconds) || float.IsInfinity(elapsedSeconds))
                throw new ArgumentException("Invalid Cutter timer or state.");
            CuttingRecipe recipe = null;
            if (state == CutterState.Idle
                ? input != null || output != null || elapsedSeconds != 0f
                : input == null || output == null ||
                  catalog.Find(input, out recipe) != ProcessingRecipeMatch.Unique ||
                  !recipe.Output.Equals(output) ||
                  state == CutterState.WaitingForOutputs && elapsedSeconds != 0f ||
                  state == CutterState.WaitingForOutput && elapsedSeconds != duration)
                throw new ArgumentException("Cutter recipe cannot be restored.");
            activeRecipe = recipe;
            elapsed = elapsedSeconds;
            State = state;
        }
    }
}
