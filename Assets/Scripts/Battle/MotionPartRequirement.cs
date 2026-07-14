using UnityEngine;

using ClayEditor.Rigging;

namespace Battle
{
    /// <summary>
    /// 攻撃モーションの必要部位・威力・間合い・ガッツ消費・命中率・硬直の対応表。
    /// </summary>
    public static class MotionPartRequirement
    {
        /// <summary>
        /// その攻撃が必要とする部位を返す(Bodyは欠損しないため常に使用可)。
        /// </summary>
        public static BonePart GetRequiredPart(MotionType motion)
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

        /// <summary>
        /// その攻撃が相手のどの部位を破壊できるかを返す
        /// Bodyは全身攻撃で残っている部位から選ぶ
        /// </summary>
        public static BonePart GetTargetDestroyPart(MotionType motion)
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

        /// <summary>
        /// 破壊対象部位のUI表示名を返す
        /// </summary>
        public static string FormatTargetDestroyPartLabel(BonePart part)
        {
            switch (part)
            {
                case BonePart.Arm: return "腕";
                case BonePart.Leg: return "脚";
                case BonePart.Front: return "前";
                case BonePart.Back: return "後";
                case BonePart.Body: return "任意";
                default: return part.ToString();
            }
        }

        /// <summary>
        /// 攻撃の破壊対象部位のUI表示名を返す
        /// </summary>
        public static string FormatTargetDestroyPartLabel(MotionType motion)
        {
            return FormatTargetDestroyPartLabel(GetTargetDestroyPart(motion));
        }

        /// <summary>
        /// 攻撃モーションかどうかを返す。
        /// </summary>
        public static bool IsAttack(MotionType motion)
        {
            return ProceduralMotionCharacter.IsAttackMotion(motion);
        }

        /// <summary>
        /// 攻撃の基礎威力倍率を返す。
        /// </summary>
        public static float GetPower(MotionType motion)
        {
            switch (motion)
            {
                case MotionType.Punch: return 0.9f;
                case MotionType.Elbow: return 1.05f;
                case MotionType.Kick: return 1.0f;
                case MotionType.Stomp: return 1.15f;
                case MotionType.Tackle: return 0.95f;
                case MotionType.SpinTackle: return 1.25f;
                case MotionType.TailWhip: return 1.1f;
                case MotionType.Headbutt: return 1.0f;
                case MotionType.BodySlam: return 1.2f;
                case MotionType.Uppercut: return 1.08f;
                case MotionType.Knee: return 1.02f;
                case MotionType.ShoulderRam: return 1f;
                case MotionType.BellyFlop: return 1.18f;
                case MotionType.HipCheck: return 0.98f;
                case MotionType.GroundPound: return 1.12f;
                case MotionType.Slap: return 0.88f;
                case MotionType.LowSweep: return 0.92f;
                case MotionType.Bite: return 1.05f;
                default: return 0.9f;
            }
        }

        /// <summary>
        /// 使用できる間合い(x=最小, y=最大)を返す。距離の単位は最大間合いに合わせる。
        /// </summary>
        public static Vector2 GetRange(MotionType motion)
        {
            switch (motion)
            {
                case MotionType.Punch: return new Vector2(0f, 3f);
                case MotionType.Elbow: return new Vector2(0f, 2f);
                case MotionType.Kick: return new Vector2(1f, 5f);
                case MotionType.Stomp: return new Vector2(0f, 2.5f);
                case MotionType.Tackle: return new Vector2(0f, 4f);
                case MotionType.SpinTackle: return new Vector2(0f, 2.5f);
                case MotionType.TailWhip: return new Vector2(3f, 7f);
                case MotionType.Headbutt: return new Vector2(0f, 3.5f);
                case MotionType.BodySlam: return new Vector2(0f, 3f);
                case MotionType.Uppercut: return new Vector2(0f, 2.5f);
                case MotionType.Knee: return new Vector2(0f, 2f);
                case MotionType.ShoulderRam: return new Vector2(0f, 3.5f);
                case MotionType.BellyFlop: return new Vector2(0f, 3f);
                case MotionType.HipCheck: return new Vector2(0f, 2.5f);
                case MotionType.GroundPound: return new Vector2(0f, 2f);
                case MotionType.Slap: return new Vector2(0f, 3.5f);
                case MotionType.LowSweep: return new Vector2(0f, 4f);
                case MotionType.Bite: return new Vector2(0f, 2.5f);
                default: return new Vector2(0f, 4f);
            }
        }

        /// <summary>
        /// 攻撃の使用距離をUI表示用テキストに整形する
        /// </summary>
        public static string FormatRangeLabel(MotionType motion)
        {
            Vector2 range = GetRange(motion);
            return FormatRangeLabel(range.x, range.y);
        }

        /// <summary>
        /// 攻撃の使用距離をUI表示用テキストに整形する
        /// </summary>
        public static string FormatRangeLabel(float rangeMin, float rangeMax)
        {
            if (Mathf.Approximately(rangeMin, rangeMax))
            {
                return rangeMin.ToString("0.#");
            }

            return rangeMin.ToString("0.#") + "〜" + rangeMax.ToString("0.#");
        }

        /// <summary>
        /// UI表示用の威力値を返す
        /// </summary>
        public static int GetPowerDisplayValue(MotionType motion)
        {
            return Mathf.RoundToInt(GetPower(motion) * 100f);
        }

        /// <summary>
        /// UI表示用のガッツコストを返す
        /// </summary>
        public static int GetGutsCostDisplayValue(MotionType motion)
        {
            return Mathf.RoundToInt(GetGutsCost(motion));
        }

        /// <summary>
        /// 必要ガッツ(行動力)を返す。
        /// </summary>
        public static float GetGutsCost(MotionType motion)
        {
            switch (motion)
            {
                case MotionType.Punch: return 20f;
                case MotionType.Elbow: return 18f;
                case MotionType.Kick: return 30f;
                case MotionType.Stomp: return 28f;
                case MotionType.Tackle: return 25f;
                case MotionType.SpinTackle: return 45f;
                case MotionType.TailWhip: return 35f;
                case MotionType.Headbutt: return 22f;
                case MotionType.BodySlam: return 38f;
                case MotionType.Uppercut: return 22f;
                case MotionType.Knee: return 24f;
                case MotionType.ShoulderRam: return 22f;
                case MotionType.BellyFlop: return 32f;
                case MotionType.HipCheck: return 20f;
                case MotionType.GroundPound: return 30f;
                case MotionType.Slap: return 18f;
                case MotionType.LowSweep: return 26f;
                case MotionType.Bite: return 24f;
                default: return 25f;
            }
        }

        /// <summary>
        /// 命中率(0〜1)を返す。
        /// </summary>
        public static float GetAccuracy(MotionType motion)
        {
            switch (motion)
            {
                case MotionType.Punch: return 0.9f;
                case MotionType.Elbow: return 0.88f;
                case MotionType.Kick: return 0.8f;
                case MotionType.Stomp: return 0.82f;
                case MotionType.Tackle: return 0.85f;
                case MotionType.SpinTackle: return 0.6f;
                case MotionType.TailWhip: return 0.75f;
                case MotionType.Headbutt: return 0.86f;
                case MotionType.BodySlam: return 0.78f;
                case MotionType.Uppercut: return 0.87f;
                case MotionType.Knee: return 0.84f;
                case MotionType.ShoulderRam: return 0.88f;
                case MotionType.BellyFlop: return 0.76f;
                case MotionType.HipCheck: return 0.9f;
                case MotionType.GroundPound: return 0.8f;
                case MotionType.Slap: return 0.91f;
                case MotionType.LowSweep: return 0.83f;
                case MotionType.Bite: return 0.86f;
                default: return 0.8f;
            }
        }

        /// <summary>
        /// 使用後の硬直時間(秒)を返す。
        /// </summary>
        public static float GetRecovery(MotionType motion)
        {
            switch (motion)
            {
                case MotionType.Punch: return 0.85f;
                case MotionType.Elbow: return 0.78f;
                case MotionType.Kick: return 1.05f;
                case MotionType.Stomp: return 0.98f;
                case MotionType.Tackle: return 1.05f;
                case MotionType.SpinTackle: return 1.45f;
                case MotionType.TailWhip: return 1.25f;
                case MotionType.Headbutt: return 0.92f;
                case MotionType.BodySlam: return 1.25f;
                case MotionType.Uppercut: return 0.8f;
                case MotionType.Knee: return 0.85f;
                case MotionType.ShoulderRam: return 0.95f;
                case MotionType.BellyFlop: return 1.2f;
                case MotionType.HipCheck: return 0.88f;
                case MotionType.GroundPound: return 1.15f;
                case MotionType.Slap: return 0.82f;
                case MotionType.LowSweep: return 1f;
                case MotionType.Bite: return 0.9f;
                default: return 1f;
            }
        }

        /// <summary>
        /// 攻撃前の溜め時間(秒)を返す。
        /// </summary>
        public static float GetWindUpDuration(MotionType motion)
        {
            switch (motion)
            {
                case MotionType.Punch: return 0.75f;
                case MotionType.Elbow: return 0.68f;
                case MotionType.Kick: return 0.9f;
                case MotionType.Stomp: return 0.82f;
                case MotionType.Tackle: return 0.82f;
                case MotionType.SpinTackle: return 1.15f;
                case MotionType.TailWhip: return 0.95f;
                case MotionType.Headbutt: return 0.72f;
                case MotionType.BodySlam: return 1.05f;
                case MotionType.Uppercut: return 0.7f;
                case MotionType.Knee: return 0.72f;
                case MotionType.ShoulderRam: return 0.75f;
                case MotionType.BellyFlop: return 0.95f;
                case MotionType.HipCheck: return 0.68f;
                case MotionType.GroundPound: return 0.88f;
                case MotionType.Slap: return 0.7f;
                case MotionType.LowSweep: return 0.78f;
                case MotionType.Bite: return 0.74f;
                default: return 0.8f;
            }
        }

        /// <summary>
        /// カウンター時の溜め時間(秒)を返す。
        /// </summary>
        public static float GetCounterWindUpDuration(MotionType motion)
        {
            return Mathf.Min(0.42f, GetWindUpDuration(motion) * 0.5f);
        }

        /// <summary>
        /// UI表示名の最大文字数
        /// </summary>
        public const int MaxDisplayNameLength = 8;

        /// <summary>
        /// UI表示名を返す
        /// </summary>
        public static string GetDisplayName(MotionType motion)
        {
            string name = GetDisplayNameInternal(motion);
            return NormalizeDisplayName(name);
        }

        /// <summary>
        /// UI表示名が最大文字数以内か検証する
        /// </summary>
        public static bool IsDisplayNameLengthValid(string displayName)
        {
            return !string.IsNullOrEmpty(displayName) && displayName.Length <= MaxDisplayNameLength;
        }

        private static string GetDisplayNameInternal(MotionType motion)
        {
            switch (motion)
            {
                case MotionType.Punch: return "パンチ";
                case MotionType.Elbow: return "エルボー";
                case MotionType.Kick: return "キック";
                case MotionType.Stomp: return "ストンプ";
                case MotionType.Tackle: return "タックル";
                case MotionType.SpinTackle: return "回転タックル";
                case MotionType.TailWhip: return "しっぽ攻撃";
                case MotionType.Headbutt: return "頭突き";
                case MotionType.BodySlam: return "ボディスラム";
                case MotionType.Uppercut: return "アッパー";
                case MotionType.Knee: return "膝蹴り";
                case MotionType.ShoulderRam: return "ショルダー";
                case MotionType.BellyFlop: return "のしかかり";
                case MotionType.HipCheck: return "腰ブン";
                case MotionType.GroundPound: return "地叩き";
                case MotionType.Slap: return "平打ち";
                case MotionType.LowSweep: return "足払い";
                case MotionType.Bite: return "噛みつき";
                default: return motion.ToString();
            }
        }

        private static string NormalizeDisplayName(string displayName)
        {
            if (string.IsNullOrEmpty(displayName) || displayName.Length <= MaxDisplayNameLength)
            {
                return displayName;
            }

            return displayName.Substring(0, MaxDisplayNameLength);
        }
    }
}
