namespace SaveData
{
    /// <summary>
    /// 新規モデル保存時の既定ステータス
    /// Max系は作成時の形状反映範囲(最大200)
    /// 育成後の戦闘上限はBattleStatusBalanceを使う
    /// </summary>
    public static class ModelStatusDefaults
    {
        /// <summary>
        /// 作成時HP下限
        /// </summary>
        public const int MinHp = 30;

        /// <summary>
        /// 作成時HP上限
        /// 育成でBattleStatusBalance.MaxHpまで伸ばせる
        /// </summary>
        public const int MaxHp = 200;

        /// <summary>
        /// 作成時HP既定値
        /// </summary>
        public const int DefaultHp = 60;

        /// <summary>
        /// 作成時攻撃下限
        /// </summary>
        public const int MinAttack = 20;

        /// <summary>
        /// 作成時攻撃上限
        /// </summary>
        public const int MaxAttack = 200;

        /// <summary>
        /// 作成時攻撃既定値
        /// </summary>
        public const int DefaultAttack = 50;

        /// <summary>
        /// 作成時防御下限
        /// </summary>
        public const int MinDefense = 20;

        /// <summary>
        /// 作成時防御上限
        /// </summary>
        public const int MaxDefense = 200;

        /// <summary>
        /// 作成時防御既定値
        /// </summary>
        public const int DefaultDefense = 50;

        /// <summary>
        /// 作成時速度下限
        /// </summary>
        public const int MinSpeed = 20;

        /// <summary>
        /// 作成時速度上限
        /// </summary>
        public const int MaxSpeed = 200;

        /// <summary>
        /// 作成時速度既定値
        /// </summary>
        public const int DefaultSpeed = 50;

        /// <summary>
        /// 作成時命中下限
        /// </summary>
        public const int MinHit = 20;

        /// <summary>
        /// 作成時命中上限
        /// </summary>
        public const int MaxHit = 200;

        /// <summary>
        /// 作成時命中既定値
        /// </summary>
        public const int DefaultHit = 50;

        /// <summary>
        /// 新規保存向けの既定ステータスを返す
        /// </summary>
        public static ModelStatus Create()
        {
            return new ModelStatus
            {
                hp = DefaultHp,
                attack = DefaultAttack,
                defense = DefaultDefense,
                speed = DefaultSpeed,
                hit = DefaultHit
            };
        }
    }
}
