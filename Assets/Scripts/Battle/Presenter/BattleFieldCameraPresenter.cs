using System;
using Camera.Interface;
using R3;
using UnityEngine;

namespace Battle.Presenter
{
    /// <summary>
    /// 戦闘中のカメラを両モデル間へ注視させつつ扇状オービットで構図を更新する
    /// 攻撃時は攻撃側の正面へ寄せて技モーションを見せる
    /// 命中時は注視点を揺らして画面揺れを表現する
    /// </summary>
    public sealed class BattleFieldCameraPresenter : IDisposable
    {
        private readonly IClayEditCameraView cameraView;
        private readonly BattleSystem system;
        private readonly Transform playerModel;
        private readonly Transform enemyModel;
        private readonly BattleFieldCameraProfile profile;
        private readonly CompositeDisposable disposables = new CompositeDisposable();

        private readonly Vector3 battleCenterFlat;
        private readonly float battleGroundY;
        private readonly float maxFanOffset;

        private Vector3 smoothedFocus;
        private float smoothedHorizontalAngle;
        private float smoothedOrbitDistance;

        private Transform attackAttackerModel;
        private Transform attackTargetModel;
        private float attackCameraRemaining;
        private float idleOrbitPhase;
        private float smoothedVerticalAngle;

        private float shakeRemaining;
        private float shakeDuration;
        private float shakeAmplitude;
        private float shakeFrequency;
        private Vector3 shakeSeed;

        /// <summary>
        /// 戦闘更新に追従してカメラを制御する
        /// </summary>
        public BattleFieldCameraPresenter(
            IClayEditCameraView cameraView,
            BattleSystem system,
            Transform playerModel,
            Transform enemyModel,
            Transform playerSpawn,
            Transform enemySpawn,
            BattleFieldCameraProfile profile)
        {
            this.cameraView = cameraView;
            this.system = system;
            this.playerModel = playerModel;
            this.enemyModel = enemyModel;
            this.profile = profile ?? BattleFieldCameraProfile.Default;

            Vector3 playerHome = playerSpawn != null ? playerSpawn.position : playerModel.position;
            Vector3 enemyHome = enemySpawn != null ? enemySpawn.position : enemyModel.position;

            battleGroundY = (playerHome.y + enemyHome.y) * 0.5f;

            battleCenterFlat = (playerHome + enemyHome) * 0.5f;
            battleCenterFlat.y = battleGroundY;

            Vector3 axis = playerHome - enemyHome;
            axis.y = 0f;
            maxFanOffset = axis.sqrMagnitude > 1e-6f ? axis.magnitude * 0.5f : 1f;

            smoothedFocus = ResolveFocusPoint();
            smoothedHorizontalAngle = this.profile.HorizontalAngle;
            smoothedVerticalAngle = this.profile.VerticalAngle;
            smoothedOrbitDistance = ResolveTargetOrbitDistance();

            cameraView.SetCameraOperatable(false);
            ApplyCamera(smoothedFocus, smoothedHorizontalAngle, smoothedOrbitDistance, smoothedVerticalAngle);

            system.OnUpdated
                .Subscribe(_ => Tick())
                .AddTo(disposables);

            system.OnMoveUsed
                .Subscribe(OnMoveUsed)
                .AddTo(disposables);

            system.OnAttackWindUpStarted
                .Subscribe(OnAttackWindUpStarted)
                .AddTo(disposables);
        }

        private void OnAttackWindUpStarted(AttackWindUpStarted started)
        {
            float holdDuration = started.WindUpDuration
                + Mathf.Max(
                    profile.AttackMinimumHoldSeconds,
                    started.Move.Recovery * profile.AttackHoldRecoveryMultiplier)
                + profile.EnemyTelegraphHoldPadding;
            BeginAttackCamera(started.Attacker, started.Target, holdDuration);
        }

        private void OnMoveUsed(MoveUsedResult result)
        {
            float holdDuration = Mathf.Max(
                profile.AttackMinimumHoldSeconds,
                result.Move.Recovery * profile.AttackHoldRecoveryMultiplier);
            BeginAttackCamera(result.Attacker, result.Target, holdDuration);

            if (result.Hit)
            {
                BeginHitShake(result.PartLost || result.IsKnockout);
            }
        }

        private void Tick()
        {
            TickHitShake(Time.unscaledDeltaTime);

            if (attackCameraRemaining > 0f)
            {
                attackCameraRemaining -= Time.deltaTime;
                TickAttackCamera();
                return;
            }

            TickDefaultCamera();
        }

        private void BeginHitShake(bool intense)
        {
            if (intense)
            {
                shakeAmplitude = profile.PartBreakShakeAmplitude;
                shakeDuration = Mathf.Max(0.01f, profile.PartBreakShakeDuration);
                shakeFrequency = profile.PartBreakShakeFrequency;
            }
            else
            {
                shakeAmplitude = profile.HitShakeAmplitude;
                shakeDuration = Mathf.Max(0.01f, profile.HitShakeDuration);
                shakeFrequency = profile.HitShakeFrequency;
            }

            shakeRemaining = Mathf.Max(shakeRemaining, shakeDuration);
            shakeSeed = new Vector3(
                UnityEngine.Random.value * 64f,
                UnityEngine.Random.value * 64f,
                UnityEngine.Random.value * 64f);
        }

        private void TickHitShake(float unscaledDeltaTime)
        {
            if (shakeRemaining <= 0f)
            {
                return;
            }

            shakeRemaining = Mathf.Max(0f, shakeRemaining - unscaledDeltaTime);
        }

        private Vector3 ResolveShakeOffset()
        {
            if (shakeRemaining <= 0f || shakeDuration <= 0f || shakeAmplitude <= 0f)
            {
                return Vector3.zero;
            }

            float falloff = shakeRemaining / shakeDuration;
            falloff *= falloff;
            float sampleTime = Time.unscaledTime * shakeFrequency;
            float offsetX = (Mathf.PerlinNoise(shakeSeed.x, sampleTime) * 2f - 1f) * shakeAmplitude * falloff;
            float offsetY = (Mathf.PerlinNoise(shakeSeed.y, sampleTime + 17f) * 2f - 1f) * shakeAmplitude * falloff;
            float offsetZ = (Mathf.PerlinNoise(shakeSeed.z, sampleTime + 31f) * 2f - 1f) * shakeAmplitude * 0.55f * falloff;
            return new Vector3(offsetX, offsetY, offsetZ);
        }

        private void BeginAttackCamera(BattleUnit attacker, BattleUnit target, float holdDuration)
        {
            attackAttackerModel = ResolveModel(attacker);
            attackTargetModel = ResolveModel(target);
            if (attackAttackerModel == null)
            {
                return;
            }

            attackCameraRemaining = Mathf.Max(attackCameraRemaining, holdDuration);
        }

        private Transform ResolveModel(BattleUnit unit)
        {
            if (unit == system.Player)
            {
                return playerModel;
            }

            if (unit == system.Enemy)
            {
                return enemyModel;
            }

            return null;
        }

        private void TickAttackCamera()
        {
            if (attackAttackerModel == null)
            {
                attackCameraRemaining = 0f;
                TickDefaultCamera();
                return;
            }

            float focusBlend = 1f - Mathf.Exp(-profile.AttackFocusSmoothing * Time.deltaTime);
            float distanceBlend = 1f - Mathf.Exp(-profile.AttackDistanceSmoothing * Time.deltaTime);
            float angleBlend = 1f - Mathf.Exp(-profile.AttackAngleSmoothing * Time.deltaTime);

            Vector3 targetFocus = ResolveAttackFocusPoint();
            float targetDistance = profile.AttackOrbitDistance;
            float targetHorizontal = ResolveAttackHorizontalAngle();
            float targetVertical = profile.AttackVerticalAngle;

            smoothedFocus = Vector3.Lerp(smoothedFocus, targetFocus, focusBlend);
            smoothedOrbitDistance = Mathf.Lerp(smoothedOrbitDistance, targetDistance, distanceBlend);
            smoothedHorizontalAngle = Mathf.LerpAngle(smoothedHorizontalAngle, targetHorizontal, angleBlend);
            smoothedVerticalAngle = Mathf.LerpAngle(smoothedVerticalAngle, targetVertical, angleBlend);

            ApplyCamera(
                smoothedFocus,
                smoothedHorizontalAngle,
                smoothedOrbitDistance,
                smoothedVerticalAngle);
        }

        private void TickDefaultCamera()
        {
            attackAttackerModel = null;
            attackTargetModel = null;

            float focusBlend = 1f - Mathf.Exp(-profile.FocusSmoothing * Time.deltaTime);
            float distanceBlend = 1f - Mathf.Exp(-profile.DistanceSmoothing * Time.deltaTime);
            float angleBlend = 1f - Mathf.Exp(-profile.HorizontalAngleSmoothing * Time.deltaTime);

            AdvanceIdleOrbitPhase(Time.deltaTime);
            Vector3 targetFocus = ResolveFocusPoint();
            float targetDistance = ResolveTargetOrbitDistance();
            float targetHorizontal = ResolveTargetHorizontalAngle() + ResolveIdleHorizontalSway();
            float targetVertical = profile.VerticalAngle + ResolveIdleVerticalSway();

            smoothedFocus = Vector3.Lerp(smoothedFocus, targetFocus, focusBlend);
            smoothedOrbitDistance = Mathf.Lerp(smoothedOrbitDistance, targetDistance, distanceBlend);
            smoothedHorizontalAngle = Mathf.LerpAngle(smoothedHorizontalAngle, targetHorizontal, angleBlend);
            smoothedVerticalAngle = Mathf.LerpAngle(smoothedVerticalAngle, targetVertical, angleBlend);

            ApplyCamera(smoothedFocus, smoothedHorizontalAngle, smoothedOrbitDistance, smoothedVerticalAngle);
        }

        private void AdvanceIdleOrbitPhase(float deltaTime)
        {
            float period = Mathf.Max(0.1f, profile.IdleOrbitPeriodSeconds);
            idleOrbitPhase += deltaTime * (Mathf.PI * 2f / period);
            if (idleOrbitPhase > Mathf.PI * 2f)
            {
                idleOrbitPhase -= Mathf.PI * 2f;
            }
        }

        private float ResolveIdleHorizontalSway()
        {
            return Mathf.Sin(idleOrbitPhase) * profile.IdleOrbitAmplitudeDegrees;
        }

        private float ResolveIdleVerticalSway()
        {
            return Mathf.Sin(idleOrbitPhase * 0.5f) * profile.IdleVerticalAmplitudeDegrees;
        }

        private void ApplyCamera(Vector3 focusPoint, float horizontalAngle, float orbitDistance, float verticalAngle)
        {
            cameraView.SetFocusPosition(focusPoint + ResolveShakeOffset());
            cameraView.SetOrbitView(horizontalAngle, verticalAngle, orbitDistance);
        }

        private Vector3 ResolveAttackFocusPoint()
        {
            return BattleFieldFocusResolver.ResolveAttackFocus(
                attackAttackerModel,
                attackTargetModel,
                battleGroundY,
                profile.AttackFocusHeightOffset,
                profile.AttackFocusTowardTargetRatio);
        }

        private float ResolveAttackHorizontalAngle()
        {
            Vector3 attackerForward = ResolveAttackerForwardFlat();
            if (attackerForward.sqrMagnitude < 1e-4f)
            {
                return smoothedHorizontalAngle;
            }

            // 攻撃側正面(向きの反対側)を基準にわずかな横偏りで三四分構図にする
            float fightYaw = Mathf.Atan2(attackerForward.x, attackerForward.z) * Mathf.Rad2Deg;
            float frontYaw = fightYaw + 180f;
            float bias = Mathf.Max(0f, profile.AttackSideBiasDegrees);
            float candidatePositive = frontYaw + bias;
            float candidateNegative = frontYaw - bias;
            float referenceAngle = ResolveTargetHorizontalAngle();

            return Mathf.Abs(Mathf.DeltaAngle(referenceAngle, candidatePositive))
                <= Mathf.Abs(Mathf.DeltaAngle(referenceAngle, candidateNegative))
                ? candidatePositive
                : candidateNegative;
        }

        private Vector3 ResolveAttackerForwardFlat()
        {
            Vector3 forward = attackAttackerModel.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f && attackTargetModel != null)
            {
                forward = attackTargetModel.position - attackAttackerModel.position;
                forward.y = 0f;
            }

            return forward.sqrMagnitude < 1e-4f ? Vector3.zero : forward.normalized;
        }

        private Vector3 ResolveFocusPoint()
        {
            return BattleFieldFocusResolver.ResolveMidpoint(
                playerModel,
                enemyModel,
                battleGroundY,
                profile.FocusHeightOffset);
        }

        private float ResolveTargetHorizontalAngle()
        {
            if (playerModel == null || enemyModel == null || maxFanOffset < 1e-4f)
            {
                return profile.HorizontalAngle;
            }

            Vector3 fightMidpoint = BattleFieldFocusResolver.ResolveMidpointOnGround(
                playerModel,
                enemyModel,
                battleGroundY);

            Vector3 screenRight = BattleFieldScreenAxis.ResolveScreenRight(profile.HorizontalAngle);
            float offset = Vector3.Dot(fightMidpoint - battleCenterFlat, screenRight);
            float ratio = Mathf.Clamp(offset / maxFanOffset, -1f, 1f);
            return profile.HorizontalAngle - ratio * profile.MaxFanAngleDegrees;
        }

        private float ResolveTargetOrbitDistance()
        {
            float ratio = system.MaxDistance > 0f
                ? Mathf.Clamp01(system.Distance / system.MaxDistance)
                : 0f;

            float baseline = Mathf.Lerp(profile.OrbitDistanceAtClose, profile.OrbitDistanceAtFar, ratio);
            float framed = ComputeFramedOrbitDistance();
            return Mathf.Clamp(Mathf.Max(baseline, framed), profile.MinOrbitDistance, profile.MaxOrbitDistance);
        }

        private float ComputeFramedOrbitDistance()
        {
            if (playerModel == null || enemyModel == null)
            {
                return profile.OrbitDistanceAtFar;
            }

            Vector3 screenRight = BattleFieldScreenAxis.ResolveScreenRight(smoothedHorizontalAngle);
            float separation = Mathf.Abs(Vector3.Dot(
                BattleFieldFocusResolver.FlattenY(enemyModel.position, battleGroundY)
                    - BattleFieldFocusResolver.FlattenY(playerModel.position, battleGroundY),
                screenRight));
            float halfVisibleWidth = ResolveHalfHorizontalVisibleWidth();
            if (halfVisibleWidth < 1e-4f)
            {
                return profile.OrbitDistanceAtFar;
            }

            float halfSpan = separation * 0.5f + profile.FramingSideMargin;
            return halfSpan / halfVisibleWidth;
        }

        private float ResolveHalfHorizontalVisibleWidth()
        {
            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            float halfVerticalRad = profile.VerticalFovDegrees * 0.5f * Mathf.Deg2Rad;
            float halfHorizontalRad = Mathf.Atan(Mathf.Tan(halfVerticalRad) * aspect);
            return Mathf.Tan(halfHorizontalRad);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            disposables.Dispose();
        }
    }
}
