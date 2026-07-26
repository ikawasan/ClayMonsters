using ClayEditor.Rigging;
using SaveData;
using SaveData.Interface;
using System.Collections.Generic;
using UnityEngine;

namespace Scene.TrainingScene.Domain
{
    /// <summary>
    /// 育成開始時のステータスと技の継承を解決する
    /// </summary>
    public static class TrainingInheritanceResolver
    {
        /// <summary>
        /// 育成済みスロット数を数える
        /// </summary>
        /// <param name="saveService">セーブサービス</param>
        public static int CountUsedTrainedSlots(IClayModelSaveService saveService)
        {
            if (saveService == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < ModelSavePoolSettings.GetSlotCount(ModelSavePool.TrainedPlayer); i++)
            {
                ModelSaveSlot slot = saveService.GetSlot(ModelSavePool.TrainedPlayer, i);
                if (slot != null && slot.isUsed)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// 2体の親からステータス上昇と技継承を適用する
        /// </summary>
        /// <param name="baseStatus">育成開始前ステータス</param>
        /// <param name="baseAttacks">育成開始前攻撃</param>
        /// <param name="parentA">継承元1</param>
        /// <param name="parentB">継承元2</param>
        /// <param name="usableAttacks">骨格で使用可能な攻撃</param>
        /// <param name="random">乱数</param>
        public static TrainingInheritanceResult Resolve(
            ModelStatus baseStatus,
            IReadOnlyList<MotionType> baseAttacks,
            ModelSaveSlot parentA,
            ModelSaveSlot parentB,
            IReadOnlyList<MotionType> usableAttacks,
            System.Random random)
        {
            if (parentA == null || parentB == null || random == null)
            {
                return TrainingInheritanceResult.None(baseStatus, baseAttacks);
            }

            ModelStatus status = ModelStatus.CloneOrDefault(baseStatus);
            TrainingStatGain gain = BuildStatGain(parentA.status, parentB.status);
            status.hp += gain.Hp;
            status.attack += gain.Attack;
            status.defense += gain.Defense;
            status.speed += gain.Speed;
            status.hit += gain.Hit;
            TrainingActionResolver.ClampStatus(status);

            List<MotionType> attacks = ModelAttackMotionUtility.SanitizeForUsableAttacks(
                baseAttacks,
                usableAttacks,
                TrainingSettings.AttackSlotCount);

            MotionType? inheritedA = TryPickInheritedAttack(
                parentA.attackMotions,
                attacks,
                usableAttacks,
                random,
                excludeAttack: null);
            MotionType? inheritedB = TryPickInheritedAttack(
                parentB.attackMotions,
                attacks,
                usableAttacks,
                random,
                excludeAttack: inheritedA);

            // 技の入れ替え先はプレイヤー選択のためここでは候補のみ返す
            return new TrainingInheritanceResult(
                status,
                attacks,
                gain,
                inheritedA,
                inheritedB,
                true);
        }

        /// <summary>
        /// 継承結果の表示メッセージを返す
        /// </summary>
        /// <param name="result">継承結果</param>
        public static string FormatSummary(TrainingInheritanceResult result)
        {
            if (result == null || !result.Applied)
            {
                return "ボーナスなしで育成を開始します";
            }

            TrainingStatGain gain = result.StatGain;
            string message =
                $"ボーナス HP+{gain.Hp} 攻撃+{gain.Attack} 防御+{gain.Defense} 速度+{gain.Speed} 命中+{gain.Hit}";

            if (result.InheritedAttackFromParentA.HasValue)
            {
                message += "\n技候補1: "
                    + TrainingAttackTeacher.FormatAttackName(result.InheritedAttackFromParentA.Value);
            }

            if (result.InheritedAttackFromParentB.HasValue)
            {
                message += "\n技候補2: "
                    + TrainingAttackTeacher.FormatAttackName(result.InheritedAttackFromParentB.Value);
            }

            if (result.InheritedAttackFromParentA.HasValue
                || result.InheritedAttackFromParentB.HasValue)
            {
                message += "\n続けて技の入れ替えスロットを選んでください";
            }

            return message;
        }

        /// <summary>
        /// 1体の継承元から得られるステータス上昇を返す
        /// </summary>
        /// <param name="parentStatus">継承元ステータス</param>
        public static TrainingStatGain BuildStatGainFromParent(ModelStatus parentStatus)
        {
            ModelStatus parent = parentStatus ?? new ModelStatus();
            int percent = TrainingSettings.InheritanceStatPercentPerParent;
            return new TrainingStatGain(
                CalcInheritedStatFromParent(parent.hp, percent),
                CalcInheritedStatFromParent(parent.attack, percent),
                CalcInheritedStatFromParent(parent.defense, percent),
                CalcInheritedStatFromParent(parent.speed, percent),
                CalcInheritedStatFromParent(parent.hit, percent));
        }

        /// <summary>
        /// 継承元ホバー用の上昇値文言を返す
        /// </summary>
        /// <param name="parent">継承元スロット</param>
        public static string FormatParentStatGainPreview(ModelSaveSlot parent)
        {
            if (parent == null || !parent.isUsed)
            {
                return string.Empty;
            }

            TrainingStatGain gain = BuildStatGainFromParent(parent.status);
            string modelName = string.IsNullOrEmpty(parent.modelName)
                ? "継承元"
                : parent.modelName;
            return $"{modelName}\n継承上昇 HP+{gain.Hp} 攻撃+{gain.Attack} 防御+{gain.Defense} 速度+{gain.Speed} 命中+{gain.Hit}";
        }

        private static TrainingStatGain BuildStatGain(ModelStatus parentA, ModelStatus parentB)
        {
            return BuildStatGainFromParent(parentA).Add(BuildStatGainFromParent(parentB));
        }

        private static int CalcInheritedStatFromParent(int parentStat, int percent)
        {
            return Mathf.Max(0, parentStat) * percent / 100;
        }

        private static MotionType? TryPickInheritedAttack(
            IReadOnlyList<MotionType> parentAttacks,
            IReadOnlyList<MotionType> currentAttacks,
            IReadOnlyList<MotionType> usableAttacks,
            System.Random random,
            MotionType? excludeAttack)
        {
            if (parentAttacks == null || parentAttacks.Count == 0)
            {
                return null;
            }

            var candidates = new List<MotionType>();
            for (int i = 0; i < parentAttacks.Count; i++)
            {
                MotionType motion = parentAttacks[i];
                if (excludeAttack.HasValue && motion == excludeAttack.Value)
                {
                    continue;
                }

                if (ContainsAttack(currentAttacks, motion))
                {
                    continue;
                }

                if (ContainsAttack(candidates, motion))
                {
                    continue;
                }

                if (usableAttacks != null
                    && usableAttacks.Count > 0
                    && !ContainsAttack(usableAttacks, motion))
                {
                    continue;
                }

                candidates.Add(motion);
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            return candidates[random.Next(candidates.Count)];
        }

        private static bool ContainsAttack(IReadOnlyList<MotionType> attacks, MotionType motion)
        {
            if (attacks == null)
            {
                return false;
            }

            for (int i = 0; i < attacks.Count; i++)
            {
                if (attacks[i] == motion)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
