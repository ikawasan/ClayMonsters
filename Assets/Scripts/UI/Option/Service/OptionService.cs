using SaveData;
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
        private readonly SaveData.SaveData currentSaveData;

        [Inject]
        public OptionService(VolumeProfile profile)
        {
            globalVolumeProfile = profile;

            if (globalVolumeProfile != null)
            {
                globalVolumeProfile.TryGet(out colorAdjustments);
            }

            currentSaveData = SaveDataManager.Load();

            // ‹N“®Žž‚Éƒ[ƒh‚µ‚½Ý’è‚ðƒQ[ƒ€“à‚É”½‰f‚³‚¹‚é
            ApplySettings();
        }

        private void SaveOptions()
        {
            SaveDataManager.Save(currentSaveData);
        }

        private void ApplySettings()
        {
            ApplyBrightness(currentSaveData.VideoOptionData.Brightness);
            ApplyFullScreen(currentSaveData.VideoOptionData.IsFullScreen);
            ApplyVSync(currentSaveData.VideoOptionData.VSync);
            ApplyMusicVolume(currentSaveData.SoundOptionData.MusicVolume);
            ApplySoundEffectVolume(currentSaveData.SoundOptionData.SoundEffectVolume);
        }

        private void ApplyBrightness(float brightness)
        {
            if (colorAdjustments != null)
            {
                float mappedExposure = Mathf.Lerp(-2f, 2f, brightness);
                colorAdjustments.postExposure.value = mappedExposure;
            }
        }

        private static void ApplyFullScreen(bool isFullScreen)
        {
            Screen.fullScreen = isFullScreen;
        }

        private static void ApplyVSync(bool isVSync)
        {
            QualitySettings.vSyncCount = isVSync ? 1 : 0;
        }

        private static void ApplyMusicVolume(float volume)
        {

        }

        private static void ApplySoundEffectVolume(float volume)
        {

        }

        public bool GetFullScreen => currentSaveData.VideoOptionData.IsFullScreen;

        public bool GetVSync => currentSaveData.VideoOptionData.VSync;

        public float GetBrightness => currentSaveData.VideoOptionData.Brightness;

        public float GetMusicVolume => currentSaveData.SoundOptionData.MusicVolume;

        public float GetSoundEffectVolume => currentSaveData.SoundOptionData.SoundEffectVolume;

        public void SetFullScreen(bool isFullScreen)
        {
            currentSaveData.VideoOptionData.IsFullScreen = isFullScreen;
            ApplyFullScreen(isFullScreen);
            SaveOptions();
        }

        public void SetBrightness(float brightness)
        {
            currentSaveData.VideoOptionData.Brightness = brightness;
            ApplyBrightness(brightness);
            SaveOptions();
        }

        public void SetVSync(bool isVSync)
        {
            currentSaveData.VideoOptionData.VSync = isVSync;
            ApplyVSync(isVSync);
            SaveOptions();
        }

        public void SetMusicVolume(float volume)
        {
            currentSaveData.SoundOptionData.MusicVolume = volume;
            ApplyMusicVolume(volume);
            SaveOptions();
        }

        public void SetSoundEffectVolume(float volume)
        {
            currentSaveData.SoundOptionData.SoundEffectVolume = volume;
            ApplySoundEffectVolume(volume);
            SaveOptions();
        }
    }
}