using Scene.ClayEditScene.Interface;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Scene.ClayEditScene.View
{
    /// <summary>
    /// ClayEditシーン向けのBloomポストプロセスと黒背景を適用する
    /// 造形範囲グリッドの発光表現を強調する
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ClayEditPostProcessView : MonoBehaviour, IClayEditPostProcess
    {
        [SerializeField] private VolumeProfile clayEditVolumeProfile;
        [SerializeField] private float volumePriority = 10f;
        [SerializeField] private Color backgroundColor = Color.black;

        private Volume volume;
        private UniversalAdditionalCameraData cameraData;
        private CameraClearFlags previousClearFlags;
        private Color previousBackgroundColor;
        private bool previousPostProcessingEnabled;
        private bool isEnabled;

        /// <inheritdoc/>
        public void Enable()
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            EnsureVolume();

            if (volume != null)
            {
                volume.enabled = true;
            }

            ApplyBlackBackground();
            ApplyPostProcessing();
            isEnabled = true;
        }

        /// <inheritdoc/>
        public void Disable()
        {
            if (!isEnabled)
            {
                return;
            }

            isEnabled = false;

            if (volume != null)
            {
                volume.enabled = false;
            }

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

        private void ApplyPostProcessing()
        {
            UnityEngine.Camera camera = UnityEngine.Camera.main;
            if (camera == null || !camera.TryGetComponent(out UniversalAdditionalCameraData additionalCameraData))
            {
                return;
            }

            cameraData = additionalCameraData;
            previousPostProcessingEnabled = cameraData.renderPostProcessing;
            cameraData.renderPostProcessing = true;
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

        private void EnsureVolume()
        {
            if (volume != null)
            {
                return;
            }

            volume = GetComponent<Volume>();
            if (volume == null)
            {
                volume = gameObject.AddComponent<Volume>();
            }

            volume.isGlobal = true;
            volume.priority = volumePriority;
            volume.profile = clayEditVolumeProfile;
            volume.enabled = false;
        }
    }
}
