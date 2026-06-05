using Extensions;
using LighthouseExtends.UIComponent.Button;
using System;
using UI.Option.Interface;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Option.View
{
    public class OptionView : MonoBehaviour, IOptionView
    {
        [SerializeField] Canvas canvas;
        [SerializeField] LHButton closeButton;
        [SerializeField] Slider resolutionSlider;
        [SerializeField] Toggle fullScreenToggle;
        [SerializeField] Slider brightnessSlider;
        [SerializeField] Toggle vSyncToggle;

        [SerializeField] Slider musicVolumeSlider;
        [SerializeField] Slider soundEffectSlider;

        private void Awake()
        {
            closeButton.SubscribeOnClick(Hide);
            Hide();
        }

        public void Show() => canvas.enabled = true;
        public void Hide()=> canvas.enabled = false;

        public IDisposable SubscribeCloseButtonClick(Action action) => closeButton.SubscribeOnClick(action);

        public void InitVideoSettings(int resolutionMax, int resolutionCurrent, bool isFullScreen, float brightness, bool isVSync)
        {
            resolutionSlider.maxValue = resolutionMax;
            resolutionSlider.SetValueWithoutNotify(resolutionCurrent);
            fullScreenToggle.SetIsOnWithoutNotify(isFullScreen);
            brightnessSlider.SetValueWithoutNotify(brightness);
            vSyncToggle.SetIsOnWithoutNotify(isVSync);
        }

        public void InitSoundSettings(float musicVolume, float soundEffectVolume)
        {
            musicVolumeSlider.SetValueWithoutNotify(musicVolume);
            soundEffectSlider.SetValueWithoutNotify(soundEffectVolume);
        }

        public void SubscribeResolutionChanged(Action<float> action) => resolutionSlider.onValueChanged.AddListener(val => action(val));
        public void SubscribeFullScreenChanged(Action<bool> action) => fullScreenToggle.onValueChanged.AddListener(val => action(val));
        public void SubscribeBrightnessChanged(Action<float> action) => brightnessSlider.onValueChanged.AddListener(val => action(val));
        public void SubscribeVSyncChanged(Action<bool> action) => vSyncToggle.onValueChanged.AddListener(val => action(val));

        public void SubscribeMusicVolumeChanged(Action<float> action) => musicVolumeSlider.onValueChanged.AddListener(val => action(val));
        public void SubscribeSoundEffectVolumeChanged(Action<float> action) => soundEffectSlider.onValueChanged.AddListener(val => action(val));
    }
}