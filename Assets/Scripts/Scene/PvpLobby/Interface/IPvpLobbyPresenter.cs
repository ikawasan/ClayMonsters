namespace Scene.PvpLobby.Interface
{
    /// <summary>
    /// PvPロビーのマッチングUIを制御する
    /// </summary>
    public interface IPvpLobbyPresenter
    {
        /// <summary>
        /// ボタン購読など初期設定を行う
        /// </summary>
        void Setup();

        /// <summary>
        /// ロビー表示時の処理
        /// </summary>
        void OnShow();

        /// <summary>
        /// ロビー非表示時のUIリセット。ネットワークセッションは維持する
        /// </summary>
        void OnHide();
    }
}
