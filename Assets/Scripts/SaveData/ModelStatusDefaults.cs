namespace SaveData
{
    /// <summary>
    /// 新規モデル保存時の既定ステータス
    /// </summary>
    public static class ModelStatusDefaults
    {
        public const int DefaultHp = 400;
        public const int MinAttack = 22;
        public const int MaxAttack = 48;
        public const int DefaultAttack = 36;
        public const int MinDefense = 22;
        public const int MaxDefense = 48;
        public const int DefaultDefense = 36;
        public const int DefaultSpeed = 10;

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
                speed = DefaultSpeed
            };
        }
    }
}
