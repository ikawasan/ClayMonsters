using System;
using System.Collections.Generic;
using UnityEngine;

namespace ClayEditor.Rigging
{
    /// <summary>
    /// 骨格解析で使用可能と判定した攻撃モーションから
    /// 保存登録用の攻撃を選ぶ
    /// </summary>
    public static class AttackMotionSelector
    {
        /// <summary>
        /// 骨格解析で使用可能と判定した攻撃モーションの一覧を返す
        /// MotionRequirementの条件とリム部位の両方を満たすものだけを含める
        /// </summary>
        /// <param name="analyzer">部位分類器</param>
        /// <param name="bones">ボーン配列</param>
        /// <returns>使用可能な攻撃モーション</returns>
        public static List<MotionType> CollectUsableAttacks(SkeletonPartAnalyzer analyzer, Transform[] bones)
        {
            var attacks = new List<MotionType>();
            if (analyzer == null || bones == null || bones.Length == 0)
            {
                return attacks;
            }

            HashSet<BonePart> availableParts = SkeletonLimbPartCollector.CollectAvailableParts(analyzer, bones);
            List<MotionType> availableMotions = analyzer.GetAvailableMotions(bones);
            for (int i = 0; i < availableMotions.Count; i++)
            {
                MotionType motion = availableMotions[i];
                if (!ProceduralMotionCharacter.IsAttackMotion(motion) || attacks.Contains(motion))
                {
                    continue;
                }

                if (availableParts.Contains(GetRequiredPart(motion)))
                {
                    attacks.Add(motion);
                }
            }

            return attacks;
        }

        /// <summary>
        /// 使用可能な部位だけで使える攻撃モーションの一覧を返す
        /// </summary>
        /// <param name="availableParts">使用可能な部位</param>
        /// <returns>使用可能な攻撃モーション</returns>
        public static List<MotionType> CollectAttacksForAvailableParts(ICollection<BonePart> availableParts)
        {
            HashSet<BonePart> parts = availableParts != null
                ? new HashSet<BonePart>(availableParts)
                : new HashSet<BonePart>();
            parts.Add(BonePart.Body);

            var attacks = new List<MotionType>();
            Array values = Enum.GetValues(typeof(MotionType));
            for (int i = 0; i < values.Length; i++)
            {
                var motion = (MotionType)values.GetValue(i);
                if (!ProceduralMotionCharacter.IsAttackMotion(motion))
                {
                    continue;
                }

                if (parts.Contains(GetRequiredPart(motion)))
                {
                    attacks.Add(motion);
                }
            }

            return attacks;
        }

        /// <summary>
        /// 使用可能な攻撃から保存登録用の攻撃を最大count件ランダムで選ぶ
        /// 使用可能数が不足する場合はエラーを出す
        /// </summary>
        /// <param name="analyzer">部位分類器</param>
        /// <param name="bones">ボーン配列</param>
        /// <param name="count">選ぶ件数</param>
        /// <returns>保存登録用の攻撃</returns>
        public static List<MotionType> PickRandomAttacks(
            SkeletonPartAnalyzer analyzer,
            Transform[] bones,
            int count)
        {
            List<MotionType> usable = CollectUsableAttacks(analyzer, bones);
            if (usable.Count < count)
            {
                Debug.LogError(
                    $"[AttackMotionSelector] 使用可能攻撃が不足しています({usable.Count}/{count})");
            }

            return ShuffleAndTake(usable, count);
        }

        /// <summary>
        /// 候補攻撃から骨格で使用可能なものだけを残す
        /// </summary>
        /// <param name="candidates">候補攻撃</param>
        /// <param name="analyzer">部位分類器</param>
        /// <param name="bones">ボーン配列</param>
        /// <returns>使用可能な攻撃</returns>
        public static List<MotionType> FilterUsableAttacks(
            IReadOnlyList<MotionType> candidates,
            SkeletonPartAnalyzer analyzer,
            Transform[] bones)
        {
            var attacks = new List<MotionType>();
            if (candidates == null || candidates.Count == 0)
            {
                return attacks;
            }

            HashSet<MotionType> usable = new HashSet<MotionType>(CollectUsableAttacks(analyzer, bones));
            for (int i = 0; i < candidates.Count; i++)
            {
                MotionType motion = candidates[i];
                if (usable.Contains(motion) && !attacks.Contains(motion))
                {
                    attacks.Add(motion);
                }
            }

            return attacks;
        }

        /// <summary>
        /// 現在の攻撃を残しつつ使用可能攻撃でスロット数まで埋める
        /// 埋められない場合はエラーを出す
        /// </summary>
        /// <param name="currentAttacks">現在の攻撃</param>
        /// <param name="usableAttacks">使用可能な攻撃</param>
        /// <param name="slotCount">スロット数</param>
        /// <returns>スロット数へ整えた攻撃</returns>
        public static List<MotionType> EnsureAttackSlots(
            IReadOnlyList<MotionType> currentAttacks,
            IReadOnlyList<MotionType> usableAttacks,
            int slotCount)
        {
            int count = Mathf.Max(0, slotCount);
            var usableSet = new HashSet<MotionType>();
            if (usableAttacks != null)
            {
                for (int i = 0; i < usableAttacks.Count; i++)
                {
                    MotionType motion = usableAttacks[i];
                    if (ProceduralMotionCharacter.IsAttackMotion(motion))
                    {
                        usableSet.Add(motion);
                    }
                }
            }

            var attacks = new List<MotionType>();
            if (currentAttacks != null)
            {
                for (int i = 0; i < currentAttacks.Count; i++)
                {
                    MotionType motion = currentAttacks[i];
                    if (!usableSet.Contains(motion) || attacks.Contains(motion))
                    {
                        continue;
                    }

                    attacks.Add(motion);
                    if (attacks.Count >= count)
                    {
                        return attacks;
                    }
                }
            }

            int beforeFill = attacks.Count;
            if (usableAttacks != null && attacks.Count < count)
            {
                List<MotionType> fillers = ShuffleAndTake(usableAttacks, usableAttacks.Count);
                for (int i = 0; i < fillers.Count && attacks.Count < count; i++)
                {
                    MotionType candidate = fillers[i];
                    if (!usableSet.Contains(candidate) || attacks.Contains(candidate))
                    {
                        continue;
                    }

                    attacks.Add(candidate);
                }
            }

            if (beforeFill < count && attacks.Count > beforeFill)
            {
                Debug.LogError(
                    $"[AttackMotionSelector] 攻撃スロット不足({beforeFill}/{count})のため使用可能攻撃で補充しました");
            }

            if (attacks.Count < count)
            {
                Debug.LogError(
                    $"[AttackMotionSelector] 使用可能攻撃でスロットを埋められません({attacks.Count}/{count})");
            }

            return attacks;
        }

        /// <summary>
        /// 攻撃リストをシャッフルして先頭count件を返す
        /// </summary>
        /// <param name="attacks">攻撃リスト</param>
        /// <param name="count">選ぶ件数</param>
        /// <returns>選んだ攻撃</returns>
        public static List<MotionType> ShuffleAndTake(IReadOnlyList<MotionType> attacks, int count)
        {
            var result = new List<MotionType>();
            if (attacks == null || attacks.Count == 0 || count <= 0)
            {
                return result;
            }

            for (int i = 0; i < attacks.Count; i++)
            {
                if (!result.Contains(attacks[i]))
                {
                    result.Add(attacks[i]);
                }
            }

            for (int i = result.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (result[i], result[j]) = (result[j], result[i]);
            }

            if (result.Count > count)
            {
                result.RemoveRange(count, result.Count - count);
            }

            return result;
        }

        /// <summary>
        /// 使用可能な部位だけで構成される攻撃リストへ整える
        /// 必要部位が無い技はエラーを出して除外し使用可能攻撃で不足を埋める
        /// </summary>
        /// <param name="attackMotions">元の攻撃</param>
        /// <param name="availableParts">使用可能な部位</param>
        /// <param name="slotCount">スロット数</param>
        /// <returns>正規化済みリスト</returns>
        public static List<MotionType> SanitizeForAvailableParts(
            IReadOnlyList<MotionType> attackMotions,
            ICollection<BonePart> availableParts,
            int slotCount)
        {
            HashSet<BonePart> parts = availableParts != null
                ? new HashSet<BonePart>(availableParts)
                : new HashSet<BonePart>();
            parts.Add(BonePart.Body);

            var attacks = new List<MotionType>();
            if (attackMotions != null)
            {
                for (int i = 0; i < attackMotions.Count; i++)
                {
                    MotionType motion = attackMotions[i];
                    if (!ProceduralMotionCharacter.IsAttackMotion(motion))
                    {
                        Debug.LogError($"[AttackMotionSelector] 攻撃でないモーション{motion}を除外しました");
                        continue;
                    }

                    if (!parts.Contains(GetRequiredPart(motion)))
                    {
                        Debug.LogError(
                            $"[AttackMotionSelector] 必要部位{GetRequiredPart(motion)}が無い攻撃{motion}を除外しました");
                        continue;
                    }

                    if (attacks.Contains(motion))
                    {
                        continue;
                    }

                    attacks.Add(motion);
                    if (attacks.Count >= slotCount)
                    {
                        return attacks;
                    }
                }
            }

            List<MotionType> usable = CollectAttacksForAvailableParts(parts);
            return EnsureAttackSlots(attacks, usable, slotCount);
        }

        /// <summary>
        /// ModelPartLossのリム定義から使用可能部位を集める
        /// </summary>
        /// <param name="partLoss">部位欠損コントローラ</param>
        /// <returns>使用可能な部位</returns>
        public static HashSet<BonePart> CollectAvailableParts(ModelPartLossController partLoss)
        {
            var parts = new HashSet<BonePart> { BonePart.Body };
            if (partLoss == null || partLoss.Limbs == null)
            {
                return parts;
            }

            for (int i = 0; i < partLoss.Limbs.Count; i++)
            {
                BonePart part = partLoss.Limbs[i].Part;
                if (part != BonePart.Body)
                {
                    parts.Add(part);
                }
            }

            return parts;
        }

        private static BonePart GetRequiredPart(MotionType motion)
        {
            switch (motion)
            {
                case MotionType.Punch:
                case MotionType.Elbow:
                case MotionType.Uppercut:
                case MotionType.Slap:
                    return BonePart.Arm;
                case MotionType.Kick:
                case MotionType.Stomp:
                case MotionType.Knee:
                case MotionType.LowSweep:
                    return BonePart.Leg;
                case MotionType.TailWhip:
                    return BonePart.Back;
                case MotionType.Headbutt:
                case MotionType.Bite:
                    return BonePart.Front;
                case MotionType.Tackle:
                case MotionType.SpinTackle:
                case MotionType.BodySlam:
                case MotionType.ShoulderRam:
                case MotionType.BellyFlop:
                case MotionType.HipCheck:
                case MotionType.GroundPound:
                default:
                    return BonePart.Body;
            }
        }
    }
}
