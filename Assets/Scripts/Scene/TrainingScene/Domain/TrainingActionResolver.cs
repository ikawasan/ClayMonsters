using Battle;
using SaveData;
using UnityEngine;

namespace Scene.TrainingScene.Domain
{
    /// <summary>
    /// 週次コマンドの行動判定を行う
    /// </summary>
    public static class TrainingActionResolver
    {
        /// <summary>
        /// 訓練を実行して結果を返す
        /// </summary>
        /// <param name="focus">主ステ</param>
        /// <param name="session">育成セッション</param>
        /// <param name="random">乱数</param>
        public static TrainingActionResult ExecuteTrain(
            TrainingFocus focus,
            TrainingSession session,
            System.Random random = null)
        {
            float bonus = session != null ? session.TrainGreatSuccessBonusPercent : 0f;
            return ExecuteFocusCommand(
                TrainingCommandType.Train,
                focus,
                session != null ? session.Stamina : TrainingSettings.MaxStamina,
                TrainingSettings.TrainStaminaCost,
                TrainingSettings.TrainGreatSuccessPercent + bonus,
                1f,
                TrainingSettings.TrainGreatSuccessMultiplier,
                random);
        }

        /// <summary>
        /// 特訓を実行して結果を返す
        /// </summary>
        /// <param name="focus">主ステ</param>
        /// <param name="session">育成セッション</param>
        /// <param name="random">乱数</param>
        public static TrainingActionResult ExecuteSpecialTrain(
            TrainingFocus focus,
            TrainingSession session,
            System.Random random = null)
        {
            float bonus = session != null ? session.TrainGreatSuccessBonusPercent : 0f;
            return ExecuteFocusCommand(
                TrainingCommandType.SpecialTrain,
                focus,
                session != null ? session.Stamina : TrainingSettings.MaxStamina,
                TrainingSettings.SpecialTrainStaminaCost,
                TrainingSettings.SpecialTrainGreatSuccessPercent + bonus,
                TrainingSettings.SpecialTrainSuccessMultiplier,
                TrainingSettings.SpecialTrainGreatSuccessMultiplier,
                random);
        }

        /// <summary>
        /// 互換用の旧訓練API
        /// </summary>
        public static TrainingActionResult ExecuteTrain(
            TrainingFocus focus,
            int currentStamina,
            System.Random random = null)
        {
            return ExecuteFocusCommand(
                TrainingCommandType.Train,
                focus,
                currentStamina,
                TrainingSettings.TrainStaminaCost,
                TrainingSettings.TrainGreatSuccessPercent,
                1f,
                TrainingSettings.TrainGreatSuccessMultiplier,
                random);
        }

        /// <summary>
        /// 互換用の旧特訓API
        /// </summary>
        public static TrainingActionResult ExecuteSpecialTrain(
            TrainingFocus focus,
            int currentStamina,
            System.Random random = null)
        {
            return ExecuteFocusCommand(
                TrainingCommandType.SpecialTrain,
                focus,
                currentStamina,
                TrainingSettings.SpecialTrainStaminaCost,
                TrainingSettings.SpecialTrainGreatSuccessPercent,
                TrainingSettings.SpecialTrainSuccessMultiplier,
                TrainingSettings.SpecialTrainGreatSuccessMultiplier,
                random);
        }

        /// <summary>
        /// 休憩を実行して結果を返す
        /// </summary>
        /// <param name="currentStamina">現在体力</param>
        public static TrainingActionResult ExecuteRest(int currentStamina)
        {
            return new TrainingActionResult(
                TrainingCommandType.Rest,
                default,
                TrainingLocation.Library,
                succeeded: true,
                failedByLowStamina: false,
                isGreatSuccess: false,
                currentStamina,
                TrainingSettings.MaxStamina,
                default);
        }

        /// <summary>
        /// 互換用の旧行き先行動
        /// </summary>
        public static TrainingActionResult ExecuteAction(
            TrainingLocation location,
            int currentStamina,
            System.Random random = null)
        {
            TrainingFocus focus = location switch
            {
                TrainingLocation.HomeEcRoom => TrainingFocus.Hp,
                TrainingLocation.Gymnasium => TrainingFocus.Attack,
                TrainingLocation.CraftRoom => TrainingFocus.Defense,
                TrainingLocation.MusicRoom => TrainingFocus.Speed,
                TrainingLocation.Library => TrainingFocus.Hit,
                _ => TrainingFocus.Attack
            };
            return ExecuteTrain(focus, currentStamina, random);
        }

        /// <summary>
        /// 互換用の旧休憩
        /// </summary>
        public static TrainingActionResult ExecuteRest(
            int currentStamina,
            System.Random random)
        {
            return ExecuteRest(currentStamina);
        }

        /// <summary>
        /// 互換用の行き先抽選
        /// </summary>
        public static TrainingLocation[] PickLocationChoices(
            int choiceCount,
            System.Random random = null)
        {
            random ??= new System.Random();
            TrainingLocation[] pool = TrainingLocationCatalog.AllLocations;
            int count = Mathf.Clamp(choiceCount, 1, pool.Length);
            var indices = new System.Collections.Generic.List<int>(pool.Length);
            for (int i = 0; i < pool.Length; i++)
            {
                indices.Add(i);
            }

            var result = new TrainingLocation[count];
            for (int i = 0; i < count; i++)
            {
                int pick = random.Next(indices.Count);
                result[i] = pool[indices[pick]];
                indices.RemoveAt(pick);
            }

            return result;
        }

        /// <summary>
        /// ステータスを戦闘用の育成後上限へ丸める
        /// </summary>
        /// <param name="status">対象ステータス</param>
        public static void ClampStatus(ModelStatus status)
        {
            if (status == null)
            {
                return;
            }

            BattleStatusBalance.Normalize(
                status,
                out int hp,
                out int attack,
                out int defense,
                out int speed,
                out int hit);
            status.hp = hp;
            status.attack = attack;
            status.defense = defense;
            status.speed = speed;
            status.hit = hit;
        }

        /// <summary>
        /// 低体力時の失敗率を返す
        /// </summary>
        /// <param name="stamina">現在体力</param>
        /// <param name="requiredCost">必要消費</param>
        public static float ComputeFailurePercent(int stamina, int requiredCost)
        {
            if (stamina >= requiredCost)
            {
                return 0f;
            }

            float deficit = requiredCost - stamina;
            return TrainingSettings.BaseFailurePercentAtLowStamina
                + deficit * TrainingSettings.FailurePercentPerStaminaBelowThreshold;
        }

        /// <summary>
        /// 互換用の失敗率
        /// </summary>
        public static float ComputeFailurePercent(int stamina)
        {
            return ComputeFailurePercent(stamina, TrainingSettings.TrainStaminaCost);
        }

        private static TrainingActionResult ExecuteFocusCommand(
            TrainingCommandType command,
            TrainingFocus focus,
            int currentStamina,
            int cost,
            float greatSuccessPercent,
            float successMultiplier,
            float greatSuccessMultiplier,
            System.Random random)
        {
            random ??= new System.Random();
            int staminaAfter = Mathf.Max(0, currentStamina - cost);
            TrainingLocation presentation =
                TrainingFocusCatalog.GetPresentationLocation(focus);

            bool failedByLowStamina = currentStamina < cost
                && random.NextDouble() * 100d < ComputeFailurePercent(currentStamina, cost);
            if (failedByLowStamina)
            {
                return new TrainingActionResult(
                    command,
                    focus,
                    presentation,
                    succeeded: false,
                    failedByLowStamina: true,
                    isGreatSuccess: false,
                    currentStamina,
                    staminaAfter,
                    default);
            }

            bool isGreatSuccess =
                random.NextDouble() * 100d < Mathf.Min(100f, greatSuccessPercent);
            float multiplier = isGreatSuccess
                ? greatSuccessMultiplier
                : successMultiplier;
            TrainingStatGain gain = TrainingFocusCatalog.ScaleGain(
                TrainingFocusCatalog.GetBaseGain(focus),
                multiplier);
            return new TrainingActionResult(
                command,
                focus,
                presentation,
                succeeded: true,
                failedByLowStamina: false,
                isGreatSuccess,
                currentStamina,
                staminaAfter,
                gain);
        }
    }
}
