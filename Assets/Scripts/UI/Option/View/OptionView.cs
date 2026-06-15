using Extensions;
using LighthouseExtends.UIComponent.Button;
using System;
using UI.Option.Interface;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace UI.Option.View
{
    public class OptionView : MonoBehaviour, IOptionView
    {
        [SerializeField] Canvas canvas;
        [SerializeField] LHButton closeButton;

        [SerializeField] Toggle fullScreenToggle;
        [SerializeField] Slider brightnessSlider;
        [SerializeField] Toggle vSyncToggle;

        [SerializeField] Slider musicVolumeSlider;
        [SerializeField] Slider soundEffectSlider;

        private void Awake()
        {
            Hide();
        }

        public void Show() => canvas.enabled = true;
        public void Hide() => canvas.enabled = false;


        public void InitVideoSettings(bool isFullScreen, float brightness, bool isVSync)
        {
            fullScreenToggle.SetIsOnWithoutNotify(isFullScreen);
            brightnessSlider.SetValueWithoutNotify(brightness);
            vSyncToggle.SetIsOnWithoutNotify(isVSync);
        }

        public void InitSoundSettings(float musicVolume, float soundEffectVolume)
        {
            musicVolumeSlider.SetValueWithoutNotify(musicVolume);
            soundEffectSlider.SetValueWithoutNotify(soundEffectVolume);
        }

        public IDisposable SubscribeCloseButtonClick(Action action) => closeButton.SubscribeOnClick(action);

        public void SubscribeFullScreenChanged(UnityAction<bool> action) => fullScreenToggle.onValueChanged.AddListener(action);
        public void SubscribeBrightnessChanged(UnityAction<float> action) => brightnessSlider.onValueChanged.AddListener(action);
        public void SubscribeVSyncChanged(UnityAction<bool> action) => vSyncToggle.onValueChanged.AddListener(action);

        public void SubscribeMusicVolumeChanged(UnityAction<float> action) => musicVolumeSlider.onValueChanged.AddListener(action);
        public void SubscribeSoundEffectVolumeChanged(UnityAction<float> action) => soundEffectSlider.onValueChanged.AddListener(action);
    }
}