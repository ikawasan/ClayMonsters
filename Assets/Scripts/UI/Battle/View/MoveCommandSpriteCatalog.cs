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
        /// 破壊対象部位アイコンを読み込む
        /// 実アルファ付きResourcesを優先し無ければ生成スプライトを使う
        /// </summary>
        public static Sprite LoadTargetPartIcon(MoveTargetPartId targetPartId)
        {
            Sprite sprite = targetPartId switch
            {
                MoveTargetPartId.None => LoadSprite($"{Root}/TargetPart/NoneIcon"),
                MoveTargetPartId.Arm => LoadSprite($"{Root}/TargetPart/ArmIcon"),
                MoveTargetPartId.Leg => LoadSprite($"{Root}/TargetPart/LegIcon"),
                MoveTargetPartId.Front => LoadSprite($"{Root}/TargetPart/HeadIcon"),
                MoveTargetPartId.Back => LoadSprite($"{Root}/TargetPart/TailIcon"),
                MoveTargetPartId.Body => LoadSprite($"{Root}/TargetPart/NoneIcon"),
                MoveTargetPartId.Any => LoadSprite($"{Root}/TargetPart/NoneIcon"),
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

        /// <summary>
        /// 部位欠損ロック用の交差鎖画像を読み込む
        /// </summary>
        public static Sprite LoadPartLockChain()
        {
            return LoadSprite($"{Root}/Battle/MovePartLockChain");
        }

        /// <summary>
        /// 部位欠損ロック用の錠画像を読み込む
        /// </summary>
        public static Sprite LoadPartLockPadlock()
        {
            return LoadSprite($"{Root}/Battle/MovePartLockPadlock");
        }

        private static Sprite LoadSprite(string resourcePath)
        {
            return Resources.Load<Sprite>(resourcePath);
        }
    }
}
