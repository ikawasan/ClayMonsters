using Battle.Interface;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 間合い調整・ふきとばし・技評価・とどめ狙いを行う標準敵AI
    /// </summary>
    public sealed class StandardBattleEnemyAi : IBattleEnemyAi
    {
        private readonly BattleEnemyAiProfile profile;
        private float attackCommitRemaining = -1f;
        private int pendingAttackMoveIndex = -1;

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
                ClearAttackCommit();
                return BattleEnemyAiDecision.Hold;
            }

            if (!context.Self.CanAct)
            {
                ClearAttackCommit();
                return BattleEnemyAiDecision.Hold;
            }

            int bestMove = SelectBestMove(context, out int secondBestMove);

            if (ShouldKnockback(context, bestMove))
            {
                ClearAttackCommit();
                return BattleEnemyAiDecision.Knockback;
            }

            if (ShouldRepair(context, bestMove))
            {
                ClearAttackCommit();
                return BattleEnemyAiDecision.Repair;
            }

            int movement = ResolveMovement(context, bestMove, secondBestMove);
            int stepIntent = ResolveStepIntent(context, movement, bestMove, secondBestMove);

            bool canAttackNow = context.AttackCooldownRemaining <= 0f
                && bestMove >= 0
                && context.Self.CanUseMove(bestMove, context.Distance, context.MaxDistance);

            int attackMoveIndex = -1;
            if (canAttackNow)
            {
                attackMoveIndex = UpdateAttackCommit(context, bestMove, secondBestMove);
                if (attackMoveIndex >= 0 || attackCommitRemaining >= 0f)
                {
                    movement = 0;
                    stepIntent = 0;
                }
            }
            else
            {
                ClearAttackCommit();
            }

            return new BattleEnemyAiDecision(movement, attackMoveIndex, stepIntent: stepIntent);
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

        private bool ShouldKnockback(BattleEnemyAiContext context, int bestMove)
        {
            if (!context.CanUseKnockback)
            {
                return false;
            }

            // とどめが取れる間合いなら先に攻撃を優先する
            if (bestMove >= 0
                && context.AttackCooldownRemaining <= 0f
                && context.Self.CanUseMove(bestMove, context.Distance, context.MaxDistance)
                && WouldFinish(context, context.Self.Moves[bestMove]))
            {
                return false;
            }

            float closeThreshold = Mathf.Max(
                context.Settings.KnockbackCloseThreshold,
                context.MaxDistance * 0.22f);
            bool tooClose = context.Distance <= closeThreshold;

            bool tooCloseForBestMove = false;
            if (bestMove >= 0)
            {
                AttackMove best = context.Self.Moves[bestMove];
                tooCloseForBestMove =
                    context.Distance < best.RangeMin - profile.KnockbackRangeInside;
            }

            bool escapePressure = context.SelfHpRatio <= profile.LowSelfHpAggression
                && context.Distance <= context.MaxDistance * profile.KnockbackEscapeDistanceRatio;

            // 全技が遠い射程のみで密着しているときも離す
            bool needsSpaceForAnyMove = !HasUsableMoveNearDistance(context)
                && context.Distance <= closeThreshold * 1.15f;

            if (!tooClose && !tooCloseForBestMove && !escapePressure && !needsSpaceForAnyMove)
            {
                return false;
            }

            // ガッツを次の技に温存するためふきとばし後に最低限残す
            float reserve = context.Self.MaxGuts * profile.GutsReserveRatio * 0.5f;
            if (!escapePressure
                && context.Self.Guts - context.Settings.KnockbackGutsCost < reserve
                && !tooCloseForBestMove)
            {
                return false;
            }

            // 緊急時は必ず使うそれ以外は確率
            if (escapePressure || tooCloseForBestMove)
            {
                return true;
            }

            return Random.value <= profile.KnockbackChance;
        }

        private bool HasUsableMoveNearDistance(BattleEnemyAiContext context)
        {
            for (int i = 0; i < context.Self.Moves.Count; i++)
            {
                if (!context.Self.IsMoveUsableByPart(i))
                {
                    continue;
                }

                AttackMove move = context.Self.Moves[i];
                if (context.Distance >= move.RangeMin - profile.ApproachMargin
                    && context.Distance <= move.RangeMax + profile.RetreatMargin)
                {
                    return true;
                }
            }

            return false;
        }

        private int UpdateAttackCommit(
            BattleEnemyAiContext context,
            int bestMove,
            int secondBestMove)
        {
            if (attackCommitRemaining < 0f)
            {
                int desiredMove = MaybePickAlternateMove(bestMove, secondBestMove);
                if (desiredMove < 0 || !context.Self.CanUseMove(desiredMove, context.Distance, context.MaxDistance))
                {
                    ClearAttackCommit();
                    return -1;
                }

                pendingAttackMoveIndex = desiredMove;
                float min = profile.AttackCommitDelayMin;
                float max = profile.AttackCommitDelayMax;
                attackCommitRemaining = min >= max
                    ? min
                    : Random.Range(min, max);
            }
            else if (pendingAttackMoveIndex < 0
                || !context.Self.CanUseMove(pendingAttackMoveIndex, context.Distance, context.MaxDistance))
            {
                ClearAttackCommit();
                return -1;
            }

            attackCommitRemaining -= context.DeltaTime;
            if (attackCommitRemaining > 0f)
            {
                return -1;
            }

            int committedMove = pendingAttackMoveIndex;
            ClearAttackCommit();
            return committedMove;
        }

        private void ClearAttackCommit()
        {
            attackCommitRemaining = -1f;
            pendingAttackMoveIndex = -1;
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
                && context.Self.CanUseMove(bestMove, context.Distance, context.MaxDistance);
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

            float hitRate = context.Self.GetHitRate(moveIndex, context.Opponent.Speed);
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

            // 帯の中央へ微調整する強敵ほど補正帯が狭い
            float center = (move.RangeMin + move.RangeMax) * 0.5f;
            float halfSpan = Mathf.Max(0.25f, (move.RangeMax - move.RangeMin) * 0.5f);
            float deadzone = Mathf.Clamp(
                Mathf.Max(profile.ApproachMargin, profile.RetreatMargin) * 1.4f,
                0.12f,
                halfSpan * 0.85f);
            if (context.Distance > center + deadzone)
            {
                return -1;
            }

            if (context.Distance < center - deadzone)
            {
                return 1;
            }

            if (context.SelfHpRatio <= profile.LowSelfHpAggression
                && context.Distance < move.RangeMin + profile.RetreatMargin)
            {
                return 1;
            }

            return 0;
        }

        private int ResolveStepIntent(
            BattleEnemyAiContext context,
            int walkIntent,
            int bestMove,
            int secondBestMove)
        {
            if (context.StepCooldownRemaining > 0f || walkIntent == 0)
            {
                return 0;
            }

            int targetMove = bestMove >= 0 ? bestMove : secondBestMove;
            float gap;
            if (targetMove >= 0)
            {
                AttackMove move = context.Self.Moves[targetMove];
                gap = ComputeRangeGap(move.RangeMin, move.RangeMax, context.Distance);
            }
            else
            {
                gap = Mathf.Abs(context.Distance - context.MaxDistance * 0.5f);
            }

            if (gap < profile.StepGapThreshold)
            {
                return 0;
            }

            return walkIntent;
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
                // 技が無いときは安全距離へ下がる/寄る
                float preferred = context.MaxDistance * 0.55f;
                if (context.Distance > preferred + 0.4f)
                {
                    return -1;
                }

                if (context.Distance < preferred - 0.4f)
                {
                    return 1;
                }

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
