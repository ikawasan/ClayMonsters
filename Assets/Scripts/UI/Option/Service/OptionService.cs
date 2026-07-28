using Audio.Interface;
using SaveData;
using UI.Option.Interface;
using UnityEngine;
using VContainer;

namespace UI.Option.Service
{
    /// <summary>
    /// オプション設定の保存と画面・音量への反映を行う
    /// </summary>
    public sealed class OptionService : IOptionService
    {
        private readonly SaveData.SaveData currentSaveData;
        private readonly IBgmService bgmService;
        private readonly IUiSoundService uiSoundService;
        private readonly ISeService seService;

        private const int WindowedWidth = 1600;
        private const int WindowedHeight = 900;

        [Inject]
        public OptionService(
            IBgmService bgmService,
            IUiSoundService uiSoundService,
            ISeService seService)
        {
            this.bgmService = bgmService;
            this.uiSoundService = uiSoundService;
            this.seService = seService;
            currentSaveData = SaveDataManager.Load();
            ApplySettings();
        }

        public bool GetFullScreen => currentSaveData.VideoOptionData.IsFullScreen;

        public bool GetVSync => currentSaveData.VideoOptionData.VSync;

        public float GetMusicVolume => currentSaveData.SoundOptionData.MusicVolume;

        public float GetSoundEffectVolume => currentSaveData.SoundOptionData.SoundEffectVolume;

        public void SetFullScreen(bool isFullScreen)
        {
            currentSaveData.VideoOptionData.IsFullScreen = isFullScreen;
            ApplyFullScreen(isFullScreen);
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

        private void SaveOptions()
        {
            SaveDataManager.Update(data =>
            {
                data.VideoOptionData = currentSaveData.VideoOptionData;
                data.SoundOptionData = currentSaveData.SoundOptionData;
            });
        }

        private void ApplySettings()
        {
            ApplyFullScreen(currentSaveData.VideoOptionData.IsFullScreen);
            ApplyVSync(currentSaveData.VideoOptionData.VSync);
            ApplyMusicVolume(currentSaveData.SoundOptionData.MusicVolume);
            ApplySoundEffectVolume(currentSaveData.SoundOptionData.SoundEffectVolume);
        }

        private static void ApplyFullScreen(bool isFullScreen)
        {
#if UNITY_EDITOR
            // EditorのGameViewではOS解像度切替が起きないため反映しない
            return;
#else
            Resolution desktop = Screen.currentResolution;
            if (isFullScreen)
            {
                // 排他フルスクリーンを避けデスクトップ解像度のボーダレスにする
                Screen.SetResolution(
                    desktop.width,
                    desktop.height,
                    FullScreenMode.FullScreenWindow,
                    desktop.refreshRateRatio);
                return;
            }

            Screen.SetResolution(
                WindowedWidth,
                WindowedHeight,
                FullScreenMode.Windowed);
#endif
        }

        private static void ApplyVSync(bool isVSync)
        {
            QualitySettings.vSyncCount = isVSync ? 1 : 0;
        }

        private void ApplyMusicVolume(float volume)
        {
            bgmService?.SetMusicVolume(volume);
        }

        private void ApplySoundEffectVolume(float volume)
        {
            uiSoundService?.SetSoundEffectVolume(volume);
            seService?.SetSoundEffectVolume(volume);
        }
    }
}
