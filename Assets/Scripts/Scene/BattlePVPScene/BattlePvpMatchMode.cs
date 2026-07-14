namespace Scene.BattlePVPScene
{
    /// <summary>
    /// PvPマッチングの接続方式
    /// </summary>
    public enum BattlePvpMatchMode
    {
        /// <summary>
        /// ルームコードで特定の相手と対戦
        /// </summary>
        Direct = 0,

        /// <summary>
        /// 不特定の相手を自動マッチング
        /// </summary>
        Random = 1
    }
}
