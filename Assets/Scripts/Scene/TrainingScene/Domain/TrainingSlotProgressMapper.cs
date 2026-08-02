using Localization;
using ClayEditor.Rigging;
using SaveData;
using SaveData.Interface;
using System.Collections.Generic;
using UI.ClayEditor.View;
using UnityEngine;

namespace Scene.TrainingScene.Domain
{
    /// <summary>
    /// 育成セッションとスロット保存データの相互変換
    /// </summary>
    public static class TrainingSlotProgressMapper
    {
        /// <summary>
        /// 保存データが再開可能か検証する
        /// </summary>
        /// <param name="progress">育成途中データ</param>
        /// <returns>再開可能ならtrue</returns>
        public static bool IsResumable(TrainingSlotProgress progress)
        {
            if (progress == null || !progress.inProgress)
            {
                return false;
            }

            // 最終日を越えた保存は育成完了扱いで再開しない
            if (progress.day > TrainingSettings.TotalDays)
            {
                return false;
            }

            if (progress.status == null)
            {
                Debug.LogError("[TrainingSlotProgressMapper] 育成途中データのステータスが欠落しています");
                return false;
            }

            if (progress.day < 1)
            {
                Debug.LogError($"[TrainingSlotProgressMapper] 育成途中データの日付が不正です: {progress.day}");
                return false;
            }

            if (progress.turnIndexInDay < 0
                || progress.turnIndexInDay > TrainingDailySchedule.TurnsPerDay)
            {
                Debug.LogError(
                    $"[TrainingSlotProgressMapper] 育成途中データのターン位置が不正です: {progress.turnIndexInDay}");
                return false;
            }

            if (progress.attackMotions == null || progress.attackMotions.Count == 0)
            {
                // 技が空でも再開は許可し原因が分かるようエラーだけ出す
                Debug.LogError("[TrainingSlotProgressMapper] 育成途中データの攻撃が空です");
            }

            return true;
        }

        /// <summary>
        /// セッション状態を保存データへ変換する
        /// </summary>
        /// <param name="session">育成セッション</param>
        /// <returns>育成途中データ</returns>
        public static TrainingSlotProgress ToSaveData(TrainingSession session)
        {
            if (session == null || session.IsCompleted)
            {
                return null;
            }

            return new TrainingSlotProgress
            {
                inProgress = true,
                day = (int)session.CurrentDay,
                turnIndexInDay = session.TurnIndexInDay,
                stamina = session.Stamina,
                motivation = (int)session.Motivation,
                money = session.Money,
                trainGreatSuccessBonusPercent = session.TrainGreatSuccessBonusPercent,
                trainGreatSuccessBonusWeeks = session.TrainGreatSuccessBonusWeeks,
                inventory = session.CloneInventoryForSave(),
                shopOfferItemIds = session.CloneShopOfferForSave(),
                status = new ModelStatus
                {
                    hp = session.CurrentStatus.hp,
                    attack = session.CurrentStatus.attack,
                    defense = session.CurrentStatus.defense,
                    speed = session.CurrentStatus.speed,
                    hit = session.CurrentStatus.hit
                },
                attackMotions = new List<MotionType>(session.AttackMotions)
            };
        }

        /// <summary>
        /// 保存データからセッションを復元する
        /// </summary>
        /// <param name="slotIndex">対象スロット</param>
        /// <param name="progress">育成途中データ</param>
        /// <returns>復元したセッション</returns>
        public static TrainingSession FromSaveData(int slotIndex, TrainingSlotProgress progress)
        {
            return TrainingSession.Resume(slotIndex, progress);
        }

        /// <summary>
        /// 再開可能なプレイヤースロットを検索する
        /// </summary>
        public static bool TryFindResumablePlayerSlot(
            IClayModelSaveService saveService,
            out int slotIndex,
            out ModelSaveSlot slot,
            out TrainingSlotProgress progress)
        {
            slotIndex = -1;
            slot = null;
            progress = null;
            if (saveService == null)
            {
                return false;
            }

            for (int i = 0; i < ModelSavePoolSettings.GetSlotCount(ModelSavePool.Player); i++)
            {
                TrainingSlotProgress candidate =
                    saveService.GetTrainingProgress(ModelSavePool.Player, i);
                if (!IsResumable(candidate))
                {
                    continue;
                }

                ModelSaveSlot candidateSlot = saveService.GetSlot(ModelSavePool.Player, i);
                if (candidateSlot == null || string.IsNullOrEmpty(candidateSlot.glbFileName))
                {
                    continue;
                }

                slotIndex = i;
                slot = candidateSlot;
                progress = candidate;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 再開確認ウィンドウ向けの表示データを返す
        /// </summary>
        public static TrainingResumeProgressPresentation BuildResumePresentation(
            ModelSaveSlot slot,
            TrainingSlotProgress progress)
        {
            if (progress == null)
            {
                return new TrainingResumeProgressPresentation(
                    slot != null ? slot.modelName : string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    System.Array.Empty<MotionType>(),
                    null);
            }

            string periodLabel = ResolvePeriodDisplayName(progress.turnIndexInDay);
            TrainingMotivation motivation = TrainingMotivationCatalog.Clamp(progress.motivation);
            string scheduleMoneyLabel =
                $"{TrainingDayCatalog.GetDisplayName((TrainingDayOfWeek)progress.day)}"
                + (string.IsNullOrEmpty(periodLabel) ? string.Empty : $" {periodLabel}")
                + $", 所持金 {progress.money}G";
            const string motivationLabel = "やる気　";
            string staminaLabel = $", 体力 {progress.stamina} / {TrainingSettings.MaxStamina}";
            ModelStatus status = progress.status ?? new ModelStatus();
            string statsText =
                $"HP {status.hp}\n"
                + $"攻撃 {status.attack}\n"
                + $"防御 {status.defense}\n"
                + $"速度 {status.speed}\n"
                + $"命中 {status.hit}";

            return new TrainingResumeProgressPresentation(
                slot != null ? slot.modelName : string.Empty,
                scheduleMoneyLabel,
                motivationLabel,
                staminaLabel,
                statsText,
                ModelAttackMotionUtility.Normalize(
                    progress.attackMotions,
                    TrainingSettings.AttackSlotCount),
                ModelSaveStorage.ReadThumbnailPng(slot),
                motivation);
        }

        /// <summary>
        /// 途中データの時間割表示名を返す
        /// </summary>
        /// <param name="turnIndexInDay">日内ターン位置</param>
        private static string ResolvePeriodDisplayName(int turnIndexInDay)
        {
            TrainingPeriod[] periods = TrainingDailySchedule.AllPeriods;
            if (turnIndexInDay < 0 || turnIndexInDay >= periods.Length)
            {
                return string.Empty;
            }

            return TrainingPeriodCatalog.GetDisplayName(periods[turnIndexInDay]);
        }

        /// <summary>
        /// 再開確認用の概要文を返す
        /// </summary>
        public static string FormatResumeSummary(TrainingSlotProgress progress)
        {
            if (progress == null)
            {
                return string.Empty;
            }

            ModelStatus status = progress.status ?? new ModelStatus();
            string periodLabel = ResolvePeriodDisplayName(progress.turnIndexInDay);
            return $"{TrainingDayCatalog.GetDisplayName((TrainingDayOfWeek)progress.day)}"
                + (string.IsNullOrEmpty(periodLabel) ? string.Empty : $" {periodLabel}")
                + $", 所持金 {progress.money}G"
                + $"\nやる気{TrainingMotivationCatalog.GetIconGlyph(TrainingMotivationCatalog.Clamp(progress.motivation))}"
                + $" 体力{progress.stamina}/{TrainingSettings.MaxStamina}"
                + $"\nHP {status.hp} 攻撃 {status.attack}"
                + $" 防御 {status.defense} 速度 {status.speed}"
                + $" 命中 {status.hit}";
        }

        /// <summary>
        /// 再開確認用の概要文を返す
        /// </summary>
        public static string FormatResumeSummary(ModelSaveSlot slot, TrainingSlotProgress progress)
        {
            string body = FormatResumeSummary(progress);
            if (slot == null || string.IsNullOrEmpty(slot.modelName))
            {
                return body;
            }

            return $"{slot.modelName}\n{body}";
        }

        /// <summary>
        /// 育成完了リザルト向けの表示データを返す
        /// </summary>
        public static TrainingAutoResultPresentation BuildAutoResultPresentation(
            string modelName,
            ModelStatus status,
            IReadOnlyList<MotionType> attacks,
            string saveResultMessage = "",
            string continueButtonLabel = "保存先を選ぶ",
            byte[] thumbnailPng = null,
            string titleText = null)
        {
            ModelStatus resolvedStatus = status ?? new ModelStatus();
            string statsText =
                ModelSaveSummaryFormatter.FormatTrainingFinalStatusParameters(resolvedStatus);

            return new TrainingAutoResultPresentation(
                modelName ?? string.Empty,
                statsText,
                attacks,
                saveResultMessage ?? string.Empty,
                continueButtonLabel ?? "保存先を選ぶ",
                thumbnailPng,
                titleText ?? LocalizedText.Get(GameTextKeys.TrainingComplete));
        }
    }
}
