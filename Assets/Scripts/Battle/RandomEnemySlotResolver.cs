using SaveData;
using SaveData.Interface;
using System.Collections.Generic;

namespace Battle
{
    /// <summary>
    /// 敵セーブデータから使用中スロットをランダムに1つ選ぶ
    /// 使用中スロットが無い場合は指定の既定スロットを返す
    /// </summary>
    public static class RandomEnemySlotResolver
    {
        /// <summary>
        /// 使用中の敵スロットからランダムにスロット番号を返す
        /// </summary>
        /// <param name="saveService">セーブサービス</param>
        /// <param name="fallbackSlotIndex">使用中スロットが無い場合の既定スロット</param>
        /// <returns>選ばれた敵スロット番号</returns>
        public static int Resolve(IClayModelSaveService saveService, int fallbackSlotIndex)
        {
            if (saveService == null)
            {
                return fallbackSlotIndex;
            }

            var usedSlots = new List<int>();
            for (int i = 0; i < ModelSavePoolSettings.GetSlotCount(ModelSavePool.Enemy); i++)
            {
                ModelSaveSlot slot = saveService.GetSlot(ModelSavePool.Enemy, i);
                if (slot != null
                    && slot.isUsed
                    && !string.IsNullOrEmpty(slot.glbFileName)
                    && ModelSaveStorage.Exists(slot.glbFileName))
                {
                    usedSlots.Add(i);
                }
            }

            if (usedSlots.Count == 0)
            {
                return fallbackSlotIndex;
            }

            return usedSlots[UnityEngine.Random.Range(0, usedSlots.Count)];
        }
    }
}
