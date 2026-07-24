namespace Scene.TrainingScene.Domain
{
    /// <summary>
    /// 育成中に発生するイベント種別
    /// </summary>
    public enum TrainingEventType
    {
        /// <summary>
        /// 発生なし
        /// </summary>
        None,

        /// <summary>
        /// 追加ステータス上昇
        /// </summary>
        StatBoost,

        /// <summary>
        /// 攻撃の習得
        /// </summary>
        LearnAttack,

        /// <summary>
        /// 強敵急襲
        /// </summary>
        Ambush
    }
}
