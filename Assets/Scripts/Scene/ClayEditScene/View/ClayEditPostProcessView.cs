using System.Collections.Generic;
using Scene.ClayEditScene.Interface;
using Scene.Rendering;
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
        private const int DefaultRendererIndex = -1;

        [SerializeField] private Color backgroundColor = Color.black;

        private UniversalAdditionalCameraData cameraData;
        private CameraClearFlags previousClearFlags;
        private Color previousBackgroundColor;
        private bool previousPostProcessingEnabled;
        private int previousRendererIndex = DefaultRendererIndex;
        private CameraOverrideOption previousDepthOption = CameraOverrideOption.UsePipelineSettings;
        private bool hasStoredCameraState;
        private bool isEnabled;
        private int remainingUiExclusionFrames;
        private readonly List<Volume> disabledVolumes = new();
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
            if (isEnabled)
            {
                return;
            }

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            isEnabled = true;
            remainingUiExclusionFrames = UiExclusionFrameCount;
            ApplyUiExclusion();
            DisableSceneVolumes();
            BackgroundOutlineActivation.SuppressForClayEdit();
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
            BackgroundOutlineActivation.ClearClayEditSuppression();
            RestoreSceneVolumes();
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
            previousDepthOption = cameraData.requiresDepthOption;
            // 前シーンのRenderer(SSAO等)が残ると色味が変わるためデフォルトへ戻す
            previousRendererIndex = DefaultRendererIndex;
            hasStoredCameraState = true;

            cameraData.renderPostProcessing = false;
            cameraData.requiresDepthOption = CameraOverrideOption.UsePipelineSettings;
            cameraData.SetRenderer(DefaultRendererIndex);
        }

        private void DisableSceneVolumes()
        {
            RestoreSceneVolumes();

            Volume[] volumes = Object.FindObjectsByType<Volume>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < volumes.Length; i++)
            {
                Volume volume = volumes[i];
                if (volume == null || !volume.enabled)
                {
                    continue;
                }

                disabledVolumes.Add(volume);
                volume.enabled = false;
            }
        }

        private void RestoreSceneVolumes()
        {
            for (int i = 0; i < disabledVolumes.Count; i++)
            {
                Volume volume = disabledVolumes[i];
                if (volume != null)
                {
                    volume.enabled = true;
                }
            }

            disabledVolumes.Clear();
        }

        private void RestoreCameraSettings()
        {
            UnityEngine.Camera camera = UnityEngine.Camera.main;
            if (camera != null)
            {
                camera.clearFlags = previousClearFlags;
                camera.backgroundColor = previousBackgroundColor;
            }

            if (hasStoredCameraState && cameraData != null)
            {
                cameraData.renderPostProcessing = previousPostProcessingEnabled;
                cameraData.requiresDepthOption = previousDepthOption;
                cameraData.SetRenderer(previousRendererIndex);
                cameraData = null;
                hasStoredCameraState = false;
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
