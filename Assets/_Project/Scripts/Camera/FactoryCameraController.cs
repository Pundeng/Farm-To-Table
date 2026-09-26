using FantasyShapez.Buildings;
using FantasyShapez.Food;
using FantasyShapez.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FantasyShapez.CameraControl
{
    [RequireComponent(typeof(Camera))]
    public sealed class FactoryCameraController : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float movementSpeed = 10f;
        [SerializeField, Min(0.01f)] private float zoomSpeed = 1.5f;
        [SerializeField, Min(0.01f)] private float minimumZoom = 3f;
        [SerializeField, Min(0.01f)] private float maximumZoom = 20f;
        [SerializeField] private bool zoomTowardCursor = true;
        [SerializeField] private MarketPanel marketPanel = null;
        [SerializeField] private RecipeDiscoveryPanel recipeDiscoveryPanel = null;
        [SerializeField] private BuildingPlacementController buildings = null;

        private Camera controlledCamera;

        private void Awake()
        {
            controlledCamera = GetComponent<Camera>();
            controlledCamera.orthographic = true;
            ClampZoomSettings();
            controlledCamera.orthographicSize = Mathf.Clamp(
                controlledCamera.orthographicSize,
                minimumZoom,
                maximumZoom);
        }

        private void Update()
        {
            if (buildings != null && buildings.BlocksAllWorldInput) return;
            HandleKeyboardMovement();
            HandleMiddleMousePan();
            HandleZoom();
        }

        private void HandleKeyboardMovement()
        {
            if (Keyboard.current == null)
            {
                return;
            }

            Vector2 direction = Vector2.zero;
            direction.x = (Keyboard.current.dKey.isPressed ? 1f : 0f) -
                          (Keyboard.current.aKey.isPressed ? 1f : 0f);
            direction.y = (Keyboard.current.wKey.isPressed ? 1f : 0f) -
                          (Keyboard.current.sKey.isPressed ? 1f : 0f);

            if (direction.sqrMagnitude > 1f)
            {
                direction.Normalize();
            }

            transform.position += (Vector3)(direction * movementSpeed * Time.unscaledDeltaTime);
        }

        private void HandleMiddleMousePan()
        {
            if (Mouse.current == null || !Mouse.current.middleButton.isPressed)
            {
                return;
            }

            Vector2 currentPosition = Mouse.current.position.ReadValue();
            Vector2 previousPosition = currentPosition - Mouse.current.delta.ReadValue();
            Vector3 previousWorldPosition = ScreenToGridPlane(previousPosition);
            Vector3 currentWorldPosition = ScreenToGridPlane(currentPosition);
            transform.position += previousWorldPosition - currentWorldPosition;
        }

        private void HandleZoom()
        {
            if (Mouse.current == null ||
                (marketPanel != null && marketPanel.IsPointerOverPanel) ||
                (recipeDiscoveryPanel != null && recipeDiscoveryPanel.BlocksWorldInput) ||
                (buildings != null && buildings.IsPointerOverInterface))
            {
                return;
            }

            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Approximately(scroll, 0f))
            {
                return;
            }

            Vector2 cursorPosition = Mouse.current.position.ReadValue();
            Vector3 cursorWorldBeforeZoom = ScreenToGridPlane(cursorPosition);
            float scrollStep = Mathf.Sign(scroll);
            controlledCamera.orthographicSize = Mathf.Clamp(
                controlledCamera.orthographicSize - scrollStep * zoomSpeed,
                minimumZoom,
                maximumZoom);

            if (zoomTowardCursor)
            {
                Vector3 cursorWorldAfterZoom = ScreenToGridPlane(cursorPosition);
                transform.position += cursorWorldBeforeZoom - cursorWorldAfterZoom;
            }
        }

        private Vector3 ScreenToGridPlane(Vector2 screenPosition)
        {
            Ray ray = controlledCamera.ScreenPointToRay(screenPosition);
            if (Mathf.Approximately(ray.direction.z, 0f))
            {
                return transform.position;
            }

            float distance = -ray.origin.z / ray.direction.z;
            return ray.GetPoint(distance);
        }

        private void OnValidate()
        {
            ClampZoomSettings();

            Camera cameraComponent = GetComponent<Camera>();
            cameraComponent.orthographic = true;
            cameraComponent.orthographicSize = Mathf.Clamp(
                cameraComponent.orthographicSize,
                minimumZoom,
                maximumZoom);
        }

        private void ClampZoomSettings()
        {
            movementSpeed = Mathf.Max(0f, movementSpeed);
            zoomSpeed = Mathf.Max(0.01f, zoomSpeed);
            minimumZoom = Mathf.Max(0.01f, minimumZoom);
            maximumZoom = Mathf.Max(minimumZoom, maximumZoom);
        }
    }
}
