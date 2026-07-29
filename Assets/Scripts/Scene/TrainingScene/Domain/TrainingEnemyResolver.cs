using SaveData;
using SaveData.Interface;
using System.Collections.Generic;

namespace Scene.TrainingScene.Domain
{
    /// <summary>
    /// 放課後戦闘と強敵急襲の敵スロットを決める
    /// </summary>
    public static class TrainingEnemyResolver
    {
        /// <summary>
        /// 曜日に応じた放課後戦闘の敵強さ段階を返す
        /// </summary>
        /// <param name="day">育成曜日</param>
        public static EnemyStrengthTier ResolveAfterSchoolTier(TrainingDayOfWeek day)
        {
            return day switch
            {
                TrainingDayOfWeek.Monday => EnemyStrengthTier.Weak,
                TrainingDayOfWeek.Tuesday => EnemyStrengthTier.Normal,
                TrainingDayOfWeek.Wednesday => EnemyStrengthTier.Normal,
                TrainingDayOfWeek.Thursday => EnemyStrengthTier.Strong,
                TrainingDayOfWeek.Friday => EnemyStrengthTier.VeryStrong,
                _ => EnemyStrengthTier.Normal
            };
        }

        /// <summary>
        /// 曜日に応じた強敵急襲の敵強さ段階を返す
        /// 放課後より1段階強い
        /// </summary>
        /// <param name="day">育成曜日</param>
        public static EnemyStrengthTier ResolveAmbushTier(TrainingDayOfWeek day)
        {
            int next = (int)ResolveAfterSchoolTier(day) + 1;
            if (next > (int)EnemyStrengthTier.VeryStrong)
            {
                return EnemyStrengthTier.VeryStrong;
            }

            return (EnemyStrengthTier)next;
        }

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
            TrainingDayOfWeek day = (TrainingDayOfWeek)UnityEngine.Mathf.Clamp(
                week,
                (int)TrainingDayOfWeek.Monday,
                TrainingSettings.TotalDays);
            return TryPickEnemySlotIndex(saveService, day, out slotIndex);
        }

        /// <summary>
        /// 曜日指定の放課後戦闘敵を返す
        /// 全敵モデルからランダムに選ぶ
        /// </summary>
        public static bool TryPickEnemySlotIndex(
            IClayModelSaveService saveService,
            TrainingDayOfWeek day,
            out int slotIndex)
        {
            return TryPickAnyUsed(saveService, out slotIndex);
        }

        /// <summary>
        /// 強敵急襲向けの敵スロットを返す
        /// 全敵モデルからランダムに選ぶ
        /// </summary>
        /// <param name="saveService">セーブサービス</param>
        /// <param name="day">育成曜日</param>
        /// <param name="slotIndex">敵スロット</param>
        /// <returns>見つかったらtrue</returns>
        public static bool TryPickAmbushEnemySlotIndex(
            IClayModelSaveService saveService,
            TrainingDayOfWeek day,
            out int slotIndex)
        {
            return TryPickAnyUsed(saveService, out slotIndex);
        }

        /// <summary>
        /// 互換用の強敵急襲選出
        /// </summary>
        public static bool TryPickAmbushEnemySlotIndex(
            IClayModelSaveService saveService,
            out int slotIndex)
        {
            return TryPickAnyUsed(saveService, out slotIndex);
        }

        private static bool TryPickAnyUsed(
            IClayModelSaveService saveService,
            out int slotIndex)
        {
            slotIndex = -1;
            if (saveService == null || !saveService.HasAnySavedModel(ModelSavePool.Enemy))
            {
                return false;
            }

            var matches = new List<int>();
            for (int i = 0; i < ModelSavePoolSettings.GetSlotCount(ModelSavePool.Enemy); i++)
            {
                ModelSaveSlot slot = saveService.GetSlot(ModelSavePool.Enemy, i);
                if (slot == null || !slot.isUsed || string.IsNullOrEmpty(slot.glbFileName))
                {
                    continue;
                }

                matches.Add(i);
            }

            if (matches.Count == 0)
            {
                return false;
            }

            slotIndex = matches[UnityEngine.Random.Range(0, matches.Count)];
            return true;
        }
    }
}
