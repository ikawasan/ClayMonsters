using Battle;
using ClayEditor.Rigging;
using UI.Battle.View;
using UnityEngine;
using UnityEngine.UI;

namespace UI.ClayEditor.View
{
    /// <summary>
    /// セーブスロット向け破壊対象部位アイコンUIを組み立てる
    /// </summary>
    public static class MoveTargetPartIconView
    {
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
                typeof(LayoutElement));
            rootObject.transform.SetParent(parent, false);

            LayoutElement rootLayout = rootObject.GetComponent<LayoutElement>();
            rootLayout.preferredWidth = size;
            rootLayout.preferredHeight = size;
            rootLayout.minWidth = size;
            rootLayout.minHeight = size;
            rootLayout.flexibleWidth = 0f;
            rootLayout.flexibleHeight = 0f;

            var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(rootObject.transform, false);

            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;

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
        /// 部位アイコン枠は使わないため既存枠Imageを無効化する
        /// </summary>
        public static void ApplyFrameVisual(Image frameImage)
        {
            if (frameImage == null)
            {
                return;
            }

            frameImage.enabled = false;
            frameImage.sprite = null;
            frameImage.color = new Color(1f, 1f, 1f, 0f);
            frameImage.raycastTarget = false;
        }
    }
}
