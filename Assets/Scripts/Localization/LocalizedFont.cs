using System.Collections.Generic;
using LighthouseExtends.Font;
using TMPro;
using UnityEngine;

namespace Localization
{
    /// <summary>
    /// 現在言語のTMPFontAssetをプレーンTMPへ適用する
    /// LHTextMeshPro以外はFontServiceを購読しないためここで補う
    /// 輪郭と色とBoldなど見た目のスタイルはフォント差替時に必ず引き継ぐ
    /// LHTextMeshProの素のfont代入で材質が消えた場合も定期適用で復旧する
    /// </summary>
    public static class LocalizedFont
    {
        private static readonly string[] VisualKeywords =
        {
            ShaderUtilities.Keyword_Outline,
            ShaderUtilities.Keyword_Underlay,
            ShaderUtilities.Keyword_Glow,
            ShaderUtilities.Keyword_Bevel,
        };

        // 実体ごとのデザイン時スタイル(輪郭Face等)
        // LHTextMeshProが後からfontを当てて潰してもここから復旧する
        private static readonly Dictionary<int, StyleSnapshot> DesignStyles = new();
        private static readonly List<int> DesignStylePruneScratch = new List<int>(256);
        private static readonly List<TMP_Text> LoadedTextsCache = new List<TMP_Text>(256);
        private static bool loadedTextsCacheDirty = true;

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
        /// シーン遷移時などに破棄済みスタイルキャッシュを捨てる
        /// </summary>
        public static void NotifySceneHierarchyChanged()
        {
            loadedTextsCacheDirty = true;
            PruneDestroyedDesignStyles();
        }

        /// <summary>
        /// 破棄されたTMPに紐づくデザインキャッシュを削除する
        /// </summary>
        public static void PruneDestroyedDesignStyles()
        {
            if (DesignStyles.Count == 0)
            {
                return;
            }

            DesignStylePruneScratch.Clear();
            foreach (KeyValuePair<int, StyleSnapshot> pair in DesignStyles)
            {
                if (Resources.InstanceIDToObject(pair.Key) == null)
                {
                    DesignStylePruneScratch.Add(pair.Key);
                }
            }

            for (int i = 0; i < DesignStylePruneScratch.Count; i++)
            {
                DesignStyles.Remove(DesignStylePruneScratch[i]);
            }

            DesignStylePruneScratch.Clear();
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
        /// 表示文字をDynamicアトラスへ先に載せる
        /// fallbackも含めて欠損字形の初回生成をまとめる
        /// </summary>
        /// <param name="text">載せる文字群</param>
        public static void WarmupCharacters(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            WarmupCharactersOnFont(CurrentOrNull, text);
        }

        /// <summary>
        /// ロード済みシーンの全TMPへ現在言語フォントを適用する
        /// </summary>
        public static void ApplyToAllLoaded()
        {
            ApplyToAllLoaded(forceRefreshCache: false);
        }

        /// <summary>
        /// ロード済みシーンの全TMPへ現在言語フォントを適用する
        /// </summary>
        /// <param name="forceRefreshCache">TMP一覧を再スキャンするか</param>
        public static void ApplyToAllLoaded(bool forceRefreshCache)
        {
            TMP_FontAsset font = CurrentOrNull;
            if (font == null)
            {
                return;
            }

            RefreshLoadedTextsCache(forceRefreshCache);

            int nullCount = 0;
            for (int i = 0; i < LoadedTextsCache.Count; i++)
            {
                TMP_Text text = LoadedTextsCache[i];
                if (text == null)
                {
                    nullCount++;
                    continue;
                }

                ApplyFont(text, font);
            }

            // 破棄が多ければ次回再スキャン
            if (nullCount > 8 || (LoadedTextsCache.Count > 0 && nullCount * 4 > LoadedTextsCache.Count))
            {
                loadedTextsCacheDirty = true;
            }
        }

        private static void RefreshLoadedTextsCache(bool force)
        {
            if (!force && !loadedTextsCacheDirty && LoadedTextsCache.Count > 0)
            {
                return;
            }

            LoadedTextsCache.Clear();
            TMP_Text[] texts = Object.FindObjectsByType<TMP_Text>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null)
                {
                    LoadedTextsCache.Add(texts[i]);
                }
            }

            loadedTextsCacheDirty = false;
        }

        /// <summary>
        /// 指定フォントをTMPへ適用する
        /// アトラス差し替え後も輪郭幅やFace色はデザイン時から保持する
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

            int instanceId = text.GetInstanceID();
            bool fontAlreadyCurrent = text.font == fontAsset;
            bool atlasAlreadyCorrect = UsesFontAtlas(text.fontSharedMaterial, fontAsset);

            // 既に正しいフォントアトラスかつデザイン一致なら材質読取を省略する
            if (fontAlreadyCurrent
                && atlasAlreadyCorrect
                && DesignStyles.TryGetValue(instanceId, out StyleSnapshot cachedDesign)
                && StyleMatches(text, cachedDesign))
            {
                return;
            }

            // font代入前の現状を保持する(動的なBold切替も尊重する)
            StyleSnapshot sampleBeforeFont = CaptureStyle(text);
            UpgradeDesign(instanceId, sampleBeforeFont);
            DesignStyles.TryGetValue(instanceId, out StyleSnapshot design);

            bool styleMatchesDesign = StyleMatches(text, design);

            // 定期スキャンでインスタンス材質を壊さないが潰されていれば復旧する
            // FontStyleは動的切替を尊重し差替前サンプルを優先する
            if (fontAlreadyCurrent && atlasAlreadyCorrect)
            {
                if (!styleMatchesDesign && design.HasMaterial)
                {
                    Color vertexColor = text.color;
                    EnsureStyleMaterial(text, fontAsset);
                    StyleSnapshot restore = design;
                    restore.FontStyle = sampleBeforeFont.FontStyle;
                    restore.FontWeight = sampleBeforeFont.FontWeight;
                    RestoreStyle(text, text.fontSharedMaterial, restore);
                    text.color = vertexColor;
                    if (text.isActiveAndEnabled)
                    {
                        text.ForceMeshUpdate(true);
                    }
                }

                return;
            }

            Color preservedVertexColor = text.color;
            StyleSnapshot styleToApply = MergeStyleForApply(design, sampleBeforeFont);
            // font差替で落ちる対策は差替前の現状のみを戻す
            // 設計Boldを常ORするとタブ非選択時のNormalも潰す
            styleToApply.FontStyle = sampleBeforeFont.FontStyle;
            styleToApply.FontWeight = sampleBeforeFont.FontWeight;

            if (!fontAlreadyCurrent)
            {
                text.font = fontAsset;
            }

            Material fontBase = fontAsset.material;
            if (fontBase == null)
            {
                // 材質が無くてもBoldなどTMP側スタイルは復元する
                RestoreFontStyle(text, styleToApply);
                text.color = preservedVertexColor;
                return;
            }

            text.fontSharedMaterial = fontBase;
            Material styleMaterial = text.fontMaterial;
            text.fontSharedMaterial = styleMaterial;
            RestoreStyle(text, styleMaterial, styleToApply);
            text.color = preservedVertexColor;

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

        private static void WarmupCharactersOnFont(TMP_FontAsset fontAsset, string text)
        {
            if (fontAsset == null || string.IsNullOrEmpty(text))
            {
                return;
            }

            fontAsset.TryAddCharacters(text, out _);

            List<TMP_FontAsset> fallbacks = fontAsset.fallbackFontAssetTable;
            if (fallbacks == null || fallbacks.Count == 0)
            {
                return;
            }

            for (int i = 0; i < fallbacks.Count; i++)
            {
                WarmupCharactersOnFont(fallbacks[i], text);
            }
        }

        private static void EnsureStyleMaterial(TMP_Text text, TMP_FontAsset fontAsset)
        {
            Material shared = text.fontSharedMaterial;
            Material fontBase = fontAsset.material;
            if (fontBase == null)
            {
                return;
            }

            // 共有アセットを汚さないように常にインスタンスへ寄せる
            if (shared == null || shared == fontBase || !shared.name.Contains("(Instance)"))
            {
                text.fontSharedMaterial = fontBase;
                text.fontSharedMaterial = text.fontMaterial;
            }
        }

        private static void UpgradeDesign(int instanceId, StyleSnapshot sample)
        {
            if (!DesignStyles.TryGetValue(instanceId, out StyleSnapshot design))
            {
                DesignStyles[instanceId] = sample;
                return;
            }

            DesignStyles[instanceId] = PreferRicher(design, sample);
        }

        private static StyleSnapshot PreferRicher(StyleSnapshot design, StyleSnapshot sample)
        {
            if (!design.HasMaterial && sample.HasMaterial)
            {
                // 材質はsample優先だが初回FontStyleはdesign側を維持
                FontStyles designFontStyle = design.FontStyle;
                FontWeight designFontWeight = design.FontWeight;
                sample.FontStyle = designFontStyle != FontStyles.Normal
                    ? designFontStyle
                    : sample.FontStyle;
                if (designFontWeight > sample.FontWeight)
                {
                    sample.FontWeight = designFontWeight;
                }

                if (design.OutlineWidth > sample.OutlineWidth + 0.0001f)
                {
                    sample.OutlineWidth = design.OutlineWidth;
                    sample.OutlineColor = design.OutlineColor;
                    sample.OutlineSoftness = design.OutlineSoftness;
                }

                return sample;
            }

            if (!sample.HasMaterial)
            {
                return design;
            }

            if (sample.OutlineWidth > design.OutlineWidth + 0.0001f)
            {
                design.OutlineWidth = sample.OutlineWidth;
                design.OutlineColor = sample.OutlineColor;
                design.OutlineSoftness = sample.OutlineSoftness;
            }

            if (IsNearWhite(design.FaceColor) && !IsNearWhite(sample.FaceColor))
            {
                design.FaceColor = sample.FaceColor;
            }

            if (sample.FaceDilate > design.FaceDilate)
            {
                design.FaceDilate = sample.FaceDilate;
            }

            if (sample.UnderlayDilate > design.UnderlayDilate
                || sample.UnderlaySoftness > design.UnderlaySoftness
                || sample.UnderlayColor.a > design.UnderlayColor.a)
            {
                design.UnderlayColor = sample.UnderlayColor;
                design.UnderlayOffsetX = sample.UnderlayOffsetX;
                design.UnderlayOffsetY = sample.UnderlayOffsetY;
                design.UnderlayDilate = sample.UnderlayDilate;
                design.UnderlaySoftness = sample.UnderlaySoftness;
            }

            if (sample.GlowOuter > design.GlowOuter || sample.GlowColor.a > design.GlowColor.a)
            {
                design.GlowColor = sample.GlowColor;
                design.GlowOffset = sample.GlowOffset;
                design.GlowPower = sample.GlowPower;
                design.GlowOuter = sample.GlowOuter;
                design.GlowInner = sample.GlowInner;
            }

            // FontStyleは初回キャプチャを正とする
            // PreferRicherで強くORするとタブ選択など動的Boldを固定してしまう

            design.EnabledKeywordsMask |= sample.EnabledKeywordsMask;
            // 輪郭幅があるならOutlineキーワードを立てる(材質にKWが無くても可)
            if (design.OutlineWidth > 0.0001f)
            {
                design.EnabledKeywordsMask |= 1 << 0;
            }

            if (design.UnderlayDilate > 0.0001f
                || design.UnderlayOffsetX != 0f
                || design.UnderlayOffsetY != 0f
                || design.UnderlayColor.a > 0.001f)
            {
                design.EnabledKeywordsMask |= 1 << 1;
            }

            design.HasMaterial = true;
            return design;
        }

        private static StyleSnapshot MergeStyleForApply(StyleSnapshot design, StyleSnapshot sample)
        {
            if (!design.HasMaterial && !HasFontStyle(design) && design.FontWeight <= FontWeight.Regular)
            {
                return sample;
            }

            if (!sample.HasMaterial && !HasFontStyle(sample) && sample.FontWeight <= FontWeight.Regular)
            {
                return design;
            }

            return PreferRicher(design, sample);
        }

        private static bool HasFontStyle(StyleSnapshot snapshot)
        {
            return snapshot.FontStyle != FontStyles.Normal;
        }

        private static bool StyleMatches(TMP_Text text, StyleSnapshot design)
        {
            // FontStyleは動的切替のため定期比較しない(差替時のsample保持で担保)

            if (!design.HasMaterial)
            {
                return true;
            }

            Material material = text.fontSharedMaterial;
            if (material == null)
            {
                return false;
            }

            float outlineWidth = ReadFloat(material, ShaderUtilities.ID_OutlineWidth);
            if (design.OutlineWidth > 0.0001f && outlineWidth + 0.0001f < design.OutlineWidth)
            {
                return false;
            }

            if (!IsNearWhite(design.FaceColor))
            {
                Color face = ReadColor(material, ShaderUtilities.ID_FaceColor);
                if (!ColorsNearlyEqual(face, design.FaceColor))
                {
                    return false;
                }
            }

            if (design.UnderlayDilate > 0.0001f || design.UnderlayColor.a > 0.001f)
            {
                float underlayDilate = ReadFloat(material, ShaderUtilities.ID_UnderlayDilate);
                if (underlayDilate + 0.0001f < design.UnderlayDilate)
                {
                    return false;
                }
            }

            return true;
        }

        private static StyleSnapshot CaptureStyle(TMP_Text text)
        {
            Material material = text.fontSharedMaterial;
            float outlineWidth = text.outlineWidth;
            Color32 outlineColor = text.outlineColor;
            Color32 faceColor = text.faceColor;

            // TMPプロパティが0でも材質側に輪郭がある場合がある
            if (material != null && material.HasProperty(ShaderUtilities.ID_OutlineWidth))
            {
                float materialOutline = material.GetFloat(ShaderUtilities.ID_OutlineWidth);
                if (materialOutline > outlineWidth + 0.0001f)
                {
                    outlineWidth = materialOutline;
                    if (material.HasProperty(ShaderUtilities.ID_OutlineColor))
                    {
                        outlineColor = material.GetColor(ShaderUtilities.ID_OutlineColor);
                    }
                }
            }

            // faceColorは材質Faceが非白ならそちらを使う
            if (material != null && material.HasProperty(ShaderUtilities.ID_FaceColor))
            {
                Color materialFace = material.GetColor(ShaderUtilities.ID_FaceColor);
                if (!IsNearWhite(materialFace))
                {
                    faceColor = materialFace;
                }
            }

            var snapshot = new StyleSnapshot
            {
                FaceColor = faceColor,
                OutlineWidth = outlineWidth,
                OutlineColor = outlineColor,
                FontStyle = text.fontStyle,
                FontWeight = text.fontWeight,
                HasMaterial = material != null,
            };

            if (material == null)
            {
                return snapshot;
            }

            snapshot.FaceDilate = ReadFloat(material, ShaderUtilities.ID_FaceDilate);
            snapshot.OutlineSoftness = ReadFloat(material, ShaderUtilities.ID_OutlineSoftness);
            snapshot.UnderlayColor = ReadColor(material, ShaderUtilities.ID_UnderlayColor);
            snapshot.UnderlayOffsetX = ReadFloat(material, ShaderUtilities.ID_UnderlayOffsetX);
            snapshot.UnderlayOffsetY = ReadFloat(material, ShaderUtilities.ID_UnderlayOffsetY);
            snapshot.UnderlayDilate = ReadFloat(material, ShaderUtilities.ID_UnderlayDilate);
            snapshot.UnderlaySoftness = ReadFloat(material, ShaderUtilities.ID_UnderlaySoftness);
            snapshot.GlowColor = ReadColor(material, ShaderUtilities.ID_GlowColor);
            snapshot.GlowOffset = ReadFloat(material, ShaderUtilities.ID_GlowOffset);
            snapshot.GlowPower = ReadFloat(material, ShaderUtilities.ID_GlowPower);
            snapshot.GlowOuter = ReadFloat(material, ShaderUtilities.ID_GlowOuter);
            snapshot.GlowInner = ReadFloat(material, ShaderUtilities.ID_GlowInner);

            for (int i = 0; i < VisualKeywords.Length; i++)
            {
                string keyword = VisualKeywords[i];
                if (!string.IsNullOrEmpty(keyword) && material.IsKeywordEnabled(keyword))
                {
                    snapshot.EnabledKeywordsMask |= 1 << i;
                }
            }

            if (snapshot.OutlineWidth > 0.0001f)
            {
                snapshot.EnabledKeywordsMask |= 1 << 0;
            }

            return snapshot;
        }

        private static void RestoreStyle(TMP_Text text, Material styleMaterial, StyleSnapshot snapshot)
        {
            if (styleMaterial == null)
            {
                text.faceColor = snapshot.FaceColor;
                text.outlineWidth = snapshot.OutlineWidth;
                text.outlineColor = snapshot.OutlineColor;
                RestoreFontStyle(text, snapshot);
                return;
            }

            WriteColor(styleMaterial, ShaderUtilities.ID_FaceColor, snapshot.FaceColor);
            WriteFloat(styleMaterial, ShaderUtilities.ID_FaceDilate, snapshot.FaceDilate);
            WriteColor(styleMaterial, ShaderUtilities.ID_OutlineColor, snapshot.OutlineColor);
            WriteFloat(styleMaterial, ShaderUtilities.ID_OutlineWidth, snapshot.OutlineWidth);
            WriteFloat(styleMaterial, ShaderUtilities.ID_OutlineSoftness, snapshot.OutlineSoftness);
            WriteColor(styleMaterial, ShaderUtilities.ID_UnderlayColor, snapshot.UnderlayColor);
            WriteFloat(styleMaterial, ShaderUtilities.ID_UnderlayOffsetX, snapshot.UnderlayOffsetX);
            WriteFloat(styleMaterial, ShaderUtilities.ID_UnderlayOffsetY, snapshot.UnderlayOffsetY);
            WriteFloat(styleMaterial, ShaderUtilities.ID_UnderlayDilate, snapshot.UnderlayDilate);
            WriteFloat(styleMaterial, ShaderUtilities.ID_UnderlaySoftness, snapshot.UnderlaySoftness);
            WriteColor(styleMaterial, ShaderUtilities.ID_GlowColor, snapshot.GlowColor);
            WriteFloat(styleMaterial, ShaderUtilities.ID_GlowOffset, snapshot.GlowOffset);
            WriteFloat(styleMaterial, ShaderUtilities.ID_GlowPower, snapshot.GlowPower);
            WriteFloat(styleMaterial, ShaderUtilities.ID_GlowOuter, snapshot.GlowOuter);
            WriteFloat(styleMaterial, ShaderUtilities.ID_GlowInner, snapshot.GlowInner);

            for (int i = 0; i < VisualKeywords.Length; i++)
            {
                string keyword = VisualKeywords[i];
                if (string.IsNullOrEmpty(keyword))
                {
                    continue;
                }

                if ((snapshot.EnabledKeywordsMask & (1 << i)) != 0)
                {
                    styleMaterial.EnableKeyword(keyword);
                }
                else
                {
                    styleMaterial.DisableKeyword(keyword);
                }
            }

            // TMPプロパティ側も同期し後続の読み取りで0に戻らないようにする
            text.faceColor = snapshot.FaceColor;
            text.outlineWidth = snapshot.OutlineWidth;
            text.outlineColor = snapshot.OutlineColor;
            RestoreFontStyle(text, snapshot);
        }

        private static void RestoreFontStyle(TMP_Text text, StyleSnapshot snapshot)
        {
            if (text == null)
            {
                return;
            }

            // font代入や材質差し替えでBoldが消えることがあるので復元する
            if (text.fontStyle != snapshot.FontStyle)
            {
                text.fontStyle = snapshot.FontStyle;
            }

            // Regular未満は既定値扱いだがSemiBold/Bold等は明示復元する
            if (snapshot.FontWeight > FontWeight.Regular
                && text.fontWeight != snapshot.FontWeight)
            {
                text.fontWeight = snapshot.FontWeight;
            }
        }

        private static bool UsesFontAtlas(Material material, TMP_FontAsset fontAsset)
        {
            if (material == null || fontAsset == null || fontAsset.material == null)
            {
                return false;
            }

            Texture fontTex = GetMainTexture(fontAsset.material);
            Texture materialTex = GetMainTexture(material);
            return fontTex != null && materialTex == fontTex;
        }

        private static Texture GetMainTexture(Material material)
        {
            if (material == null || !material.HasProperty(ShaderUtilities.ID_MainTex))
            {
                return null;
            }

            return material.GetTexture(ShaderUtilities.ID_MainTex);
        }

        private static bool IsNearWhite(Color color)
        {
            return color.r > 0.99f
                && color.g > 0.99f
                && color.b > 0.99f
                && color.a > 0.99f;
        }

        private static bool ColorsNearlyEqual(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.02f
                && Mathf.Abs(a.g - b.g) < 0.02f
                && Mathf.Abs(a.b - b.b) < 0.02f
                && Mathf.Abs(a.a - b.a) < 0.02f;
        }

        private static float ReadFloat(Material material, int propertyId)
        {
            if (propertyId == 0 || !material.HasProperty(propertyId))
            {
                return 0f;
            }

            return material.GetFloat(propertyId);
        }

        private static Color ReadColor(Material material, int propertyId)
        {
            if (propertyId == 0 || !material.HasProperty(propertyId))
            {
                return Color.white;
            }

            return material.GetColor(propertyId);
        }

        private static void WriteFloat(Material material, int propertyId, float value)
        {
            if (propertyId == 0 || !material.HasProperty(propertyId))
            {
                return;
            }

            material.SetFloat(propertyId, value);
        }

        private static void WriteColor(Material material, int propertyId, Color value)
        {
            if (propertyId == 0 || !material.HasProperty(propertyId))
            {
                return;
            }

            material.SetColor(propertyId, value);
        }

        private struct StyleSnapshot
        {
            public Color32 FaceColor;
            public float OutlineWidth;
            public Color32 OutlineColor;
            public float FaceDilate;
            public float OutlineSoftness;
            public Color UnderlayColor;
            public float UnderlayOffsetX;
            public float UnderlayOffsetY;
            public float UnderlayDilate;
            public float UnderlaySoftness;
            public Color GlowColor;
            public float GlowOffset;
            public float GlowPower;
            public float GlowOuter;
            public float GlowInner;
            public FontStyles FontStyle;
            public FontWeight FontWeight;
            public int EnabledKeywordsMask;
            public bool HasMaterial;
        }
    }
}
