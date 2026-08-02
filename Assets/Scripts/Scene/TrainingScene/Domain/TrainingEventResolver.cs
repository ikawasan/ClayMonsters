using ClayEditor.Rigging;
using Localization;
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
                LocalizedText.GetOrFallback(GameTextKeys.TrainingEventLearnTitle, "技習得イベント"),
                LocalizedText.GetOrFallback(
                    GameTextKeys.TrainingEventLearnMessage,
                    "新しい技「{name}」を覚えるチャンスです\n入れ替えるスロットを選んでください",
                    "name",
                    label),
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
                LocalizedText.GetOrFallback(GameTextKeys.TrainingEventStatTitle, "特訓イベント"),
                LocalizedText.GetOrFallback(
                    GameTextKeys.TrainingEventStatMessage,
                    "HP+{hp} 攻撃+{atk} 防御+{def} 速度+{spd} 命中+{hit}",
                    new Dictionary<string, object>
                    {
                        { "hp", gain.Hp },
                        { "atk", gain.Attack },
                        { "def", gain.Defense },
                        { "spd", gain.Speed },
                        { "hit", gain.Hit },
                    }),
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
