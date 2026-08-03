using System;
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
    /// </summary>
    public sealed class LocalizedFontDriver : IStartable, ITickable, IDisposable
    {
        private const float ScanIntervalSeconds = 0.25f;

        private readonly IFontService fontService;
        private IDisposable subscription;
        private TMP_FontAsset currentFont;
        private float nextScanTime;

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
            ApplyCurrentFont();
        }

        /// <inheritdoc/>
        public void Tick()
        {
            if (currentFont == null)
            {
                return;
            }

            if (Time.unscaledTime < nextScanTime)
            {
                return;
            }

            nextScanTime = Time.unscaledTime + ScanIntervalSeconds;
            LocalizedFont.ApplyToAllLoaded();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            subscription?.Dispose();
            subscription = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
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
            LocalizedFont.ApplyToAllLoaded();
            nextScanTime = Time.unscaledTime + ScanIntervalSeconds;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplyCurrentFont();
        }

        private void OnSceneUnloaded(Scene scene)
        {
            // 次Tickで再スキャンさせる
            nextScanTime = 0f;
        }

        private void ApplyCurrentFont()
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
            LocalizedFont.ApplyToAllLoaded();
            nextScanTime = Time.unscaledTime + ScanIntervalSeconds;
        }
    }
}
