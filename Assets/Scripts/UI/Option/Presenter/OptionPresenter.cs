using UI.Option.Interface;
using UnityEngine;
using VContainer;

namespace UI.Option
{
    public class OptionPresenter : IOptionPresenter
    {
        readonly IOptionView optionView;
        readonly IOptionService optionService;

        [Inject]
        public OptionPresenter(IOptionView optionView, IOptionService optionService)
        {
            this.optionView = optionView;
            this.optionService = optionService;
            Initialize();
        }

        public void Initialize()
        {
            // UIイベントの購読
            optionView.SubscribeResolutionChanged(OnResolutionChanged);
            optionView.SubscribeFullScreenChanged(OnFullScreenChanged);
            optionView.SubscribeBrightnessChanged(OnBrightnessChanged);
            optionView.SubscribeVSyncChanged(OnVSyncChanged);
            optionView.SubscribeMusicVolumeChanged(OnMusicVolumeChanged);
            optionView.SubscribeSoundEffectVolumeChanged(OnSoundEffectChanged);

            // 初期値の読み込みと適用
            var resolutions = optionService.GetSupportedResolutions();
            int currentResolutionIndex = resolutions.Length > 0 ? resolutions.Length - 1 : 0;
            float savedBrightness = optionService.GetBrightness();

            optionView.InitVideoSettings(
                resolutionMax: resolutions.Length > 0 ? resolutions.Length - 1 : 0,
                resolutionCurrent: currentResolutionIndex,
                isFullScreen: Screen.fullScreen,
                brightness: savedBrightness,
                isVSync: QualitySettings.vSyncCount > 0
            );

            optionView.InitSoundSettings(
                musicVolume: 0.5f,
                soundEffectVolume: 0.5f
            );

            optionService.ApplyBrightness(savedBrightness);
        }

        void OnResolutionChanged(float value) => optionService.ApplyResolution(Mathf.RoundToInt(value));
        void OnFullScreenChanged(bool isOn) => optionService.ApplyFullScreen(isOn);
        void OnVSyncChanged(bool isOn) => optionService.ApplyVSync(isOn);
        void OnBrightnessChanged(float value) => optionService.ApplyBrightness(value);

        void OnMusicVolumeChanged(float value) => optionService.ApplyMusicVolume(value);
        void OnSoundEffectChanged(float value) => optionService.ApplySoundEffectVolume(value);

        public void Show() => optionView.Show();
        public void Hide() => optionView.Hide();
    }
}