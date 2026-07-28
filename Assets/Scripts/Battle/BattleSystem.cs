using System;
using System.Collections.Generic;
using System.Threading;
using Battle.Interface;
using ClayEditor.Rigging;
using Cysharp.Threading.Tasks;
using Extensions;
using R3;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 戦闘の設定値(間合い・制限時間など)
    /// </summary>
    public struct BattleSettings
    {
        public float MaxDistance;
        public float TimeLimit;
        public float InitialGuts;
        public float KnockbackCloseThreshold;
        public float KnockbackPushDistance;
        public float KnockbackGutsCost;
        public float KnockbackRecovery;
        public float ChainBonusWindow;
        public float EnemyAttackTelegraphDuration;
        public float EnemyAttackCooldownMin;
        public float EnemyAttackCooldownMax;
        public float CounterDamageMultiplier;
        public float DamageScale;
        public float StepDistance;
        public float StepDuration;
        public float StepCooldown;
        public float MinCloseSeparation;
        public float PartRepairSecondsPerLimb;
        public float PostAttackLockoutDuration;
    }

    /// <summary>
    /// 技を使用した結果(命中可否・ダメージ・欠損)
    /// </summary>
    public readonly struct MoveUsedResult
    {
        public MoveUsedResult(
            BattleUnit attacker,
            BattleUnit target,
            AttackMove move,
            bool hit,
            int damage,
            bool partLost,
            BonePart lostPart,
            bool isKnockout,
            int lostLimbIndex = -1)
        {
            Attacker = attacker;
            Target = target;
            Move = move;
            Hit = hit;
            Damage = damage;
            PartLost = partLost;
            LostPart = lostPart;
            IsKnockout = isKnockout;
            LostLimbIndex = lostLimbIndex;
        }

        public BattleUnit Attacker { get; }
        public BattleUnit Target { get; }
        public AttackMove Move { get; }
        public bool Hit { get; }
        public int Damage { get; }
        public bool PartLost { get; }
        public BonePart LostPart { get; }
        public bool IsKnockout { get; }
        public int LostLimbIndex { get; }
    }

    /// <summary>
    /// 間合いとガッツを管理して技を出す・リアルタイム1対1戦闘
    /// </summary>
    public sealed class BattleSystem
    {
        private const float DistanceSyncInterval = 0.2f;

        private readonly BattleUnit player;
        private readonly BattleUnit enemy;
        private readonly BattleSettings settings;
        private readonly IBattleMovementInput playerMovementInput;
        private readonly IBattlePlayerInput playerCombatInput;

        private readonly Subject<Unit> updatedSubject = new Subject<Unit>();
        private readonly Subject<MoveUsedResult> moveUsedSubject = new Subject<MoveUsedResult>();
        private readonly Subject<AttackWindUpStarted> attackWindUpStartedSubject = new Subject<AttackWindUpStarted>();
        private readonly Subject<AttackProjectileStarted> attackProjectileStartedSubject = new Subject<AttackProjectileStarted>();
        private readonly Subject<KnockbackPerformed> knockbackPerformedSubject = new Subject<KnockbackPerformed>();
        private readonly Subject<BattleUnit> battleEndSubject = new Subject<BattleUnit>();

        private BattleUnit pendingAttackAttacker;
        private BattleUnit pendingAttackTarget;
        private AttackMove pendingAttackMove;
        private int pendingAttackMoveIndex = -1;
        private float pendingAttackWindUpRemaining;
        private float pendingProjectileFlightRemaining;
        private bool pendingProjectileReplayMotionSkipped;
        private float pendingAttackPowerMultiplier = 1f;
        private bool pendingAttackIsCounter;
        private int pendingAttackSequence;
        private float attackLockoutRemaining;
        private float enemyAttackCooldownRemaining;
        private int lastPlayerMoveIndex = -1;
        private float chainWindowRemaining;
        private int playerChainCount = 1;
        private int playerStepMovementIntent;
        private float playerStepCooldownRemaining;
        private bool isPlayerStepping;
        private float playerStepStartDistance;
        private float playerStepTargetDistance;
        private float playerStepElapsed;
        private int enemyStepMovementIntent;
        private float enemyStepCooldownRemaining;
        private bool isEnemyStepping;
        private float enemyStepStartDistance;
        private float enemyStepTargetDistance;
        private float enemyStepElapsed;
        private float partRepairProgress;
        private int repairingLimbIndex = -1;
        private bool isPartRepairVisualActive;
        private float enemyPartRepairProgress;
        private int enemyRepairingLimbIndex = -1;
        private bool isEnemyPartRepairVisualActive;
        private readonly IBattleEnemyAi enemyAi;
        private readonly IBattlePvpCombatSync combatSync;
        private int lastRemoteEnemyMoveIndex = -1;
        private BattleRemoteStrikePayload pendingFailedRemoteStrike;
        private bool hasPendingFailedRemoteStrike;
        private int deferredEnemyAttackSequence;
        private bool deferredEnemyAttackConsumed;
        private float distanceSyncRemaining;
        private readonly HashSet<int> cancelledAttackSequences = new HashSet<int>();
        private IBattleFieldBoundary fieldBoundary;
        private IBattleFieldMovement fieldMovement;

        public BattleSystem(
            BattleUnit player,
            BattleUnit enemy,
            BattleSettings settings,
            IBattleMovementInput playerMovementInput = null,
            IBattleEnemyAi enemyAi = null,
            IBattlePvpCombatSync combatSync = null)
        {
            this.player = player;
            this.enemy = enemy;
            this.settings = settings;
            this.playerMovementInput = playerMovementInput;
            this.playerCombatInput = playerMovementInput as IBattlePlayerInput;
            this.enemyAi = enemyAi ?? new StandardBattleEnemyAi();
            this.combatSync = combatSync;

            Distance = settings.MaxDistance;
            TimeRemaining = settings.TimeLimit;
            player.InitializeBattleGuts(settings.InitialGuts);
            enemy.InitializeBattleGuts(settings.InitialGuts);
            player.SuspendPartLossRebuild();
            enemy.SuspendPartLossRebuild();
        }

        /// <summary>
        /// フィールド境界判定を接続する
        /// </summary>
        public void SetFieldBoundary(IBattleFieldBoundary boundary)
        {
            fieldBoundary = boundary;
            fieldMovement = boundary as IBattleFieldMovement;
        }

        /// <summary>
        /// プレイヤー側ユニット
        /// </summary>
        public BattleUnit Player => player;

        /// <summary>
        /// 敵側ユニット
        /// </summary>
        public BattleUnit Enemy => enemy;

        /// <summary>
        /// 現在の間合い(0=密着〜MaxDistance)
        /// </summary>
        public float Distance { get; private set; }

        /// <summary>
        /// 最大間合い
        /// </summary>
        public float MaxDistance => settings.MaxDistance;

        /// <summary>
        /// 残り時間(秒)
        /// </summary>
        public float TimeRemaining { get; private set; }

        /// <summary>
        /// 決着済みか
        /// </summary>
        public bool IsFinished { get; private set; }

        /// <summary>
        /// 直近の決着理由KOか時間切れのHP判定か
        /// </summary>
        public BattleEndReason EndReason { get; private set; }

        /// <summary>
        /// 現在の距離帯
        /// </summary>
        public BattleDistanceBand CurrentDistanceBand => BattleDistanceBandResolver.Resolve(Distance, MaxDistance);

        /// <summary>
        /// ふきとばしが使えるか
        /// </summary>
        public bool IsKnockbackAvailable =>
            Distance < settings.MaxDistance - 1e-3f
            && player.CanAct
            && player.Guts >= settings.KnockbackGutsCost;

        /// <summary>
        /// プレイヤーのチェーン数
        /// </summary>
        public int PlayerChainCount => playerChainCount;

        /// <summary>
        /// カウンター受付中か
        /// </summary>
        public bool IsCounterWindowOpen =>
            pendingAttackAttacker == enemy
            && pendingAttackWindUpRemaining > 0f
            && !pendingAttackIsCounter
            && pendingAttackMove != null
            && pendingAttackMove.TargetDestroyPart != BonePart.Body;

        /// <summary>
        /// カウンター対象の敵技番号
        /// </summary>
        public int PendingEnemyMoveIndex =>
            pendingAttackAttacker == enemy ? pendingAttackMoveIndex : -1;

        /// <summary>
        /// カウンター対象の敵技の破壊部位
        /// </summary>
        public BonePart PendingEnemyTargetDestroyPart =>
            pendingAttackAttacker == enemy && pendingAttackMove != null
                ? pendingAttackMove.TargetDestroyPart
                : BonePart.Body;

        /// <summary>
        /// 敵が攻撃中か(溜めまたは投射飛行または攻撃硬直)
        /// </summary>
        public bool IsEnemyPerformingAttack =>
            (pendingAttackAttacker == enemy
                && (pendingAttackWindUpRemaining > 0f || pendingProjectileFlightRemaining > 0f))
            || enemy.IsPerformingAttack;

        /// <summary>
        /// プレイヤーが攻撃中か(溜めまたは投射飛行または攻撃硬直)
        /// </summary>
        public bool IsPlayerPerformingAttack =>
            (pendingAttackAttacker == player
                && (pendingAttackWindUpRemaining > 0f || pendingProjectileFlightRemaining > 0f))
            || player.IsPerformingAttack;

        /// <summary>
        /// 攻撃演出中か(双方の溜めまたは攻撃モーション)
        /// </summary>
        public bool IsAttackPresentationActive =>
            IsPlayerPerformingAttack || IsEnemyPerformingAttack;

        /// <summary>
        /// プレイヤーがステップ移動中か
        /// </summary>
        public bool IsPlayerStepping => isPlayerStepping;

        /// <summary>
        /// ステップ中の移動意図(-1=接近・+1=後退)未ステップ時は0
        /// </summary>
        public int PlayerStepMovementIntent => isPlayerStepping ? playerStepMovementIntent : 0;

        /// <summary>
        /// 敵がステップ移動中か
        /// </summary>
        public bool IsEnemyStepping => isEnemyStepping;

        /// <summary>
        /// 敵ステップ中の移動意図(-1=接近・+1=後退)未ステップ時は0
        /// </summary>
        public int EnemyStepMovementIntent => isEnemyStepping ? enemyStepMovementIntent : 0;

        /// <summary>
        /// 攻撃後の双方攻撃不可時間が残っているか
        /// </summary>
        public bool IsAttackLockoutActive => attackLockoutRemaining > 0f;

        /// <summary>
        /// 指定側の技が現在使用可能か(相手攻撃中の制限を含む)
        /// </summary>
        public bool IsMoveUsableForUnit(bool isPlayerUnit, int moveIndex)
        {
            BattleUnit unit = isPlayerUnit ? player : enemy;
            if (moveIndex < 0 || moveIndex >= unit.Moves.Count)
            {
                return false;
            }

            bool isCounterMove = isPlayerUnit && IsPlayerCounterMove(moveIndex);
            if (attackLockoutRemaining > 0f && !isCounterMove)
            {
                return false;
            }

            if (isPlayerUnit)
            {
                if (isCounterMove)
                {
                    AttackMove counterMove = unit.Moves[moveIndex];
                    return unit.CanAct
                        && unit.IsMoveUsableByPart(moveIndex)
                        && unit.Guts >= counterMove.GutsCost;
                }

                if (pendingAttackAttacker == player)
                {
                    return false;
                }

                if (IsEnemyPerformingAttack)
                {
                    return false;
                }

                return unit.CanUseMove(moveIndex, Distance);
            }

            if (IsPlayerPerformingAttack)
            {
                return false;
            }

            return unit.CanUseMove(moveIndex, Distance);
        }

        /// <summary>
        /// 毎フレームの状態更新通知(UIの連続更新用)
        /// </summary>
        public Observable<Unit> OnUpdated => updatedSubject;

        /// <summary>
        /// 技が使われた通知(ログ・演出用)
        /// </summary>
        public Observable<MoveUsedResult> OnMoveUsed => moveUsedSubject;

        /// <summary>
        /// 攻撃の溜めが始まった通知(カメラ演出用)
        /// </summary>
        public Observable<AttackWindUpStarted> OnAttackWindUpStarted => attackWindUpStartedSubject;

        /// <summary>
        /// 投射魔法の飛翔開始通知
        /// </summary>
        public Observable<AttackProjectileStarted> OnAttackProjectileStarted => attackProjectileStartedSubject;

        /// <summary>
        /// ふきとばしが成立した通知
        /// </summary>
        public Observable<KnockbackPerformed> OnKnockbackPerformed => knockbackPerformedSubject;

        /// <summary>
        /// 決着通知(勝者引き分け時はnull)
        /// </summary>
        public Observable<BattleUnit> OnBattleEnd => battleEndSubject;

        /// <summary>
        /// プレイヤーの移動意図を設定する(-1=接近,0=停止,+1=後退)
        /// </summary>
        public void SetPlayerMovement(int sign)
        {
            player.MovementIntent = Mathf.Clamp(sign, -1, 1);
        }

        /// <summary>
        /// プレイヤーが技を使用する使用可能なら実行してtrueを返す
        /// </summary>
        public bool TryUsePlayerMove(int moveIndex)
        {
            if (IsFinished)
            {
                return false;
            }

            if (IsPlayerCounterMove(moveIndex))
            {
                if (!player.CanAct || !player.IsMoveUsableByPart(moveIndex))
                {
                    return false;
                }

                AttackMove counterMove = player.Moves[moveIndex];
                if (player.Guts < counterMove.GutsCost)
                {
                    return false;
                }

                ResolveCounter(moveIndex);
                return true;
            }

            if (attackLockoutRemaining > 0f)
            {
                return false;
            }

            if (!IsCounterWindowOpen && IsEnemyPerformingAttack)
            {
                return false;
            }

            if (!player.CanUseMove(moveIndex, Distance))
            {
                return false;
            }

            ExecutePlayerMove(moveIndex);
            return true;
        }

        /// <summary>
        /// 戦闘ループを実行する決着するまで毎フレーム進行する
        /// </summary>
        public async UniTask RunAsync(CancellationToken cancellationToken)
        {
            player.UpdateLocomotionMotion();
            enemy.UpdateLocomotionMotion();

            while (!IsFinished && !cancellationToken.IsCancellationRequested)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                Tick(GameplayTime.DeltaTime);
            }

            await BattleHitStopClock.WaitUntilFinishedAsync(cancellationToken);
            BattleHitStopClock.Clear();
        }

        private void BeginHitFeedback(bool partLost)
        {
            if (partLost)
            {
                BattleHitStopClock.RequestSlowMotion();
                return;
            }

            BattleHitStopClock.RequestHitStop();
        }

        private void BeginFinishFeedback()
        {
            BattleHitStopClock.RequestFinishSlowMotion();
        }

        private void ClearHitStop()
        {
            BattleHitStopClock.Clear();
        }

        // 1フレーム分の進行
        private void Tick(float deltaTime)
        {
            BattleHitStopClock.Tick(Time.unscaledDeltaTime);

            if (IsFinished)
            {
                return;
            }

            if (GameplayTime.IsPaused)
            {
                return;
            }

            if (playerMovementInput != null)
            {
                playerMovementInput.RefreshInput();
                if (!isPlayerStepping && !IsAttackPresentationActive && player.CanAct)
                {
                    SetPlayerMovement(playerMovementInput.MovementIntent);
                }
                else
                {
                    SetPlayerMovement(0);
                }
            }

            if (playerCombatInput != null)
            {
                if (!isPlayerStepping && !IsAttackPresentationActive && player.CanAct)
                {
                    int stepIntent = playerCombatInput.ConsumeStepIntent();
                    if (stepIntent != 0)
                    {
                        SetPlayerMovement(0);
                        TryBeginPlayerStep(stepIntent);
                    }
                }
                else
                {
                    playerCombatInput.ConsumeStepIntent();
                }

                int attackIndex = playerCombatInput.ConsumeAttackMoveIndex();
                if (attackIndex >= 0)
                {
                    TryUsePlayerMove(attackIndex);
                }
            }

            TryKnockback();
            UpdatePartRepair(deltaTime);

            if (chainWindowRemaining > 0f)
            {
                chainWindowRemaining = Mathf.Max(0f, chainWindowRemaining - deltaTime);
            }

            if (playerStepCooldownRemaining > 0f)
            {
                playerStepCooldownRemaining = Mathf.Max(0f, playerStepCooldownRemaining - deltaTime);
            }

            if (enemyAttackCooldownRemaining > 0f)
            {
                enemyAttackCooldownRemaining = Mathf.Max(0f, enemyAttackCooldownRemaining - deltaTime);
            }

            if (attackLockoutRemaining > 0f && !IsAttackPresentationActive)
            {
                attackLockoutRemaining = Mathf.Max(0f, attackLockoutRemaining - deltaTime);
            }

            if (enemyStepCooldownRemaining > 0f)
            {
                enemyStepCooldownRemaining = Mathf.Max(0f, enemyStepCooldownRemaining - deltaTime);
            }

            TimeRemaining -= deltaTime;

            bool gainGuts = !IsPlayerPerformingAttack && !IsEnemyPerformingAttack;
            player.Tick(deltaTime, gainGuts);
            enemy.Tick(deltaTime, gainGuts);

            // カウンター無効化を溜め解決より先に反映する
            ProcessRemoteCombatSync();
            // 同期攻撃開始はローカル攻撃演出中でも取り込む(カウンター迎撃のため)
            TryBeginNetworkSyncedEnemyAttack();
            UpdateAttackWindUp(deltaTime);
            if (IsFinished)
            {
                return;
            }

            if (IsAttackPresentationActive)
            {
                player.MovementIntent = 0;
                enemy.MovementIntent = 0;
                PausePartRepair();
                PauseEnemyPartRepair();
            }
            else
            {
                UpdateEnemyAi(deltaTime);
            }
            if (IsFinished)
            {
                return;
            }

            if (TimeRemaining <= 0f)
            {
                TimeRemaining = 0f;
                FinishByHp(BattleEndReason.TimeUp);
                return;
            }

            UpdatePlayerStep(deltaTime);
            UpdateEnemyStep(deltaTime);
            if (!isPlayerStepping && !isEnemyStepping)
            {
                UpdateDistance(deltaTime);
            }

            UpdateDistanceAuthoritySync(deltaTime);
            player.UpdateLocomotionMotion();
            enemy.UpdateLocomotionMotion();
            updatedSubject.OnNext(Unit.Default);
        }

        // 双方の移動意図で間合いを更新する
        private void UpdateDistance(float deltaTime)
        {
            int playerIntent = player.CanAct ? player.MovementIntent : 0;
            int enemyIntent = enemy.CanAct ? enemy.MovementIntent : 0;

            if (Distance >= settings.MaxDistance - 1e-4f)
            {
                if (playerIntent > 0)
                {
                    playerIntent = 0;
                }

                if (enemyIntent > 0)
                {
                    enemyIntent = 0;
                }
            }

            if (Distance <= 1e-4f)
            {
                if (playerIntent < 0)
                {
                    playerIntent = 0;
                }

                if (enemyIntent < 0)
                {
                    enemyIntent = 0;
                }
            }

            if (fieldMovement != null)
            {
                fieldMovement.ApplyIndependentMovement(
                    deltaTime,
                    playerIntent,
                    enemyIntent,
                    player.MoveSpeed,
                    enemy.MoveSpeed,
                    settings.MaxDistance,
                    out float newDistance);
                Distance = Mathf.Clamp(newDistance, 0f, settings.MaxDistance);
                return;
            }

            if (playerIntent > 0 && fieldBoundary != null && fieldBoundary.IsPlayerAtHomeBoundary)
            {
                playerIntent = 0;
            }

            if (enemyIntent > 0 && fieldBoundary != null && fieldBoundary.IsEnemyAtHomeBoundary)
            {
                enemyIntent = 0;
            }

            float delta =
                (playerIntent * player.MoveSpeed
                 + enemyIntent * enemy.MoveSpeed)
                * deltaTime;
            Distance = Mathf.Clamp(Distance + delta, 0f, settings.MaxDistance);
        }

        // 敵AI:間合い調整と技選択と部位修復をIBattleEnemyAiに委譲する
        private void UpdateEnemyAi(float deltaTime)
        {
            if (TryBeginNetworkSyncedEnemyAttack())
            {
                return;
            }

            if (IsPlayerPerformingAttack || pendingAttackAttacker != null)
            {
                enemy.MovementIntent = 0;
                PauseEnemyPartRepair();
                return;
            }

            if (isEnemyStepping)
            {
                enemy.MovementIntent = 0;
                return;
            }

            if (!enemy.CanAct)
            {
                enemy.MovementIntent = 0;
                PauseEnemyPartRepair();
                return;
            }

            var context = new BattleEnemyAiContext(
                enemy,
                player,
                Distance,
                settings.MaxDistance,
                TimeRemaining,
                IsPlayerPerformingAttack,
                enemyAttackCooldownRemaining,
                settings);

            BattleEnemyAiDecision decision = enemyAi.Decide(context);

            if (decision.WantsRepair)
            {
                enemy.MovementIntent = 0;
                UpdateEnemyPartRepair(deltaTime);
                return;
            }

            PauseEnemyPartRepair();
            if (decision.StepIntent != 0)
            {
                TryBeginEnemyStep(decision.StepIntent);
                return;
            }

            enemy.MovementIntent = decision.MovementIntent;

            if (decision.AttackMoveIndex < 0
                || enemyAttackCooldownRemaining > 0f
                || attackLockoutRemaining > 0f)
            {
                return;
            }

            if (!enemy.CanUseMove(decision.AttackMoveIndex, Distance))
            {
                return;
            }

            enemy.MovementIntent = 0;
            BeginAttackWindUp(
                enemy,
                player,
                enemy.Moves[decision.AttackMoveIndex],
                decision.AttackMoveIndex,
                1f,
                false,
                decision.AttackSequence);
        }

        // ネットワーク同期の敵攻撃開始をローカル制約を越えて反映する
        private bool TryBeginNetworkSyncedEnemyAttack()
        {
            if (combatSync == null || enemyAi == null || IsFinished)
            {
                return false;
            }

            if (!enemyAi.TryConsumeNetworkAttackStart(out int moveIndex, out int attackSequence, out bool isCounter))
            {
                return false;
            }

            if (moveIndex < 0 || moveIndex >= enemy.Moves.Count || attackSequence <= 0)
            {
                return false;
            }

            // カウンター済みの自分の溜めは敵攻撃開始より先に止める
            if (pendingAttackAttacker == player)
            {
                if (pendingAttackSequence > 0
                    && cancelledAttackSequences.Contains(pendingAttackSequence))
                {
                    ClearPendingAttack();
                    player.PlayMotion(MotionType.Idle);
                }
                else if (isCounter)
                {
                    // カウンター通知より攻撃開始が先に届いた場合も自分の溜めを止める
                    ClearPendingAttack();
                    player.PlayMotion(MotionType.Idle);
                }
            }

            AttackMove move = enemy.Moves[moveIndex];
            enemy.MovementIntent = 0;
            PauseEnemyPartRepair();
            float powerMultiplier = isCounter ? settings.CounterDamageMultiplier : 1f;
            BeginAttackWindUp(
                enemy,
                player,
                move,
                moveIndex,
                powerMultiplier,
                isCounter,
                attackSequence);
            return true;
        }

        private void BeginAttackWindUp(
            BattleUnit attacker,
            BattleUnit target,
            AttackMove move,
            int moveIndex,
            float powerMultiplier,
            bool isCounter,
            int attackSequence = 0)
        {
            bool isNetworkSynced = combatSync != null && attackSequence > 0;
            if (attackLockoutRemaining > 0f && !isCounter && !isNetworkSynced)
            {
                return;
            }

            if (!isNetworkSynced && !attacker.IsMoveUsableByPart(moveIndex))
            {
                return;
            }

            float windUp = isCounter ? move.CounterWindUp : move.WindUp;
            windUp = Mathf.Max(0.05f, windUp);

            pendingAttackAttacker = attacker;
            pendingAttackTarget = target;
            pendingAttackMove = move;
            pendingAttackMoveIndex = moveIndex;
            pendingAttackWindUpRemaining = windUp;
            pendingAttackPowerMultiplier = powerMultiplier;
            pendingAttackIsCounter = isCounter;
            pendingAttackSequence = attackSequence;

            if (attacker == enemy)
            {
                lastRemoteEnemyMoveIndex = moveIndex;
            }

            attacker.PlayAttackCharge(move.Motion, windUp);
            attacker.MovementIntent = 0;
            target.MovementIntent = 0;

            attackWindUpStartedSubject.OnNext(new AttackWindUpStarted(attacker, target, move, windUp));
        }

        private void UpdateAttackWindUp(float deltaTime)
        {
            if (pendingAttackAttacker == null)
            {
                return;
            }

            if (pendingProjectileFlightRemaining > 0f)
            {
                pendingProjectileFlightRemaining -= deltaTime;
                if (pendingProjectileFlightRemaining > 0f)
                {
                    return;
                }

                ResolvePendingAttackStrike(replayAttackMotion: !pendingProjectileReplayMotionSkipped);
                return;
            }

            pendingAttackWindUpRemaining -= deltaTime;
            if (pendingAttackWindUpRemaining > 0f)
            {
                return;
            }

            if (pendingAttackAttacker == enemy && IsPlayerPerformingAttack && !player.IsPerformingAttack)
            {
                return;
            }

            if (ShouldLaunchProjectileBeforeStrike(pendingAttackMove))
            {
                LaunchPendingProjectile();
                return;
            }

            ResolvePendingAttackStrike();
        }

        private static bool ShouldLaunchProjectileBeforeStrike(AttackMove move)
        {
            return move != null && move.Motion == MotionType.Fireball;
        }

        private void LaunchPendingProjectile()
        {
            if (pendingAttackAttacker == null || pendingAttackMove == null)
            {
                ClearPendingAttack();
                return;
            }

            float travelDuration = BattleMagicAttackTiming.FireballTravelSeconds;
            pendingProjectileFlightRemaining = travelDuration;
            pendingProjectileReplayMotionSkipped = true;
            pendingAttackAttacker.PlayMotion(pendingAttackMove.Motion, pendingAttackMove.Recovery);
            attackProjectileStartedSubject.OnNext(
                new AttackProjectileStarted(
                    pendingAttackAttacker,
                    pendingAttackTarget,
                    pendingAttackMove,
                    travelDuration));
        }

        private void ResolvePendingAttackStrike(bool replayAttackMotion = true)
        {
            if (pendingAttackAttacker == null || pendingAttackMove == null)
            {
                ClearPendingAttack();
                return;
            }

            BattleUnit attacker = pendingAttackAttacker;
            BattleUnit target = pendingAttackTarget;
            AttackMove move = pendingAttackMove;
            int moveIndex = pendingAttackMoveIndex;
            float powerMultiplier = pendingAttackPowerMultiplier;
            bool wasCounter = pendingAttackIsCounter;
            bool wasEnemyAttack = attacker == enemy;
            int attackSequence = pendingAttackSequence;

            ClearPendingAttack();

            if (IsFinished || !attacker.IsMoveUsableByPart(moveIndex))
            {
                if (wasEnemyAttack)
                {
                    enemyAttackCooldownRemaining = UnityEngine.Random.Range(
                        settings.EnemyAttackCooldownMin,
                        settings.EnemyAttackCooldownMax);
                }

                return;
            }

            // カウンターされた攻撃は命中解決しない
            if (!wasCounter
                && !wasEnemyAttack
                && attackSequence > 0
                && cancelledAttackSequences.Contains(attackSequence))
            {
                attacker.PlayMotion(MotionType.Idle);
                return;
            }

            if (wasCounter)
            {
                ExecuteMove(
                    attacker,
                    target,
                    move,
                    moveIndex,
                    settings.CounterDamageMultiplier,
                    attackSequence,
                    replayAttackMotion);
                enemyAttackCooldownRemaining = UnityEngine.Random.Range(
                    settings.EnemyAttackCooldownMin,
                    settings.EnemyAttackCooldownMax);
                return;
            }

            if (wasEnemyAttack && combatSync != null && combatSync.ShouldDeferRemoteEnemyStrike)
            {
                if (replayAttackMotion)
                {
                    enemy.PlayMotion(move.Motion, move.Recovery);
                }

                enemy.ConsumeForMove(move);
                deferredEnemyAttackSequence = attackSequence;
                deferredEnemyAttackConsumed = attackSequence > 0;
                enemyAttackCooldownRemaining = UnityEngine.Random.Range(
                    settings.EnemyAttackCooldownMin,
                    settings.EnemyAttackCooldownMax);
                ProcessRemoteCombatSync();
                return;
            }

            ExecuteMove(attacker, target, move, moveIndex, powerMultiplier, attackSequence, replayAttackMotion);

            if (wasEnemyAttack)
            {
                enemyAttackCooldownRemaining = UnityEngine.Random.Range(
                    settings.EnemyAttackCooldownMin,
                    settings.EnemyAttackCooldownMax);
            }
        }

        private void ClearPendingAttack()
        {
            pendingAttackAttacker = null;
            pendingAttackTarget = null;
            pendingAttackMove = null;
            pendingAttackMoveIndex = -1;
            pendingAttackWindUpRemaining = 0f;
            pendingProjectileFlightRemaining = 0f;
            pendingProjectileReplayMotionSkipped = false;
            pendingAttackPowerMultiplier = 1f;
            pendingAttackIsCounter = false;
            pendingAttackSequence = 0;
        }

        private void ExecutePlayerMove(int moveIndex)
        {
            if (moveIndex == lastPlayerMoveIndex && chainWindowRemaining > 0f)
            {
                playerChainCount++;
            }
            else
            {
                playerChainCount = 1;
            }

            lastPlayerMoveIndex = moveIndex;
            chainWindowRemaining = settings.ChainBonusWindow;

            AttackMove move = player.Moves[moveIndex];
            float powerMultiplier = BattleCombatRules.ComputeChainPowerMultiplier(playerChainCount);
            int attackSequence = combatSync != null
                ? combatSync.ReportLocalAttackStart(moveIndex)
                : 0;
            BeginAttackWindUp(player, enemy, move, moveIndex, powerMultiplier, false, attackSequence);
        }

        private void ResolveCounter(int moveIndex)
        {
            if (moveIndex == lastPlayerMoveIndex && chainWindowRemaining > 0f)
            {
                playerChainCount++;
            }
            else
            {
                playerChainCount = 1;
            }

            lastPlayerMoveIndex = moveIndex;
            chainWindowRemaining = settings.ChainBonusWindow;

            AttackMove playerMove = player.Moves[moveIndex];
            int counteredAttackSequence = pendingAttackSequence;
            if (counteredAttackSequence > 0)
            {
                cancelledAttackSequences.Add(counteredAttackSequence);
                combatSync?.ReportLocalCounter(counteredAttackSequence);
            }

            ClearPendingAttack();
            enemyAttackCooldownRemaining = UnityEngine.Random.Range(
                settings.EnemyAttackCooldownMin,
                settings.EnemyAttackCooldownMax);

            int counterAttackSequence = combatSync != null
                ? combatSync.ReportLocalAttackStart(moveIndex, isCounter: true)
                : 0;
            BeginAttackWindUp(
                player,
                enemy,
                playerMove,
                moveIndex,
                settings.CounterDamageMultiplier,
                true,
                counterAttackSequence);
        }

        // 敵技の破壊部位と同じ使用部位の技ならカウンターできる(使用部位なしは不可)
        private bool IsPlayerCounterMove(int moveIndex)
        {
            if (!IsCounterWindowOpen || pendingAttackMove == null)
            {
                return false;
            }

            if (moveIndex < 0 || moveIndex >= player.Moves.Count)
            {
                return false;
            }

            AttackMove playerMove = player.Moves[moveIndex];
            if (playerMove == null || playerMove.RequiredPart == BonePart.Body)
            {
                return false;
            }

            return playerMove.RequiredPart == pendingAttackMove.TargetDestroyPart;
        }

        private void TryKnockback()
        {
            if (IsAttackPresentationActive)
            {
                return;
            }

            if (playerCombatInput == null || !playerCombatInput.ConsumeKnockbackPressed())
            {
                return;
            }

            if (!IsKnockbackAvailable)
            {
                return;
            }

            SetPlayerMovement(0);
            player.ConsumeGuts(settings.KnockbackGutsCost);
            player.BeginRecovery(settings.KnockbackRecovery);
            player.PlayMotion(MotionType.Tackle);
            float openAmount = Mathf.Max(0f, settings.MaxDistance - Distance);
            if (fieldMovement != null)
            {
                fieldMovement.PushEnemyAway(
                    openAmount,
                    Distance,
                    settings.MaxDistance,
                    out float newDistance);
                Distance = newDistance;
            }
            else
            {
                Distance = settings.MaxDistance;
            }

            combatSync?.ReportLocalKnockback(Distance);
            knockbackPerformedSubject.OnNext(new KnockbackPerformed(player, enemy));
        }

        private void ApplyRemoteEnemyKnockback(float resultingDistance)
        {
            if (IsFinished)
            {
                return;
            }

            if (enemy.Guts >= settings.KnockbackGutsCost)
            {
                enemy.ConsumeGuts(settings.KnockbackGutsCost);
            }

            enemy.BeginRecovery(settings.KnockbackRecovery);
            enemy.PlayMotion(MotionType.Tackle);
            float openAmount = Mathf.Max(0f, resultingDistance - Distance);
            if (fieldMovement != null && openAmount > 1e-4f)
            {
                fieldMovement.PushPlayerAway(
                    openAmount,
                    Distance,
                    settings.MaxDistance,
                    out float newDistance);
                Distance = newDistance;
            }
            else
            {
                Distance = Mathf.Clamp(resultingDistance, 0f, settings.MaxDistance);
            }

            knockbackPerformedSubject.OnNext(new KnockbackPerformed(enemy, player));
        }

        private void UpdatePartRepair(float deltaTime)
        {
            if (playerCombatInput == null || !playerCombatInput.IsHoldingPartRepair)
            {
                PausePartRepair();
                return;
            }

            if (IsFinished || player.LostPartCount <= 0)
            {
                ResetPartRepair();
                return;
            }

            if (IsAttackPresentationActive)
            {
                PausePartRepair();
                return;
            }

            int? nextLimb = player.PeekNextRestoreLimbIndex();
            if (!nextLimb.HasValue)
            {
                ResetPartRepair();
                return;
            }

            if (repairingLimbIndex >= 0 && repairingLimbIndex != nextLimb.Value)
            {
                ResetPartRepair();
                return;
            }

            player.MovementIntent = 0;
            float repairDuration = Mathf.Max(0.1f, settings.PartRepairSecondsPerLimb);

            if (repairingLimbIndex < 0)
            {
                repairingLimbIndex = nextLimb.Value;
                partRepairProgress = 0f;
                player.PlayMotion(MotionType.Idle);
            }

            partRepairProgress += deltaTime;
            float progress = Mathf.Clamp01(partRepairProgress / repairDuration);
            EnsurePartRepairVisual(repairingLimbIndex, progress);

            if (progress < 1f)
            {
                return;
            }

            int restoredLimbIndex = repairingLimbIndex;
            if (player.TryRestoreOnePart())
            {
                ResetPartRepair();
                combatSync?.ReportLocalPlayerPartRestored(restoredLimbIndex);
                return;
            }

            ResetPartRepair();
        }

        private void EnsurePartRepairVisual(int limbIndex, float progress)
        {
            if (!isPartRepairVisualActive)
            {
                player.BeginPartRepairVisual(limbIndex);
                isPartRepairVisualActive = true;
            }

            player.SetPartRepairVisualProgress(limbIndex, progress);
        }

        private void PausePartRepair()
        {
            if (!isPartRepairVisualActive)
            {
                return;
            }

            player.CancelPartRepairVisual();
            isPartRepairVisualActive = false;
        }

        private void ResetPartRepair()
        {
            PausePartRepair();
            repairingLimbIndex = -1;
            partRepairProgress = 0f;
        }

        // 敵の欠損部位を時間経過で1つずつ修復する
        private void UpdateEnemyPartRepair(float deltaTime)
        {
            if (IsFinished || enemy.LostPartCount <= 0)
            {
                ResetEnemyPartRepair();
                return;
            }

            if (IsAttackPresentationActive)
            {
                PauseEnemyPartRepair();
                return;
            }

            int? nextLimb = enemy.PeekNextRestoreLimbIndex();
            if (!nextLimb.HasValue)
            {
                ResetEnemyPartRepair();
                return;
            }

            if (enemyRepairingLimbIndex >= 0 && enemyRepairingLimbIndex != nextLimb.Value)
            {
                ResetEnemyPartRepair();
                return;
            }

            enemy.MovementIntent = 0;
            float repairDuration = Mathf.Max(0.1f, settings.PartRepairSecondsPerLimb);

            if (enemyRepairingLimbIndex < 0)
            {
                enemyRepairingLimbIndex = nextLimb.Value;
                enemyPartRepairProgress = 0f;
                enemy.PlayMotion(MotionType.Idle);
            }

            enemyPartRepairProgress += deltaTime;
            float progress = Mathf.Clamp01(enemyPartRepairProgress / repairDuration);
            EnsureEnemyPartRepairVisual(enemyRepairingLimbIndex, progress);

            if (progress < 1f)
            {
                return;
            }

            if (combatSync != null)
            {
                ResetEnemyPartRepair();
                return;
            }

            if (enemy.TryRestoreOnePart())
            {
                ResetEnemyPartRepair();
                return;
            }

            ResetEnemyPartRepair();
        }

        private void EnsureEnemyPartRepairVisual(int limbIndex, float progress)
        {
            if (!isEnemyPartRepairVisualActive)
            {
                enemy.BeginPartRepairVisual(limbIndex);
                isEnemyPartRepairVisualActive = true;
            }

            enemy.SetPartRepairVisualProgress(limbIndex, progress);
        }

        private void PauseEnemyPartRepair()
        {
            if (!isEnemyPartRepairVisualActive)
            {
                return;
            }

            enemy.CancelPartRepairVisual();
            isEnemyPartRepairVisualActive = false;
        }

        private void ResetEnemyPartRepair()
        {
            PauseEnemyPartRepair();
            enemyRepairingLimbIndex = -1;
            enemyPartRepairProgress = 0f;
        }

        private void ProcessRemoteCombatSync()
        {
            if (combatSync == null || IsFinished)
            {
                return;
            }

            combatSync.EnsureListening();
            combatSync.PollRemoteSync();
            ProcessRemoteCounterSync();
            ProcessRemoteStrikeSync();
            ProcessRemoteDistanceSync();
            ProcessRemoteKnockbackSync();
            ProcessRemoteStepSync();
            ProcessRemotePartRestoreSync();
        }

        private void ProcessRemoteKnockbackSync()
        {
            while (combatSync.TryConsumeRemoteKnockback(out float resultingDistance))
            {
                ApplyRemoteEnemyKnockback(resultingDistance);
            }
        }

        private void ProcessRemoteDistanceSync()
        {
            // 間合いは移動結果から算出するため権威距離では位置も間合いも上書きしない
            while (combatSync.TryConsumeAuthoritativeDistance(out _))
            {
            }
        }

        private void UpdateDistanceAuthoritySync(float deltaTime)
        {
            if (combatSync == null || !combatSync.IsDistanceAuthority)
            {
                return;
            }

            distanceSyncRemaining -= deltaTime;
            if (distanceSyncRemaining > 0f)
            {
                return;
            }

            distanceSyncRemaining = DistanceSyncInterval;
            combatSync.ReportAuthoritativeDistance(Distance);
        }

        private void ProcessRemoteCounterSync()
        {
            while (combatSync.TryConsumeRemoteCounter(out int counteredAttackSequence))
            {
                if (counteredAttackSequence <= 0)
                {
                    continue;
                }

                cancelledAttackSequences.Add(counteredAttackSequence);
                if (pendingAttackAttacker == player
                    && pendingAttackSequence == counteredAttackSequence)
                {
                    ClearPendingAttack();
                    player.PlayMotion(MotionType.Idle);
                }
            }
        }

        private void ProcessRemotePartRestoreSync()
        {
            while (combatSync.TryConsumeRemotePartRestore(out BattleRemotePartRestorePayload payload))
            {
                enemy.ApplySyncedRestoreLimb(payload.LimbIndex);
            }
        }

        private void ProcessRemoteStrikeSync()
        {
            if (hasPendingFailedRemoteStrike)
            {
                if (TryApplyRemoteStrikePayload(pendingFailedRemoteStrike))
                {
                    hasPendingFailedRemoteStrike = false;
                }
            }

            while (combatSync.TryConsumeRemoteStrike(out BattleRemoteStrikePayload payload))
            {
                if (!TryApplyRemoteStrikePayload(payload))
                {
                    pendingFailedRemoteStrike = payload;
                    hasPendingFailedRemoteStrike = true;
                }
            }
        }

        private bool TryApplyRemoteStrikePayload(BattleRemoteStrikePayload payload)
        {
            // カウンター済みの相手攻撃結果は破棄する
            if (payload.AttackSequence > 0
                && cancelledAttackSequences.Contains(payload.AttackSequence))
            {
                if (pendingAttackAttacker == enemy
                    && pendingAttackSequence == payload.AttackSequence)
                {
                    ClearPendingAttack();
                }

                return true;
            }

            if (pendingAttackAttacker == enemy)
            {
                ClearPendingAttack();
            }

            AttackMove move = ResolveMoveForUnit(enemy, payload.MoveIndex);
            if (move == null && lastRemoteEnemyMoveIndex >= 0)
            {
                move = ResolveMoveForUnit(enemy, lastRemoteEnemyMoveIndex);
            }

            if (move == null)
            {
                return false;
            }

            ApplySyncedStrike(enemy, player, move, payload);
            return true;
        }

        private void PublishMoveUsed(MoveUsedResult result)
        {
            moveUsedSubject.OnNext(result);
        }

        private void ProcessRemoteStepSync()
        {
            if (!combatSync.TryConsumeRemoteStep(out BattleRemoteStepPayload payload))
            {
                return;
            }

            float targetDistance = Mathf.Clamp(payload.TargetDistance, 0f, settings.MaxDistance);
            if (Mathf.Approximately(targetDistance, Distance))
            {
                combatSync.ConfirmRemoteStepConsumed();
                return;
            }

            if (TryBeginRemoteEnemyStep(
                payload.StepIntent,
                respectPlayerAttack: false,
                explicitTargetDistance: targetDistance,
                isNetworkSynced: true))
            {
                combatSync.ConfirmRemoteStepConsumed();
            }
        }

        private static AttackMove ResolveMoveForUnit(BattleUnit unit, int moveIndex)
        {
            if (unit == null || moveIndex < 0 || moveIndex >= unit.Moves.Count)
            {
                return null;
            }

            return unit.Moves[moveIndex];
        }

        private void TryBeginPlayerStep(int stepIntent)
        {
            if (IsFinished || stepIntent == 0 || isPlayerStepping || isEnemyStepping || IsAttackPresentationActive)
            {
                return;
            }

            if (!player.CanAct || playerStepCooldownRemaining > 0f)
            {
                return;
            }

            if (stepIntent > 0
                && fieldBoundary != null
                && fieldBoundary.IsPlayerAtHomeBoundary)
            {
                return;
            }

            float delta = stepIntent * player.ResolveStepDistance(settings.StepDistance);
            float targetDistance = Mathf.Clamp(Distance + delta, 0f, settings.MaxDistance);
            if (Mathf.Approximately(targetDistance, Distance))
            {
                return;
            }

            isPlayerStepping = true;
            playerStepMovementIntent = stepIntent;
            playerStepStartDistance = Distance;
            playerStepTargetDistance = targetDistance;
            playerStepElapsed = 0f;
            fieldMovement?.BeginPlayerStep(playerStepStartDistance, playerStepTargetDistance, settings.MaxDistance);
            player.BeginRecovery(settings.StepDuration);
            player.PlayStepMotion(stepIntent, settings.StepDuration);
            playerStepCooldownRemaining = settings.StepCooldown;
            combatSync?.ReportLocalPlayerStep(stepIntent, playerStepTargetDistance);
        }

        private void TryBeginEnemyStep(int stepIntent)
        {
            TryBeginRemoteEnemyStep(stepIntent, respectPlayerAttack: true);
        }

        private bool TryBeginRemoteEnemyStep(
            int stepIntent,
            bool respectPlayerAttack = false,
            float? explicitTargetDistance = null,
            bool isNetworkSynced = false)
        {
            if (IsFinished || stepIntent == 0 || isEnemyStepping || isPlayerStepping)
            {
                return false;
            }

            if (respectPlayerAttack && (IsPlayerPerformingAttack || pendingAttackAttacker != null))
            {
                return false;
            }

            if (!isNetworkSynced && (!enemy.CanAct || enemyStepCooldownRemaining > 0f))
            {
                return false;
            }

            if (!isNetworkSynced
                && stepIntent > 0
                && fieldBoundary != null
                && fieldBoundary.IsEnemyAtHomeBoundary)
            {
                return false;
            }

            float targetDistance = explicitTargetDistance.HasValue
                ? Mathf.Clamp(explicitTargetDistance.Value, 0f, settings.MaxDistance)
                : Mathf.Clamp(
                    Distance + stepIntent * enemy.ResolveStepDistance(settings.StepDistance),
                    0f,
                    settings.MaxDistance);
            if (Mathf.Approximately(targetDistance, Distance))
            {
                return false;
            }

            isEnemyStepping = true;
            enemyStepMovementIntent = stepIntent;
            enemyStepStartDistance = Distance;
            enemyStepTargetDistance = targetDistance;
            enemyStepElapsed = 0f;
            fieldMovement?.BeginEnemyStep(enemyStepStartDistance, enemyStepTargetDistance, settings.MaxDistance);
            enemy.BeginRecovery(settings.StepDuration);
            enemy.PlayStepMotion(stepIntent, settings.StepDuration);

            if (!isNetworkSynced)
            {
                enemyStepCooldownRemaining = settings.StepCooldown;
            }

            return true;
        }

        private void UpdateEnemyStep(float deltaTime)
        {
            if (!isEnemyStepping)
            {
                return;
            }

            enemyStepElapsed += deltaTime;
            float duration = Mathf.Max(0.01f, settings.StepDuration);
            float t = Mathf.Clamp01(enemyStepElapsed / duration);
            float eased = 1f - (1f - t) * (1f - t);
            if (fieldMovement != null)
            {
                fieldMovement.SetEnemyStepProgress(eased, settings.MaxDistance, out float newDistance);
                Distance = newDistance;
            }
            else
            {
                Distance = Mathf.Lerp(enemyStepStartDistance, enemyStepTargetDistance, eased);
            }

            if (t >= 1f)
            {
                if (fieldMovement != null)
                {
                    fieldMovement.SetEnemyStepProgress(1f, settings.MaxDistance, out float finalDistance);
                    Distance = finalDistance;
                }
                else
                {
                    Distance = enemyStepTargetDistance;
                }

                isEnemyStepping = false;
                enemyStepMovementIntent = 0;
            }
        }

        private void UpdatePlayerStep(float deltaTime)
        {
            if (!isPlayerStepping)
            {
                return;
            }

            playerStepElapsed += deltaTime;
            float duration = Mathf.Max(0.01f, settings.StepDuration);
            float t = Mathf.Clamp01(playerStepElapsed / duration);
            float eased = 1f - (1f - t) * (1f - t);
            if (fieldMovement != null)
            {
                fieldMovement.SetPlayerStepProgress(eased, settings.MaxDistance, out float newDistance);
                Distance = newDistance;
            }
            else
            {
                Distance = Mathf.Lerp(playerStepStartDistance, playerStepTargetDistance, eased);
            }

            if (t >= 1f)
            {
                if (fieldMovement != null)
                {
                    fieldMovement.SetPlayerStepProgress(1f, settings.MaxDistance, out float finalDistance);
                    Distance = finalDistance;
                }
                else
                {
                    Distance = playerStepTargetDistance;
                }

                isPlayerStepping = false;
                playerStepMovementIntent = 0;
            }
        }

        // 技を実行する(消費・命中判定・ダメージ・部位欠損)
        private void ExecuteMove(
            BattleUnit attacker,
            BattleUnit target,
            AttackMove move,
            int moveIndex,
            float powerMultiplier = 1f,
            int attackSequence = 0,
            bool replayAttackMotion = true)
        {
            float hitRate = BattleCombatRules.ComputeHitRate(
                move.Accuracy,
                attacker.Guts,
                attacker.MaxGuts,
                attacker.Hit);
            if (replayAttackMotion)
            {
                attacker.PlayMotion(move.Motion, move.Recovery);
            }

            attacker.ConsumeForMove(move);

            bool hit = UnityEngine.Random.value <= hitRate;
            int damage = 0;
            bool partLost = false;
            BonePart lostPart = BonePart.Body;
            int lostLimbIndex = -1;

            bool isKnockout = false;
            if (hit)
            {
                damage = ComputeDamage(attacker, move, target, powerMultiplier);
                target.TakeDamage(damage);
                partLost = target.TryLosePart(move, out lostPart, out lostLimbIndex);
                target.PlayHitMotion(partLost);
                isKnockout = target.IsDefeated;
            }

            if (hit)
            {
                if (isKnockout)
                {
                    BeginFinishFeedback();
                }
                else
                {
                    BeginHitFeedback(partLost);
                }
            }

            var result = new MoveUsedResult(
                attacker,
                target,
                move,
                hit,
                damage,
                partLost,
                lostPart,
                isKnockout,
                lostLimbIndex);
            PublishMoveUsed(result);

            if (attacker == player && combatSync != null)
            {
                combatSync.ReportLocalPlayerStrike(result, moveIndex, attackSequence);
            }

            if (target.IsDefeated)
            {
                Finish(attacker, BattleEndReason.Knockout);
            }

            BeginPostAttackLockout();
        }

        private void ApplySyncedStrike(
            BattleUnit attacker,
            BattleUnit target,
            AttackMove move,
            BattleRemoteStrikePayload payload)
        {
            AttackMove strikeMove = ResolveMoveForUnit(attacker, payload.MoveIndex) ?? move;
            bool skipConsume = deferredEnemyAttackConsumed
                && payload.AttackSequence > 0
                && payload.AttackSequence == deferredEnemyAttackSequence;
            if (skipConsume)
            {
                deferredEnemyAttackConsumed = false;
                deferredEnemyAttackSequence = 0;
            }
            else if (!attacker.IsPerformingAttack)
            {
                attacker.PlayMotion(strikeMove.Motion, strikeMove.Recovery);
                attacker.ConsumeForMove(strikeMove);
            }

            bool hit = payload.Hit;
            int damage = payload.Damage;
            bool partLost = payload.PartLost;
            BonePart lostPart = payload.LostPart;
            bool isKnockout = payload.IsKnockout;

            if (hit)
            {
                target.TakeDamage(damage);
                if (partLost)
                {
                    if (payload.LostLimbIndex >= 0)
                    {
                        target.ApplySyncedPartLoss(payload.LostLimbIndex, payload.LostPart, out lostPart);
                    }
                    else
                    {
                        target.ApplySyncedPartLoss(payload.LostPart, out lostPart);
                    }
                }

                target.PlayHitMotion(partLost);
            }

            if (hit)
            {
                if (isKnockout)
                {
                    BeginFinishFeedback();
                }
                else
                {
                    BeginHitFeedback(partLost);
                }
            }

            PublishMoveUsed(new MoveUsedResult(
                attacker,
                target,
                strikeMove,
                hit,
                damage,
                partLost,
                lostPart,
                isKnockout,
                payload.LostLimbIndex));

            if (target.IsDefeated)
            {
                Finish(attacker, BattleEndReason.Knockout);
            }

            BeginPostAttackLockout();
        }

        private void BeginPostAttackLockout()
        {
            if (settings.PostAttackLockoutDuration <= 0f)
            {
                return;
            }

            attackLockoutRemaining = settings.PostAttackLockoutDuration;
        }

        private int ComputeDamage(BattleUnit attacker, AttackMove move, BattleUnit target, float powerMultiplier = 1f)
        {
            float raw = attacker.Attack * move.Power * powerMultiplier * settings.DamageScale * (100f / (100f + target.Defense));
            return Mathf.Max(1, Mathf.RoundToInt(raw));
        }

        // 時間切れ:HPが多い方を勝者にする(同値は引き分け=null)
        private void FinishByHp(BattleEndReason reason)
        {
            float playerRatio = (float)player.CurrentHp / player.MaxHp;
            float enemyRatio = (float)enemy.CurrentHp / enemy.MaxHp;

            if (Mathf.Approximately(playerRatio, enemyRatio))
            {
                Finish(null, reason);
            }
            else
            {
                Finish(playerRatio > enemyRatio ? player : enemy, reason);
            }
        }

        private void Finish(BattleUnit winner, BattleEndReason reason)
        {
            if (IsFinished)
            {
                return;
            }

            IsFinished = true;
            EndReason = reason;
            isPlayerStepping = false;
            playerStepMovementIntent = 0;
            ClearPendingAttack();
            player.MovementIntent = 0;
            enemy.MovementIntent = 0;
            player.SuspendPartLossRebuild();
            enemy.SuspendPartLossRebuild();
            TimeRemaining = Mathf.Max(0f, TimeRemaining);
            updatedSubject.OnNext(Unit.Default);
            Debug.Log($"[BattleSystem] 決着: {reason} 勝者={(winner != null ? winner.Name : "引き分け")} 残り時間={TimeRemaining:0.0}s プレイヤーHP={player.CurrentHp}/{player.MaxHp} 敵HP={enemy.CurrentHp}/{enemy.MaxHp}");
            battleEndSubject.OnNext(winner);
        }

        /// <summary>
        /// 購読を破棄する
        /// </summary>
        public void Dispose()
        {
            ClearHitStop();
            updatedSubject.Dispose();
            moveUsedSubject.Dispose();
            attackWindUpStartedSubject.Dispose();
            attackProjectileStartedSubject.Dispose();
            knockbackPerformedSubject.Dispose();
            battleEndSubject.Dispose();
        }
    }
}