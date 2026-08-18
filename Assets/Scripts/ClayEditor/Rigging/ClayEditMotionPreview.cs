using System.Collections.Generic;
using UnityEngine;

namespace ClayEditor.Rigging
{
    /// <summary>
    /// ClayEditのアニメーション確認用に走行と汎用攻撃のモーションを決める
    /// </summary>
    public static class ClayEditMotionPreview
    {
        private static readonly MotionType[] GenericAttackPriority =
        {
            MotionType.Punch,
            MotionType.Kick,
            MotionType.Tackle,
            MotionType.SpinTackle,
            MotionType.TailWhip,
            MotionType.Headbutt,
            MotionType.Elbow,
            MotionType.Uppercut,
            MotionType.Stomp,
            MotionType.Knee,
            MotionType.BodySlam,
            MotionType.ShoulderRam,
            MotionType.BellyFlop,
            MotionType.HipCheck,
            MotionType.GroundPound,
            MotionType.Slap,
            MotionType.LowSweep,
            MotionType.Bite,
            MotionType.Chop,
            MotionType.DoubleSlap,
            MotionType.DoubleKick,
            MotionType.Peck,
            MotionType.HornAttack,
            MotionType.TailSlam,
            MotionType.Rollout
        };

        /// <summary>
        /// モデル形状に合わせた走行モーションを返す
        /// </summary>
        /// <param name="character">手続き的アニメーション</param>
        /// <returns>LegRunまたはRun</returns>
        public static MotionType ResolveRunMotion(ProceduralMotionCharacter character)
        {
            if (character != null && character.HasLegs)
            {
                return MotionType.LegRun;
            }

            return MotionType.Run;
        }

        /// <summary>
        /// モデル形状からプレビュー用の汎用攻撃モーションを返す
        /// </summary>
        /// <param name="analyzer">部位解析</param>
        /// <param name="bones">生成済みボーン群</param>
        /// <returns>使用可能な攻撃がなければnull</returns>
        public static MotionType? ResolveGenericAttack(SkeletonPartAnalyzer analyzer, Transform[] bones)
        {
            if (analyzer == null || bones == null || bones.Length == 0)
            {
                return null;
            }

            List<MotionType> available = analyzer.GetAvailableMotions(bones);
            if (available == null || available.Count == 0)
            {
                return null;
            }

            for (int i = 0; i < GenericAttackPriority.Length; i++)
            {
                MotionType candidate = GenericAttackPriority[i];
                if (!ProceduralMotionCharacter.IsAttackMotion(candidate))
                {
                    continue;
                }

                if (available.Contains(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
