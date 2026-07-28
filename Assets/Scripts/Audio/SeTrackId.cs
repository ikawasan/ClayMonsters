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
        MagicThunderShock,

        /// <summary>
        /// ふきとばし押し出し
        /// </summary>
        PressAction,

        /// <summary>
        /// 継承演出の光に包まれる
        /// </summary>
        InheritanceGlow,

        /// <summary>
        /// 継承演出の光上昇
        /// </summary>
        InheritanceLightRise,

        /// <summary>
        /// 継承演出の光下降
        /// </summary>
        InheritanceLightFall,

        /// <summary>
        /// 継承演出で継承先が光る
        /// </summary>
        InheritanceLightGlow
    }
}
