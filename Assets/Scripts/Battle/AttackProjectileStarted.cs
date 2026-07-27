namespace Battle
{
    /// <summary>
    /// 投射魔法の飛翔が始まった通知
    /// </summary>
    public readonly struct AttackProjectileStarted
    {
        /// <summary>
        /// 投射魔法の飛翔が始まった通知を生成する
        /// </summary>
        public AttackProjectileStarted(
            BattleUnit attacker,
            BattleUnit target,
            AttackMove move,
            float travelDuration)
        {
            Attacker = attacker;
            Target = target;
            Move = move;
            TravelDuration = travelDuration;
        }

        /// <summary>
        /// 攻撃側ユニット
        /// </summary>
        public BattleUnit Attacker { get; }

        /// <summary>
        /// 被攻撃側ユニット
        /// </summary>
        public BattleUnit Target { get; }

        /// <summary>
        /// 使用予定の技
        /// </summary>
        public AttackMove Move { get; }

        /// <summary>
        /// 飛翔時間(秒)
        /// </summary>
        public float TravelDuration { get; }
    }
}
