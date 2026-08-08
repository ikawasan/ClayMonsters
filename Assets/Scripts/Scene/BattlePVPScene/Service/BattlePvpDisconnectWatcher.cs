using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using Unity.Netcode;
using UnityEngine;

namespace Scene.BattlePVPScene.Service
{
    /// <summary>
    /// Netcodeの切断を検知して通知する
    /// コールバック漏れ対策として接続数ポーリングも行う
    /// </summary>
    public sealed class BattlePvpDisconnectWatcher : IDisposable
    {
        private const float PollIntervalSeconds = 0.2f;

        private bool isMonitoring;
        private bool suppressNotifications;
        private bool hasNotified;
        private bool isSubscribed;
        private bool hasSeenPeer;
        private CancellationTokenSource pollCts;

        /// <summary>
        /// 相手またはサーバーとの通信が切れた
        /// </summary>
        public event Action Disconnected;

        /// <summary>
        /// 切断監視を開始する
        /// </summary>
        public void BeginMonitoring()
        {
            isMonitoring = true;
            hasNotified = false;
            suppressNotifications = false;
            // 監視開始時点で既に2接続なら同伴済みとする
            hasSeenPeer = CountConnectedClients() >= 2;
            Subscribe();
            StartPolling();
        }

        /// <summary>
        /// NetworkManager準備後の購読を再試行する
        /// </summary>
        /// <returns>購読済みならtrue</returns>
        public bool TryEnsureSubscribed()
        {
            if (!isMonitoring || suppressNotifications)
            {
                return isSubscribed;
            }

            Subscribe();
            return isSubscribed;
        }

        /// <summary>
        /// 切断監視を停止する
        /// </summary>
        public void EndMonitoring()
        {
            isMonitoring = false;
            StopPolling();
            Unsubscribe();
        }

        /// <summary>
        /// 意図的なセッション終了中は切断通知を抑止する
        /// </summary>
        public void SuppressNotifications()
        {
            suppressNotifications = true;
        }

        /// <summary>
        /// 外部から強制的に切断イベントを発火する
        /// </summary>
        public void ForceNotify()
        {
            if (!ShouldNotify())
            {
                return;
            }

            Notify();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            EndMonitoring();
            Disconnected = null;
        }

        private void StartPolling()
        {
            StopPolling();
            pollCts = new CancellationTokenSource();
            PollConnectionAsync(pollCts.Token).Forget();
        }

        private void StopPolling()
        {
            if (pollCts == null)
            {
                return;
            }

            pollCts.Cancel();
            pollCts.Dispose();
            pollCts = null;
        }

        private async UniTaskVoid PollConnectionAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && isMonitoring)
                {
                    EvaluateConnectionHealth();
                    await UniTask.Delay(
                        TimeSpan.FromSeconds(PollIntervalSeconds),
                        cancellationToken: cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void EvaluateConnectionHealth()
        {
            if (!ShouldNotify())
            {
                return;
            }

            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || manager.ShutdownInProgress)
            {
                Notify();
                return;
            }

            int connected = CountConnectedClients();
            if (connected >= 2)
            {
                hasSeenPeer = true;
                return;
            }

            // 一度でも対戦相手を見てから1接続以下になったら切断
            if (hasSeenPeer && connected < 2)
            {
                Debug.LogWarning(
                    "[BattlePvpDisconnect] 接続数低下を検知しました"
                    + $" connected={connected}");
                Notify();
                return;
            }

            if (manager.IsClient && !manager.IsServer && !manager.IsConnectedClient)
            {
                Notify();
            }
        }

        private static int CountConnectedClients()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || manager.ConnectedClientsIds == null)
            {
                return 0;
            }

            return manager.ConnectedClientsIds.Count;
        }

        private void Subscribe()
        {
            if (isSubscribed)
            {
                return;
            }

            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null)
            {
                Debug.LogWarning("[BattlePvpDisconnect] NetworkManager未準備のため監視購読を保留します");
                return;
            }

            manager.OnClientDisconnectCallback += OnClientDisconnect;
            manager.OnClientStopped += OnClientStopped;
            manager.OnServerStopped += OnServerStopped;
            manager.OnTransportFailure += OnTransportFailure;
            manager.OnConnectionEvent += OnConnectionEvent;
            isSubscribed = true;
            Debug.Log("[BattlePvpDisconnect] 切断監視を開始しました");
        }

        private void Unsubscribe()
        {
            if (!isSubscribed)
            {
                return;
            }

            NetworkManager manager = NetworkManager.Singleton;
            if (manager != null)
            {
                manager.OnClientDisconnectCallback -= OnClientDisconnect;
                manager.OnClientStopped -= OnClientStopped;
                manager.OnServerStopped -= OnServerStopped;
                manager.OnTransportFailure -= OnTransportFailure;
                manager.OnConnectionEvent -= OnConnectionEvent;
            }

            isSubscribed = false;
        }

        private void OnClientDisconnect(ulong clientId)
        {
            if (!ShouldNotify())
            {
                return;
            }

            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null)
            {
                Notify();
                return;
            }

            // ホスト:相手クライアント切断のみ通知
            if (manager.IsServer)
            {
                if (clientId != manager.LocalClientId)
                {
                    Notify();
                }

                return;
            }

            // クライアント:自分またはサーバー切断
            Notify();
        }

        private void OnClientStopped(bool wasHost)
        {
            if (!ShouldNotify())
            {
                return;
            }

            Notify();
        }

        private void OnServerStopped(bool _)
        {
            if (!ShouldNotify())
            {
                return;
            }

            Notify();
        }

        private void OnTransportFailure()
        {
            if (!ShouldNotify())
            {
                return;
            }

            Notify();
        }

        private void OnConnectionEvent(NetworkManager manager, ConnectionEventData eventData)
        {
            if (!ShouldNotify())
            {
                return;
            }

            if (eventData.EventType == ConnectionEvent.ClientDisconnected
                || eventData.EventType == ConnectionEvent.PeerDisconnected)
            {
                // ホスト自身のLocalClient切断は無視する
                if (manager != null
                    && manager.IsServer
                    && eventData.ClientId == manager.LocalClientId)
                {
                    return;
                }

                Notify();
            }
        }

        private bool ShouldNotify()
        {
            return isMonitoring && !suppressNotifications && !hasNotified;
        }

        private void Notify()
        {
            if (hasNotified)
            {
                return;
            }

            hasNotified = true;
            Debug.LogWarning("[BattlePvpDisconnect] 通信切断を検知しました");
            Disconnected?.Invoke();
        }
    }
}
