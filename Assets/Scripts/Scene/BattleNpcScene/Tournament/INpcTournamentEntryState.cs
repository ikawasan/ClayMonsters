using Scene.TitleScene;

namespace Scene.BattleNpcScene.Tournament
{
    /// <summary>
    /// トーナメント入場情報
    /// </summary>
    public interface INpcTournamentEntryState
    {
        /// <summary>
        /// トーナメント入場か
        /// </summary>
        bool IsTournament { get; }

        /// <summary>
        /// 中断からの再開か
        /// </summary>
        bool IsResume { get; }

        /// <summary>
        /// 難易度
        /// </summary>
        NpcTournamentDifficulty Difficulty { get; }

        /// <summary>
        /// トーナメント入場を設定する
        /// </summary>
        /// <param name="difficulty">難易度</param>
        /// <param name="resume">再開ならtrue</param>
        void SetTournament(NpcTournamentDifficulty difficulty, bool resume = false);

        /// <summary>
        /// 入場情報を消す
        /// </summary>
        void Clear();
    }
}
