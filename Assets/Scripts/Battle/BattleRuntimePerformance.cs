using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 戦闘中だけ影距離などを絞ってGPU負荷を抑える
    /// </summary>
    public static class BattleRuntimePerformance
    {
        private const float BattleShadowDistance = 22f;
        private const int BattleShadowCascades = 2;
        private const int BattleParticleBudget = 512;
        private const float BattleLodBias = 0.7f;

        private static bool applied;
        private static float savedShadowDistance;
        private static int savedShadowCascades;
        private static ShadowResolution savedShadowResolution;
        private static int savedParticleBudget;
        private static float savedLodBias;
        private static int applyCount;

        /// <summary>
        /// 戦闘向け品質を適用する
        /// </summary>
        public static void EnterBattle()
        {
            applyCount++;
            if (applied)
            {
                return;
            }

            savedShadowDistance = QualitySettings.shadowDistance;
            savedShadowCascades = QualitySettings.shadowCascades;
            savedShadowResolution = QualitySettings.shadowResolution;
            savedParticleBudget = QualitySettings.particleRaycastBudget;
            savedLodBias = QualitySettings.lodBias;

            QualitySettings.shadowDistance = Mathf.Min(savedShadowDistance, BattleShadowDistance);
            if (savedShadowCascades > BattleShadowCascades)
            {
                QualitySettings.shadowCascades = BattleShadowCascades;
            }

            if (savedShadowResolution > ShadowResolution.Medium)
            {
                QualitySettings.shadowResolution = ShadowResolution.Medium;
            }

            if (savedParticleBudget > BattleParticleBudget)
            {
                QualitySettings.particleRaycastBudget = BattleParticleBudget;
            }

            QualitySettings.lodBias = Mathf.Min(savedLodBias, BattleLodBias);

            applied = true;
        }

        /// <summary>
        /// 戦闘向け品質を元に戻す
        /// </summary>
        public static void ExitBattle()
        {
            if (applyCount > 0)
            {
                applyCount--;
            }

            if (!applied || applyCount > 0)
            {
                return;
            }

            QualitySettings.shadowDistance = savedShadowDistance;
            QualitySettings.shadowCascades = savedShadowCascades;
            QualitySettings.shadowResolution = savedShadowResolution;
            QualitySettings.particleRaycastBudget = savedParticleBudget;
            QualitySettings.lodBias = savedLodBias;
            applied = false;
            BattleFieldFocusResolver.PrunePosedBoundsCache();
            View.BattleModelBoundsCache.Prune();
        }
    }
}
