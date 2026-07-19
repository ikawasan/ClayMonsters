using ClayEditor.Rigging;
using SaveData;
using System.Collections.Generic;
using UnityEngine;

namespace Scene.TrainingScene.Domain
{
    /// <summary>
    /// 1回の行動結果
    /// </summary>
    public readonly struct TrainingActionResult
    {
        public TrainingActionResult(
            TrainingLocation location,
            bool succeeded,
            bool failedByLowStamina,
            bool isRestAction,
            bool isGreatSuccess,
            int staminaBefore,
            int staminaAfter,
            TrainingStatGain appliedGain)
        {
            Location = location;
            Succeeded = succeeded;
            FailedByLowStamina = failedByLowStamina;
            IsRestAction = isRestAction;
            IsGreatSuccess = isGreatSuccess;
            StaminaBefore = staminaBefore;
            StaminaAfter = staminaAfter;
            AppliedGain = appliedGain;
        }

        /// <summary>
        /// 選択した行き先
        /// </summary>
        public TrainingLocation Location { get; }

        /// <summary>
        /// 行動が成功したか
        /// </summary>
        public bool Succeeded { get; }

        /// <summary>
        /// 低体力による失敗か
        /// </summary>
        public bool FailedByLowStamina { get; }

        /// <summary>
        /// 休憩行動か
        /// </summary>
        public bool IsRestAction { get; }

        /// <summary>
        /// 大成功だったか
        /// </summary>
        public bool IsGreatSuccess { get; }

        /// <summary>
        /// 行動前体力
        /// </summary>
        public int StaminaBefore { get; }

        /// <summary>
        /// 行動後体力
        /// </summary>
        public int StaminaAfter { get; }

        /// <summary>
        /// 適用されたステータス上昇
        /// </summary>
        public TrainingStatGain AppliedGain { get; }
    }

    /// <summary>
    /// 育成セッションの進行状態
    /// </summary>
    public sealed class TrainingSession
    {
        /// <summary>
        /// 育成対象スロット番号
        /// </summary>
        public int PlayerSlotIndex { get; }

        /// <summary>
        /// 現在の曜日
        /// </summary>
        public TrainingDayOfWeek CurrentDay { get; private set; }

        /// <summary>
        /// 当日の完了ターン数
        /// </summary>
        public int TurnIndexInDay { get; private set; }

        /// <summary>
        /// 行動体力
        /// </summary>
        public int Stamina { get; private set; }

        /// <summary>
        /// 現在のステータス
        /// </summary>
        public ModelStatus CurrentStatus { get; }

        /// <summary>
        /// 現在の攻撃構成
        /// </summary>
        public List<MotionType> AttackMotions { get; }

        /// <summary>
        /// 育成完了済みか
        /// </summary>
        public bool IsCompleted { get; private set; }

        /// <summary>
        /// 育成完了状態へ遷移する
        /// </summary>
        public void MarkCompleted()
        {
            IsCompleted = true;
        }

        /// <summary>
        /// セッションを生成する
        /// </summary>
        /// <param name="playerSlotIndex">対象スロット</param>
        /// <param name="baseStatus">開始時ステータス</param>
        /// <param name="attackMotions">開始時攻撃</param>
        public TrainingSession(int playerSlotIndex, ModelStatus baseStatus, IReadOnlyList<MotionType> attackMotions)
        {
            PlayerSlotIndex = playerSlotIndex;
            CurrentDay = TrainingDayOfWeek.Monday;
            TurnIndexInDay = 0;
            Stamina = TrainingSettings.MaxStamina;
            CurrentStatus = ModelStatus.CloneOrDefault(baseStatus);
            AttackMotions = ModelAttackMotionUtility.Normalize(attackMotions, TrainingSettings.AttackSlotCount);
        }

        /// <summary>
        /// 保存データから育成セッションを復元する
        /// </summary>
        /// <param name="playerSlotIndex">対象スロット</param>
        /// <param name="progress">育成途中データ</param>
        /// <returns>復元したセッション</returns>
        public static TrainingSession Resume(int playerSlotIndex, TrainingSlotProgress progress)
        {
            var session = new TrainingSession(
                playerSlotIndex,
                progress.status,
                progress.attackMotions);
            session.CurrentDay = (TrainingDayOfWeek)Mathf.Clamp(
                progress.day,
                (int)TrainingDayOfWeek.Monday,
                (int)TrainingDayOfWeek.Friday);
            session.TurnIndexInDay = Mathf.Clamp(
                progress.turnIndexInDay,
                0,
                TrainingDailySchedule.TurnsPerDay);
            session.Stamina = Mathf.Clamp(progress.stamina, 0, TrainingSettings.MaxStamina);
            return session;
        }

        /// <summary>
        /// 行動結果を適用する
        /// </summary>
        /// <param name="result">行動結果</param>
        public void ApplyAction(TrainingActionResult result)
        {
            Stamina = result.StaminaAfter;
            if (result.Succeeded)
            {
                ApplyGain(result.AppliedGain);
            }

            TurnIndexInDay++;
        }

        /// <summary>
        /// 翌日へ進む
        /// </summary>
        public void AdvanceDay()
        {
            TurnIndexInDay = 0;

            if ((int)CurrentDay >= TrainingSettings.TotalDays)
            {
                IsCompleted = true;
                return;
            }

            CurrentDay = (TrainingDayOfWeek)((int)CurrentDay + 1);
        }

        /// <summary>
        /// イベントのステータス上昇を適用する
        /// </summary>
        /// <param name="gain">上昇量</param>
        public void ApplyEventStatGain(TrainingStatGain gain)
        {
            ApplyGain(gain);
        }

        /// <summary>
        /// 指定スロットの攻撃を入れ替える
        /// </summary>
        /// <param name="slotIndex">攻撃スロット(0〜3)</param>
        /// <param name="newAttack">新しい攻撃</param>
        /// <returns>入れ替えに成功したか</returns>
        public bool TryReplaceAttack(int slotIndex, MotionType newAttack)
        {
            if (slotIndex < 0 || slotIndex >= TrainingSettings.AttackSlotCount || slotIndex >= AttackMotions.Count)
            {
                return false;
            }

            AttackMotions[slotIndex] = newAttack;
            return true;
        }

        /// <summary>
        /// 時間割ターンを完了する
        /// </summary>
        public void CompletePeriod()
        {
            TurnIndexInDay++;
        }

        /// <summary>
        /// 放課後勝利時の体力回復を適用する
        /// </summary>
        public void ApplyAfterSchoolVictoryRecovery()
        {
            Stamina = System.Math.Min(
                TrainingSettings.MaxStamina,
                Stamina + TrainingSettings.AfterSchoolVictoryStaminaRecovery);
        }

        private void ApplyGain(TrainingStatGain gain)
        {
            CurrentStatus.hp += gain.Hp;
            CurrentStatus.attack += gain.Attack;
            CurrentStatus.defense += gain.Defense;
            CurrentStatus.speed += gain.Speed;
        }

    }
}
