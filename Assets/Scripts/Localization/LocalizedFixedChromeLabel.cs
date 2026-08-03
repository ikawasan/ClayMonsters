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
        /// 固定SizeDeltaの横ラベルへ全文を載せ右隣UIへ寄せる
        /// 省略記号は使わず必要なら左へはみ出して全文を読めるようにする
        /// RectTransformは変更しない
        /// </summary>
        /// <param name="text">対象TMP</param>
        /// <param name="key">文言キー</param>
        /// <param name="japaneseFallback">プレハブ原文の日本語</param>
        public static void ApplyFixedRectLabel(TMP_Text text, string key, string japaneseFallback)
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
            // 枠内で右寄せし短い翻訳でも右隣ゲージとの間隔を保つ
            text.alignment = TextAlignmentOptions.MidlineRight;
            if (text.isActiveAndEnabled)
            {
                text.ForceMeshUpdate(true);
            }

            // 親がHLGなら幅も合わせて後ろの要素を追従させる
            RefreshHorizontalLayoutWidth(text);
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
    }
}
