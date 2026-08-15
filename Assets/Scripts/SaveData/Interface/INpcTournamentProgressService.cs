using SaveData;

namespace SaveData.Interface
{
    /// <summary>
    /// 中断トーナメントの再開進捗
    /// </summary>
    public interface INpcTournamentProgressService
    {
        /// <summary>
        /// 再開可能な進捗があるか
        /// </summary>
        bool HasProgress { get; }

        /// <summary>
        /// 保存済み進捗を返す進捗が無い場合はnull
        /// </summary>
        NpcTournamentProgressSaveData GetProgressOrNull();

        /// <summary>
        /// 進捗を保存する
        /// </summary>
        /// <param name="progress">保存内容</param>
        void SaveProgress(NpcTournamentProgressSaveData progress);

        /// <summary>
        /// 進捗を消す
        /// </summary>
        void ClearProgress();

        /// <summary>
        /// 永続化データから再読込する
        /// </summary>
        void Reload();
    }
}
