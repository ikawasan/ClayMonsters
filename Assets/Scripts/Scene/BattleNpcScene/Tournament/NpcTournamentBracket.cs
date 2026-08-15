using SaveData;
using Scene.TitleScene;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scene.BattleNpcScene.Tournament
{
    /// <summary>
    /// 8体シングルエリミネーションのブラケット状態
    /// </summary>
    public sealed class NpcTournamentBracket
    {
        public const int FighterCount = 8;
        public const int QuarterMatchCount = 4;
        public const int SemiMatchCount = 2;

        private readonly NpcTournamentFighter[] leaves = new NpcTournamentFighter[FighterCount];
        private readonly int[] quarterWinners = new int[QuarterMatchCount];
        private readonly int[] semiWinners = new int[SemiMatchCount];
        private int championLeafIndex = -1;
        private int currentRound;

        /// <summary>
        /// 葉の参加者
        /// </summary>
        public IReadOnlyList<NpcTournamentFighter> Leaves => leaves;

        /// <summary>
        /// 現在ラウンド(0=準々 1=準 2=決勝)
        /// </summary>
        public int CurrentRound => currentRound;

        /// <summary>
        /// 優勝葉インデックス
        /// </summary>
        public int ChampionLeafIndex => championLeafIndex;

        /// <summary>
        /// プレイヤー葉インデックス
        /// </summary>
        public int PlayerLeafIndex { get; private set; }

        /// <summary>
        /// ブラケットを初期化する
        /// </summary>
        /// <param name="playerName">プレイヤー名</param>
        /// <param name="npcFighters">NPC7体</param>
        /// <param name="difficulty">難易度</param>
        public void Initialize(
            string playerName,
            IReadOnlyList<NpcTournamentFighter> npcFighters,
            NpcTournamentDifficulty difficulty)
        {
            if (npcFighters == null || npcFighters.Count != FighterCount - 1)
            {
                throw new ArgumentException("NPCは7体必要です", nameof(npcFighters));
            }

            PlayerLeafIndex = 0;
            EnemyStrengthTier tier = ResolveStrengthTier(difficulty);

            leaves[0] = new NpcTournamentFighter
            {
                LeafIndex = 0,
                IsPlayer = true,
                EnemySlotIndex = -1,
                DisplayName = string.IsNullOrEmpty(playerName) ? "Player" : playerName,
                StrengthTier = EnemyStrengthTier.Normal,
            };

            for (int i = 0; i < npcFighters.Count; i++)
            {
                NpcTournamentFighter src = npcFighters[i];
                int leaf = i + 1;
                leaves[leaf] = new NpcTournamentFighter
                {
                    LeafIndex = leaf,
                    IsPlayer = false,
                    EnemySlotIndex = src.EnemySlotIndex,
                    DisplayName = src.DisplayName,
                    StrengthTier = tier,
                };
            }

            for (int i = 0; i < quarterWinners.Length; i++)
            {
                quarterWinners[i] = -1;
            }

            for (int i = 0; i < semiWinners.Length; i++)
            {
                semiWinners[i] = -1;
            }

            championLeafIndex = -1;
            currentRound = 0;
        }

        /// <summary>
        /// 難易度から敵強さを返す
        /// </summary>
        public static EnemyStrengthTier ResolveStrengthTier(NpcTournamentDifficulty difficulty)
        {
            switch (difficulty)
            {
                case NpcTournamentDifficulty.Easy:
                    return EnemyStrengthTier.Weak;
                case NpcTournamentDifficulty.Hard:
                    return EnemyStrengthTier.VeryStrong;
                case NpcTournamentDifficulty.VeryHard:
                    return EnemyStrengthTier.Strongest;
                case NpcTournamentDifficulty.Normal:
                default:
                    return EnemyStrengthTier.Normal;
            }
        }

        /// <summary>
        /// 指定ラウンドの対戦組を返す
        /// </summary>
        /// <param name="round">ラウンド</param>
        /// <param name="matchIndex">ラウンド内試合番号</param>
        /// <param name="leftLeaf">左側葉</param>
        /// <param name="rightLeaf">右側葉</param>
        public bool TryGetMatchPair(
            int round,
            int matchIndex,
            out int leftLeaf,
            out int rightLeaf)
        {
            leftLeaf = -1;
            rightLeaf = -1;
            if (round == 0)
            {
                if (matchIndex < 0 || matchIndex >= QuarterMatchCount)
                {
                    return false;
                }

                leftLeaf = matchIndex * 2;
                rightLeaf = leftLeaf + 1;
                return true;
            }

            if (round == 1)
            {
                if (matchIndex < 0 || matchIndex >= SemiMatchCount)
                {
                    return false;
                }

                leftLeaf = quarterWinners[matchIndex * 2];
                rightLeaf = quarterWinners[(matchIndex * 2) + 1];
                return leftLeaf >= 0 && rightLeaf >= 0;
            }

            if (round == 2)
            {
                if (matchIndex != 0)
                {
                    return false;
                }

                leftLeaf = semiWinners[0];
                rightLeaf = semiWinners[1];
                return leftLeaf >= 0 && rightLeaf >= 0;
            }

            return false;
        }

        /// <summary>
        /// プレイヤーが含まれる試合番号を返す
        /// </summary>
        /// <param name="round">ラウンド</param>
        public int FindPlayerMatchIndex(int round)
        {
            int matchCount = round == 0 ? QuarterMatchCount : round == 1 ? SemiMatchCount : 1;
            for (int i = 0; i < matchCount; i++)
            {
                if (!TryGetMatchPair(round, i, out int left, out int right))
                {
                    continue;
                }

                if (left == PlayerLeafIndex || right == PlayerLeafIndex)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// 試合結果を反映する
        /// </summary>
        /// <param name="round">ラウンド</param>
        /// <param name="matchIndex">試合番号</param>
        /// <param name="winnerLeaf">勝者葉</param>
        public void ApplyMatchResult(int round, int matchIndex, int winnerLeaf)
        {
            int loserLeaf = ResolveLoserLeaf(round, matchIndex, winnerLeaf);
            if (loserLeaf >= 0 && loserLeaf < leaves.Length && leaves[loserLeaf] != null)
            {
                leaves[loserLeaf].IsDefeated = true;
            }

            if (round == 0)
            {
                quarterWinners[matchIndex] = winnerLeaf;
            }
            else if (round == 1)
            {
                semiWinners[matchIndex] = winnerLeaf;
            }
            else if (round == 2)
            {
                championLeafIndex = winnerLeaf;
            }
        }

        /// <summary>
        /// NPC同士の勝敗を決める
        /// </summary>
        public int ResolveNpcVersusNpcWinner(int leftLeaf, int rightLeaf)
        {
            return UnityEngine.Random.Range(0, 2) == 0 ? leftLeaf : rightLeaf;
        }

        /// <summary>
        /// プレイヤーが生存しているか
        /// </summary>
        public bool IsPlayerAlive()
        {
            NpcTournamentFighter player = leaves[PlayerLeafIndex];
            return player != null && !player.IsDefeated;
        }

        /// <summary>
        /// プレイヤーが優勝したか
        /// </summary>
        public bool IsPlayerChampion()
        {
            return championLeafIndex == PlayerLeafIndex;
        }

        /// <summary>
        /// 次のラウンドへ進める
        /// </summary>
        public void AdvanceRound()
        {
            currentRound = Mathf.Min(currentRound + 1, 2);
        }

        /// <summary>
        /// 保存データからブラケットを復元する
        /// </summary>
        /// <param name="progress">保存進捗</param>
        /// <param name="difficulty">難易度</param>
        public void RestoreFromProgress(
            NpcTournamentProgressSaveData progress,
            NpcTournamentDifficulty difficulty)
        {
            if (progress == null)
            {
                throw new ArgumentNullException(nameof(progress));
            }

            PlayerLeafIndex = 0;
            EnemyStrengthTier tier = ResolveStrengthTier(difficulty);
            currentRound = Mathf.Clamp(progress.currentRound, 0, 2);
            championLeafIndex = progress.championLeafIndex;

            for (int i = 0; i < FighterCount; i++)
            {
                bool isPlayer = i == 0;
                int enemySlot = progress.leafEnemySlotIndices != null
                    && i < progress.leafEnemySlotIndices.Length
                    ? progress.leafEnemySlotIndices[i]
                    : -1;
                string name = progress.leafDisplayNames != null
                    && i < progress.leafDisplayNames.Length
                    ? progress.leafDisplayNames[i]
                    : string.Empty;
                bool defeated = progress.leafDefeated != null
                    && i < progress.leafDefeated.Length
                    && progress.leafDefeated[i];

                leaves[i] = new NpcTournamentFighter
                {
                    LeafIndex = i,
                    IsPlayer = isPlayer,
                    EnemySlotIndex = isPlayer ? -1 : enemySlot,
                    DisplayName = string.IsNullOrEmpty(name)
                        ? (isPlayer ? "Player" : $"NPC{i}")
                        : name,
                    StrengthTier = isPlayer ? EnemyStrengthTier.Normal : tier,
                    IsDefeated = defeated,
                };
            }

            for (int i = 0; i < quarterWinners.Length; i++)
            {
                quarterWinners[i] = progress.quarterWinners != null
                    && i < progress.quarterWinners.Length
                    ? progress.quarterWinners[i]
                    : -1;
            }

            for (int i = 0; i < semiWinners.Length; i++)
            {
                semiWinners[i] = progress.semiWinners != null
                    && i < progress.semiWinners.Length
                    ? progress.semiWinners[i]
                    : -1;
            }
        }

        /// <summary>
        /// 準々決勝勝者葉を返す
        /// </summary>
        public int GetQuarterWinner(int matchIndex)
        {
            if (matchIndex < 0 || matchIndex >= quarterWinners.Length)
            {
                return -1;
            }

            return quarterWinners[matchIndex];
        }

        /// <summary>
        /// 準決勝勝者葉を返す
        /// </summary>
        public int GetSemiWinner(int matchIndex)
        {
            if (matchIndex < 0 || matchIndex >= semiWinners.Length)
            {
                return -1;
            }

            return semiWinners[matchIndex];
        }

        private int ResolveLoserLeaf(int round, int matchIndex, int winnerLeaf)
        {
            if (!TryGetMatchPair(round, matchIndex, out int left, out int right))
            {
                return -1;
            }

            if (winnerLeaf == left)
            {
                return right;
            }

            if (winnerLeaf == right)
            {
                return left;
            }

            return -1;
        }
    }
}
