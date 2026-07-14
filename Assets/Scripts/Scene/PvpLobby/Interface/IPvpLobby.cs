namespace Scene.PvpLobby.Interface
{
    /// <summary>
    /// DDOL常駐のPvPロビーUIを表示する
    /// </summary>
    public interface IPvpLobby
    {
        /// <summary>
        /// ロビーUIを表示する
        /// </summary>
        void Show();

        /// <summary>
        /// ロビーUIを非表示にする
        /// </summary>
        void Hide();
    }
}
