using ClayEditor.Rigging;
using SaveData;
using SaveData.Interface;
using System.Collections.Generic;

namespace Scene.TrainingScene.Domain
{
    /// <summary>
    /// 自動育成の5日間進行をシミュレートする
    /// </summary>
    public static class TrainingAutoSimulator
    {
        /// <summary>
        /// セッションを完了まで自動進行する
        /// </summary>
        /// <param name="session">育成セッション</param>
        /// <param name="saveService">セーブサービス</param>
        /// <param name="usableAttacks">骨格で使用可能な攻撃</param>
        /// <param name="random">乱数</param>
        public static void Run(
            TrainingSession session,
            IClayModelSaveService saveService,
            IReadOnlyList<MotionType> usableAttacks,
            System.Random random)
        {
            if (session == null || random == null)
            {
                return;
            }

            while (!session.IsCompleted)
            {
                RunSingleDay(session, saveService, usableAttacks, random);
                if (session.IsCompleted)
                {
                    break;
                }

                session.AdvanceDay();
            }
        }

        private static void RunSingleDay(
            TrainingSession session,
            IClayModelSaveService saveService,
            IReadOnlyList<MotionType> usableAttacks,
            System.Random random)
        {
            TrainingPeriod[] periods = TrainingDailySchedule.AllPeriods;
            for (int i = 0; i < periods.Length; i++)
            {
                TrainingPeriod period = periods[i];
                if (TrainingPeriodCatalog.IsBattlePeriod(period))
                {
                    SimulateAfterSchoolBattle(session, saveService, random);
                    continue;
                }

                TrainingLocation[] choices = TrainingActionResolver.PickLocationChoices(
                    TrainingSettings.LocationChoiceCount,
                    random);
                TrainingTurnChoice turnChoice = TrainingAutoPolicy.PickTurnChoice(session, choices);
                TrainingActionResult result = turnChoice.IsRest
                    ? TrainingActionResolver.ExecuteRest(session.Stamina, random)
                    : TrainingActionResolver.ExecuteAction(turnChoice.Location, session.Stamina, random);
                session.ApplyAction(result);

                if (!result.Succeeded)
                {
                    continue;
                }

                TryApplyRandomEvent(session, usableAttacks, random);
            }
        }

        private static void SimulateAfterSchoolBattle(
            TrainingSession session,
            IClayModelSaveService saveService,
            System.Random random)
        {
            if (!TrainingEnemyResolver.TryPickEnemySlotIndex(saveService, session.CurrentDay, out int enemySlotIndex))
            {
                session.CompletePeriod();
                return;
            }

            ModelStatus enemyStatus = saveService.GetSlot(ModelSavePool.Enemy, enemySlotIndex)?.status;
            bool playerWon = TrainingAutoBattleResolver.TrySimulateVictory(
                session.CurrentStatus,
                enemyStatus,
                random);
            if (playerWon)
            {
                session.ApplyAfterSchoolVictoryRecovery();
            }

            session.CompletePeriod();
        }

        private static void TryApplyRandomEvent(
            TrainingSession session,
            IReadOnlyList<MotionType> usableAttacks,
            System.Random random)
        {
            if (TrainingEventResolver.TryRollLearnAttackEvent(
                random,
                session.AttackMotions,
                usableAttacks,
                out TrainingEventOutcome learnOutcome))
            {
                int replaceIndex = TrainingAutoPolicy.PickAttackSwapSlot(session, learnOutcome.LearnedAttack);
                if (replaceIndex >= 0)
                {
                    session.TryReplaceAttack(replaceIndex, learnOutcome.LearnedAttack);
                }

                return;
            }

            if (TrainingEventResolver.TryRollStatBoostEvent(random, out TrainingEventOutcome statOutcome))
            {
                session.ApplyEventStatGain(statOutcome.StatGain);
            }
        }
    }
}
