using Camera.Interface;
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

        [Header("Orbit Settings")]
        [SerializeField] private float stepAngle = 15f;
        [SerializeField] private Vector2 distanceRange = new Vector2(2f, 20f);
        [SerializeField] private Vector2 verticalRange = new Vector2(-80f, 80f);

        [Header("Initial View (シーン入場時の初期構図)")]
        [SerializeField] private float initialHorizontalAngle = 0f;
        [SerializeField] private float initialVerticalAngle = 20f;
        [SerializeField] private float initialDistance = 15f;

        private CinemachineOrbitalFollow orbitalFollow;

        private void Awake()
        {
            if (cinemachineCamera != null)
            {
                cinemachineCamera.TryGetComponent(out orbitalFollow);
            }

            cameraModel.Configure(stepAngle, distanceRange, verticalRange);

            // 現在のCinemachineの状態をModelに同期しておく
            if (orbitalFollow != null)
            {
                cameraModel.Synchronize(orbitalFollow.HorizontalAxis.Value, orbitalFollow.VerticalAxis.Value, orbitalFollow.Radius);
            }
        }

        private void LateUpdate()
        {
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

            ApplyToCinemachine();
        }

        private void ReadOrbitInput(Mouse mouse, Keyboard keyboard)
        {
            if (!keyboard.altKey.isPressed)
            {
                return;
            }

            // 右ドラッグで回転
            if (mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                cameraModel.Rotate(delta * rotateSpeed);
            }

            // ホイールでズーム
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                cameraModel.Zoom(scroll * 0.01f * zoomSpeed);
            }
        }

        private void ReadPresetInput(Keyboard keyboard)
        {
            if (keyboard.numpad1Key.wasPressedThisFrame || keyboard.digit1Key.wasPressedThisFrame || keyboard.zKey.wasPressedThisFrame)
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
            cameraModel.SetFocus(centerPosition);
        }

        /// <inheritdoc />
        public void SetInitializeView()
        {
            cameraModel.Synchronize(initialHorizontalAngle, initialVerticalAngle, initialDistance);
            ApplyToCinemachine();
        }

        /// <inheritdoc />
        public void SetCameraEnable(bool isEnable)
        {
            cameraModel.SetEnabled(isEnable);

            if (cinemachineCamera != null)
            {
                cinemachineCamera.Priority = isEnable ? 20 : 10;
            }

            // 有効化時は現在のCinemachine状態をModelに同期
            if (isEnable && orbitalFollow != null)
            {
                cameraModel.Synchronize(orbitalFollow.HorizontalAxis.Value, orbitalFollow.VerticalAxis.Value, orbitalFollow.Radius);
            }
        }
    }
}