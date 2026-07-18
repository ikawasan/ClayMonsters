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

            if (targetPartId == MoveTargetPartId.None)
            {
                iconImage.sprite = null;
                if (preserveGameObjectActive)
                {
                    iconImage.enabled = false;
                    SetFrameImageEnabled(iconImage, false);
                }
                else
                {
                    iconImage.gameObject.SetActive(false);
                }

                return;
            }

            iconImage.sprite = MoveCommandSpriteCatalog.LoadTargetPartIcon(targetPartId);
            iconImage.preserveAspect = true;
            iconImage.type = Image.Type.Simple;

            if (preserveGameObjectActive)
            {
                iconImage.enabled = true;
                SetFrameImageEnabled(iconImage, true);
                return;
            }

            iconImage.gameObject.SetActive(true);
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

        private static void SetFrameImageEnabled(Image iconImage, bool enabled)
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

            frameImage.enabled = enabled;
        }

        private static MoveTargetPartId ToRequiredPartId(BonePart part)
        {
            switch (part)
            {
                case BonePart.Arm: return MoveTargetPartId.Arm;
                case BonePart.Leg: return MoveTargetPartId.Leg;
                case BonePart.Front: return MoveTargetPartId.Front;
                case BonePart.Back: return MoveTargetPartId.Back;
                case BonePart.Body: return MoveTargetPartId.Body;
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
                case BonePart.Body: return MoveTargetPartId.Any;
                default: return MoveTargetPartId.None;
            }
        }
    }
}
