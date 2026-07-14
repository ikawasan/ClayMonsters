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
        public void PanFocus(Vector2 screenDelta, float panSpeed)
        {
            if (!isOperatable)
            {
                return;
            }

            float scale = panSpeed * 0.01f * Mathf.Max(distance, 1f);
            Vector3 screenRight = ResolveScreenRight(horizontalAngle);
            focusPoint += screenRight * (screenDelta.x * scale) + Vector3.up * (screenDelta.y * scale);
        }

        private static Vector3 ResolveScreenRight(float horizontalAngleDegrees)
        {
            Quaternion orbit = Quaternion.Euler(0f, horizontalAngleDegrees, 0f);
            Vector3 cameraForward = orbit * Vector3.forward;
            cameraForward.y = 0f;

            if (cameraForward.sqrMagnitude < 1e-6f)
            {
                return Vector3.right;
            }

            cameraForward.Normalize();
            return Vector3.Cross(Vector3.up, cameraForward).normalized;
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