using System;

namespace CozyFoodFactory.Food
{
    public enum ProcessorState
    {
        Idle,
        Processing,
        WaitingForOutput
    }

    public sealed class ProcessorProcess
    {
        private readonly ProcessingRecipeCatalog catalog;
        private readonly RecipeDiscoveryRegistry discoveries;
        private readonly float duration;
        private ProcessingRecipe activeRecipe;
        private FoodItemData output;
        private float elapsed;

        public ProcessorProcess(ProcessingRecipeCatalog catalog, float duration,
            RecipeDiscoveryRegistry discoveries = null)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.discoveries = discoveries;
            if (duration <= 0f || float.IsNaN(duration) || float.IsInfinity(duration))
            {
                throw new ArgumentOutOfRangeException(nameof(duration));
            }

            this.duration = duration;
        }

        public ProcessorState State { get; private set; }
        public CookingProperty ActiveProperty { get; private set; }
        public bool HasOutput => State == ProcessorState.WaitingForOutput;
        public FoodItemData PeekOutput() => HasOutput ? output : null;
        public FoodItemData ActiveInput => activeRecipe?.Input;
        public FoodItemData PendingOutput => output;
        public float ElapsedTime => elapsed;

        public void Restore(ProcessorState state, FoodItemData input,
            CookingProperty property, FoodItemData pendingOutput, float elapsedSeconds)
        {
            if (elapsedSeconds < 0f || float.IsNaN(elapsedSeconds) ||
                float.IsInfinity(elapsedSeconds))
            {
                throw new ArgumentException("Invalid Processor timer.");
            }

            ProcessingRecipe recipe = null;
            if (state != ProcessorState.Idle &&
                (input == null || pendingOutput == null ||
                 catalog.Find(input, property, out recipe) != ProcessingRecipeMatch.Unique ||
                 !recipe.Output.Equals(pendingOutput)))
            {
                throw new ArgumentException("Processor recipe cannot be restored.");
            }

            activeRecipe = recipe;
            output = pendingOutput;
            ActiveProperty = property;
            elapsed = elapsedSeconds;
            State = state;
        }

        public ProcessingRecipeMatch Evaluate(FoodItemData food,
            CookingProperty property, bool hasSupply)
        {
            return State == ProcessorState.Idle && hasSupply
                ? catalog.Find(food, property, out _)
                : ProcessingRecipeMatch.None;
        }

        public bool TryAccept(FoodItemData food, CookingProperty property,
            bool hasSupply)
        {
            if (State != ProcessorState.Idle || !hasSupply ||
                catalog.Find(food, property, out ProcessingRecipe recipe) !=
                ProcessingRecipeMatch.Unique)
            {
                return false;
            }

            output = recipe.Output;
            activeRecipe = recipe;
            ActiveProperty = property;
            elapsed = 0f;
            State = ProcessorState.Processing;
            return true;
        }

        public bool Advance(float deltaTime, bool hasSupply)
        {
            if (deltaTime < 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            }

            if (State != ProcessorState.Processing || !hasSupply)
            {
                return false;
            }

            elapsed += deltaTime;
            if (elapsed < duration)
            {
                return false;
            }

            State = ProcessorState.WaitingForOutput;
            discoveries?.Record(activeRecipe);
            return true;
        }

        public bool TryTakeOutput(out FoodItemData food)
        {
            if (!HasOutput)
            {
                food = null;
                return false;
            }

            food = output;
            output = null;
            activeRecipe = null;
            elapsed = 0f;
            State = ProcessorState.Idle;
            return true;
        }
    }
}
