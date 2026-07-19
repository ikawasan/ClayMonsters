using System.Collections.Generic;
using Scene.Rendering;
using Scene.BattleNpcScene;
using Scene.TitleScene.Interface;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Scene.TitleScene.View
{
    /// <summary>
    /// タイトルシーン相当のBloom・ビネット・SSAOを適用する
    /// 3Dフィールドのみに効かせUIはOverlay表示でポストプロセス対象外にする
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TitlePostProcessView : MonoBehaviour, ITitlePostProcess
    {
        private const int DefaultRendererIndex = -1;
        private const int UiExclusionFrameCount = 3;

        [SerializeField] private VolumeProfile titleVolumeProfile;
        [SerializeField] private float volumePriority = 10f;
        [SerializeField] private int titleRendererIndex;
        [SerializeField] private GameObject fieldRoot;

        private Volume volume;
        private UniversalAdditionalCameraData cameraData;
        private bool previousPostProcessingEnabled;
        private int previousRendererIndex = DefaultRendererIndex;
        private CameraOverrideOption previousDepthOption = CameraOverrideOption.UsePipelineSettings;
        private bool isPostProcessEnabled;
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

            EnsureVolume();
            isPostProcessEnabled = true;
            remainingUiExclusionFrames = UiExclusionFrameCount;
            ApplyUiExclusion();
            BackgroundOutlineActivation.ClearClayEditSuppression();
            FieldBackgroundLayerUtility.Apply(fieldRoot);

            if (volume != null)
            {
                volume.enabled = true;
            }

            UnityEngine.Camera camera = UnityEngine.Camera.main;
            if (camera == null || !camera.TryGetComponent(out UniversalAdditionalCameraData additionalCameraData))
            {
                return;
            }

            cameraData = additionalCameraData;
            previousPostProcessingEnabled = cameraData.renderPostProcessing;
            previousRendererIndex = DefaultRendererIndex;
            previousDepthOption = cameraData.requiresDepthOption;

            cameraData.renderPostProcessing = true;
            cameraData.requiresDepthOption = CameraOverrideOption.On;
            cameraData.SetRenderer(titleRendererIndex);
        }

        /// <inheritdoc/>
        public void Disable()
        {
            isPostProcessEnabled = false;
            remainingUiExclusionFrames = 0;

            if (volume != null)
            {
                volume.enabled = false;
            }

            if (cameraData != null)
            {
                cameraData.renderPostProcessing = previousPostProcessingEnabled;
                cameraData.requiresDepthOption = previousDepthOption;
                cameraData.SetRenderer(previousRendererIndex);
                cameraData = null;
            }

            RestoreUiCanvases();
        }

        private void LateUpdate()
        {
            if (!isPostProcessEnabled)
            {
                return;
            }

            ApplyUiExclusion();
            if (remainingUiExclusionFrames > 0)
            {
                remainingUiExclusionFrames--;
            }
        }

        private void EnsureVolume()
        {
            if (volume == null)
            {
                volume = GetComponent<Volume>();
                if (volume == null)
                {
                    volume = gameObject.AddComponent<Volume>();
                }

                volume.isGlobal = true;
                volume.priority = volumePriority;
                volume.profile = titleVolumeProfile;
            }

            volume.enabled = false;
        }

        private void ApplyUiExclusion()
        {
            foreach (Canvas canvas in transform.root.GetComponentsInChildren<Canvas>(true))
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
