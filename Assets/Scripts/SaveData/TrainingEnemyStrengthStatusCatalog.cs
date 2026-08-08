using UnityEngine;

namespace SaveData
{
    /// <summary>
    /// 育成モード専用の敵ステータス解決
    /// NPC対戦の強さ段階とは別カーブで弱めに調整する
    /// </summary>
    public static class TrainingEnemyStrengthStatusCatalog
    {
        /// <summary>
        /// 弱い進行度(作成時基準そのまま)
        /// </summary>
        public const float WeakProgress = 0f;

        /// <summary>
        /// 普通進行度(ソフト目標への半分)
        /// </summary>
        public const float NormalProgress = 0.5f;

        /// <summary>
        /// 強い進行度(ソフト目標到達)
        /// </summary>
        public const float StrongProgress = 1f;

        /// <summary>
        /// 最強進行度(ソフト目標を超えて伸ばす)
        /// </summary>
        public const float StrongestProgress = 2f;

        /// <summary>
        /// 育成普通相当のHPソフト目標
        /// NPCの280より低く育成序盤向けに抑える
        /// </summary>
        public const int SoftTargetHp = 150;

        /// <summary>
        /// 育成攻撃ソフト目標
        /// </summary>
        public const int SoftTargetAttack = 70;

        /// <summary>
        /// 育成防御ソフト目標
        /// </summary>
        public const int SoftTargetDefense = 70;

        /// <summary>
        /// 育成速さソフト目標
        /// </summary>
        public const int SoftTargetSpeed = 16;

        /// <summary>
        /// 育成命中ソフト目標
        /// </summary>
        public const int SoftTargetHit = 20;

        private const int CreationMinSpeed = 6;
        private const int CreationMaxSpeed = 18;

        /// <summary>
        /// 強さ段階の表示名を返す
        /// </summary>
        /// <param name="tier">育成強さ段階</param>
        public static string GetDisplayName(TrainingEnemyStrengthTier tier)
        {
            return tier switch
            {
                TrainingEnemyStrengthTier.Weak => Localization.LocalizedText.GetOrFallback(
                    Localization.GameTextKeys.EnemyWeak,
                    "弱い"),
                TrainingEnemyStrengthTier.Normal => Localization.LocalizedText.GetOrFallback(
                    Localization.GameTextKeys.EnemyNormal,
                    "普通"),
                TrainingEnemyStrengthTier.Strong => Localization.LocalizedText.GetOrFallback(
                    Localization.GameTextKeys.EnemyStrong,
                    "強い"),
                TrainingEnemyStrengthTier.Strongest => Localization.LocalizedText.GetOrFallback(
                    Localization.GameTextKeys.EnemyStrongest,
                    "最強"),
                _ => Localization.LocalizedText.GetOrFallback(
                    Localization.GameTextKeys.EnemyNormal,
                    "普通"),
            };
        }

        /// <summary>
        /// 育成強さ段階からステータスを解決する
        /// 作成時形状範囲を基準に育成専用カーブで伸ばす
        /// </summary>
        /// <param name="slot">敵スロット</param>
        /// <param name="tier">育成強さ段階</param>
        public static ModelStatus Resolve(ModelSaveSlot slot, TrainingEnemyStrengthTier tier)
        {
            if (slot == null)
            {
                return new ModelStatus();
            }

            ModelStatus baseStatus = ResolveCreationBase(slot);
            float progress = ResolveProgress(tier);
            return BuildTier(baseStatus, progress);
        }

        /// <summary>
        /// 技抽選用にNPC段階へ控えめに写像する
        /// 育成最強でもNPCの強い相当まで
        /// </summary>
        /// <param name="tier">育成強さ段階</param>
        public static EnemyStrengthTier ToAttackSelectionTier(TrainingEnemyStrengthTier tier)
        {
            return tier switch
            {
                TrainingEnemyStrengthTier.Weak => EnemyStrengthTier.Weak,
                TrainingEnemyStrengthTier.Normal => EnemyStrengthTier.Weak,
                TrainingEnemyStrengthTier.Strong => EnemyStrengthTier.Normal,
                TrainingEnemyStrengthTier.Strongest => EnemyStrengthTier.Strong,
                _ => EnemyStrengthTier.Weak
            };
        }

        /// <summary>
        /// AI用のNPC段階へ写像する
        /// 育成側は全体的に一段弱く扱う
        /// </summary>
        /// <param name="tier">育成強さ段階</param>
        public static EnemyStrengthTier ToAiTier(TrainingEnemyStrengthTier tier)
        {
            return ToAttackSelectionTier(tier);
        }

        private static float ResolveProgress(TrainingEnemyStrengthTier tier)
        {
            return tier switch
            {
                TrainingEnemyStrengthTier.Weak => WeakProgress,
                TrainingEnemyStrengthTier.Normal => NormalProgress,
                TrainingEnemyStrengthTier.Strong => StrongProgress,
                TrainingEnemyStrengthTier.Strongest => StrongestProgress,
                _ => NormalProgress
            };
        }

        // 作成時の形状反映範囲へ収めた基準値を返す
        private static ModelStatus ResolveCreationBase(ModelSaveSlot slot)
        {
            ModelStatus source = ModelStatus.CloneOrDefault(slot.status);
            // NPC段階で膨らんだ値でも作成範囲へ戻し育成基準として使う
            source.hp = NormalizeCreationStat(
                source.hp,
                ModelStatusDefaults.MinHp,
                ModelStatusDefaults.MaxHp,
                ModelStatusDefaults.DefaultHp);
            source.attack = NormalizeCreationStat(
                source.attack,
                ModelStatusDefaults.MinAttack,
                ModelStatusDefaults.MaxAttack,
                ModelStatusDefaults.DefaultAttack);
            source.defense = NormalizeCreationStat(
                source.defense,
                ModelStatusDefaults.MinDefense,
                ModelStatusDefaults.MaxDefense,
                ModelStatusDefaults.DefaultDefense);
            source.speed = NormalizeCreationStat(
                source.speed,
                CreationMinSpeed,
                CreationMaxSpeed,
                ModelStatusDefaults.DefaultSpeed);
            source.hit = NormalizeCreationStat(
                source.hit,
                ModelStatusDefaults.MinHit,
                ModelStatusDefaults.MaxHit,
                ModelStatusDefaults.DefaultHit);
            return source;
        }

        private static int NormalizeCreationStat(int value, int min, int max, int defaultValue)
        {
            if (value <= 0)
            {
                return defaultValue;
            }

            return Mathf.Clamp(value, min, max);
        }

        private static ModelStatus BuildTier(ModelStatus untrainedBase, float progress)
        {
            return new ModelStatus
            {
                hp = LerpTowardTarget(untrainedBase.hp, SoftTargetHp, progress),
                attack = LerpTowardTarget(untrainedBase.attack, SoftTargetAttack, progress),
                defense = LerpTowardTarget(untrainedBase.defense, SoftTargetDefense, progress),
                speed = LerpTowardTarget(untrainedBase.speed, SoftTargetSpeed, progress),
                hit = LerpTowardTarget(untrainedBase.hit, SoftTargetHit, progress)
            };
        }

        private static int LerpTowardTarget(int baseValue, int softTarget, float progress)
        {
            if (baseValue <= 0)
            {
                return baseValue;
            }

            int target = Mathf.Max(baseValue, softTarget);
            int lerped = Mathf.RoundToInt(Mathf.LerpUnclamped(baseValue, target, progress));
            return Mathf.Max(1, lerped);
        }
    }
}
