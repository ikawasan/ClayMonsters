namespace SaveData
{
    /// <summary>
    /// CPU戦進捗の敵軸とスロット別強さ軸の範囲計算
    /// </summary>
    public static class NpcBattleProgressRules
    {
        /// <summary>
        /// 強さ段階の種類数(弱い〜最強)
        /// </summary>
        public const int TierCount = 5;

        /// <summary>
        /// 初期に開放する敵数
        /// </summary>
        public const int InitialUnlockedEnemyCount = 1;

        /// <summary>
        /// スロット開放時の初期強さ数
        /// 弱いと普通を開放する
        /// </summary>
        public const int InitialSlotStrengthCount = 2;

        /// <summary>
        /// 敵スロット数の上限
        /// </summary>
        public static int MaxEnemyCount => ModelSavePoolSettings.EnemySlotCount;

        /// <summary>
        /// 強さ段階数の上限
        /// </summary>
        public static int MaxStrengthCount => TierCount;

        /// <summary>
        /// 開放敵数を有効範囲に丸める
        /// </summary>
        /// <param name="unlockedEnemyCount">開放敵数</param>
        public static int ClampUnlockedEnemyCount(int unlockedEnemyCount)
        {
            if (unlockedEnemyCount < InitialUnlockedEnemyCount)
            {
                return InitialUnlockedEnemyCount;
            }

            int max = MaxEnemyCount;
            return unlockedEnemyCount > max ? max : unlockedEnemyCount;
        }

        /// <summary>
        /// スロットの開放強さ数を有効範囲に丸める
        /// </summary>
        /// <param name="unlockedStrengthCount">開放強さ数</param>
        /// <param name="slotUnlocked">そのスロットが開放済みか</param>
        public static int ClampSlotStrengthCount(int unlockedStrengthCount, bool slotUnlocked)
        {
            if (!slotUnlocked)
            {
                return 0;
            }

            if (unlockedStrengthCount < InitialSlotStrengthCount)
            {
                return InitialSlotStrengthCount;
            }

            int max = MaxStrengthCount;
            return unlockedStrengthCount > max ? max : unlockedStrengthCount;
        }

        /// <summary>
        /// 初期のスロット別強さ配列を作る
        /// </summary>
        public static int[] CreateInitialSlotStrengthCounts()
        {
            int[] counts = new int[MaxEnemyCount];
            counts[0] = InitialSlotStrengthCount;
            return counts;
        }

        /// <summary>
        /// 全敵スロットと全強さを開放した配列を作る
        /// </summary>
        public static int[] CreateFullyUnlockedSlotStrengthCounts()
        {
            int maxEnemy = MaxEnemyCount;
            int[] counts = new int[maxEnemy];
            for (int i = 0; i < maxEnemy; i++)
            {
                counts[i] = MaxStrengthCount;
            }

            return counts;
        }
    }
}
