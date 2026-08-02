using System;
using LighthouseExtends.Language;
using UnityEngine;

namespace Localization
{
    /// <summary>
    /// PCのシステム言語を対応言語へ解決する
    /// </summary>
    public static class SystemLanguageResolver
    {
        /// <summary>
        /// システム言語から対応言語コードを返す
        /// 未対応時は英語
        /// </summary>
        /// <param name="supportedLanguageService">対応言語一覧</param>
        /// <returns>言語コード</returns>
        public static string Resolve(ISupportedLanguageService supportedLanguageService)
        {
            string mapped = MapSystemLanguage(Application.systemLanguage);
            if (IsSupported(mapped, supportedLanguageService))
            {
                return mapped;
            }

            string fallback = supportedLanguageService != null
                && !string.IsNullOrEmpty(supportedLanguageService.DefaultLanguage)
                ? supportedLanguageService.DefaultLanguage
                : GameLanguageCodes.Default;

            if (IsSupported(fallback, supportedLanguageService))
            {
                return fallback;
            }

            return GameLanguageCodes.Default;
        }

        /// <summary>
        /// 保存済みコードを検証し無効なら英語にする
        /// </summary>
        /// <param name="languageCode">保存コード</param>
        /// <param name="supportedLanguageService">対応言語一覧</param>
        /// <returns>有効な言語コード</returns>
        public static string Normalize(
            string languageCode,
            ISupportedLanguageService supportedLanguageService)
        {
            if (IsSupported(languageCode, supportedLanguageService))
            {
                return languageCode;
            }

            return supportedLanguageService != null
                && !string.IsNullOrEmpty(supportedLanguageService.DefaultLanguage)
                ? supportedLanguageService.DefaultLanguage
                : GameLanguageCodes.Default;
        }

        private static string MapSystemLanguage(SystemLanguage systemLanguage)
        {
            switch (systemLanguage)
            {
                case SystemLanguage.Japanese:
                    return GameLanguageCodes.Japanese;
                case SystemLanguage.English:
                    return GameLanguageCodes.English;
                case SystemLanguage.ChineseSimplified:
                    return GameLanguageCodes.ChineseSimplified;
                case SystemLanguage.ChineseTraditional:
                    return GameLanguageCodes.ChineseTraditional;
                case SystemLanguage.Chinese:
                    // OSが一般のChineseを返す場合は簡体字にする
                    return GameLanguageCodes.ChineseSimplified;
                case SystemLanguage.Korean:
                    return GameLanguageCodes.Korean;
                case SystemLanguage.German:
                    return GameLanguageCodes.German;
                case SystemLanguage.French:
                    return GameLanguageCodes.French;
                case SystemLanguage.Spanish:
                    return GameLanguageCodes.Spanish;
                case SystemLanguage.Portuguese:
                    // UnityのSystemLanguageは地域区別がないためpt-BRへ寄せる
                    return GameLanguageCodes.PortugueseBrazil;
                default:
                    return GameLanguageCodes.Default;
            }
        }

        private static bool IsSupported(
            string languageCode,
            ISupportedLanguageService supportedLanguageService)
        {
            if (string.IsNullOrEmpty(languageCode))
            {
                return false;
            }

            if (supportedLanguageService?.SupportedLanguages == null)
            {
                return Array.IndexOf(GameLanguageCodes.All, languageCode) >= 0;
            }

            for (int i = 0; i < supportedLanguageService.SupportedLanguages.Count; i++)
            {
                if (string.Equals(
                        supportedLanguageService.SupportedLanguages[i],
                        languageCode,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
