namespace Scene.ClayEditScene.Interface
{
    /// <summary>
    /// ClayEditシーンのポストプロセスと黒背景を制御する
    /// </summary>
    public interface IClayEditPostProcess
    {
        /// <summary>
        /// Bloomと黒背景を有効化する
        /// </summary>
        void Enable();

        /// <summary>
        /// 設定を無効化して他シーンへの影響を戻す
        /// </summary>
        void Disable();
    }
}
