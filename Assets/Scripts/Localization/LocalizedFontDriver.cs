using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using LighthouseExtends.Font;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

namespace Localization
{
    /// <summary>
    /// 言語切替シーン遷移生成UIすべてに現在言語フォントを適用する
    /// シーン直置きのTextMeshProUGUIもLanguageFontSettingsで上書きする
    /// LHTextMeshProがfontのみ差し替えて輪郭を潰した直後も再適用で復旧する
    /// </summary>
    public sealed class LocalizedFontDriver : IStartable, ITickable, IDisposable
    {
        private const float ScanIntervalSeconds = 3f;
        private const float PruneIntervalSeconds = 8f;
        private const float CacheRefreshIntervalSeconds = 6f;

        private readonly IFontService fontService;
        private IDisposable subscription;
        private TMP_FontAsset currentFont;
        private float nextScanTime;
        private float nextPruneTime;
        private float nextCacheRefreshTime;
        private CancellationTokenSource reapplyCts;

        [Inject]
        public LocalizedFontDriver(IFontService fontService)
        {
            this.fontService = fontService;
        }

        /// <inheritdoc/>
        public void Start()
        {
            subscription = fontService.CurrentFont.Subscribe(OnFontChanged);
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            ApplyCurrentFont(forceRefreshCache: true);
        }

        /// <inheritdoc/>
        public void Tick()
        {
            if (currentFont == null)
            {
                return;
            }

            float now = Time.unscaledTime;
            if (now >= nextPruneTime)
            {
                nextPruneTime = now + PruneIntervalSeconds;
                LocalizedFont.PruneDestroyedDesignStyles();
            }

            if (now < nextScanTime)
            {
                return;
            }

            nextScanTime = now + ScanIntervalSeconds;
            bool forceRefresh = now >= nextCacheRefreshTime;
            if (forceRefresh)
            {
                nextCacheRefreshTime = now + CacheRefreshIntervalSeconds;
            }

            LocalizedFont.ApplyToAllLoaded(forceRefresh);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            subscription?.Dispose();
            subscription = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            CancelReapply();
            currentFont = null;
        }

        private void OnFontChanged(TMP_FontAsset fontAsset)
        {
            currentFont = fontAsset;
            if (fontAsset == null)
            {
                return;
            }

            TMP_Settings.defaultFontAsset = fontAsset;
            LocalizedFont.NotifySceneHierarchyChanged();
            ApplyCurrentFont(forceRefreshCache: true);
            ScheduleReapplyAfterLhFontWipe();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            LocalizedFont.NotifySceneHierarchyChanged();
            ApplyCurrentFont(forceRefreshCache: true);
        }

        private void OnSceneUnloaded(Scene scene)
        {
            LocalizedFont.NotifySceneHierarchyChanged();
            LanguageAwareUi.NotifyHierarchyChanged();
            nextScanTime = 0f;
            nextPruneTime = 0f;
            nextCacheRefreshTime = 0f;
        }

        private void ApplyCurrentFont(bool forceRefreshCache)
        {
            if (currentFont == null)
            {
                currentFont = fontService.CurrentFont.CurrentValue;
            }

            if (currentFont == null)
            {
                return;
            }

            TMP_Settings.defaultFontAsset = currentFont;
            LocalizedFont.ApplyToAllLoaded(forceRefreshCache);
            ScheduleReapplyAfterLhFontWipe();
            float now = Time.unscaledTime;
            nextScanTime = now + ScanIntervalSeconds;
            nextCacheRefreshTime = now + CacheRefreshIntervalSeconds;
        }

        private void ScheduleReapplyAfterLhFontWipe()
        {
            CancelReapply();
            reapplyCts = new CancellationTokenSource();
            ReapplyAfterLhFontWipeAsync(reapplyCts.Token).Forget();
        }

        private void CancelReapply()
        {
            if (reapplyCts == null)
            {
                return;
            }

            reapplyCts.Cancel();
            reapplyCts.Dispose();
            reapplyCts = null;
        }

        private async UniTaskVoid ReapplyAfterLhFontWipeAsync(CancellationToken cancellationToken)
        {
            // LHTextMeshProのCurrentFont購読がLocalizedFontDriverより後に走ることがある
            // 同一フレーム末と数フレーム後に再適用して潰した輪郭Faceを戻す
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);
            if (cancellationToken.IsCancellationRequested || currentFont == null)
            {
                return;
            }

            LocalizedFont.ApplyToAllLoaded(forceRefreshCache: false);

            await UniTask.DelayFrame(2, cancellationToken: cancellationToken);
            if (cancellationToken.IsCancellationRequested || currentFont == null)
            {
                return;
            }

            LocalizedFont.ApplyToAllLoaded(forceRefreshCache: false);
        }
    }
}
