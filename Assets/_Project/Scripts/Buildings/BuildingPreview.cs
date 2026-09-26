using System;
using System.Collections.Generic;
using FantasyShapez.Grid;
using UnityEngine;

namespace FantasyShapez.Buildings
{
    public sealed class BuildingPreview : MonoBehaviour
    {
        [SerializeField] private Color validColor = new(0.25f, 0.9f, 0.45f, 0.55f);
        [SerializeField] private Color invalidColor = new(0.95f, 0.25f, 0.25f, 0.55f);

        private BuildingDefinition currentDefinition;
        private IReadOnlyList<BuildingPortPreview> currentPortPreviews;
        private GameObject directionIndicator;
        private GameObject portIndicatorsRoot;
        private GameObject visualRoot;
        private string reason;

        public void SetReason(string message) => reason = message;

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(reason) || !gameObject.activeInHierarchy)
                return;
            Vector2 pointer = UnityEngine.InputSystem.Mouse.current?.position.ReadValue() ??
                Vector2.zero;
            const float width = 260f;
            GUIStyle style = new(GUI.skin.box) { wordWrap = true };
            float height = Mathf.Max(30f, style.CalcHeight(new GUIContent(reason), width));
            float x = Mathf.Clamp(pointer.x + 16f, 8f,
                Mathf.Max(8f, Screen.width - width - 8f));
            float y = Mathf.Clamp(Screen.height - pointer.y + 16f, 8f,
                Mathf.Max(8f, Screen.height - height - 8f));
            GUI.Box(new Rect(x, y, width, height), reason, style);
        }

        public void Show(
            BuildingPlacementOption option,
            GridSystem gridSystem,
            Vector2Int anchorCell,
            BuildingRotation rotation,
            bool isValid)
        {
            BuildingDefinition definition = option.Definition;
            EnsureVisual(definition, gridSystem.CellSize);
            EnsureDirectionIndicator(gridSystem.CellSize);
            EnsurePortIndicators(option.PortPreviews, gridSystem.CellSize);

            bool hasPortPreviews = option.PortPreviews.Count > 0;
            directionIndicator.SetActive(!hasPortPreviews);
            portIndicatorsRoot.SetActive(hasPortPreviews);

            Vector2Int rotatedFootprint = definition.GetRotatedFootprint(rotation);
            Vector3 firstCellCenter = gridSystem.GridToWorld(anchorCell);
            transform.position = firstCellCenter + new Vector3(
                (rotatedFootprint.x - 1) * gridSystem.CellSize * 0.5f,
                (rotatedFootprint.y - 1) * gridSystem.CellSize * 0.5f,
                -0.02f);
            transform.rotation = Quaternion.Euler(0f, 0f, -(int)rotation);
            BuildingVisualFactory.Tint(visualRoot, isValid ? validColor : invalidColor);
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            reason = null;
            gameObject.SetActive(false);
        }

        public void SetPortConnectionFeedback(IReadOnlyList<bool?> connections)
        {
            if (portIndicatorsRoot == null) return;
            for (int index = 0; index < portIndicatorsRoot.transform.childCount; index++)
            {
                bool? connected = index < connections.Count ? connections[index] : null;
                Color color = connected == true
                    ? new Color(0.25f, 1f, 0.4f)
                    : new Color(1f, 0.25f, 0.2f);
                Transform indicator = portIndicatorsRoot.transform.GetChild(index);
                foreach (SpriteRenderer renderer in indicator.GetComponentsInChildren<SpriteRenderer>())
                    renderer.color = connected.HasValue ? color :
                        currentPortPreviews[index].Kind switch
                        {
                            BuildingPortKind.Input => BuildingPortPreviewLayouts.InputColor,
                            BuildingPortKind.PropertyInput => BuildingPortPreviewLayouts.PropertyInputColor,
                            _ => BuildingPortPreviewLayouts.OutputColor
                        };
            }
        }

        public void SetDirectionConnectionFeedback(bool? connected)
        {
            if (directionIndicator == null) return;
            Color color = connected == true ? new Color(0.25f, 1f, 0.4f) :
                connected == false ? new Color(1f, 0.25f, 0.2f) : Color.white;
            foreach (SpriteRenderer renderer in
                     directionIndicator.GetComponentsInChildren<SpriteRenderer>())
                renderer.color = color;
        }

        private void EnsureVisual(BuildingDefinition definition, float cellSize)
        {
            if (visualRoot != null && ReferenceEquals(currentDefinition, definition))
            {
                return;
            }

            if (visualRoot != null)
            {
                Destroy(visualRoot);
            }

            currentDefinition = definition;
            visualRoot = BuildingVisualFactory.Create(definition, transform, cellSize, 75);
        }

        private void EnsureDirectionIndicator(float cellSize)
        {
            if (directionIndicator != null)
            {
                return;
            }

            directionIndicator = new GameObject("Direction Indicator");
            directionIndicator.transform.SetParent(transform, false);
            CreateIndicatorPart(
                directionIndicator.transform,
                "Shaft",
                new Vector2(0f, 0.12f) * cellSize,
                new Vector2(0.09f, 0.42f) * cellSize,
                0f,
                Color.white);
            CreateIndicatorPart(
                directionIndicator.transform,
                "Left",
                new Vector2(-0.1f, 0.29f) * cellSize,
                new Vector2(0.09f, 0.26f) * cellSize,
                -45f,
                Color.white);
            CreateIndicatorPart(
                directionIndicator.transform,
                "Right",
                new Vector2(0.1f, 0.29f) * cellSize,
                new Vector2(0.09f, 0.26f) * cellSize,
                45f,
                Color.white);
        }

        private void EnsurePortIndicators(
            IReadOnlyList<BuildingPortPreview> portPreviews,
            float cellSize)
        {
            if (portIndicatorsRoot != null &&
                ReferenceEquals(currentPortPreviews, portPreviews))
            {
                return;
            }

            if (portIndicatorsRoot != null)
            {
                Destroy(portIndicatorsRoot);
            }

            currentPortPreviews = portPreviews;
            portIndicatorsRoot = new GameObject("Port Indicators");
            portIndicatorsRoot.transform.SetParent(transform, false);
            for (int index = 0; index < portPreviews.Count; index++)
            {
                CreatePortIndicator(portPreviews[index], index, cellSize);
            }
        }

        private void CreatePortIndicator(
            BuildingPortPreview port,
            int index,
            float cellSize)
        {
            var indicator = new GameObject($"{port.Kind} Port {index + 1}");
            indicator.transform.SetParent(portIndicatorsRoot.transform, false);
            indicator.transform.localPosition =
                new Vector3(port.LocalPosition.x, port.LocalPosition.y, -0.04f) * cellSize;
            indicator.transform.localRotation =
                Quaternion.Euler(0f, 0f, -(int)port.LocalDirection);
            Color color = port.Kind switch
            {
                BuildingPortKind.Input => BuildingPortPreviewLayouts.InputColor,
                BuildingPortKind.PropertyInput => BuildingPortPreviewLayouts.PropertyInputColor,
                _ => BuildingPortPreviewLayouts.OutputColor
            };
            CreateIndicatorPart(
                indicator.transform,
                "Shaft",
                new Vector2(0f, 0.04f) * cellSize,
                new Vector2(0.07f, 0.24f) * cellSize,
                0f,
                color);
            CreateIndicatorPart(
                indicator.transform,
                "Left",
                new Vector2(-0.06f, 0.12f) * cellSize,
                new Vector2(0.07f, 0.16f) * cellSize,
                -45f,
                color);
            CreateIndicatorPart(
                indicator.transform,
                "Right",
                new Vector2(0.06f, 0.12f) * cellSize,
                new Vector2(0.07f, 0.16f) * cellSize,
                45f,
                color);
            int sameKind = 0;
            int ordinal = 0;
            for (int portIndex = 0; portIndex < currentPortPreviews.Count; portIndex++)
            {
                if (currentPortPreviews[portIndex].Kind != port.Kind) continue;
                sameKind++;
                if (portIndex <= index) ordinal++;
            }
            if (sameKind > 1)
            {
                var label = new GameObject($"Port {(ordinal == 1 ? "A" : "B")}");
                label.transform.SetParent(indicator.transform, false);
                label.transform.localPosition = new Vector3(0f, 0.23f * cellSize,
                    -0.05f);
                TextMesh text = label.AddComponent<TextMesh>();
                text.text = ordinal == 1 ? "A" : "B";
                text.fontSize = 32;
                text.characterSize = 0.15f * cellSize;
                text.anchor = TextAnchor.MiddleCenter;
                text.color = Color.white;
                text.GetComponent<MeshRenderer>().sortingOrder = 81;
            }
        }

        private void CreateIndicatorPart(
            Transform parent,
            string partName,
            Vector2 localPosition,
            Vector2 localScale,
            float angle,
            Color color)
        {
            var part = new GameObject(partName);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = new Vector3(localPosition.x, localPosition.y, -0.03f);
            part.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            part.transform.localScale = new Vector3(localScale.x, localScale.y, 1f);
            SpriteRenderer renderer = part.AddComponent<SpriteRenderer>();
            renderer.sprite = BuildingVisualFactory.PlaceholderSprite;
            renderer.color = color;
            renderer.sortingOrder = 80;
        }
    }

    public enum BuildingPortKind
    {
        Input,
        Output,
        PropertyInput
    }

    public readonly struct BuildingPortPreview
    {
        public BuildingPortPreview(
            BuildingPortKind kind,
            Vector2 localPosition,
            BuildingRotation localDirection)
        {
            Kind = kind;
            LocalPosition = localPosition;
            LocalDirection = localDirection;
        }

        public BuildingPortKind Kind { get; }

        public Vector2 LocalPosition { get; }

        public BuildingRotation LocalDirection { get; }

        public BuildingRotation ResolveDirection(BuildingRotation buildingRotation)
        {
            int combinedRotation = ((int)LocalDirection + (int)buildingRotation) % 360;
            return (BuildingRotation)combinedRotation;
        }
    }

    public static class BuildingPortPreviewLayouts
    {
        public static readonly Color InputColor = new(0.2f, 0.75f, 1f, 1f);
        public static readonly Color OutputColor = new(1f, 0.55f, 0.15f, 1f);
        public static readonly Color PropertyInputColor = new(0.65f, 0.9f, 0.5f, 1f);

        public static readonly IReadOnlyList<BuildingPortPreview> DirectionalProcessor =
            new[]
            {
                new BuildingPortPreview(
                    BuildingPortKind.Input,
                    new Vector2(0f, -0.36f),
                    BuildingRotation.Degrees0),
                new BuildingPortPreview(
                    BuildingPortKind.Output,
                    new Vector2(0f, 0.36f),
                    BuildingRotation.Degrees0)
            };

        public static readonly IReadOnlyList<BuildingPortPreview> OutputOnly =
            new[]
            {
                new BuildingPortPreview(
                    BuildingPortKind.Output,
                    new Vector2(0f, 0.36f),
                    BuildingRotation.Degrees0)
            };
    }
}
