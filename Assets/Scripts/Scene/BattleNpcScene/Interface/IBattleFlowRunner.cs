namespace Scene.BattleNpcScene.Interface
{
    /// <summary>
    /// BattleNpcの戦闘フロー実行
    /// </summary>
    public interface IBattleFlowRunner
    {
        /// <summary>
        /// 戦闘フローを非同期で開始する
        /// シーン遷移完了を待たせない
        /// </summary>
        void StartFlow();

        /// <summary>
        /// 進行中の戦闘フローを停止する
        /// </summary>
        void Stop();

        /// <summary>
        /// シーン退場時に戦闘UIと配置済みモデルを整理する
        /// </summary>
        void CleanupForLeave();
    }
}
