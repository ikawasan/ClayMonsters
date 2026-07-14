using Battle;
using ClayEditor.Rigging;
using System.Collections.Generic;

namespace Scene.TrainingScene.Domain
{
    /// <summary>
    /// 自動育成の行動方針
    /// 体力管理と育成効率を優先して選択する
    /// </summary>
    public static class TrainingAutoPolicy
    {
        /// <summary>
        /// 行き先または休憩を選ぶ
        /// </summary>
        /// <param name="session">育成セッション</param>
        /// <param name="choices">提示された行き先</param>
        public static TrainingTurnChoice PickTurnChoice(
            TrainingSession session,
            IReadOnlyList<TrainingLocation> choices)
        {
            if (session == null)
            {
                return TrainingTurnChoice.Rest();
            }

            if (ShouldRest(session))
            {
                return TrainingTurnChoice.Rest();
            }

            if (choices == null || choices.Count == 0)
            {
                return TrainingTurnChoice.Rest();
            }

            TrainingLocation bestLocation = choices[0];
            int bestScore = ScoreLocation(choices[0]);
            for (int i = 1; i < choices.Count; i++)
            {
                int score = ScoreLocation(choices[i]);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestLocation = choices[i];
                }
            }

            return TrainingTurnChoice.FromLocation(bestLocation);
        }

        /// <summary>
        /// 習得技の入れ替え先スロットを選ぶ
        /// 威力が上がる場合のみ入れ替える
        /// </summary>
        /// <param name="session">育成セッション</param>
        /// <param name="learnedAttack">習得候補</param>
        public static int PickAttackSwapSlot(TrainingSession session, MotionType learnedAttack)
        {
            if (session == null || session.AttackMotions == null || session.AttackMotions.Count == 0)
            {
                return -1;
            }

            int learnedPower = MotionPartRequirement.GetPowerDisplayValue(learnedAttack);
            int weakestSlot = -1;
            int weakestPower = int.MaxValue;
            for (int i = 0; i < session.AttackMotions.Count; i++)
            {
                int power = MotionPartRequirement.GetPowerDisplayValue(session.AttackMotions[i]);
                if (power >= weakestPower)
                {
                    continue;
                }

                weakestPower = power;
                weakestSlot = i;
            }

            if (weakestSlot < 0 || learnedPower <= weakestPower)
            {
                return -1;
            }

            return weakestSlot;
        }

        private static bool ShouldRest(TrainingSession session)
        {
            if (session.Stamina <= TrainingSettings.LowStaminaThreshold)
            {
                return true;
            }

            return session.Stamina < TrainingSettings.StaminaCostPerAction + 8;
        }

        private static int ScoreLocation(TrainingLocation location)
        {
            TrainingStatGain gain = TrainingLocationCatalog.GetBaseGain(location);
            return gain.Hp + gain.Attack * 2 + gain.Defense + gain.Speed * 2;
        }
    }
}
