namespace Battle
{
    /// <summary>
    /// ふきとばしが成立した通知
    /// </summary>
    public readonly struct KnockbackPerformed
    {
        /// <summary>
        /// ふきとばし通知を生成する
        /// </summary>
        /// <param name="source">ふきとばしを発動した側</param>
        /// <param name="target">押し出された側</param>
        public KnockbackPerformed(BattleUnit source, BattleUnit target)
        {
            Source = source;
            Target = target;
        }

        /// <summary>
        /// ふきとばしを発動した側
        /// </summary>
        public BattleUnit Source { get; }

        /// <summary>
        /// 押し出された側
        /// </summary>
        public BattleUnit Target { get; }
    }
}
