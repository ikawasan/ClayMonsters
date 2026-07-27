using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 勝利演出で勝者を歩きモーションのままその場回転させつつ軽く跳ねさせる
    /// </summary>
    public static class BattleVictoryWalkSpin
    {
        /// <summary>
        /// 既定の回転速度(度/秒)
        /// </summary>
        public const float DefaultDegreesPerSecond = 48f;

        /// <summary>
        /// 既定の跳ね高さ
        /// </summary>
        public const float DefaultHopHeight = 0.07f;

        /// <summary>
        /// 既定の跳ね周波数(回/秒)
        /// </summary>
        public const float DefaultHopFrequency = 2.4f;

        /// <summary>
        /// 勝者に歩きモーションを開始する
        /// </summary>
        /// <param name="winner">勝者</param>
        public static void BeginWalk(BattleUnit winner)
        {
            winner?.PlayPresentationWalk();
        }

        /// <summary>
        /// モデルを水平軸まわりにその場回転させ続け軽く跳ねさせる
        /// </summary>
        /// <param name="model">勝者モデル</param>
        /// <param name="degreesPerSecond">回転速度(度/秒)</param>
        /// <param name="cancellationToken">キャンセル</param>
        public static async UniTask SpinWhileAsync(
            Transform model,
            float degreesPerSecond,
            CancellationToken cancellationToken)
        {
            await SpinWhileAsync(
                model,
                degreesPerSecond,
                BattleSpawnPlacement.ScaleAuthoredLength(DefaultHopHeight),
                DefaultHopFrequency,
                cancellationToken);
        }

        /// <summary>
        /// モデルを水平軸まわりにその場回転させ続け軽く跳ねさせる
        /// </summary>
        /// <param name="model">勝者モデル</param>
        /// <param name="degreesPerSecond">回転速度(度/秒)</param>
        /// <param name="hopHeight">跳ね高さ</param>
        /// <param name="hopFrequency">跳ね周波数(回/秒)</param>
        /// <param name="cancellationToken">キャンセル</param>
        public static async UniTask SpinWhileAsync(
            Transform model,
            float degreesPerSecond,
            float hopHeight,
            float hopFrequency,
            CancellationToken cancellationToken)
        {
            if (model == null)
            {
                return;
            }

            Vector3 pivot = ResolveSpinPivot(model);
            float baseY = model.position.y;
            pivot.y = baseY;
            float elapsed = 0f;
            float safeHopHeight = Mathf.Max(0f, hopHeight);
            float safeHopFrequency = Mathf.Max(0.01f, hopFrequency);

            while (!cancellationToken.IsCancellationRequested)
            {
                float deltaTime = Time.unscaledDeltaTime;
                elapsed += deltaTime;

                if (model == null)
                {
                    return;
                }

                if (degreesPerSecond != 0f)
                {
                    model.RotateAround(pivot, Vector3.up, degreesPerSecond * deltaTime);
                }

                // 地面に着いてからまた跳ねるAbs(Sin)でぴょんぴょんさせる
                float hop = Mathf.Abs(Mathf.Sin(elapsed * safeHopFrequency * Mathf.PI)) * safeHopHeight;
                Vector3 position = model.position;
                position.y = baseY + hop;
                model.position = position;

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
