using UI.Option.Interface;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using VContainer;

namespace UI.Option.Service
{
    public class OptionService : IOptionService
    {
        private readonly VolumeProfile globalVolumeProfile;
        private readonly ColorAdjustments colorAdjustments;
        private static readonly string BrightnessKey = "Settings_Brightness";

        [Inject]
        public OptionService(VolumeProfile profile)
        {
            globalVolumeProfile = profile;

            if (globalVolumeProfile != null)
            {
                globalVolumeProfile.TryGet(out colorAdjustments);
            }
        }

        public float GetBrightness()
        {
            return PlayerPrefs.GetFloat(BrightnessKey, 0.5f);
        }

        public void ApplyBrightness(float brightness)
        {
            if (colorAdjustments != null)
            {
                float mappedExposure = Mathf.Lerp(-2f, 2f, brightness);
                colorAdjustments.postExposure.value = mappedExposure;
            }

            PlayerPrefs.SetFloat(BrightnessKey, brightness);
        }

        public Resolution[] GetSupportedResolutions()
        {
            return Screen.resolutions;
        }

        public void ApplyResolution(int index)
        {
            var resolutions = Screen.resolutions;
            if (index >= 0 && index < resolutions.Length)
            {
                var target = resolutions[index];
                Screen.SetResolution(target.width, target.height, Screen.fullScreen);
            }
        }

        public void ApplyFullScreen(bool isFullScreen)
        {
            Screen.fullScreen = isFullScreen;
        }

        public void ApplyVSync(bool isVSync)
        {
            QualitySettings.vSyncCount = isVSync ? 1 : 0;
        }

        public void ApplyMusicVolume(float volume)
        {

        }

        public void ApplySoundEffectVolume(float volume)
        {

        }
    }
}