using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Extensions
{
    /// <summary>
    /// プロジェクト標準のTMPフォントを提供する
    /// LightNovelPOPv2 SDFを適用する
    /// </summary>
    public static class AppTmpFontUtility
    {
        /// <summary>
        /// 標準フォントアセットのプロジェクト内パス
        /// </summary>
        public const string FontAssetPath = "Assets/TextMesh Pro/Fonts/LightNovelPOPv2 SDF.asset";

        private static TMP_FontAsset cachedDefaultFont;

        /// <summary>
        /// 標準フォントアセットを取得する
        /// </summary>
        public static TMP_FontAsset DefaultFont
        {
            get
            {
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
        /// </summary>
        /// <param name="text">適用対象</param>
        public static void ApplyDefaultFont(TMP_Text text)
        {
            if (text == null)
            {
                return;
            }

            if (text.font != null)
            {
                if (text.fontSharedMaterial == null)
                {
                    text.fontSharedMaterial = text.font.material;
                }

                return;
            }

            TMP_FontAsset font = DefaultFont;
            if (font != null)
            {
                text.font = font;
                if (text.fontSharedMaterial == null)
                {
                    text.fontSharedMaterial = font.material;
                }
            }
        }
    }
}
