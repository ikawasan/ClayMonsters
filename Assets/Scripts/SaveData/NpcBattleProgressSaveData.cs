using System;

namespace SaveData
{
    /// <summary>
    /// CPU戦のアンロック進捗
    /// 敵種類の開放とスロットごとの強さ開放は別軸
    /// </summary>
    [Serializable]
    public sealed class NpcBattleProgressSaveData
    {
        /// <summary>
        /// 開放済み敵スロット数(初期1=スロット0のみ)
        /// </summary>
        public int unlockedEnemyCount = 1;

        /// <summary>
        /// スロットごとの開放済み強さ段階数
        /// 要素数は敵スロット数索引iの値がそのスロットの開放数
        /// </summary>
        public int[] slotUnlockedStrengthCounts;
    }
}
