using Battle;
using SaveData;
using System.Collections.Generic;
using UnityEngine;

namespace Scene.TrainingScene.Domain
{
    /// <summary>
    /// 行き先候補の抽選と行動判定を行う
    /// </summary>
    public static class TrainingActionResolver
    {
        /// <summary>
        /// 行き先候補をランダムに抽選する
        /// </summary>
        /// <param name="choiceCount">提示数</param>
        /// <param name="random">乱数</param>
        public static TrainingLocation[] PickLocationChoices(int choiceCount, System.Random random = null)
        {
            random ??= new System.Random();
            TrainingLocation[] pool = TrainingLocationCatalog.AllLocations;
            int count = Mathf.Clamp(choiceCount, 1, pool.Length);
            var indices = new List<int>(pool.Length);
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
        /// 行動を実行して結果を返す
        /// </summary>
        /// <param name="location">選択行き先</param>
        /// <param name="currentStamina">現在体力</param>
        /// <param name="random">乱数</param>
        public static TrainingActionResult ExecuteAction(
            TrainingLocation location,
            int currentStamina,
            System.Random random = null)
        {
            random ??= new System.Random();

            int staminaAfter = Mathf.Max(0, currentStamina - TrainingSettings.StaminaCostPerAction);
            bool failedByLowStamina = currentStamina <= TrainingSettings.LowStaminaThreshold
                && random.NextDouble() * 100d < ComputeFailurePercent(currentStamina);

            if (failedByLowStamina)
            {
                return new TrainingActionResult(
                    location,
                    succeeded: false,
                    failedByLowStamina: true,
                    isRestAction: false,
                    isGreatSuccess: false,
                    currentStamina,
                    staminaAfter,
                    default);
            }

            TrainingStatGain gain = BuildGain(location, random);
            return new TrainingActionResult(
                location,
                succeeded: true,
                failedByLowStamina: false,
                isRestAction: false,
                isGreatSuccess: false,
                currentStamina,
                staminaAfter,
                gain);
        }

        /// <summary>
        /// 休憩を実行して結果を返す
        /// </summary>
        /// <param name="currentStamina">現在体力</param>
        /// <param name="random">乱数</param>
        public static TrainingActionResult ExecuteRest(int currentStamina, System.Random random = null)
        {
            random ??= new System.Random();
            bool isGreatSuccess = random.NextDouble() * 100d < TrainingSettings.RestGreatSuccessPercent;
            int recovery = isGreatSuccess
                ? TrainingSettings.RestGreatSuccessRecovery
                : TrainingSettings.RestStaminaRecovery;
            int staminaAfter = Mathf.Min(TrainingSettings.MaxStamina, currentStamina + recovery);

            return new TrainingActionResult(
                default,
                succeeded: true,
                failedByLowStamina: false,
                isRestAction: true,
                isGreatSuccess,
                currentStamina,
                staminaAfter,
                default);
        }

        /// <summary>
        /// ステータスを戦闘用の範囲へ丸める
        /// </summary>
        /// <param name="status">対象ステータス</param>
        public static void ClampStatus(ModelStatus status)
        {
            if (status == null)
            {
                return;
            }

            BattleStatusBalance.Normalize(status, out int hp, out int attack, out int defense, out int speed);
            status.hp = hp;
            status.attack = attack;
            status.defense = defense;
            status.speed = speed;
        }

        /// <summary>
        /// 低体力時の失敗率を返す
        /// </summary>
        /// <param name="stamina">現在体力</param>
        public static float ComputeFailurePercent(int stamina)
        {
            if (stamina > TrainingSettings.LowStaminaThreshold)
            {
                return 0f;
            }

            float deficit = TrainingSettings.LowStaminaThreshold - stamina;
            return TrainingSettings.BaseFailurePercentAtLowStamina
                + deficit * TrainingSettings.FailurePercentPerStaminaBelowThreshold;
        }

        private static TrainingStatGain BuildGain(TrainingLocation location, System.Random random)
        {
            TrainingStatGain gain = TrainingLocationCatalog.GetBaseGain(location);
            if (location != TrainingLocation.PrincipalOffice)
            {
                return gain;
            }

            int bonusStat = random.Next(0, 4);
            float multiplier = TrainingSettings.PrincipalOfficeBonusMultiplier;
            return bonusStat switch
            {
                0 => new TrainingStatGain(
                    Mathf.RoundToInt(gain.Hp * multiplier),
                    gain.Attack,
                    gain.Defense,
                    gain.Speed),
                1 => new TrainingStatGain(
                    gain.Hp,
                    Mathf.RoundToInt(gain.Attack * multiplier),
                    gain.Defense,
                    gain.Speed),
                2 => new TrainingStatGain(
                    gain.Hp,
                    gain.Attack,
                    Mathf.RoundToInt(gain.Defense * multiplier),
                    gain.Speed),
                _ => new TrainingStatGain(
                    gain.Hp,
                    gain.Attack,
                    gain.Defense,
                    Mathf.RoundToInt(gain.Speed * multiplier))
            };
        }
    }
}
