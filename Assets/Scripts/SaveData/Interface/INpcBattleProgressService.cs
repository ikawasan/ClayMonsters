using SaveData;

namespace SaveData.Interface
{
    /// <summary>
    /// CPU戦の敵種類と強さのアンロック進捗
    /// </summary>
    public interface INpcBattleProgressService
    {
        /// <summary>
        /// 開放済み敵スロット数
        /// </summary>
        int UnlockedEnemyCount { get; }

        /// <summary>
        /// 敵スロットが選択可能か
        /// </summary>
        /// <param name="slotIndex">敵スロット</param>
        bool IsEnemySlotUnlocked(int slotIndex);

        /// <summary>
        /// 指定スロットの強さが選択可能か
        /// </summary>
        /// <param name="slotIndex">敵スロット</param>
        /// <param name="tier">強さ</param>
        bool IsEnemyStrengthUnlocked(int slotIndex, EnemyStrengthTier tier);

        /// <summary>
        /// 指定スロットで選択可能な最高強さを返す
        /// </summary>
        /// <param name="slotIndex">敵スロット</param>
        EnemyStrengthTier GetHighestUnlockedStrength(int slotIndex);

        /// <summary>
        /// 勝利時に進捗を進め変化があればtrue
        /// </summary>
        /// <param name="slotIndex">倒した敵スロット</param>
        /// <param name="tier">倒した強さ</param>
        bool RegisterVictory(int slotIndex, EnemyStrengthTier tier);

        /// <summary>
        /// 全敵スロットと全強さを開放する
        /// </summary>
        void UnlockAllProgress();

        /// <summary>
        /// 進捗を初期状態へ戻す
        /// </summary>
        void ResetProgress();

        /// <summary>
        /// 永続化データから進捗を再読込する
        /// </summary>
        void Reload();
    }
}
