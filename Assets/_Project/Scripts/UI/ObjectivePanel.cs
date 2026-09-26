using FantasyShapez.Buildings;
using FantasyShapez.Food;
using FantasyShapez.Objectives;
using FantasyShapez.Production;
using FantasyShapez.Runes;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FantasyShapez.UI
{
    public sealed class ObjectivePanel : MonoBehaviour
    {
        [SerializeField] private Hub hub = null;
        private static readonly RuneSigil[] EngraverSigilOptions =
        {
            RuneSigil.Spirit,
            RuneSigil.Acceleration
        };
        private static readonly RuneElement[] InfuserElementOptions =
        {
            RuneElement.Fire,
            RuneElement.Water,
            RuneElement.Earth,
            RuneElement.Wind
        };

        private Engraver selectedEngraver;
        private ElementInfuser selectedInfuser;
        private FarmPlot selectedFarmPlot;
        private Harvester selectedHarvester;
        private Processor selectedProcessor;
        private BasicMixer selectedMixer;
        private Cutter selectedCutter;
        private BuildingPlacementController demoController;
        private Vector2 configurationScroll;

        public bool HasConfiguration => GetConfigurationPanelHeight() > 0f;
        public void CloseConfiguration()
        {
            selectedEngraver = null;
            selectedInfuser = null;
            selectedFarmPlot = null;
            selectedHarvester = null;
            selectedProcessor = null;
            selectedMixer = null;
            selectedCutter = null;
        }

        private void Awake() => demoController = GetComponent<BuildingPlacementController>();

        public bool IsPointerOverPanel
        {
            get
            {
                if (!isActiveAndEnabled || Mouse.current == null)
                {
                    return false;
                }

                Vector2 pointer = Mouse.current.position.ReadValue();
                pointer.y = Screen.height - pointer.y;
                float configurationHeight = GetConfigurationPanelHeight();
                return (hub != null && hub.Progress != null &&
                        new Rect(16f, 16f, 320f, GetObjectivePanelHeight()).Contains(pointer)) ||
                    (configurationHeight > 0f &&
                     new Rect(ConfigurationX, ConfigurationY, 300f,
                         VisibleConfigurationHeight(configurationHeight))
                         .Contains(pointer));
            }
        }

        public void Configure(Hub objectiveHub)
        {
            hub = objectiveHub;
        }

        public void ShowEngraver(Engraver engraver)
        {
            selectedCutter = null;
            selectedEngraver = engraver;
            selectedInfuser = null;
            selectedFarmPlot = null;
            selectedHarvester = null;
            selectedProcessor = null;
            selectedMixer = null;
        }

        public void ShowElementInfuser(ElementInfuser infuser)
        {
            selectedCutter = null;
            selectedEngraver = null;
            selectedInfuser = infuser;
            selectedFarmPlot = null;
            selectedHarvester = null;
            selectedProcessor = null;
            selectedMixer = null;
        }

        public void ShowFarmPlot(FarmPlot farmPlot)
        {
            selectedCutter = null;
            selectedEngraver = null;
            selectedInfuser = null;
            selectedFarmPlot = farmPlot;
            selectedHarvester = null;
            selectedProcessor = null;
            selectedMixer = null;
        }

        public void ShowHarvester(Harvester harvester)
        {
            selectedCutter = null;
            selectedEngraver = null;
            selectedInfuser = null;
            selectedFarmPlot = null;
            selectedHarvester = harvester;
            selectedProcessor = null;
            selectedMixer = null;
        }

        public void ShowProcessor(Processor processor)
        {
            selectedCutter = null;
            selectedEngraver = null;
            selectedInfuser = null;
            selectedFarmPlot = null;
            selectedHarvester = null;
            selectedProcessor = processor;
            selectedMixer = null;
        }

        public void ShowMixer(BasicMixer mixer)
        {
            selectedCutter = null;
            selectedEngraver = null;
            selectedInfuser = null;
            selectedFarmPlot = null;
            selectedHarvester = null;
            selectedProcessor = null;
            selectedMixer = mixer;
        }

        public void ShowCutter(Cutter cutter)
        {
            ShowMixer(null);
            selectedCutter = cutter;
        }

        private void OnGUI()
        {
            if (hub != null && hub.Progress != null)
            {
                DrawObjectivePanel();
            }

            if (demoController == null || !demoController.IsFoodDemo ||
                demoController.OpenDemoPanel == BuildingPlacementController.DemoPanel.Machine)
            {
                Matrix4x4 original = GUI.matrix;
                bool originalEnabled = GUI.enabled;
                if (demoController?.BlocksAllWorldInput == true) GUI.enabled = false;
                if (demoController != null && demoController.IsFoodDemo)
                    GUI.matrix = Matrix4x4.Translate(new Vector3(
                        ConfigurationX - 352f, ConfigurationY - 16f)) *
                        original;
                DrawMachineConfigurationPanel();
                GUI.matrix = original;
                GUI.enabled = originalEnabled;
                if (demoController != null && demoController.IsFoodDemo &&
                    !HasConfiguration &&
                    demoController.OpenDemoPanel == BuildingPlacementController.DemoPanel.Machine)
                    demoController.ClosePanel();
            }
        }

        private float ConfigurationX => demoController != null && demoController.IsFoodDemo
            ? Mathf.Max(8f, Screen.width - 308f) : 352f;
        private float ConfigurationY => demoController != null && demoController.IsFoodDemo &&
            Screen.width < 500f ? 76f : 16f;
        private float VisibleConfigurationHeight(float height) =>
            demoController != null && demoController.IsFoodDemo
                ? Mathf.Max(60f, Mathf.Min(height,
                    Screen.height - ConfigurationY - 114f)) : height;

        private void BeginConfiguration(float height)
        {
            float visible = VisibleConfigurationHeight(height);
            GUI.Box(new Rect(352f, 16f, 300f, visible), GUIContent.none);
            GUILayout.BeginArea(new Rect(364f, 24f, 276f, visible - 16f));
            if (demoController != null && demoController.IsFoodDemo)
                configurationScroll = GUILayout.BeginScrollView(configurationScroll);
        }

        private void EndConfiguration()
        {
            if (demoController != null && demoController.IsFoodDemo)
                GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawObjectivePanel()
        {
            float panelHeight = GetObjectivePanelHeight();
            var panelRect = new Rect(16f, 16f, 320f, panelHeight);
            GUI.Box(panelRect, GUIContent.none);
            GUILayout.BeginArea(new Rect(28f, 24f, 296f, panelHeight - 16f));

            if (hub.Progress.AreAllObjectivesComplete)
            {
                GUILayout.Label("MVP Objectives Complete");
            }
            else
            {
                ObjectiveDefinition current = hub.Progress.CurrentObjective;
                GUILayout.Label(current.DisplayName);
                for (int index = 0; index < current.Requirements.Count; index++)
                {
                    ObjectiveRequirement requirement = current.Requirements[index];
                    if (requirement.RequirementType == ObjectiveRequirementType.SustainedRate)
                    {
                        GUILayout.Label($"{requirement.TargetRune}: " +
                            $"{hub.Progress.GetCurrentRate(index):0.##} / " +
                            $"{requirement.TargetRatePerSecond:0.##} per sec");
                        GUILayout.Label($"Sustain: " +
                            $"{hub.Progress.GetSustainProgress(index):0.#} / " +
                            $"{requirement.SustainDurationSeconds:0.#} sec");
                    }
                    else
                    {
                        GUILayout.Label(
                            $"{requirement.TargetRune}: " +
                            $"{hub.Progress.GetCurrentCount(index)} / {requirement.RequiredCount}");
                    }
                }
            }

            if (!string.IsNullOrEmpty(hub.LastDeliveryMessage))
            {
                GUILayout.Label(hub.LastDeliveryMessage);
            }

            GUILayout.EndArea();
        }

        private float GetObjectivePanelHeight()
        {
            int requirementLineCount = 0;
            if (!hub.Progress.AreAllObjectivesComplete)
            {
                foreach (ObjectiveRequirement requirement in
                    hub.Progress.CurrentObjective.Requirements)
                {
                    requirementLineCount += requirement.RequirementType ==
                        ObjectiveRequirementType.SustainedRate ? 2 : 1;
                }
            }

            return 72f + requirementLineCount * 22f;
        }

        private float GetConfigurationPanelHeight()
        {
            if (selectedEngraver != null) return 214f;
            if (selectedInfuser != null) return 128f;
            if (selectedFarmPlot != null)
                return 132f + selectedFarmPlot.AvailableCrops.Count * 28f;
            if (selectedHarvester != null)
                return 150f + (selectedHarvester.ConnectedFarmPlot?.AvailableCrops.Count ?? 0) * 28f;
            if (selectedProcessor != null) return 190f;
            if (selectedMixer != null) return 180f;
            if (selectedCutter != null) return 180f;
            return 0f;
        }

        private void DrawMachineConfigurationPanel()
        {
            if (selectedEngraver != null)
            {
                DrawEngraverPanel();
            }
            else if (selectedInfuser != null)
            {
                DrawInfuserPanel();
            }
            else if (selectedFarmPlot != null)
            {
                DrawFarmPlotPanel();
            }
            else if (selectedHarvester != null)
            {
                DrawHarvesterPanel();
            }
            else if (selectedProcessor != null)
            {
                DrawProcessorPanel();
            }
            else if (selectedMixer != null)
            {
                DrawMixerPanel();
            }
            else if (selectedCutter != null)
            {
                DrawCutterPanel();
            }
        }

        private void DrawCutterPanel()
        {
            const float height = 180f;
            BeginConfiguration(height);
            GUILayout.Label("Cutter");
            if (demoController?.IsDevInspectorOpen == true || demoController?.IsFoodDemo != true)
            {
                GUILayout.Label($"Input: {selectedCutter.InputCell}");
                GUILayout.Label($"Outputs: {selectedCutter.OutputACell}, " +
                    selectedCutter.OutputBCell);
            }
            GUILayout.Label($"State: {selectedCutter.State}");
            GUILayout.Label(selectedCutter.LastEvent);
            if (GUILayout.Button("Close")) selectedCutter = null;
            EndConfiguration();
        }

        private void DrawMixerPanel()
        {
            const float height = 180f;
            BeginConfiguration(height);
            GUILayout.Label("Basic Mixer");
            GUILayout.Label("Input A: " +
                (selectedMixer.SlotA?.Id ?? "empty"));
            GUILayout.Label("Input B: " +
                (selectedMixer.SlotB?.Id ?? "empty"));
            GUILayout.Label(selectedMixer.HasOutput
                ? $"Output blocked: {selectedMixer.PeekOutput()?.ToString()}"
                : "Waiting for a valid ingredient pair.");
            GUILayout.Label($"Last event: {selectedMixer.LastEvent}");
            if (GUILayout.Button("Close"))
            {
                selectedMixer = null;
            }

            EndConfiguration();
        }

        private void DrawProcessorPanel()
        {
            const float height = 190f;
            BeginConfiguration(height);
            GUILayout.Label("Processor");
            GUILayout.Label($"State: {selectedProcessor.ProcessingStateMessage}");
            if (demoController?.IsDevInspectorOpen == true || demoController?.IsFoodDemo != true)
                GUILayout.Label($"Property port: {selectedProcessor.PropertyCell}");
            GUILayout.Label(selectedProcessor.SupplyMessage);
            GUILayout.Label($"Last event: {selectedProcessor.LastRecipeMessage}");
            if (selectedProcessor.HasOutput)
            {
                GUILayout.Label("Product output blocked; waiting for a belt.");
            }

            if (GUILayout.Button("Close"))
            {
                selectedProcessor = null;
            }

            EndConfiguration();
        }

        private void DrawFarmPlotPanel()
        {
            float height = 132f + selectedFarmPlot.AvailableCrops.Count * 28f;
            BeginConfiguration(height);
            GUILayout.Label("Farm Plot");
            GUILayout.Label($"Crop: {selectedFarmPlot.SelectedCrop?.Id ?? "None"}");
            GUILayout.Label($"Mature crops: {selectedFarmPlot.MatureCount} / " +
                selectedFarmPlot.MatureCapacity);
            if (selectedFarmPlot.SelectedCrop == null)
            {
                GUILayout.Label("Select a crop to start growing.");
            }
            DrawCropSelection(selectedFarmPlot);

            if (GUILayout.Button("Close"))
            {
                selectedFarmPlot = null;
            }

            EndConfiguration();
        }

        private void DrawHarvesterPanel()
        {
            FarmPlot plot = selectedHarvester.ConnectedFarmPlot;
            float height = 150f + (plot?.AvailableCrops.Count ?? 0) * 28f;
            BeginConfiguration(height);
            GUILayout.Label("Harvester");
            GUILayout.Label(plot != null
                ? $"Farm Plot crop: {plot.SelectedCrop?.Id ?? "None"}"
                : "No Farm Plot under Harvester");
            if (plot != null)
            {
                GUILayout.Label($"Mature crops: {plot.MatureCount} / {plot.MatureCapacity}");
                DrawCropSelection(plot);
            }
            if (demoController?.IsDevInspectorOpen == true || demoController?.IsFoodDemo != true)
                GUILayout.Label($"Buffered crops: {selectedHarvester.OutputCount} / " +
                    selectedHarvester.OutputCapacity);
            if (GUILayout.Button("Close"))
            {
                selectedHarvester = null;
            }

            EndConfiguration();
        }

        private static void DrawCropSelection(FarmPlot plot)
        {
            foreach (CropDefinition crop in plot.AvailableCrops)
            {
                if (crop == null)
                {
                    continue;
                }

                if (!plot.IsCropUnlocked(crop))
                {
                    GUILayout.Label($"{crop.Id} (locked: " +
                        (crop.Id == "Potato" ? "restore East Field" :
                         crop.Id == "Basil" ? "buy Basil Seeds" :
                         "complete the Market order") + ")");
                }
                else if (GUILayout.Button($"Grow {crop.Id}"))
                {
                    plot.SelectCrop(crop);
                }
            }

            if (GUILayout.Button("Clear Crop"))
            {
                plot.SelectCrop(null);
            }
        }

        private void DrawEngraverPanel()
        {
            BeginConfiguration(214f);
            GUILayout.Label("Engraver Configuration");
            GUILayout.Label("Sigil (next rune)");
            DrawSigilButtons();
            GUILayout.Space(6f);
            GUILayout.Label(selectedEngraver.IsAccelerationSocketOccupied
                ? "Socket: Acceleration Rune installed"
                : "Socket: Empty");
            GUILayout.Label($"Hub Acceleration Runes: {hub.AccelerationRuneCount}");
            float durationPercent = 100f / selectedEngraver.UpgradedSpeedMultiplier;
            GUILayout.Label($"Installed effect: {durationPercent:0}% processing duration");

            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && !selectedEngraver.IsAccelerationSocketOccupied &&
                hub.AccelerationRuneCount >= 1;
            if (GUILayout.Button("Install Acceleration Rune"))
            {
                selectedEngraver.TryInstallAccelerationRune(hub.TryConsumeAccelerationRune);
            }

            GUI.enabled = previousEnabled;
            if (GUILayout.Button("Close"))
            {
                selectedEngraver = null;
            }

            EndConfiguration();
        }

        private void DrawSigilButtons()
        {
            GUILayout.BeginHorizontal();
            foreach (RuneSigil sigil in EngraverSigilOptions)
            {
                bool previousEnabled = GUI.enabled;
                GUI.enabled = previousEnabled && selectedEngraver.SelectedSigil != sigil;
                if (GUILayout.Button(sigil.ToString()))
                {
                    selectedEngraver.SetSelectedSigil(sigil);
                }

                GUI.enabled = previousEnabled;
            }

            GUILayout.EndHorizontal();
        }

        private void DrawInfuserPanel()
        {
            if (selectedInfuser == null)
            {
                return;
            }

            BeginConfiguration(128f);
            GUILayout.Label("Element Infuser Configuration");
            GUILayout.Label("Primary element (next rune)");
            GUILayout.BeginHorizontal();
            foreach (RuneElement element in InfuserElementOptions)
            {
                bool previousEnabled = GUI.enabled;
                GUI.enabled = previousEnabled && selectedInfuser.PrimaryElement != element;
                if (GUILayout.Button(element.ToString()))
                {
                    selectedInfuser.SetPrimaryElement(element);
                }

                GUI.enabled = previousEnabled;
            }

            GUILayout.EndHorizontal();
            if (GUILayout.Button("Close"))
            {
                selectedInfuser = null;
            }

            EndConfiguration();
        }
    }
}
