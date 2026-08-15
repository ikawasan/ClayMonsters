using SaveData.Interface;
using UnityEngine;

namespace SaveData.Service
{
    /// <summary>
    /// 中断トーナメント進捗の読み書き
    /// </summary>
    public sealed class NpcTournamentProgressService : INpcTournamentProgressService
    {
        private const int FighterCount = 8;
        private const int QuarterMatchCount = 4;
        private const int SemiMatchCount = 2;

        private NpcTournamentProgressSaveData progress;

        /// <summary>
        /// 永続化データを読み込む
        /// </summary>
        public NpcTournamentProgressService()
        {
            ReloadFromDisk();
        }

        /// <inheritdoc/>
        public bool HasProgress => progress != null && progress.hasProgress && IsValid(progress);

        /// <inheritdoc/>
        public NpcTournamentProgressSaveData GetProgressOrNull()
        {
            if (!HasProgress)
            {
                return null;
            }

            return Clone(progress);
        }

        /// <inheritdoc/>
        public void SaveProgress(NpcTournamentProgressSaveData next)
        {
            if (next == null || !IsValid(next))
            {
                Debug.LogError("[NpcTournamentProgressService] 無効なトーナメント進捗です");
                return;
            }

            progress = Clone(next);
            progress.hasProgress = true;
            Persist();
            Debug.Log(
                "[NpcTournamentProgressService] トーナメント進捗を保存しました"
                + $" difficulty={progress.difficulty} round={progress.currentRound}"
                + $" playerSlot={progress.playerSlotIndex}");
        }

        /// <inheritdoc/>
        public void ClearProgress()
        {
            if (progress == null || !progress.hasProgress)
            {
                progress = CreateEmpty();
                return;
            }

            progress = CreateEmpty();
            Persist();
            Debug.Log("[NpcTournamentProgressService] トーナメント進捗を消去しました");
        }

        /// <inheritdoc/>
        public void Reload()
        {
            ReloadFromDisk();
        }

        private void ReloadFromDisk()
        {
            SaveData saveData = SaveDataManager.Load();
            progress = Clone(saveData.NpcTournamentProgress ?? CreateEmpty());
            if (!IsValid(progress))
            {
                progress = CreateEmpty();
            }
        }

        private void Persist()
        {
            NpcTournamentProgressSaveData current = Clone(progress);
            SaveDataManager.Update(data =>
            {
                data.NpcTournamentProgress = current;
            });
        }

        private static bool IsValid(NpcTournamentProgressSaveData data)
        {
            if (data == null || !data.hasProgress)
            {
                return false;
            }

            if (data.playerSlotIndex < 0)
            {
                return false;
            }

            if (data.currentRound < 0 || data.currentRound > 2)
            {
                return false;
            }

            if (data.leafEnemySlotIndices == null
                || data.leafEnemySlotIndices.Length != FighterCount)
            {
                return false;
            }

            if (data.leafDisplayNames == null
                || data.leafDisplayNames.Length != FighterCount)
            {
                return false;
            }

            if (data.leafDefeated == null
                || data.leafDefeated.Length != FighterCount)
            {
                return false;
            }

            if (data.quarterWinners == null
                || data.quarterWinners.Length != QuarterMatchCount)
            {
                return false;
            }

            if (data.semiWinners == null
                || data.semiWinners.Length != SemiMatchCount)
            {
                return false;
            }

            return true;
        }

        private static NpcTournamentProgressSaveData CreateEmpty()
        {
            return new NpcTournamentProgressSaveData
            {
                hasProgress = false,
                difficulty = 1,
                playerSlotIndex = -1,
                currentRound = 0,
                leafEnemySlotIndices = new int[FighterCount],
                leafDisplayNames = new string[FighterCount],
                leafDefeated = new bool[FighterCount],
                quarterWinners = CreateUnsetWinners(QuarterMatchCount),
                semiWinners = CreateUnsetWinners(SemiMatchCount),
                championLeafIndex = -1,
            };
        }

        private static int[] CreateUnsetWinners(int count)
        {
            var winners = new int[count];
            for (int i = 0; i < winners.Length; i++)
            {
                winners[i] = -1;
            }

            return winners;
        }

        private static NpcTournamentProgressSaveData Clone(NpcTournamentProgressSaveData source)
        {
            if (source == null)
            {
                return CreateEmpty();
            }

            return new NpcTournamentProgressSaveData
            {
                hasProgress = source.hasProgress,
                difficulty = source.difficulty,
                playerSlotIndex = source.playerSlotIndex,
                currentRound = source.currentRound,
                leafEnemySlotIndices = CloneInts(source.leafEnemySlotIndices, FighterCount),
                leafDisplayNames = CloneStrings(source.leafDisplayNames, FighterCount),
                leafDefeated = CloneBools(source.leafDefeated, FighterCount),
                quarterWinners = CloneInts(source.quarterWinners, QuarterMatchCount, unsetValue: -1),
                semiWinners = CloneInts(source.semiWinners, SemiMatchCount, unsetValue: -1),
                championLeafIndex = source.championLeafIndex,
            };
        }

        private static int[] CloneInts(int[] source, int length, int unsetValue = 0)
        {
            var result = new int[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = unsetValue;
            }

            if (source == null)
            {
                return result;
            }

            int copy = Mathf.Min(source.Length, length);
            for (int i = 0; i < copy; i++)
            {
                result[i] = source[i];
            }

            return result;
        }

        private static string[] CloneStrings(string[] source, int length)
        {
            var result = new string[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = string.Empty;
            }

            if (source == null)
            {
                return result;
            }

            int copy = Mathf.Min(source.Length, length);
            for (int i = 0; i < copy; i++)
            {
                result[i] = source[i] ?? string.Empty;
            }

            return result;
        }

        private static bool[] CloneBools(bool[] source, int length)
        {
            var result = new bool[length];
            if (source == null)
            {
                return result;
            }

            int copy = Mathf.Min(source.Length, length);
            for (int i = 0; i < copy; i++)
            {
                result[i] = source[i];
            }

            return result;
        }
    }
}
