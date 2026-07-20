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
        private readonly List<Volume> disabledVolumes = new();

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
            BackgroundOutlineActivation.ClearClayEditSuppression();
            RestoreSceneVolumes();
            RestoreCameraSettings();
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
    }
}
