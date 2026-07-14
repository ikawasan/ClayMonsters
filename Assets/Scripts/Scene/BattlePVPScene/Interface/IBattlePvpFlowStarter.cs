namespace Scene.BattlePVPScene.Interface
{
    /// <summary>
    /// マッチング完了後の戦闘フロー開始を提供する
    /// </summary>
    public interface IBattlePvpFlowStarter
    {
        /// <summary>
        /// マッチング完了後にモンスター選択と戦闘フローを開始する
        /// </summary>
        void BeginAfterMatchmaking();
    }
}
