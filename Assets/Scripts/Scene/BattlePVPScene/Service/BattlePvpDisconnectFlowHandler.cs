using Cysharp.Threading.Tasks;
using Scene.BattlePVPScene.Interface;
using System;
using System.Threading;
using UnityEngine;

namespace Scene.BattlePVPScene.Service
{
    /// <summary>
    /// 通信切断検知からUI表示とタイトル遷移までをまとめる
    /// </summary>
    public sealed class BattlePvpDisconnectFlowHandler : IDisposable
    {
        private IBattlePvpDisconnectView disconnectView;
        private readonly BattlePvpDisconnectWatcher watcher = new BattlePvpDisconnectWatcher();
        private Func<CancellationToken, UniTask> returnToTitleAsync;
        private Func<CancellationToken> destroyTokenProvider;
        private Action stopBattleFlow;
        private Action releaseSceneFade;
        private bool isHandling;
        private bool isDisposed;
        private CancellationTokenSource subscribeRetryCts;

        /// <summary>
        /// 切断UIと監視を初期化する
        /// </summary>
        public BattlePvpDisconnectFlowHandler(IBattlePvpDisconnectView disconnectView)
        {
            this.disconnectView = disconnectView;
            watcher.Disconnected += OnDisconnected;
        }

        /// <summary>
        /// 切断時の戦闘停止とタイトル遷移を登録する
        /// </summary>
        public void Bind(
            Action stopBattleFlow,
            Func<CancellationToken, UniTask> returnToTitleAsync,
            Func<CancellationToken> destroyTokenProvider,
            Action releaseSceneFade = null)
        {
            this.stopBattleFlow = stopBattleFlow;
            this.returnToTitleAsync = returnToTitleAsync;
            this.destroyTokenProvider = destroyTokenProvider;
            this.releaseSceneFade = releaseSceneFade;
        }

        /// <summary>
        /// 切断UI参照を後から補完する
        /// </summary>
        public void SetDisconnectView(IBattlePvpDisconnectView view)
        {
            if (view != null)
            {
                disconnectView = view;
            }
        }

        /// <summary>
        /// 切断監視を開始する
        /// </summary>
        public void BeginMonitoring()
        {
            watcher.BeginMonitoring();
            StartSubscribeRetry();
        }

        /// <summary>
        /// 意図的な終了時に切断通知を抑止する
        /// </summary>
        public void SuppressNotifications()
        {
            StopSubscribeRetry();
            watcher.SuppressNotifications();
            watcher.EndMonitoring();
        }

        /// <summary>
        /// フロー側から切断相当を強制通知する
        /// </summary>
        public void ForceNotifyDisconnect()
        {
            watcher.ForceNotify();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            StopSubscribeRetry();
            watcher.Disconnected -= OnDisconnected;
            watcher.Dispose();
        }

        private void StartSubscribeRetry()
        {
            StopSubscribeRetry();
            subscribeRetryCts = new CancellationTokenSource();
            EnsureSubscribedAsync(subscribeRetryCts.Token).Forget();
        }

        private void StopSubscribeRetry()
        {
            if (subscribeRetryCts == null)
            {
                return;
            }

            subscribeRetryCts.Cancel();
            subscribeRetryCts.Dispose();
            subscribeRetryCts = null;
        }

        private async UniTaskVoid EnsureSubscribedAsync(CancellationToken cancellationToken)
        {
            try
            {
                for (int i = 0; i < 180; i++)
                {
                    if (cancellationToken.IsCancellationRequested || isDisposed)
                    {
                        return;
                    }

                    if (watcher.TryEnsureSubscribed())
                    {
                        return;
                    }

                    await UniTask.DelayFrame(1, cancellationToken: cancellationToken);
                }

                Debug.LogWarning("[BattlePvpDisconnect] NetworkManager購読に失敗しました");
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void OnDisconnected()
        {
            if (isHandling || isDisposed)
            {
                return;
            }

            isHandling = true;
            HandleDisconnectAsync().Forget();
        }

        private async UniTaskVoid HandleDisconnectAsync()
        {
            // 待機中のボタン待ちはフローCTSと切り離しオブジェクト破棄のみキャンセル
            CancellationToken destroyToken = destroyTokenProvider != null
                ? destroyTokenProvider.Invoke()
                : CancellationToken.None;

            try
            {
                Debug.LogWarning("[BattlePvpDisconnect] 切断UIを表示します");
                // 暗転の下に切断UIが埋もれないよう先にフェードを解除する
                releaseSceneFade?.Invoke();
                // 戦闘進行を止め待機ループが暗転を戻し続けないようにする
                stopBattleFlow?.Invoke();
                // 1フレーム空けてCanvasの再活性を安定させる
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);

                if (disconnectView != null)
                {
                    disconnectView.SetVisible(true);
                    try
                    {
                        await disconnectView.WaitReturnToTitleAsync(destroyToken);
                    }
                    finally
                    {
                        disconnectView.SetVisible(false);
                    }
                }
                else
                {
                    Debug.LogError("[BattlePvpDisconnect] 切断UIが未設定のため即Titleへ戻します");
                }

                watcher.SuppressNotifications();
                if (returnToTitleAsync != null)
                {
                    // 戻る遷移は破棄トークンが落ちていても必ず試行する
                    await returnToTitleAsync(CancellationToken.None);
                }
                else
                {
                    Debug.LogError("[BattlePvpDisconnect] タイトル遷移ハンドラが未登録です");
                }
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning("[BattlePvpDisconnect] 切断UI処理がキャンセルされました");
                await TryReturnToTitleFallbackAsync();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                await TryReturnToTitleFallbackAsync();
            }
            finally
            {
                isHandling = false;
            }
        }

        private async UniTask TryReturnToTitleFallbackAsync()
        {
            if (returnToTitleAsync == null)
            {
                return;
            }

            try
            {
                releaseSceneFade?.Invoke();
                watcher.SuppressNotifications();
                await returnToTitleAsync(CancellationToken.None);
            }
            catch (Exception fallbackException)
            {
                Debug.LogException(fallbackException);
            }
        }
    }
}
