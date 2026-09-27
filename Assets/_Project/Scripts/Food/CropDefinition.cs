using System;
using UnityEngine;

namespace CozyFoodFactory.Food
{
    [Serializable]
    public sealed class CropDefinition
    {
        [SerializeField] private string id;
        [SerializeField] private FoodItemData output;
        [SerializeField, Min(0.01f)] private float productionDuration;
        [SerializeField] private string requiredUnlockId;

        public CropDefinition(string id, FoodItemData output, float productionDuration,
            string requiredUnlockId = null)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("A crop ID is required.", nameof(id));
            }

            this.output = output ?? throw new ArgumentNullException(nameof(output));
            if (output.Kind != FoodItemKind.RawIngredient)
            {
                throw new ArgumentException("A crop must produce a raw ingredient.", nameof(output));
            }

            if (productionDuration <= 0f || float.IsNaN(productionDuration) ||
                float.IsInfinity(productionDuration))
            {
                throw new ArgumentOutOfRangeException(nameof(productionDuration));
            }

            this.id = id;
            this.productionDuration = productionDuration;
            this.requiredUnlockId = requiredUnlockId;
        }

        public string Id => id;

        public FoodItemData Output => output;

        public float ProductionDuration => productionDuration;
        public string RequiredUnlockId => requiredUnlockId;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(id) || output == null ||
                output.Kind != FoodItemKind.RawIngredient || !output.IsValid ||
                productionDuration <= 0f || float.IsNaN(productionDuration) ||
                float.IsInfinity(productionDuration))
            {
                throw new InvalidOperationException("The crop definition is invalid.");
            }
        }
    }
}
