using Audio.Interface;
using UnityEngine;

namespace Audio.Service
{
    /// <summary>
    /// UI効果音を2Dでワンショット再生する
    /// </summary>
    public sealed class UiSoundService : IUiSoundService
    {
        private readonly UiSoundSettings settings;
        private readonly AudioSource source;
        private float soundEffectVolume = 0.3f;

        public UiSoundService(UiSoundSettings settings)
        {
            this.settings = settings;

            var root = new GameObject("UiSoundService");
            Object.DontDestroyOnLoad(root);
            source = root.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
        }

        /// <inheritdoc />
        public void PlayHover()
        {
            float scale = settings != null ? settings.HoverVolumeScale : 1f;
            PlayClip(settings != null ? settings.HoverClip : null, scale);
        }

        /// <inheritdoc />
        public void PlayClick()
        {
            float scale = settings != null ? settings.ClickVolumeScale : 1f;
            PlayClip(settings != null ? settings.ClickClip : null, scale);
        }

        /// <inheritdoc />
        public void SetSoundEffectVolume(float volume)
        {
            soundEffectVolume = Mathf.Clamp01(volume);
        }

        private void PlayClip(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null || soundEffectVolume <= 0f || source == null)
            {
                return;
            }

            float volume = soundEffectVolume * Mathf.Clamp01(volumeScale);
            if (volume <= 0f)
            {
                return;
            }

            source.PlayOneShot(clip, volume);
        }
    }
}
