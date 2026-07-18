using Extensions;
using LighthouseExtends.UIComponent.Button;
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
    public sealed class OptionView : MonoBehaviour, IOptionView
    {
        private const float PanelWidth = 560f;
        private const float PanelHeight = 860f;
        private const string PanelResourcePath = "Image/Title/TitleOptionPanel";
        private const string ButtonNormalResourcePath = "Image/Title/TitleMenuButton_Normal";
        private const string ButtonHighlightedResourcePath = "Image/Title/TitleMenuButton_Highlighted";
        private const string ButtonPressedResourcePath = "Image/Title/TitleMenuButton_Pressed";
        private const string SliderTrackResourcePath = "Image/Title/TitleOptionSlider_Track";
        private const string SliderFillResourcePath = "Image/Title/TitleOptionSlider_Fill";
        private const string SliderHandleResourcePath = "Image/Title/TitleOptionSlider_Handle";
        private const string ToggleOffResourcePath = "Image/Title/TitleOptionToggle_Off";
        private const string ToggleOnResourcePath = "Image/Title/TitleOptionToggle_On";

        [SerializeField] private Canvas canvas;
        [SerializeField] private LHButton closeButton;
        [SerializeField] private Toggle fullScreenToggle;
        [SerializeField] private Toggle vSyncToggle;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Slider soundEffectSlider;
        [SerializeField] private Image panelImage;

        private void Awake()
        {
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
            musicVolumeSlider.SetValueWithoutNotify(musicVolume);
            soundEffectSlider.SetValueWithoutNotify(soundEffectVolume);
        }

        /// <inheritdoc/>
        public IDisposable SubscribeCloseButtonClick(UnityAction action) => closeButton.SubscribeOnClick(action);

        /// <inheritdoc/>
        public void SubscribeFullScreenChanged(UnityAction<bool> action) => fullScreenToggle.onValueChanged.AddListener(action);

        /// <inheritdoc/>
        public void SubscribeVSyncChanged(UnityAction<bool> action) => vSyncToggle.onValueChanged.AddListener(action);

        /// <inheritdoc/>
        public void SubscribeMusicVolumeChanged(UnityAction<float> action) => musicVolumeSlider.onValueChanged.AddListener(action);

        /// <inheritdoc/>
        public void SubscribeSoundEffectVolumeChanged(UnityAction<float> action) => soundEffectSlider.onValueChanged.AddListener(action);

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

        private void ApplyClayStyle()
        {
            ApplyBlockerStyle();
            ApplyPanelStyle();
            ApplyCloseButtonStyle();
            ApplyTextStyles();
            ApplySliderStyle(musicVolumeSlider);
            ApplySliderStyle(soundEffectSlider);
            ApplyToggleStyle(fullScreenToggle);
            ApplyToggleStyle(vSyncToggle);
        }

        private void ApplyBlockerStyle()
        {
            Transform blocker = transform.Find("BackgroundBlocker");
            if (blocker == null || !blocker.TryGetComponent(out Image image))
            {
                return;
            }

            image.raycastTarget = true;
        }

        private void ApplyPanelStyle()
        {
            if (panelImage == null)
            {
                Transform panelTransform = transform.Find("OptionPanel");
                if (panelTransform != null)
                {
                    panelImage = panelTransform.GetComponent<Image>();
                }
            }

            if (panelImage == null)
            {
                return;
            }

            Sprite panelSprite = Resources.Load<Sprite>(PanelResourcePath);
            if (panelSprite == null)
            {
                panelSprite = Resources.Load<Sprite>(ButtonNormalResourcePath);
            }

            if (panelSprite == null)
            {
                return;
            }

            panelImage.sprite = panelSprite;
            panelImage.type = Image.Type.Sliced;
            panelImage.raycastTarget = false;
            panelImage.pixelsPerUnitMultiplier = 1f;
        }

        private void ApplyCloseButtonStyle()
        {
            Sprite normalSprite = Resources.Load<Sprite>(ButtonNormalResourcePath);
            Sprite highlightedSprite = Resources.Load<Sprite>(ButtonHighlightedResourcePath);
            Sprite pressedSprite = Resources.Load<Sprite>(ButtonPressedResourcePath);
            if (normalSprite == null)
            {
                return;
            }

            highlightedSprite ??= normalSprite;
            pressedSprite ??= normalSprite;
            ApplyButtonStyle(closeButton, normalSprite, highlightedSprite, pressedSprite, "閉じる", TextAlignmentOptions.Center);
        }

        private void ApplyTextStyles()
        {
            foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.transform.IsChildOf(closeButton.transform))
                {
                    continue;
                }

                text.raycastTarget = false;
            }
        }

        private void ApplySliderStyle(Slider slider)
        {
            if (slider == null)
            {
                return;
            }

            Sprite trackSprite = Resources.Load<Sprite>(SliderTrackResourcePath);
            Sprite fillSprite = Resources.Load<Sprite>(SliderFillResourcePath);
            Sprite handleSprite = Resources.Load<Sprite>(SliderHandleResourcePath);
            if (trackSprite == null || fillSprite == null || handleSprite == null)
            {
                return;
            }

            slider.transform.localScale = Vector3.one;

            Image background = slider.transform.Find("Background")?.GetComponent<Image>();
            if (background != null)
            {
                background.sprite = trackSprite;
                background.type = Image.Type.Sliced;
            }

            Transform fillArea = slider.fillRect != null ? slider.fillRect.parent : slider.transform.Find("Fill Area");
            Image fill = slider.fillRect != null ? slider.fillRect.GetComponent<Image>() : fillArea?.Find("Fill")?.GetComponent<Image>();
            if (fill != null)
            {
                fill.sprite = fillSprite;
                fill.type = Image.Type.Sliced;
            }

            Image handle = slider.handleRect != null
                ? slider.handleRect.GetComponent<Image>()
                : slider.transform.Find("Handle Slide Area/Handle")?.GetComponent<Image>();
            if (handle != null)
            {
                handle.sprite = handleSprite;
                handle.type = Image.Type.Simple;
                handle.SetNativeSize();
            }

            slider.targetGraphic = handle;
        }

        private void ApplyToggleStyle(Toggle toggle)
        {
            if (toggle == null)
            {
                return;
            }

            Sprite offSprite = Resources.Load<Sprite>(ToggleOffResourcePath);
            Sprite onSprite = Resources.Load<Sprite>(ToggleOnResourcePath);
            if (offSprite == null || onSprite == null)
            {
                return;
            }

            toggle.transform.localScale = Vector3.one;

            Image background = toggle.transform.Find("Background")?.GetComponent<Image>();
            if (background != null)
            {
                background.sprite = offSprite;
                background.type = Image.Type.Simple;
                background.SetNativeSize();
                toggle.targetGraphic = background;
            }

            Image checkmark = toggle.graphic as Image;
            if (checkmark == null)
            {
                checkmark = toggle.transform.Find("Checkmark")?.GetComponent<Image>();
            }

            if (checkmark != null)
            {
                checkmark.sprite = onSprite;
                checkmark.type = Image.Type.Simple;
                checkmark.SetNativeSize();
                toggle.graphic = checkmark;
            }
        }

        private static void ApplyButtonStyle(
            LHButton button,
            Sprite normalSprite,
            Sprite highlightedSprite,
            Sprite pressedSprite,
            string label,
            TextAlignmentOptions alignment)
        {
            if (button == null)
            {
                return;
            }

            if (button.targetGraphic is Image image)
            {
                image.sprite = normalSprite;
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 1f;
            }

            button.transition = Selectable.Transition.SpriteSwap;
            SpriteState spriteState = button.spriteState;
            spriteState.highlightedSprite = highlightedSprite;
            spriteState.pressedSprite = pressedSprite;
            spriteState.selectedSprite = highlightedSprite;
            spriteState.disabledSprite = normalSprite;
            button.spriteState = spriteState;

            TMP_Text labelText = button.GetComponentInChildren<TMP_Text>(true);
            if (labelText != null)
            {
                labelText.text = label;
                labelText.raycastTarget = false;
            }
        }
    }
}
