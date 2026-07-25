using Battle;
using ClayEditor.Rigging;
using UI.Battle.Interface;
using UI.Battle.View;
using UnityEngine;
using UnityEngine.UI;

namespace UI.ClayEditor.View
{
    /// <summary>
    /// セーブUI向けに破壊対象部位アイコンをImageへ適用する
    /// </summary>
    public static class MoveTargetPartIconUtility
    {
        /// <summary>
        /// 攻撃モーションの使用部位アイコンをImageへ適用する
        /// </summary>
        public static void ApplyRequiredPartIcon(Image image, MotionType motion, bool preserveGameObjectActive = false)
        {
            ApplyTargetPartIcon(
                image,
                ToRequiredPartId(MotionPartRequirement.GetRequiredPart(motion)),
                preserveGameObjectActive);
        }

        /// <summary>
        /// 攻撃モーションの破壊対象部位アイコンをImageへ適用する
        /// </summary>
        public static void ApplyTargetPartIcon(Image image, MotionType motion, bool preserveGameObjectActive = false)
        {
            ApplyTargetPartIcon(
                image,
                ToTargetPartId(MotionPartRequirement.GetTargetDestroyPart(motion)),
                preserveGameObjectActive);
        }

        /// <summary>
        /// 破壊対象部位アイコンをImageへ適用する
        /// </summary>
        public static void ApplyTargetPartIcon(Image image, BonePart targetPart, bool preserveGameObjectActive = false)
        {
            ApplyTargetPartIcon(image, ToTargetPartId(targetPart), preserveGameObjectActive);
        }

        /// <summary>
        /// 破壊対象部位アイコンをImageへ適用する
        /// </summary>
        public static void ApplyTargetPartIcon(
            Image image,
            MoveTargetPartId targetPartId,
            bool preserveGameObjectActive = false)
        {
            if (image == null)
            {
                return;
            }

            Image iconImage = ResolveIconImage(image);

            iconImage.sprite = MoveCommandSpriteCatalog.LoadTargetPartIcon(targetPartId);
            iconImage.color = Color.white;
            iconImage.preserveAspect = true;
            iconImage.type = Image.Type.Simple;
            iconImage.enabled = true;
            DisableFrameImage(iconImage);

            if (!preserveGameObjectActive)
            {
                iconImage.gameObject.SetActive(true);
            }
        }

        private static Image ResolveIconImage(Image image)
        {
            if (image == null)
            {
                return null;
            }

            if (image.gameObject.name == "Icon")
            {
                return image;
            }

            Transform iconTransform = image.transform.Find("Icon");
            if (iconTransform != null && iconTransform.TryGetComponent(out Image childIcon))
            {
                return childIcon;
            }

            return image;
        }

        private static void DisableFrameImage(Image iconImage)
        {
            if (iconImage == null)
            {
                return;
            }

            Transform frameTransform = iconImage.transform.parent;
            if (frameTransform == null || !frameTransform.TryGetComponent(out Image frameImage))
            {
                return;
            }

            if (frameImage == iconImage)
            {
                return;
            }

            frameImage.enabled = false;
            frameImage.sprite = null;
            frameImage.color = new Color(1f, 1f, 1f, 0f);
        }

        private static MoveTargetPartId ToRequiredPartId(BonePart part)
        {
            switch (part)
            {
                case BonePart.Arm: return MoveTargetPartId.Arm;
                case BonePart.Leg: return MoveTargetPartId.Leg;
                case BonePart.Front: return MoveTargetPartId.Front;
                case BonePart.Back: return MoveTargetPartId.Back;
                case BonePart.Body: return MoveTargetPartId.None;
                default: return MoveTargetPartId.None;
            }
        }

        private static MoveTargetPartId ToTargetPartId(BonePart part)
        {
            switch (part)
            {
                case BonePart.Arm: return MoveTargetPartId.Arm;
                case BonePart.Leg: return MoveTargetPartId.Leg;
                case BonePart.Front: return MoveTargetPartId.Front;
                case BonePart.Back: return MoveTargetPartId.Back;
                case BonePart.Body: return MoveTargetPartId.None;
                default: return MoveTargetPartId.None;
            }
        }
    }
}
