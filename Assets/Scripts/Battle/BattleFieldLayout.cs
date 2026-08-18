using Battle.Interface;
using ClayEditor.Rigging;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 各ユニットの移動オフセットをフィールド上のモデル配置へ反映する
    /// 間合いはオフセットから算出する結果値であり配置の入力には使わない
    /// </summary>
    public sealed class BattleFieldLayout : IBattleFieldMovement
    {
        private readonly Transform playerModel;
        private readonly Transform enemyModel;
        private readonly Vector3 playerHomePosition;
        private readonly Vector3 enemyHomePosition;
        private readonly Vector3 approachAxisFromEnemy;
        private readonly float maxSeparation;
        private readonly float minCloseSeparation;
        private float playerClosureOffset;
        private float enemyClosureOffset;
        private float playerStepStartOffset;
        private float playerStepTargetOffset;
        private float enemyStepStartOffset;
        private float enemyStepTargetOffset;

        /// <inheritdoc/>
        public bool IsPlayerAtHomeBoundary => playerClosureOffset <= 1e-6f;

        /// <inheritdoc/>
        public bool IsEnemyAtHomeBoundary => enemyClosureOffset <= 1e-6f;

        /// <summary>
        /// スポーン位置とモデル参照からレイアウトを構築する
        /// </summary>
        public BattleFieldLayout(
            Transform playerModel,
            Transform enemyModel,
            Transform playerSpawn,
            Transform enemySpawn,
            float cameraHorizontalAngle = 0f,
            float minCloseSeparation = 0.6f)
        {
            this.playerModel = playerModel;
            this.enemyModel = enemyModel;
            this.minCloseSeparation = Mathf.Max(0.1f, minCloseSeparation);

            Vector3 playerSpawnPosition = playerSpawn != null ? playerSpawn.position : Vector3.zero;
            Vector3 enemySpawnPosition = enemySpawn != null ? enemySpawn.position : Vector3.zero;

            playerHomePosition = playerSpawnPosition;
            enemyHomePosition = enemySpawnPosition;

            Vector3 axis = playerHomePosition - enemyHomePosition;
            axis.y = 0f;
            maxSeparation = axis.magnitude;
            if (maxSeparation > 1e-4f)
            {
                approachAxisFromEnemy = axis / maxSeparation;
            }
            else
            {
                Vector3 screenLeft = -BattleFieldScreenAxis.ResolveScreenRight(cameraHorizontalAngle);
                approachAxisFromEnemy = screenLeft.sqrMagnitude > 1e-6f ? screenLeft.normalized : Vector3.left;
                maxSeparation = 0f;
            }
        }

        /// <inheritdoc/>
        public void ApplyIndependentMovement(
            float deltaTime,
            int playerMovementIntent,
            int enemyMovementIntent,
            float playerMoveSpeed,
            float enemyMoveSpeed,
            float maxDistance,
            out float distance)
        {
            ApplyOffsetMovement(ref playerClosureOffset, playerMovementIntent, playerMoveSpeed * deltaTime);
            ApplyOffsetMovement(ref enemyClosureOffset, enemyMovementIntent, enemyMoveSpeed * deltaTime);
            ClampOffsets(maxDistance, playerMovementIntent, enemyMovementIntent);
            distance = ComputeDistanceFromOffsets(maxDistance);
        }

        /// <inheritdoc/>
        public void BeginPlayerStep(float startDistance, float targetDistance, float maxDistance)
        {
            float closureDelta = ComputeClosure(targetDistance, maxDistance) - ComputeClosure(startDistance, maxDistance);
            playerStepStartOffset = playerClosureOffset;
            playerStepTargetOffset = Mathf.Max(0f, playerClosureOffset + closureDelta);
        }

        /// <inheritdoc/>
        public void BeginEnemyStep(float startDistance, float targetDistance, float maxDistance)
        {
            float closureDelta = ComputeClosure(targetDistance, maxDistance) - ComputeClosure(startDistance, maxDistance);
            enemyStepStartOffset = enemyClosureOffset;
            enemyStepTargetOffset = Mathf.Max(0f, enemyClosureOffset + closureDelta);
        }

        /// <inheritdoc/>
        public void SetPlayerStepProgress(float easedT01, float maxDistance, out float distance)
        {
            playerClosureOffset = Mathf.Lerp(playerStepStartOffset, playerStepTargetOffset, Mathf.Clamp01(easedT01));
            ClampOffsetsPreferringPlayer(maxDistance);
            distance = ComputeDistanceFromOffsets(maxDistance);
        }

        /// <inheritdoc/>
        public void SetEnemyStepProgress(float easedT01, float maxDistance, out float distance)
        {
            enemyClosureOffset = Mathf.Lerp(enemyStepStartOffset, enemyStepTargetOffset, Mathf.Clamp01(easedT01));
            ClampOffsetsPreferringEnemy(maxDistance);
            distance = ComputeDistanceFromOffsets(maxDistance);
        }

        /// <inheritdoc/>
        public void PushEnemyAway(
            float openDistanceAmount,
            float currentDistance,
            float maxDistance,
            out float distance)
        {
            ApplySingleUnitOpen(ref enemyClosureOffset, openDistanceAmount, currentDistance, maxDistance, out distance);
        }

        /// <inheritdoc/>
        public void PushPlayerAway(
            float openDistanceAmount,
            float currentDistance,
            float maxDistance,
            out float distance)
        {
            ApplySingleUnitOpen(ref playerClosureOffset, openDistanceAmount, currentDistance, maxDistance, out distance);
        }

        /// <inheritdoc/>
        public float ComputeDistance(float maxDistance) => ComputeDistanceFromOffsets(maxDistance);

        /// <inheritdoc/>
        public void ApplyModelTransforms()
        {
            Vector3 playerPosition = playerHomePosition - approachAxisFromEnemy * playerClosureOffset;
            Vector3 enemyPosition = enemyHomePosition + approachAxisFromEnemy * enemyClosureOffset;

            if (playerModel != null)
            {
                playerPosition.y = playerHomePosition.y;
                playerModel.SetPositionAndRotation(playerPosition, playerModel.rotation);
                BattleSpawnPlacement.SnapBottomToGroundY(playerModel, playerHomePosition.y);
            }

            if (enemyModel != null)
            {
                enemyPosition.y = enemyHomePosition.y;
                enemyModel.SetPositionAndRotation(enemyPosition, enemyModel.rotation);
                BattleSpawnPlacement.SnapBottomToGroundY(enemyModel, enemyHomePosition.y);
            }

            ClampPlayerApproachSide();
            ApplyFacing();
        }

        /// <summary>
        /// 両ユニットの戦闘モーション制約と跳び乗り目標を設定する
        /// </summary>
        public void ConfigureCombatMotions(
            ProceduralMotionCharacter playerMotion,
            ProceduralMotionCharacter enemyMotion)
        {
            ConfigurePositionConstraint(playerMotion);
            playerMotion?.ConfigureBattleAttackTarget(enemyModel);
            enemyMotion?.ConfigureBattleAttackTarget(playerModel);
        }

        /// <summary>
        /// 戦闘中にプレイヤーが敵の右側へはみ出さないようモーションへ制約を渡す
        /// </summary>
        public void ConfigurePositionConstraint(ProceduralMotionCharacter playerMotion)
        {
            if (playerMotion == null)
            {
                return;
            }

            playerMotion.ConfigureBattlePositionConstraint(
                enemyModel,
                approachAxisFromEnemy,
                minCloseSeparation,
                maxSeparation);
        }

        /// <summary>
        /// 両ユニットをホーム位置へ戻す
        /// </summary>
        public void ApplyInitialBattlePositions(float distance, float maxDistance)
        {
            _ = distance;
            playerClosureOffset = 0f;
            enemyClosureOffset = 0f;
            ApplyModelTransforms();
        }

        /// <summary>
        /// プレイヤーが敵の右側へ入らないよう現在位置を補正する
        /// </summary>
        public void ClampPlayerApproachSide()
        {
            if (playerModel == null || enemyModel == null)
            {
                return;
            }

            ClampPlayerApproachSide(
                playerModel,
                enemyModel.position,
                approachAxisFromEnemy,
                minCloseSeparation,
                maxSeparation);
        }

        /// <summary>
        /// プレイヤーが敵の右側へ入らないよう位置を補正する
        /// </summary>
        public static void ClampPlayerApproachSide(
            Transform player,
            Vector3 enemyPosition,
            Vector3 approachAxisFromEnemy,
            float minSeparation,
            float maxSeparation)
        {
            if (player == null)
            {
                return;
            }

            Vector3 axis = approachAxisFromEnemy;
            axis.y = 0f;
            if (axis.sqrMagnitude < 1e-6f)
            {
                return;
            }

            axis.Normalize();
            Vector3 enemyPos = enemyPosition;
            enemyPos.y = 0f;

            Vector3 playerPos = player.position;
            float groundY = enemyPosition.y;
            playerPos.y = 0f;

            float along = Vector3.Dot(playerPos - enemyPos, axis);
            float minAlong = Mathf.Max(0.1f, minSeparation);
            float maxAlong = maxSeparation > 1e-4f ? maxSeparation : minAlong;
            float clampedAlong = Mathf.Clamp(along, minAlong, maxAlong);
            if (Mathf.Approximately(along, clampedAlong))
            {
                return;
            }

            Vector3 corrected = enemyPos + axis * clampedAlong;
            corrected.y = groundY;
            player.position = corrected;
        }

        private void ApplySingleUnitOpen(
            ref float closureOffset,
            float openDistanceAmount,
            float currentDistance,
            float maxDistance,
            out float distance)
        {
            float targetDistance = Mathf.Min(maxDistance, currentDistance + Mathf.Max(0f, openDistanceAmount));
            float closureDelta = ComputeClosure(targetDistance, maxDistance) - ComputeClosure(currentDistance, maxDistance);
            closureOffset = Mathf.Max(0f, closureOffset + closureDelta);
            distance = ComputeDistanceFromOffsets(maxDistance);
        }

        private void ClampOffsets(float maxDistance, int playerMovementIntent, int enemyMovementIntent)
        {
            playerClosureOffset = Mathf.Max(0f, playerClosureOffset);
            enemyClosureOffset = Mathf.Max(0f, enemyClosureOffset);

            float maxTotalClosure = ComputeClosure(0f, maxDistance);
            float excess = playerClosureOffset + enemyClosureOffset - maxTotalClosure;
            if (excess <= 1e-4f)
            {
                return;
            }

            bool playerApproaching = playerMovementIntent < 0;
            bool enemyApproaching = enemyMovementIntent < 0;
            if (playerApproaching && !enemyApproaching)
            {
                AbsorbExcess(ref playerClosureOffset, ref enemyClosureOffset, excess);
                return;
            }

            if (enemyApproaching && !playerApproaching)
            {
                AbsorbExcess(ref enemyClosureOffset, ref playerClosureOffset, excess);
                return;
            }

            AbsorbExcess(ref playerClosureOffset, ref enemyClosureOffset, excess);
        }

        private void ClampOffsetsPreferringPlayer(float maxDistance)
        {
            playerClosureOffset = Mathf.Max(0f, playerClosureOffset);
            enemyClosureOffset = Mathf.Max(0f, enemyClosureOffset);
            float excess = playerClosureOffset + enemyClosureOffset - ComputeClosure(0f, maxDistance);
            if (excess > 1e-4f)
            {
                AbsorbExcess(ref playerClosureOffset, ref enemyClosureOffset, excess);
            }
        }

        private void ClampOffsetsPreferringEnemy(float maxDistance)
        {
            playerClosureOffset = Mathf.Max(0f, playerClosureOffset);
            enemyClosureOffset = Mathf.Max(0f, enemyClosureOffset);
            float excess = playerClosureOffset + enemyClosureOffset - ComputeClosure(0f, maxDistance);
            if (excess > 1e-4f)
            {
                AbsorbExcess(ref enemyClosureOffset, ref playerClosureOffset, excess);
            }
        }

        private static void AbsorbExcess(ref float primary, ref float secondary, float excess)
        {
            float absorbed = Mathf.Min(excess, primary);
            primary -= absorbed;
            excess -= absorbed;
            if (excess > 1e-4f)
            {
                secondary = Mathf.Max(0f, secondary - excess);
            }
        }

        private static void ApplyOffsetMovement(ref float closureOffset, int movementIntent, float delta)
        {
            if (movementIntent < 0)
            {
                closureOffset += Mathf.Abs(delta);
            }
            else if (movementIntent > 0)
            {
                closureOffset = Mathf.Max(0f, closureOffset - Mathf.Abs(delta));
            }
        }

        private float ComputeDistanceFromOffsets(float maxDistance)
        {
            float totalClosure = playerClosureOffset + enemyClosureOffset;
            if (totalClosure <= 1e-6f)
            {
                return maxDistance;
            }

            float maxSep = Mathf.Max(maxSeparation, minCloseSeparation);
            float separation = maxSeparation - totalClosure;
            float span = maxSep - minCloseSeparation;
            if (span <= 1e-6f)
            {
                return 0f;
            }

            float ratio = Mathf.Clamp01((separation - minCloseSeparation) / span);
            return ratio * maxDistance;
        }

        private float ComputeClosure(float distance, float maxDistance)
        {
            float ratio = maxDistance > 0f ? Mathf.Clamp01(distance / maxDistance) : 0f;
            float maxSep = Mathf.Max(maxSeparation, minCloseSeparation);
            float separation = Mathf.Lerp(minCloseSeparation, maxSep, ratio);
            return Mathf.Max(0f, maxSeparation - separation);
        }

        private void ApplyFacing()
        {
            if (playerModel == null || enemyModel == null)
            {
                return;
            }

            BattleFieldFacing.ApplyBetween(
                playerModel,
                enemyModel,
                playerModel.position,
                enemyModel.position);
        }
    }
}
