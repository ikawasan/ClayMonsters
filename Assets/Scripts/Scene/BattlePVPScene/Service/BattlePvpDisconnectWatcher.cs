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
                return;
            }

            manager.OnClientDisconnectCallback += OnClientDisconnect;
            manager.OnClientStopped += OnClientStopped;
            isSubscribed = true;
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
            if (manager == null || !manager.IsServer)
            {
                return;
            }

            if (clientId != manager.LocalClientId)
            {
                Notify();
            }
        }

        private void OnClientStopped(bool isHost)
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

            if (!manager.IsServer || manager.ConnectedClientsIds.Count <= 1)
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
