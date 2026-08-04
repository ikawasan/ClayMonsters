using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using LighthouseExtends.TextTable;
using Localization;
using R3;
using UI.Option.Interface;
using VContainer;

namespace UI.Option
{
    /// <summary>
    /// オプション画面のPresenter
    /// ViewとServiceを接続する
    /// </summary>
    public sealed class OptionPresenter : IOptionPresenter, IDisposable
    {
        private readonly IOptionView optionView;
        private readonly IOptionService optionService;
        private readonly ITextTableService textTableService;
        private readonly CancellationTokenSource lifetimeCts = new();
        private IDisposable languageSubscription;
        private bool isCyclingLanguage;

        [Inject]
        public OptionPresenter(
            IOptionView optionView,
            IOptionService optionService,
            ITextTableService textTableService)
        {
            this.optionView = optionView;
            this.optionService = optionService;
            this.textTableService = textTableService;
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

            optionView.InitLanguageSetting(optionService.GetLanguageDisplayName);
            ApplyLabels();

            optionView.SubscribeCloseButtonClick(Hide);
            optionView.SubscribeFullScreenChanged(optionService.SetFullScreen);
            optionView.SubscribeVSyncChanged(optionService.SetVSync);
            optionView.SubscribeMusicVolumeChanged(optionService.SetMusicVolume);
            optionView.SubscribeSoundEffectVolumeChanged(optionService.SetSoundEffectVolume);
            optionView.SubscribeLanguagePrevButtonClick(() => CycleLanguage(-1));
            optionView.SubscribeLanguageNextButtonClick(() => CycleLanguage(1));

            languageSubscription = textTableService.CurrentLanguage
                .Subscribe(_ =>
                {
                    ApplyLabels();
                    optionView.InitLanguageSetting(optionService.GetLanguageDisplayName);
                });
        }

        /// <inheritdoc/>
        public void Show()
        {
            ApplyLabels();
            optionView.InitLanguageSetting(optionService.GetLanguageDisplayName);
            optionView.Show();
        }

        /// <inheritdoc/>
        public void Hide() => optionView.Hide();

        public void Dispose()
        {
            languageSubscription?.Dispose();
            languageSubscription = null;
            lifetimeCts.Cancel();
            lifetimeCts.Dispose();
        }

        private void ApplyLabels()
        {
            // シーン配置の日本語原文をフォールバックにする
            optionView.ApplyCapturedLocalizedLabels();
        }

        private void CycleLanguage(int delta)
        {
            if (isCyclingLanguage)
            {
                return;
            }

            CycleLanguageAsync(delta).Forget();
        }

        private async UniTaskVoid CycleLanguageAsync(int delta)
        {
            isCyclingLanguage = true;
            try
            {
                await optionService.CycleLanguageAsync(delta, lifetimeCts.Token);
                optionView.InitLanguageSetting(optionService.GetLanguageDisplayName);
                ApplyLabels();
            }
            finally
            {
                isCyclingLanguage = false;
            }
        }
    }
}
