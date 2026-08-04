using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Localization
{
    /// <summary>
    /// 部位ラベルなどアイコン横並びの文言幅を現在文字に合わせる
    /// 言語切替後にHorizontalLayoutGroupが再計算されず余白が残るのを防ぐ
    /// 文面は常に全文表示する(省略記号は使わない)
    /// </summary>
    public static class LocalizedFixedChromeLabel
    {
        private const float WidthEpsilon = 1f;
        private const float FixedBesideGap = 4f;
        private const float FixedFallbackMaxWidth = 48f;
        private const float AutoSizeMinScale = 0.55f;
        private const float AutoSizeMinFontSize = 10f;

        /// <summary>
        /// ラベルへ文言を載せ現在幅でLayoutElementを更新する
        /// HorizontalLayoutGroup配下のLayoutElementがある場合のみ幅追従する
        /// </summary>
        /// <param name="text">対象TMP</param>
        /// <param name="key">文言キー</param>
        /// <param name="japaneseFallback">プレハブ原文の日本語</param>
        public static void Apply(TMP_Text text, string key, string japaneseFallback)
        {
            if (text == null)
            {
                return;
            }

            LocalizedFont.SetText(
                text,
                LocalizedText.GetOrFallback(key, japaneseFallback));
            RefreshHorizontalLayoutWidth(text);
        }

        /// <summary>
        /// 固定配置ラベルへローカライズ文言を載せ右隣UIと重ならないよう収める
        /// HLG配下では幅追従し固定配置では表示領域を隣要素手前までにして必要なら縮小する
        /// RectTransformは変更しない
        /// </summary>
        /// <param name="text">対象TMP</param>
        /// <param name="key">文言キー</param>
        /// <param name="japaneseFallback">プレハブ原文の日本語</param>
        public static void ApplyFixedRectLabel(TMP_Text text, string key, string japaneseFallback)
        {
            ApplyFixedRectLabel(text, key, japaneseFallback, null);
        }

        /// <summary>
        /// 固定配置ラベルへローカライズ文言を載せ右隣UIと重ならないよう収める
        /// </summary>
        /// <param name="text">対象TMP</param>
        /// <param name="key">文言キー</param>
        /// <param name="japaneseFallback">プレハブ原文の日本語</param>
        /// <param name="companion">右隣の表示物(RangeColumnやゲージ根など)</param>
        public static void ApplyFixedRectLabel(
            TMP_Text text,
            string key,
            string japaneseFallback,
            RectTransform companion)
        {
            if (text == null)
            {
                return;
            }

            LocalizedFont.SetText(
                text,
                LocalizedText.GetOrFallback(key, japaneseFallback));
            text.enableWordWrapping = false;
            // 全文を残す省略記号は使わない
            text.overflowMode = TextOverflowModes.Overflow;
            // 左寄せで隣UI側へのはみ出しを減らす
            text.alignment = TextAlignmentOptions.MidlineLeft;

            if (IsHorizontalLayoutLabel(text))
            {
                ResetFixedLayoutClamp(text);
                RefreshHorizontalLayoutWidth(text);
                return;
            }

            ApplyFixedBesideCompanion(text, companion);
        }

        /// <summary>
        /// 既に設定済み文言のpreferred幅を現在文字に合わせて更新する
        /// </summary>
        /// <param name="text">対象TMP</param>
        /// <param name="japaneseFallback">互換のため残す未使用</param>
        public static void StabilizeToFallbackWidth(TMP_Text text, string japaneseFallback)
        {
            _ = japaneseFallback;
            RefreshHorizontalLayoutWidth(text);
        }

        /// <summary>
        /// HorizontalLayoutGroup配下の文言幅を現在の表示幅へ合わせる
        /// </summary>
        /// <param name="text">対象TMP</param>
        public static void RefreshHorizontalLayoutWidth(TMP_Text text)
        {
            if (text == null)
            {
                return;
            }

            LayoutElement layout = text.GetComponent<LayoutElement>();
            if (layout == null)
            {
                return;
            }

            Transform parent = text.transform.parent;
            if (parent == null || parent.GetComponent<HorizontalLayoutGroup>() == null)
            {
                return;
            }

            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            if (text.isActiveAndEnabled)
            {
                text.ForceMeshUpdate(true);
            }

            string current = text.text ?? string.Empty;
            float width = text.GetPreferredValues(current).x + WidthEpsilon;
            if (width < 1f)
            {
                width = 1f;
            }

            // LayoutElementのsetterが親HLGをdirtyにするのでアイコン位置が追従する
            layout.minWidth = -1f;
            layout.preferredWidth = width;
            layout.flexibleWidth = 0f;
        }

        private static bool IsHorizontalLayoutLabel(TMP_Text text)
        {
            if (text.GetComponent<LayoutElement>() == null)
            {
                return false;
            }

            Transform parent = text.transform.parent;
            return parent != null && parent.GetComponent<HorizontalLayoutGroup>() != null;
        }

        private static void ResetFixedLayoutClamp(TMP_Text text)
        {
            text.enableAutoSizing = false;
            text.margin = Vector4.zero;
        }

        private static void ApplyFixedBesideCompanion(TMP_Text text, RectTransform companion)
        {
            RectTransform labelRect = text.rectTransform;
            RectTransform sibling = ResolveCompanionSibling(labelRect, companion);
            float rightMargin = 0f;
            float availableWidth = labelRect.rect.width;

            if (sibling != null && availableWidth > 1f)
            {
                float labelLeft = GetLocalLeft(labelRect);
                float companionLeft = GetLocalLeft(sibling);
                float maxWidth = companionLeft - FixedBesideGap - labelLeft;
                if (maxWidth < 8f)
                {
                    maxWidth = 8f;
                }

                if (maxWidth < availableWidth)
                {
                    rightMargin = availableWidth - maxWidth;
                    availableWidth = maxWidth;
                }
            }
            else if (availableWidth > FixedFallbackMaxWidth)
            {
                // ゲージ未配線時でも右隣想定帯へ被らないよう上限を掛ける
                rightMargin = availableWidth - FixedFallbackMaxWidth;
                availableWidth = FixedFallbackMaxWidth;
            }

            text.margin = new Vector4(0f, 0f, rightMargin, 0f);

            float baseSize = ResolveBaseFontSize(text);
            float preferred = text.GetPreferredValues(text.text ?? string.Empty, 10000f, 0f).x;
            if (preferred <= availableWidth + WidthEpsilon)
            {
                text.enableAutoSizing = false;
                text.fontSize = baseSize;
            }
            else
            {
                text.enableAutoSizing = true;
                text.fontSizeMax = baseSize;
                text.fontSizeMin = Mathf.Max(AutoSizeMinFontSize, baseSize * AutoSizeMinScale);
                text.fontSize = baseSize;
            }

            if (text.isActiveAndEnabled)
            {
                text.ForceMeshUpdate(true);
            }
        }

        private static float ResolveBaseFontSize(TMP_Text text)
        {
            if (text.enableAutoSizing && text.fontSizeMax > 0f)
            {
                return text.fontSizeMax;
            }

            if (text.fontSize > 0f)
            {
                return text.fontSize;
            }

            return 20f;
        }

        private static RectTransform ResolveCompanionSibling(
            RectTransform labelRect,
            RectTransform companion)
        {
            if (labelRect == null || companion == null)
            {
                return null;
            }

            Transform parent = labelRect.parent;
            if (parent == null)
            {
                return null;
            }

            Transform current = companion;
            while (current != null)
            {
                if (current.parent == parent)
                {
                    return current as RectTransform;
                }

                current = current.parent;
            }

            return null;
        }

        private static float GetLocalLeft(RectTransform rect)
        {
            if (rect == null)
            {
                return 0f;
            }

            // 同一親ローカルX上の左端
            float pivotOffset = rect.rect.width * rect.pivot.x;
            return rect.anchoredPosition.x - pivotOffset;
        }
    }
}
