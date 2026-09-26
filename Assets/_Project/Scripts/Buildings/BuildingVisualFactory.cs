using UnityEngine;
using System.Collections.Generic;
using System;

namespace FantasyShapez.Buildings
{
    public enum MachineFeedbackState
    {
        Working, Idle, NeedsInput, NeedsProperty, InvalidRecipe, OutputBlocked
    }

    [Flags]
    public enum MachineFeedbackPort
    {
        None = 0, InputA = 1, InputB = 2, Property = 4,
        OutputA = 8, OutputB = 16, Combination = 32, Crop = 64
    }

    public readonly struct MachineFeedback
    {
        public MachineFeedback(MachineFeedbackState state, MachineFeedbackPort ports,
            string problem = null, string action = null)
        {
            State = state;
            Ports = ports;
            Problem = problem;
            Action = action;
        }

        public MachineFeedbackState State { get; }
        public MachineFeedbackPort Ports { get; }
        public string Problem { get; }
        public string Action { get; }
        public bool HasProblem => Ports != MachineFeedbackPort.None;
    }

    public static class MachineFeedbackResolver
    {
        public static MachineFeedback Processor(bool blocked, bool invalid,
            bool needsProperty, bool needsInput)
        {
            if (blocked) return new(MachineFeedbackState.OutputBlocked,
                MachineFeedbackPort.OutputA, "Output blocked", "Clear the connected belt.");
            if (invalid) return new(MachineFeedbackState.InvalidRecipe,
                MachineFeedbackPort.InputA, "No valid recipe",
                "Change the ingredient or property.");
            if (needsProperty) return new(MachineFeedbackState.NeedsProperty,
                MachineFeedbackPort.Property, "No property supply", "Connect a property pipe.");
            if (needsInput) return new(MachineFeedbackState.NeedsInput,
                MachineFeedbackPort.InputA, "No ingredient", "Connect an ingredient belt.");
            return new(MachineFeedbackState.Working, MachineFeedbackPort.None);
        }

        public static MachineFeedback Mixer(bool blocked, bool invalid,
            bool missingA, bool missingB)
        {
            if (blocked) return new(MachineFeedbackState.OutputBlocked,
                MachineFeedbackPort.OutputA, "Output blocked", "Clear the connected belt.");
            if (invalid) return Invalid();
            if (missingA || missingB)
            {
                MachineFeedbackPort ports =
                    (missingA ? MachineFeedbackPort.InputA : 0) |
                    (missingB ? MachineFeedbackPort.InputB : 0);
                string name = missingA && missingB ? "ingredients A and B" :
                    missingA ? "ingredient A" : "ingredient B";
                return new(MachineFeedbackState.NeedsInput, ports,
                    $"Missing {name}", "Connect the ingredient belt.");
            }
            return new(MachineFeedbackState.Working, MachineFeedbackPort.None);
        }

        public static MachineFeedback Cutter(bool blockedA, bool blockedB,
            bool invalid, bool needsInput)
        {
            if (blockedA || blockedB)
            {
                MachineFeedbackPort ports =
                    (blockedA ? MachineFeedbackPort.OutputA : 0) |
                    (blockedB ? MachineFeedbackPort.OutputB : 0);
                string name = blockedA && blockedB ? "Outputs A and B" :
                    blockedA ? "Output A" : "Output B";
                return new(MachineFeedbackState.OutputBlocked, ports,
                    $"{name} blocked", "Both outputs must be available.");
            }
            if (invalid) return new(MachineFeedbackState.InvalidRecipe,
                MachineFeedbackPort.InputA, "No cutting recipe",
                "Change the ingredient.");
            if (needsInput) return new(MachineFeedbackState.NeedsInput,
                MachineFeedbackPort.InputA, "No ingredient", "Connect an ingredient belt.");
            return new(MachineFeedbackState.Working, MachineFeedbackPort.None);
        }

        public static MachineFeedback Harvester(bool blocked, bool cropUnset)
        {
            if (blocked) return new(MachineFeedbackState.OutputBlocked,
                MachineFeedbackPort.OutputA, "Output blocked", "Clear the connected belt.");
            if (cropUnset) return new(MachineFeedbackState.NeedsInput,
                MachineFeedbackPort.Crop, "No crop selected", "Choose a crop on the Farm Plot.");
            return new(MachineFeedbackState.Idle, MachineFeedbackPort.None);
        }

        private static MachineFeedback Invalid() => new(
            MachineFeedbackState.InvalidRecipe, MachineFeedbackPort.Combination,
            "No valid recipe", "Change the ingredients.");
    }

    public sealed class EventToastQueue
    {
        public sealed class Entry
        {
            public string Text { get; internal set; }
            public float ExpiresAt { get; internal set; }
        }

        private readonly List<Entry> active = new();
        private readonly Queue<Entry> pending = new();
        private long currencyAmount;
        private float lastCurrencyAt = float.NegativeInfinity;
        public IReadOnlyList<Entry> Active => active;

        public bool Enqueue(string message, float now, float duration = 3f)
        {
            Prune(now);
            if (string.IsNullOrWhiteSpace(message) ||
                active.Exists(entry => entry.Text == message) ||
                System.Linq.Enumerable.Any(pending, entry => entry.Text == message))
                return false;
            if (active.Count == 3)
                pending.Enqueue(new Entry { Text = message, ExpiresAt = duration });
            else active.Add(new Entry { Text = message, ExpiresAt = now + duration });
            return true;
        }

        public void AddCurrency(long amount, float now)
        {
            if (amount <= 0) return;
            Prune(now);
            if (now - lastCurrencyAt > 1f) currencyAmount = 0;
            currencyAmount += amount;
            lastCurrencyAt = now;
            Entry existing = active.Find(entry => entry.Text.StartsWith("+", StringComparison.Ordinal) &&
                entry.Text.EndsWith(" coins", StringComparison.Ordinal));
            if (existing == null)
                existing = System.Linq.Enumerable.FirstOrDefault(pending,
                    entry => entry.Text.StartsWith("+", StringComparison.Ordinal) &&
                        entry.Text.EndsWith(" coins", StringComparison.Ordinal));
            if (existing != null)
            {
                existing.Text = $"+{currencyAmount} coins";
                if (active.Contains(existing)) existing.ExpiresAt = now + 3f;
            }
            else Enqueue($"+{currencyAmount} coins", now);
        }

        public void Prune(float now)
        {
            active.RemoveAll(entry => entry.ExpiresAt <= now);
            while (active.Count < 3 && pending.Count > 0)
            {
                Entry next = pending.Dequeue();
                next.ExpiresAt += now;
                active.Add(next);
            }
        }
    }

    internal static class BuildingVisualFactory
    {
        private static Sprite placeholderSprite;

        public static Sprite PlaceholderSprite => GetPlaceholderSprite();

        public static GameObject Create(
            BuildingDefinition definition,
            Transform parent,
            float cellSize,
            int sortingOrder)
        {
            if (definition.VisualPrefab != null)
            {
                GameObject prefabVisual = UnityEngine.Object.Instantiate(definition.VisualPrefab, parent);
                prefabVisual.name = definition.VisualPrefab.name;
                prefabVisual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                return prefabVisual;
            }

            var placeholder = new GameObject("Placeholder Visual");
            placeholder.transform.SetParent(parent, false);
            if (definition.HasExplicitFootprint)
            {
                foreach (Vector2Int cell in definition.OccupiedCells)
                {
                    var tile = new GameObject($"Cell {cell.x},{cell.y}");
                    tile.transform.SetParent(placeholder.transform, false);
                    tile.transform.localPosition = new Vector3(
                        (cell.x - (definition.Footprint.x - 1) * 0.5f) * cellSize,
                        (cell.y - (definition.Footprint.y - 1) * 0.5f) * cellSize,
                        0f);
                    tile.transform.localScale = new Vector3(cellSize, cellSize, 1f);
                    SpriteRenderer tileRenderer = tile.AddComponent<SpriteRenderer>();
                    tileRenderer.sprite = GetPlaceholderSprite();
                    tileRenderer.sortingOrder = sortingOrder;
                }

                return placeholder;
            }

            placeholder.transform.localScale = new Vector3(
                definition.Footprint.x * cellSize,
                definition.Footprint.y * cellSize,
                1f);

            SpriteRenderer renderer = placeholder.AddComponent<SpriteRenderer>();
            renderer.sprite = GetPlaceholderSprite();
            renderer.sortingOrder = sortingOrder;
            return placeholder;
        }

        public static void Tint(GameObject visualRoot, Color color)
        {
            foreach (SpriteRenderer renderer in visualRoot.GetComponentsInChildren<SpriteRenderer>())
            {
                renderer.color = color;
            }
        }

        public static void CreatePortMarkers(Transform parent,
            IReadOnlyList<BuildingPortPreview> ports, float cellSize,
            BuildingRotation rotation)
        {
            int inputs = 0;
            int outputs = 0;
            int inputCount = 0;
            int outputCount = 0;
            foreach (BuildingPortPreview port in ports)
            {
                if (port.Kind == BuildingPortKind.Input) inputCount++;
                if (port.Kind == BuildingPortKind.Output) outputCount++;
            }
            foreach (BuildingPortPreview port in ports)
            {
                int ordinal = port.Kind switch
                {
                    BuildingPortKind.Input => ++inputs,
                    BuildingPortKind.Output => ++outputs,
                    _ => 0
                };
                string label = port.Kind switch
                {
                    BuildingPortKind.Input => "IN",
                    BuildingPortKind.Output => "OUT",
                    _ => "PROP"
                };
                if (port.Kind == BuildingPortKind.Input && inputCount > 1 ||
                    port.Kind == BuildingPortKind.Output && outputCount > 1)
                    label += ordinal == 1 ? " A" : " B";
                var marker = new GameObject($"{label} port");
                marker.transform.SetParent(parent, false);
                marker.transform.localPosition = new Vector3(
                    port.LocalPosition.x * cellSize,
                    port.LocalPosition.y * cellSize, -0.04f);
                marker.transform.localScale = Vector3.one * 0.24f * cellSize;
                if (port.Kind == BuildingPortKind.PropertyInput)
                    marker.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                SpriteRenderer renderer = marker.AddComponent<SpriteRenderer>();
                renderer.sprite = PlaceholderSprite;
                renderer.color = port.Kind switch
                {
                    BuildingPortKind.Input => BuildingPortPreviewLayouts.InputColor,
                    BuildingPortKind.Output => BuildingPortPreviewLayouts.OutputColor,
                    _ => BuildingPortPreviewLayouts.PropertyInputColor
                };
                renderer.sortingOrder = 22;
                var labelObject = new GameObject($"{label} label");
                labelObject.transform.SetParent(parent, false);
                labelObject.transform.localPosition = marker.transform.localPosition +
                    new Vector3(0f, -0.22f * cellSize, -0.05f);
                labelObject.transform.localRotation = Quaternion.Euler(0f, 0f,
                    (int)rotation);
                TextMesh text = labelObject.AddComponent<TextMesh>();
                text.text = label;
                text.fontSize = 32;
                text.characterSize = 0.1f * cellSize;
                text.anchor = TextAnchor.MiddleCenter;
                text.color = Color.white;
                text.GetComponent<MeshRenderer>().sortingOrder = 23;
            }
        }

        public static MachineFeedbackView CreateFeedbackView(Transform parent,
            float cellSize)
        {
            MachineFeedbackView view = parent.gameObject.AddComponent<MachineFeedbackView>();
            view.Initialize(cellSize);
            return view;
        }

        private static Sprite GetPlaceholderSprite()
        {
            if (placeholderSprite == null)
            {
                placeholderSprite = Sprite.Create(
                    Texture2D.whiteTexture,
                    new Rect(0f, 0f, 1f, 1f),
                    new Vector2(0.5f, 0.5f),
                    1f);
                placeholderSprite.name = "Runtime Building Placeholder";
            }

            return placeholderSprite;
        }
    }

    public sealed class MachineFeedbackView : MonoBehaviour
    {
        private readonly Dictionary<MachineFeedbackPort, Transform> targets = new();
        private readonly Dictionary<MachineFeedbackPort, SpriteRenderer> highlights = new();
        private readonly Dictionary<MachineFeedbackPort, Vector3> highlightScales = new();
        private TextMesh indicator;
        private LineRenderer trace;
        private MachineFeedback feedback;
        private float emphasisUntil;
        private bool hovered;
        private float cellSize;

        public MachineFeedback Feedback => feedback;

        public void Initialize(float size)
        {
            cellSize = size;
            foreach (Transform child in GetComponentsInChildren<Transform>())
            {
                MachineFeedbackPort port = child.name switch
                {
                    "IN port" or "IN A port" => MachineFeedbackPort.InputA,
                    "IN B port" => MachineFeedbackPort.InputB,
                    "OUT port" or "OUT A port" => MachineFeedbackPort.OutputA,
                    "OUT B port" => MachineFeedbackPort.OutputB,
                    "PROP port" => MachineFeedbackPort.Property,
                    _ => MachineFeedbackPort.None
                };
                if (port != MachineFeedbackPort.None && !targets.ContainsKey(port))
                    targets.Add(port, child);
            }
            targets[MachineFeedbackPort.Combination] = transform;
            targets[MachineFeedbackPort.Crop] = targets.TryGetValue(
                MachineFeedbackPort.InputA, out Transform crop) ? crop : transform;
            foreach (KeyValuePair<MachineFeedbackPort, Transform> entry in targets)
            {
                var marker = new GameObject($"Feedback {entry.Key}");
                marker.transform.SetParent(entry.Value, false);
                marker.transform.localPosition = new Vector3(0f, 0f, -0.1f);
                marker.transform.localScale = entry.Value == transform
                    ? Vector3.one * 0.42f * size : Vector3.one * 1.9f;
                SpriteRenderer renderer = marker.AddComponent<SpriteRenderer>();
                renderer.sprite = BuildingVisualFactory.PlaceholderSprite;
                renderer.sortingOrder = 24;
                renderer.enabled = false;
                highlights.Add(entry.Key, renderer);
                highlightScales.Add(entry.Key, marker.transform.localScale);
            }
            var badge = new GameObject("Problem indicator");
            badge.transform.SetParent(transform, false);
            badge.transform.localPosition = new Vector3(0f, size * 1.2f, -0.1f);
            indicator = badge.AddComponent<TextMesh>();
            indicator.text = "!";
            indicator.anchor = TextAnchor.MiddleCenter;
            indicator.fontSize = 64;
            indicator.characterSize = 0.16f * size;
            indicator.GetComponent<MeshRenderer>().sortingOrder = 32;
            badge.SetActive(false);
        }

        public void SetFeedback(MachineFeedback next, bool isHovered, bool suppress)
        {
            feedback = next;
            hovered = isHovered && !suppress;
            indicator.gameObject.SetActive(next.HasProblem);
            Color color = next.State is MachineFeedbackState.OutputBlocked or
                MachineFeedbackState.InvalidRecipe
                ? new Color(1f, 0.25f, 0.2f, 0.85f)
                : new Color(1f, 0.62f, 0.18f, 0.85f);
            indicator.color = color;
            foreach (KeyValuePair<MachineFeedbackPort, SpriteRenderer> entry in highlights)
            {
                entry.Value.enabled = next.HasProblem && (next.Ports & entry.Key) != 0;
                entry.Value.color = color;
            }
            if (suppress) emphasisUntil = 0f;
        }

        public void Emphasize(Vector3? nearbyConnection)
        {
            if (!feedback.HasProblem) return;
            emphasisUntil = Time.time + 3f;
            if (!nearbyConnection.HasValue) return;
            trace ??= gameObject.AddComponent<LineRenderer>();
            trace.sharedMaterial ??= new Material(Shader.Find("Sprites/Default"));
            trace.useWorldSpace = true;
            trace.positionCount = 2;
            trace.startWidth = trace.endWidth = 0.07f * cellSize;
            trace.startColor = trace.endColor = Color.yellow;
            trace.sortingOrder = 31;
            trace.SetPosition(0, GetProblemPosition());
            trace.SetPosition(1, nearbyConnection.Value);
            trace.enabled = true;
        }

        private void Update()
        {
            bool emphasized = Time.time < emphasisUntil;
            if (trace != null) trace.enabled = emphasized;
            foreach (KeyValuePair<MachineFeedbackPort, SpriteRenderer> entry in highlights)
                if (entry.Value.enabled)
                    entry.Value.transform.localScale = highlightScales[entry.Key] *
                        (emphasized ? 1f + 0.13f * Mathf.Sin(Time.time * 12f) : 1f);
        }

        public Vector3 GetProblemPosition()
        {
            foreach (KeyValuePair<MachineFeedbackPort, Transform> entry in targets)
                if ((feedback.Ports & entry.Key) != 0) return entry.Value.position;
            return transform.position;
        }

        private void OnGUI()
        {
            if (!feedback.HasProblem || !hovered && Time.time >= emphasisUntil ||
                Camera.main == null) return;
            Vector3 screen = Camera.main.WorldToScreenPoint(GetProblemPosition());
            if (screen.z <= 0f) return;
            float x = Mathf.Clamp(screen.x + 12f, 8f, Mathf.Max(8f, Screen.width - 225f));
            float y = Mathf.Clamp(Screen.height - screen.y - 65f, 8f,
                Mathf.Max(8f, Screen.height - 64f));
            GUI.Box(new Rect(x, y, 218f, 56f),
                $"{feedback.Problem}\n{feedback.Action}");
        }

        private void OnDestroy()
        {
            if (trace != null && trace.sharedMaterial != null)
                Destroy(trace.sharedMaterial);
        }
    }
}
