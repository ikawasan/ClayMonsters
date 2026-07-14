using Audio.Interface;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Audio.Service
{
    /// <summary>
    /// ResourcesからBGMを読み込みクロスフェード再生する
    /// </summary>
    public sealed class BgmService : IBgmService
    {
        private const float CrossfadeDurationSeconds = 1.2f;

        private readonly BgmAudioHost host;
        private readonly AudioSource sourceA;
        private readonly AudioSource sourceB;
        private readonly Dictionary<BgmTrackId, AudioClip> clipCache = new();

        private AudioSource activeSource;
        private AudioSource inactiveSource;
        private BgmTrackId? currentTrackId;
        private float musicVolume = 0.5f;
        private int crossfadeGeneration;
        private CancellationTokenSource crossfadeCts;

        /// <inheritdoc />
        public bool IsPlaying =>
            (activeSource != null && activeSource.isPlaying)
            || (inactiveSource != null && inactiveSource.isPlaying);

        /// <inheritdoc />
        public bool TryGetCurrentTrack(out BgmTrackId trackId)
        {
            if (currentTrackId.HasValue && IsPlaying)
            {
                trackId = currentTrackId.Value;
                return true;
            }

            trackId = default;
            return false;
        }

        public BgmService()
        {
            var root = new GameObject("BgmService");
            UnityEngine.Object.DontDestroyOnLoad(root);
            host = root.AddComponent<BgmAudioHost>();

            sourceA = CreateSource(root.transform, "BgmSourceA");
            sourceB = CreateSource(root.transform, "BgmSourceB");
            activeSource = sourceA;
            inactiveSource = sourceB;
            ApplyMusicVolume();
        }

        /// <inheritdoc />
        public void Play(BgmTrackId trackId)
        {
            host.RefreshListenerState();

            AudioClip clip = LoadClip(trackId);
            if (clip == null)
            {
                Debug.LogWarning($"[BgmService] BGMが見つかりません: {BgmCatalog.GetResourcePath(trackId)}");
                return;
            }

            if (IsTrackAudiblyPlaying(trackId, clip))
            {
                return;
            }

            if (activeSource.isPlaying)
            {
                activeSource.volume = musicVolume;
            }

            int generation = ++crossfadeGeneration;
            crossfadeCts?.Cancel();
            crossfadeCts?.Dispose();
            crossfadeCts = new CancellationTokenSource();
            CrossfadeAsync(clip, trackId, generation, crossfadeCts.Token).Forget();
        }

        /// <inheritdoc />
        public async UniTask PlayAsync(
            BgmTrackId trackId,
            bool forceSwitch = false,
            CancellationToken cancellationToken = default)
        {
            host.RefreshListenerState();

            AudioClip clip = LoadClip(trackId);
            if (clip == null)
            {
                Debug.LogWarning($"[BgmService] BGMが見つかりません: {BgmCatalog.GetResourcePath(trackId)}");
                return;
            }

            if (!forceSwitch && IsTrackAudiblyPlaying(trackId, clip))
            {
                return;
            }

            if (activeSource.isPlaying)
            {
                activeSource.volume = musicVolume;
            }

            int generation = ++crossfadeGeneration;
            crossfadeCts?.Cancel();
            crossfadeCts?.Dispose();
            crossfadeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            await CrossfadeAsync(clip, trackId, generation, crossfadeCts.Token);
        }

        /// <inheritdoc />
        public async UniTask FadeOutAsync(CancellationToken cancellationToken = default)
        {
            int generation = ++crossfadeGeneration;
            crossfadeCts?.Cancel();
            crossfadeCts?.Dispose();
            crossfadeCts = null;

            if (!IsPlaying)
            {
                currentTrackId = null;
                return;
            }

            float startVolumeA = sourceA.isPlaying ? sourceA.volume : 0f;
            float startVolumeB = sourceB.isPlaying ? sourceB.volume : 0f;
            float elapsed = 0f;

            while (elapsed < CrossfadeDurationSeconds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (generation != crossfadeGeneration)
                {
                    return;
                }

                elapsed += Time.unscaledDeltaTime;
                float volumeScale = 1f - Mathf.Clamp01(elapsed / CrossfadeDurationSeconds);
                if (sourceA.isPlaying)
                {
                    sourceA.volume = startVolumeA * volumeScale;
                }

                if (sourceB.isPlaying)
                {
                    sourceB.volume = startVolumeB * volumeScale;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            if (generation != crossfadeGeneration)
            {
                return;
            }

            StopInternal();
        }

        /// <inheritdoc />
        public void Stop()
        {
            crossfadeGeneration++;
            crossfadeCts?.Cancel();
            crossfadeCts?.Dispose();
            crossfadeCts = null;
            StopInternal();
        }

        private void StopInternal()
        {
            currentTrackId = null;
            sourceA.Stop();
            sourceB.Stop();
            sourceA.clip = null;
            sourceB.clip = null;
            ApplyMusicVolume();
        }

        /// <inheritdoc />
        public void SetMusicVolume(float volume)
        {
            musicVolume = Mathf.Clamp01(volume);
            ApplyMusicVolume();
        }

        private bool IsTrackAudiblyPlaying(BgmTrackId trackId, AudioClip clip)
        {
            return currentTrackId == trackId
                && activeSource != null
                && activeSource.isPlaying
                && activeSource.clip == clip;
        }

        private static AudioSource CreateSource(Transform parent, string objectName)
        {
            var sourceObject = new GameObject(objectName);
            sourceObject.transform.SetParent(parent, false);
            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            return source;
        }

        private AudioClip LoadClip(BgmTrackId trackId)
        {
            if (clipCache.TryGetValue(trackId, out AudioClip cached))
            {
                return cached;
            }

            string path = BgmCatalog.GetResourcePath(trackId);
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

        private void ApplyMusicVolume()
        {
            if (activeSource != null)
            {
                activeSource.volume = musicVolume;
            }

            if (inactiveSource != null)
            {
                inactiveSource.volume = musicVolume;
            }
        }

        private async UniTask CrossfadeAsync(
            AudioClip nextClip,
            BgmTrackId trackId,
            int generation,
            CancellationToken cancellationToken)
        {
            AudioSource fromSource = activeSource;
            AudioSource toSource = inactiveSource;

            try
            {
                toSource.clip = nextClip;
                toSource.volume = 0f;
                toSource.Play();

                if (!fromSource.isPlaying || fromSource.clip == null)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (generation != crossfadeGeneration)
                    {
                        return;
                    }

                    fromSource.Stop();
                    fromSource.clip = null;
                    toSource.volume = musicVolume;
                    activeSource = toSource;
                    inactiveSource = fromSource;
                    currentTrackId = trackId;
                    return;
                }

                float elapsed = 0f;
                float fromStartVolume = musicVolume;

                while (elapsed < CrossfadeDurationSeconds)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (generation != crossfadeGeneration)
                    {
                        return;
                    }

                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / CrossfadeDurationSeconds);
                    toSource.volume = Mathf.Lerp(0f, musicVolume, t);
                    fromSource.volume = Mathf.Lerp(fromStartVolume, 0f, t);
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }

                if (generation != crossfadeGeneration)
                {
                    return;
                }

                fromSource.Stop();
                fromSource.clip = null;
                toSource.volume = musicVolume;
                activeSource = toSource;
                inactiveSource = fromSource;
                currentTrackId = trackId;
            }
            catch (OperationCanceledException)
            {
            }
        }

        private sealed class BgmAudioHost : MonoBehaviour
        {
            private AudioListener audioListener;

            private void Awake()
            {
                audioListener = gameObject.GetComponent<AudioListener>();
                if (audioListener == null)
                {
                    audioListener = gameObject.AddComponent<AudioListener>();
                }

                SceneManager.sceneLoaded += OnSceneChanged;
                SceneManager.sceneUnloaded += OnSceneChanged;
                RefreshListenerState();
            }

            private void OnDestroy()
            {
                SceneManager.sceneLoaded -= OnSceneChanged;
                SceneManager.sceneUnloaded -= OnSceneChanged;
            }

            private void OnSceneChanged(Scene scene, LoadSceneMode mode)
            {
                RefreshListenerState();
            }

            private void OnSceneChanged(Scene scene)
            {
                RefreshListenerState();
            }

            public void RefreshListenerState()
            {
                if (audioListener == null)
                {
                    return;
                }

                AudioListener[] listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                bool hasActiveSceneListener = false;
                for (int i = 0; i < listeners.Length; i++)
                {
                    AudioListener listener = listeners[i];
                    if (listener == null || listener == audioListener || !listener.isActiveAndEnabled)
                    {
                        continue;
                    }

                    if (listener.gameObject.scene.name == "DontDestroyOnLoad")
                    {
                        continue;
                    }

                    hasActiveSceneListener = true;
                    break;
                }

                audioListener.enabled = !hasActiveSceneListener;
            }
        }
    }
}
