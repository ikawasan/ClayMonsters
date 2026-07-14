using SaveData;
using SaveData.Interface;

namespace Scene.TrainingScene.Domain
{
    /// <summary>
    /// 放課後戦闘の敵スロットを決める
    /// </summary>
    public static class TrainingEnemyResolver
    {
        /// <summary>
        /// 曜日に応じた敵スロットを返す
        /// </summary>
        /// <param name="saveService">セーブサービス</param>
        /// <param name="day">曜日</param>
        /// <param name="slotIndex">敵スロット</param>
        public static bool TryPickEnemySlotIndex(
            IClayModelSaveService saveService,
            TrainingDayOfWeek day,
            out int slotIndex)
        {
            slotIndex = -1;
            if (saveService == null || !saveService.HasAnySavedModel(ModelSavePool.Enemy))
            {
                return false;
            }

            int preferred = ((int)day - 1) % ModelSavePoolSettings.SlotCount;
            if (IsUsedEnemySlot(saveService, preferred))
            {
                slotIndex = preferred;
                return true;
            }

            for (int i = 0; i < ModelSavePoolSettings.SlotCount; i++)
            {
                if (!IsUsedEnemySlot(saveService, i))
                {
                    continue;
                }

                slotIndex = i;
                return true;
            }

            return false;
        }

        private static bool IsUsedEnemySlot(IClayModelSaveService saveService, int slotIndex)
        {
            ModelSaveSlot slot = saveService.GetSlot(ModelSavePool.Enemy, slotIndex);
            return slot != null && slot.isUsed && !string.IsNullOrEmpty(slot.glbFileName);
        }
    }
}
