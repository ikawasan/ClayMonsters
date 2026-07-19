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
            if (progress == null || !progress.inProgress || progress.status == null)
            {
                return false;
            }

            if (progress.day < (int)TrainingDayOfWeek.Monday
                || progress.day > (int)TrainingDayOfWeek.Friday)
            {
                return false;
            }

            if (progress.turnIndexInDay < 0
                || progress.turnIndexInDay > TrainingDailySchedule.TurnsPerDay)
            {
                return false;
            }

            return progress.attackMotions != null && progress.attackMotions.Count > 0;
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
                status = new ModelStatus
                {
                    hp = session.CurrentStatus.hp,
                    attack = session.CurrentStatus.attack,
                    defense = session.CurrentStatus.defense,
                    speed = session.CurrentStatus.speed
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
        /// <param name="saveService">セーブサービス</param>
        /// <param name="slotIndex">見つかったスロット番号</param>
        /// <param name="slot">見つかったスロット</param>
        /// <param name="progress">育成途中データ</param>
        /// <returns>見つかったらtrue</returns>
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

            for (int i = 0; i < ModelSavePoolSettings.SlotCount; i++)
            {
                TrainingSlotProgress candidate = saveService.GetTrainingProgress(ModelSavePool.Player, i);
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
        /// <param name="slot">対象スロット</param>
        /// <param name="progress">育成途中データ</param>
        /// <returns>表示データ</returns>
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
                    System.Array.Empty<MotionType>(),
                    null);
            }

            var day = (TrainingDayOfWeek)Mathf.Clamp(progress.day, 1, (int)TrainingDayOfWeek.Friday);
            int turnNumber = Mathf.Clamp(progress.turnIndexInDay + 1, 1, TrainingDailySchedule.TurnsPerDay);
            TrainingPeriod period = TrainingDailySchedule.AllPeriods[
                Mathf.Clamp(progress.turnIndexInDay, 0, TrainingDailySchedule.TurnsPerDay - 1)];

            string dayPeriodTurnLabel =
                $"{TrainingDayCatalog.GetDisplayName(day)}"
                + $"  {TrainingPeriodCatalog.GetDisplayName(period)}"
                + $"\nターン {turnNumber}/{TrainingDailySchedule.TurnsPerDay}";

            ModelStatus status = progress.status ?? new ModelStatus();
            string statsText =
                $"HP {status.hp}\n"
                + $"攻 {status.attack}\n"
                + $"防 {status.defense}\n"
                + $"速 {status.speed}";

            return new TrainingResumeProgressPresentation(
                slot != null ? slot.modelName : string.Empty,
                dayPeriodTurnLabel,
                $"体力 {progress.stamina} / {TrainingSettings.MaxStamina}",
                statsText,
                progress.attackMotions,
                ModelSaveStorage.ReadThumbnailPng(slot));
        }

        /// <summary>
        /// 再開確認用の概要文を返す
        /// </summary>
        /// <param name="progress">育成途中データ</param>
        /// <returns>概要文</returns>
        public static string FormatResumeSummary(TrainingSlotProgress progress)
        {
            if (progress == null)
            {
                return string.Empty;
            }

            var day = (TrainingDayOfWeek)Mathf.Clamp(progress.day, 1, (int)TrainingDayOfWeek.Friday);
            int turnNumber = Mathf.Clamp(progress.turnIndexInDay + 1, 1, TrainingDailySchedule.TurnsPerDay);
            TrainingPeriod period = TrainingDailySchedule.AllPeriods[
                Mathf.Clamp(progress.turnIndexInDay, 0, TrainingDailySchedule.TurnsPerDay - 1)];
            return $"{TrainingDayCatalog.GetDisplayName(day)}"
                + $" {TrainingPeriodCatalog.GetDisplayName(period)}"
                + $"({turnNumber}/{TrainingDailySchedule.TurnsPerDay}ターン目)"
                + $"\n体力 {progress.stamina}/{TrainingSettings.MaxStamina}"
                + $"\nHP {progress.status.hp} 攻 {progress.status.attack}"
                + $" 防 {progress.status.defense} 速 {progress.status.speed}";
        }

        /// <summary>
        /// 再開確認用の概要文を返す
        /// </summary>
        /// <param name="slot">対象スロット</param>
        /// <param name="progress">育成途中データ</param>
        /// <returns>概要文</returns>
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
        /// <param name="modelName">モデル名</param>
        /// <param name="status">最終ステータス</param>
        /// <param name="attacks">技構成</param>
        /// <param name="saveResultMessage">保存結果</param>
        /// <param name="continueButtonLabel">続行ボタンラベル</param>
        /// <param name="thumbnailPng">モデルサムネイルPNG</param>
        /// <param name="titleText">ウィンドウタイトル</param>
        /// <returns>表示データ</returns>
        public static TrainingAutoResultPresentation BuildAutoResultPresentation(
            string modelName,
            ModelStatus status,
            IReadOnlyList<MotionType> attacks,
            string saveResultMessage = "",
            string continueButtonLabel = "保存先を選ぶ",
            byte[] thumbnailPng = null,
            string titleText = "育成完了")
        {
            ModelStatus resolvedStatus = status ?? new ModelStatus();
            string statsText = ModelSaveSummaryFormatter.FormatTrainingFinalStatusParameters(resolvedStatus);

            return new TrainingAutoResultPresentation(
                modelName ?? string.Empty,
                statsText,
                attacks,
                saveResultMessage ?? string.Empty,
                continueButtonLabel ?? "保存先を選ぶ",
                thumbnailPng,
                titleText ?? "育成完了");
        }
    }
}
