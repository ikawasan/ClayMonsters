namespace SaveData
{
    /// <summary>
    /// 敵モデルの強さ段階ごとのステータス解決
    /// </summary>
    public static class EnemyStrengthStatusCatalog
    {
        /// <summary>
        /// 弱い倍率(基準ステータス比)
        /// </summary>
        public const float WeakScale = 0.75f;

        /// <summary>
        /// 普通倍率(基準ステータス比)
        /// </summary>
        public const float NormalScale = 1f;

        /// <summary>
        /// 強い倍率(基準ステータス比)
        /// </summary>
        public const float StrongScale = 1.25f;

        /// <summary>
        /// 超強い倍率(基準ステータス比)
        /// </summary>
        public const float VeryStrongScale = 1.55f;

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

            return ModelStatus.CloneOrDefault(selected);
        }

        /// <summary>
        /// 4段階ステータスが無ければ基準ステータスから用意する
        /// </summary>
        /// <param name="slot">敵スロット</param>
        public static void EnsureFromBase(ModelSaveSlot slot)
        {
            if (slot == null)
            {
                return;
            }

            if (slot.hasEnemyStrengthStatuses && HasCompleteSet(slot))
            {
                return;
            }

            ApplyGeneratedFromBase(slot, slot.status);
        }

        /// <summary>
        /// 基準ステータスから4段階を書き込む
        /// </summary>
        /// <param name="slot">敵スロット</param>
        /// <param name="baseStatus">基準ステータス(普通相当)</param>
        public static void ApplyGeneratedFromBase(ModelSaveSlot slot, ModelStatus baseStatus)
        {
            if (slot == null)
            {
                return;
            }

            ModelStatus source = ModelStatus.CloneOrDefault(baseStatus);
            slot.statusWeak = Scale(source, WeakScale);
            slot.statusNormal = Scale(source, NormalScale);
            slot.statusStrong = Scale(source, StrongScale);
            slot.statusVeryStrong = Scale(source, VeryStrongScale);
            slot.status = Scale(source, NormalScale);
            slot.hasEnemyStrengthStatuses = true;
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
