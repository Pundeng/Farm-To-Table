using System;
using System.Collections.Generic;
using UnityEngine;

namespace FantasyShapez.Buildings
{
    [Serializable]
    public sealed class BuildingDefinition
    {
        [SerializeField] private string id = "PrototypeMachine";
        [SerializeField] private Vector2Int footprint = new(2, 1);
        [SerializeField] private Vector2Int[] occupiedCells = Array.Empty<Vector2Int>();
        [SerializeField] private GameObject instancePrefab = null;
        [SerializeField] private GameObject visualPrefab = null;
        [SerializeField] private Sprite visualSprite = null;
        [SerializeField] private Vector2 visualScale = Vector2.one;
        [SerializeField] private Vector2 visualOffset = Vector2.zero;
        [SerializeField] private int visualSortingOffset;
        [SerializeField] private bool keepVisualUpright;
        [SerializeField] private Color placedColor = new(0.3f, 0.65f, 0.9f, 1f);

        public string Id => id;

        public Vector2Int Footprint => footprint;

        // Empty means the whole rectangle, preserving existing scene definitions.
        public IReadOnlyList<Vector2Int> OccupiedCells => occupiedCells;

        public bool HasExplicitFootprint => occupiedCells != null && occupiedCells.Length > 0;

        public GameObject InstancePrefab => instancePrefab;

        public GameObject VisualPrefab => visualPrefab;

        public Sprite VisualSprite => visualSprite;
        public Vector2 VisualScale => new(
            visualScale.x > 0f ? visualScale.x : 1f,
            visualScale.y > 0f ? visualScale.y : 1f);
        public Vector2 VisualOffset => visualOffset;
        public int VisualSortingOffset => Mathf.Clamp(visualSortingOffset, 0, 2);
        public bool RotateVisualWithBuilding => !keepVisualUpright;
        public bool UsesPlaceholderVisual => visualPrefab == null && visualSprite == null;

        public Color PlacedColor => placedColor;

        public Vector2Int GetRotatedFootprint(BuildingRotation rotation)
        {
            return rotation.GetRotatedFootprint(footprint);
        }

        public void Validate()
        {
            footprint.x = Mathf.Max(1, footprint.x);
            footprint.y = Mathf.Max(1, footprint.y);
            visualScale = VisualScale;
            visualSortingOffset = Mathf.Clamp(visualSortingOffset, 0, 2);
            if (!HasExplicitFootprint)
            {
                return;
            }

            var unique = new HashSet<Vector2Int>();
            foreach (Vector2Int cell in occupiedCells)
            {
                if (cell.x < 0 || cell.y < 0 || cell.x >= footprint.x ||
                    cell.y >= footprint.y || !unique.Add(cell))
                {
                    throw new ArgumentException($"Invalid occupied cell {cell} in {id}.");
                }
            }
        }
    }
}
