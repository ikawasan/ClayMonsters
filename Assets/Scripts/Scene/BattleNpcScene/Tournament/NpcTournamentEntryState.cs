using Scene.TitleScene;

namespace Scene.BattleNpcScene.Tournament
{
    /// <summary>
    /// Titleから渡されたトーナメント入場情報
    /// </summary>
    public sealed class NpcTournamentEntryState : INpcTournamentEntryState
    {
        /// <inheritdoc/>
        public bool IsTournament { get; private set; }

        /// <inheritdoc/>
        public bool IsResume { get; private set; }

        /// <inheritdoc/>
        public NpcTournamentDifficulty Difficulty { get; private set; }

        /// <inheritdoc/>
        public void SetTournament(NpcTournamentDifficulty difficulty, bool resume = false)
        {
            IsTournament = true;
            IsResume = resume;
            Difficulty = difficulty;
        }

        /// <inheritdoc/>
        public void Clear()
        {
            IsTournament = false;
            IsResume = false;
            Difficulty = NpcTournamentDifficulty.Normal;
        }
    }
}
