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
        private const string ResourcesFolder = "Image/GameUiInputIcons/";
        private const float IconScale = 1.45f;
        // 複数文字キーも単一キーと同程度の表示幅に揃える
        private const float WideKeyIconScale = 1.45f;

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
            // 親テキスト色の影響で薄くならないよう色を固定する
            return $"<sprite name=\"{iconName}\" color=#FFFFFFFF>";
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

            // 余白を均等にした中心揃えテクスチャに整形する
            for (int i = 0; i < textures.Count; i++)
            {
                textures[i] = CenterContentOnTransparentCanvas(textures[i], names[i]);
            }

            var atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Rect[] rects = atlas.PackTextures(textures.ToArray(), 2, 2048, false);
            // 複数文字キーのぼやけを抑えるためニアレスト近傍
            atlas.filterMode = FilterMode.Point;
            atlas.wrapMode = TextureWrapMode.Clamp;
            atlas.name = "GameUiInputIconAtlas";

            var asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            asset.name = "GameUiInputIcons";
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
                name = "GameUiInputIcons Material",
                mainTexture = atlas
            };
            // 一部端末でマテリアル側のフィルタがバイリニアに戻るのを防ぐ
            material.mainTexture.filterMode = FilterMode.Point;
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

                // アイコン全体の中央が大文字の中高付近に来るよう配置する
                float bearingY = h * 0.72f;
                float bearingX = 0f;
                var glyph = new TMP_SpriteGlyph(
                    (uint)i,
                    new GlyphMetrics(w, h, bearingX, bearingY, w),
                    new GlyphRect(x, y, w, h),
                    1f,
                    0,
                    Sprite.Create(
                        atlas,
                        new Rect(x, y, w, h),
                        new Vector2(0.5f, 0.5f),
                        100f,
                        0,
                        SpriteMeshType.FullRect));
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
                familyName = "GameUiInputIcons",
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

        /// <summary>
        /// 不透明領域を中心に揃えた透明キャンバスへ載せる
        /// </summary>
        private static Texture2D CenterContentOnTransparentCanvas(Texture2D source, string iconName)
        {
            if (source == null)
            {
                return null;
            }

            Color32[] pixels = source.GetPixels32();
            int width = source.width;
            int height = source.height;
            if (!TryGetOpaqueBounds(pixels, width, height, out int minX, out int minY, out int maxX, out int maxY))
            {
                return source;
            }

            int contentW = maxX - minX + 1;
            int contentH = maxY - minY + 1;
            int pad = WideKeyIconNames.Contains(iconName) ? 4 : 3;
            int canvasW = contentW + (pad * 2);
            int canvasH = contentH + (pad * 2);
            var centered = new Texture2D(canvasW, canvasH, TextureFormat.RGBA32, false)
            {
                name = source.name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            var clear = new Color32[canvasW * canvasH];
            centered.SetPixels32(clear);

            var content = new Color32[contentW * contentH];
            for (int y = 0; y < contentH; y++)
            {
                int srcRow = (minY + y) * width + minX;
                int dstRow = y * contentW;
                for (int x = 0; x < contentW; x++)
                {
                    content[dstRow + x] = pixels[srcRow + x];
                }
            }

            centered.SetPixels32(pad, pad, contentW, contentH, content);
            centered.Apply(false, false);
            return centered;
        }

        private static bool TryGetOpaqueBounds(
            Color32[] pixels,
            int width,
            int height,
            out int minX,
            out int minY,
            out int maxX,
            out int maxY)
        {
            minX = width;
            minY = height;
            maxX = -1;
            maxY = -1;
            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    if (pixels[row + x].a <= 16)
                    {
                        continue;
                    }

                    if (x < minX)
                    {
                        minX = x;
                    }

                    if (y < minY)
                    {
                        minY = y;
                    }

                    if (x > maxX)
                    {
                        maxX = x;
                    }

                    if (y > maxY)
                    {
                        maxY = y;
                    }
                }
            }

            return maxX >= 0;
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
            var copy = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
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
                copy.filterMode = FilterMode.Point;
                return copy;
            }
        }
    }
}
