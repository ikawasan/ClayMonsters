using Battle;
using ClayEditor.Rigging;
using UI.Battle.View;
using UnityEngine;
using UnityEngine.UI;

namespace UI.ClayEditor.View
{
    /// <summary>
    /// セーブスロット向け破壊対象部位アイコンUIを組み立てる
    /// 枠付きアイコンを生成して攻撃チップへ配置する
    /// </summary>
    public static class MoveTargetPartIconView
    {
        private static readonly Color FrameOutlineColor = new Color(0.58f, 0.66f, 0.78f, 0.55f);

        /// <summary>
        /// 使用部位アイコンUIを親へ生成する
        /// </summary>
        public static RectTransform CreateRequired(Transform parent, float size, MotionType motion)
        {
            return CreateInternal(
                parent,
                size,
                motion,
                "RequiredPartIcon",
                static (image, attack) => MoveTargetPartIconUtility.ApplyRequiredPartIcon(image, attack));
        }

        /// <summary>
        /// 破壊対象部位アイコンUIを親へ生成する
        /// </summary>
        public static RectTransform CreateTarget(Transform parent, float size, MotionType motion)
        {
            return CreateInternal(
                parent,
                size,
                motion,
                "TargetPartIcon",
                static (image, attack) => MoveTargetPartIconUtility.ApplyTargetPartIcon(image, attack));
        }

        /// <summary>
        /// 破壊対象部位アイコンUIを親へ生成する
        /// </summary>
        public static RectTransform Create(Transform parent, float size, MotionType motion)
        {
            return CreateTarget(parent, size, motion);
        }

        private static RectTransform CreateInternal(
            Transform parent,
            float size,
            MotionType motion,
            string objectName,
            System.Action<Image, MotionType> applyIcon)
        {
            var rootObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(LayoutElement),
                typeof(Image));
            rootObject.transform.SetParent(parent, false);

            LayoutElement rootLayout = rootObject.GetComponent<LayoutElement>();
            rootLayout.preferredWidth = size;
            rootLayout.preferredHeight = size;
            rootLayout.minWidth = size;
            rootLayout.minHeight = size;
            rootLayout.flexibleWidth = 0f;
            rootLayout.flexibleHeight = 0f;

            Image frameImage = rootObject.GetComponent<Image>();
            frameImage.raycastTarget = false;
            frameImage.type = Image.Type.Simple;
            frameImage.sprite = CreateFrameSprite();

            var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(rootObject.transform, false);

            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            float inset = Mathf.Max(1f, size * 0.06f);
            iconRect.offsetMin = new Vector2(inset, inset);
            iconRect.offsetMax = new Vector2(-inset, -inset);

            Image iconImage = iconObject.GetComponent<Image>();
            iconImage.raycastTarget = false;
            iconImage.preserveAspect = true;
            iconImage.type = Image.Type.Simple;
            applyIcon(iconImage, motion);

            return rootObject.GetComponent<RectTransform>();
        }

        /// <summary>
        /// 既存Imageへ破壊対象部位アイコンを適用する
        /// </summary>
        public static void Apply(Image image, MotionType motion)
        {
            MoveTargetPartIconUtility.ApplyTargetPartIcon(image, motion);
        }

        /// <summary>
        /// 部位アイコン枠の見た目をEditorBakeで適用する
        /// </summary>
        public static void ApplyFrameVisual(Image frameImage)
        {
            if (frameImage == null)
            {
                return;
            }

            frameImage.raycastTarget = false;
            frameImage.type = Image.Type.Simple;
            frameImage.sprite = CreateFrameSprite();
            frameImage.color = Color.white;
        }

        private static Sprite frameSprite;

        private static Sprite CreateFrameSprite()
        {
            if (frameSprite != null)
            {
                return frameSprite;
            }

            const int size = 16;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;

            Color fill = Color.white;
            Color border = FrameOutlineColor;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isBorder = x == 0 || y == 0 || x == size - 1 || y == size - 1;
                    bool isCorner = (x <= 1 || x >= size - 2) && (y <= 1 || y >= size - 2);
                    if (isBorder)
                    {
                        texture.SetPixel(x, y, border);
                    }
                    else if (isCorner)
                    {
                        texture.SetPixel(x, y, border * 0.65f);
                    }
                    else
                    {
                        texture.SetPixel(x, y, fill);
                    }
                }
            }

            texture.Apply();
            frameSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(4f, 4f, 4f, 4f));
            return frameSprite;
        }
    }
}
