using Camera.Interface;
using Camera.Utility;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace Camera.View
{
    /// <summary>
    /// 粘土編集カメラのView
    /// </summary>
    public class ClayEditCameraView : MonoBehaviour, IClayEditCameraView
    {
        [Inject] private readonly IClayEditCameraModel cameraModel;

        [Header("Cinemachine Settings")]
        [SerializeField] private CinemachineCamera cinemachineCamera;
        [SerializeField] private Transform focusPoint;

        [Header("Input Sensitivity")]
        [SerializeField] private float rotateSpeed = 0.5f;
        [SerializeField] private float zoomSpeed = 0.5f;
        [SerializeField] private float focusPanSpeed = 0.5f;

        [Header("Orbit Settings")]
        [SerializeField] private float stepAngle = 15f;
        [SerializeField] private Vector2 distanceRange = new Vector2(2f, 20f);
        [SerializeField] private Vector2 verticalRange = new Vector2(-80f, 80f);

        [Header("Initial View (シーン入場時の初期構図)")]
        [SerializeField] private float initialHorizontalAngle = 0f;
        [SerializeField] private float initialVerticalAngle = 20f;
        [SerializeField] private float initialDistance = 15f;

        private CinemachineOrbitalFollow orbitalFollow;
        private Vector3 defaultPositionDamping;
        private Vector3 defaultRotationDamping;
        private bool hasDefaultDamping;
        private bool isOrbitDampingSuppressed;
        private bool orbitInputRequiresAlt = true;

        private void Awake()
        {
            if (cinemachineCamera != null)
            {
                cinemachineCamera.TryGetComponent(out orbitalFollow);
            }
        }

        private void Start()
        {
            if (cameraModel == null)
            {
                return;
            }

            cameraModel.Configure(stepAngle, distanceRange, verticalRange);

            if (orbitalFollow != null)
            {
                defaultPositionDamping = orbitalFollow.TrackerSettings.PositionDamping;
                defaultRotationDamping = orbitalFollow.TrackerSettings.RotationDamping;
                hasDefaultDamping = true;
                cameraModel.Synchronize(
                    orbitalFollow.HorizontalAxis.Value,
                    orbitalFollow.VerticalAxis.Value,
                    orbitalFollow.Radius);
            }
        }

        private void LateUpdate()
        {
            if (cameraModel == null)
            {
                return;
            }

            if (cameraModel.IsOperatable)
            {
                var mouse = Mouse.current;
                var keyboard = Keyboard.current;
                if (mouse != null && keyboard != null)
                {
                    ReadOrbitInput(mouse, keyboard);
                    ReadPresetInput(keyboard);
                }
            }

            EnforceSuppressedDamping();
            ApplyToCinemachine();
        }

        private void ReadOrbitInput(Mouse mouse, Keyboard keyboard)
        {
            Vector2 delta = mouse.delta.ReadValue();
            if (orbitInputRequiresAlt)
            {
                if (!keyboard.altKey.isPressed)
                {
                    return;
                }

                if (mouse.rightButton.isPressed)
                {
                    cameraModel.Rotate(delta * rotateSpeed);
                }
            }
            else
            {
                bool panFocus = mouse.middleButton.isPressed
                    || (mouse.rightButton.isPressed && keyboard.shiftKey.isPressed);
                if (panFocus)
                {
                    cameraModel.PanFocus(delta, focusPanSpeed);
                }
                else if (mouse.rightButton.isPressed)
                {
                    cameraModel.Rotate(delta * rotateSpeed);
                }
            }

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                cameraModel.Zoom(scroll * 0.01f * zoomSpeed);
            }
        }

        private void ReadPresetInput(Keyboard keyboard)
        {
            if (keyboard.numpad1Key.wasPressedThisFrame || keyboard.digit1Key.wasPressedThisFrame || (keyboard.zKey.wasPressedThisFrame && !keyboard.ctrlKey.isPressed))
            {
                cameraModel.SetFrontView();
            }

            if (keyboard.numpad3Key.wasPressedThisFrame || keyboard.digit3Key.wasPressedThisFrame || keyboard.xKey.wasPressedThisFrame)
            {
                cameraModel.SetRightView();
            }

            if (keyboard.numpad7Key.wasPressedThisFrame || keyboard.digit7Key.wasPressedThisFrame || keyboard.qKey.wasPressedThisFrame)
            {
                cameraModel.SetTopView();
            }

            if (keyboard.numpad9Key.wasPressedThisFrame || keyboard.digit9Key.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame)
            {
                cameraModel.FlipView();
            }

            if (keyboard.numpad4Key.wasPressedThisFrame || keyboard.digit4Key.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame)
            {
                cameraModel.StepYaw(-1);
            }

            if (keyboard.numpad6Key.wasPressedThisFrame || keyboard.digit6Key.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame)
            {
                cameraModel.StepYaw(1);
            }

            if (keyboard.numpad8Key.wasPressedThisFrame || keyboard.digit8Key.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame)
            {
                cameraModel.StepPitch(1);
            }

            if (keyboard.numpad2Key.wasPressedThisFrame || keyboard.digit2Key.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame)
            {
                cameraModel.StepPitch(-1);
            }
        }

        private void ApplyToCinemachine()
        {
            if (cameraModel == null)
            {
                return;
            }

            if (focusPoint != null)
            {
                focusPoint.position = cameraModel.FocusPoint;
            }

            if (orbitalFollow != null)
            {
                orbitalFollow.HorizontalAxis.Value = cameraModel.HorizontalAngle;
                orbitalFollow.VerticalAxis.Value = cameraModel.VerticalAngle;
                orbitalFollow.Radius = cameraModel.Distance;
            }
        }

        /// <inheritdoc />
        public void SetFocusPosition(Vector3 centerPosition)
        {
            if (cameraModel == null)
            {
                return;
            }

            cameraModel.SetFocus(centerPosition);
        }

        /// <inheritdoc />
        public void SetInitializeView()
        {
            if (cameraModel == null)
            {
                return;
            }

            cameraModel.Synchronize(initialHorizontalAngle, initialVerticalAngle, initialDistance);
            SnapToCurrentView();
        }

        /// <inheritdoc />
        public void SetOrbitView(float horizontalAngle, float verticalAngle, float distance)
        {
            if (cameraModel == null)
            {
                return;
            }

            cameraModel.Synchronize(horizontalAngle, verticalAngle, distance);
            SnapToCurrentView();
        }

        /// <inheritdoc />
        public bool TryGetOrbitState(
            out float horizontalAngle,
            out float verticalAngle,
            out float distance,
            out Vector3 focusPoint)
        {
            if (cameraModel != null)
            {
                horizontalAngle = cameraModel.HorizontalAngle;
                verticalAngle = cameraModel.VerticalAngle;
                distance = cameraModel.Distance;
                focusPoint = cameraModel.FocusPoint;
                return true;
            }

            if (orbitalFollow != null)
            {
                horizontalAngle = orbitalFollow.HorizontalAxis.Value;
                verticalAngle = orbitalFollow.VerticalAxis.Value;
                distance = orbitalFollow.Radius;
                focusPoint = this.focusPoint != null ? this.focusPoint.position : Vector3.zero;
                return true;
            }

            horizontalAngle = 0f;
            verticalAngle = 0f;
            distance = 0f;
            focusPoint = Vector3.zero;
            return false;
        }

        /// <inheritdoc />
        public void SetCameraEnable(bool isEnable)
        {
            if (cameraModel == null)
            {
                if (!isEnable && cinemachineCamera != null)
                {
                    ApplyCinemachinePriority(false);
                }

                return;
            }

            if (!isEnable)
            {
                cameraModel.SetEnabled(false);
                ApplyCinemachinePriority(false);
                return;
            }

            using (CinemachineSceneBlendScope.EnterCutBlend())
            {
                ApplyCinemachinePriority(true);
                SnapToCurrentView();
            }
        }

        /// <inheritdoc />
        public void PrepareSceneEntry()
        {
            if (cameraModel == null)
            {
                return;
            }

            SnapToCurrentView();
        }

        /// <inheritdoc />
        public void ReleaseSceneEntryDamping()
        {
            if (!isOrbitDampingSuppressed || orbitalFollow == null || !hasDefaultDamping)
            {
                return;
            }

            orbitalFollow.TrackerSettings.PositionDamping = defaultPositionDamping;
            orbitalFollow.TrackerSettings.RotationDamping = defaultRotationDamping;
            isOrbitDampingSuppressed = false;
        }

        /// <inheritdoc />
        public void SetCameraOperatable(bool isOperatable)
        {
            if (cameraModel == null)
            {
                return;
            }

            cameraModel.SetEnabled(isOperatable);
        }

        /// <inheritdoc />
        public void SetOrbitInputRequiresAlt(bool requiresAlt)
        {
            orbitInputRequiresAlt = requiresAlt;
        }

        private void EnforceSuppressedDamping()
        {
            if (!isOrbitDampingSuppressed || orbitalFollow == null || !hasDefaultDamping)
            {
                return;
            }

            orbitalFollow.TrackerSettings.PositionDamping = Vector3.zero;
            orbitalFollow.TrackerSettings.RotationDamping = Vector3.zero;
        }

        private void SnapToCurrentView()
        {
            EnforceSuppressedDamping();
            ApplyToCinemachine();

            if (cinemachineCamera != null)
            {
                cinemachineCamera.InternalUpdateCameraState(Vector3.up, -1f);
                CameraState state = cinemachineCamera.State;
                cinemachineCamera.ForceCameraPosition(
                    state.GetFinalPosition(),
                    state.GetFinalOrientation());
            }

            isOrbitDampingSuppressed = true;
        }

        private void ApplyCinemachinePriority(bool isEnable)
        {
            if (cinemachineCamera == null)
            {
                return;
            }

            PrioritySettings priority = cinemachineCamera.Priority;
            if (isEnable)
            {
                priority.Enabled = true;
                priority.Value = 20;
            }
            else
            {
                priority.Enabled = false;
                priority.Value = 0;
            }

            cinemachineCamera.Priority = priority;
        }
    }
}
