namespace Scene.TrainingScene.Domain
{
    /// <summary>
    /// 育成モードの定数とバランス設定
    /// </summary>
    public static class TrainingSettings
    {
        /// <summary>
        /// 育成日数
        /// </summary>
        public const int TotalDays = 5;

        /// <summary>
        /// 1日のターン数
        /// </summary>
        public static int TurnsPerDay => TrainingDailySchedule.TurnsPerDay;

        /// <summary>
        /// 提示する行き先候補数
        /// </summary>
        public const int LocationChoiceCount = 3;

        /// <summary>
        /// 攻撃スロット数
        /// </summary>
        public const int AttackSlotCount = SaveData.ModelAttackMotionUtility.SlotCount;

        /// <summary>
        /// 行動体力の最大値
        /// </summary>
        public const int MaxStamina = 100;

        /// <summary>
        /// 行動1回あたりの体力消費
        /// </summary>
        public const int StaminaCostPerAction = 12;

        /// <summary>
        /// 休憩の通常回復量
        /// </summary>
        public const int RestStaminaRecovery = 40;

        /// <summary>
        /// 休憩の大成功時回復量
        /// </summary>
        public const int RestGreatSuccessRecovery = 80;

        /// <summary>
        /// 休憩の大成功率(百分率)
        /// </summary>
        public const float RestGreatSuccessPercent = 20f;

        /// <summary>
        /// 低体力判定のしきい値
        /// </summary>
        public const int LowStaminaThreshold = 30;

        /// <summary>
        /// 低体力時の基礎失敗率(百分率)
        /// </summary>
        public const float BaseFailurePercentAtLowStamina = 8f;

        /// <summary>
        /// しきい値を下回る体力1につき加算する失敗率(百分率)
        /// </summary>
        public const float FailurePercentPerStaminaBelowThreshold = 1.5f;

        /// <summary>
        /// 校長室の成功時ボーナス倍率
        /// </summary>
        public const float PrincipalOfficeBonusMultiplier = 1.6f;

        /// <summary>
        /// 放課後勝利時の体力回復量
        /// </summary>
        public const int AfterSchoolVictoryStaminaRecovery = 20;

        /// <summary>
        /// 行動成功後に技習得イベントが発生する確率(百分率)
        /// </summary>
        public const float LearnAttackEventTriggerPercent = 10f;

        /// <summary>
        /// 行動成功後に特訓イベントが発生する確率(百分率)
        /// </summary>
        public const float StatBoostEventTriggerPercent = 8f;

        /// <summary>
        /// イベントのHP上昇最小
        /// </summary>
        public const int EventStatHpMin = 4;

        /// <summary>
        /// イベントのHP上昇最大
        /// </summary>
        public const int EventStatHpMax = 12;

        /// <summary>
        /// イベントの攻撃上昇最小
        /// </summary>
        public const int EventStatAttackMin = 2;

        /// <summary>
        /// イベントの攻撃上昇最大
        /// </summary>
        public const int EventStatAttackMax = 6;

        /// <summary>
        /// イベントの防御上昇最小
        /// </summary>
        public const int EventStatDefenseMin = 2;

        /// <summary>
        /// イベントの防御上昇最大
        /// </summary>
        public const int EventStatDefenseMax = 6;

        /// <summary>
        /// イベントの速度上昇最小
        /// </summary>
        public const int EventStatSpeedMin = 1;

        /// <summary>
        /// イベントの速度上昇最大
        /// </summary>
        public const int EventStatSpeedMax = 4;
    }
}
