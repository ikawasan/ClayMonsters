using System.Collections.Generic;
using Scene.BattleNpcScene.Interface;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Scene.BattleNpcScene.View
{
    /// <summary>
    /// BattleNpc教室シーン向けの暖色ポストプロセスとSSAOを適用する
    /// Bloomはintensity0で上書きしグローバルVolume分も含めて完全に切る
    /// 3Dフィールドのみに効かせUIはOverlay表示でポストプロセス対象外にする
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleNpcPostProcessView : MonoBehaviour, IBattleNpcPostProcess
    {
        private const int UiExclusionFrameCount = 3;

        private const int DefaultRendererIndex = -1;

        [SerializeField] private VolumeProfile classroomVolumeProfile;
        [SerializeField] private float volumePriority = 10f;
        [SerializeField] private int battleNpcRendererIndex = 1;

        [Header("Background")]
        [SerializeField] private bool useSolidBackground;
        [SerializeField] private Color backgroundColor = Color.black;

        private Volume volume;
        private UniversalAdditionalCameraData cameraData;
        private bool previousPostProcessingEnabled;
        private int previousRendererIndex = DefaultRendererIndex;
        private CameraOverrideOption previousDepthOption = CameraOverrideOption.UsePipelineSettings;
        private CameraClearFlags previousClearFlags;
        private Color previousBackgroundColor;
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
            DisableBloomCompletely();

            isPostProcessEnabled = true;
            remainingUiExclusionFrames = UiExclusionFrameCount;
            ApplyUiExclusion();

            if (volume != null)
            {
                volume.enabled = true;
            }

            ApplySolidBackground();

            var camera = UnityEngine.Camera.main;
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
            cameraData.SetRenderer(battleNpcRendererIndex);
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

            RestoreSolidBackground();
            RestoreUiCanvases();
        }

        private void LateUpdate()
        {
            if (!isPostProcessEnabled || remainingUiExclusionFrames <= 0)
            {
                return;
            }

            ApplyUiExclusion();
            remainingUiExclusionFrames--;
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
            }

            volume.isGlobal = true;
            volume.priority = volumePriority;
            if (classroomVolumeProfile != null)
            {
                volume.sharedProfile = classroomVolumeProfile;
            }

            volume.enabled = false;
        }

        /// <summary>
        /// グローバルVolumeのBloomを優先度付きでintensity0上書きする
        /// activeを落とすだけでは他VolumeのBloomが残る
        /// </summary>
        private void DisableBloomCompletely()
        {
            VolumeProfile profile = ResolveRuntimeProfile();
            if (profile == null || !profile.TryGet(out Bloom bloom))
            {
                return;
            }

            bloom.active = true;
            bloom.intensity.Override(0f);
            bloom.threshold.Override(1f);
        }

        private VolumeProfile ResolveRuntimeProfile()
        {
            if (volume != null)
            {
                // profile取得で実行時インスタンスを使い共有アセットを汚さない
                return volume.profile;
            }

            return classroomVolumeProfile;
        }

        private void ApplyUiExclusion()
        {
            foreach (var canvas in transform.root.GetComponentsInChildren<Canvas>(true))
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
            foreach (var state in savedCanvasStates)
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
            foreach (var state in savedCanvasStates)
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

        private void ApplySolidBackground()
        {
            if (!useSolidBackground)
            {
                return;
            }

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

        private void RestoreSolidBackground()
        {
            if (!useSolidBackground)
            {
                return;
            }

            UnityEngine.Camera camera = UnityEngine.Camera.main;
            if (camera == null)
            {
                return;
            }

            camera.clearFlags = previousClearFlags;
            camera.backgroundColor = previousBackgroundColor;
        }
    }
}
