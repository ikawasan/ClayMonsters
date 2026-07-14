using Cysharp.Threading.Tasks;
using Scene.BattlePVPScene.Interface;
using System;
using System.Threading;
using UnityEngine;

namespace Scene.BattlePVPScene.Service
{
    /// <summary>
    /// 通信切断検知からUI表示とタイトル遷移までをまとめる
    /// PvPフロー中の切断時に戦闘停止と戻り導線を提供する
    /// </summary>
    public sealed class BattlePvpDisconnectFlowHandler : IDisposable
    {
        private readonly IBattlePvpDisconnectView disconnectView;
        private readonly BattlePvpDisconnectWatcher watcher = new BattlePvpDisconnectWatcher();
        private Func<CancellationToken, UniTask> returnToTitleAsync;
        private Func<CancellationToken> destroyTokenProvider;
        private Action stopBattleFlow;
        private bool isHandling;

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
            Func<CancellationToken> destroyTokenProvider)
        {
            this.stopBattleFlow = stopBattleFlow;
            this.returnToTitleAsync = returnToTitleAsync;
            this.destroyTokenProvider = destroyTokenProvider;
        }

        /// <summary>
        /// 切断監視を開始する
        /// </summary>
        public void BeginMonitoring()
        {
            watcher.BeginMonitoring();
        }

        /// <summary>
        /// 意図的な終了時に切断通知を抑止する
        /// </summary>
        public void SuppressNotifications()
        {
            watcher.SuppressNotifications();
            watcher.EndMonitoring();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            watcher.Disconnected -= OnDisconnected;
            watcher.Dispose();
        }

        private void OnDisconnected()
        {
            if (isHandling)
            {
                return;
            }

            isHandling = true;
            HandleDisconnectAsync().Forget();
        }

        private async UniTaskVoid HandleDisconnectAsync()
        {
            try
            {
                stopBattleFlow?.Invoke();

                if (disconnectView != null)
                {
                    disconnectView.SetVisible(true);
                    CancellationToken token = destroyTokenProvider != null
                        ? destroyTokenProvider.Invoke()
                        : CancellationToken.None;
                    await disconnectView.WaitReturnToTitleAsync(token);
                    disconnectView.SetVisible(false);
                }

                watcher.SuppressNotifications();
                if (returnToTitleAsync != null)
                {
                    await returnToTitleAsync(CancellationToken.None);
                }
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning("[BattlePvpDisconnect] 切断UI処理がキャンセルされました");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                isHandling = false;
            }
        }
    }
}
