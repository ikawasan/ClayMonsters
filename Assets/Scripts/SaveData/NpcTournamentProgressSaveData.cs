using System;

namespace SaveData
{
    /// <summary>
    /// 中断したNPCトーナメントの再開用進捗
    /// </summary>
    [Serializable]
    public sealed class NpcTournamentProgressSaveData
    {
        /// <summary>
        /// 再開可能な進捗があるか
        /// </summary>
        public bool hasProgress;

        /// <summary>
        /// 難易度(NpcTournamentDifficultyのint)
        /// </summary>
        public int difficulty;

        /// <summary>
        /// プレイヤーの育成済みスロット
        /// </summary>
        public int playerSlotIndex = -1;

        /// <summary>
        /// 現在ラウンド(0=準々1=準2=決勝)
        /// </summary>
        public int currentRound;

        /// <summary>
        /// 葉ごとの敵スロット(プレイヤー葉は-1)
        /// </summary>
        public int[] leafEnemySlotIndices;

        /// <summary>
        /// 葉ごとの表示名
        /// </summary>
        public string[] leafDisplayNames;

        /// <summary>
        /// 葉ごとの敗北済みフラグ
        /// </summary>
        public bool[] leafDefeated;

        /// <summary>
        /// 準々決勝勝者葉
        /// </summary>
        public int[] quarterWinners;

        /// <summary>
        /// 準決勝勝者葉
        /// </summary>
        public int[] semiWinners;

        /// <summary>
        /// 優勝葉(-1は未確定)
        /// </summary>
        public int championLeafIndex = -1;
    }
}
