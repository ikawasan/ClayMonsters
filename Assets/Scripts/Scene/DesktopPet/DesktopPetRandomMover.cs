using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Scene.DesktopPet.Interface;
using UnityEngine;

namespace Scene.DesktopPet
{
    /// <summary>
    /// デスクトップ上で窓位置をランダムに巡回させる
    /// </summary>
    public sealed class DesktopPetRandomMover
    {
        private readonly IDesktopPetWindow window;
        private readonly int windowWidth;
        private readonly int windowHeight;
        private readonly float minIdleSeconds;
        private readonly float maxIdleSeconds;
        private readonly float moveSeconds;
        private readonly Action<Vector2> onMoveDirection;
        private readonly Action onIdle;

        /// <summary>
        /// 移動制御を生成する
        /// </summary>
        public DesktopPetRandomMover(
            IDesktopPetWindow window,
            int windowWidth,
            int windowHeight,
            Action<Vector2> onMoveDirection,
            Action onIdle,
            float minIdleSeconds = 1.2f,
            float maxIdleSeconds = 3.5f,
            float moveSeconds = 2.4f)
        {
            this.window = window;
            this.windowWidth = windowWidth;
            this.windowHeight = windowHeight;
            this.onMoveDirection = onMoveDirection;
            this.onIdle = onIdle;
            this.minIdleSeconds = minIdleSeconds;
            this.maxIdleSeconds = maxIdleSeconds;
            this.moveSeconds = moveSeconds;
        }

        /// <summary>
        /// ランダム移動ループを開始する
        /// </summary>
        /// <param name="cancellationToken">中断トークン</param>
        public async UniTask RunAsync(CancellationToken cancellationToken)
        {
            Vector2Int current = ResolveInitialPosition();
            window.SetScreenPosition(current.x, current.y);
            onIdle?.Invoke();

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    Vector2Int target = PickRandomPosition();
                    Vector2 direction = new Vector2(target.x - current.x, target.y - current.y);
                    onMoveDirection?.Invoke(direction);
                    await MoveToAsync(current, target, cancellationToken);
                    current = target;
                    onIdle?.Invoke();

                    float idle = UnityEngine.Random.Range(minIdleSeconds, maxIdleSeconds);
                    await UniTask.Delay(
                        System.TimeSpan.FromSeconds(idle),
                        cancellationToken: cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async UniTask MoveToAsync(
            Vector2Int from,
            Vector2Int to,
            CancellationToken cancellationToken)
        {
            float elapsed = 0f;
            float duration = Mathf.Max(0.35f, moveSeconds);
            while (elapsed < duration)
            {
                cancellationToken.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                t = t * t * (3f - 2f * t);
                int x = Mathf.RoundToInt(Mathf.Lerp(from.x, to.x, t));
                int y = Mathf.RoundToInt(Mathf.Lerp(from.y, to.y, t));
                window.SetScreenPosition(x, y);
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            window.SetScreenPosition(to.x, to.y);
        }

        private Vector2Int ResolveInitialPosition()
        {
            return DesktopPetScreenBounds.CenterTopLeft(windowWidth, windowHeight);
        }

        private Vector2Int PickRandomPosition()
        {
            return DesktopPetScreenBounds.RandomTopLeft(windowWidth, windowHeight);
        }
    }
}
