using System.Threading;
using Cysharp.Threading.Tasks;
using LighthouseExtends.Font;
using LighthouseExtends.Language;
using LighthouseExtends.TextTable;
using UnityEngine;
using VContainer;

namespace Localization
{
    /// <summary>
    /// 初回はOS言語未対応なら英語で開始し言語設定を保存する
    /// </summary>
    public sealed class LanguageInitializer : ILanguageInitializer
    {
        private readonly ILanguageService languageService;
        private readonly ISupportedLanguageService supportedLanguageService;
        private readonly ILanguageOptionStore languageOptionStore;

        // TextTableとFontの生成を起動時に強制しSetLanguage前にハンドラを登録する
        private readonly ITextTableService textTableService;
        private readonly IFontService fontService;

        private string currentLanguageCode = GameLanguageCodes.Default;

        [Inject]
        public LanguageInitializer(
            ILanguageService languageService,
            ISupportedLanguageService supportedLanguageService,
            ILanguageOptionStore languageOptionStore,
            ITextTableService textTableService,
            IFontService fontService)
        {
            this.languageService = languageService;
            this.supportedLanguageService = supportedLanguageService;
            this.languageOptionStore = languageOptionStore;
            this.textTableService = textTableService;
            this.fontService = fontService;
        }

        /// <inheritdoc/>
        public string CurrentLanguageCode => currentLanguageCode;

        /// <inheritdoc/>
        public async UniTask InitializeAsync(CancellationToken cancellationToken)
        {
            // 依存注入の副作用でTextTableService/FontServiceコンストラクタを実行済みにする
            _ = textTableService;
            _ = fontService;

            string savedCode = languageOptionStore.LoadLanguageCode() ?? string.Empty;

            string languageCode;
            if (string.IsNullOrEmpty(savedCode))
            {
                languageCode = SystemLanguageResolver.Resolve(supportedLanguageService);
                languageOptionStore.SaveLanguageCode(languageCode);
                Debug.Log(
                    $"[LanguageInitializer] 初回起動のためシステム言語を適用 language={languageCode} system={Application.systemLanguage}");
            }
            else
            {
                languageCode = SystemLanguageResolver.Normalize(
                    savedCode,
                    supportedLanguageService);
                if (!string.Equals(languageCode, savedCode, System.StringComparison.Ordinal))
                {
                    languageOptionStore.SaveLanguageCode(languageCode);
                    Debug.LogWarning(
                        $"[LanguageInitializer] 保存言語が未対応のため変更 saved={savedCode} language={languageCode}");
                }
            }

            await ApplyLanguageAsync(languageCode, cancellationToken);

            if (string.IsNullOrEmpty(textTableService.CurrentLanguage.CurrentValue))
            {
                Debug.LogError(
                    $"[LanguageInitializer] TextTableの読み込みに失敗しました language={languageCode}");
            }
        }

        /// <inheritdoc/>
        public async UniTask SetLanguageAsync(
            string languageCode,
            CancellationToken cancellationToken)
        {
            string normalized = SystemLanguageResolver.Normalize(
                languageCode,
                supportedLanguageService);
            languageOptionStore.SaveLanguageCode(normalized);
            await ApplyLanguageAsync(normalized, cancellationToken);
        }

        private async UniTask ApplyLanguageAsync(
            string languageCode,
            CancellationToken cancellationToken)
        {
            await languageService.SetLanguage(languageCode, cancellationToken);
            currentLanguageCode = languageCode;
        }
    }
}
