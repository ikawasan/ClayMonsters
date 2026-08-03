using LighthouseExtends.Font;
using TMPro;
using UnityEngine;

namespace Localization
{
    /// <summary>
    /// 現在言語のTMPFontAssetをプレーンTMPへ適用する
    /// LHTextMeshPro以外はFontServiceを購読しないためここで補う
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
        /// TMPへ現在言語フォントがあれば適用する
        /// シーン直置きのTextMeshProUGUIも含む
        /// </summary>
        /// <param name="text">対象TMP</param>
        public static void Apply(TMP_Text text)
        {
            if (text == null)
            {
                return;
            }

            TMP_FontAsset font = CurrentOrNull;
            if (font == null)
            {
                return;
            }

            ApplyFont(text, font);
        }

        /// <summary>
        /// ロード済みシーンの全TMPへ現在言語フォントを適用する
        /// </summary>
        public static void ApplyToAllLoaded()
        {
            TMP_FontAsset font = CurrentOrNull;
            if (font == null)
            {
                return;
            }

            TMP_Text[] texts = UnityEngine.Object.FindObjectsByType<TMP_Text>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < texts.Length; i++)
            {
                ApplyFont(texts[i], font);
            }
        }

        /// <summary>
        /// 指定フォントをTMPへ適用するマテリアルも合わせる
        /// </summary>
        /// <param name="text">対象TMP</param>
        /// <param name="fontAsset">適用フォント</param>
        public static void ApplyFont(TMP_Text text, TMP_FontAsset fontAsset)
        {
            if (text == null || fontAsset == null)
            {
                return;
            }

            // エディタ生成のみのアセットインスタンスには触れない
            if (text.gameObject.scene.IsValid() == false)
            {
                return;
            }

            bool fontChanged = text.font != fontAsset;
            if (fontChanged)
            {
                text.font = fontAsset;
            }

            // 共有マテリアルが旧フォントのままだと字形が欠けるため合わせる
            // アウトライン用インスタンス材質はfontMaterial経由で後から再生成される
            Material desired = fontAsset.material;
            if (desired != null
                && (text.fontSharedMaterial == null
                    || !IsSameBaseMaterial(text.fontSharedMaterial, desired)))
            {
                text.fontSharedMaterial = desired;
                fontChanged = true;
            }

            if (fontChanged && text.isActiveAndEnabled)
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

        private static bool IsSameBaseMaterial(Material current, Material desired)
        {
            if (current == null || desired == null)
            {
                return false;
            }

            if (current == desired)
            {
                return true;
            }

            // インスタンス化されたマテリアルは名前にInstanceが付く
            // ソースが同じならアトラス一致とみなす
            if (current.shader != desired.shader)
            {
                return false;
            }

            Texture main = current.HasProperty(ShaderUtilities.ID_MainTex)
                ? current.GetTexture(ShaderUtilities.ID_MainTex)
                : null;
            Texture desiredMain = desired.HasProperty(ShaderUtilities.ID_MainTex)
                ? desired.GetTexture(ShaderUtilities.ID_MainTex)
                : null;
            return main != null && main == desiredMain;
        }
    }
}
