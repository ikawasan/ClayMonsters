using UI.Option.Interface;
using VContainer;

namespace UI.Option
{
    /// <summary>
    /// ????????Presenter
    /// View?Service?????
    /// </summary>
    public sealed class OptionPresenter : IOptionPresenter
    {
        private readonly IOptionView optionView;
        private readonly IOptionService optionService;

        [Inject]
        public OptionPresenter(IOptionView optionView, IOptionService optionService)
        {
            this.optionView = optionView;
            this.optionService = optionService;
            Initialize();
        }

        public void Initialize()
        {
            optionView.InitVideoSettings(
                isFullScreen: optionService.GetFullScreen,
                isVSync: optionService.GetVSync);

            optionView.InitSoundSettings(
                musicVolume: optionService.GetMusicVolume,
                soundEffectVolume: optionService.GetSoundEffectVolume);

            optionView.SubscribeCloseButtonClick(Hide);
            optionView.SubscribeFullScreenChanged(optionService.SetFullScreen);
            optionView.SubscribeVSyncChanged(optionService.SetVSync);
            optionView.SubscribeMusicVolumeChanged(optionService.SetMusicVolume);
            optionView.SubscribeSoundEffectVolumeChanged(optionService.SetSoundEffectVolume);
        }

        /// <inheritdoc/>
        public void Show() => optionView.Show();

        /// <inheritdoc/>
        public void Hide() => optionView.Hide();
    }
}
