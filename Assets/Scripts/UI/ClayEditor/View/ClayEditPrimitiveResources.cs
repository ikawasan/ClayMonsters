using ClayEditor;
using System.Collections.Generic;
using UnityEngine;

namespace UI.ClayEditor.View
{
    /// <summary>
    /// ClayEdit成形プリミティブボタン用画像のResources読み込み
    /// </summary>
    public static class ClayEditPrimitiveResources
    {
        private const string ResourceRoot = "Image/ClayEditPrimitive/";
        private static readonly Dictionary<ClaySculptBrushShape, Sprite> CachedSprites = new();

        /// <summary>
        /// 成形プリミティブ形状のアイコンSpriteを返す
        /// </summary>
        /// <param name="shape">成形ブラシ形状</param>
        public static Sprite GetIcon(ClaySculptBrushShape shape)
        {
            if (CachedSprites.TryGetValue(shape, out Sprite cachedSprite) && cachedSprite != null)
            {
                return cachedSprite;
            }

            string assetName = ResolveAssetName(shape);
            string resourcePath = ResourceRoot + assetName;
            Sprite sprite = LoadSprite(resourcePath);
            if (sprite == null)
            {
                Debug.LogError(
                    $"[ClayEditPrimitiveResources] {resourcePath} のSprite読み込みに失敗しました",
                    null);
                return null;
            }

            CachedSprites[shape] = sprite;
            return sprite;
        }

        private static string ResolveAssetName(ClaySculptBrushShape shape)
        {
            return shape switch
            {
                ClaySculptBrushShape.Cube => "CubePrimitive",
                ClaySculptBrushShape.Cone => "ConePrimitive",
                ClaySculptBrushShape.SquarePyramid => "SquarePyramidPrimitive",
                ClaySculptBrushShape.CircularRing => "CircularRingPrimitive",
                _ => "SpherePrimitive",
            };
        }

        private static Sprite LoadSprite(string resourcePath)
        {
            Sprite sprite = Resources.Load<Sprite>(resourcePath);
            if (sprite != null)
            {
                return sprite;
            }

            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null)
            {
                return null;
            }

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
        }
    }
}
