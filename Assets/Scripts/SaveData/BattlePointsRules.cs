namespace SaveData
{
    /// <summary>
    /// NPC対戦と対人戦のポイント報酬ルール
    /// </summary>
    public static class BattlePointsRules
    {
        /// <summary>
        /// 所持できるポイントの上限
        /// </summary>
        public const int MaxHeldPoints = 9999;

        /// <summary>
        /// ゲーム開始時に付与する所持ポイント
        /// </summary>
        public const int InitialHeldPoints = 100;

        /// <summary>
        /// NPC弱い勝利の基本ポイント
        /// </summary>
        public const int NpcWeakVictoryPoints = 10;

        /// <summary>
        /// NPC強さが1段階上がるごとの加算ポイント
        /// </summary>
        public const int NpcVictoryPointsPerTier = 5;

        /// <summary>
        /// 特定相手PvPの勝利ポイント
        /// </summary>
        public const int PvpDirectVictoryPoints = 30;

        /// <summary>
        /// 特定相手PvPの敗北ポイント
        /// </summary>
        public const int PvpDirectDefeatPoints = 15;

        /// <summary>
        /// 不特定相手PvPの勝利ポイント
        /// </summary>
        public const int PvpRandomVictoryPoints = 50;

        /// <summary>
        /// 不特定相手PvPの敗北ポイント
        /// </summary>
        public const int PvpRandomDefeatPoints = 25;

        /// <summary>
        /// トーナメント優勝(イージー)のポイント
        /// </summary>
        public const int TournamentChampionEasyPoints = 100;

        /// <summary>
        /// トーナメント優勝(ノーマル)のポイント
        /// </summary>
        public const int TournamentChampionNormalPoints = 150;

        /// <summary>
        /// トーナメント優勝(ハード)のポイント
        /// </summary>
        public const int TournamentChampionHardPoints = 200;

        /// <summary>
        /// トーナメント優勝(ベリーハード)のポイント
        /// </summary>
        public const int TournamentChampionVeryHardPoints = 300;

        /// <summary>
        /// NPC勝利時の獲得ポイントを返す
        /// </summary>
        /// <param name="tier">倒した敵の強さ</param>
        public static int ResolveNpcVictoryPoints(EnemyStrengthTier tier)
        {
            int tierIndex = (int)tier;
            if (tierIndex < 0)
            {
                tierIndex = 0;
            }

            return NpcWeakVictoryPoints + (tierIndex * NpcVictoryPointsPerTier);
        }

        /// <summary>
        /// トーナメント優勝時の獲得ポイントを返す
        /// </summary>
        /// <param name="difficulty">NpcTournamentDifficultyの数値</param>
        public static int ResolveTournamentChampionPoints(int difficulty)
        {
            switch (difficulty)
            {
                case 0:
                    return TournamentChampionEasyPoints;
                case 1:
                    return TournamentChampionNormalPoints;
                case 2:
                    return TournamentChampionHardPoints;
                default:
                    return TournamentChampionVeryHardPoints;
            }
        }

        /// <summary>
        /// PvP勝敗時の獲得ポイントを返す
        /// 引き分けは0
        /// </summary>
        /// <param name="isRandomMatch">不特定相手マッチか</param>
        /// <param name="playerWon">プレイヤー勝利か</param>
        /// <param name="isDraw">引き分けか</param>
        public static int ResolvePvpPoints(
            bool isRandomMatch,
            bool playerWon,
            bool isDraw)
        {
            if (isDraw)
            {
                return 0;
            }

            if (isRandomMatch)
            {
                return playerWon ? PvpRandomVictoryPoints : PvpRandomDefeatPoints;
            }

            return playerWon ? PvpDirectVictoryPoints : PvpDirectDefeatPoints;
        }

        /// <summary>
        /// 所持ポイントを0〜上限に収める
        /// </summary>
        /// <param name="points">元のポイント</param>
        public static int ClampHeldPoints(int points)
        {
            if (points < 0)
            {
                return 0;
            }

            if (points > MaxHeldPoints)
            {
                return MaxHeldPoints;
            }

            return points;
        }
    }
}
