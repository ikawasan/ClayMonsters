namespace SaveData
{
    /// <summary>
    /// 敵モデルの強さ段階ごとのステータス解決
    /// 弱い=未育成を基準にし強い=育成完了程度へ伸ばす
    /// </summary>
    public static class EnemyStrengthStatusCatalog
    {
        /// <summary>
        /// バランス改訂番号(不一致なら段階ステータスを再生成する)
        /// </summary>
        public const int BalanceVersion = 4;

        /// <summary>
        /// 弱い倍率(未育成=基準)
        /// </summary>
        public const float WeakScale = 1f;

        /// <summary>
        /// 普通倍率(弱い比・育成中盤程度)
        /// </summary>
        public const float NormalScale = 1.3f;

        /// <summary>
        /// 強い倍率(弱い比・育成完了程度)
        /// </summary>
        public const float StrongScale = 1.7f;

        /// <summary>
        /// 超強い倍率(弱い比・継承込みでも超えられる余白)
        /// </summary>
        public const float VeryStrongScale = 2.1f;

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

        /// <summary>
        /// 強さ段階の表示名を返す
        /// </summary>
        /// <param name="tier">強さ段階</param>
        public static string GetDisplayName(EnemyStrengthTier tier)
        {
            return tier switch
            {
                EnemyStrengthTier.Weak => "弱い",
                EnemyStrengthTier.Normal => "普通",
                EnemyStrengthTier.Strong => "強い",
                EnemyStrengthTier.VeryStrong => "超強い",
                _ => "普通"
            };
        }

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
                _ => slot.statusNormal
            };

            return Clamp(ModelStatus.CloneOrDefault(selected));
        }

        /// <summary>
        /// 4段階ステータスが無ければ基準ステータスから用意する
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
        /// 未育成基準ステータスから4段階を書き込む
        /// </summary>
        /// <param name="slot">敵スロット</param>
        /// <param name="baseStatus">未育成ステータス(弱い相当)</param>
        public static void ApplyGeneratedFromBase(ModelSaveSlot slot, ModelStatus baseStatus)
        {
            if (slot == null)
            {
                return;
            }

            ModelStatus source = Clamp(ModelStatus.CloneOrDefault(baseStatus));
            slot.statusWeak = Scale(source, WeakScale);
            slot.statusNormal = Scale(source, NormalScale);
            slot.statusStrong = Scale(source, StrongScale);
            slot.statusVeryStrong = Scale(source, VeryStrongScale);
            // 表示・基準用は未育成(弱い)を保持する
            slot.status = Scale(source, WeakScale);
            slot.hasEnemyStrengthStatuses = true;
            slot.enemyStrengthBalanceVersion = BalanceVersion;
        }

        // 旧仕様は普通=未育成だったので改訂時は普通を基準に戻す
        private static ModelStatus ResolveUntrainedBase(ModelSaveSlot slot)
        {
            if (slot.hasEnemyStrengthStatuses
                && HasCompleteSet(slot)
                && slot.enemyStrengthBalanceVersion < BalanceVersion
                && HasAnyValue(slot.statusNormal))
            {
                return slot.statusNormal;
            }

            return slot.status;
        }

        private static void ClampAllTiers(ModelSaveSlot slot)
        {
            slot.status = Clamp(slot.status);
            slot.statusWeak = Clamp(slot.statusWeak);
            slot.statusNormal = Clamp(slot.statusNormal);
            slot.statusStrong = Clamp(slot.statusStrong);
            slot.statusVeryStrong = Clamp(slot.statusVeryStrong);
        }

        private static bool HasCompleteSet(ModelSaveSlot slot)
        {
            return HasAnyValue(slot.statusWeak)
                && HasAnyValue(slot.statusNormal)
                && HasAnyValue(slot.statusStrong)
                && HasAnyValue(slot.statusVeryStrong);
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

        private static ModelStatus Scale(ModelStatus source, float scale)
        {
            ModelStatus status = ModelStatus.CloneOrDefault(source);
            status.hp = ScaleStat(status.hp, scale);
            status.attack = ScaleStat(status.attack, scale);
            status.defense = ScaleStat(status.defense, scale);
            status.speed = ScaleStat(status.speed, scale);
            status.hit = ScaleStat(status.hit, scale);
            return Clamp(status);
        }

        private static ModelStatus Clamp(ModelStatus source)
        {
            ModelStatus status = ModelStatus.CloneOrDefault(source);
            if (status.hp > 0)
            {
                status.hp = UnityEngine.Mathf.Min(status.hp, MaxHp);
            }

            if (status.attack > 0)
            {
                status.attack = UnityEngine.Mathf.Min(status.attack, MaxAttack);
            }

            if (status.defense > 0)
            {
                status.defense = UnityEngine.Mathf.Min(status.defense, MaxDefense);
            }

            if (status.speed > 0)
            {
                status.speed = UnityEngine.Mathf.Min(status.speed, MaxSpeed);
            }

            if (status.hit > 0)
            {
                status.hit = UnityEngine.Mathf.Min(status.hit, MaxHit);
            }

            return status;
        }

        private static int ScaleStat(int value, float scale)
        {
            if (value <= 0)
            {
                return value;
            }

            return UnityEngine.Mathf.Max(1, UnityEngine.Mathf.RoundToInt(value * scale));
        }
    }
}
