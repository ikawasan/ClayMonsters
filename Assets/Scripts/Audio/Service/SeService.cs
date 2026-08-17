using Audio.Interface;
using System.Collections.Generic;
using UnityEngine;

namespace Audio.Service
{
    /// <summary>
    /// ResourcesからSEを読み込みワンショットまたはループ再生する
    /// </summary>
    public sealed class SeService : ISeService
    {
        private const float MinTimedPitch = 0.35f;
        private const float MaxTimedPitch = 3f;
        private const float NativePitchMax = 3f;

        private readonly AudioSource oneShotSource;
        private readonly AudioSource timedSource;
        private readonly AudioSource loopSource;
        private readonly Dictionary<SeTrackId, AudioClip> clipCache = new();
        private float soundEffectVolume = 0.5f;
        private SeTrackId? timedTrackId;
        private SeTrackId? loopTrackId;
        private float[] loopPcmSamples;
        private int loopPcmChannels;
        private int loopPcmFrames;
        private double loopPcmReadPos;
        private volatile float loopPcmSpeed = 1f;
        private AudioClip loopStreamClip;
        private bool loopUsesStream;

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

            loopSource = root.AddComponent<AudioSource>();
            loopSource.playOnAwake = false;
            loopSource.loop = true;
            loopSource.spatialBlend = 0f;
            loopSource.dopplerLevel = 0f;
        }

        /// <inheritdoc />
        public void Play(SeTrackId trackId)
        {
            PlayOneShotClip(LoadClip(trackId), trackId);
        }

        /// <inheritdoc />
        public void PlayLoop(SeTrackId trackId, float pitch = 1f)
        {
            AudioClip clip = LoadClip(trackId);
            if (clip == null)
            {
                Debug.LogWarning($"[SeService] SEが見つかりません: {SeCatalog.GetResourcePath(trackId)}");
                return;
            }

            if (loopSource == null)
            {
                return;
            }

            float safePitch = Mathf.Max(0.01f, pitch);
            loopSource.volume = soundEffectVolume;
            if (loopTrackId == trackId && loopSource.isPlaying)
            {
                ApplyLoopSpeed(clip, safePitch, restart: false);
                return;
            }

            StartLoop(clip, trackId, safePitch);
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
            if (timedSource != null && timedTrackId.HasValue && timedTrackId.Value == trackId)
            {
                timedSource.Stop();
                timedSource.clip = null;
                timedSource.pitch = 1f;
                timedTrackId = null;
            }

            if (loopSource != null && loopTrackId.HasValue && loopTrackId.Value == trackId)
            {
                StopLoop();
            }
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

            if (loopSource != null && loopSource.isPlaying)
            {
                loopSource.volume = soundEffectVolume;
            }
        }

        private void StartLoop(AudioClip clip, SeTrackId trackId, float pitch)
        {
            loopSource.Stop();
            ReleaseLoopStreamClip();
            loopSource.loop = true;
            loopSource.volume = soundEffectVolume;
            ApplyLoopSpeed(clip, pitch, restart: true);
            loopTrackId = trackId;
        }

        private void ApplyLoopSpeed(AudioClip clip, float pitch, bool restart)
        {
            bool useStream = pitch > NativePitchMax;
            if (useStream)
            {
                if (restart || !loopUsesStream || loopStreamClip == null)
                {
                    StartStreamLoop(clip, pitch);
                    return;
                }

                loopPcmSpeed = pitch;
                return;
            }

            if (restart || loopUsesStream)
            {
                StartNativeLoop(clip, pitch);
                return;
            }

            loopSource.pitch = pitch;
        }

        private void StartNativeLoop(AudioClip clip, float pitch)
        {
            loopSource.Stop();
            ReleaseLoopStreamClip();
            loopSource.clip = clip;
            loopSource.loop = true;
            loopSource.volume = soundEffectVolume;
            loopSource.time = 0f;
            loopSource.Play();
            loopSource.pitch = pitch;
        }

        private void StartStreamLoop(AudioClip clip, float pitch)
        {
            if (!TryCacheLoopPcm(clip))
            {
                StartNativeLoop(clip, NativePitchMax);
                return;
            }

            loopSource.Stop();
            ReleaseLoopStreamClip();
            loopPcmSpeed = pitch;
            loopPcmReadPos = 0d;
            loopUsesStream = true;
            int streamSamples = Mathf.Max(clip.samples, clip.frequency);
            loopStreamClip = AudioClip.Create(
                clip.name + "_FastLoop",
                streamSamples,
                loopPcmChannels,
                clip.frequency,
                true,
                ReadLoopPcm,
                SetLoopPcmPosition);
            loopSource.clip = loopStreamClip;
            loopSource.loop = true;
            loopSource.pitch = 1f;
            loopSource.volume = soundEffectVolume;
            loopSource.Play();
        }

        private bool TryCacheLoopPcm(AudioClip clip)
        {
            int channels = clip.channels;
            int frames = clip.samples;
            if (channels <= 0 || frames <= 0)
            {
                return false;
            }

            int sampleCount = frames * channels;
            if (loopPcmSamples == null || loopPcmSamples.Length != sampleCount)
            {
                loopPcmSamples = new float[sampleCount];
            }

            if (!clip.GetData(loopPcmSamples, 0))
            {
                return false;
            }

            loopPcmChannels = channels;
            loopPcmFrames = frames;
            return true;
        }

        private void ReadLoopPcm(float[] data)
        {
            float[] source = loopPcmSamples;
            int channels = loopPcmChannels;
            int frames = loopPcmFrames;
            if (source == null || channels <= 0 || frames <= 0)
            {
                System.Array.Clear(data, 0, data.Length);
                return;
            }

            double pos = loopPcmReadPos;
            double speed = loopPcmSpeed;
            int outFrames = data.Length / channels;
            for (int i = 0; i < outFrames; i++)
            {
                int srcFrame = (int)pos % frames;
                if (srcFrame < 0)
                {
                    srcFrame += frames;
                }

                int srcIndex = srcFrame * channels;
                int dstIndex = i * channels;
                for (int c = 0; c < channels; c++)
                {
                    data[dstIndex + c] = source[srcIndex + c];
                }

                pos += speed;
            }

            loopPcmReadPos = pos;
        }

        private void SetLoopPcmPosition(int position)
        {
            int frames = loopPcmFrames;
            if (frames <= 0)
            {
                loopPcmReadPos = 0d;
                return;
            }

            loopPcmReadPos = position % frames;
        }

        private void StopLoop()
        {
            if (loopSource != null)
            {
                loopSource.Stop();
                loopSource.clip = null;
                loopSource.pitch = 1f;
            }

            ReleaseLoopStreamClip();
            loopTrackId = null;
        }

        private void ReleaseLoopStreamClip()
        {
            if (loopSource != null && loopStreamClip != null && loopSource.clip == loopStreamClip)
            {
                loopSource.clip = null;
            }

            if (loopStreamClip != null)
            {
                Object.Destroy(loopStreamClip);
                loopStreamClip = null;
            }

            loopUsesStream = false;
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
