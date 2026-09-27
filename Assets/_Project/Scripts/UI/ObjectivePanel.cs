using CozyFoodFactory.Buildings;
using CozyFoodFactory.Food;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CozyFoodFactory.UI
{
    public sealed class ObjectivePanel : MonoBehaviour
    {
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
                return configurationHeight > 0f &&
                      new Rect(ConfigurationX, ConfigurationY, 300f,
                          VisibleConfigurationHeight(configurationHeight))
                          .Contains(pointer);
            }
        }

        public void ShowFarmPlot(FarmPlot farmPlot)
        {
            selectedCutter = null;
            selectedFarmPlot = farmPlot;
            selectedHarvester = null;
            selectedProcessor = null;
            selectedMixer = null;
        }

        public void ShowHarvester(Harvester harvester)
        {
            selectedCutter = null;
            selectedFarmPlot = null;
            selectedHarvester = harvester;
            selectedProcessor = null;
            selectedMixer = null;
        }

        public void ShowProcessor(Processor processor)
        {
            selectedCutter = null;
            selectedFarmPlot = null;
            selectedHarvester = null;
            selectedProcessor = processor;
            selectedMixer = null;
        }

        public void ShowMixer(BasicMixer mixer)
        {
            selectedCutter = null;
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

        private float GetConfigurationPanelHeight()
        {
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
            if (selectedFarmPlot != null)
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

    }
}
