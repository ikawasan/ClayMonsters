namespace Scene.BattleNpcScene.Interface
{
    /// <summary>
    /// BattleNpcシーンのポストプロセスとSSAO有効化を制御する
    /// </summary>
    public interface IBattleNpcPostProcess
    {
        /// <summary>
        /// 教室向けポストプロセスとSSAOを有効化する
        /// </summary>
        void Enable();

        /// <summary>
        /// ポストプロセスを無効化して他シーンへの影響を戻す
        /// </summary>
        void Disable();
    }
}
