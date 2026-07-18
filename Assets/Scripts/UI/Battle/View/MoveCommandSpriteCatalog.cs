using UI.Battle.Interface;
using UnityEngine;

namespace UI.Battle.View
{
    /// <summary>
    /// 攻撃コマンドUI用スプライトをResourcesから読み込む
    /// </summary>
    public static class MoveCommandSpriteCatalog
    {
        private const string Root = "Image";

        /// <summary>
        /// 技アイコンを読み込む
        /// </summary>
        public static Sprite LoadMoveIcon(MoveIconId iconId)
        {
            switch (iconId)
            {
                case MoveIconId.Tackle:
                    return LoadSprite($"{Root}/Move/Tackle");
                case MoveIconId.Punch:
                    return LoadSprite($"{Root}/Move/Punch");
                case MoveIconId.Kick:
                    return LoadSprite($"{Root}/Move/Kick");
                case MoveIconId.SpinTackle:
                    return LoadSprite($"{Root}/Move/SpinTackle");
                case MoveIconId.TailWhip:
                    return LoadSprite($"{Root}/Move/TailWhip");
                default:
                    return null;
            }
        }

        /// <summary>
        /// 破壊対象部位アイコンを読み込む
        /// </summary>
        public static Sprite LoadTargetPartIcon(MoveTargetPartId targetPartId)
        {
            Sprite sprite = targetPartId switch
            {
                MoveTargetPartId.Arm => LoadSprite($"{Root}/TargetPart/Arm"),
                MoveTargetPartId.Leg => LoadSprite($"{Root}/TargetPart/Leg"),
                MoveTargetPartId.Front => LoadSprite($"{Root}/TargetPart/Front"),
                MoveTargetPartId.Back => LoadSprite($"{Root}/TargetPart/Back"),
                MoveTargetPartId.Body => LoadSprite($"{Root}/TargetPart/Body"),
                MoveTargetPartId.Any => LoadSprite($"{Root}/TargetPart/Any"),
                _ => null
            };

            return sprite != null ? sprite : MoveTargetPartSpriteFactory.GetOrCreate(targetPartId);
        }

        /// <summary>
        /// Resourcesに破壊対象部位アイコンがあるかを返す
        /// </summary>
        public static bool HasTargetPartSprite(MoveTargetPartId targetPartId)
        {
            return targetPartId switch
            {
                MoveTargetPartId.Arm => LoadSprite($"{Root}/TargetPart/Arm") != null,
                MoveTargetPartId.Leg => LoadSprite($"{Root}/TargetPart/Leg") != null,
                MoveTargetPartId.Front => LoadSprite($"{Root}/TargetPart/Front") != null,
                MoveTargetPartId.Back => LoadSprite($"{Root}/TargetPart/Back") != null,
                MoveTargetPartId.Body => LoadSprite($"{Root}/TargetPart/Body") != null,
                MoveTargetPartId.Any => LoadSprite($"{Root}/TargetPart/Any") != null,
                _ => false
            };
        }

        /// <summary>
        /// 射程セグメント有効時の画像を読み込む
        /// </summary>
        public static Sprite LoadRangeActive()
        {
            return LoadSprite($"{Root}/Range/Active");
        }

        /// <summary>
        /// 射程セグメント無効時の画像を読み込む
        /// </summary>
        public static Sprite LoadRangeInactive()
        {
            return LoadSprite($"{Root}/Range/Inactive");
        }

        private static Sprite LoadSprite(string resourcePath)
        {
            return Resources.Load<Sprite>(resourcePath);
        }
    }
}
