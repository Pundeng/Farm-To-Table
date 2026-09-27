using System;
using CozyFoodFactory.CameraControl;
using CozyFoodFactory.Food;
using CozyFoodFactory.Logistics;
using UnityEngine;

namespace CozyFoodFactory.Buildings
{
    public sealed class PropertyActivityVisual : MonoBehaviour
    {
        private Transform part;
        private SpriteRenderer renderer;
        private bool supplying;
        private bool forceDetail;
        private WorldInformationLevel level;

        public void Initialize(float cellSize)
        {
            var marker = new GameObject("Animated Parts");
            marker.transform.SetParent(transform, false);
            marker.transform.localPosition = new Vector3(0f, 0f, -0.03f);
            Vector3 parentScale = transform.localScale;
            marker.transform.localScale = new Vector3(
                cellSize * 0.16f / parentScale.x,
                cellSize * 0.16f / parentScale.y, 1f);
            part = marker.transform;
            renderer = marker.AddComponent<SpriteRenderer>();
            renderer.sprite = BuildingVisualFactory.PlaceholderSprite;
            renderer.color = new Color(1f, 1f, 0.78f, 0.82f);
            renderer.sortingOrder = 13;
        }

        public void SetState(bool isSupplying, WorldInformationLevel informationLevel,
            bool detail)
        {
            supplying = isSupplying;
            level = informationLevel;
            forceDetail = detail;
            renderer.enabled = supplying &&
                MachineAnimationDecisions.ShowMotion(level, forceDetail);
        }

        private void LateUpdate()
        {
            if (!supplying || !MachineAnimationDecisions.ShowMotion(
                    level, forceDetail)) return;
            part.localRotation = Quaternion.Euler(0f, 0f, Time.time * 35f);
        }
    }

}
