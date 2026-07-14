namespace Scene.PvpLobby.Interface
{
    /// <summary>
    /// DDOL常駐のPvP接続セッションを終了する
    /// </summary>
    public interface IPvpSessionController
    {
        /// <summary>
        /// マッチングとNetcode接続を終了する
        /// </summary>
        void EndSession();
    }
}
