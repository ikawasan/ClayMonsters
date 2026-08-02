using LighthouseExtends.Font;
using TMPro;
using UnityEngine;

namespace Localization
{
    /// <summary>
    /// 現在言語のTMPFontAssetをプレーンTMPに適用する
    /// LHTextMeshPro以外のUIはFontServiceを購読しないためここで補う
    /// </summary>
    public static class LocalizedFont
    {
        /// <summary>
        /// 現在言語のFontAssetを返す未初期化時はnull
        /// </summary>
        public static TMP_FontAsset CurrentOrNull
        {
            get
            {
                IFontService service = FontService.Instance;
                if (service == null)
                {
                    return null;
                }

                return service.CurrentFont.CurrentValue;
            }
        }

        /// <summary>
        /// TMPに現在言語フォントがあれば適用する
        /// </summary>
        /// <param name="text">対象TMP</param>
        public static void Apply(TMP_Text text)
        {
            if (text == null)
            {
                return;
            }

            TMP_FontAsset font = CurrentOrNull;
            if (font == null || text.font == font)
            {
                return;
            }

            if (text.gameObject.scene.IsValid() == false)
            {
                return;
            }

            text.font = font;
            if (text.isActiveAndEnabled)
            {
                text.ForceMeshUpdate(true);
            }
        }

        /// <summary>
        /// 文言設定と同時に現在言語フォントを適用する
        /// </summary>
        /// <param name="text">対象TMP</param>
        /// <param name="value">表示文言</param>
        public static void SetText(TMP_Text text, string value)
        {
            if (text == null)
            {
                return;
            }

            text.text = value ?? string.Empty;
            Apply(text);
        }
    }
}
