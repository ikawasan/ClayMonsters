namespace SaveData
{
    /// <summary>
    /// スキルツリー解放済み効果の集計結果
    /// </summary>
    public readonly struct SkillTreeBonuses
    {
        /// <summary>
        /// 育成開始時HP加算
        /// </summary>
        public int StartingHp { get; }

        /// <summary>
        /// 育成開始時攻撃加算
        /// </summary>
        public int StartingAttack { get; }

        /// <summary>
        /// 育成開始時防御加算
        /// </summary>
        public int StartingDefense { get; }

        /// <summary>
        /// 育成開始時速度加算
        /// </summary>
        public int StartingSpeed { get; }

        /// <summary>
        /// 育成開始時命中加算
        /// </summary>
        public int StartingHit { get; }

        /// <summary>
        /// 育成開始時全ステ加算
        /// </summary>
        public int StartingAll { get; }

        /// <summary>
        /// 大成功率加算
        /// </summary>
        public float GreatSuccessBonusPercent { get; }

        /// <summary>
        /// 育成開始所持金加算
        /// </summary>
        public int StartingMoney { get; }

        /// <summary>
        /// 育成獲得金百分率加算
        /// </summary>
        public float TrainingMoneyGainPercent { get; }

        /// <summary>
        /// ポイント取得百分率加算
        /// </summary>
        public float PointsGainPercent { get; }

        /// <summary>
        /// 継承ステ上昇百分率加算
        /// </summary>
        public int InheritancePercentBonus { get; }

        /// <summary>
        /// 集計結果を生成する
        /// </summary>
        public SkillTreeBonuses(
            int startingHp,
            int startingAttack,
            int startingDefense,
            int startingSpeed,
            int startingHit,
            int startingAll,
            float greatSuccessBonusPercent,
            int startingMoney,
            float trainingMoneyGainPercent,
            float pointsGainPercent,
            int inheritancePercentBonus)
        {
            StartingHp = startingHp;
            StartingAttack = startingAttack;
            StartingDefense = startingDefense;
            StartingSpeed = startingSpeed;
            StartingHit = startingHit;
            StartingAll = startingAll;
            GreatSuccessBonusPercent = greatSuccessBonusPercent;
            StartingMoney = startingMoney;
            TrainingMoneyGainPercent = trainingMoneyGainPercent;
            PointsGainPercent = pointsGainPercent;
            InheritancePercentBonus = inheritancePercentBonus;
        }

        /// <summary>
        /// 効果なし
        /// </summary>
        public static SkillTreeBonuses None => new(0, 0, 0, 0, 0, 0, 0f, 0, 0f, 0f, 0);
    }
}
