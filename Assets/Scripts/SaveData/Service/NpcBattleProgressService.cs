using SaveData.Interface;
using UnityEngine;

namespace SaveData.Service
{
    /// <summary>
    /// CPU戦アンロック進捗の読み書き
    /// 敵種類の開放とスロットごとの強さ開放は別軸
    /// </summary>
    public sealed class NpcBattleProgressService : INpcBattleProgressService
    {
        private NpcBattleProgressSaveData progress;

        /// <summary>
        /// 永続化データを読み込む
        /// </summary>
        public NpcBattleProgressService()
        {
            ReloadFromDisk();
        }

        /// <inheritdoc />
        public int UnlockedEnemyCount =>
            NpcBattleProgressRules.ClampUnlockedEnemyCount(progress.unlockedEnemyCount);

        /// <inheritdoc />
        public bool IsEnemySlotUnlocked(int slotIndex)
        {
            if (!ModelSavePoolSettings.IsValidSlotIndex(ModelSavePool.Enemy, slotIndex))
            {
                return false;
            }

            return slotIndex < UnlockedEnemyCount;
        }

        /// <inheritdoc />
        public bool IsEnemyStrengthUnlocked(int slotIndex, EnemyStrengthTier tier)
        {
            if (!IsEnemySlotUnlocked(slotIndex))
            {
                return false;
            }

            int tierValue = (int)tier;
            int strengthCount = GetSlotStrengthCount(slotIndex);
            return tierValue >= 0 && tierValue < strengthCount;
        }

        /// <inheritdoc />
        public EnemyStrengthTier GetHighestUnlockedStrength(int slotIndex)
        {
            if (!IsEnemySlotUnlocked(slotIndex))
            {
                return EnemyStrengthTier.Weak;
            }

            int highest = GetSlotStrengthCount(slotIndex) - 1;
            if (highest < 0)
            {
                return EnemyStrengthTier.Weak;
            }

            return (EnemyStrengthTier)highest;
        }

        /// <inheritdoc />
        public bool RegisterVictory(int slotIndex, EnemyStrengthTier tier)
        {
            if (!ModelSavePoolSettings.IsValidSlotIndex(ModelSavePool.Enemy, slotIndex))
            {
                Debug.LogWarning(
                    $"[NpcBattleProgressService] 無効な敵スロットです slot={slotIndex}");
                return false;
            }

            if (!IsEnemySlotUnlocked(slotIndex) || !IsEnemyStrengthUnlocked(slotIndex, tier))
            {
                Debug.LogWarning(
                    "[NpcBattleProgressService] 未開放の組み合わせへの勝利です"
                    + $" slot={slotIndex}"
                    + $" tier={tier}");
                return false;
            }

            EnsureSlotStrengthArray();
            bool changed = false;
            int enemyCount = UnlockedEnemyCount;
            int slotStrengthCount = GetSlotStrengthCount(slotIndex);

            // そのスロットの現在最高強さに勝利すると同スロットの次の強さを開放する
            if ((int)tier == slotStrengthCount - 1
                && slotStrengthCount < NpcBattleProgressRules.MaxStrengthCount)
            {
                progress.slotUnlockedStrengthCounts[slotIndex] = slotStrengthCount + 1;
                changed = true;
            }

            // 最前線の敵に勝利すると次の敵を弱いのみで開放する
            if (slotIndex == enemyCount - 1
                && enemyCount < NpcBattleProgressRules.MaxEnemyCount)
            {
                int nextSlot = enemyCount;
                progress.unlockedEnemyCount = enemyCount + 1;
                progress.slotUnlockedStrengthCounts[nextSlot] =
                    NpcBattleProgressRules.InitialSlotStrengthCount;
                changed = true;
            }

            if (!changed)
            {
                return false;
            }

            Persist();
            Debug.Log(
                "[NpcBattleProgressService] CPU戦進捗を更新しました"
                + $" slot={slotIndex}"
                + $" tier={tier}"
                + $" unlockedEnemyCount={progress.unlockedEnemyCount}"
                + $" slotStrength={GetSlotStrengthCount(slotIndex)}");
            return true;
        }

        /// <inheritdoc />
        public void UnlockAllProgress()
        {
            int maxEnemy = NpcBattleProgressRules.MaxEnemyCount;
            progress = new NpcBattleProgressSaveData
            {
                unlockedEnemyCount = maxEnemy,
                slotUnlockedStrengthCounts =
                    NpcBattleProgressRules.CreateFullyUnlockedSlotStrengthCounts()
            };
            Persist();
            Debug.Log(
                "[NpcBattleProgressService] CPU戦進捗を全解放しました"
                + $" unlockedEnemyCount={progress.unlockedEnemyCount}"
                + $" strength={NpcBattleProgressRules.MaxStrengthCount}");
        }

        /// <inheritdoc />
        public void ResetProgress()
        {
            progress = CreateInitialProgress();
            Persist();
            Debug.Log("[NpcBattleProgressService] CPU戦進捗を初期化しました");
        }

        /// <inheritdoc />
        public void Reload()
        {
            ReloadFromDisk();
        }

        private int GetSlotStrengthCount(int slotIndex)
        {
            EnsureSlotStrengthArray();
            if (slotIndex < 0 || slotIndex >= progress.slotUnlockedStrengthCounts.Length)
            {
                return 0;
            }

            return NpcBattleProgressRules.ClampSlotStrengthCount(
                progress.slotUnlockedStrengthCounts[slotIndex],
                slotUnlocked: slotIndex < UnlockedEnemyCount);
        }

        private void EnsureSlotStrengthArray()
        {
            Normalize(progress);
        }

        private void ReloadFromDisk()
        {
            SaveData saveData = SaveDataManager.Load();
            progress = saveData.NpcBattleProgress ?? CreateInitialProgress();
            Normalize(progress);
        }

        private void Persist()
        {
            Normalize(progress);
            SaveDataManager.Update(data =>
            {
                data.NpcBattleProgress = progress;
            });
        }

        private static NpcBattleProgressSaveData CreateInitialProgress()
        {
            return new NpcBattleProgressSaveData
            {
                unlockedEnemyCount = NpcBattleProgressRules.InitialUnlockedEnemyCount,
                slotUnlockedStrengthCounts = NpcBattleProgressRules.CreateInitialSlotStrengthCounts()
            };
        }

        private static void Normalize(NpcBattleProgressSaveData data)
        {
            if (data == null)
            {
                return;
            }

            data.unlockedEnemyCount = NpcBattleProgressRules.ClampUnlockedEnemyCount(
                data.unlockedEnemyCount);

            int maxEnemy = NpcBattleProgressRules.MaxEnemyCount;
            if (data.slotUnlockedStrengthCounts == null
                || data.slotUnlockedStrengthCounts.Length != maxEnemy)
            {
                int[] resized = new int[maxEnemy];
                if (data.slotUnlockedStrengthCounts != null)
                {
                    int copy = Mathf.Min(data.slotUnlockedStrengthCounts.Length, maxEnemy);
                    for (int i = 0; i < copy; i++)
                    {
                        resized[i] = data.slotUnlockedStrengthCounts[i];
                    }
                }

                data.slotUnlockedStrengthCounts = resized;
            }

            for (int i = 0; i < maxEnemy; i++)
            {
                bool unlocked = i < data.unlockedEnemyCount;
                data.slotUnlockedStrengthCounts[i] = NpcBattleProgressRules.ClampSlotStrengthCount(
                    data.slotUnlockedStrengthCounts[i],
                    unlocked);
            }

            // 開放済みスロットで強さ0なら弱いを補う
            for (int i = 0; i < data.unlockedEnemyCount && i < maxEnemy; i++)
            {
                if (data.slotUnlockedStrengthCounts[i] < NpcBattleProgressRules.InitialSlotStrengthCount)
                {
                    data.slotUnlockedStrengthCounts[i] = NpcBattleProgressRules.InitialSlotStrengthCount;
                }
            }
        }
    }
}
