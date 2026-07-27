namespace Audio
{
    /// <summary>
    /// 戦闘SEトラック識別子
    /// </summary>
    public enum SeTrackId
    {
        /// <summary>
        /// 攻撃ヒット
        /// </summary>
        AttackHit,

        /// <summary>
        /// 攻撃ミス
        /// </summary>
        AttackMiss,

        /// <summary>
        /// パーツ破壊ととどめ
        /// </summary>
        PartsBreak,

        /// <summary>
        /// 攻撃溜めチャージ
        /// </summary>
        PowerCharge,

        /// <summary>
        /// 魔法詠唱円
        /// </summary>
        MagicCircle,

        /// <summary>
        /// ファイアーボール飛翔
        /// </summary>
        MagicFireball,

        /// <summary>
        /// ファイアーボール着弾
        /// </summary>
        MagicFireballHit,

        /// <summary>
        /// ウィンドスラッシャー
        /// </summary>
        MagicWindSlasher,

        /// <summary>
        /// ダイヤモンドダスト
        /// </summary>
        MagicDiamondDust,

        /// <summary>
        /// サンダーショック
        /// </summary>
        MagicThunderShock
    }
}
