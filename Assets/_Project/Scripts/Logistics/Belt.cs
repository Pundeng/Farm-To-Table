using FantasyShapez.Buildings;
using FantasyShapez.Grid;
using FantasyShapez.Food;
using UnityEngine;

namespace FantasyShapez.Logistics
{
    public sealed class Belt : MonoBehaviour, IBuildingRemovalRule, IBuildingMoveState
    {
        [SerializeField] private GridDirection direction;
        [SerializeField] private bool hasItem;
        [SerializeField, Range(0f, 1f)] private float itemProgress;
        [SerializeField] private string runeDebug;

        private static Sprite placeholderSprite;
        private BeltTransportCoordinator coordinator;
        private BeltCell cell;
        private GameObject itemVisual;
        private TextMesh itemLabel;

        // Rebuilding intentionally discards any item currently carried by this Belt.
        public bool CanRemove => true;

        public bool CanMove => cell != null && !cell.HasItem;

        public SavedBelt CaptureWorldState()
        {
            if (cell == null)
            {
                throw new System.InvalidOperationException("Belt is not initialized.");
            }

            TransportedRune carried = cell.Item;
            return new SavedBelt
            {
                item = SavedFood.FromTransport(carried?.Item),
                entryDirection = carried?.EntryDirection ?? default,
                progress = carried?.Progress ?? 0f
            };
        }

        public void RestoreWorldState(SavedBelt saved)
        {
            if (saved.item != null)
            {
                cell.RestoreItem(saved.item.ToFood(), saved.entryDirection,
                    saved.progress);
            }
        }

        public void DetachForMove()
        {
            coordinator?.UnregisterBelt(cell);
        }

        public void ReattachAfterFailedMove()
        {
            cell = coordinator.RegisterBelt(this, cell.Cell, direction);
        }

        public void Initialize(
            Vector2Int gridCell,
            GridDirection beltDirection,
            BeltTransportCoordinator transportCoordinator)
        {
            coordinator = transportCoordinator;
            direction = beltDirection;
            cell = coordinator.RegisterBelt(this, gridCell, beltDirection);
            CreateDirectionArrow();
            CreateItemVisual();
        }

        public void RefreshItemVisual(GridSystem gridSystem, BeltCell beltCell)
        {
            hasItem = beltCell.HasItem;
            itemProgress = beltCell.Item?.Progress ?? 0f;
            runeDebug = beltCell.Item?.Item?.ToString() ?? string.Empty;

            itemVisual.SetActive(hasItem);
            itemLabel.gameObject.SetActive(hasItem);

            if (!hasItem)
            {
                return;
            }

            if (beltCell.Item.Item is FoodItemData food)
            {
                itemVisual.GetComponent<SpriteRenderer>().color = FoodColor(food);
                itemVisual.transform.rotation = Quaternion.Euler(0f, 0f,
                    food.Kind == FoodItemKind.ProcessedFood ? 45f : 0f);
                itemLabel.text = FoodLabel(food);
            }
            else
            {
                itemVisual.GetComponent<SpriteRenderer>().color = Color.white;
                itemVisual.transform.rotation = Quaternion.identity;
                itemLabel.text = string.Empty;
            }

            Vector2 startOffset = -(Vector2)beltCell.Item.EntryDirection.ToOffset() * 0.5f;
            Vector2 endOffset = (Vector2)beltCell.Direction.ToOffset() * 0.5f;
            Vector2 localOffset = itemProgress < 0.5f
                ? Vector2.Lerp(startOffset, Vector2.zero, itemProgress * 2f)
                : Vector2.Lerp(Vector2.zero, endOffset, (itemProgress - 0.5f) * 2f);
            itemVisual.transform.position =
                gridSystem.GridToWorld(beltCell.Cell) + (Vector3)(localOffset * gridSystem.CellSize);
            itemLabel.transform.position = itemVisual.transform.position +
                new Vector3(0f, 0f, -0.03f);
            itemLabel.transform.rotation = Quaternion.identity;
        }

        private void CreateDirectionArrow()
        {
            CreateArrowPart("Arrow Shaft", new Vector2(0f, -0.02f), new Vector2(0.1f, 0.5f), 0f);
            CreateArrowPart("Arrow Left", new Vector2(-0.1f, 0.18f), new Vector2(0.1f, 0.3f), -45f);
            CreateArrowPart("Arrow Right", new Vector2(0.1f, 0.18f), new Vector2(0.1f, 0.3f), 45f);
        }

        private void CreateArrowPart(string partName, Vector2 position, Vector2 scale, float angle)
        {
            var part = new GameObject(partName);
            part.transform.SetParent(transform, false);
            part.transform.localPosition = new Vector3(position.x, position.y, -0.02f);
            part.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            part.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            SpriteRenderer renderer = part.AddComponent<SpriteRenderer>();
            renderer.sprite = GetPlaceholderSprite();
            renderer.color = Color.white;
            renderer.sortingOrder = 15;
        }

        private void CreateItemVisual()
        {
            itemVisual = new GameObject("Transported Item");
            itemVisual.transform.SetParent(transform, false);
            itemVisual.transform.localScale = Vector3.one * 0.32f;
            SpriteRenderer renderer = itemVisual.AddComponent<SpriteRenderer>();
            renderer.sprite = GetPlaceholderSprite();
            renderer.color = Color.white;
            renderer.sortingOrder = 25;
            var label = new GameObject("Food identity");
            label.transform.SetParent(transform, false);
            itemLabel = label.AddComponent<TextMesh>();
            itemLabel.fontSize = 32;
            itemLabel.characterSize = 0.12f;
            itemLabel.anchor = TextAnchor.MiddleCenter;
            itemLabel.color = Color.black;
            itemLabel.GetComponent<MeshRenderer>().sortingOrder = 26;
            itemVisual.SetActive(false);
            itemLabel.gameObject.SetActive(false);
        }

        private static string FoodLabel(FoodItemData food) => food.Id.ToLowerInvariant() switch
        {
            "apple" => "AP", "onion" => "ON", "tomato" => "TO",
            "potato" => "PO", "basil" => "BA", "dried apple" => "DA",
            "vegetable base" => "VB", "cut potato" => "CP",
            "french fries" => "FF", _ => food.Id.Length > 1
                ? food.Id.Substring(0, 2).ToUpperInvariant() : food.Id.ToUpperInvariant()
        };

        private static Color FoodColor(FoodItemData food) => food.Id.ToLowerInvariant() switch
        {
            "apple" => new Color(0.91f, 0.23f, 0.18f),
            "onion" => new Color(0.78f, 0.62f, 0.87f),
            "tomato" => new Color(0.96f, 0.39f, 0.17f),
            "potato" => new Color(0.71f, 0.54f, 0.29f),
            "basil" => new Color(0.28f, 0.72f, 0.36f),
            "dried apple" => new Color(0.81f, 0.48f, 0.27f),
            "vegetable base" => new Color(0.45f, 0.78f, 0.37f),
            "cut potato" => new Color(0.93f, 0.8f, 0.48f),
            "french fries" => new Color(0.97f, 0.68f, 0.24f),
            _ => new Color(0.47f, 0.77f, 0.83f)
        };

        private static Sprite GetPlaceholderSprite()
        {
            if (placeholderSprite == null)
            {
                placeholderSprite = Sprite.Create(
                    Texture2D.whiteTexture,
                    new Rect(0f, 0f, 1f, 1f),
                    new Vector2(0.5f, 0.5f),
                    1f);
                placeholderSprite.name = "Runtime Belt Placeholder";
            }

            return placeholderSprite;
        }

        private void OnDestroy()
        {
            coordinator?.UnregisterBelt(cell);
        }
    }
}
