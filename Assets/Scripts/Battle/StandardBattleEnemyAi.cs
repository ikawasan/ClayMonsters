using Battle.Interface;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 間合い調整・技評価・とどめ狙いを行う標準敵AI
    /// </summary>
    public sealed class StandardBattleEnemyAi : IBattleEnemyAi
    {
        private readonly BattleEnemyAiProfile profile;

        /// <summary>
        /// プロファイルを指定して敵AIを生成する
        /// </summary>
        public StandardBattleEnemyAi(BattleEnemyAiProfile profile = null)
        {
            this.profile = profile ?? BattleEnemyAiProfile.Default;
        }

        /// <inheritdoc/>
        public BattleEnemyAiDecision Decide(BattleEnemyAiContext context)
        {
            if (context.Self == null || context.Opponent == null)
            {
                return BattleEnemyAiDecision.Hold;
            }

            if (!context.Self.CanAct)
            {
                return BattleEnemyAiDecision.Hold;
            }

            int bestMove = SelectBestMove(context, out int secondBestMove);

            if (ShouldRepair(context, bestMove))
            {
                return BattleEnemyAiDecision.Repair;
            }

            int movement = ResolveMovement(context, bestMove, secondBestMove);

            int attackMoveIndex = -1;
            if (context.AttackCooldownRemaining <= 0f
                && bestMove >= 0
                && context.Self.CanUseMove(bestMove, context.Distance))
            {
                attackMoveIndex = MaybePickAlternateMove(bestMove, secondBestMove);
                movement = 0;
            }

            return new BattleEnemyAiDecision(movement, attackMoveIndex);
        }

        /// <inheritdoc/>
        public int ConsumeRemoteStepIntent() => 0;

        /// <inheritdoc/>
        public bool TryConsumeNetworkAttackStart(out int moveIndex, out int attackSequence, out bool isCounter)
        {
            moveIndex = -1;
            attackSequence = 0;
            isCounter = false;
            return false;
        }

        // 欠損部位があり安全なときや攻撃手段が無いときは修復を優先する
        private bool ShouldRepair(BattleEnemyAiContext context, int bestMove)
        {
            if (context.Self.LostPartCount <= 0)
            {
                return false;
            }

            if (context.IsOpponentPerformingAttack)
            {
                return false;
            }

            bool canAttackNow = bestMove >= 0
                && context.AttackCooldownRemaining <= 0f
                && context.Self.CanUseMove(bestMove, context.Distance);
            if (canAttackNow)
            {
                return false;
            }

            bool safeDistance = context.Distance >= context.MaxDistance * profile.RepairSafeDistanceRatio;
            bool noUsableMove = bestMove < 0;
            return safeDistance || noUsableMove;
        }

        private int SelectBestMove(BattleEnemyAiContext context, out int secondBestMove)
        {
            secondBestMove = -1;
            int bestMove = -1;
            float bestScore = float.MinValue;
            float secondScore = float.MinValue;

            for (int i = 0; i < context.Self.Moves.Count; i++)
            {
                if (!context.Self.IsMoveUsableByPart(i))
                {
                    continue;
                }

                float score = ScoreMove(context, i);
                if (score <= 0f)
                {
                    continue;
                }

                if (score > bestScore)
                {
                    secondScore = bestScore;
                    secondBestMove = bestMove;
                    bestScore = score;
                    bestMove = i;
                }
                else if (score > secondScore)
                {
                    secondScore = score;
                    secondBestMove = i;
                }
            }

            return bestMove;
        }

        private float ScoreMove(BattleEnemyAiContext context, int moveIndex)
        {
            AttackMove move = context.Self.Moves[moveIndex];
            float reserve = context.Self.MaxGuts * profile.GutsReserveRatio;
            if (context.Self.Guts < move.GutsCost && !WouldFinish(context, move))
            {
                return 0f;
            }

            if (!WouldFinish(context, move) && context.Self.Guts - move.GutsCost < reserve)
            {
                return 0f;
            }

            float hitRate = context.Self.GetHitRate(moveIndex);
            float rangeFit = ComputeRangeFit(move.RangeMin, move.RangeMax, context.Distance);
            float score = profile.PowerWeight * move.Power
                + profile.HitRateWeight * hitRate
                + profile.RangeFitWeight * rangeFit;

            if (WouldFinish(context, move))
            {
                score += profile.FinisherWeight;
            }

            if (context.OpponentHpRatio <= 0.35f)
            {
                score += profile.LowOpponentHpWeight * (1f - context.OpponentHpRatio);
            }

            if (context.SelfHpRatio <= profile.LowSelfHpAggression)
            {
                score += move.RangeMax >= context.Distance ? 0.15f : -0.1f;
            }

            score += Random.Range(-profile.AttackRandomness, profile.AttackRandomness);
            return score;
        }

        private int MaybePickAlternateMove(int bestMove, int secondBestMove)
        {
            if (secondBestMove < 0)
            {
                return bestMove;
            }

            return Random.value < profile.SecondBestMoveChance ? secondBestMove : bestMove;
        }

        private int ResolveMovement(BattleEnemyAiContext context, int bestMove, int secondBestMove)
        {
            int targetMove = bestMove >= 0 ? bestMove : secondBestMove;
            if (targetMove < 0)
            {
                return ResolveFallbackMovement(context);
            }

            AttackMove move = context.Self.Moves[targetMove];
            if (context.Distance > move.RangeMax + profile.ApproachMargin)
            {
                return -1;
            }

            if (context.Distance < move.RangeMin - profile.RetreatMargin)
            {
                return 1;
            }

            if (context.SelfHpRatio <= profile.LowSelfHpAggression && move.RangeMax < context.Distance * 0.8f)
            {
                return 1;
            }

            return 0;
        }

        private int ResolveFallbackMovement(BattleEnemyAiContext context)
        {
            int closestMove = -1;
            float closestGap = float.MaxValue;

            for (int i = 0; i < context.Self.Moves.Count; i++)
            {
                if (!context.Self.IsMoveUsableByPart(i))
                {
                    continue;
                }

                AttackMove move = context.Self.Moves[i];
                float gap = ComputeRangeGap(move.RangeMin, move.RangeMax, context.Distance);
                if (gap < closestGap)
                {
                    closestGap = gap;
                    closestMove = i;
                }
            }

            if (closestMove < 0)
            {
                return 0;
            }

            AttackMove fallback = context.Self.Moves[closestMove];
            if (context.Distance > fallback.RangeMax)
            {
                return -1;
            }

            if (context.Distance < fallback.RangeMin)
            {
                return 1;
            }

            return 0;
        }

        private bool WouldFinish(BattleEnemyAiContext context, AttackMove move)
        {
            float raw = context.Self.Attack
                * move.Power
                * context.Settings.DamageScale
                * (100f / (100f + context.Opponent.Defense));
            int estimatedDamage = Mathf.Max(1, Mathf.RoundToInt(raw));
            return estimatedDamage >= context.Opponent.CurrentHp;
        }

        private static float ComputeRangeFit(float rangeMin, float rangeMax, float distance)
        {
            if (distance >= rangeMin && distance <= rangeMax)
            {
                float center = (rangeMin + rangeMax) * 0.5f;
                float halfSpan = Mathf.Max(0.1f, (rangeMax - rangeMin) * 0.5f);
                return 1f - Mathf.Clamp01(Mathf.Abs(distance - center) / halfSpan) * 0.25f;
            }

            if (distance < rangeMin)
            {
                return Mathf.Clamp01(1f - (rangeMin - distance) / 2.5f) * 0.45f;
            }

            return Mathf.Clamp01(1f - (distance - rangeMax) / 3.5f) * 0.45f;
        }

        private static float ComputeRangeGap(float rangeMin, float rangeMax, float distance)
        {
            if (distance < rangeMin)
            {
                return rangeMin - distance;
            }

            if (distance > rangeMax)
            {
                return distance - rangeMax;
            }

            return 0f;
        }
    }
}
