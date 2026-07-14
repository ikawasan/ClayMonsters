using UnityEngine;

namespace Battle
{
    /// <summary>
    /// モンスターファーム風の3段階間合い
    /// </summary>
    public enum BattleDistanceBand
    {
        /// <summary>
        /// 近距離
        /// </summary>
        Close,
        /// <summary>
        /// 中距離
        /// </summary>
        Mid,
        /// <summary>
        /// 遠距離
        /// </summary>
        Far
    }

    /// <summary>
    /// 連続間合い値を距離帯へ変換する
    /// </summary>
    public static class BattleDistanceBandResolver
    {
        /// <summary>
        /// 間合いから距離帯を返す
        /// </summary>
        public static BattleDistanceBand Resolve(float distance, float maxDistance)
        {
            if (maxDistance <= 0f)
            {
                return BattleDistanceBand.Close;
            }

            float ratio = Mathf.Clamp01(distance / maxDistance);
            if (ratio <= 0.33f)
            {
                return BattleDistanceBand.Close;
            }

            if (ratio <= 0.66f)
            {
                return BattleDistanceBand.Mid;
            }

            return BattleDistanceBand.Far;
        }

        /// <summary>
        /// 距離帯の表示名を返す
        /// </summary>
        public static string ToDisplayName(BattleDistanceBand band)
        {
            switch (band)
            {
                case BattleDistanceBand.Close: return "近距離";
                case BattleDistanceBand.Mid: return "中距離";
                case BattleDistanceBand.Far: return "遠距離";
                default: return string.Empty;
            }
        }
    }
}
