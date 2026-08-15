using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace Scene.BattleNpcScene.Tournament
{
    /// <summary>
    /// 未配線時のトーナメントUIダミー
    /// </summary>
    public sealed class NullNpcTournamentBracketView : INpcTournamentBracketView
    {
        /// <inheritdoc/>
        public bool IsConfigured => false;

        /// <inheritdoc/>
        public void Show(NpcTournamentBracket bracket)
        {
        }

        /// <inheritdoc/>
        public void FocusPlayerEntry(NpcTournamentBracket bracket)
        {
        }

        /// <inheritdoc/>
        public void Hide()
        {
        }

        /// <inheritdoc/>
        public void HideImmediate()
        {
        }

        /// <inheritdoc/>
        public UniTask<NpcTournamentBracketWaitResult> WaitForAdvanceOrAbortAsync(
            CancellationToken cancellationToken)
        {
            return UniTask.FromResult(NpcTournamentBracketWaitResult.Advance);
        }

        /// <inheritdoc/>
        public void SetLeafThumbnail(int leafIndex, Sprite sprite, string displayName)
        {
        }

        /// <inheritdoc/>
        public void RefreshDefeated(NpcTournamentBracket bracket)
        {
        }

        /// <inheritdoc/>
        public void RefreshAdvanceSlots(NpcTournamentBracket bracket)
        {
        }

        /// <inheritdoc/>
        public UniTask PlayTravelRiseAndShakeAsync(
            int round,
            int matchIndex,
            int leftLeaf,
            int rightLeaf,
            bool restoreSlotsAfter,
            CancellationToken cancellationToken)
        {
            return UniTask.CompletedTask;
        }

        /// <inheritdoc/>
        public UniTask PlayPostBattleResolveAsync(
            int round,
            int matchIndex,
            int winnerLeaf,
            int loserLeaf,
            CancellationToken cancellationToken)
        {
            return UniTask.CompletedTask;
        }

        /// <inheritdoc/>
        public UniTask PrepareChampionAtIntersectionAsync(
            int round,
            int matchIndex,
            int winnerLeaf,
            int loserLeaf,
            CancellationToken cancellationToken)
        {
            return UniTask.CompletedTask;
        }

        /// <inheritdoc/>
        public UniTask PlayChampionRiseAsync(
            int winnerLeaf,
            CancellationToken cancellationToken)
        {
            return UniTask.CompletedTask;
        }

        /// <inheritdoc/>
        public UniTask ShowChampionRewardAsync(int points, CancellationToken cancellationToken)
        {
            return UniTask.CompletedTask;
        }
    }
}
