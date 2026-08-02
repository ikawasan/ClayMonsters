using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;

namespace Extensions
{
    /// <summary>
    /// 入力アイコンをTMPスプライトとして埋め込む
    /// </summary>
    public static class InputIconTmpUtility
    {
        private const string ResourcesFolder = "Image/InputIcons/";
        private const float IconScale = 1.45f;
        private const float WideKeyIconScale = 1.55f;

        private static readonly string[] IconNames =
        {
            "Mouse",
            "MouseLeft",
            "MouseRight",
            "MouseWheel",
            "KeyA",
            "KeyD",
            "KeyZ",
            "KeyY",
            "Key1",
            "Key2",
            "Key3",
            "Key4",
            "KeyLeft",
            "KeyRight",
            "KeyShift",
            "KeyCtrl",
            "KeyAlt",
            "KeyEsc",
            "KeySpace",
            "KeyDel"
        };

        private static readonly HashSet<string> WideKeyIconNames = new HashSet<string>
        {
            "KeyShift",
            "KeyCtrl",
            "KeyAlt",
            "KeyDel",
            "KeyEsc"
        };

        private static TMP_SpriteAsset cachedAsset;

        /// <summary>
        /// スプライトタグを返す
        /// </summary>
        /// <param name="iconName">アイコン名</param>
        public static string Icon(string iconName)
        {
            return $"<sprite name=\"{iconName}\">";
        }

        /// <summary>
        /// TMPへ入力アイコン用スプライトアセットを適用する
        /// </summary>
        /// <param name="text">対象TMP</param>
        public static void ApplySpriteAsset(TMP_Text text)
        {
            if (text == null)
            {
                return;
            }

            TMP_SpriteAsset asset = GetOrCreateSpriteAsset();
            if (asset == null)
            {
                Debug.LogError("[InputIconTmpUtility] 入力アイコンのSpriteAsset生成に失敗しました");
                return;
            }

            text.spriteAsset = asset;
            text.richText = true;
        }

        /// <summary>
        /// 入力アイコン用TMP_SpriteAssetを返す
        /// </summary>
        public static TMP_SpriteAsset GetOrCreateSpriteAsset()
        {
            if (cachedAsset != null)
            {
                return cachedAsset;
            }

            var textures = new List<Texture2D>(IconNames.Length);
            var names = new List<string>(IconNames.Length);
            for (int i = 0; i < IconNames.Length; i++)
            {
                string iconName = IconNames[i];
                Texture2D texture = LoadIconTexture(iconName);
                if (texture == null)
                {
                    Debug.LogError(
                        $"[InputIconTmpUtility] アイコンが見つかりません name={iconName}");
                    continue;
                }

                textures.Add(texture);
                names.Add(iconName);
            }

            if (textures.Count == 0)
            {
                return null;
            }

            var atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Rect[] rects = atlas.PackTextures(textures.ToArray(), 2, 1024, false);
            atlas.filterMode = FilterMode.Bilinear;
            atlas.wrapMode = TextureWrapMode.Clamp;
            atlas.name = "InputIconAtlas";

            var asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            asset.name = "InputIcons";
            asset.spriteSheet = atlas;
            SetAssetVersion(asset, "1.1.0");

            Shader shader = Shader.Find("TextMeshPro/Sprite");
            if (shader == null)
            {
                Debug.LogError("[InputIconTmpUtility] TextMeshPro/Spriteシェーダが見つかりません");
                return null;
            }

            var material = new Material(shader)
            {
                name = "InputIcons Material",
                mainTexture = atlas
            };
            asset.material = material;

            List<TMP_SpriteGlyph> glyphs = asset.spriteGlyphTable;
            List<TMP_SpriteCharacter> characters = asset.spriteCharacterTable;
            glyphs.Clear();
            characters.Clear();
            for (int i = 0; i < names.Count; i++)
            {
                Rect uv = rects[i];
                int x = Mathf.RoundToInt(uv.x * atlas.width);
                int y = Mathf.RoundToInt(uv.y * atlas.height);
                int w = Mathf.Max(1, Mathf.RoundToInt(uv.width * atlas.width));
                int h = Mathf.Max(1, Mathf.RoundToInt(uv.height * atlas.height));

                // bearingYはアイコン中央が文字のxハイト付近に来るよう設定
                float bearingY = h * 0.78f;
                var glyph = new TMP_SpriteGlyph(
                    (uint)i,
                    new GlyphMetrics(w, h, 0f, bearingY, w),
                    new GlyphRect(x, y, w, h),
                    1f,
                    0,
                    Sprite.Create(
                        atlas,
                        new Rect(x, y, w, h),
                        new Vector2(0.5f, 0.5f),
                        100f));
                glyphs.Add(glyph);

                var character = new TMP_SpriteCharacter(0xFFFEu, asset, glyph)
                {
                    name = names[i],
                    scale = ResolveIconScale(names[i])
                };
                characters.Add(character);
            }

            asset.faceInfo = new FaceInfo
            {
                familyName = "InputIcons",
                styleName = "Regular",
                pointSize = 60,
                scale = 1f,
                lineHeight = 60f,
                ascentLine = 50f,
                baseline = 0f,
                descentLine = -10f
            };
            asset.UpdateLookupTables();
            cachedAsset = asset;
            return cachedAsset;
        }

        private static float ResolveIconScale(string iconName)
        {
            return WideKeyIconNames.Contains(iconName) ? WideKeyIconScale : IconScale;
        }

        private static void SetAssetVersion(TMP_SpriteAsset asset, string version)
        {
            // versionのsetterがinternalのためリフレクションで設定
            PropertyInfo property = typeof(TMP_Asset).GetProperty(
                "version",
                BindingFlags.Instance | BindingFlags.Public);
            MethodInfo setter = property?.GetSetMethod(nonPublic: true);
            setter?.Invoke(asset, new object[] { version });
        }

        private static Texture2D LoadIconTexture(string iconName)
        {
            string path = ResourcesFolder + iconName;
            Texture2D texture = Resources.Load<Texture2D>(path);
            if (texture != null)
            {
                return EnsureReadableCopy(texture);
            }

            Sprite sprite = Resources.Load<Sprite>(path);
            if (sprite == null)
            {
                return null;
            }

            return EnsureReadableCopy(sprite.texture, sprite.textureRect);
        }

        private static Texture2D EnsureReadableCopy(Texture2D source)
        {
            return EnsureReadableCopy(
                source,
                new Rect(0f, 0f, source.width, source.height));
        }

        private static Texture2D EnsureReadableCopy(Texture2D source, Rect pixelRect)
        {
            if (source == null)
            {
                return null;
            }

            int width = Mathf.Max(1, Mathf.RoundToInt(pixelRect.width));
            int height = Mathf.Max(1, Mathf.RoundToInt(pixelRect.height));
            var copy = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                Color[] pixels = source.GetPixels(
                    Mathf.RoundToInt(pixelRect.x),
                    Mathf.RoundToInt(pixelRect.y),
                    width,
                    height);
                copy.SetPixels(pixels);
                copy.Apply(false, false);
                copy.name = source.name;
                return copy;
            }
            catch (UnityException)
            {
                // isReadableでない場合はBlitでコピー
                RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
                RenderTexture previous = RenderTexture.active;
                Graphics.Blit(source, rt);
                RenderTexture.active = rt;
                copy.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                copy.Apply(false, false);
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
                copy.name = source.name;
                return copy;
            }
        }
    }
}
