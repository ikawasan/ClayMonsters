using UnityEngine;

namespace Battle
{
    /// <summary>
    /// モンスターファーム風の命中・チェーン補正
    /// </summary>
    public static class BattleCombatRules
    {
        /// <summary>
        /// ガッツ量と命中ステータスに応じた命中率を返す
        /// </summary>
        public static float ComputeHitRate(float baseAccuracy, float guts, float maxGuts, int hit)
        {
            float gutsRatio = maxGuts > 0f ? Mathf.Clamp01(guts / maxGuts) : 0f;
            float hitMultiplier = Mathf.Clamp(
                hit / (float)BattleStatusBalance.DefaultHit,
                0.7f,
                1.4f);
            return Mathf.Clamp01(baseAccuracy * (0.55f + 0.45f * gutsRatio) * hitMultiplier);
        }

        /// <summary>
        /// チェーンボーナス込みの威力倍率を返す
        /// </summary>
        public static float ComputeChainPowerMultiplier(int chainCount)
        {
            if (chainCount <= 1)
            {
                return 1f;
            }

            return 1f + (chainCount - 1) * 0.2f;
        }

        /// <summary>
        /// 命中率を4段階ラベルへ変換する
        /// </summary>
        public static string ToHitRateLabel(float hitRate)
        {
            if (hitRate >= 0.85f)
            {
                return Localization.LocalizedText.GetOrFallback(
                    Localization.GameTextKeys.BattleHitHigh, "高");
            }

            if (hitRate >= 0.65f)
            {
                return Localization.LocalizedText.GetOrFallback(
                    Localization.GameTextKeys.BattleHitMid, "中");
            }

            if (hitRate >= 0.4f)
            {
                return Localization.LocalizedText.GetOrFallback(
                    Localization.GameTextKeys.BattleHitLow, "低");
            }

            return Localization.LocalizedText.GetOrFallback(
                Localization.GameTextKeys.BattleHitVeryLow, "極低");
        }
    }
}
