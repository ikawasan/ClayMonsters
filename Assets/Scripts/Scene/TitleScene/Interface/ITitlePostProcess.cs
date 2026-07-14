namespace Scene.TitleScene.Interface
{
    /// <summary>
    /// タイトルシーン相当のポストプロセス有効化を制御する
    /// </summary>
    public interface ITitlePostProcess
    {
        /// <summary>
        /// タイトル向けポストプロセスを有効化する
        /// </summary>
        void Enable();

        /// <summary>
        /// ポストプロセスを無効化して他シーンへの影響を戻す
        /// </summary>
        void Disable();
    }
}
