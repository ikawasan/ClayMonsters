using ClayEditor.Rigging;
using SaveData;
using SaveData.Interface;
using System.Collections.Generic;

namespace Scene.TrainingScene.Domain
{
    /// <summary>
    /// 自動育成の日次進行をシミュレートする
    /// </summary>
    public static class TrainingAutoSimulator
    {
        /// <summary>
        /// セッションを完了まで自動進行する
        /// </summary>
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

            session.SetStatusGainMultiplier(TrainingSettings.AutoTrainingStatGainMultiplier);
            try
            {
                while (!session.IsCompleted)
                {
                    session.EnsureShopOffer(random);
                    RunSingleDay(session, saveService, usableAttacks, random);
                    if (session.IsCompleted)
                    {
                        break;
                    }

                    session.AdvanceDay();
                }
            }
            finally
            {
                session.SetStatusGainMultiplier(1f);
            }
        }

        private static void RunSingleDay(
            TrainingSession session,
            IClayModelSaveService saveService,
            IReadOnlyList<MotionType> usableAttacks,
            System.Random random)
        {
            TrainingPeriod[] periods = TrainingDailySchedule.AllPeriods;
            int startPeriodIndex = UnityEngine.Mathf.Clamp(
                session.TurnIndexInDay,
                0,
                periods.Length);
            for (int i = startPeriodIndex; i < periods.Length; i++)
            {
                TrainingPeriod period = periods[i];
                int turnNumber = i + 1;
                if (TrainingPeriodCatalog.IsBattlePeriod(period))
                {
                    SimulateAfterSchoolBattle(session, saveService, random);
                    continue;
                }

                if (TrainingPeriodCatalog.IsShopPeriod(period))
                {
                    RunLunchBreak(session);
                    continue;
                }

                RunCommandPeriod(session, usableAttacks, random, turnNumber);
            }
        }

        private static void RunLunchBreak(TrainingSession session)
        {
            if (TrainingAutoPolicy.TryPickShopItem(session, out TrainingShopItem shopItem))
            {
                TrainingShopResolver.TryPurchase(session, shopItem);
                TrainingShopResolver.TryUseItem(session, shopItem.Id);
            }

            session.CompletePeriod();
        }

        private static void RunCommandPeriod(
            TrainingSession session,
            IReadOnlyList<MotionType> usableAttacks,
            System.Random random,
            int turnNumber)
        {
            while (session.TurnIndexInDay < turnNumber)
            {
                TrainingWeekChoice choice = TrainingAutoPolicy.PickWeekChoice(session, random);
                switch (choice.Command)
                {
                    case TrainingCommandType.Rest:
                        session.ApplyAction(TrainingActionResolver.ExecuteRest(session.Stamina, random));
                        return;
                    case TrainingCommandType.SpecialTrain:
                    {
                        TrainingActionResult special = TrainingActionResolver.ExecuteSpecialTrain(
                            choice.Focus,
                            session,
                            random);
                        session.ApplyAction(special);
                        if (special.Succeeded)
                        {
                            TryApplyRandomEvent(session, usableAttacks, random);
                        }

                        return;
                    }
                    default:
                    {
                        TrainingActionResult train = TrainingActionResolver.ExecuteTrain(
                            choice.Focus,
                            session,
                            random);
                        session.ApplyAction(train);
                        if (train.Succeeded)
                        {
                            TryApplyRandomEvent(session, usableAttacks, random);
                        }

                        return;
                    }
                }
            }
        }

        private static void SimulateAfterSchoolBattle(
            TrainingSession session,
            IClayModelSaveService saveService,
            System.Random random)
        {
            if (!TrainingEnemyResolver.TryPickEnemySlotIndex(
                    saveService,
                    session.CurrentDay,
                    out int enemySlotIndex))
            {
                session.CompletePeriod();
                return;
            }

            ModelSaveSlot enemySlot = saveService.GetSlot(ModelSavePool.Enemy, enemySlotIndex);
            EnemyStrengthTier tier = TrainingEnemyResolver.ResolveAfterSchoolTier(session.CurrentDay);
            ModelStatus enemyStatus = enemySlot != null
                ? EnemyStrengthStatusCatalog.Resolve(enemySlot, tier)
                : null;
            bool playerWon = TrainingAutoBattleResolver.TrySimulateVictory(
                session.CurrentStatus,
                enemyStatus,
                random);
            if (playerWon)
            {
                session.ApplyAfterSchoolVictoryRecovery();
                int reward = TrainingShopResolver.ResolveTournamentReward((int)session.CurrentDay);
                if (reward > 0)
                {
                    session.AddMoney(reward);
                }
            }
            else
            {
                session.LowerMotivation(1);
                int defeatReward = TrainingSettings.AfterSchoolDefeatReward;
                if (defeatReward > 0)
                {
                    session.AddMoney(defeatReward);
                }
            }

            session.RefreshShopOffer(random);
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
                // 空きスロットがあれば入れ替えずにそのまま覚える
                if (session.TryAddAttack(learnOutcome.LearnedAttack))
                {
                    return;
                }

                int replaceIndex = TrainingAutoPolicy.PickAttackSwapSlot(
                    session,
                    learnOutcome.LearnedAttack);
                if (replaceIndex >= 0)
                {
                    session.TryReplaceAttack(replaceIndex, learnOutcome.LearnedAttack);
                }

                return;
            }

            if (!TrainingEventResolver.TryRollStatBoostEvent(
                    random,
                    out TrainingEventOutcome statOutcome))
            {
                return;
            }

            session.ApplyEventStatGain(statOutcome.StatGain);
        }
    }
}
