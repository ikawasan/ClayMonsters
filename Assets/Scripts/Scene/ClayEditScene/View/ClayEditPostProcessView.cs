using System.Collections.Generic;
using Scene.ClayEditScene.Interface;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Scene.ClayEditScene.View
{
    /// <summary>
    /// ClayEditシーン向けの黒背景を適用する
    /// メッシュ色味を他シーンと揃えるためポストプロセスは掛けない
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ClayEditPostProcessView : MonoBehaviour, IClayEditPostProcess
    {
        private const int UiExclusionFrameCount = 3;

        [SerializeField] private Color backgroundColor = Color.black;

        private UniversalAdditionalCameraData cameraData;
        private CameraClearFlags previousClearFlags;
        private Color previousBackgroundColor;
        private bool previousPostProcessingEnabled;
        private bool isEnabled;
        private int remainingUiExclusionFrames;
        private readonly List<CanvasRenderState> savedCanvasStates = new();

        private readonly struct CanvasRenderState
        {
            public CanvasRenderState(Canvas canvas, RenderMode renderMode, UnityEngine.Camera worldCamera)
            {
                Canvas = canvas;
                RenderMode = renderMode;
                WorldCamera = worldCamera;
            }

            public Canvas Canvas { get; }
            public RenderMode RenderMode { get; }
            public UnityEngine.Camera WorldCamera { get; }
        }

        /// <inheritdoc/>
        public void Enable()
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            isEnabled = true;
            remainingUiExclusionFrames = UiExclusionFrameCount;
            ApplyUiExclusion();
            DisableSceneVolumes();
            ApplyBlackBackground();
            DisableCameraPostProcessing();
        }

        /// <inheritdoc/>
        public void Disable()
        {
            if (!isEnabled)
            {
                return;
            }

            isEnabled = false;
            remainingUiExclusionFrames = 0;
            RestoreCameraSettings();
            RestoreUiCanvases();
        }

        private void LateUpdate()
        {
            if (!isEnabled || remainingUiExclusionFrames <= 0)
            {
                return;
            }

            ApplyUiExclusion();
            remainingUiExclusionFrames--;
        }

        private void ApplyBlackBackground()
        {
            UnityEngine.Camera camera = UnityEngine.Camera.main;
            if (camera == null)
            {
                return;
            }

            previousClearFlags = camera.clearFlags;
            previousBackgroundColor = camera.backgroundColor;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = backgroundColor;
        }

        private void DisableCameraPostProcessing()
        {
            UnityEngine.Camera camera = UnityEngine.Camera.main;
            if (camera == null || !camera.TryGetComponent(out UniversalAdditionalCameraData additionalCameraData))
            {
                return;
            }

            cameraData = additionalCameraData;
            previousPostProcessingEnabled = cameraData.renderPostProcessing;
            cameraData.renderPostProcessing = false;
        }

        private static void DisableSceneVolumes()
        {
            Volume[] volumes = Object.FindObjectsByType<Volume>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < volumes.Length; i++)
            {
                volumes[i].enabled = false;
            }
        }

        private void RestoreCameraSettings()
        {
            UnityEngine.Camera camera = UnityEngine.Camera.main;
            if (camera != null)
            {
                camera.clearFlags = previousClearFlags;
                camera.backgroundColor = previousBackgroundColor;
            }

            if (cameraData != null)
            {
                cameraData.renderPostProcessing = previousPostProcessingEnabled;
                cameraData = null;
            }
        }

        private void ApplyUiExclusion()
        {
            Canvas[] canvases = Object.FindObjectsByType<Canvas>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (Canvas canvas in canvases)
            {
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    continue;
                }

                if (!ContainsCanvas(canvas))
                {
                    savedCanvasStates.Add(new CanvasRenderState(
                        canvas,
                        canvas.renderMode,
                        canvas.worldCamera));
                }

                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.worldCamera = null;
            }
        }

        private bool ContainsCanvas(Canvas canvas)
        {
            foreach (CanvasRenderState state in savedCanvasStates)
            {
                if (state.Canvas == canvas)
                {
                    return true;
                }
            }

            return false;
        }

        private void RestoreUiCanvases()
        {
            foreach (CanvasRenderState state in savedCanvasStates)
            {
                if (state.Canvas == null)
                {
                    continue;
                }

                state.Canvas.renderMode = state.RenderMode;
                state.Canvas.worldCamera = state.WorldCamera;
            }

            savedCanvasStates.Clear();
        }
    }
}
