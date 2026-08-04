namespace Battle
{
    /// <summary>
    /// 敵AIが参照する戦闘状況のスナップショット
    /// </summary>
    public readonly struct BattleEnemyAiContext
    {
        /// <summary>
        /// 敵AI用の戦闘状況を生成する
        /// </summary>
        public BattleEnemyAiContext(
            BattleUnit self,
            BattleUnit opponent,
            float distance,
            float maxDistance,
            float timeRemaining,
            bool isOpponentPerformingAttack,
            float attackCooldownRemaining,
            float stepCooldownRemaining,
            float knockbackRecastRemaining,
            float deltaTime,
            BattleSettings settings)
        {
            Self = self;
            Opponent = opponent;
            Distance = distance;
            MaxDistance = maxDistance;
            TimeRemaining = timeRemaining;
            IsOpponentPerformingAttack = isOpponentPerformingAttack;
            AttackCooldownRemaining = attackCooldownRemaining;
            StepCooldownRemaining = stepCooldownRemaining;
            KnockbackRecastRemaining = knockbackRecastRemaining;
            DeltaTime = deltaTime;
            Settings = settings;
        }

        /// <summary>
        /// 敵自身
        /// </summary>
        public BattleUnit Self { get; }

        /// <summary>
        /// 相手(プレイヤー)
        /// </summary>
        public BattleUnit Opponent { get; }

        /// <summary>
        /// 現在の間合い
        /// </summary>
        public float Distance { get; }

        /// <summary>
        /// 最大間合い
        /// </summary>
        public float MaxDistance { get; }

        /// <summary>
        /// 残り時間(秒)
        /// </summary>
        public float TimeRemaining { get; }

        /// <summary>
        /// 相手が攻撃中か
        /// </summary>
        public bool IsOpponentPerformingAttack { get; }

        /// <summary>
        /// 攻撃クールダウン残り(秒)
        /// </summary>
        public float AttackCooldownRemaining { get; }

        /// <summary>
        /// ステップ冷却残り(秒)
        /// </summary>
        public float StepCooldownRemaining { get; }

        /// <summary>
        /// ふきとばしリキャスト残り(秒)
        /// </summary>
        public float KnockbackRecastRemaining { get; }

        /// <summary>
        /// 今フレームの経過秒
        /// </summary>
        public float DeltaTime { get; }

        /// <summary>
        /// 戦闘設定
        /// </summary>
        public BattleSettings Settings { get; }

        /// <summary>
        /// 現在の距離帯
        /// </summary>
        public BattleDistanceBand DistanceBand => BattleDistanceBandResolver.Resolve(Distance, MaxDistance);

        /// <summary>
        /// 自身のHP比率
        /// </summary>
        public float SelfHpRatio => Self.MaxHp > 0 ? (float)Self.CurrentHp / Self.MaxHp : 0f;

        /// <summary>
        /// 相手のHP比率
        /// </summary>
        public float OpponentHpRatio => Opponent.MaxHp > 0 ? (float)Opponent.CurrentHp / Opponent.MaxHp : 0f;

        /// <summary>
        /// ふきとばし可能かガッツとリキャストから判定
        /// </summary>
        public bool CanUseKnockback =>
            Self != null
            && Self.CanAct
            && KnockbackRecastRemaining <= 0f
            && Self.Guts >= Settings.KnockbackGutsCost
            && Distance < MaxDistance - 1e-3f;
    }
}
