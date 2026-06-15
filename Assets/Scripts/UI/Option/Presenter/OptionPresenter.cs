using UI.Option.Interface;
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
            // 初期値の適用
            optionView.InitVideoSettings(
                isFullScreen: this.optionService.GetFullScreen,
                brightness: this.optionService.GetBrightness,
                isVSync: this.optionService.GetVSync
            );

            optionView.InitSoundSettings(
                musicVolume: this.optionService.GetMusicVolume,
                soundEffectVolume: this.optionService.GetSoundEffectVolume
            );

            // UIが完全に準備完了した状態でイベントを購読する
            optionView.SubscribeCloseButtonClick(Hide);
            optionView.SubscribeFullScreenChanged(optionService.SetFullScreen);
            optionView.SubscribeBrightnessChanged(optionService.SetBrightness);
            optionView.SubscribeVSyncChanged(optionService.SetVSync);
            optionView.SubscribeMusicVolumeChanged(optionService.SetMusicVolume);
            optionView.SubscribeSoundEffectVolumeChanged(optionService.SetSoundEffectVolume);
        }

        public void Show() => optionView.Show();
        public void Hide() => optionView.Hide();
    }
}