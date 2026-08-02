using System.Collections.Generic;

namespace Localization
{
    /// <summary>
    /// 言語選択UI向けの各言語の固有名称
    /// </summary>
    public static class LanguageDisplayNames
    {
        private static readonly IReadOnlyDictionary<string, string> Names =
            new Dictionary<string, string>
            {
                { GameLanguageCodes.Japanese, "日本語" },
                { GameLanguageCodes.English, "English" },
                { GameLanguageCodes.ChineseSimplified, "简体中文" },
                { GameLanguageCodes.ChineseTraditional, "繁體中文" },
                { GameLanguageCodes.Korean, "한국어" },
                { GameLanguageCodes.German, "Deutsch" },
                { GameLanguageCodes.French, "Français" },
                { GameLanguageCodes.Spanish, "Español" },
                { GameLanguageCodes.PortugueseBrazil, "Português (Brasil)" },
            };

        /// <summary>
        /// 言語コードに対応する表示名を返す
        /// </summary>
        /// <param name="languageCode">言語コード</param>
        /// <returns>表示名</returns>
        public static string Get(string languageCode)
        {
            if (string.IsNullOrEmpty(languageCode))
            {
                return languageCode ?? string.Empty;
            }

            return Names.TryGetValue(languageCode, out string name)
                ? name
                : languageCode;
        }
    }
}
