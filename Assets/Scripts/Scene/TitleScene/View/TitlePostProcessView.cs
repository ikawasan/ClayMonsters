using Scene.BattleNpcScene;
using Scene.Rendering;
using Scene.TitleScene.Interface;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Scene.TitleScene.View
{
    /// <summary>
    /// タイトルシーン相当のBloom・ビネット・SSAOを適用する
    /// UIはシーン上でScreenSpaceOverlayとしポストプロセス対象外にする
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TitlePostProcessView : MonoBehaviour, ITitlePostProcess
    {
        private const int DefaultRendererIndex = -1;

        [SerializeField] private VolumeProfile titleVolumeProfile;
        [SerializeField] private float volumePriority = 10f;
        [SerializeField] private int titleRendererIndex;
        [SerializeField] private GameObject fieldRoot;

        private Volume volume;
        private UniversalAdditionalCameraData cameraData;
        private bool previousPostProcessingEnabled;
        private int previousRendererIndex = DefaultRendererIndex;
        private CameraOverrideOption previousDepthOption = CameraOverrideOption.UsePipelineSettings;

        /// <inheritdoc/>
        public void Enable()
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            EnsureVolume();
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
                    "[TitlePostProcessView] Volumeが未配線ですHierarchyで追加してください",
                    this);
                return;
            }

            volume.isGlobal = true;
            volume.priority = volumePriority;
            volume.profile = titleVolumeProfile;
            volume.enabled = false;
        }
    }
}
