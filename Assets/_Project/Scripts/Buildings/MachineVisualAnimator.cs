using System;
using CozyFoodFactory.CameraControl;
using CozyFoodFactory.Food;
using CozyFoodFactory.Logistics;
using UnityEngine;

namespace CozyFoodFactory.Buildings
{
    public static class MachineAnimationDecisions
    {
        public static bool OutputCreated(bool observed, bool previous, bool current) =>
            observed && !previous && current;

        public static bool Harvested(bool observed, int previous, int current) =>
            observed && current > previous;

        public static bool Processing(ProcessorState state, bool supplied) =>
            state == ProcessorState.Processing && supplied;

        public static bool Cutting(CutterState state, bool outputsAvailable) =>
            state == CutterState.Processing && outputsAvailable;

        public static bool ShowMotion(WorldInformationLevel level, bool detail) =>
            detail || level != WorldInformationLevel.Far;
    }

    // Presentation only. The animated child never owns ports, occupancy, or simulation state.
    [DefaultExecutionOrder(50)]
    public sealed class MachineVisualAnimator : MonoBehaviour
    {
        private FarmPlot farm;
        private Harvester harvester;
        private Processor processor;
        private BasicMixer mixer;
        private Cutter cutter;
        private TradeBuilding trade;
        private int previousTradeOutput;
        private Market market;
        private BeltTransportCoordinator transport;
        private MachineFeedbackView feedbackView;
        private Transform part;
        private SpriteRenderer partRenderer;
        private Vector3 baseScale;
        private WorldInformationLevel informationLevel;
        private bool forceDetail;
        private bool observed;
        private bool previousOutput;
        private string previousMixerEvent;
        private int previousHarvestCount;
        private int previousFarmMatureCount;
        private CropDefinition previousCrop;
        private ProcessorState previousProcessorState;
        private CutterState previousCutterState;
        private float reactionUntil;

        public void Initialize(GameObject visualRoot, float cellSize,
            BeltTransportCoordinator coordinator)
        {
            farm = GetComponent<FarmPlot>();
            harvester = GetComponent<Harvester>();
            processor = GetComponent<Processor>();
            mixer = GetComponent<BasicMixer>();
            cutter = GetComponent<Cutter>();
            trade = GetComponent<TradeBuilding>();
            transport = coordinator;
            feedbackView = GetComponent<MachineFeedbackView>();
            CreatePart(visualRoot.transform, cellSize,
                farm != null ? new Color(0.48f, 0.88f, 0.35f) :
                harvester != null ? new Color(1f, 0.8f, 0.38f) :
                processor != null ? new Color(1f, 0.62f, 0.34f) :
                mixer != null ? new Color(0.68f, 0.88f, 1f) :
                new Color(1f, 0.78f, 0.5f),
                farm != null ? 0.18f : 0.28f, 19);
        }

        public void InitializeMarket(Market owner)
        {
            market = owner;
            CreatePart(owner.transform, 1f, new Color(1f, 0.9f, 0.52f),
                0.22f, 11);
            market.FoodDelivered += OnFoodDelivered;
        }

        public void SetInformationLevel(WorldInformationLevel level, bool detail)
        {
            informationLevel = level;
            forceDetail = detail;
        }

        private void CreatePart(Transform parent, float cellSize, Color color,
            float size, int sortingOrder)
        {
            var anchor = new GameObject("Animated Parts");
            anchor.transform.SetParent(parent, false);
            anchor.transform.localPosition = new Vector3(0f, 0f, -0.03f);
            part = anchor.transform;
            baseScale = Vector3.one * (cellSize * size);
            part.localScale = baseScale;
            partRenderer = anchor.AddComponent<SpriteRenderer>();
            partRenderer.sprite = BuildingVisualFactory.PlaceholderSprite;
            partRenderer.color = color;
            partRenderer.sortingOrder = sortingOrder;
        }

        private void OnFoodDelivered(FoodItemData food, int count) => React();

        private void React() => reactionUntil = Time.time + 0.35f;

        private void LateUpdate()
        {
            if (part == null || FactoryWorldLoadSession.IsReconstructing) return;
            bool working = false;
            float growth = 0f;
            bool output = mixer != null && mixer.HasOutput ||
                processor != null && processor.HasOutput ||
                cutter != null && cutter.HasOutputPair;
            if (farm != null && farm.SelectedCrop != null)
            {
                FarmPlotProcess process = farm.Process;
                growth = process.MatureCount > 0 ? 1f :
                    Mathf.Clamp01(process.ElapsedTime /
                        farm.SelectedCrop.ProductionDuration);
            }
            if (harvester != null)
            {
                FarmPlot connectedFarm = harvester.ConnectedFarmPlot;
                CropDefinition crop = connectedFarm?.SelectedCrop;
                int matureCount = connectedFarm?.MatureCount ?? 0;
                if (MachineAnimationDecisions.Harvested(observed,
                        previousHarvestCount, harvester.OutputCount) ||
                    observed && crop != null && crop == previousCrop &&
                    matureCount < previousFarmMatureCount) React();
                previousCrop = crop;
                previousFarmMatureCount = matureCount;
            }
            if (MachineAnimationDecisions.OutputCreated(observed,
                    previousOutput, output)) React();
            if (mixer != null)
            {
                string currentEvent = mixer.LastEvent;
                if (observed && !ReferenceEquals(currentEvent, previousMixerEvent) &&
                    currentEvent != null && currentEvent.StartsWith(
                        "Output ", StringComparison.Ordinal)) React();
                previousMixerEvent = currentEvent;
            }
            if (processor != null)
            {
                working = MachineAnimationDecisions.Processing(processor.State,
                    feedbackView != null &&
                    feedbackView.Feedback.State == MachineFeedbackState.Working);
                if (observed && previousProcessorState == ProcessorState.Processing &&
                    processor.State == ProcessorState.WaitingForOutput) React();
                previousProcessorState = processor.State;
            }
            if (cutter != null)
            {
                working = MachineAnimationDecisions.Cutting(cutter.State,
                    transport != null && transport.CanAcceptOutputPair(
                        cutter.OutputACell, cutter.OutputBCell));
                if (observed && previousCutterState == CutterState.Processing &&
                    cutter.State == CutterState.WaitingForOutput) React();
                previousCutterState = cutter.State;
            }
            if (trade != null)
            {
                working = trade.Process.PendingOutput > 0 && !trade.OutputBlocked;
                if (observed && trade.Process.PendingOutput > previousTradeOutput) React();
                previousTradeOutput = trade.Process.PendingOutput;
            }
            previousOutput = output;
            if (harvester != null) previousHarvestCount = harvester.OutputCount;
            observed = true;

            bool visible = MachineAnimationDecisions.ShowMotion(
                informationLevel, forceDetail);
            if (!visible)
            {
                partRenderer.enabled = false;
                return;
            }
            bool reacting = Time.time < reactionUntil;
            partRenderer.enabled = market != null ? reacting :
                farm != null ? farm.SelectedCrop != null :
                working || reacting;
            if (!partRenderer.enabled) return;
            float phase = Time.time * (processor != null ? 4f : 7f);
            float activity = reacting ? 1f - (reactionUntil - Time.time) / 0.35f : 0f;
            float scale = farm != null ? 0.45f + growth * 0.8f :
                reacting ? 1f + 0.35f * Mathf.Sin(activity * Mathf.PI) : 1f;
            part.localScale = baseScale * scale;
            part.localRotation = mixer != null && reacting
                ? Quaternion.Euler(0f, 0f, activity * 270f) :
                cutter != null && working
                    ? Quaternion.Euler(0f, 0f, Mathf.Sin(phase) * 18f) :
                    Quaternion.identity;
            if (processor != null && working)
                part.localPosition = new Vector3(0f,
                    Mathf.Sin(phase) * 0.035f, -0.03f);
            else if (harvester != null && reacting)
                part.localPosition = new Vector3(
                    Mathf.Sin(activity * Mathf.PI) * 0.12f, 0f, -0.03f);
            else part.localPosition = new Vector3(0f, 0f, -0.03f);
        }

        private void OnDestroy()
        {
            if (market != null) market.FoodDelivered -= OnFoodDelivered;
        }
    }

}
