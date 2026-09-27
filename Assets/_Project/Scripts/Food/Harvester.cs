using System;
using System.Linq;
using CozyFoodFactory.Buildings;
using CozyFoodFactory.Logistics;
using UnityEngine;

namespace CozyFoodFactory.Food
{
    public sealed class Harvester : MonoBehaviour, IItemOutputSource
    {
        [SerializeField, Min(0.01f)] private float harvestInterval = 1f;
        [SerializeField, Min(1)] private int outputCapacity = 4;

        private HarvesterProcess process;
        private BeltTransportCoordinator transportCoordinator;

        public Vector2Int FarmCell { get; private set; }

        public Vector2Int OutputCell { get; private set; }

        public GridDirection OutputDirection { get; private set; }

        public int OutputCount => process?.OutputCount ?? 0;

        public int OutputCapacity => outputCapacity;

        public bool HasOutput => process?.HasOutput ?? false;

        public FarmPlot ConnectedFarmPlot => FarmPlot.GetAt(FarmCell);

        public SavedHarvester CaptureWorldState()
        {
            if (process == null)
            {
                throw new InvalidOperationException("Harvester is not initialized.");
            }

            return new SavedHarvester
            {
                outputs = process.Outputs.Select(SavedFood.From).ToArray(),
                elapsedSeconds = process.ElapsedTime
            };
        }

        public void RestoreWorldState(SavedHarvester saved) => process.Restore(
            saved.outputs.Select(food => food.ToFood()).ToArray(), saved.elapsedSeconds);

        public void Initialize(
            BuildingPlacement placement,
            BeltTransportCoordinator coordinator)
        {
            if (placement == null)
            {
                throw new ArgumentNullException(nameof(placement));
            }

            transportCoordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            process = new HarvesterProcess(harvestInterval, outputCapacity);
            OutputDirection = placement.Rotation.ToGridDirection();
            FarmCell = HarvesterPlacementBehavior.GetFarmCell(
                placement.AnchorCell, placement.Footprint, placement.Rotation);
            OutputCell = FarmCell + OutputDirection.ToOffset() * 2;
            transportCoordinator.RegisterOutputSource(this);
            CreateOutputArrow();
        }

        public ITransportItem PeekOutput() => process?.PeekOutput();

        public bool TryTakeOutput(out ITransportItem item)
        {
            if (process != null && process.TryTakeOutput(out FoodItemData crop))
            {
                item = crop;
                return true;
            }

            item = null;
            return false;
        }

        private void Update()
        {
            if (!FactoryWorldLoadSession.IsReconstructing)
            {
                process?.Advance(Time.deltaTime, ConnectedFarmPlot?.Process);
            }
        }

        private void OnValidate()
        {
            harvestInterval = Mathf.Max(0.01f, harvestInterval);
            outputCapacity = Mathf.Max(1, outputCapacity);
        }

        private void OnDestroy()
        {
            transportCoordinator?.UnregisterOutputSource(this);
        }

        private void CreateOutputArrow()
        {
            if (transform.Find("Output Arrow Shaft") != null)
            {
                return;
            }

            CreateArrowPart("Output Arrow Shaft", new Vector2(0f, 0.6f),
                new Vector2(0.08f, 0.28f), 0f);
            CreateArrowPart("Output Arrow Left", new Vector2(-0.08f, 0.72f),
                new Vector2(0.08f, 0.18f), -45f);
            CreateArrowPart("Output Arrow Right", new Vector2(0.08f, 0.72f),
                new Vector2(0.08f, 0.18f), 45f);
        }

        private void CreateArrowPart(string name, Vector2 position, Vector2 scale, float angle)
        {
            var part = new GameObject(name);
            part.transform.SetParent(transform, false);
            part.transform.localPosition = new Vector3(position.x, position.y, -0.02f);
            part.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            part.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            SpriteRenderer renderer = part.AddComponent<SpriteRenderer>();
            renderer.sprite = BuildingVisualFactory.PlaceholderSprite;
            renderer.color = Color.white;
            renderer.sortingOrder = 15;
        }
    }
}
