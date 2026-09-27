using System;
using FantasyShapez.Buildings;
using FantasyShapez.Food;
using FantasyShapez.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FantasyShapez.CameraControl
{
    public enum WorldInformationLevel { Close, Medium, Far }

    public sealed class WorldInformationLod
    {
        public float MediumThreshold { get; set; } = 7f;
        public float FarThreshold { get; set; } = 13f;
        public float Hysteresis { get; set; } = 0.6f;
        public WorldInformationLevel Level { get; private set; }

        public WorldInformationLevel Update(float zoom)
        {
            float band = Mathf.Max(0f, Hysteresis);
            switch (Level)
            {
                case WorldInformationLevel.Close:
                    if (zoom > FarThreshold + band) Level = WorldInformationLevel.Far;
                    else if (zoom > MediumThreshold + band) Level = WorldInformationLevel.Medium;
                    break;
                case WorldInformationLevel.Medium:
                    if (zoom < MediumThreshold - band) Level = WorldInformationLevel.Close;
                    else if (zoom > FarThreshold + band) Level = WorldInformationLevel.Far;
                    break;
                case WorldInformationLevel.Far:
                    if (zoom < MediumThreshold - band) Level = WorldInformationLevel.Close;
                    else if (zoom < FarThreshold - band) Level = WorldInformationLevel.Medium;
                    break;
            }
            return Level;
        }
    }

    [RequireComponent(typeof(Camera))]
    public sealed class FactoryCameraController : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float movementSpeed = 10f;
        [SerializeField, Min(0.01f)] private float zoomSpeed = 1.5f;
        [SerializeField, Min(0.01f)] private float minimumZoom = 3f;
        [SerializeField, Min(0.01f)] private float maximumZoom = 20f;
        [SerializeField] private bool zoomTowardCursor = true;
        [SerializeField, Min(0.01f)] private float mediumLodZoom = 7f;
        [SerializeField, Min(0.01f)] private float farLodZoom = 13f;
        [SerializeField, Min(0f)] private float lodHysteresis = 0.6f;
        [SerializeField, Min(0.1f)] private float issueJumpSpeed = 18f;
        [SerializeField] private MarketPanel marketPanel = null;
        [SerializeField] private RecipeDiscoveryPanel recipeDiscoveryPanel = null;
        [SerializeField] private BuildingPlacementController buildings = null;

        private Camera controlledCamera;
        private readonly WorldInformationLod lod = new();
        private Vector3? jumpTarget;
        private Action jumpCompleted;
        public WorldInformationLevel InformationLevel => lod.Level;

        public void JumpTo(Vector3 worldPosition, Action completed = null)
        {
            jumpTarget = new Vector3(worldPosition.x, worldPosition.y,
                transform.position.z);
            jumpCompleted = completed;
        }

        private void Awake()
        {
            controlledCamera = GetComponent<Camera>();
            controlledCamera.orthographic = true;
            ClampZoomSettings();
            controlledCamera.orthographicSize = Mathf.Clamp(
                controlledCamera.orthographicSize,
                minimumZoom,
                maximumZoom);
            RefreshLod();
        }

        private void Update()
        {
            if (buildings != null && buildings.BlocksAllWorldInput) return;
            bool keyboardBlocked = buildings != null &&
                buildings.BlocksGameplayKeyboardInput;
            if (!keyboardBlocked && Keyboard.current != null &&
                (Keyboard.current.wKey.isPressed || Keyboard.current.aKey.isPressed ||
                 Keyboard.current.sKey.isPressed || Keyboard.current.dKey.isPressed) ||
                Mouse.current != null &&
                (Mouse.current.middleButton.isPressed ||
                 !Mathf.Approximately(Mouse.current.scroll.ReadValue().y, 0f)))
            {
                jumpTarget = null;
                jumpCompleted = null;
            }
            if (jumpTarget.HasValue)
            {
                transform.position = Vector3.MoveTowards(transform.position,
                    jumpTarget.Value, issueJumpSpeed * Time.unscaledDeltaTime);
                if ((transform.position - jumpTarget.Value).sqrMagnitude < 0.0001f)
                {
                    jumpTarget = null;
                    Action callback = jumpCompleted;
                    jumpCompleted = null;
                    callback?.Invoke();
                }
            }
            if (!keyboardBlocked) HandleKeyboardMovement();
            HandleMiddleMousePan();
            HandleZoom();
            RefreshLod();
        }

        private void RefreshLod()
        {
            lod.MediumThreshold = mediumLodZoom;
            lod.FarThreshold = farLodZoom;
            lod.Hysteresis = lodHysteresis;
            lod.Update(controlledCamera.orthographicSize);
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
            mediumLodZoom = Mathf.Clamp(mediumLodZoom, minimumZoom, maximumZoom);
            farLodZoom = Mathf.Clamp(farLodZoom, mediumLodZoom, maximumZoom);
            lodHysteresis = Mathf.Max(0f, lodHysteresis);
        }
    }
}
