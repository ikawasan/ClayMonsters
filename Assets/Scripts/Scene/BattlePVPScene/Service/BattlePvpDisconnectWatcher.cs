using System;
using Unity.Netcode;
using UnityEngine;

namespace Scene.BattlePVPScene.Service
{
    /// <summary>
    /// Netcodeの切断を検知して通知する
    /// </summary>
    public sealed class BattlePvpDisconnectWatcher : IDisposable
    {
        private bool isMonitoring;
        private bool suppressNotifications;
        private bool hasNotified;
        private bool isSubscribed;

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
            Subscribe();
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
            Unsubscribe();
        }

        /// <summary>
        /// 意図的なセッション終了中は切断通知を抑止する
        /// </summary>
        public void SuppressNotifications()
        {
            suppressNotifications = true;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            EndMonitoring();
            Disconnected = null;
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
