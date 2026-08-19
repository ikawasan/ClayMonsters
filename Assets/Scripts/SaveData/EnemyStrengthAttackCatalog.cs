using ClayEditor.Rigging;
using System.Collections.Generic;
using UnityEngine;

namespace SaveData
{
    /// <summary>
    /// 敵の強さ段階に応じた習得技を決める
    /// 候補は骨格で使用可能な技のみとし必要部位がない技は選ばない
    /// </summary>
    public static class EnemyStrengthAttackCatalog
    {
        /// <summary>
        /// NPC対戦の保存技抽選改訂番号
        /// </summary>
        public const int AttackDrawVersion = 3;

        // 実行時抽選用
        private const int DrawSalt = 0x62A26F;

        // 値を変えるとNPC対戦の保存技が差し替わる
        private const int NpcStoredDrawSalt = 0xC38B14;

        private static readonly EnemyStrengthTier[] AllTiers =
        {
            EnemyStrengthTier.Weak,
            EnemyStrengthTier.Normal,
            EnemyStrengthTier.Strong,
            EnemyStrengthTier.VeryStrong,
            EnemyStrengthTier.Strongest
        };

        /// <summary>
        /// 強さ段階と使用可能技から攻撃スロットを返す
        /// </summary>
        /// <param name="tier">強さ段階</param>
        /// <param name="usableAttacks">骨格で使える攻撃</param>
        /// <param name="slotIndex">敵スロット番号(抽選シード)</param>
        /// <param name="slotCount">スロット数</param>
        public static List<MotionType> Resolve(
            EnemyStrengthTier tier,
            IReadOnlyList<MotionType> usableAttacks,
            int slotIndex,
            int slotCount = ModelAttackMotionUtility.SlotCount)
        {
            return Resolve(tier, usableAttacks, slotIndex, slotCount, DrawSalt);
        }

        private static List<MotionType> Resolve(
            EnemyStrengthTier tier,
            IReadOnlyList<MotionType> usableAttacks,
            int slotIndex,
            int slotCount,
            int drawSalt)
        {
            int count = Mathf.Max(0, slotCount);
            var result = new List<MotionType>(count);
            if (count == 0)
            {
                return result;
            }

            List<MotionType> pool = BuildUsablePool(usableAttacks);
            if (pool.Count == 0)
            {
                Debug.LogError("[EnemyStrengthAttackCatalog] 使用可能攻撃がありません");
                return result;
            }

            var random = new System.Random(BuildSeed(slotIndex, tier, drawSalt));
            int[] rankTargets = BuildRankTargets(tier, count, random);
            var usedParts = new HashSet<BonePart>();

            for (int i = 0; i < count; i++)
            {
                MotionType? picked = TryPick(
                    pool,
                    result,
                    usedParts,
                    rankTargets[i],
                    preferUniquePart: true,
                    random);
                if (!picked.HasValue)
                {
                    picked = TryPick(
                        pool,
                        result,
                        usedParts,
                        rankTargets[i],
                        preferUniquePart: false,
                        random);
                }

                if (!picked.HasValue)
                {
                    picked = TryPickAny(pool, result, random);
                }

                if (!picked.HasValue)
                {
                    break;
                }

                MotionType motion = picked.Value;
                if (!pool.Contains(motion))
                {
                    Debug.LogError(
                        $"[EnemyStrengthAttackCatalog] 使用不可攻撃{motion}を選ぼうとしました");
                    continue;
                }

                result.Add(motion);
                usedParts.Add(GetRequiredPart(motion));
            }

            ValidateAgainstPool(result, pool);

            if (result.Count < count)
            {
                Debug.LogError(
                    $"[EnemyStrengthAttackCatalog] 攻撃スロットを埋められません({result.Count}/{count})");
            }

            return result;
        }

        /// <summary>
        /// 確認UI向けに保存技から推定した部位だけで候補を作り攻撃を返す
        /// 部位が無いのに全部位開放はしない
        /// </summary>
        /// <param name="tier">強さ段階</param>
        /// <param name="savedAttacks">保存攻撃</param>
        /// <param name="slotIndex">敵スロット番号</param>
        /// <param name="slotCount">スロット数</param>
        public static List<MotionType> ResolveForConfirmPreview(
            EnemyStrengthTier tier,
            IReadOnlyList<MotionType> savedAttacks,
            int slotIndex,
            int slotCount = ModelAttackMotionUtility.SlotCount)
        {
            return DrawFromSavedAttacks(tier, savedAttacks, slotIndex, slotCount, DrawSalt);
        }

        /// <summary>
        /// NPC対戦向けに強さ段階ごとの習得技を抽選して保存する
        /// 改訂番号が変わったら全員分を引き直す
        /// </summary>
        /// <param name="slot">敵スロット</param>
        /// <param name="slotIndex">敵スロット番号</param>
        /// <returns>抽選し直したらtrue</returns>
        public static bool EnsureFromSaved(ModelSaveSlot slot, int slotIndex)
        {
            if (slot == null)
            {
                return false;
            }

            if (slot.hasEnemyStrengthAttacks
                && slot.enemyStrengthAttackVersion == AttackDrawVersion
                && HasCompleteSet(slot))
            {
                return false;
            }

            int slotCount = ModelAttackMotionUtility.SlotCount;
            for (int i = 0; i < AllTiers.Length; i++)
            {
                EnemyStrengthTier tier = AllTiers[i];
                List<MotionType> drawn = DrawFromSavedAttacks(
                    tier,
                    slot.attackMotions,
                    slotIndex,
                    slotCount,
                    NpcStoredDrawSalt);
                SetStored(slot, tier, drawn);
            }

            slot.hasEnemyStrengthAttacks = true;
            slot.enemyStrengthAttackVersion = AttackDrawVersion;
            return true;
        }

        /// <summary>
        /// 保存済みの強さ段階技を返す
        /// 未抽選なら空リスト
        /// </summary>
        /// <param name="slot">敵スロット</param>
        /// <param name="tier">強さ段階</param>
        public static List<MotionType> GetStored(ModelSaveSlot slot, EnemyStrengthTier tier)
        {
            if (slot == null)
            {
                return new List<MotionType>();
            }

            List<MotionType> source = GetStoredList(slot, tier);
            if (source == null || source.Count == 0)
            {
                return new List<MotionType>();
            }

            return new List<MotionType>(source);
        }

        /// <summary>
        /// 保存済み攻撃から使用部位を推定する
        /// Body以外が無い場合はBodyのみとし勝手に腕脚を足さない
        /// </summary>
        /// <param name="savedAttacks">保存攻撃</param>
        public static HashSet<BonePart> InferAvailableParts(IReadOnlyList<MotionType> savedAttacks)
        {
            var parts = new HashSet<BonePart> { BonePart.Body };
            if (savedAttacks == null)
            {
                return parts;
            }

            for (int i = 0; i < savedAttacks.Count; i++)
            {
                MotionType motion = savedAttacks[i];
                if (!ProceduralMotionCharacter.IsAttackMotion(motion))
                {
                    continue;
                }

                parts.Add(GetRequiredPart(motion));
            }

            return parts;
        }

        private static List<MotionType> DrawFromSavedAttacks(
            EnemyStrengthTier tier,
            IReadOnlyList<MotionType> savedAttacks,
            int slotIndex,
            int slotCount,
            int drawSalt)
        {
            HashSet<BonePart> parts = InferAvailableParts(savedAttacks);
            List<MotionType> previewPool = AttackMotionSelector.CollectAttacksForAvailableParts(parts);
            return Resolve(tier, previewPool, slotIndex, slotCount, drawSalt);
        }

        private static bool HasCompleteSet(ModelSaveSlot slot)
        {
            for (int i = 0; i < AllTiers.Length; i++)
            {
                List<MotionType> list = GetStoredList(slot, AllTiers[i]);
                if (list == null || list.Count == 0)
                {
                    return false;
                }
            }

            return true;
        }

        private static List<MotionType> GetStoredList(ModelSaveSlot slot, EnemyStrengthTier tier)
        {
            return tier switch
            {
                EnemyStrengthTier.Weak => slot.attackMotionsWeak,
                EnemyStrengthTier.Normal => slot.attackMotionsNormal,
                EnemyStrengthTier.Strong => slot.attackMotionsStrong,
                EnemyStrengthTier.VeryStrong => slot.attackMotionsVeryStrong,
                EnemyStrengthTier.Strongest => slot.attackMotionsStrongest,
                _ => slot.attackMotionsNormal
            };
        }

        private static void SetStored(ModelSaveSlot slot, EnemyStrengthTier tier, List<MotionType> attacks)
        {
            List<MotionType> copy = attacks != null ? new List<MotionType>(attacks) : new List<MotionType>();
            switch (tier)
            {
                case EnemyStrengthTier.Weak:
                    slot.attackMotionsWeak = copy;
                    break;
                case EnemyStrengthTier.Normal:
                    slot.attackMotionsNormal = copy;
                    break;
                case EnemyStrengthTier.Strong:
                    slot.attackMotionsStrong = copy;
                    break;
                case EnemyStrengthTier.VeryStrong:
                    slot.attackMotionsVeryStrong = copy;
                    break;
                default:
                    slot.attackMotionsStrongest = copy;
                    break;
            }
        }

        private static List<MotionType> BuildUsablePool(IReadOnlyList<MotionType> usableAttacks)
        {
            var pool = new List<MotionType>();
            if (usableAttacks == null)
            {
                return pool;
            }

            for (int i = 0; i < usableAttacks.Count; i++)
            {
                MotionType motion = usableAttacks[i];
                if (!ProceduralMotionCharacter.IsAttackMotion(motion) || pool.Contains(motion))
                {
                    continue;
                }

                pool.Add(motion);
            }

            return pool;
        }

        private static void ValidateAgainstPool(List<MotionType> result, List<MotionType> pool)
        {
            if (result == null || pool == null)
            {
                return;
            }

            var poolSet = new HashSet<MotionType>(pool);
            for (int i = result.Count - 1; i >= 0; i--)
            {
                MotionType motion = result[i];
                if (poolSet.Contains(motion))
                {
                    continue;
                }

                Debug.LogError(
                    $"[EnemyStrengthAttackCatalog] 部位条件を満たさない攻撃{motion}を除外しました");
                result.RemoveAt(i);
            }
        }

        private static int BuildSeed(int slotIndex, EnemyStrengthTier tier, int drawSalt)
        {
            unchecked
            {
                return (slotIndex + 1) * 397 ^ ((int)tier + 1) * 7919 ^ drawSalt;
            }
        }

        private static int[] BuildRankTargets(EnemyStrengthTier tier, int count, System.Random random)
        {
            var targets = new int[count];
            for (int i = 0; i < count; i++)
            {
                targets[i] = tier switch
                {
                    EnemyStrengthTier.Weak => 1,
                    EnemyStrengthTier.Normal => i < count / 2 ? 1 : 2,
                    EnemyStrengthTier.Strong => i < count / 2 ? 2 : 3,
                    EnemyStrengthTier.VeryStrong => random.Next(1, 4),
                    EnemyStrengthTier.Strongest => random.Next(2, 4),
                    _ => 1
                };
            }

            if (tier == EnemyStrengthTier.Weak && count > 0 && random.Next(0, 100) < 12)
            {
                targets[random.Next(0, count)] = 2;
            }

            if (tier == EnemyStrengthTier.Normal && count > 0 && random.Next(0, 100) < 18)
            {
                targets[random.Next(0, count)] = 3;
            }

            ShuffleInPlace(targets, random);
            return targets;
        }

        private static MotionType? TryPick(
            IReadOnlyList<MotionType> pool,
            List<MotionType> used,
            HashSet<BonePart> usedParts,
            int preferredRank,
            bool preferUniquePart,
            System.Random random)
        {
            var exact = new List<MotionType>();
            var near = new List<MotionType>();
            for (int i = 0; i < pool.Count; i++)
            {
                MotionType motion = pool[i];
                if (used.Contains(motion))
                {
                    continue;
                }

                BonePart part = GetRequiredPart(motion);
                if (preferUniquePart
                    && usedParts.Contains(part)
                    && HasUnusedPartCandidate(pool, used, usedParts))
                {
                    continue;
                }

                int rank = AttackMotionSelector.GetStrengthRank(motion);
                if (rank == preferredRank)
                {
                    exact.Add(motion);
                }
                else if (Mathf.Abs(rank - preferredRank) == 1)
                {
                    near.Add(motion);
                }
            }

            if (exact.Count > 0)
            {
                return exact[random.Next(0, exact.Count)];
            }

            if (near.Count > 0)
            {
                return near[random.Next(0, near.Count)];
            }

            return null;
        }

        private static MotionType? TryPickAny(
            IReadOnlyList<MotionType> pool,
            List<MotionType> used,
            System.Random random)
        {
            var candidates = new List<MotionType>();
            for (int i = 0; i < pool.Count; i++)
            {
                MotionType motion = pool[i];
                if (used.Contains(motion))
                {
                    continue;
                }

                candidates.Add(motion);
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            return candidates[random.Next(0, candidates.Count)];
        }

        private static bool HasUnusedPartCandidate(
            IReadOnlyList<MotionType> pool,
            List<MotionType> used,
            HashSet<BonePart> usedParts)
        {
            for (int i = 0; i < pool.Count; i++)
            {
                MotionType motion = pool[i];
                if (used.Contains(motion))
                {
                    continue;
                }

                if (!usedParts.Contains(GetRequiredPart(motion)))
                {
                    return true;
                }
            }

            return false;
        }

        private static void ShuffleInPlace(int[] values, System.Random random)
        {
            if (values == null || values.Length <= 1)
            {
                return;
            }

            for (int i = values.Length - 1; i > 0; i--)
            {
                int j = random.Next(0, i + 1);
                (values[i], values[j]) = (values[j], values[i]);
            }
        }

        private static BonePart GetRequiredPart(MotionType motion)
        {
            switch (motion)
            {
                case MotionType.Punch:
                case MotionType.Elbow:
                case MotionType.Uppercut:
                case MotionType.Slap:
                case MotionType.Chop:
                case MotionType.DoubleSlap:
                case MotionType.HammerArm:
                    return BonePart.Arm;
                case MotionType.Kick:
                case MotionType.Stomp:
                case MotionType.Knee:
                case MotionType.LowSweep:
                case MotionType.DoubleKick:
                case MotionType.DropKick:
                    return BonePart.Leg;
                case MotionType.TailWhip:
                case MotionType.TailSlam:
                    return BonePart.Back;
                case MotionType.Headbutt:
                case MotionType.Bite:
                case MotionType.Peck:
                case MotionType.HornAttack:
                    return BonePart.Front;
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
                case MotionType.Rollout:
                default:
                    return BonePart.Body;
            }
        }
    }
}
