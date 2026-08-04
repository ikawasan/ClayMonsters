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
        /// 近距離帯の上限(この値以下が近距離)
        /// </summary>
        public const float CloseMaxDistance = 3f;

        /// <summary>
        /// 中距離帯の上限(この値以下が中距離超えは遠距離)
        /// </summary>
        public const float MidMaxDistance = 6f;

        /// <summary>
        /// 距離帯セグメント数(近中遠)
        /// </summary>
        public const int BandCount = 3;

        /// <summary>
        /// 射程計算の既定最大間合い
        /// </summary>
        public const float DefaultMaxDistance = 10f;

        /// <summary>
        /// 間合いから距離帯を返す
        /// </summary>
        public static BattleDistanceBand Resolve(float distance, float maxDistance)
        {
            if (maxDistance <= 0f)
            {
                return BattleDistanceBand.Close;
            }

            float closeMax = GetCloseMax(maxDistance);
            if (distance <= closeMax)
            {
                return BattleDistanceBand.Close;
            }

            float midMax = GetMidMax(maxDistance);
            if (distance <= midMax)
            {
                return BattleDistanceBand.Mid;
            }

            return BattleDistanceBand.Far;
        }

        /// <summary>
        /// 指定セグメントの下限距離を返す
        /// </summary>
        /// <param name="segmentIndex">0=近1=中2=遠</param>
        /// <param name="maxDistance">最大間合い</param>
        public static float GetSegmentLower(int segmentIndex, float maxDistance)
        {
            if (maxDistance <= 0f || segmentIndex <= 0)
            {
                return 0f;
            }

            if (segmentIndex == 1)
            {
                return GetCloseMax(maxDistance);
            }

            return GetMidMax(maxDistance);
        }

        /// <summary>
        /// 指定セグメントの上限距離を返す
        /// </summary>
        /// <param name="segmentIndex">0=近1=中2=遠</param>
        /// <param name="maxDistance">最大間合い</param>
        public static float GetSegmentUpper(int segmentIndex, float maxDistance)
        {
            if (maxDistance <= 0f)
            {
                return 0f;
            }

            if (segmentIndex <= 0)
            {
                return GetCloseMax(maxDistance);
            }

            if (segmentIndex == 1)
            {
                return GetMidMax(maxDistance);
            }

            return maxDistance;
        }

        /// <summary>
        /// 距離帯の表示名を返す
        /// </summary>
        public static string ToDisplayName(BattleDistanceBand band)
        {
            switch (band)
            {
                case BattleDistanceBand.Close:
                    return Localization.LocalizedText.GetOrFallback(
                        Localization.GameTextKeys.BattleBandClose, "近距離");
                case BattleDistanceBand.Mid:
                    return Localization.LocalizedText.GetOrFallback(
                        Localization.GameTextKeys.BattleBandMid, "中距離");
                case BattleDistanceBand.Far:
                    return Localization.LocalizedText.GetOrFallback(
                        Localization.GameTextKeys.BattleBandFar, "遠距離");
                default:
                    return string.Empty;
            }
        }

        private static float GetCloseMax(float maxDistance)
        {
            return Mathf.Min(CloseMaxDistance, maxDistance);
        }

        private static float GetMidMax(float maxDistance)
        {
            float closeMax = GetCloseMax(maxDistance);
            if (maxDistance <= closeMax)
            {
                return maxDistance;
            }

            float midMax = Mathf.Min(MidMaxDistance, maxDistance);
            return Mathf.Max(midMax, closeMax);
        }
    }
}
