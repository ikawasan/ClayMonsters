using UnityEngine;

namespace Battle
{
    /// <summary>
    /// モンスターファーム風の命中・チェーン補正
    /// </summary>
    public static class BattleCombatRules
    {
        /// <summary>
        /// ガッツ量に応じた命中率を返す
        /// </summary>
        public static float ComputeHitRate(float baseAccuracy, float guts, float maxGuts)
        {
            float gutsRatio = maxGuts > 0f ? Mathf.Clamp01(guts / maxGuts) : 0f;
            return Mathf.Clamp01(baseAccuracy * (0.55f + 0.45f * gutsRatio));
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
                return "高";
            }

            if (hitRate >= 0.65f)
            {
                return "中";
            }

            if (hitRate >= 0.4f)
            {
                return "低";
            }

            return "極低";
        }
    }
}
