using Scene.BattleNpcScene;
using Scene.BattleNpcScene.Interface;
using Scene.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Scene.BattleNpcScene.View
{
    /// <summary>
    /// BattleNpc教室シーン向けの暖色ポストプロセスとSSAOを適用する
    /// Bloomはintensity0で上書きしグローバルVolume側も含めて完全に切る
    /// UIはシーン上でScreenSpaceOverlayとしポストプロセス対象外にする
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleNpcPostProcessView : MonoBehaviour, IBattleNpcPostProcess
    {
        private const int DefaultRendererIndex = -1;

        [SerializeField] private VolumeProfile classroomVolumeProfile;
        [SerializeField] private float volumePriority = 10f;
        [SerializeField] private int battleNpcRendererIndex = 1;

        [Header("Background")]
        [SerializeField] private bool useSolidBackground;
        [SerializeField] private Color backgroundColor = Color.black;
        [SerializeField] private GameObject fieldRoot;

        private Volume volume;
        private UniversalAdditionalCameraData cameraData;
        private bool previousPostProcessingEnabled;
        private int previousRendererIndex = DefaultRendererIndex;
        private CameraOverrideOption previousDepthOption = CameraOverrideOption.UsePipelineSettings;
        private CameraClearFlags previousClearFlags;
        private Color previousBackgroundColor;

        /// <inheritdoc/>
        public void Enable()
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            EnsureVolume();
            DisableBloomCompletely();
            BackgroundOutlineActivation.ClearClayEditSuppression();
            FieldBackgroundLayerUtility.Apply(fieldRoot);

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
                Debug.LogError(
                    "[BattleNpcPostProcessView] Volumeが未配線ですHierarchyで追加してください",
                    this);
                return;
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