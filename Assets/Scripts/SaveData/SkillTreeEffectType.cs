namespace SaveData
{
    /// <summary>
    /// スキルツリーの効果種別
    /// </summary>
    public enum SkillTreeEffectType
    {
        /// <summary>
        /// 育成開始時HP
        /// </summary>
        StartingHp = 0,

        /// <summary>
        /// 育成開始時攻撃
        /// </summary>
        StartingAttack = 1,

        /// <summary>
        /// 育成開始時防御
        /// </summary>
        StartingDefense = 2,

        /// <summary>
        /// 育成開始時全ステ
        /// </summary>
        StartingAll = 3,

        /// <summary>
        /// 育成開始時速度
        /// </summary>
        StartingSpeed = 4,

        /// <summary>
        /// 育成開始時命中
        /// </summary>
        StartingHit = 5,

        /// <summary>
        /// 訓練大成功率加算
        /// </summary>
        GreatSuccessPercent = 10,

        /// <summary>
        /// 育成開始時所持金
        /// </summary>
        StartingMoney = 20,

        /// <summary>
        /// 育成中の獲得金百分率
        /// </summary>
        TrainingMoneyPercent = 21,

        /// <summary>
        /// 対戦ポイント取得百分率
        /// </summary>
        PointsGainPercent = 30,

        /// <summary>
        /// 継承ステ上昇百分率加算
        /// </summary>
        InheritancePercent = 40
    }
}
