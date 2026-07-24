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
        /// 週番号に応じた敵スロットを返す
        /// </summary>
        /// <param name="saveService">セーブサービス</param>
        /// <param name="week">現在週(1始まり)</param>
        /// <param name="slotIndex">敵スロット</param>
        public static bool TryPickEnemySlotIndex(
            IClayModelSaveService saveService,
            int week,
            out int slotIndex)
        {
            slotIndex = -1;
            if (saveService == null || !saveService.HasAnySavedModel(ModelSavePool.Enemy))
            {
                return false;
            }

            int preferred = (System.Math.Max(1, week) - 1) % ModelSavePoolSettings.SlotCount;
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

        /// <summary>
        /// 互換用の曜日指定
        /// </summary>
        public static bool TryPickEnemySlotIndex(
            IClayModelSaveService saveService,
            TrainingDayOfWeek day,
            out int slotIndex)
        {
            return TryPickEnemySlotIndex(saveService, (int)day, out slotIndex);
        }

        /// <summary>
        /// 強敵急襲向けに強めの敵スロットを返す
        /// </summary>
        /// <param name="saveService">セーブサービス</param>
        /// <param name="slotIndex">敵スロット</param>
        /// <returns>見つかったらtrue</returns>
        public static bool TryPickAmbushEnemySlotIndex(
            IClayModelSaveService saveService,
            out int slotIndex)
        {
            slotIndex = -1;
            if (saveService == null || !saveService.HasAnySavedModel(ModelSavePool.Enemy))
            {
                return false;
            }

            int best = -1;
            for (int i = 0; i < ModelSavePoolSettings.SlotCount; i++)
            {
                if (!IsUsedEnemySlot(saveService, i))
                {
                    continue;
                }

                best = i;
            }

            if (best < 0)
            {
                return false;
            }

            slotIndex = best;
            return true;
        }

        private static bool IsUsedEnemySlot(IClayModelSaveService saveService, int slotIndex)
        {
            ModelSaveSlot slot = saveService.GetSlot(ModelSavePool.Enemy, slotIndex);
            return slot != null && slot.isUsed && !string.IsNullOrEmpty(slot.glbFileName);
        }
    }
}
