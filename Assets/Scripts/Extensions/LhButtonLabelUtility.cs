using Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Extensions
{
    /// <summary>
    /// TMP_Textへのラベル設定ヘルパー
    /// シーン直置きボタン文言も現在言語フォントを付ける
    /// </summary>
    public static class LhButtonLabelUtility
    {
        /// <summary>
        /// ラベルテキストを設定する
        /// </summary>
        /// <param name="text">対象TMP_Text</param>
        /// <param name="label">表示テキスト</param>
        public static void SetLabel(TMP_Text text, string label)
        {
            if (text == null)
            {
                return;
            }

            LocalizedFont.SetText(text, label);
        }

        /// <summary>
        /// コンポーネント配下のTMPへラベルを設定する
        /// LHButtonはButton派生のためButtonとして渡せる
        /// </summary>
        /// <param name="component">対象コンポーネント</param>
        /// <param name="label">表示テキスト</param>
        public static void SetLabel(Component component, string label)
        {
            if (component == null)
            {
                return;
            }

            SetLabel(component.GetComponentInChildren<TMP_Text>(true), label);
        }

        /// <summary>
        /// Toggle配下のラベルTMPへ設定する
        /// Checkmark/Chevronなど装飾用TMPは避ける
        /// </summary>
        /// <param name="toggle">対象トグル</param>
        /// <param name="label">表示テキスト</param>
        public static void SetLabel(Toggle toggle, string label)
        {
            if (toggle == null)
            {
                return;
            }

            SetLabel(ResolveToggleLabelText(toggle), label);
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

        /// <summary>
        /// Unity Button配下のTMPへラベルを設定する
        /// </summary>
        /// <param name="button">対象ボタン</param>
        /// <param name="label">表示テキスト</param>
        public static void SetLabel(Button button, string label)
        {
            SetLabel((Component)button, label);
        }
    }
}
