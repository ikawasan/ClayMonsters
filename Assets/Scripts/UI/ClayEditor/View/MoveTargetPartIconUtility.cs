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
        public static void ApplyRequiredPartIcon(Image image, MotionType motion)
        {
            ApplyTargetPartIcon(image, MotionPartRequirement.GetRequiredPart(motion));
        }

        /// <summary>
        /// 攻撃モーションの破壊対象部位アイコンをImageへ適用する
        /// </summary>
        public static void ApplyTargetPartIcon(Image image, MotionType motion)
        {
            ApplyTargetPartIcon(image, MotionPartRequirement.GetTargetDestroyPart(motion));
        }

        /// <summary>
        /// 破壊対象部位アイコンをImageへ適用する
        /// </summary>
        public static void ApplyTargetPartIcon(Image image, BonePart targetPart)
        {
            ApplyTargetPartIcon(image, ToTargetPartId(targetPart));
        }

        /// <summary>
        /// 破壊対象部位アイコンをImageへ適用する
        /// </summary>
        public static void ApplyTargetPartIcon(Image image, MoveTargetPartId targetPartId)
        {
            if (image == null)
            {
                return;
            }

            if (targetPartId == MoveTargetPartId.None)
            {
                image.gameObject.SetActive(false);
                return;
            }

            image.gameObject.SetActive(true);
            image.sprite = MoveCommandSpriteCatalog.LoadTargetPartIcon(targetPartId);
            image.color = MoveTargetPartSpriteFactory.GetDisplayColor(targetPartId);
            image.preserveAspect = true;
            image.type = Image.Type.Simple;
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
