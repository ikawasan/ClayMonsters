using System.Collections.Generic;
using UI.Battle.Interface;
using UnityEngine;

namespace UI.Battle.View
{
    /// <summary>
    /// 使用部位と破壊部位のアイコンを生成する
    /// 添付参考と同じ二重円枠の線画スタイルで描く
    /// </summary>
    public static class MoveTargetPartSpriteFactory
    {
        private const int Size = 72;
        private static readonly Color32 Clear = new Color32(0, 0, 0, 0);
        private static readonly Color32 Plate = new Color32(236, 236, 236, 255);
        private static readonly Color32 Ink = new Color32(70, 70, 70, 255);
        private static readonly Dictionary<MoveTargetPartId, Sprite> Cache =
            new Dictionary<MoveTargetPartId, Sprite>();

        /// <summary>
        /// 部位アイコンスプライトを返す
        /// </summary>
        public static Sprite GetOrCreate(MoveTargetPartId targetPartId)
        {
            if (Cache.TryGetValue(targetPartId, out Sprite cached))
            {
                return cached;
            }

            Sprite sprite = CreateSprite(targetPartId);
            Cache[targetPartId] = sprite;
            return sprite;
        }

        /// <summary>
        /// 部位アイコンの乗算色を返す
        /// </summary>
        public static Color GetDisplayColor(MoveTargetPartId targetPartId)
        {
            _ = targetPartId;
            return Color.white;
        }

        private static Sprite CreateSprite(MoveTargetPartId targetPartId)
        {
            var pixels = new Color32[Size * Size];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Clear;
            }

            DrawFrame(pixels);
            switch (targetPartId)
            {
                case MoveTargetPartId.Arm:
                    DrawArm(pixels);
                    break;
                case MoveTargetPartId.Leg:
                    DrawLeg(pixels);
                    break;
                case MoveTargetPartId.Front:
                    DrawFront(pixels);
                    break;
                case MoveTargetPartId.Back:
                    DrawBack(pixels);
                    break;
                case MoveTargetPartId.Body:
                case MoveTargetPartId.None:
                case MoveTargetPartId.Any:
                default:
                    // Bodyは破壊なしと同じ×アイコン
                    DrawNone(pixels);
                    break;
            }

            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                alphaIsTransparency = true
            };
            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, Size, Size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect);
        }

        private static void DrawFrame(Color32[] pixels)
        {
            // 円内は不透明の薄い塗り円外は透明
            FillCircle(pixels, 36, 36, 34, Plate);
            StrokeCircle(pixels, 36, 36, 33, Ink, 3);
            StrokeCircle(pixels, 36, 36, 28, Ink, 2);
        }

        private static void DrawFront(Color32[] pixels)
        {
            FillCircle(pixels, 28, 40, 3, Ink);
            FillCircle(pixels, 44, 40, 3, Ink);
            StrokeLine(pixels, 24, 28, 48, 28, Ink, 2);
        }

        private static void DrawArm(Color32[] pixels)
        {
            // 力こぶの輪郭
            StrokeBezier(pixels, 22, 18, 12, 28, 14, 44, 24, 50, Ink, 2);
            StrokeBezier(pixels, 24, 50, 32, 54, 40, 48, 42, 40, Ink, 2);
            StrokeBezier(pixels, 42, 40, 44, 46, 50, 52, 58, 50, Ink, 2);
            StrokeBezier(pixels, 58, 50, 64, 48, 66, 40, 62, 34, Ink, 2);
            StrokeBezier(pixels, 62, 34, 56, 30, 48, 34, 44, 40, Ink, 2);
            StrokeBezier(pixels, 42, 40, 36, 30, 28, 22, 22, 18, Ink, 2);
        }

        private static void DrawLeg(Color32[] pixels)
        {
            StrokeBezier(pixels, 28, 54, 22, 44, 22, 30, 28, 22, Ink, 2);
            StrokeBezier(pixels, 28, 22, 36, 16, 44, 20, 48, 28, Ink, 2);
            StrokeBezier(pixels, 48, 28, 52, 36, 54, 46, 52, 54, Ink, 2);
            StrokeBezier(pixels, 52, 54, 56, 58, 64, 56, 68, 50, Ink, 2);
            StrokeBezier(pixels, 68, 50, 60, 48, 54, 46, 50, 42, Ink, 2);
            StrokeBezier(pixels, 50, 42, 42, 48, 34, 52, 28, 54, Ink, 2);
        }

        private static void DrawBack(Color32[] pixels)
        {
            // 尻尾のS曲線
            StrokeBezier(pixels, 22, 20, 16, 36, 28, 52, 48, 48, Ink, 3);
            StrokeBezier(pixels, 48, 48, 60, 44, 58, 28, 46, 30, Ink, 3);
        }

        private static void DrawNone(Color32[] pixels)
        {
            // 部位なしの×マーク
            StrokeLine(pixels, 22, 22, 50, 50, Ink, 5);
            StrokeLine(pixels, 50, 22, 22, 50, Ink, 5);
        }

        private static void StrokeCircle(Color32[] pixels, int cx, int cy, int radius, Color32 color, int thickness)
        {
            for (int t = 0; t < thickness; t++)
            {
                int r = radius - t;
                if (r <= 0)
                {
                    continue;
                }

                for (int angle = 0; angle < 360; angle++)
                {
                    float rad = angle * Mathf.Deg2Rad;
                    int x = cx + Mathf.RoundToInt(Mathf.Cos(rad) * r);
                    int y = cy + Mathf.RoundToInt(Mathf.Sin(rad) * r);
                    FillCircle(pixels, x, y, 0, color);
                    SetPixel(pixels, x, y, color);
                }
            }
        }

        private static void StrokeLine(Color32[] pixels, int x0, int y0, int x1, int y1, Color32 color, int thickness)
        {
            int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0), 1);
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t));
                int y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
                FillCircle(pixels, x, y, Mathf.Max(thickness / 2, 0), color);
            }
        }

        private static void StrokeBezier(
            Color32[] pixels,
            float x0,
            float y0,
            float x1,
            float y1,
            float x2,
            float y2,
            float x3,
            float y3,
            Color32 color,
            int thickness)
        {
            const int steps = 32;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                float u = 1f - t;
                float x = (u * u * u * x0) + (3f * u * u * t * x1) + (3f * u * t * t * x2) + (t * t * t * x3);
                float y = (u * u * u * y0) + (3f * u * u * t * y1) + (3f * u * t * t * y2) + (t * t * t * y3);
                FillCircle(pixels, Mathf.RoundToInt(x), Mathf.RoundToInt(y), Mathf.Max(thickness / 2, 0), color);
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
