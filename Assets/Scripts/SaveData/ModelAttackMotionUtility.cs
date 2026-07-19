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
        /// </summary>
        /// <param name="attackMotions">元の攻撃モーション</param>
        /// <param name="slotCount">スロット数</param>
        /// <returns>正規化済みリスト</returns>
        public static List<MotionType> Normalize(
            IReadOnlyList<MotionType> attackMotions,
            int slotCount = SlotCount)
        {
            List<MotionType> attacks = attackMotions != null
                ? new List<MotionType>(attackMotions)
                : new List<MotionType>();
            int count = Mathf.Min(attacks.Count, slotCount);
            if (count < attacks.Count)
            {
                attacks.RemoveRange(count, attacks.Count - count);
            }

            while (attacks.Count < slotCount)
            {
                attacks.Add(MotionType.Punch);
            }

            return attacks;
        }
    }
}
