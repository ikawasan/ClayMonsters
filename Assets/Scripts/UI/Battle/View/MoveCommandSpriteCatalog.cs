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
            if (iconId == MoveIconId.None)
            {
                return null;
            }

            return LoadSprite($"{Root}/Move/{iconId}");
        }

        /// <summary>
        /// 破壊対象部位アイコンを読み込む
        /// 実アルファ付きResourcesを優先し無ければ生成スプライトを使う
        /// </summary>
        public static Sprite LoadTargetPartIcon(MoveTargetPartId targetPartId)
        {
            Sprite sprite = targetPartId switch
            {
                MoveTargetPartId.None => LoadSprite($"{Root}/TargetPart/None"),
                MoveTargetPartId.Arm => LoadSprite($"{Root}/TargetPart/Arm"),
                MoveTargetPartId.Leg => LoadSprite($"{Root}/TargetPart/Leg"),
                MoveTargetPartId.Front => LoadSprite($"{Root}/TargetPart/Front"),
                MoveTargetPartId.Back => LoadSprite($"{Root}/TargetPart/Back"),
                MoveTargetPartId.Body => LoadSprite($"{Root}/TargetPart/None"),
                MoveTargetPartId.Any => LoadSprite($"{Root}/TargetPart/None"),
                _ => null
            };

            return sprite != null ? sprite : MoveTargetPartSpriteFactory.GetOrCreate(targetPartId);
        }

        /// <summary>
        /// 破壊対象部位アイコンが利用可能かを返す
        /// </summary>
        public static bool HasTargetPartSprite(MoveTargetPartId targetPartId)
        {
            _ = targetPartId;
            return true;
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
