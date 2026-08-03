using Localization;
using TMPro;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Extensions
{
    /// <summary>
    /// プロジェクト標準のTMPフォントを提供する
    /// 実行時は現在言語のFontServiceを優先し無いときLightNovelPOPv2 SDFを使う
    /// アウトラインはApplyOutlineを明示したときだけ付ける
    /// </summary>
    public static class AppTmpFontUtility
    {
        /// <summary>
        /// 標準フォントアセットのプロジェクト内パス
        /// </summary>
        public const string FontAssetPath = "Assets/TextMesh Pro/Fonts/LightNovelPOPv2 SDF.asset";

        /// <summary>
        /// アウトライン用マテリアルプリセットのプロジェクト内パス
        /// Inspectorで個別に割り当てる用途
        /// </summary>
        public const string OutlineMaterialPath = "Assets/TextMesh Pro/Fonts/LightNovelPOPv2 SDF - Outline.mat";

        private static TMP_FontAsset cachedDefaultFont;

        /// <summary>
        /// 標準フォントアセットを取得する
        /// </summary>
        public static TMP_FontAsset DefaultFont
        {
            get
            {
                if (Application.isPlaying)
                {
                    TMP_FontAsset localized = LocalizedFont.CurrentOrNull;
                    if (localized != null)
                    {
                        return localized;
                    }
                }

                if (cachedDefaultFont != null)
                {
                    return cachedDefaultFont;
                }

#if UNITY_EDITOR
                cachedDefaultFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
#endif
                if (cachedDefaultFont == null)
                {
                    cachedDefaultFont = TMP_Settings.defaultFontAsset;
                }

                return cachedDefaultFont;
            }
        }

        /// <summary>
        /// TMP_Textへ標準フォントを適用する
        /// アウトラインは変更しない
        /// </summary>
        /// <param name="text">適用対象</param>
        public static void ApplyDefaultFont(TMP_Text text)
        {
            if (text == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                LocalizedFont.Apply(text);
                if (text.font != null)
                {
                    return;
                }
            }

            TMP_FontAsset font = DefaultFont;
            if (font == null)
            {
                return;
            }

            LocalizedFont.ApplyFont(text, font);
        }

        /// <summary>
        /// TMP_Textへ縁取りを適用する
        /// 必要な箇所だけで明示的に呼ぶ
        /// </summary>
        /// <param name="text">適用対象</param>
        /// <param name="width">縁取り幅</param>
        /// <param name="color">縁取り色(省略時は現状色を維持)</param>
        public static void ApplyOutline(TMP_Text text, float width, Color? color = null)
        {
            if (text == null)
            {
                return;
            }

            EnsureFontAssigned(text);

            if (text.font == null)
            {
                return;
            }

            if (text.fontSharedMaterial == null)
            {
                text.fontSharedMaterial = text.font.material;
            }

            if (text.fontSharedMaterial == null)
            {
                return;
            }

            // fontMaterialでインスタンス化し共有マテリアルを汚さない
            text.fontSharedMaterial = text.fontMaterial;
            text.outlineWidth = width;
            if (color.HasValue)
            {
                text.outlineColor = color.Value;
            }
        }

        private static void EnsureFontAssigned(TMP_Text text)
        {
            if (text.font != null)
            {
                return;
            }

            ApplyDefaultFont(text);
        }
    }
}
