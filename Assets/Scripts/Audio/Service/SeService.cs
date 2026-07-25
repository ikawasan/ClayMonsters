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
        private const float MinTimedPitch = 0.35f;
        private const float MaxTimedPitch = 3f;

        private readonly AudioSource oneShotSource;
        private readonly AudioSource timedSource;
        private readonly Dictionary<SeTrackId, AudioClip> clipCache = new();
        private float soundEffectVolume = 0.5f;
        private SeTrackId? timedTrackId;

        public SeService()
        {
            var root = new GameObject("SeService");
            Object.DontDestroyOnLoad(root);

            oneShotSource = root.AddComponent<AudioSource>();
            oneShotSource.playOnAwake = false;
            oneShotSource.loop = false;
            oneShotSource.spatialBlend = 0f;

            timedSource = root.AddComponent<AudioSource>();
            timedSource.playOnAwake = false;
            timedSource.loop = false;
            timedSource.spatialBlend = 0f;
        }

        /// <inheritdoc />
        public void Play(SeTrackId trackId)
        {
            PlayOneShotClip(LoadClip(trackId), trackId);
        }

        /// <inheritdoc />
        public void PlayTimed(SeTrackId trackId, float durationSeconds)
        {
            AudioClip clip = LoadClip(trackId);
            if (clip == null)
            {
                Debug.LogWarning($"[SeService] SEが見つかりません: {SeCatalog.GetResourcePath(trackId)}");
                return;
            }

            if (soundEffectVolume <= 0f || timedSource == null)
            {
                return;
            }

            float safeDuration = Mathf.Max(0.05f, durationSeconds);
            float pitch = Mathf.Clamp(clip.length / safeDuration, MinTimedPitch, MaxTimedPitch);

            timedSource.Stop();
            timedSource.clip = clip;
            timedSource.volume = soundEffectVolume;
            timedSource.pitch = pitch;
            timedSource.time = 0f;
            timedSource.Play();
            timedTrackId = trackId;
        }

        /// <inheritdoc />
        public void Stop(SeTrackId trackId)
        {
            if (timedSource == null || !timedTrackId.HasValue || timedTrackId.Value != trackId)
            {
                return;
            }

            timedSource.Stop();
            timedSource.clip = null;
            timedSource.pitch = 1f;
            timedTrackId = null;
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
            if (timedSource != null && timedSource.isPlaying)
            {
                timedSource.volume = soundEffectVolume;
            }
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

        private void PlayOneShotClip(AudioClip clip, SeTrackId trackId)
        {
            if (clip == null)
            {
                Debug.LogWarning($"[SeService] SEが見つかりません: {SeCatalog.GetResourcePath(trackId)}");
                return;
            }

            if (soundEffectVolume <= 0f || oneShotSource == null)
            {
                return;
            }

            oneShotSource.pitch = 1f;
            oneShotSource.PlayOneShot(clip, soundEffectVolume);
        }
    }
}
