using Audio.Interface;
using System.Collections.Generic;
using UnityEngine;

namespace Audio.Service
{
    /// <summary>
    /// Resourcesから戦闘SEを読み込みワンショット再生する
    /// </summary>
    public sealed class SeService : ISeService
    {
        private readonly AudioSource source;
        private readonly Dictionary<SeTrackId, AudioClip> clipCache = new();
        private float soundEffectVolume = 0.5f;

        public SeService()
        {
            var root = new GameObject("SeService");
            Object.DontDestroyOnLoad(root);
            source = root.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
        }

        /// <inheritdoc />
        public void Play(SeTrackId trackId)
        {
            PlayClip(LoadClip(trackId), trackId);
        }

        /// <inheritdoc />
        public void PlayAttackHit()
        {
            Play(SeTrackId.AttackHit);
        }

        /// <inheritdoc />
        public void PlayAttackMiss()
        {
            Play(SeTrackId.AttackMiss);
        }

        /// <inheritdoc />
        public void PlayPartsBreak()
        {
            Play(SeTrackId.PartsBreak);
        }

        /// <inheritdoc />
        public void SetSoundEffectVolume(float volume)
        {
            soundEffectVolume = Mathf.Clamp01(volume);
        }

        private AudioClip LoadClip(SeTrackId trackId)
        {
            if (clipCache.TryGetValue(trackId, out AudioClip cached) && cached != null)
            {
                return cached;
            }

            string path = SeCatalog.GetResourcePath(trackId);
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            AudioClip clip = Resources.Load<AudioClip>(path);
            if (clip != null)
            {
                clipCache[trackId] = clip;
            }

            return clip;
        }

        private void PlayClip(AudioClip clip, SeTrackId trackId)
        {
            if (clip == null)
            {
                Debug.LogWarning($"[SeService] SEが見つかりません: {SeCatalog.GetResourcePath(trackId)}");
                return;
            }

            if (soundEffectVolume <= 0f || source == null)
            {
                return;
            }

            source.PlayOneShot(clip, soundEffectVolume);
        }
    }
}
