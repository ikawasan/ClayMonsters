using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 戦闘のレベルデザイン用パラメータ
    /// 間合い・ステップ・ガッツ・AIテンポなどをInspectorで調整する
    /// </summary>
    [CreateAssetMenu(fileName = "BattleLevelDesignSettings", menuName = "ClayMonsters/BattleLevelDesignSettings")]
    public sealed class BattleLevelDesignSettings : ScriptableObject
    {
        private const string DefaultAssetPath = "Assets/Scripts/StaticResources/BattleLevelDesignSettings.asset";

        [Header("Distance")]
        [Tooltip("最大間合い")]
        [SerializeField] private float maxDistance = 10f;
        [Tooltip("密着時の最小間隔")]
        [SerializeField] private float minCloseSeparation = 0.6f;

        [Header("Time And Guts")]
        [Tooltip("制限時間(秒)")]
        [SerializeField] private float timeLimit = 60f;
        [Tooltip("開幕ガッツ")]
        [SerializeField] private float initialGuts = 50f;
        [Tooltip("ガッツ回復速度(/秒)攻撃中は停止")]
        [SerializeField] private float gutsGainPerSecond = 7f;

        [Header("Knockback")]
        [Tooltip("ふきとばし可能な最大間合い")]
        [SerializeField] private float knockbackCloseThreshold = 2.5f;
        [Tooltip("ふきとばしで押し出す距離")]
        [SerializeField] private float knockbackPushDistance = 3.5f;
        [Tooltip("ふきとばしのガッツ消費")]
        [SerializeField] private float knockbackGutsCost = 15f;
        [Tooltip("ふきとばし硬直(秒)")]
        [SerializeField] private float knockbackRecovery = 0.5f;

        [Header("Combo And Counter")]
        [Tooltip("攻撃演出終了後に双方が攻撃不可になる時間(秒)")]
        [SerializeField] private float postAttackLockoutDuration = 3f;
        [Tooltip("チェーン継続猶予(秒)")]
        [SerializeField] private float chainBonusWindow = 1.2f;
        [Tooltip("カウンター時ダメージ倍率")]
        [SerializeField] private float counterDamageMultiplier = 1.25f;
        [Tooltip("ダメージ全体倍率(育成後想定で5〜10発決着になる値)")]
        [SerializeField] private float damageScale = 0.6f;

        [Header("Enemy Ai")]
        [Tooltip("敵攻撃予兆時間(秒)")]
        [SerializeField] private float enemyAttackTelegraphDuration = 0.65f;
        [Tooltip("敵攻撃クールダウン最小(秒)")]
        [SerializeField] private float enemyAttackCooldownMin = 1.8f;
        [Tooltip("敵攻撃クールダウン最大(秒)")]
        [SerializeField] private float enemyAttackCooldownMax = 3.2f;

        [Header("Step")]
        [Tooltip("既定速度時の1回ステップ移動距離")]
        [SerializeField] private float stepDistance = 4f;
        [Tooltip("ステップ移動時間(秒)")]
        [SerializeField] private float stepDuration = 0.25f;
        [Tooltip("ステップクールダウン(秒)")]
        [SerializeField] private float stepCooldown = 1.2f;

        [Header("Part Repair")]
        [Tooltip("部位1本あたりの修理時間(秒)")]
        [SerializeField] private float partRepairSecondsPerLimb = 3f;

        [Header("Move Recast")]
        [Tooltip("技リキャストの基準秒数(参照速度100時速度が速いほど短縮)")]
        [SerializeField] private float moveRecastBaseSeconds = 14f;

        /// <summary>
        /// ランタイム用BattleSettingsへ変換する
        /// </summary>
        public BattleSettings ToRuntimeSettings()
        {
            return new BattleSettings
            {
                MaxDistance = maxDistance,
                TimeLimit = timeLimit,
                InitialGuts = initialGuts,
                GutsGainPerSecond = gutsGainPerSecond,
                KnockbackCloseThreshold = knockbackCloseThreshold,
                KnockbackPushDistance = knockbackPushDistance,
                KnockbackGutsCost = knockbackGutsCost,
                KnockbackRecovery = knockbackRecovery,
                PostAttackLockoutDuration = postAttackLockoutDuration,
                ChainBonusWindow = chainBonusWindow,
                EnemyAttackTelegraphDuration = enemyAttackTelegraphDuration,
                EnemyAttackCooldownMin = enemyAttackCooldownMin,
                EnemyAttackCooldownMax = enemyAttackCooldownMax,
                CounterDamageMultiplier = counterDamageMultiplier,
                DamageScale = damageScale,
                StepDistance = stepDistance,
                StepDuration = stepDuration,
                StepCooldown = stepCooldown,
                MinCloseSeparation = minCloseSeparation,
                PartRepairSecondsPerLimb = partRepairSecondsPerLimb,
                MoveRecastBaseSeconds = moveRecastBaseSeconds > 0f
                    ? moveRecastBaseSeconds
                    : BattleCombatRules.MoveRecastBaseSeconds
            };
        }

        /// <summary>
        /// 指定アセットまたは既定アセットからランタイム設定を解決する
        /// </summary>
        /// <param name="asset">シーンから渡された設定</param>
        public static BattleSettings Resolve(BattleLevelDesignSettings asset)
        {
            if (asset != null)
            {
                return asset.ToRuntimeSettings();
            }

            BattleLevelDesignSettings fallback = LoadDefaultAsset();
            return fallback != null
                ? fallback.ToRuntimeSettings()
                : CreateInstance<BattleLevelDesignSettings>().ToRuntimeSettings();
        }

        private static BattleLevelDesignSettings LoadDefaultAsset()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<BattleLevelDesignSettings>(DefaultAssetPath);
#else
            return null;
#endif
        }
    }
}
