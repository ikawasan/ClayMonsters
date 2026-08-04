using Extensions;
using LighthouseExtends.UIComponent.Button;
using Localization;
using System;
using TMPro;
using UI.Option.Interface;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace UI.Option.View
{
    /// <summary>
    /// ゲーム全体で常駐するオプション画面のView
    /// 粘土風パネル・スライダー・トグル・閉じるボタンを表示する
    /// </summary>
    public sealed class OptionView : MonoBehaviour, IOptionView, ILanguageAwareUi
    {
        [SerializeField] private Canvas canvas;
        [SerializeField] private LHButton closeButton;
        [SerializeField] private Toggle fullScreenToggle;
        [SerializeField] private Toggle vSyncToggle;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Slider soundEffectSlider;
        [SerializeField] private Image panelImage;
        [SerializeField] private LHButton languagePrevButton;
        [SerializeField] private LHButton languageNextButton;
        [SerializeField] private TMP_Text languageValueText;
        [SerializeField] private TMP_Text optionTitleText;
        [SerializeField] private TMP_Text videoTitleText;
        [SerializeField] private TMP_Text audioTitleText;
        [SerializeField] private TMP_Text languageTitleText;
        [SerializeField] private TMP_Text fullScreenLabelText;
        [SerializeField] private TMP_Text vSyncLabelText;
        [SerializeField] private TMP_Text musicLabelText;
        [SerializeField] private TMP_Text soundEffectLabelText;
        [SerializeField] private TMP_Text closeButtonLabelText;

        private void Awake()
        {
            ValidateReferences();
            HideBrightnessSettingUi();
            Hide();
        }

        /// <inheritdoc/>
        public void Show() => canvas.enabled = true;

        /// <inheritdoc/>
        public void Hide() => canvas.enabled = false;

        /// <inheritdoc/>
        public void InitVideoSettings(bool isFullScreen, bool isVSync)
        {
            if (fullScreenToggle != null)
            {
                fullScreenToggle.SetIsOnWithoutNotify(isFullScreen);
            }

            if (vSyncToggle != null)
            {
                vSyncToggle.SetIsOnWithoutNotify(isVSync);
            }
        }

        /// <inheritdoc/>
        public void InitSoundSettings(float musicVolume, float soundEffectVolume)
        {
            if (musicVolumeSlider != null)
            {
                musicVolumeSlider.SetValueWithoutNotify(musicVolume);
            }

            if (soundEffectSlider != null)
            {
                soundEffectSlider.SetValueWithoutNotify(soundEffectVolume);
            }
        }

        /// <inheritdoc/>
        public void InitLanguageSetting(string languageDisplayName)
        {
            LocalizedFont.SetText(languageValueText, languageDisplayName);
        }

        /// <inheritdoc/>
        public void ApplyLocalizedLabels(
            string title,
            string videoTitle,
            string audioTitle,
            string languageTitle,
            string fullScreen,
            string vSync,
            string music,
            string soundEffect,
            string close)
        {
            SetText(optionTitleText, title);
            SetText(videoTitleText, videoTitle);
            SetText(audioTitleText, audioTitle);
            SetText(languageTitleText, languageTitle);
            SetText(fullScreenLabelText, fullScreen);
            SetText(vSyncLabelText, vSync);
            SetText(musicLabelText, music);
            SetText(soundEffectLabelText, soundEffect);
            SetText(closeButtonLabelText, close);
        }

        /// <inheritdoc/>
        public void RefreshLocalizedUi()
        {
            // LanguageAwareUi経路でもラベルを再適用する
            // PresenterのCurrentLanguage購読と二重でも問題ない
            ApplyCapturedLocalizedLabels();
            InitLanguageSetting(LanguageDisplayNames.Get(LocalizedText.CurrentLanguageCode));
        }

        /// <inheritdoc/>
        public void ApplyCapturedLocalizedLabels()
        {
            CaptureSceneLabelOriginalsIfNeeded();
            ApplyLocalizedLabels(
                title: SceneLocalizedLabel.Resolve(GameTextKeys.OptionTitle, optionTitleOriginal),
                videoTitle: SceneLocalizedLabel.Resolve(GameTextKeys.OptionVideo, videoTitleOriginal),
                audioTitle: SceneLocalizedLabel.Resolve(GameTextKeys.OptionAudio, audioTitleOriginal),
                languageTitle: SceneLocalizedLabel.Resolve(
                    GameTextKeys.OptionLanguage,
                    languageTitleOriginal),
                fullScreen: SceneLocalizedLabel.Resolve(
                    GameTextKeys.OptionFullScreen,
                    fullScreenOriginal),
                vSync: SceneLocalizedLabel.Resolve(GameTextKeys.OptionVSync, vSyncOriginal),
                music: SceneLocalizedLabel.Resolve(GameTextKeys.OptionMusic, musicOriginal),
                soundEffect: SceneLocalizedLabel.Resolve(
                    GameTextKeys.OptionSoundEffect,
                    soundEffectOriginal),
                close: SceneLocalizedLabel.Resolve(GameTextKeys.OptionClose, closeOriginal));
        }

        private bool sceneLabelOriginalsCaptured;
        private string optionTitleOriginal = "オプション";
        private string videoTitleOriginal = "【ビデオ】";
        private string audioTitleOriginal = "【オーディオ】";
        private string languageTitleOriginal = "【言語】";
        private string fullScreenOriginal = "全画面";
        private string vSyncOriginal = "垂直同期";
        private string musicOriginal = "音楽";
        private string soundEffectOriginal = "効果音";
        private string closeOriginal = "閉じる";

        private void CaptureSceneLabelOriginalsIfNeeded()
        {
            if (sceneLabelOriginalsCaptured)
            {
                return;
            }

            optionTitleOriginal = SceneLocalizedLabel.Capture(optionTitleText, optionTitleOriginal);
            videoTitleOriginal = SceneLocalizedLabel.Capture(videoTitleText, videoTitleOriginal);
            audioTitleOriginal = SceneLocalizedLabel.Capture(audioTitleText, audioTitleOriginal);
            languageTitleOriginal = SceneLocalizedLabel.Capture(
                languageTitleText,
                languageTitleOriginal);
            fullScreenOriginal = SceneLocalizedLabel.Capture(fullScreenLabelText, fullScreenOriginal);
            vSyncOriginal = SceneLocalizedLabel.Capture(vSyncLabelText, vSyncOriginal);
            musicOriginal = SceneLocalizedLabel.Capture(musicLabelText, musicOriginal);
            soundEffectOriginal = SceneLocalizedLabel.Capture(
                soundEffectLabelText,
                soundEffectOriginal);
            closeOriginal = SceneLocalizedLabel.Capture(closeButtonLabelText, closeOriginal);
            sceneLabelOriginalsCaptured = true;
        }

        /// <inheritdoc/>
        public IDisposable SubscribeCloseButtonClick(UnityAction action) => closeButton.SubscribeOnClick(action);

        /// <inheritdoc/>
        public void SubscribeFullScreenChanged(UnityAction<bool> action)
        {
            if (fullScreenToggle != null)
            {
                fullScreenToggle.onValueChanged.AddListener(action);
            }
        }

        /// <inheritdoc/>
        public void SubscribeVSyncChanged(UnityAction<bool> action)
        {
            if (vSyncToggle != null)
            {
                vSyncToggle.onValueChanged.AddListener(action);
            }
        }

        /// <inheritdoc/>
        public void SubscribeMusicVolumeChanged(UnityAction<float> action)
        {
            if (musicVolumeSlider != null)
            {
                musicVolumeSlider.onValueChanged.AddListener(action);
            }
        }

        /// <inheritdoc/>
        public void SubscribeSoundEffectVolumeChanged(UnityAction<float> action)
        {
            if (soundEffectSlider != null)
            {
                soundEffectSlider.onValueChanged.AddListener(action);
            }
        }

        /// <inheritdoc/>
        public IDisposable SubscribeLanguagePrevButtonClick(UnityAction action)
        {
            if (languagePrevButton == null)
            {
                return EmptyDisposable.Instance;
            }

            return languagePrevButton.SubscribeOnClick(action);
        }

        /// <inheritdoc/>
        public IDisposable SubscribeLanguageNextButtonClick(UnityAction action)
        {
            if (languageNextButton == null)
            {
                return EmptyDisposable.Instance;
            }

            return languageNextButton.SubscribeOnClick(action);
        }

        private void ValidateReferences()
        {
            if (canvas == null)
            {
                Debug.LogError("[OptionView] canvasが未配線です", this);
            }

            if (closeButton == null)
            {
                Debug.LogError("[OptionView] closeButtonが未配線です", this);
            }

            if (languagePrevButton == null || languageNextButton == null || languageValueText == null)
            {
                Debug.LogError(
                    "[OptionView] 言語切替UIが未配線です languagePrevButton/languageNextButton/languageValueTextをInspectorで接続してください",
                    this);
            }
        }

        private void HideBrightnessSettingUi()
        {
            Transform brightnessRoot = transform.Find("OptionPanel/VideoOptionUI/VideoOption/BrightnessSlider");
            if (brightnessRoot == null)
            {
                brightnessRoot = transform.Find("BrightnessSlider");
            }

            if (brightnessRoot != null)
            {
                brightnessRoot.gameObject.SetActive(false);
            }
        }

        private static void SetText(TMP_Text text, string value)
        {
            LocalizedFont.SetText(text, value);
        }

        private sealed class EmptyDisposable : IDisposable
        {
            public static readonly EmptyDisposable Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
