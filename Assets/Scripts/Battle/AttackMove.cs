using ClayEditor.Rigging;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 1つの攻撃技。保存済みのMotionTypeから必要部位・威力・間合い・ガッツ・命中・硬直を決める。
    /// </summary>
    public sealed class AttackMove
    {
        public AttackMove(MotionType motion)
        {
            Motion = motion;
            RequiredPart = MotionPartRequirement.GetRequiredPart(motion);
            TargetDestroyPart = MotionPartRequirement.GetTargetDestroyPart(motion);
            Power = MotionPartRequirement.GetPower(motion);

            RangeBand = MotionPartRequirement.GetRangeBand(motion);
            Vector2 range = MotionPartRequirement.GetRange(RangeBand, BattleDistanceBandResolver.DefaultMaxDistance);
            RangeMin = range.x;
            RangeMax = range.y;

            GutsCost = MotionPartRequirement.GetGutsCost(motion);
            Accuracy = MotionPartRequirement.GetAccuracy(motion);
            Recovery = MotionPartRequirement.GetRecovery(motion);
            WindUp = MotionPartRequirement.GetWindUpDuration(motion);
            CounterWindUp = MotionPartRequirement.GetCounterWindUpDuration(motion);
        }

        /// <summary>
        /// 再生する攻撃モーション。
        /// </summary>
        public MotionType Motion { get; }

        /// <summary>
        /// この技に必要な部位(Bodyは常に使用可)。
        /// </summary>
        public BonePart RequiredPart { get; }

        /// <summary>
        /// 命中時に相手のどの部位を破壊できるか(Bodyは破壊なし)
        /// </summary>
        public BonePart TargetDestroyPart { get; }

        /// <summary>
        /// 威力倍率。
        /// </summary>
        public float Power { get; }

        /// <summary>
        /// 技が有効な距離帯(短中長のいずれか1帯)
        /// </summary>
        public BattleDistanceBand RangeBand { get; }

        /// <summary>
        /// 使用できる最小間合い。
        /// </summary>
        public float RangeMin { get; }

        /// <summary>
        /// 使用できる最大間合い。
        /// </summary>
        public float RangeMax { get; }

        /// <summary>
        /// 必要ガッツ(行動力)。
        /// </summary>
        public float GutsCost { get; }

        /// <summary>
        /// 命中率(0〜1)。
        /// </summary>
        public float Accuracy { get; }

        /// <summary>
        /// 使用後の硬直時間(秒)。
        /// </summary>
        public float Recovery { get; }

        /// <summary>
        /// 攻撃前の溜め時間(秒)。
        /// </summary>
        public float WindUp { get; }

        /// <summary>
        /// カウンター時の溜め時間(秒)。
        /// </summary>
        public float CounterWindUp { get; }

        /// <summary>
        /// UI表示名(現在の言語で都度解決する)
        /// </summary>
        public string DisplayName => MotionPartRequirement.GetDisplayName(Motion);

        /// <summary>
        /// 指定の間合いがこの技の射程帯内かを返す。
        /// </summary>
        public bool IsInRange(float distance)
        {
            return IsInRange(distance, BattleDistanceBandResolver.DefaultMaxDistance);
        }

        /// <summary>
        /// 指定の間合いがこの技の射程帯内かを返す。
        /// </summary>
        /// <param name="distance">現在間合い</param>
        /// <param name="maxDistance">最大間合い</param>
        public bool IsInRange(float distance, float maxDistance)
        {
            float safeMax = maxDistance > 0f
                ? maxDistance
                : BattleDistanceBandResolver.DefaultMaxDistance;
            return BattleDistanceBandResolver.Resolve(distance, safeMax) == RangeBand;
        }
    }
}