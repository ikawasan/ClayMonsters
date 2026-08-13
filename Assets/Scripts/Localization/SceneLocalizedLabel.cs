using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Localization
{
    /// <summary>
    /// シーンプレハブ配置の日本語TMPをローカライズ原文として採取する
    /// Localizationアセンブリ内に閉じるため外部UI型やExtensionsに依存しない
    /// </summary>
    public static class SceneLocalizedLabel
    {
        /// <summary>
        /// TMP配置文言を原文にする空ならフォールバックを使う
        /// </summary>
        /// <param name="text">対象TMP</param>
        /// <param name="fallback">配置が空のときの原文</param>
        /// <returns>日本語原文</returns>
        public static string Capture(TMP_Text text, string fallback)
        {
            if (text != null)
            {
                string current = text.text;
                if (!string.IsNullOrWhiteSpace(current))
                {
                    return current.Trim();
                }
            }

            return fallback ?? string.Empty;
        }

        /// <summary>
        /// Toggle配下ラベルTMPの配置文言を原文にする
        /// </summary>
        /// <param name="toggle">対象トグル</param>
        /// <param name="fallback">配置が空のときの原文</param>
        /// <returns>日本語原文</returns>
        public static string Capture(Toggle toggle, string fallback)
        {
            return Capture(ResolveLabelText(toggle), fallback);
        }

        /// <summary>
        /// コンポーネント配下TMPの配置文言を原文にする
        /// LHButton等はComponent経由で受け取る
        /// </summary>
        /// <param name="component">対象コンポーネント</param>
        /// <param name="fallback">配置が空のときの原文</param>
        /// <returns>日本語原文</returns>
        public static string Capture(Component component, string fallback)
        {
            return Capture(ResolveLabelText(component), fallback);
        }

        /// <summary>
        /// キーとシーン原文で現在言語の文言を返す
        /// </summary>
        /// <param name="key">テキストキー</param>
        /// <param name="sceneOriginal">シーン配置原文</param>
        /// <returns>表示文言</returns>
        public static string Resolve(string key, string sceneOriginal)
        {
            return LocalizedText.GetOrFallback(key, sceneOriginal ?? string.Empty);
        }

        /// <summary>
        /// パラメータ付きキーとシーン原文テンプレートで文言を返す
        /// </summary>
        /// <param name="key">テキストキー</param>
        /// <param name="sceneOriginalTemplate">シーン配置原文テンプレート</param>
        /// <param name="paramName">パラメータ名</param>
        /// <param name="paramValue">パラメータ値</param>
        /// <returns>表示文言</returns>
        public static string Resolve(
            string key,
            string sceneOriginalTemplate,
            string paramName,
            object paramValue)
        {
            return LocalizedText.GetOrFallback(
                key,
                sceneOriginalTemplate ?? string.Empty,
                paramName,
                paramValue);
        }

        private static TMP_Text ResolveLabelText(Component component)
        {
            if (component == null)
            {
                return null;
            }

            if (component is Toggle toggle)
            {
                return ResolveToggleLabelText(toggle);
            }

            if (component is TMP_Text tmpText)
            {
                return tmpText;
            }

            return component.GetComponentInChildren<TMP_Text>(true);
        }

        private static TMP_Text ResolveToggleLabelText(Toggle toggle)
        {
            TMP_Text[] texts = toggle.GetComponentsInChildren<TMP_Text>(true);
            if (texts == null || texts.Length == 0)
            {
                return null;
            }

            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (text == null)
                {
                    continue;
                }

                string objectName = text.gameObject.name;
                if (objectName == "ToggleLabel"
                    || objectName == "Label"
                    || objectName == "Text"
                    || objectName == "LabelText")
                {
                    return text;
                }
            }

            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (text == null || IsDecorativeToggleText(text))
                {
                    continue;
                }

                return text;
            }

            return null;
        }

        private static bool IsDecorativeToggleText(TMP_Text text)
        {
            string objectName = text.gameObject.name;
            if (objectName == "Chevron"
                || objectName == "Checkmark"
                || objectName == "Item Checkmark")
            {
                return true;
            }

            Transform current = text.transform;
            while (current != null)
            {
                string name = current.name;
                if (name == "Checkmark" || name == "GuideVisibilityToggle")
                {
                    return true;
                }

                current = current.parent;
            }

            string value = text.text != null ? text.text.Trim() : string.Empty;
            return value.Length <= 1;
        }
    }
}
