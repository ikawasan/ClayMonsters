namespace Scene.ClayEditScene.Interface
{
    /// <summary>
    /// ClayEditシーンの黒背景を制御する
    /// メッシュ色味維持のためポストプロセスは掛けない
    /// </summary>
    public interface IClayEditPostProcess
    {
        /// <summary>
        /// 黒背景を有効化しカメラのポストプロセスを切る
        /// </summary>
        void Enable();

        /// <summary>
        /// 設定を無効化して他シーンへの影響を戻す
        /// </summary>
        void Disable();
    }
}
