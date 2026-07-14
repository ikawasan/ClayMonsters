namespace Scene.BattlePVPScene.Interface
{
    /// <summary>
    /// 相手入力待ちメッセージの表示を切り替える
    /// </summary>
    public interface IBattlePvpOpponentWaitView
    {
        /// <summary>
        /// メッセージ表示を切り替える
        /// </summary>
        /// <param name="visible">表示するか</param>
        void SetVisible(bool visible);
    }
}
