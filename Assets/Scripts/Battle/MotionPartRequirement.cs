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
        /// Bodyは破壊部位なし
        /// 使用部位と同じ部位は狙わない
        /// </summary>
        public static BonePart GetTargetDestroyPart(MotionType motion)
        {
            switch (motion)
            {
                // 腕技→頭や脚や後ろを狙う
                case MotionType.Punch:
                    return BonePart.Front;
                case MotionType.Elbow:
                    return BonePart.Leg;
                case MotionType.Uppercut:
                    return BonePart.Front;
                case MotionType.Slap:
                    return BonePart.Back;

                // 脚技→腕や頭や後ろを狙う
                case MotionType.Kick:
                    return BonePart.Front;
                case MotionType.Stomp:
                    return BonePart.Arm;
                case MotionType.Knee:
                    return BonePart.Back;
                case MotionType.LowSweep:
                    return BonePart.Arm;

                // 前技→腕や脚を狙う
                case MotionType.Headbutt:
                    return BonePart.Arm;
                case MotionType.Bite:
                    return BonePart.Leg;

                // 後ろ技→脚を狙う
                case MotionType.TailWhip:
                    return BonePart.Leg;

                // 胴体技と魔法は破壊部位なし
                case MotionType.Tackle:
                case MotionType.SpinTackle:
                case MotionType.BodySlam:
                case MotionType.ShoulderRam:
                case MotionType.BellyFlop:
                case MotionType.HipCheck:
                case MotionType.GroundPound:
                case MotionType.Fireball:
                case MotionType.WindSlasher:
                case MotionType.DiamondDust:
                case MotionType.ThunderShock:
                    return BonePart.Body;

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
                case BonePart.Arm:
                    return Localization.LocalizedText.GetOrFallback(
                        Localization.GameTextKeys.BattlePartArm, "腕");
                case BonePart.Leg:
                    return Localization.LocalizedText.GetOrFallback(
                        Localization.GameTextKeys.BattlePartLeg, "脚");
                case BonePart.Front:
                    return Localization.LocalizedText.GetOrFallback(
                        Localization.GameTextKeys.BattlePartFront, "前");
                case BonePart.Back:
                    return Localization.LocalizedText.GetOrFallback(
                        Localization.GameTextKeys.BattlePartBack, "後");
                case BonePart.Body:
                    return Localization.LocalizedText.GetOrFallback(
                        Localization.GameTextKeys.BattlePartBody, "なし");
                default:
                    return part.ToString();
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
        /// 攻撃の基礎威力倍率を返す
        /// 星が多いほど威力とコストと射程のコスパが良い
        /// 星1:約2.5 星2:約3.6 星3:約4.1(表示威力/ガッツ)
        /// </summary>
        public static float GetPower(MotionType motion)
        {
            switch (motion)
            {
                // 星1
                case MotionType.Slap: return 0.50f;
                case MotionType.LowSweep: return 0.55f;
                case MotionType.Punch: return 0.58f;
                case MotionType.HipCheck: return 0.65f;
                case MotionType.Tackle: return 0.68f;
                // 星2
                case MotionType.Elbow: return 0.88f;
                case MotionType.Bite: return 0.90f;
                case MotionType.ShoulderRam: return 0.92f;
                case MotionType.Uppercut: return 0.95f;
                case MotionType.Knee: return 0.98f;
                case MotionType.Kick: return 1.00f;
                case MotionType.Headbutt: return 1.05f;
                case MotionType.TailWhip: return 1.05f;
                // 星3
                case MotionType.Stomp: return 1.25f;
                case MotionType.WindSlasher: return 1.30f;
                case MotionType.GroundPound: return 1.32f;
                case MotionType.Fireball: return 1.35f;
                case MotionType.BellyFlop: return 1.38f;
                case MotionType.DiamondDust: return 1.40f;
                case MotionType.BodySlam: return 1.45f;
                case MotionType.SpinTackle: return 1.48f;
                case MotionType.ThunderShock: return 1.50f;
                default: return 0.58f;
            }
        }

        /// <summary>
        /// 技の射程距離帯を返す(短距離中距離長距離の3パターン)
        /// </summary>
        /// <param name="motion">攻撃モーション</param>
        public static BattleDistanceBand GetRangeBand(MotionType motion)
        {
            switch (motion)
            {
                // 中距離
                case MotionType.LowSweep:
                case MotionType.Tackle:
                case MotionType.ShoulderRam:
                case MotionType.Kick:
                case MotionType.TailWhip:
                case MotionType.SpinTackle:
                    return BattleDistanceBand.Mid;

                // 長距離
                case MotionType.WindSlasher:
                case MotionType.Fireball:
                case MotionType.DiamondDust:
                case MotionType.ThunderShock:
                    return BattleDistanceBand.Far;

                // 短距離(既定含む)
                case MotionType.Slap:
                case MotionType.Punch:
                case MotionType.HipCheck:
                case MotionType.Bite:
                case MotionType.Elbow:
                case MotionType.Uppercut:
                case MotionType.Knee:
                case MotionType.Headbutt:
                case MotionType.Stomp:
                case MotionType.GroundPound:
                case MotionType.BellyFlop:
                case MotionType.BodySlam:
                default:
                    return BattleDistanceBand.Close;
            }
        }

        /// <summary>
        /// 使用できる間合い(x=最小 y=最大)を返す
        /// 短距離中距離長距離の3帯のいずれか
        /// </summary>
        public static Vector2 GetRange(MotionType motion)
        {
            return GetRange(GetRangeBand(motion), BattleDistanceBandResolver.DefaultMaxDistance);
        }

        /// <summary>
        /// 距離帯の間合い区間を返す
        /// </summary>
        /// <param name="band">距離帯</param>
        /// <param name="maxDistance">最大間合い</param>
        public static Vector2 GetRange(BattleDistanceBand band, float maxDistance)
        {
            float safeMax = maxDistance > 0f
                ? maxDistance
                : BattleDistanceBandResolver.DefaultMaxDistance;
            float closeMax = Mathf.Min(BattleDistanceBandResolver.CloseMaxDistance, safeMax);
            float midMax = Mathf.Min(BattleDistanceBandResolver.MidMaxDistance, safeMax);
            midMax = Mathf.Max(midMax, closeMax);

            switch (band)
            {
                case BattleDistanceBand.Mid:
                    return new Vector2(closeMax, midMax);
                case BattleDistanceBand.Far:
                    return new Vector2(midMax, safeMax);
                case BattleDistanceBand.Close:
                default:
                    return new Vector2(0f, closeMax);
            }
        }

        /// <summary>
        /// 攻撃の使用距離をUI表示用テキストに整形する
        /// </summary>
        public static string FormatRangeLabel(MotionType motion)
        {
            return BattleDistanceBandResolver.ToDisplayName(GetRangeBand(motion));
        }

        /// <summary>
        /// 攻撃の使用距離をUI表示用テキストに整形する
        /// </summary>
        public static string FormatRangeLabel(float rangeMin, float rangeMax)
        {
            BattleDistanceBand band = ResolveRangeBandFromBounds(rangeMin, rangeMax);
            return BattleDistanceBandResolver.ToDisplayName(band);
        }

        private static BattleDistanceBand ResolveRangeBandFromBounds(float rangeMin, float rangeMax)
        {
            float center = (rangeMin + rangeMax) * 0.5f;
            return BattleDistanceBandResolver.Resolve(
                center,
                BattleDistanceBandResolver.DefaultMaxDistance);
        }

        /// <summary>
        /// UI表示用の威力値を返す
        /// </summary>
        public static int GetPowerDisplayValue(MotionType motion)
        {
            return Mathf.RoundToInt(GetPower(motion) * 100f);
        }

        /// <summary>
        /// 攻撃の強さランクを返す(1〜3)
        /// </summary>
        /// <param name="motion">攻撃</param>
        public static int GetStrengthRank(MotionType motion)
        {
            return AttackMotionSelector.GetStrengthRank(motion);
        }

        /// <summary>
        /// 強さランクを★表示にする
        /// </summary>
        /// <param name="rank">強さランク(1〜3)</param>
        public static string FormatStrengthRankStars(int rank)
        {
            int clamped = Mathf.Clamp(rank, 1, MaxStrengthRank);
            var chars = new char[MaxStrengthRank];
            for (int i = 0; i < MaxStrengthRank; i++)
            {
                chars[i] = i < clamped ? '★' : '☆';
            }

            return new string(chars);
        }

        /// <summary>
        /// 技名の横に強さランク★を付けた表示名を返す
        /// 戦闘UI以外で使う
        /// </summary>
        /// <param name="motion">攻撃</param>
        public static string FormatDisplayNameWithStrengthRank(MotionType motion)
        {
            return GetDisplayName(motion) + " " + FormatStrengthRankStars(GetStrengthRank(motion));
        }

        /// <summary>
        /// 強さランクの最大値
        /// </summary>
        public const int MaxStrengthRank = 3;

        /// <summary>
        /// UI表示用のガッツコストを返す
        /// </summary>
        public static int GetGutsCostDisplayValue(MotionType motion)
        {
            return Mathf.RoundToInt(GetGutsCost(motion));
        }

        /// <summary>
        /// 必要ガッツ(行動力)を返す
        /// 星が多いほど威力あたりのコストが下がる
        /// 星1:約2.5 星2:約3.6 星3:約4.1(表示威力/ガッツ)
        /// </summary>
        public static float GetGutsCost(MotionType motion)
        {
            switch (motion)
            {
                // 星1 コスパが悪い
                case MotionType.Slap: return 20f;
                case MotionType.LowSweep: return 22f;
                case MotionType.Punch: return 22f;
                case MotionType.HipCheck: return 25f;
                case MotionType.Tackle: return 26f;
                // 星2
                case MotionType.Elbow: return 24f;
                case MotionType.Bite: return 25f;
                case MotionType.ShoulderRam: return 26f;
                case MotionType.Uppercut: return 26f;
                case MotionType.Knee: return 27f;
                case MotionType.Kick: return 28f;
                case MotionType.Headbutt: return 28f;
                case MotionType.TailWhip: return 30f;
                // 星3 コスパが良い
                case MotionType.Stomp: return 30f;
                case MotionType.WindSlasher: return 32f;
                case MotionType.GroundPound: return 32f;
                case MotionType.Fireball: return 33f;
                case MotionType.BellyFlop: return 34f;
                case MotionType.DiamondDust: return 34f;
                case MotionType.BodySlam: return 35f;
                case MotionType.SpinTackle: return 36f;
                case MotionType.ThunderShock: return 36f;
                default: return 22f;
            }
        }

        /// <summary>
        /// 命中率(0〜1)を返す
        /// </summary>
        public static float GetAccuracy(MotionType motion)
        {
            switch (motion)
            {
                // 星1
                case MotionType.Slap: return 0.93f;
                case MotionType.Punch: return 0.91f;
                case MotionType.HipCheck: return 0.90f;
                case MotionType.LowSweep: return 0.88f;
                case MotionType.Tackle: return 0.89f;
                // 星2
                case MotionType.Elbow: return 0.88f;
                case MotionType.Uppercut: return 0.86f;
                case MotionType.Knee: return 0.85f;
                case MotionType.Bite: return 0.85f;
                case MotionType.Headbutt: return 0.86f;
                case MotionType.ShoulderRam: return 0.85f;
                case MotionType.Kick: return 0.84f;
                case MotionType.TailWhip: return 0.83f;
                // 星3
                case MotionType.Stomp: return 0.82f;
                case MotionType.GroundPound: return 0.81f;
                case MotionType.BellyFlop: return 0.80f;
                case MotionType.BodySlam: return 0.79f;
                case MotionType.SpinTackle: return 0.78f;
                case MotionType.WindSlasher: return 0.82f;
                case MotionType.Fireball: return 0.80f;
                case MotionType.DiamondDust: return 0.79f;
                case MotionType.ThunderShock: return 0.78f;
                default: return 0.88f;
            }
        }

        /// <summary>
        /// 使用後の硬直時間(秒)を返す
        /// </summary>
        public static float GetRecovery(MotionType motion)
        {
            switch (motion)
            {
                // 星1
                case MotionType.Slap: return 0.55f;
                case MotionType.Punch: return 0.60f;
                case MotionType.HipCheck: return 0.65f;
                case MotionType.LowSweep: return 0.72f;
                case MotionType.Tackle: return 0.78f;
                // 星2
                case MotionType.Elbow: return 0.82f;
                case MotionType.Uppercut: return 0.85f;
                case MotionType.Knee: return 0.88f;
                case MotionType.Bite: return 0.90f;
                case MotionType.Headbutt: return 0.92f;
                case MotionType.ShoulderRam: return 0.95f;
                case MotionType.Kick: return 1.00f;
                case MotionType.TailWhip: return 1.05f;
                // 星3
                case MotionType.Stomp: return 1.10f;
                case MotionType.GroundPound: return 1.15f;
                case MotionType.BellyFlop: return 1.18f;
                case MotionType.WindSlasher: return 1.15f;
                case MotionType.Fireball: return 1.20f;
                case MotionType.BodySlam: return 1.22f;
                case MotionType.DiamondDust: return 1.25f;
                case MotionType.SpinTackle: return 1.28f;
                case MotionType.ThunderShock: return 1.30f;
                default: return 0.70f;
            }
        }

        /// <summary>
        /// 攻撃前の溜め時間(秒)を返す
        /// 全技共通
        /// </summary>
        public static float GetWindUpDuration(MotionType motion)
        {
            return 0.8f;
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
        public const int MaxDisplayNameLength = 10;

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
            string key = Localization.GameTextKeys.Motion(motion.ToString());
            string japanese;
            switch (motion)
            {
                case MotionType.Punch: japanese = "パンチ"; break;
                case MotionType.Elbow: japanese = "エルボー"; break;
                case MotionType.Kick: japanese = "キック"; break;
                case MotionType.Stomp: japanese = "ストンプ"; break;
                case MotionType.Tackle: japanese = "タックル"; break;
                case MotionType.SpinTackle: japanese = "回転タックル"; break;
                case MotionType.TailWhip: japanese = "しっぽ攻撃"; break;
                case MotionType.Headbutt: japanese = "頭突き"; break;
                case MotionType.BodySlam: japanese = "ボディスラム"; break;
                case MotionType.Uppercut: japanese = "アッパー"; break;
                case MotionType.Knee: japanese = "膝蹴り"; break;
                case MotionType.ShoulderRam: japanese = "ショルダー"; break;
                case MotionType.BellyFlop: japanese = "のしかかり"; break;
                case MotionType.HipCheck: japanese = "腰ブン"; break;
                case MotionType.GroundPound: japanese = "地叩き"; break;
                case MotionType.Slap: japanese = "平打ち"; break;
                case MotionType.LowSweep: japanese = "足払い"; break;
                case MotionType.Bite: japanese = "噛みつき"; break;
                case MotionType.Fireball: japanese = "ファイアーボール"; break;
                case MotionType.WindSlasher: japanese = "ウィンドスラッシャー"; break;
                case MotionType.DiamondDust: japanese = "ダイヤモンドダスト"; break;
                case MotionType.ThunderShock: japanese = "サンダーショック"; break;
                default: japanese = motion.ToString(); break;
            }

            return Localization.LocalizedText.GetOrFallback(key, japanese);
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
