namespace SaveData
{
    /// <summary>
    /// 新規モデル保存時の既定ステータス
    /// Max系は作成時の形状反映範囲
    /// 育成後の戦闘上限はBattleStatusBalanceを使う
    /// </summary>
    public static class ModelStatusDefaults
    {
        /// <summary>
        /// 作成時HP下限
        /// </summary>
        public const int MinHp = 60;

        /// <summary>
        /// 作成時HP上限
        /// 育成でBattleStatusBalance.MaxHpまで伸ばせる
        /// </summary>
        public const int MaxHp = 120;

        /// <summary>
        /// 作成時HP既定値
        /// </summary>
        public const int DefaultHp = 85;

        public const int MinAttack = 22;
        public const int MaxAttack = 48;
        public const int DefaultAttack = 36;
        public const int MinDefense = 22;
        public const int MaxDefense = 48;
        public const int DefaultDefense = 36;
        public const int DefaultSpeed = 10;
        public const int MinHit = 6;
        public const int MaxHit = 18;
        public const int DefaultHit = 10;

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
