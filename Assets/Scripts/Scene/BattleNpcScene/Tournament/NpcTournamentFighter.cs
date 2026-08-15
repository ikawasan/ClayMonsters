using SaveData;

namespace Scene.BattleNpcScene.Tournament
{
    /// <summary>
    /// トーナメント参加者1体
    /// </summary>
    public sealed class NpcTournamentFighter
    {
        /// <summary>
        /// 葉インデックス(0-7)
        /// </summary>
        public int LeafIndex;

        /// <summary>
        /// プレイヤーか
        /// </summary>
        public bool IsPlayer;

        /// <summary>
        /// 敵スロット番号(プレイヤーは-1)
        /// </summary>
        public int EnemySlotIndex = -1;

        /// <summary>
        /// 表示名
        /// </summary>
        public string DisplayName = string.Empty;

        /// <summary>
        /// 敗北済みか
        /// </summary>
        public bool IsDefeated;

        /// <summary>
        /// 現在の強さ段階
        /// </summary>
        public EnemyStrengthTier StrengthTier = EnemyStrengthTier.Normal;
    }
}
