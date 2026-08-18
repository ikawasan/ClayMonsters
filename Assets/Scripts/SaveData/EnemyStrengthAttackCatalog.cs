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
        private const int MaxMagicCount = 1;

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

            var random = new System.Random(BuildSeed(slotIndex, tier));
            int[] rankTargets = BuildRankTargets(tier, count, random);
            bool allowMagic = RollMagicAllowed(tier, random);
            var usedParts = new HashSet<BonePart>();
            int magicCount = 0;

            for (int i = 0; i < count; i++)
            {
                bool wantMagicNow = allowMagic
                    && magicCount < MaxMagicCount
                    && rankTargets[i] >= 3;
                MotionType? picked = null;
                if (wantMagicNow)
                {
                    picked = TryPickMagic(
                        pool,
                        result,
                        usedParts,
                        preferUniquePart: true,
                        random);
                    if (!picked.HasValue)
                    {
                        picked = TryPickMagic(
                            pool,
                            result,
                            usedParts,
                            preferUniquePart: false,
                            random);
                    }
                }

                if (!picked.HasValue)
                {
                    picked = TryPick(
                        pool,
                        result,
                        usedParts,
                        rankTargets[i],
                        preferUniquePart: true,
                        allowMagic: allowMagic && magicCount < MaxMagicCount,
                        random);
                }

                if (!picked.HasValue)
                {
                    picked = TryPick(
                        pool,
                        result,
                        usedParts,
                        rankTargets[i],
                        preferUniquePart: false,
                        allowMagic: allowMagic && magicCount < MaxMagicCount,
                        random);
                }

                if (!picked.HasValue)
                {
                    picked = TryPickAny(pool, result, allowMagic && magicCount < MaxMagicCount, random);
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
                if (ProceduralMotionCharacter.IsMagicAttack(motion))
                {
                    magicCount++;
                }
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
            HashSet<BonePart> parts = InferAvailableParts(savedAttacks);
            List<MotionType> previewPool = AttackMotionSelector.CollectAttacksForAvailableParts(parts);
            return Resolve(tier, previewPool, slotIndex, slotCount);
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

        private static int BuildSeed(int slotIndex, EnemyStrengthTier tier)
        {
            unchecked
            {
                return (slotIndex + 1) * 397 ^ ((int)tier + 1) * 7919 ^ 0x51F15E;
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
                    EnemyStrengthTier.VeryStrong => i == count - 1 ? 2 : 3,
                    EnemyStrengthTier.Strongest => 3,
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

            if (tier == EnemyStrengthTier.Strongest && count > 0)
            {
                for (int i = 0; i < count; i++)
                {
                    targets[i] = 3;
                }
            }

            ShuffleInPlace(targets, random);
            return targets;
        }

        private static bool RollMagicAllowed(EnemyStrengthTier tier, System.Random random)
        {
            int chance = tier switch
            {
                EnemyStrengthTier.Weak => 0,
                EnemyStrengthTier.Normal => 20,
                EnemyStrengthTier.Strong => 45,
                EnemyStrengthTier.VeryStrong => 70,
                EnemyStrengthTier.Strongest => 90,
                _ => 0
            };
            return chance > 0 && random.Next(0, 100) < chance;
        }

        private static MotionType? TryPickMagic(
            IReadOnlyList<MotionType> pool,
            List<MotionType> used,
            HashSet<BonePart> usedParts,
            bool preferUniquePart,
            System.Random random)
        {
            var candidates = new List<MotionType>();
            for (int i = 0; i < pool.Count; i++)
            {
                MotionType motion = pool[i];
                if (!ProceduralMotionCharacter.IsMagicAttack(motion) || used.Contains(motion))
                {
                    continue;
                }

                BonePart part = GetRequiredPart(motion);
                if (preferUniquePart && usedParts.Contains(part))
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

        private static MotionType? TryPick(
            IReadOnlyList<MotionType> pool,
            List<MotionType> used,
            HashSet<BonePart> usedParts,
            int preferredRank,
            bool preferUniquePart,
            bool allowMagic,
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

                if (ProceduralMotionCharacter.IsMagicAttack(motion) && !allowMagic)
                {
                    continue;
                }

                BonePart part = GetRequiredPart(motion);
                if (preferUniquePart
                    && usedParts.Contains(part)
                    && HasUnusedPartCandidate(pool, used, usedParts, allowMagic))
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
            bool allowMagic,
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

                if (ProceduralMotionCharacter.IsMagicAttack(motion) && !allowMagic)
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
            HashSet<BonePart> usedParts,
            bool allowMagic)
        {
            for (int i = 0; i < pool.Count; i++)
            {
                MotionType motion = pool[i];
                if (used.Contains(motion))
                {
                    continue;
                }

                if (ProceduralMotionCharacter.IsMagicAttack(motion) && !allowMagic)
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
