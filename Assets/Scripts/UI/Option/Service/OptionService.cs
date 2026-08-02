using Audio.Interface;
using Cysharp.Threading.Tasks;
using LighthouseExtends.Language;
using Localization;
using SaveData;
using System.Threading;
using UI.Option.Interface;
using UnityEngine;
using VContainer;

namespace UI.Option.Service
{
    /// <summary>
    /// オプション設定の保存と画面・音量・言語への反映を行う
    /// </summary>
    public sealed class OptionService : IOptionService
    {
        private readonly SaveData.SaveData currentSaveData;
        private readonly IBgmService bgmService;
        private readonly IUiSoundService uiSoundService;
        private readonly ISeService seService;
        private readonly ILanguageInitializer languageInitializer;
        private readonly ISupportedLanguageService supportedLanguageService;

        private const int WindowedWidth = 1600;
        private const int WindowedHeight = 900;

        [Inject]
        public OptionService(
            IBgmService bgmService,
            IUiSoundService uiSoundService,
            ISeService seService,
            ILanguageInitializer languageInitializer,
            ISupportedLanguageService supportedLanguageService)
        {
            this.bgmService = bgmService;
            this.uiSoundService = uiSoundService;
            this.seService = seService;
            this.languageInitializer = languageInitializer;
            this.supportedLanguageService = supportedLanguageService;
            currentSaveData = SaveDataManager.Load();
            ApplySettings();
        }

        public bool GetFullScreen => currentSaveData.VideoOptionData.IsFullScreen;

        public bool GetVSync => currentSaveData.VideoOptionData.VSync;

        public float GetMusicVolume => currentSaveData.SoundOptionData.MusicVolume;

        public float GetSoundEffectVolume => currentSaveData.SoundOptionData.SoundEffectVolume;

        public string GetLanguageCode => languageInitializer.CurrentLanguageCode;

        public string GetLanguageDisplayName =>
            LanguageDisplayNames.Get(languageInitializer.CurrentLanguageCode);

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

        /// <inheritdoc/>
        public async UniTask CycleLanguageAsync(int delta, CancellationToken cancellationToken)
        {
            var languages = supportedLanguageService.SupportedLanguages;
            if (languages == null || languages.Count == 0)
            {
                Debug.LogError("[OptionService] 対応言語が空です");
                return;
            }

            string current = languageInitializer.CurrentLanguageCode;
            int index = 0;
            for (int i = 0; i < languages.Count; i++)
            {
                if (string.Equals(languages[i], current, System.StringComparison.Ordinal))
                {
                    index = i;
                    break;
                }
            }

            int next = (index + delta) % languages.Count;
            if (next < 0)
            {
                next += languages.Count;
            }

            await languageInitializer.SetLanguageAsync(languages[next], cancellationToken);
            currentSaveData.LanguageOptionData ??= new LanguageOptionSaveData();
            currentSaveData.LanguageOptionData.LanguageCode = languages[next];
        }

        private void SaveOptions()
        {
            SaveDataManager.Update(data =>
            {
                data.VideoOptionData = currentSaveData.VideoOptionData;
                data.SoundOptionData = currentSaveData.SoundOptionData;
                data.LanguageOptionData = currentSaveData.LanguageOptionData
                    ?? new LanguageOptionSaveData();
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
