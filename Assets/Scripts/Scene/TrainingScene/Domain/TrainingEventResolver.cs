using ClayEditor.Rigging;
using System.Collections.Generic;

namespace Scene.TrainingScene.Domain
{
    /// <summary>
    /// 育成ターン後のランダムイベントを抽選する
    /// </summary>
    public static class TrainingEventResolver
    {
        /// <summary>
        /// 技習得イベントの発生を抽選する
        /// </summary>
        /// <param name="random">乱数</param>
        /// <param name="currentAttacks">現在の攻撃</param>
        /// <param name="usableAttacks">骨格で使用可能な攻撃</param>
        /// <param name="outcome">イベント内容</param>
        /// <returns>発生したらtrue</returns>
        public static bool TryRollLearnAttackEvent(
            System.Random random,
            IReadOnlyList<MotionType> currentAttacks,
            IReadOnlyList<MotionType> usableAttacks,
            out TrainingEventOutcome outcome)
        {
            outcome = default;
            if (random == null)
            {
                return false;
            }

            if (random.NextDouble() * 100d >= TrainingSettings.LearnAttackEventTriggerPercent)
            {
                return false;
            }

            if (!TrainingAttackTeacher.TryPickLearnableAttack(
                currentAttacks,
                usableAttacks,
                random,
                out MotionType learned))
            {
                return false;
            }

            string label = TrainingAttackTeacher.FormatAttackName(learned);
            outcome = new TrainingEventOutcome(
                TrainingEventType.LearnAttack,
                "技習得イベント",
                $"新しい技「{label}」を覚えるチャンスです\n入れ替えるスロットを選んでください",
                default,
                learned);
            return true;
        }

        /// <summary>
        /// 特訓イベントの発生を抽選する
        /// </summary>
        /// <param name="random">乱数</param>
        /// <param name="outcome">イベント内容</param>
        /// <returns>発生したらtrue</returns>
        public static bool TryRollStatBoostEvent(System.Random random, out TrainingEventOutcome outcome)
        {
            outcome = default;
            if (random == null)
            {
                return false;
            }

            if (random.NextDouble() * 100d >= TrainingSettings.StatBoostEventTriggerPercent)
            {
                return false;
            }

            TrainingStatGain gain = RollStatBoost(random);
            outcome = new TrainingEventOutcome(
                TrainingEventType.StatBoost,
                "特訓イベント",
                $"HP+{gain.Hp} 攻撃+{gain.Attack} 防御+{gain.Defense} 速度+{gain.Speed} 命中+{gain.Hit}",
                gain,
                default);
            return true;
        }

        /// <summary>
        /// 強敵急襲イベントの発生を抽選する
        /// </summary>
        /// <param name="random">乱数</param>
        /// <returns>発生したらtrue</returns>
        public static bool TryRollAmbushEvent(System.Random random)
        {
            if (random == null)
            {
                return false;
            }

            return random.NextDouble() * 100d < TrainingSettings.AmbushEventTriggerPercent;
        }

        /// <summary>
        /// 強敵急襲勝利時のステータス上昇を返す
        /// </summary>
        public static TrainingStatGain CreateAmbushVictoryGain()
        {
            return new TrainingStatGain(
                TrainingSettings.AmbushVictoryHpGain,
                TrainingSettings.AmbushVictoryAttackGain,
                TrainingSettings.AmbushVictoryDefenseGain,
                TrainingSettings.AmbushVictorySpeedGain,
                TrainingSettings.AmbushVictoryHitGain);
        }

        private static TrainingStatGain RollStatBoost(System.Random random)
        {
            int hp = random.Next(TrainingSettings.EventStatHpMin, TrainingSettings.EventStatHpMax + 1);
            int attack = random.Next(TrainingSettings.EventStatAttackMin, TrainingSettings.EventStatAttackMax + 1);
            int defense = random.Next(TrainingSettings.EventStatDefenseMin, TrainingSettings.EventStatDefenseMax + 1);
            int speed = random.Next(TrainingSettings.EventStatSpeedMin, TrainingSettings.EventStatSpeedMax + 1);
            int hit = random.Next(TrainingSettings.EventStatHitMin, TrainingSettings.EventStatHitMax + 1);
            return new TrainingStatGain(hp, attack, defense, speed, hit);
        }
    }
}
