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
            ApplyLocalizedLabels(
                title: LocalizedText.GetOrFallback(GameTextKeys.OptionTitle, "設定"),
                videoTitle: LocalizedText.GetOrFallback(GameTextKeys.OptionVideo, "【ビデオ】"),
                audioTitle: LocalizedText.GetOrFallback(GameTextKeys.OptionAudio, "【オーディオ】"),
                languageTitle: LocalizedText.GetOrFallback(GameTextKeys.OptionLanguage, "【言語】"),
                fullScreen: LocalizedText.GetOrFallback(GameTextKeys.OptionFullScreen, "全画面"),
                vSync: LocalizedText.GetOrFallback(GameTextKeys.OptionVSync, "垂直同期"),
                music: LocalizedText.GetOrFallback(GameTextKeys.OptionMusic, "音楽"),
                soundEffect: LocalizedText.GetOrFallback(GameTextKeys.OptionSoundEffect, "効果音"),
                close: LocalizedText.GetOrFallback(GameTextKeys.OptionClose, "閉じる"));
            InitLanguageSetting(LanguageDisplayNames.Get(LocalizedText.CurrentLanguageCode));
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
