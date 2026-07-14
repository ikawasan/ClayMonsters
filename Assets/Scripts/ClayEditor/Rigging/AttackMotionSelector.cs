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
        /// 使用可能な攻撃から保存登録用の攻撃を最大count件ランダムで選ぶ
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
            return ShuffleAndTake(CollectUsableAttacks(analyzer, bones), count);
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
                int j = Random.Range(0, i + 1);
                (result[i], result[j]) = (result[j], result[i]);
            }

            if (result.Count > count)
            {
                result.RemoveRange(count, result.Count - count);
            }

            return result;
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
