using System.Collections.Generic;
using LighthouseExtends.TextTable;

namespace Localization
{
    /// <summary>
    /// TextTableから文言を取得するヘルパー
    /// </summary>
    public static class LocalizedText
    {
        /// <summary>
        /// キーに対応する文言を返す
        /// </summary>
        /// <param name="key">テキストキー</param>
        /// <returns>文言</returns>
        public static string Get(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }

            ITextTableService service = TextTableService.Instance;
            if (service == null)
            {
                return key;
            }

            return service.GetText(new TextData(key));
        }

        /// <summary>
        /// キーが無いときフォールバックを返す
        /// 日本語時は原文フォールバックを優先する
        /// </summary>
        /// <param name="key">テキストキー</param>
        /// <param name="fallback">フォールバック文言(日本語原文)</param>
        /// <returns>文言</returns>
        public static string GetOrFallback(string key, string fallback)
        {
            if (!string.IsNullOrEmpty(fallback) && IsJapanese())
            {
                return fallback;
            }

            if (string.IsNullOrEmpty(key))
            {
                return fallback ?? string.Empty;
            }

            ITextTableService service = TextTableService.Instance;
            if (service == null)
            {
                return fallback ?? key;
            }

            string text = service.GetText(new TextData(key));
            if (string.IsNullOrEmpty(text) || text == key)
            {
                return fallback ?? key;
            }

            return text;
        }

        /// <summary>
        /// パラメータ付きキーの文言を返す
        /// </summary>
        /// <param name="key">テキストキー</param>
        /// <param name="parameters">置換パラメータ</param>
        /// <returns>文言</returns>
        public static string Get(string key, IReadOnlyDictionary<string, object> parameters)
        {
            if (string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }

            ITextTableService service = TextTableService.Instance;
            if (service == null)
            {
                return key;
            }

            return service.GetText(new TextData(key, parameters));
        }

        /// <summary>
        /// 単一パラメータ付きキーの文言を返す
        /// </summary>
        /// <param name="key">テキストキー</param>
        /// <param name="paramName">パラメータ名</param>
        /// <param name="paramValue">パラメータ値</param>
        /// <returns>文言</returns>
        public static string Get(string key, string paramName, object paramValue)
        {
            return Get(
                key,
                new Dictionary<string, object>
                {
                    { paramName, paramValue },
                });
        }

        /// <summary>
        /// 原文フォールバック優先のパラメータ付き文言を返す
        /// 日本語時はテンプレート原文を使い置換する
        /// </summary>
        /// <param name="key">テキストキー</param>
        /// <param name="fallbackTemplate">日本語原文テンプレート</param>
        /// <param name="parameters">置換パラメータ</param>
        /// <returns>文言</returns>
        public static string GetOrFallback(
            string key,
            string fallbackTemplate,
            IReadOnlyDictionary<string, object> parameters)
        {
            if (!string.IsNullOrEmpty(fallbackTemplate) && IsJapanese())
            {
                return ApplyParameters(fallbackTemplate, parameters);
            }

            if (string.IsNullOrEmpty(key))
            {
                return ApplyParameters(fallbackTemplate, parameters);
            }

            ITextTableService service = TextTableService.Instance;
            if (service == null)
            {
                return ApplyParameters(fallbackTemplate ?? key, parameters);
            }

            string text = service.GetText(new TextData(key, parameters));
            if (string.IsNullOrEmpty(text) || text == key)
            {
                return ApplyParameters(fallbackTemplate ?? key, parameters);
            }

            return text;
        }

        /// <summary>
        /// 原文フォールバック優先の単一パラメータ文言を返す
        /// </summary>
        /// <param name="key">テキストキー</param>
        /// <param name="fallbackTemplate">日本語原文テンプレート</param>
        /// <param name="paramName">パラメータ名</param>
        /// <param name="paramValue">パラメータ値</param>
        /// <returns>文言</returns>
        public static string GetOrFallback(
            string key,
            string fallbackTemplate,
            string paramName,
            object paramValue)
        {
            return GetOrFallback(
                key,
                fallbackTemplate,
                new Dictionary<string, object>
                {
                    { paramName, paramValue },
                });
        }

        /// <summary>
        /// string.Formatの{0}形式説明文をローカライズして埋める
        /// 日本語時は原文フォーマットを優先する
        /// </summary>
        /// <param name="key">テキストキー</param>
        /// <param name="fallbackFormat">フォールバック書式</param>
        /// <param name="arg0">第0引数</param>
        /// <returns>文言</returns>
        public static string FormatOrdinal(string key, string fallbackFormat, object arg0)
        {
            string format = GetOrFallback(key, fallbackFormat);
            try
            {
                return string.Format(format, arg0);
            }
            catch
            {
                return format;
            }
        }

        /// <summary>
        /// 現在言語が日本語かどうかを返す
        /// </summary>
        /// <returns>日本語ならtrue</returns>
        public static bool IsJapanese()
        {
            ITextTableService service = TextTableService.Instance;
            if (service == null)
            {
                return true;
            }

            string code = service.CurrentLanguage.CurrentValue;
            if (string.IsNullOrEmpty(code))
            {
                return true;
            }

            return code == GameLanguageCodes.Japanese
                || code.StartsWith("ja", System.StringComparison.OrdinalIgnoreCase);
        }

        private static string ApplyParameters(
            string template,
            IReadOnlyDictionary<string, object> parameters)
        {
            if (string.IsNullOrEmpty(template))
            {
                return string.Empty;
            }

            if (parameters == null || parameters.Count == 0)
            {
                return template;
            }

            string result = template;
            foreach (KeyValuePair<string, object> pair in parameters)
            {
                string token = "{" + pair.Key + "}";
                string value = pair.Value?.ToString() ?? string.Empty;
                result = result.Replace(token, value);
            }

            return result;
        }
    }
}
