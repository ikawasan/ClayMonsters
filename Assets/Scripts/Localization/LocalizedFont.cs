using System.Collections.Generic;
using LighthouseExtends.Font;
using TMPro;
using UnityEngine;

namespace Localization
{
    /// <summary>
    /// 現在言語のTMPFontAssetをプレーンTMPへ適用する
    /// LHTextMeshPro以外はFontServiceを購読しないためここで補う
    /// 太字はコンポーネント設定を保持し輪郭は固有材質があるときだけ戻す
    /// フォント既定材質のFaceDilateは言語間でコピーしない
    /// LHTextMeshProの素のfont代入で太字が消えた場合も復旧する
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
        private static readonly Dictionary<int, FontMaterialDefaults> FontDefaults = new();
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
        /// 言語フォントの既定材質を汚染前に記録する
        /// LHTextMeshProがFaceDilateを共有材質へ書く前に呼ぶ
        /// </summary>
        /// <param name="fontService">言語フォントの取得元</param>
        public static void SnapshotLanguageFonts(IFontService fontService)
        {
            if (fontService == null)
            {
                return;
            }

            for (int i = 0; i < GameLanguageCodes.All.Length; i++)
            {
                SnapshotFontDefaultsIfNeeded(fontService.GetFont(GameLanguageCodes.All[i]));
            }
        }

        /// <summary>
        /// フォント差替前の輪郭プリセットと太字を記録する
        /// LHTextMeshProが材質を潰す前に呼ぶ
        /// </summary>
        public static void CaptureDesignsOfAllLoaded()
        {
            RefreshLoadedTextsCache(true);
            for (int i = 0; i < LoadedTextsCache.Count; i++)
            {
                TMP_Text text = LoadedTextsCache[i];
                if (text == null || text.gameObject.scene.IsValid() == false)
                {
                    continue;
                }

                RememberDesign(text.GetInstanceID(), CaptureStyle(text));
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

            RestoreFontAssetDefaults(font);
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
        /// 太字はコンポーネント設定を保持し輪郭は固有材質があるときだけ戻す
        /// フォント既定材質のFaceDilateは言語間でコピーしない
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

            RestoreFontAssetDefaults(fontAsset);

            int instanceId = text.GetInstanceID();
            StyleSnapshot sample = CaptureStyle(text);
            RememberDesign(instanceId, sample);
            DesignStyles.TryGetValue(instanceId, out StyleSnapshot design);
            design = ResolveUniquePreset(instanceId, design, fontAsset);

            bool fontAlreadyCurrent = text.font == fontAsset;
            bool atlasAlreadyCorrect = UsesFontAtlas(text.fontSharedMaterial, fontAsset);
            if (fontAlreadyCurrent && atlasAlreadyCorrect && StyleMatches(text, design))
            {
                if (!design.HasUniqueEffects)
                {
                    ResetToFontDefaultMaterial(text, fontAsset);
                }

                return;
            }

            Color preservedVertexColor = text.color;
            if (!fontAlreadyCurrent)
            {
                StripFallbackSubMeshes(text);
                text.font = fontAsset;
            }

            RestoreFontStylePreservingRicher(text, design, sample);

            if (design.HasUniqueEffects)
            {
                if (CanAssignUniquePreset(fontAsset, design))
                {
                    if (text.fontSharedMaterial != design.UniquePresetMaterial)
                    {
                        text.fontSharedMaterial = design.UniquePresetMaterial;
                    }
                }
                else
                {
                    Material styleMaterial = EnsureCleanInstanceMaterial(text, fontAsset);
                    RestoreUniqueEffects(text, styleMaterial, design);
                }
            }
            else
            {
                ResetToFontDefaultMaterial(text, fontAsset);
            }

            text.color = preservedVertexColor;
            if (!fontAlreadyCurrent || !atlasAlreadyCorrect)
            {
                RebuildMesh(text);
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
            RebuildMesh(text);
        }

        /// <summary>
        /// 言語切替後に旧フォントのサブメッシュが残らないよう全TMPを再構築する
        /// </summary>
        public static void RebuildAllLoadedMeshes()
        {
            RefreshLoadedTextsCache(true);
            for (int i = 0; i < LoadedTextsCache.Count; i++)
            {
                TMP_Text text = LoadedTextsCache[i];
                if (text == null)
                {
                    continue;
                }

                RebuildMesh(text);
            }
        }

        /// <summary>
        /// 旧言語のフォールバックサブメッシュを捨てて現在文言だけで描画し直す
        /// </summary>
        public static void RebuildMesh(TMP_Text text)
        {
            if (!CanRebuildMesh(text))
            {
                return;
            }

            StripFallbackSubMeshes(text);
            if (!CanRebuildMesh(text))
            {
                return;
            }

            text.ClearMesh();
            text.ForceMeshUpdate(true, true);
        }

        private static bool CanRebuildMesh(TMP_Text text)
        {
            if (text == null)
            {
                return false;
            }

            if (text.gameObject.scene.IsValid() == false)
            {
                return false;
            }

            // 未Awakeや破棄途中のUGUIはCanvasRendererが無くClearMeshが落ちる
            if (text is TextMeshProUGUI ugui && ugui.canvasRenderer == null)
            {
                return false;
            }

            return true;
        }

        private static void StripFallbackSubMeshes(TMP_Text text)
        {
            Transform root = text.transform;
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                if (child.GetComponent<TMP_SubMeshUI>() == null
                    && child.GetComponent<TMP_SubMesh>() == null)
                {
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
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

        private static bool IsUniqueStyleMaterial(TMP_Text text, Material material)
        {
            if (text == null || material == null || text.font == null)
            {
                return false;
            }

            Material fontBase = text.font.material;
            return fontBase == null || material != fontBase;
        }

        private static Material EnsureCleanInstanceMaterial(TMP_Text text, TMP_FontAsset fontAsset)
        {
            Material fontBase = fontAsset != null ? fontAsset.material : null;
            if (fontBase == null)
            {
                return text.fontSharedMaterial;
            }

            // 旧フォントからコピーされたFaceDilateが残るインスタンスは使わない
            text.fontSharedMaterial = fontBase;
            Material instance = text.fontMaterial;
            if (!IsWritableInstance(instance) || instance == fontBase)
            {
                instance = new Material(fontBase)
                {
                    name = fontBase.name + " (Instance)",
                };
            }

            text.fontSharedMaterial = instance;
            return instance;
        }

        private static void ResetToFontDefaultMaterial(TMP_Text text, TMP_FontAsset fontAsset)
        {
            Material fontBase = fontAsset != null ? fontAsset.material : null;
            if (text == null || fontBase == null)
            {
                return;
            }

            if (text.fontSharedMaterial != fontBase)
            {
                text.fontSharedMaterial = fontBase;
            }
        }

        private static bool IsWritableInstance(Material material)
        {
            if (material == null)
            {
                return false;
            }

            string materialName = material.name;
            return materialName.IndexOf("(Instance)", System.StringComparison.Ordinal) >= 0;
        }

        private static void RememberDesign(int instanceId, StyleSnapshot sample)
        {
            if (!DesignStyles.TryGetValue(instanceId, out StyleSnapshot design))
            {
                DesignStyles[instanceId] = sample;
                return;
            }

            if (design.UniquePresetMaterial == null && sample.UniquePresetMaterial != null)
            {
                design.UniquePresetMaterial = sample.UniquePresetMaterial;
            }

            if (sample.FontWeight > design.FontWeight)
            {
                design.FontWeight = sample.FontWeight;
            }

            // 初回のFontStyleを正とする後からの共有材質汚染は取り込まない
            if (!sample.HasUniqueEffects)
            {
                DesignStyles[instanceId] = design;
                return;
            }

            if (!design.HasUniqueEffects)
            {
                design.FaceDilate = sample.FaceDilate;
            }

            design.HasUniqueEffects = true;

            if (sample.OutlineWidth > design.OutlineWidth + 0.0001f)
            {
                design.OutlineWidth = sample.OutlineWidth;
                design.OutlineColor = sample.OutlineColor;
                design.OutlineSoftness = sample.OutlineSoftness;
            }

            if ((sample.EnabledKeywordsMask & (1 << 1)) != 0
                && (sample.UnderlayDilate > design.UnderlayDilate
                    || sample.UnderlayColor.a > design.UnderlayColor.a))
            {
                design.UnderlayColor = sample.UnderlayColor;
                design.UnderlayOffsetX = sample.UnderlayOffsetX;
                design.UnderlayOffsetY = sample.UnderlayOffsetY;
                design.UnderlayDilate = sample.UnderlayDilate;
                design.UnderlaySoftness = sample.UnderlaySoftness;
                design.HasUniqueEffects = true;
            }

            design.EnabledKeywordsMask |= sample.EnabledKeywordsMask;
            DesignStyles[instanceId] = design;
        }

        private static bool StyleMatches(TMP_Text text, StyleSnapshot design)
        {
            if (text.fontStyle != design.FontStyle)
            {
                // 動的Boldは一致扱いフォント差替で消えたNormalだけ後で戻す
                if (design.FontStyle == FontStyles.Normal || text.fontStyle != FontStyles.Normal)
                {
                    return !design.HasUniqueEffects || UniqueEffectsMatch(text, design);
                }

                return false;
            }

            return !design.HasUniqueEffects || UniqueEffectsMatch(text, design);
        }

        private static bool UniqueEffectsMatch(TMP_Text text, StyleSnapshot design)
        {
            Material material = text.fontSharedMaterial;
            if (material == null)
            {
                return false;
            }

            if (design.UniquePresetMaterial != null && material == design.UniquePresetMaterial)
            {
                return true;
            }

            if (!IsUniqueStyleMaterial(text, material))
            {
                return false;
            }

            if (design.OutlineWidth > 0.0001f)
            {
                float outlineWidth = ReadFloat(material, ShaderUtilities.ID_OutlineWidth);
                if (outlineWidth + 0.0001f < design.OutlineWidth)
                {
                    return false;
                }
            }

            float faceDilate = ReadFloat(material, ShaderUtilities.ID_FaceDilate);
            if (Mathf.Abs(faceDilate - design.FaceDilate) > 0.0001f)
            {
                return false;
            }

            bool requireInstanceKeywords = IsWritableInstance(material);
            for (int i = 0; i < VisualKeywords.Length; i++)
            {
                if ((design.EnabledKeywordsMask & (1 << i)) == 0)
                {
                    continue;
                }

                // 幅だけ持つOutlineプリセットはOUTLINE_ONが無いのでインスタンスだけ要求する
                if (i == 0 && !requireInstanceKeywords)
                {
                    continue;
                }

                string keyword = VisualKeywords[i];
                if (!string.IsNullOrEmpty(keyword) && !material.IsKeywordEnabled(keyword))
                {
                    return false;
                }
            }

            return true;
        }

        private static StyleSnapshot CaptureStyle(TMP_Text text)
        {
            var snapshot = new StyleSnapshot
            {
                FontStyle = text.fontStyle,
                FontWeight = text.fontWeight,
            };

            Material material = text.fontSharedMaterial;
            if (!IsUniqueStyleMaterial(text, material))
            {
                return snapshot;
            }

            snapshot.HasUniqueEffects = true;
            snapshot.OutlineWidth = ReadFloat(material, ShaderUtilities.ID_OutlineWidth);
            snapshot.OutlineColor = ReadColor(material, ShaderUtilities.ID_OutlineColor);
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

            bool hasOutline = snapshot.OutlineWidth > 0.0001f
                || (snapshot.EnabledKeywordsMask & 1) != 0;
            bool hasUnderlayKeyword = (snapshot.EnabledKeywordsMask & (1 << 1)) != 0;
            bool hasGlowKeyword = (snapshot.EnabledKeywordsMask & (1 << 2)) != 0;
            bool hasBevelKeyword = (snapshot.EnabledKeywordsMask & (1 << 3)) != 0;
            snapshot.HasUniqueEffects = hasOutline
                || hasUnderlayKeyword
                || hasGlowKeyword
                || hasBevelKeyword;

            if (snapshot.HasUniqueEffects && !IsWritableInstance(material))
            {
                snapshot.UniquePresetMaterial = material;
            }

            return snapshot;
        }

        private static bool CanAssignUniquePreset(TMP_FontAsset fontAsset, StyleSnapshot design)
        {
            Material preset = design.UniquePresetMaterial;
            if (preset == null || IsWritableInstance(preset))
            {
                return false;
            }

            return UsesFontAtlas(preset, fontAsset);
        }

        private static StyleSnapshot ResolveUniquePreset(
            int instanceId,
            StyleSnapshot design,
            TMP_FontAsset fontAsset)
        {
            if (!design.HasUniqueEffects)
            {
                return design;
            }

            if (CanAssignUniquePreset(fontAsset, design))
            {
                return design;
            }

            Material found = FindCompatibleUniquePreset(design, fontAsset);
            if (found == null)
            {
                return design;
            }

            design.UniquePresetMaterial = found;
            DesignStyles[instanceId] = design;
            return design;
        }

        private static Material FindCompatibleUniquePreset(StyleSnapshot design, TMP_FontAsset fontAsset)
        {
            if (design.OutlineWidth <= 0.0001f)
            {
                return null;
            }

            foreach (KeyValuePair<int, StyleSnapshot> pair in DesignStyles)
            {
                Material preset = pair.Value.UniquePresetMaterial;
                if (preset == null || IsWritableInstance(preset))
                {
                    continue;
                }

                if (!UsesFontAtlas(preset, fontAsset))
                {
                    continue;
                }

                if (Mathf.Abs(ReadFloat(preset, ShaderUtilities.ID_OutlineWidth) - design.OutlineWidth) > 0.0001f)
                {
                    continue;
                }

                if (Mathf.Abs(ReadFloat(preset, ShaderUtilities.ID_FaceDilate) - design.FaceDilate) > 0.0001f)
                {
                    continue;
                }

                return preset;
            }

            return null;
        }

        private static void RestoreUniqueEffects(
            TMP_Text text,
            Material styleMaterial,
            StyleSnapshot snapshot)
        {
            if (styleMaterial == null)
            {
                return;
            }

            WriteFloat(styleMaterial, ShaderUtilities.ID_FaceDilate, snapshot.FaceDilate);
            WriteColor(styleMaterial, ShaderUtilities.ID_OutlineColor, snapshot.OutlineColor);
            WriteFloat(styleMaterial, ShaderUtilities.ID_OutlineWidth, snapshot.OutlineWidth);
            WriteFloat(styleMaterial, ShaderUtilities.ID_OutlineSoftness, snapshot.OutlineSoftness);
            if (snapshot.OutlineWidth > 0.0001f || (snapshot.EnabledKeywordsMask & 1) != 0)
            {
                styleMaterial.EnableKeyword(ShaderUtilities.Keyword_Outline);
            }

            // プリセット材質の未使用Glow/Underlay既定値は新しいフォントへコピーしない
            if ((snapshot.EnabledKeywordsMask & (1 << 1)) != 0)
            {
                styleMaterial.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                WriteColor(styleMaterial, ShaderUtilities.ID_UnderlayColor, snapshot.UnderlayColor);
                WriteFloat(styleMaterial, ShaderUtilities.ID_UnderlayOffsetX, snapshot.UnderlayOffsetX);
                WriteFloat(styleMaterial, ShaderUtilities.ID_UnderlayOffsetY, snapshot.UnderlayOffsetY);
                WriteFloat(styleMaterial, ShaderUtilities.ID_UnderlayDilate, snapshot.UnderlayDilate);
                WriteFloat(styleMaterial, ShaderUtilities.ID_UnderlaySoftness, snapshot.UnderlaySoftness);
            }

            if ((snapshot.EnabledKeywordsMask & (1 << 2)) != 0)
            {
                styleMaterial.EnableKeyword(ShaderUtilities.Keyword_Glow);
                WriteColor(styleMaterial, ShaderUtilities.ID_GlowColor, snapshot.GlowColor);
                WriteFloat(styleMaterial, ShaderUtilities.ID_GlowOffset, snapshot.GlowOffset);
                WriteFloat(styleMaterial, ShaderUtilities.ID_GlowPower, snapshot.GlowPower);
                WriteFloat(styleMaterial, ShaderUtilities.ID_GlowOuter, snapshot.GlowOuter);
                WriteFloat(styleMaterial, ShaderUtilities.ID_GlowInner, snapshot.GlowInner);
            }

            if ((snapshot.EnabledKeywordsMask & (1 << 3)) != 0)
            {
                styleMaterial.EnableKeyword(ShaderUtilities.Keyword_Bevel);
            }
        }

        private static void RestoreFontStylePreservingRicher(
            TMP_Text text,
            StyleSnapshot design,
            StyleSnapshot sample)
        {
            if (text == null)
            {
                return;
            }

            FontStyles desired = design.FontStyle | sample.FontStyle;
            FontStyles current = text.fontStyle;
            FontStyles merged = current | desired;
            if (merged != current)
            {
                text.fontStyle = merged;
            }

            FontWeight weight = design.FontWeight;
            if (sample.FontWeight > weight)
            {
                weight = sample.FontWeight;
            }

            if (weight != 0 && text.fontWeight < weight)
            {
                text.fontWeight = weight;
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

        private static void SnapshotFontDefaultsIfNeeded(TMP_FontAsset fontAsset)
        {
            if (fontAsset == null || fontAsset.material == null)
            {
                return;
            }

            int fontId = fontAsset.GetInstanceID();
            if (FontDefaults.ContainsKey(fontId))
            {
                return;
            }

            Material material = fontAsset.material;
            FontDefaults[fontId] = new FontMaterialDefaults
            {
                FaceDilate = ReadFloat(material, ShaderUtilities.ID_FaceDilate),
                OutlineWidth = ReadFloat(material, ShaderUtilities.ID_OutlineWidth),
                OutlineSoftness = ReadFloat(material, ShaderUtilities.ID_OutlineSoftness),
                FaceColor = ReadColor(material, ShaderUtilities.ID_FaceColor),
            };
        }

        private static void RestoreFontAssetDefaults(TMP_FontAsset fontAsset)
        {
            if (fontAsset == null || fontAsset.material == null)
            {
                return;
            }

            int fontId = fontAsset.GetInstanceID();
            if (!FontDefaults.TryGetValue(fontId, out FontMaterialDefaults defaults))
            {
                return;
            }

            Material material = fontAsset.material;
            WriteFloatIfChanged(material, ShaderUtilities.ID_FaceDilate, defaults.FaceDilate);
            WriteFloatIfChanged(material, ShaderUtilities.ID_OutlineWidth, defaults.OutlineWidth);
            WriteFloatIfChanged(material, ShaderUtilities.ID_OutlineSoftness, defaults.OutlineSoftness);
            WriteColorIfChanged(material, ShaderUtilities.ID_FaceColor, defaults.FaceColor);
        }

        private static Texture GetMainTexture(Material material)
        {
            if (material == null || !material.HasProperty(ShaderUtilities.ID_MainTex))
            {
                return null;
            }

            return material.GetTexture(ShaderUtilities.ID_MainTex);
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

        private static void WriteFloatIfChanged(Material material, int propertyId, float value)
        {
            if (Mathf.Abs(ReadFloat(material, propertyId) - value) <= 0.0001f)
            {
                return;
            }

            WriteFloat(material, propertyId, value);
        }

        private static void WriteColor(Material material, int propertyId, Color value)
        {
            if (propertyId == 0 || !material.HasProperty(propertyId))
            {
                return;
            }

            material.SetColor(propertyId, value);
        }

        private static void WriteColorIfChanged(Material material, int propertyId, Color value)
        {
            Color current = ReadColor(material, propertyId);
            if (Mathf.Abs(current.r - value.r) <= 0.001f
                && Mathf.Abs(current.g - value.g) <= 0.001f
                && Mathf.Abs(current.b - value.b) <= 0.001f
                && Mathf.Abs(current.a - value.a) <= 0.001f)
            {
                return;
            }

            WriteColor(material, propertyId, value);
        }

        private struct FontMaterialDefaults
        {
            public float FaceDilate;
            public float OutlineWidth;
            public float OutlineSoftness;
            public Color FaceColor;
        }

        private struct StyleSnapshot
        {
            public float FaceDilate;
            public float OutlineWidth;
            public Color32 OutlineColor;
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
            public bool HasUniqueEffects;
            public Material UniquePresetMaterial;
        }
    }
}
