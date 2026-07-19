using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 勝利演出で勝者を歩きモーションのままその場回転させる
    /// </summary>
    public static class BattleVictoryWalkSpin
    {
        /// <summary>
        /// 既定の回転速度(度/秒)
        /// </summary>
        public const float DefaultDegreesPerSecond = 48f;

        /// <summary>
        /// 勝者に歩きモーションを開始する
        /// </summary>
        /// <param name="winner">勝者</param>
        public static void BeginWalk(BattleUnit winner)
        {
            winner?.PlayPresentationWalk();
        }

        /// <summary>
        /// モデルを水平軸まわりにその場回転させ続ける
        /// </summary>
        /// <param name="model">勝者モデル</param>
        /// <param name="degreesPerSecond">回転速度(度/秒)</param>
        /// <param name="cancellationToken">キャンセル</param>
        public static async UniTask SpinWhileAsync(
            Transform model,
            float degreesPerSecond,
            CancellationToken cancellationToken)
        {
            if (model == null || degreesPerSecond == 0f)
            {
                return;
            }

            Vector3 pivot = ResolveSpinPivot(model);

            while (!cancellationToken.IsCancellationRequested)
            {
                float deltaDegrees = degreesPerSecond * Time.unscaledDeltaTime;
                if (model != null)
                {
                    model.RotateAround(pivot, Vector3.up, deltaDegrees);
                }

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
        }

        private static Vector3 ResolveSpinPivot(Transform model)
        {
            Vector3 pivot = model.position;
            if (BattleFieldFocusResolver.TryGetCombinedRendererBounds(model, model, out Bounds bounds))
            {
                pivot = bounds.center;
                pivot.y = model.position.y;
            }

            return pivot;
        }
    }
}
