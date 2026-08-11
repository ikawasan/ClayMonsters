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

        // 直前の移動意図(待機と微小な接近後退の往復を抑える)
        private int stickyMovementIntent;

        // 直前に狙った技(射程目標のフレーム単位の揺れを抑える)
        private int stickyTargetMoveIndex = -1;

        /// <summary>
        /// 接近から停止へ切り替えるとき帯からこの分だけ内側まで進む
        /// </summary>
        private const float MovementReleasePadding = 0.45f;

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
                stickyMovementIntent = 0;
                stickyTargetMoveIndex = -1;
                return BattleEnemyAiDecision.Hold;
            }

            if (!context.Self.CanAct)
            {
                ClearAttackCommit();
                stickyMovementIntent = 0;
                return BattleEnemyAiDecision.Hold;
            }

            int bestMove = SelectBestMove(context, out int secondBestMove);
            bestMove = StabilizeTargetMove(context, bestMove);

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
                    // 攻撃待ち中は歩きを止めるが間合い意図のstickyは維持する
                    movement = 0;
                    stepIntent = 0;
                    return new BattleEnemyAiDecision(movement, attackMoveIndex, stepIntent: stepIntent);
                }
            }
            else
            {
                ClearAttackCommit();
            }

            stickyMovementIntent = movement;
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

        private int StabilizeTargetMove(BattleEnemyAiContext context, int bestMove)
        {
            if (bestMove < 0)
            {
                stickyTargetMoveIndex = -1;
                return -1;
            }

            if (stickyTargetMoveIndex < 0
                || stickyTargetMoveIndex >= context.Self.Moves.Count
                || !context.Self.IsMoveUsableByPart(stickyTargetMoveIndex))
            {
                stickyTargetMoveIndex = bestMove;
                return bestMove;
            }

            if (stickyTargetMoveIndex == bestMove)
            {
                return bestMove;
            }

            // 現状の間合いにまだ効く技は少し粘り射程目標の瞬時切替を抑える
            AttackMove stickyMove = context.Self.Moves[stickyTargetMoveIndex];
            float stickyGap = ComputeRangeGap(
                stickyMove.RangeMin,
                stickyMove.RangeMax,
                context.Distance);
            AttackMove best = context.Self.Moves[bestMove];
            float bestGap = ComputeRangeGap(best.RangeMin, best.RangeMax, context.Distance);

            float keepBias = Mathf.Max(0.55f, profile.ApproachMargin + profile.RetreatMargin);
            if (stickyGap <= bestGap + keepBias)
            {
                return stickyTargetMoveIndex;
            }

            stickyTargetMoveIndex = bestMove;
            return bestMove;
        }

        private int ResolveMovement(BattleEnemyAiContext context, int bestMove, int secondBestMove)
        {
            int targetMove = bestMove >= 0 ? bestMove : secondBestMove;
            if (targetMove < 0)
            {
                return ApplyMovementHysteresis(
                    ResolveFallbackMovement(context),
                    context.Distance,
                    preferredMin: context.MaxDistance * 0.45f,
                    preferredMax: context.MaxDistance * 0.65f);
            }

            AttackMove move = context.Self.Moves[targetMove];
            int desired = ResolveDesiredMovementForMove(context, move);
            return ApplyMovementHysteresis(
                desired,
                context.Distance,
                preferredMin: move.RangeMin,
                preferredMax: move.RangeMax);
        }

        private int ResolveDesiredMovementForMove(BattleEnemyAiContext context, AttackMove move)
        {
            // 射程外へ出たときだけ寄る/下がる(帯内の中央寄せはしない)
            if (context.Distance > move.RangeMax + profile.ApproachMargin)
            {
                return -1;
            }

            if (context.Distance < move.RangeMin - profile.RetreatMargin)
            {
                return 1;
            }

            return 0;
        }

        private int ApplyMovementHysteresis(
            int desired,
            float distance,
            float preferredMin,
            float preferredMax)
        {
            // いったん動き始めたら余裕分だけ内側に入るまで同じ方向を維持する
            float releasePad = Mathf.Max(
                MovementReleasePadding,
                Mathf.Max(profile.ApproachMargin, profile.RetreatMargin) * 1.25f);

            if (stickyMovementIntent < 0)
            {
                float releaseDistance = preferredMax - releasePad;
                if (distance > Mathf.Max(preferredMin, releaseDistance))
                {
                    return -1;
                }
            }
            else if (stickyMovementIntent > 0)
            {
                float releaseDistance = preferredMin + releasePad;
                if (distance < Mathf.Min(preferredMax, releaseDistance))
                {
                    return 1;
                }
            }

            // 逆方向へいきなり切り替えるのは明確な帯外のみ
            if (desired != 0
                && stickyMovementIntent != 0
                && desired != stickyMovementIntent)
            {
                if (stickyMovementIntent < 0
                    && distance > preferredMax + profile.ApproachMargin * 0.5f)
                {
                    return -1;
                }

                if (stickyMovementIntent > 0
                    && distance < preferredMin - profile.RetreatMargin * 0.5f)
                {
                    return 1;
                }
            }

            return desired;
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

            // 通常歩行で足りる小さなズレはステップしない(細かいステップ連打を避ける)
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
                float preferred = context.MaxDistance * 0.55f;
                float deadzone = 0.55f;
                if (context.Distance > preferred + deadzone)
                {
                    return -1;
                }

                if (context.Distance < preferred - deadzone)
                {
                    return 1;
                }

                return 0;
            }

            AttackMove fallback = context.Self.Moves[closestMove];
            return ResolveDesiredMovementForMove(context, fallback);
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
