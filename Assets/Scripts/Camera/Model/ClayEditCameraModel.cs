using Camera.Interface;
using UnityEngine;

namespace Camera.Model
{
    /// <summary>
    /// îSìyï“èWÉJÉÅÉâÇÃModel
    /// </summary>
    public sealed class ClayEditCameraModel : IClayEditCameraModel
    {
        private float horizontalAngle;
        private float verticalAngle;
        private float distance = 15f;
        private Vector3 focusPoint = Vector3.zero;
        private bool isOperatable;

        private float stepAngle = 15f;
        private float minDistance = 2f;
        private float maxDistance = 100f;
        private float minVertical = -80f;
        private float maxVertical = 80f;

        /// <inheritdoc />
        public bool IsOperatable => isOperatable;

        /// <inheritdoc />
        public float HorizontalAngle => horizontalAngle;

        /// <inheritdoc />
        public float VerticalAngle => verticalAngle;

        /// <inheritdoc />
        public float Distance => distance;

        /// <inheritdoc />
        public Vector3 FocusPoint => focusPoint;

        /// <inheritdoc />
        public void Configure(float stepAngle, Vector2 distanceRange, Vector2 verticalRange)
        {
            this.stepAngle = stepAngle;
            minDistance = distanceRange.x;
            maxDistance = distanceRange.y;
            minVertical = verticalRange.x;
            maxVertical = verticalRange.y;
            distance = Mathf.Clamp(distance, minDistance, maxDistance);
            verticalAngle = Mathf.Clamp(verticalAngle, minVertical, maxVertical);
        }

        /// <inheritdoc />
        public void SetEnabled(bool isEnabled)
        {
            isOperatable = isEnabled;
        }

        /// <inheritdoc />
        public void SetFocus(Vector3 focusPoint)
        {
            this.focusPoint = focusPoint;
        }

        /// <inheritdoc />
        public void Synchronize(float horizontalAngle, float verticalAngle, float distance)
        {
            this.horizontalAngle = horizontalAngle;
            this.verticalAngle = Mathf.Clamp(verticalAngle, minVertical, maxVertical);
            this.distance = Mathf.Clamp(distance, minDistance, maxDistance);
        }

        /// <inheritdoc />
        public void Rotate(Vector2 scaledDelta)
        {
            horizontalAngle += scaledDelta.x;
            verticalAngle = Mathf.Clamp(verticalAngle - scaledDelta.y, minVertical, maxVertical);
        }

        /// <inheritdoc />
        public void Zoom(float zoomAmount)
        {
            distance = Mathf.Clamp(distance - zoomAmount, minDistance, maxDistance);
        }

        /// <inheritdoc />
        public void SetFrontView()
        {
            horizontalAngle = 180f;
            verticalAngle = 0f;
        }

        /// <inheritdoc />
        public void SetRightView()
        {
            horizontalAngle = 90f;
            verticalAngle = 0f;
        }

        /// <inheritdoc />
        public void SetTopView()
        {
            horizontalAngle = 0f;
            verticalAngle = Mathf.Clamp(90f, minVertical, maxVertical);
        }

        /// <inheritdoc />
        public void FlipView()
        {
            horizontalAngle += 180f;
        }

        /// <inheritdoc />
        public void StepYaw(int direction)
        {
            horizontalAngle += direction * stepAngle;
        }

        /// <inheritdoc />
        public void StepPitch(int direction)
        {
            verticalAngle = Mathf.Clamp(verticalAngle + direction * stepAngle, minVertical, maxVertical);
        }
    }
}