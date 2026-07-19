using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 勝利演出用に勝者モデルをステージ中央へ配置し敗者を隠す
    /// </summary>
    public static class BattleVictoryLayout
    {
        /// <summary>
        /// 勝者だけをステージ中央へ配置しカメラ方向を向かせる
        /// </summary>
        /// <param name="context">演出コンテキスト</param>
        /// <param name="winner">勝者ユニット</param>
        /// <param name="cameraHorizontalAngle">カメラ水平角</param>
        /// <param name="towardCameraDegrees">カメラ方向へ回す角度</param>
        public static void Apply(
            BattleStagingContext context,
            BattleUnit winner,
            float cameraHorizontalAngle,
            float towardCameraDegrees)
        {
            if (context == null || winner == null)
            {
                return;
            }

            Transform winnerModel = ResolveModel(context, winner);
            Transform loserModel = ResolveLoserModel(context, winner);
            if (winnerModel == null)
            {
                return;
            }

            if (loserModel != null)
            {
                loserModel.gameObject.SetActive(false);
            }

            winnerModel.gameObject.SetActive(true);

            // 欠損部位を戻しIdle基準ポーズへ揃えてから足元を地面に合わせる
            winner.RestoreAllParts();
            winner.PreparePresentationIdle();

            Vector3 center = context.ResolveCenterPosition();
            float groundY = ResolveGroundY(context);
            center.y = groundY;

            Vector3 towardCamera = BattleFieldScreenAxis.ResolveTowardCamera(cameraHorizontalAngle);
            Quaternion facing = towardCamera.sqrMagnitude > 1e-6f
                ? Quaternion.LookRotation(towardCamera, Vector3.up)
                : Quaternion.identity;

            BattleSpawnPlacement.ApplyAt(winnerModel, center, facing, groundY);
            RecenterHorizontally(winnerModel, center);
            BattleSpawnPlacement.SnapBottomToGroundY(winnerModel, groundY);
            winner.SyncMotionLayoutPosition();
            BattleVictoryWalkSpin.BeginWalk(winner);
        }

        // モデルのピボットずれを補正し見た目の中心を目標へ合わせる
        private static void RecenterHorizontally(Transform model, Vector3 targetCenter)
        {
            if (model == null
                || !BattleFieldFocusResolver.TryGetCombinedRendererBounds(model, model, out Bounds bounds))
            {
                return;
            }

            Vector3 offset = model.position - bounds.center;
            Vector3 corrected = targetCenter + offset;
            corrected.y = model.position.y;
            model.position = corrected;
        }

        private static Transform ResolveModel(BattleStagingContext context, BattleUnit unit)
        {
            if (unit == context.Player)
            {
                return context.PlayerModel;
            }

            if (unit == context.Enemy)
            {
                return context.EnemyModel;
            }

            return null;
        }

        private static Transform ResolveLoserModel(BattleStagingContext context, BattleUnit winner)
        {
            if (winner == context.Player)
            {
                return context.EnemyModel;
            }

            if (winner == context.Enemy)
            {
                return context.PlayerModel;
            }

            return null;
        }

        private static float ResolveGroundY(BattleStagingContext context)
        {
            float playerY = context.PlayerSpawn != null ? context.PlayerSpawn.position.y : 0f;
            float enemyY = context.EnemySpawn != null ? context.EnemySpawn.position.y : 0f;
            return Mathf.Max(playerY, enemyY);
        }
    }
}
