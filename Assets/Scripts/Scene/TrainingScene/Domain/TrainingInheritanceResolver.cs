using ClayEditor.Rigging;
using Localization;
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
            System.Random random,
            int inheritancePercentBonus = 0)
        {
            if (parentA == null || parentB == null || random == null)
            {
                return TrainingInheritanceResult.None(baseStatus, baseAttacks);
            }

            ModelStatus status = ModelStatus.CloneOrDefault(baseStatus);
            TrainingStatGain gain = BuildStatGain(
                parentA.status,
                parentB.status,
                inheritancePercentBonus);
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
                return LocalizedText.GetOrFallback(
                    GameTextKeys.TrainingInheritanceNoBonus,
                    "ボーナスなしで育成を開始します");
            }

            TrainingStatGain gain = result.StatGain;
            string message = LocalizedText.GetOrFallback(
                GameTextKeys.TrainingInheritanceBonusStats,
                "ボーナス HP+{hp} 攻撃+{atk} 防御+{def} 速度+{spd} 命中+{hit}",
                new Dictionary<string, object>
                {
                    { "hp", gain.Hp },
                    { "atk", gain.Attack },
                    { "def", gain.Defense },
                    { "spd", gain.Speed },
                    { "hit", gain.Hit },
                });

            if (result.InheritedAttackFromParentA.HasValue)
            {
                message += "\n" + LocalizedText.GetOrFallback(
                    GameTextKeys.TrainingInheritanceAttackCandidate,
                    "継承技{n}: {name}",
                    new Dictionary<string, object>
                    {
                        { "n", 1 },
                        {
                            "name",
                            TrainingAttackTeacher.FormatAttackName(
                                result.InheritedAttackFromParentA.Value)
                        },
                    });
            }

            if (result.InheritedAttackFromParentB.HasValue)
            {
                message += "\n" + LocalizedText.GetOrFallback(
                    GameTextKeys.TrainingInheritanceAttackCandidate,
                    "継承技{n}: {name}",
                    new Dictionary<string, object>
                    {
                        { "n", 2 },
                        {
                            "name",
                            TrainingAttackTeacher.FormatAttackName(
                                result.InheritedAttackFromParentB.Value)
                        },
                    });
            }

            return message;
        }

        /// <summary>
        /// 継承技の入れ替え案内文言を返す
        /// 継承技があるときのみ非空
        /// </summary>
        /// <param name="result">継承結果</param>
        public static string FormatContinueSwapHint(TrainingInheritanceResult result)
        {
            if (result == null
                || !result.Applied
                || (!result.InheritedAttackFromParentA.HasValue
                    && !result.InheritedAttackFromParentB.HasValue))
            {
                return string.Empty;
            }

            return LocalizedText.GetOrFallback(
                GameTextKeys.TrainingInheritanceContinueSwap,
                "続けて技の入れ替えスロットを選んでください");
        }

        /// <summary>
        /// 1体の継承元から得られるステータス上昇を返す
        /// 基本割合で計算したあとスキルツリー乗算を掛ける
        /// </summary>
        /// <param name="parentStatus">継承元ステータス</param>
        /// <param name="inheritancePercentBonus">スキルツリー等の乗算用百分率</param>
        public static TrainingStatGain BuildStatGainFromParent(
            ModelStatus parentStatus,
            int inheritancePercentBonus = 0)
        {
            TrainingStatGain baseGain = BuildBaseStatGainFromParent(parentStatus);
            return ApplySkillInheritanceBonus(baseGain, inheritancePercentBonus);
        }

        /// <summary>
        /// 継承元ホバー用の上昇値文言を返す
        /// </summary>
        /// <param name="parent">継承元スロット</param>
        public static string FormatParentStatGainPreview(ModelSaveSlot parent)
        {
            return FormatParentStatGainPreview(parent, inheritancePercentBonus: 0);
        }

        /// <summary>
        /// 継承元ホバー用の上昇値文言を返す
        /// </summary>
        /// <param name="parent">継承元スロット</param>
        /// <param name="inheritancePercentBonus">スキルツリー等の乗算用百分率</param>
        public static string FormatParentStatGainPreview(
            ModelSaveSlot parent,
            int inheritancePercentBonus)
        {
            if (parent == null || !parent.isUsed)
            {
                return string.Empty;
            }

            TrainingStatGain gain = BuildStatGainFromParent(parent.status, inheritancePercentBonus);
            string modelName = string.IsNullOrEmpty(parent.modelName)
                ? LocalizedText.GetOrFallback(
                    GameTextKeys.TrainingInheritanceParentDefault,
                    "継承元")
                : parent.modelName;
            return LocalizedText.GetOrFallback(
                GameTextKeys.TrainingInheritanceParentPreview,
                "{name}\n継承上昇 HP+{hp} 攻撃+{atk} 防御+{def} 速度+{spd} 命中+{hit}",
                new Dictionary<string, object>
                {
                    { "name", modelName },
                    { "hp", gain.Hp },
                    { "atk", gain.Attack },
                    { "def", gain.Defense },
                    { "spd", gain.Speed },
                    { "hit", gain.Hit },
                });
        }

        private static TrainingStatGain BuildStatGain(
            ModelStatus parentA,
            ModelStatus parentB,
            int inheritancePercentBonus = 0)
        {
            // 親ごとの基本10%を合算してからスキル乗算する
            TrainingStatGain baseGain = BuildBaseStatGainFromParent(parentA)
                .Add(BuildBaseStatGainFromParent(parentB));
            return ApplySkillInheritanceBonus(baseGain, inheritancePercentBonus);
        }

        private static TrainingStatGain BuildBaseStatGainFromParent(ModelStatus parentStatus)
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

        private static TrainingStatGain ApplySkillInheritanceBonus(
            TrainingStatGain baseGain,
            int inheritancePercentBonus)
        {
            int bonus = Mathf.Clamp(
                Mathf.Max(0, inheritancePercentBonus),
                0,
                TrainingSettings.InheritanceSkillBonusMaxPercent);
            if (bonus <= 0)
            {
                return baseGain;
            }

            // 基本上昇量の合計に対して(100+ボーナス)/100を乗算する
            int multiplier = 100 + bonus;
            return new TrainingStatGain(
                baseGain.Hp * multiplier / 100,
                baseGain.Attack * multiplier / 100,
                baseGain.Defense * multiplier / 100,
                baseGain.Speed * multiplier / 100,
                baseGain.Hit * multiplier / 100);
        }

        private static int CalcInheritedStatFromParent(int parentStat, int percent)
        {
            // 親ステ×基本割合の切り捨て
            int safeStat = Mathf.Max(0, parentStat);
            return safeStat * Mathf.Max(0, percent) / 100;
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
