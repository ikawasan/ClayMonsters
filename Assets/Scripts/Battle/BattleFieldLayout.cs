using Battle.Interface;
using ClayEditor.Rigging;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 戦闘の間合い値をフィールド上のモデル配置へ反映する
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
        private float lastAppliedDistance = float.NaN;

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
            playerClosureOffset = Mathf.Max(0f, playerClosureOffset);
            enemyClosureOffset = Mathf.Max(0f, enemyClosureOffset);

            float maxTotalClosure = ComputeClosure(0f, maxDistance);
            float sum = playerClosureOffset + enemyClosureOffset;
            if (sum > maxTotalClosure + 1e-4f)
            {
                float scale = maxTotalClosure / sum;
                playerClosureOffset *= scale;
                enemyClosureOffset *= scale;
            }

            distance = ComputeDistanceFromOffsets(maxDistance);
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
        /// 間合いに応じて両モデルを移動する各ユニットの移動意図を独立反映する
        /// </summary>
        public void ApplyDistance(
            float distance,
            float maxDistance,
            float deltaTime,
            int playerMovementIntent,
            int enemyMovementIntent,
            float playerMoveSpeed,
            float enemyMoveSpeed,
            bool isPlayerStepping = false,
            int playerStepIntent = 0,
            bool isEnemyStepping = false,
            int enemyStepIntent = 0)
        {
            float totalClosure = ComputeClosure(distance, maxDistance);
            int effectivePlayerIntent = isPlayerStepping ? playerStepIntent : playerMovementIntent;
            int effectiveEnemyIntent = isEnemyStepping ? enemyStepIntent : enemyMovementIntent;

            if (float.IsNaN(lastAppliedDistance))
            {
                playerClosureOffset = 0f;
                enemyClosureOffset = 0f;
                SyncClosureOffsetsToTotal(totalClosure);
            }
            else
            {
                float distanceDelta = distance - lastAppliedDistance;

                if (isPlayerStepping && Mathf.Abs(distanceDelta) > 1e-6f)
                {
                    ApplyClosureDelta(
                        ComputeClosure(distance, maxDistance) - ComputeClosure(lastAppliedDistance, maxDistance),
                        effectivePlayerIntent,
                        0,
                        forcePlayerOnly: true);
                }
                else if (isEnemyStepping && Mathf.Abs(distanceDelta) > 1e-6f)
                {
                    ApplyClosureDelta(
                        ComputeClosure(distance, maxDistance) - ComputeClosure(lastAppliedDistance, maxDistance),
                        0,
                        enemyStepIntent,
                        forcePlayerOnly: false,
                        forceEnemyOnly: true);
                }
                else if (!isPlayerStepping
                    && !isEnemyStepping
                    && Mathf.Abs(distanceDelta) > 1e-6f)
                {
                    ApplyClosureDelta(
                        ComputeClosure(distance, maxDistance) - ComputeClosure(lastAppliedDistance, maxDistance),
                        0,
                        0,
                        false);
                }
            }

            if (isPlayerStepping
                || isEnemyStepping
                || (!float.IsNaN(lastAppliedDistance)
                    && Mathf.Abs(distance - lastAppliedDistance) > 1e-4f))
            {
                ClampClosureOffsetsToDistance(
                    distance,
                    maxDistance,
                    effectivePlayerIntent,
                    effectiveEnemyIntent,
                    isPlayerStepping,
                    isEnemyStepping);
            }
            playerClosureOffset = Mathf.Max(0f, playerClosureOffset);
            enemyClosureOffset = Mathf.Max(0f, enemyClosureOffset);

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

            lastAppliedDistance = distance;
            ClampPlayerApproachSide();
            ApplyFacing();
        }

        private void ApplyClosureDelta(
            float deltaClosure,
            int playerMovementIntent,
            int enemyMovementIntent,
            bool forcePlayerOnly,
            bool forceEnemyOnly = false)
        {
            if (Mathf.Abs(deltaClosure) <= 1e-6f)
            {
                return;
            }

            bool approaching = deltaClosure > 0f;
            ResolveMovementWeights(
                playerMovementIntent,
                enemyMovementIntent,
                approaching,
                out float playerWeight,
                out float enemyWeight);

            if (forcePlayerOnly)
            {
                playerWeight = 1f;
                enemyWeight = 0f;
            }

            if (forceEnemyOnly)
            {
                playerWeight = 0f;
                enemyWeight = 1f;
            }

            if (!approaching)
            {
                playerClosureOffset += deltaClosure * playerWeight;
                enemyClosureOffset += deltaClosure * enemyWeight;
                return;
            }

            if (!forcePlayerOnly && !forceEnemyOnly)
            {
                RedirectOpeningWeightsAtHome(ref playerWeight, ref enemyWeight);
            }

            playerClosureOffset += deltaClosure * playerWeight;
            enemyClosureOffset += deltaClosure * enemyWeight;
        }

        private void RedirectOpeningWeightsAtHome(ref float playerWeight, ref float enemyWeight)
        {
            if (playerClosureOffset <= 1e-6f && playerWeight > 1e-6f)
            {
                enemyWeight += playerWeight;
                playerWeight = 0f;
            }

            if (enemyClosureOffset <= 1e-6f && enemyWeight > 1e-6f)
            {
                playerWeight += enemyWeight;
                enemyWeight = 0f;
            }
        }

        private void ClampClosureOffsetsToDistance(
            float distance,
            float maxDistance,
            int playerMovementIntent,
            int enemyMovementIntent,
            bool isPlayerStepping,
            bool isEnemyStepping)
        {
            float totalClosure = ComputeClosure(distance, maxDistance);
            if (totalClosure <= 1e-6f)
            {
                playerClosureOffset = 0f;
                enemyClosureOffset = 0f;
                return;
            }

            float sum = playerClosureOffset + enemyClosureOffset;
            if (sum <= 1e-6f)
            {
                playerClosureOffset = totalClosure * 0.5f;
                enemyClosureOffset = totalClosure * 0.5f;
                return;
            }

            if (sum > totalClosure + 1e-4f)
            {
                float excess = sum - totalClosure;
                if (TryAbsorbClosureExcess(
                    excess,
                    playerMovementIntent,
                    enemyMovementIntent,
                    opening: true,
                    isPlayerStepping,
                    isEnemyStepping))
                {
                    return;
                }

                float scale = totalClosure / sum;
                playerClosureOffset *= scale;
                enemyClosureOffset *= scale;
            }
            else if (sum < totalClosure - 1e-4f)
            {
                float deficit = totalClosure - sum;
                if (TryAbsorbClosureExcess(
                    deficit,
                    playerMovementIntent,
                    enemyMovementIntent,
                    opening: false,
                    isPlayerStepping,
                    isEnemyStepping))
                {
                    return;
                }

                float scale = totalClosure / sum;
                playerClosureOffset *= scale;
                enemyClosureOffset *= scale;
            }
        }

        private bool TryAbsorbClosureExcess(
            float amount,
            int playerMovementIntent,
            int enemyMovementIntent,
            bool opening,
            bool isPlayerStepping,
            bool isEnemyStepping)
        {
            if (amount <= 1e-6f)
            {
                return true;
            }

            if (isPlayerStepping)
            {
                if (opening)
                {
                    playerClosureOffset = Mathf.Max(0f, playerClosureOffset - amount);
                }
                else
                {
                    playerClosureOffset += amount;
                }

                return true;
            }

            if (isEnemyStepping)
            {
                if (opening)
                {
                    enemyClosureOffset = Mathf.Max(0f, enemyClosureOffset - amount);
                }
                else
                {
                    enemyClosureOffset += amount;
                }

                return true;
            }

            bool playerRetreating = playerMovementIntent > 0;
            bool enemyRetreating = enemyMovementIntent > 0;
            bool playerApproaching = playerMovementIntent < 0;
            bool enemyApproaching = enemyMovementIntent < 0;

            if (opening)
            {
                if (playerClosureOffset <= 1e-6f && !playerRetreating)
                {
                    enemyClosureOffset = Mathf.Max(0f, enemyClosureOffset - amount);
                    return true;
                }

                if (enemyClosureOffset <= 1e-6f && !enemyRetreating)
                {
                    playerClosureOffset = Mathf.Max(0f, playerClosureOffset - amount);
                    return true;
                }

                if (playerRetreating && !enemyRetreating && enemyClosureOffset > 1e-6f)
                {
                    float absorbed = Mathf.Min(amount, enemyClosureOffset);
                    enemyClosureOffset -= absorbed;
                    return absorbed >= amount - 1e-4f;
                }

                if (enemyRetreating && !playerRetreating && playerClosureOffset > 1e-6f)
                {
                    float absorbed = Mathf.Min(amount, playerClosureOffset);
                    playerClosureOffset -= absorbed;
                    return absorbed >= amount - 1e-4f;
                }
            }
            else
            {
                if (playerApproaching && !enemyApproaching)
                {
                    playerClosureOffset += amount;
                    return true;
                }

                if (enemyApproaching && !playerApproaching)
                {
                    enemyClosureOffset += amount;
                    return true;
                }
            }

            return false;
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

        private void SyncClosureOffsetsToTotal(float totalClosure)
        {
            if (totalClosure <= 1e-6f)
            {
                playerClosureOffset = 0f;
                enemyClosureOffset = 0f;
                return;
            }

            float sum = playerClosureOffset + enemyClosureOffset;
            if (sum <= 1e-6f)
            {
                playerClosureOffset = totalClosure * 0.5f;
                enemyClosureOffset = totalClosure * 0.5f;
                return;
            }

            if (!Mathf.Approximately(sum, totalClosure))
            {
                float scale = totalClosure / sum;
                playerClosureOffset *= scale;
                enemyClosureOffset *= scale;
            }
        }

        private static void ResolveMovementWeights(
            int playerMovementIntent,
            int enemyMovementIntent,
            bool forApproach,
            out float playerWeight,
            out float enemyWeight)
        {
            float playerDrive = 0f;
            float enemyDrive = 0f;

            if (playerMovementIntent < 0)
            {
                playerDrive = 1f;
            }
            else if (playerMovementIntent > 0)
            {
                playerDrive = -1f;
            }

            if (enemyMovementIntent < 0)
            {
                enemyDrive = 1f;
            }
            else if (enemyMovementIntent > 0)
            {
                enemyDrive = -1f;
            }

            float closePlayer = Mathf.Max(0f, playerDrive);
            float closeEnemy = Mathf.Max(0f, enemyDrive);
            float openPlayer = Mathf.Max(0f, -playerDrive);
            float openEnemy = Mathf.Max(0f, -enemyDrive);
            float playerShare = forApproach ? closePlayer : openPlayer;
            float enemyShare = forApproach ? closeEnemy : openEnemy;
            float shareSum = playerShare + enemyShare;

            if (shareSum > 1e-6f)
            {
                playerWeight = playerShare / shareSum;
                enemyWeight = enemyShare / shareSum;
                return;
            }

            if (playerMovementIntent != 0 && enemyMovementIntent == 0)
            {
                playerWeight = 1f;
                enemyWeight = 0f;
                return;
            }

            if (enemyMovementIntent != 0 && playerMovementIntent == 0)
            {
                playerWeight = 0f;
                enemyWeight = 1f;
                return;
            }

            playerWeight = 0.5f;
            enemyWeight = 0.5f;
        }

        /// <summary>
        /// 本戦開始時の間合いでモデルを配置する
        /// </summary>
        public void ApplyInitialBattlePositions(float distance, float maxDistance)
        {
            playerClosureOffset = 0f;
            enemyClosureOffset = 0f;
            lastAppliedDistance = float.NaN;
            ApplyDistance(distance, maxDistance, 0f, 0, 0, 0f, 0f);
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
