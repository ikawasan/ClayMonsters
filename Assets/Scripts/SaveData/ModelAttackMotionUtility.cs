using System.Collections.Generic;
using ClayEditor.Rigging;
using UnityEngine;

namespace SaveData
{
    /// <summary>
    /// 攻撃モーションリストの正規化ヘルパー
    /// </summary>
    public static class ModelAttackMotionUtility
    {
        /// <summary>
        /// 攻撃スロット数
        /// </summary>
        public const int SlotCount = 4;

        /// <summary>
        /// 攻撃モーションをスロット数に合わせて正規化する
        /// 攻撃でないモーションはエラーを出して除外し不足分は埋めない
        /// </summary>
        /// <param name="attackMotions">元の攻撃モーション</param>
        /// <param name="slotCount">スロット数</param>
        /// <returns>正規化済みリスト</returns>
        public static List<MotionType> Normalize(
            IReadOnlyList<MotionType> attackMotions,
            int slotCount = SlotCount)
        {
            var attacks = new List<MotionType>();
            if (attackMotions == null)
            {
                return attacks;
            }

            for (int i = 0; i < attackMotions.Count; i++)
            {
                MotionType motion = attackMotions[i];
                if (!ProceduralMotionCharacter.IsAttackMotion(motion))
                {
                    Debug.LogError($"[ModelAttackMotionUtility] 攻撃でないモーション{motion}を除外しました");
                    continue;
                }

                if (attacks.Contains(motion))
                {
                    continue;
                }

                attacks.Add(motion);
                if (attacks.Count >= slotCount)
                {
                    break;
                }
            }

            if (attacks.Count < slotCount)
            {
                Debug.LogError(
                    $"[ModelAttackMotionUtility] 攻撃スロットが不足しています({attacks.Count}/{slotCount})");
            }

            return attacks;
        }

        /// <summary>
        /// 使用可能な部位だけで構成される攻撃リストへ整える
        /// 必要部位が無い技はエラーを出して除外し使用可能攻撃で不足を埋める
        /// </summary>
        /// <param name="attackMotions">元の攻撃モーション</param>
        /// <param name="availableParts">使用可能な部位(Bodyは常に含む想定)</param>
        /// <param name="slotCount">スロット数</param>
        /// <returns>正規化済みリスト</returns>
        public static List<MotionType> SanitizeForAvailableParts(
            IReadOnlyList<MotionType> attackMotions,
            ICollection<BonePart> availableParts,
            int slotCount = SlotCount)
        {
            return AttackMotionSelector.SanitizeForAvailableParts(
                attackMotions,
                availableParts,
                slotCount);
        }

        /// <summary>
        /// 骨格で使用可能な攻撃だけを残し不足を使用可能攻撃で埋める
        /// 使えない技はエラーを出して除外する
        /// </summary>
        /// <param name="attackMotions">元の攻撃モーション</param>
        /// <param name="usableAttacks">骨格で使用可能な攻撃</param>
        /// <param name="slotCount">スロット数</param>
        /// <returns>正規化済みリスト</returns>
        public static List<MotionType> SanitizeForUsableAttacks(
            IReadOnlyList<MotionType> attackMotions,
            IReadOnlyList<MotionType> usableAttacks,
            int slotCount = SlotCount)
        {
            if (usableAttacks == null || usableAttacks.Count == 0)
            {
                return Normalize(attackMotions, slotCount);
            }

            var usable = new HashSet<MotionType>();
            for (int i = 0; i < usableAttacks.Count; i++)
            {
                MotionType motion = usableAttacks[i];
                if (ProceduralMotionCharacter.IsAttackMotion(motion))
                {
                    usable.Add(motion);
                }
            }

            var attacks = new List<MotionType>();
            if (attackMotions != null)
            {
                for (int i = 0; i < attackMotions.Count; i++)
                {
                    MotionType motion = attackMotions[i];
                    if (!usable.Contains(motion))
                    {
                        Debug.LogError(
                            $"[ModelAttackMotionUtility] 骨格で使用できない攻撃{motion}を除外しました");
                        continue;
                    }

                    if (attacks.Contains(motion))
                    {
                        continue;
                    }

                    attacks.Add(motion);
                    if (attacks.Count >= slotCount)
                    {
                        return attacks;
                    }
                }
            }

            return AttackMotionSelector.EnsureAttackSlots(attacks, usableAttacks, slotCount);
        }

        /// <summary>
        /// ModelPartLossのリム定義から使用可能部位を集める
        /// </summary>
        /// <param name="partLoss">部位欠損コントローラ</param>
        /// <returns>使用可能な部位</returns>
        public static HashSet<BonePart> CollectAvailableParts(ModelPartLossController partLoss)
        {
            return AttackMotionSelector.CollectAvailableParts(partLoss);
        }
    }
}
