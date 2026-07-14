using System.Collections.Generic;
using UI.Battle.Interface;
using UnityEngine;

namespace UI.Battle.View
{
    /// <summary>
    /// 破壊対象部位ごとの部位アイコンスプライトを生成してキャッシュする
    /// 腕・脚・前・後・任意を判別しやすいシルエットで描画する
    /// </summary>
    public static class MoveTargetPartSpriteFactory
    {
        private const int Size = 48;
        private static readonly Color32 Clear = new Color32(0, 0, 0, 0);
        private static readonly Dictionary<MoveTargetPartId, Sprite> Cache = new Dictionary<MoveTargetPartId, Sprite>();

        /// <summary>
        /// 破壊対象部位用のアイコンスプライトを返す
        /// </summary>
        public static Sprite GetOrCreate(MoveTargetPartId targetPartId)
        {
            if (targetPartId == MoveTargetPartId.None)
            {
                return null;
            }

            if (Cache.TryGetValue(targetPartId, out Sprite cached))
            {
                return cached;
            }

            Sprite sprite = CreateSprite(targetPartId);
            Cache[targetPartId] = sprite;
            return sprite;
        }

        /// <summary>
        /// 破壊対象部位の表示色を返す
        /// </summary>
        public static Color GetDisplayColor(MoveTargetPartId targetPartId)
        {
            return Color.white;
        }

        private static Sprite CreateSprite(MoveTargetPartId targetPartId)
        {
            var pixels = new Color32[Size * Size];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Clear;
            }

            switch (targetPartId)
            {
                case MoveTargetPartId.Arm:
                    DrawArmIcon(pixels);
                    break;
                case MoveTargetPartId.Leg:
                    DrawLegIcon(pixels);
                    break;
                case MoveTargetPartId.Front:
                    DrawFrontIcon(pixels);
                    break;
                case MoveTargetPartId.Back:
                    DrawBackIcon(pixels);
                    break;
                case MoveTargetPartId.Any:
                    DrawAnyIcon(pixels);
                    break;
            }

            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels32(pixels);
            texture.Apply();

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, Size, Size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect);
        }

        private static void DrawArmIcon(Color32[] pixels)
        {
            Color32 fill = new Color32(240, 120, 64, 255);
            Color32 outline = new Color32(74, 32, 16, 255);
            Color32 highlight = new Color32(255, 196, 140, 255);

            FillRect(pixels, 8, 21, 20, 8, outline);
            FillRect(pixels, 9, 22, 18, 6, fill);
            FillCircle(pixels, 32, 22, 7, outline);
            FillCircle(pixels, 32, 22, 6, fill);
            FillRect(pixels, 29, 19, 5, 4, highlight);
            FillCircle(pixels, 35, 24, 2, outline);
        }

        private static void DrawLegIcon(Color32[] pixels)
        {
            Color32 fill = new Color32(72, 216, 88, 255);
            Color32 outline = new Color32(16, 72, 24, 255);
            Color32 highlight = new Color32(170, 255, 180, 255);

            FillRect(pixels, 10, 25, 14, 8, outline);
            FillRect(pixels, 11, 26, 12, 6, fill);
            FillRect(pixels, 20, 14, 8, 14, outline);
            FillRect(pixels, 21, 15, 6, 12, fill);
            FillRect(pixels, 24, 8, 14, 8, outline);
            FillRect(pixels, 25, 9, 12, 6, fill);
            FillRect(pixels, 27, 10, 6, 2, highlight);
            FillCircle(pixels, 33, 12, 2, outline);
        }

        private static void DrawFrontIcon(Color32[] pixels)
        {
            Color32 fill = new Color32(80, 160, 248, 255);
            Color32 outline = new Color32(16, 48, 96, 255);
            Color32 face = new Color32(210, 232, 255, 255);

            FillCircle(pixels, 24, 30, 11, outline);
            FillCircle(pixels, 24, 30, 10, fill);
            FillCircle(pixels, 24, 17, 9, outline);
            FillCircle(pixels, 24, 17, 8, face);
            FillCircle(pixels, 21, 16, 2, outline);
            FillCircle(pixels, 27, 16, 2, outline);
            FillRect(pixels, 22, 19, 4, 2, outline);
            FillRect(pixels, 19, 8, 10, 4, outline);
            FillRect(pixels, 20, 9, 8, 2, fill);
        }

        private static void DrawBackIcon(Color32[] pixels)
        {
            Color32 fill = new Color32(192, 96, 240, 255);
            Color32 outline = new Color32(64, 24, 96, 255);
            Color32 tail = new Color32(255, 196, 255, 255);
            Color32 spine = new Color32(150, 72, 196, 255);

            FillCircle(pixels, 22, 20, 10, outline);
            FillCircle(pixels, 22, 20, 9, fill);
            FillRect(pixels, 18, 8, 8, 5, outline);
            FillRect(pixels, 19, 9, 6, 3, fill);
            FillRect(pixels, 20, 14, 4, 8, outline);
            FillRect(pixels, 21, 15, 2, 6, spine);
            FillRect(pixels, 30, 18, 8, 5, outline);
            FillRect(pixels, 31, 19, 6, 3, tail);
            FillRect(pixels, 36, 20, 2, 9, outline);
            FillRect(pixels, 37, 21, 1, 7, tail);
        }

        private static void DrawAnyIcon(Color32[] pixels)
        {
            Color32 fill = new Color32(208, 216, 232, 255);
            Color32 outline = new Color32(64, 72, 96, 255);
            Color32 accent = new Color32(120, 136, 168, 255);

            FillCircle(pixels, 24, 24, 13, outline);
            FillCircle(pixels, 24, 24, 12, fill);
            FillCircle(pixels, 24, 24, 5, accent);
            FillCircle(pixels, 24, 24, 3, fill);
            DrawArrow(pixels, 24, 9, true, outline, fill);
            DrawArrow(pixels, 24, 39, false, outline, fill);
            DrawArrow(pixels, 9, 24, false, outline, fill, horizontal: true, positive: false);
            DrawArrow(pixels, 39, 24, false, outline, fill, horizontal: true, positive: true);
        }

        private static void DrawArrow(
            Color32[] pixels,
            int cx,
            int cy,
            bool pointUp,
            Color32 outline,
            Color32 fill,
            bool horizontal = false,
            bool positive = true)
        {
            if (!horizontal)
            {
                int direction = pointUp ? -1 : 1;
                for (int step = 0; step < 5; step++)
                {
                    int halfWidth = 4 - step;
                    int y = cy + direction * step;
                    FillRect(pixels, cx - halfWidth - 1, y - 1, halfWidth * 2 + 3, 3, outline);
                    FillRect(pixels, cx - halfWidth, y, halfWidth * 2 + 1, 1, fill);
                }

                return;
            }

            int dir = positive ? 1 : -1;
            for (int step = 0; step < 5; step++)
            {
                int halfHeight = 4 - step;
                int x = cx + dir * step;
                FillRect(pixels, x - 1, cy - halfHeight - 1, 3, halfHeight * 2 + 3, outline);
                FillRect(pixels, x, cy - halfHeight, 1, halfHeight * 2 + 1, fill);
            }
        }

        private static void FillRect(Color32[] pixels, int x, int y, int width, int height, Color32 color)
        {
            for (int py = y; py < y + height; py++)
            {
                for (int px = x; px < x + width; px++)
                {
                    SetPixel(pixels, px, py, color);
                }
            }
        }

        private static void FillCircle(Color32[] pixels, int cx, int cy, int radius, Color32 color)
        {
            int radiusSquared = radius * radius;
            for (int py = cy - radius; py <= cy + radius; py++)
            {
                for (int px = cx - radius; px <= cx + radius; px++)
                {
                    int dx = px - cx;
                    int dy = py - cy;
                    if (dx * dx + dy * dy <= radiusSquared)
                    {
                        SetPixel(pixels, px, py, color);
                    }
                }
            }
        }

        private static void SetPixel(Color32[] pixels, int x, int y, Color32 color)
        {
            if (x < 0 || y < 0 || x >= Size || y >= Size)
            {
                return;
            }

            pixels[y * Size + x] = color;
        }
    }
}
