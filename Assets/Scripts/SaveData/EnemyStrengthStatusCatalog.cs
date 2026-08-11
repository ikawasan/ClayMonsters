using Localization;
using UnityEngine;

namespace SaveData
{
    /// <summary>
    /// 敵モデルの強さ段階ごとのステータス解決
    /// 弱い=未育成を基準にし強い=育成完了程度へ伸ばす
    /// 敵ごとに特化やバランス型の個性を付ける
    /// </summary>
    public static class EnemyStrengthStatusCatalog
    {
        /// <summary>
        /// バランス改訂番号(不一致なら段階ステータスを再生成する)
        /// </summary>
        public const int BalanceVersion = 14;

        /// <summary>
        /// 弱い進行度
        /// 未育成基準からソフト目標への途中地点
        /// </summary>
        public const float WeakProgress = 0.55f;

        /// <summary>
        /// 普通進行度
        /// </summary>
        public const float NormalProgress = 1.00f;

        /// <summary>
        /// 強い進行度
        /// </summary>
        public const float StrongProgress = 1.35f;

        /// <summary>
        /// 超強い進行度
        /// </summary>
        public const float VeryStrongProgress = 1.60f;

        /// <summary>
        /// 最強進行度
        /// </summary>
        public const float StrongestProgress = 1.85f;

        /// <summary>
        /// 普通=100%時のHPソフト目標
        /// </summary>
        public const int SoftTargetHp = 280;

        /// <summary>
        /// 普通=100%時の攻撃ソフト目標
        /// ダメージ倍率0.6時に育成後5〜10発になるよう調整
        /// </summary>
        public const int SoftTargetAttack = 250;

        /// <summary>
        /// 普通=100%時の防御ソフト目標
        /// </summary>
        public const int SoftTargetDefense = 250;

        /// <summary>
        /// 強い=100%時の速さソフト目標
        /// </summary>
        public const int SoftTargetSpeed = 160;

        /// <summary>
        /// 強い=100%時の命中ソフト目標
        /// </summary>
        public const int SoftTargetHit = 160;

        /// <summary>
        /// 敵HP上限
        /// </summary>
        public const int MaxHp = 999;

        /// <summary>
        /// 敵攻撃上限
        /// </summary>
        public const int MaxAttack = 999;

        /// <summary>
        /// 敵防御上限
        /// </summary>
        public const int MaxDefense = 999;

        /// <summary>
        /// 敵速さ上限
        /// </summary>
        public const int MaxSpeed = 999;

        /// <summary>
        /// 敵命中上限
        /// </summary>
        public const int MaxHit = 999;

        // ModelStatusCalculatorの速さ範囲と揃える
        private const int ModelStatusCalculatorMinSpeed = ModelStatusDefaults.MinSpeed;
        private const int ModelStatusCalculatorMaxSpeed = ModelStatusDefaults.MaxSpeed;

        // 特化主ステの目標倍率
        private const float PrimaryWeight = 1.38f;

        // 特化副ステの目標倍率
        private const float SecondaryWeight = 1.22f;

        // 特化しないステの目標倍率
        private const float MinorWeight = 0.78f;

        // 形状差が小さいとみなすしきい値
        private const float FlatShapeThreshold = 0.10f;

        // 2ステ特化にする2位との差上限
        private const float DualSpecialtyGap = 0.14f;

        // シード選択用の定型個性(平均はNormalizeで1に戻す)
        private static readonly StatWeights[] SeededArchetypes =
        {
            new StatWeights(1.00f, 1.00f, 1.00f, 1.00f, 1.00f),
            new StatWeights(1.40f, 0.72f, 1.35f, 0.70f, 0.83f),
            new StatWeights(1.12f, 1.40f, 0.82f, 0.80f, 1.05f),
            new StatWeights(0.70f, 1.45f, 0.68f, 1.10f, 1.18f),
            new StatWeights(0.78f, 1.05f, 0.78f, 1.45f, 1.28f),
            new StatWeights(1.28f, 0.70f, 1.45f, 0.68f, 0.90f),
            new StatWeights(0.88f, 1.18f, 0.88f, 1.12f, 1.40f),
            new StatWeights(1.22f, 1.28f, 1.15f, 0.72f, 0.78f),
            new StatWeights(0.85f, 1.32f, 0.75f, 1.25f, 1.15f),
            new StatWeights(1.35f, 0.95f, 1.20f, 0.75f, 0.82f),
        };

        /// <summary>
        /// 強さ段階の表示名を返す
        /// </summary>
        /// <param name="tier">強さ段階</param>
        public static string GetDisplayName(EnemyStrengthTier tier)
        {
            return tier switch
            {
                EnemyStrengthTier.Weak => Localization.LocalizedText.GetOrFallback(
                    Localization.GameTextKeys.EnemyWeak,
                    "弱い"),
                EnemyStrengthTier.Normal => Localization.LocalizedText.GetOrFallback(
                    Localization.GameTextKeys.EnemyNormal,
                    "普通"),
                EnemyStrengthTier.Strong => Localization.LocalizedText.GetOrFallback(
                    Localization.GameTextKeys.EnemyStrong,
                    "強い"),
                EnemyStrengthTier.VeryStrong => Localization.LocalizedText.GetOrFallback(
                    Localization.GameTextKeys.EnemyVeryStrong,
                    "超強い"),
                EnemyStrengthTier.Strongest => Localization.LocalizedText.GetOrFallback(
                    Localization.GameTextKeys.EnemyStrongest,
                    "最強"),
                _ => Localization.LocalizedText.GetOrFallback(
                    Localization.GameTextKeys.EnemyNormal,
                    "普通"),
            };
        }

        /// <summary>
        /// NPC対戦で選択時に使う強さ段階
        /// 表示弱い→弱い/普通→普通/強い→超強い/超強い→最強
        /// </summary>
        public static readonly EnemyStrengthTier[] NpcSelectableTiers =
        {
            EnemyStrengthTier.Weak,
            EnemyStrengthTier.Normal,
            EnemyStrengthTier.VeryStrong,
            EnemyStrengthTier.Strongest,
        };

        /// <summary>
        /// 指定強さ段階のステータスを返す
        /// 未設定なら基準ステータスから生成する
        /// </summary>
        /// <param name="slot">敵スロット</param>
        /// <param name="tier">強さ段階</param>
        public static ModelStatus Resolve(ModelSaveSlot slot, EnemyStrengthTier tier)
        {
            if (slot == null)
            {
                return new ModelStatus();
            }

            EnsureFromBase(slot);
            ModelStatus selected = tier switch
            {
                EnemyStrengthTier.Weak => slot.statusWeak,
                EnemyStrengthTier.Normal => slot.statusNormal,
                EnemyStrengthTier.Strong => slot.statusStrong,
                EnemyStrengthTier.VeryStrong => slot.statusVeryStrong,
                EnemyStrengthTier.Strongest => slot.statusStrongest,
                _ => slot.statusNormal
            };

            return Clamp(ModelStatus.CloneOrDefault(selected));
        }

        /// <summary>
        /// 強さ段階ステータスが無ければ基準ステータスから用意する
        /// バランス改訂時は再生成する
        /// </summary>
        /// <param name="slot">敵スロット</param>
        public static void EnsureFromBase(ModelSaveSlot slot)
        {
            if (slot == null)
            {
                return;
            }

            if (slot.hasEnemyStrengthStatuses
                && HasCompleteSet(slot)
                && slot.enemyStrengthBalanceVersion == BalanceVersion)
            {
                ClampAllTiers(slot);
                return;
            }

            ModelStatus untrainedBase = ResolveUntrainedBase(slot);
            ApplyGeneratedFromBase(slot, untrainedBase);
        }

        /// <summary>
        /// 未育成基準ステータスから各段階を書き込む
        /// </summary>
        /// <param name="slot">敵スロット</param>
        /// <param name="baseStatus">未育成ステータス(弱い相当)</param>
        public static void ApplyGeneratedFromBase(ModelSaveSlot slot, ModelStatus baseStatus)
        {
            if (slot == null)
            {
                return;
            }

            ModelStatus source = NormalizeUntrainedBase(baseStatus);
            StatWeights weights = ResolvePersonalityWeights(slot, source);
            slot.statusWeak = BuildTier(source, WeakProgress, weights);
            slot.statusNormal = BuildTier(source, NormalProgress, weights);
            slot.statusStrong = BuildTier(source, StrongProgress, weights);
            slot.statusVeryStrong = BuildTier(source, VeryStrongProgress, weights);
            slot.statusStrongest = BuildTier(source, StrongestProgress, weights);
            // 表示・基準用は弱い段階を保持する
            slot.status = ModelStatus.CloneOrDefault(slot.statusWeak);
            slot.hasEnemyStrengthStatuses = true;
            slot.enemyStrengthBalanceVersion = BalanceVersion;
        }

        // 旧v1のみ普通=未育成v2以降は弱い=未育成
        private static ModelStatus ResolveUntrainedBase(ModelSaveSlot slot)
        {
            if (slot.hasEnemyStrengthStatuses
                && HasCompleteSet(slot)
                && slot.enemyStrengthBalanceVersion < 2
                && HasAnyValue(slot.statusNormal))
            {
                return slot.statusNormal;
            }

            if (slot.hasEnemyStrengthStatuses && HasAnyValue(slot.statusWeak))
            {
                return slot.statusWeak;
            }

            return slot.status;
        }

        /// <summary>
        /// 未育成基準を作成時の形状反映範囲へ収める
        /// 旧データで膨らんだHPなどを再調整する
        /// </summary>
        private static ModelStatus NormalizeUntrainedBase(ModelStatus source)
        {
            ModelStatus status = ModelStatus.CloneOrDefault(source);
            status.hp = NormalizeCreationStat(
                status.hp,
                ModelStatusDefaults.MinHp,
                ModelStatusDefaults.MaxHp,
                ModelStatusDefaults.DefaultHp);
            status.attack = NormalizeCreationStat(
                status.attack,
                ModelStatusDefaults.MinAttack,
                ModelStatusDefaults.MaxAttack,
                ModelStatusDefaults.DefaultAttack);
            status.defense = NormalizeCreationStat(
                status.defense,
                ModelStatusDefaults.MinDefense,
                ModelStatusDefaults.MaxDefense,
                ModelStatusDefaults.DefaultDefense);
            status.speed = NormalizeCreationStat(
                status.speed,
                ModelStatusCalculatorMinSpeed,
                ModelStatusCalculatorMaxSpeed,
                ModelStatusDefaults.DefaultSpeed);
            status.hit = NormalizeCreationStat(
                status.hit,
                ModelStatusDefaults.MinHit,
                ModelStatusDefaults.MaxHit,
                ModelStatusDefaults.DefaultHit);
            return status;
        }

        private static int NormalizeCreationStat(int value, int min, int max, int defaultValue)
        {
            if (value <= 0)
            {
                return defaultValue;
            }

            return Mathf.Clamp(value, min, max);
        }

        /// <summary>
        /// 形状の偏りと固定シードから個性ウェイトを決める
        /// </summary>
        private static StatWeights ResolvePersonalityWeights(ModelSaveSlot slot, ModelStatus baseStatus)
        {
            float hp = Normalize01(
                baseStatus.hp,
                ModelStatusDefaults.MinHp,
                ModelStatusDefaults.MaxHp);
            float attack = Normalize01(
                baseStatus.attack,
                ModelStatusDefaults.MinAttack,
                ModelStatusDefaults.MaxAttack);
            float defense = Normalize01(
                baseStatus.defense,
                ModelStatusDefaults.MinDefense,
                ModelStatusDefaults.MaxDefense);
            float speed = Normalize01(
                baseStatus.speed,
                ModelStatusCalculatorMinSpeed,
                ModelStatusCalculatorMaxSpeed);
            float hit = Normalize01(
                baseStatus.hit,
                ModelStatusDefaults.MinHit,
                ModelStatusDefaults.MaxHit);

            float[] scores = { hp, attack, defense, speed, hit };
            int primary = IndexOfMax(scores, exclude: -1);
            int secondary = IndexOfMax(scores, exclude: primary);
            float average = (hp + attack + defense + speed + hit) * 0.2f;
            float spread = scores[primary] - average;
            int seed = BuildPersonalitySeed(slot, baseStatus);

            // 形状がほぼフラットなら定型個性から選びバランス型も含める
            if (spread < FlatShapeThreshold)
            {
                int archetypeIndex = Mod(seed, SeededArchetypes.Length);
                return SeededArchetypes[archetypeIndex].Normalized();
            }

            float[] weights = { MinorWeight, MinorWeight, MinorWeight, MinorWeight, MinorWeight };
            weights[primary] = PrimaryWeight;
            if (scores[primary] - scores[secondary] <= DualSpecialtyGap)
            {
                weights[secondary] = SecondaryWeight;
            }
            else if (Mod(seed, 3) == 0)
            {
                // 単特化が多いが時々副特化を付ける
                weights[secondary] = SecondaryWeight;
            }

            // シードで弱いゆらぎを加え同型クローン感を減らす
            ApplySeedJitter(weights, seed);
            return new StatWeights(
                weights[0],
                weights[1],
                weights[2],
                weights[3],
                weights[4]).Normalized();
        }

        private static void ApplySeedJitter(float[] weights, int seed)
        {
            if (weights == null || weights.Length == 0)
            {
                return;
            }

            for (int i = 0; i < weights.Length; i++)
            {
                int unit = Mod(seed + (i * 37), 11) - 5;
                weights[i] *= 1f + (unit * 0.012f);
            }
        }

        private static int BuildPersonalitySeed(ModelSaveSlot slot, ModelStatus baseStatus)
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + StableStringHash(slot != null ? slot.modelName : null);
                hash = (hash * 31) + StableStringHash(slot != null ? slot.glbFileName : null);
                if (baseStatus != null)
                {
                    hash = (hash * 31) + baseStatus.hp;
                    hash = (hash * 31) + (baseStatus.attack * 17);
                    hash = (hash * 31) + (baseStatus.defense * 23);
                    hash = (hash * 31) + (baseStatus.speed * 29);
                    hash = (hash * 31) + (baseStatus.hit * 31);
                }

                return hash;
            }
        }

        private static int StableStringHash(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return 0;
            }

            unchecked
            {
                int hash = 23;
                for (int i = 0; i < value.Length; i++)
                {
                    hash = (hash * 31) + value[i];
                }

                return hash;
            }
        }

        private static float Normalize01(int value, int min, int max)
        {
            if (max <= min)
            {
                return 0f;
            }

            return Mathf.Clamp01((value - min) / (float)(max - min));
        }

        private static int IndexOfMax(float[] scores, int exclude)
        {
            int best = exclude == 0 ? 1 : 0;
            float bestScore = float.MinValue;
            for (int i = 0; i < scores.Length; i++)
            {
                if (i == exclude)
                {
                    continue;
                }

                if (scores[i] > bestScore)
                {
                    bestScore = scores[i];
                    best = i;
                }
            }

            return best;
        }

        private static int Mod(int value, int modulo)
        {
            if (modulo <= 0)
            {
                return 0;
            }

            int result = value % modulo;
            return result < 0 ? result + modulo : result;
        }

        private static ModelStatus BuildTier(
            ModelStatus untrainedBase,
            float progress,
            StatWeights weights)
        {
            ModelStatus status = new ModelStatus
            {
                hp = LerpTowardTarget(
                    untrainedBase.hp,
                    ScaleSoftTarget(SoftTargetHp, weights.Hp),
                    progress,
                    MaxHp),
                attack = LerpTowardTarget(
                    untrainedBase.attack,
                    ScaleSoftTarget(SoftTargetAttack, weights.Attack),
                    progress,
                    MaxAttack),
                defense = LerpTowardTarget(
                    untrainedBase.defense,
                    ScaleSoftTarget(SoftTargetDefense, weights.Defense),
                    progress,
                    MaxDefense),
                speed = LerpTowardTarget(
                    untrainedBase.speed,
                    ScaleSoftTarget(SoftTargetSpeed, weights.Speed),
                    progress,
                    MaxSpeed),
                hit = LerpTowardTarget(
                    untrainedBase.hit,
                    ScaleSoftTarget(SoftTargetHit, weights.Hit),
                    progress,
                    MaxHit)
            };
            return Clamp(status);
        }

        private static int ScaleSoftTarget(int softTarget, float weight)
        {
            return Mathf.Max(1, Mathf.RoundToInt(softTarget * Mathf.Max(0.1f, weight)));
        }

        private static int LerpTowardTarget(int baseValue, int softTarget, float progress, int hardMax)
        {
            if (baseValue <= 0)
            {
                return baseValue;
            }

            int target = Mathf.Max(baseValue, softTarget);
            int lerped = Mathf.RoundToInt(Mathf.LerpUnclamped(baseValue, target, progress));
            return Mathf.Clamp(lerped, 1, hardMax);
        }

        private static void ClampAllTiers(ModelSaveSlot slot)
        {
            slot.status = Clamp(slot.status);
            slot.statusWeak = Clamp(slot.statusWeak);
            slot.statusNormal = Clamp(slot.statusNormal);
            slot.statusStrong = Clamp(slot.statusStrong);
            slot.statusVeryStrong = Clamp(slot.statusVeryStrong);
            slot.statusStrongest = Clamp(slot.statusStrongest);
        }

        private static bool HasCompleteSet(ModelSaveSlot slot)
        {
            return HasAnyValue(slot.statusWeak)
                && HasAnyValue(slot.statusNormal)
                && HasAnyValue(slot.statusStrong)
                && HasAnyValue(slot.statusVeryStrong)
                && HasAnyValue(slot.statusStrongest);
        }

        private static bool HasAnyValue(ModelStatus status)
        {
            return status != null
                && (status.hp > 0
                    || status.attack > 0
                    || status.defense > 0
                    || status.speed > 0
                    || status.hit > 0);
        }

        private static ModelStatus Clamp(ModelStatus source)
        {
            ModelStatus status = ModelStatus.CloneOrDefault(source);
            if (status.hp > 0)
            {
                status.hp = Mathf.Min(status.hp, MaxHp);
            }

            if (status.attack > 0)
            {
                status.attack = Mathf.Min(status.attack, MaxAttack);
            }

            if (status.defense > 0)
            {
                status.defense = Mathf.Min(status.defense, MaxDefense);
            }

            if (status.speed > 0)
            {
                status.speed = Mathf.Min(status.speed, MaxSpeed);
            }

            if (status.hit > 0)
            {
                status.hit = Mathf.Min(status.hit, MaxHit);
            }

            return status;
        }

        /// <summary>
        /// 各ステ目標倍率
        /// </summary>
        private readonly struct StatWeights
        {
            public StatWeights(float hp, float attack, float defense, float speed, float hit)
            {
                Hp = hp;
                Attack = attack;
                Defense = defense;
                Speed = speed;
                Hit = hit;
            }

            public float Hp { get; }
            public float Attack { get; }
            public float Defense { get; }
            public float Speed { get; }
            public float Hit { get; }

            /// <summary>
            /// 平均倍率を1に正規化し総合戦力を揃える
            /// </summary>
            public StatWeights Normalized()
            {
                float mean = (Hp + Attack + Defense + Speed + Hit) * 0.2f;
                if (mean <= 0.0001f)
                {
                    return new StatWeights(1f, 1f, 1f, 1f, 1f);
                }

                return new StatWeights(
                    Hp / mean,
                    Attack / mean,
                    Defense / mean,
                    Speed / mean,
                    Hit / mean);
            }
        }
    }
}
