namespace Battle
{
    /// <summary>
    /// 攻撃の溜めが始まった通知
    /// </summary>
    public readonly struct AttackWindUpStarted
    {
        /// <summary>
        /// 攻撃の溜めが始まった通知を生成する
        /// </summary>
        public AttackWindUpStarted(
            BattleUnit attacker,
            BattleUnit target,
            AttackMove move,
            float windUpDuration)
        {
            Attacker = attacker;
            Target = target;
            Move = move;
            WindUpDuration = windUpDuration;
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
        /// 溜め時間(秒)
        /// </summary>
        public float WindUpDuration { get; }
    }
}
