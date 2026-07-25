using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 対戦紹介用に両モデルを横並びで配置する
    /// </summary>
    public static class BattleMatchupLayout
    {
        /// <summary>
        /// 対戦紹介ポイントへ配置し互いを向かせる
        /// </summary>
        public static void Apply(
            Transform playerModel,
            Transform enemyModel,
            Transform playerPoint,
            Transform enemyPoint,
            float cameraHorizontalAngle = 0f,
            float towardCameraDegrees = 0f)
        {
            if (playerModel == null || enemyModel == null)
            {
                return;
            }

            if (playerPoint != null)
            {
                Place(playerModel, playerPoint);
            }

            if (enemyPoint != null)
            {
                Place(enemyModel, enemyPoint);
            }

            Vector3 playerPosition = playerModel.position;
            Vector3 enemyPosition = enemyModel.position;
            BattleFieldFacing.ApplyMatchupBetween(
                playerModel,
                enemyModel,
                playerPosition,
                enemyPosition,
                cameraHorizontalAngle,
                towardCameraDegrees);
        }

        /// <summary>
        /// スポーン間の中間を基準に横並び位置を算出する
        /// </summary>
        public static void ApplyFromSpawns(
            Transform playerModel,
            Transform enemyModel,
            Transform playerSpawn,
            Transform enemySpawn,
            float sideSeparation,
            float cameraHorizontalAngle,
            float towardCameraDegrees = 0f)
        {
            if (playerModel == null || enemyModel == null)
            {
                return;
            }

            Vector3 playerHome = playerSpawn != null ? playerSpawn.position : Vector3.zero;
            Vector3 enemyHome = enemySpawn != null ? enemySpawn.position : Vector3.zero;
            Vector3 center = (playerHome + enemyHome) * 0.5f;
            center.y = Mathf.Max(playerHome.y, enemyHome.y);

            float half = Mathf.Max(0.5f, sideSeparation * 0.5f);
            Vector3 screenRight = BattleFieldScreenAxis.ResolveScreenRight(cameraHorizontalAngle);
            Vector3 screenLeft = -screenRight;

            Vector3 playerPosition = center + screenLeft * half;
            Vector3 enemyPosition = center + screenRight * half;
            float groundY = center.y;
            Quaternion playerRotation = playerSpawn != null ? playerSpawn.rotation : Quaternion.identity;
            Quaternion enemyRotation = enemySpawn != null ? enemySpawn.rotation : Quaternion.identity;

            BattleSpawnPlacement.ApplyAt(playerModel, playerPosition, playerRotation, groundY, playerSpawn);
            BattleSpawnPlacement.ApplyAt(enemyModel, enemyPosition, enemyRotation, groundY, enemySpawn);

            BattleFieldFacing.ApplyMatchupBetween(
                playerModel,
                enemyModel,
                playerModel.position,
                enemyModel.position,
                cameraHorizontalAngle,
                towardCameraDegrees);

            // 回転確定後にピボットずれを補正しないと見た目が片側へ寄る
            BattleSpawnPlacement.RecenterHorizontallyTo(playerModel, playerPosition);
            BattleSpawnPlacement.RecenterHorizontallyTo(enemyModel, enemyPosition);
            BattleSpawnPlacement.SnapBottomToGroundY(playerModel, groundY);
            BattleSpawnPlacement.SnapBottomToGroundY(enemyModel, groundY);
        }

        private static void Place(Transform model, Transform point)
        {
            BattleSpawnPlacement.Apply(model, point);
        }
    }
}
