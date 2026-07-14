using UnityEngine;

namespace Battle
{
    /// <summary>
    /// Zフロントのモデルをスポーン位置で互いに向かせる
    /// </summary>
    public static class BattleFieldFacing
    {
        /// <summary>
        /// プレイヤー位置から敵位置への水平方向を返す
        /// </summary>
        public static Vector3 ResolveAxisToEnemy(Vector3 playerPosition, Vector3 enemyPosition)
        {
            Vector3 separation = enemyPosition - playerPosition;
            separation.y = 0f;
            return separation.sqrMagnitude > 1e-6f ? separation.normalized : Vector3.forward;
        }

        /// <summary>
        /// 両モデルの回転を向き合わせる
        /// </summary>
        public static void ApplyBetween(Transform playerModel, Transform enemyModel, Transform playerSpawn, Transform enemySpawn)
        {
            Vector3 playerPosition = playerSpawn != null ? playerSpawn.position : Vector3.zero;
            Vector3 enemyPosition = enemySpawn != null ? enemySpawn.position : Vector3.zero;
            ApplyBetween(playerModel, enemyModel, playerPosition, enemyPosition);
        }

        /// <summary>
        /// 両モデルの回転を向き合わせる
        /// </summary>
        public static void ApplyBetween(
            Transform playerModel,
            Transform enemyModel,
            Vector3 playerPosition,
            Vector3 enemyPosition)
        {
            ApplyMatchupBetween(
                playerModel,
                enemyModel,
                playerPosition,
                enemyPosition,
                cameraHorizontalAngle: 0f,
                towardCameraDegrees: 0f);
        }

        /// <summary>
        /// 両モデルを互いに向かせつつカメラ方向へ少し回す
        /// </summary>
        /// <param name="cameraHorizontalAngle">対戦紹介カメラの水平角</param>
        /// <param name="towardCameraDegrees">カメラ方向へ回す最大角度</param>
        public static void ApplyMatchupBetween(
            Transform playerModel,
            Transform enemyModel,
            Vector3 playerPosition,
            Vector3 enemyPosition,
            float cameraHorizontalAngle,
            float towardCameraDegrees)
        {
            Vector3 axisToEnemy = ResolveAxisToEnemy(playerPosition, enemyPosition);
            if (axisToEnemy.sqrMagnitude < 1e-6f)
            {
                return;
            }

            Vector3 towardCamera = BattleFieldScreenAxis.ResolveTowardCamera(cameraHorizontalAngle);
            Vector3 playerForward = BlendFacing(axisToEnemy, towardCamera, towardCameraDegrees);
            Vector3 enemyForward = BlendFacing(-axisToEnemy, towardCamera, towardCameraDegrees);

            if (playerModel != null)
            {
                playerModel.rotation = Quaternion.LookRotation(playerForward, Vector3.up);
            }

            if (enemyModel != null)
            {
                enemyModel.rotation = Quaternion.LookRotation(enemyForward, Vector3.up);
            }
        }

        private static Vector3 BlendFacing(Vector3 fromDirection, Vector3 toDirection, float maxDegrees)
        {
            if (maxDegrees <= 0f)
            {
                return fromDirection.normalized;
            }

            Vector3 from = fromDirection.normalized;
            Vector3 to = toDirection.normalized;
            float angle = Vector3.Angle(from, to);
            if (angle < 1e-4f)
            {
                return from;
            }

            float t = Mathf.Clamp01(maxDegrees / angle);
            return Vector3.Slerp(from, to, t).normalized;
        }

        /// <summary>
        /// 単体モデルをカメラ方向へ向ける
        /// </summary>
        /// <param name="model">対象モデル</param>
        /// <param name="cameraHorizontalAngle">カメラ水平角</param>
        /// <param name="towardCameraDegrees">カメラ正面へ寄せる角度</param>
        public static void ApplyTowardCamera(
            Transform model,
            float cameraHorizontalAngle,
            float towardCameraDegrees)
        {
            if (model == null)
            {
                return;
            }

            Vector3 towardCamera = BattleFieldScreenAxis.ResolveTowardCamera(cameraHorizontalAngle);
            Vector3 forward = BlendFacing(model.forward, towardCamera, towardCameraDegrees);
            if (forward.sqrMagnitude < 1e-6f)
            {
                forward = towardCamera;
            }

            model.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        }
    }
}
