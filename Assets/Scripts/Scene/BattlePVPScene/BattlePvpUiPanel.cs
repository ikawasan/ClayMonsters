namespace Scene.BattlePVPScene
{
    /// <summary>
    /// BattlePVP画面の表示パネル種別
    /// </summary>
    public enum BattlePvpUiPanel
    {
        /// <summary>
        /// 対戦方式の選択
        /// </summary>
        ModeSelect = 0,

        /// <summary>
        /// 特定相手との対戦
        /// </summary>
        DirectMatch = 1,

        /// <summary>
        /// 不特定相手との対戦
        /// </summary>
        RandomMatch = 2,

        /// <summary>
        /// マッチング進行中
        /// </summary>
        Matching = 3
    }
}
