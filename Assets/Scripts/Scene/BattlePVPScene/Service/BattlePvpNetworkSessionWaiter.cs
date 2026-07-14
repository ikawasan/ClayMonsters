using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using Unity.Netcode;
using UnityEngine;

namespace Scene.BattlePVPScene.Service
{
    /// <summary>
    /// Netcode接続完了をイベントとポーリングで待機する
    /// </summary>
    public static class BattlePvpNetworkSessionWaiter
    {
        private const float PollIntervalSeconds = 0.1f;
        private const float LogIntervalSeconds = 2f;

        /// <summary>
        /// ホストまたはクライアントのセッション準備完了を待つ
        /// </summary>
        public static async UniTask<bool> WaitUntilReadyAsync(
            NetworkManager networkManager,
            bool isHostSide,
            float timeoutSeconds,
            CancellationToken cancellationToken)
        {
            if (IsReady(networkManager, isHostSide))
            {
                LogReady(networkManager, isHostSide, "即時");
                return true;
            }

            bool signaledReady = false;
            void MarkReadyIfNeeded()
            {
                if (IsReady(networkManager, isHostSide))
                {
                    signaledReady = true;
                }
            }

            networkManager.OnClientConnectedCallback += OnClientConnected;
            networkManager.OnClientStarted += OnClientStarted;
            networkManager.OnServerStarted += OnServerStarted;

            try
            {
                float elapsed = 0f;
                float nextLogTime = 0f;
                while (elapsed < timeoutSeconds && !cancellationToken.IsCancellationRequested)
                {
                    if (signaledReady || IsReady(networkManager, isHostSide))
                    {
                        LogReady(networkManager, isHostSide, "待機後");
                        return true;
                    }

                    if (Time.realtimeSinceStartup >= nextLogTime)
                    {
                        nextLogTime = Time.realtimeSinceStartup + LogIntervalSeconds;
                        LogWaiting(networkManager, isHostSide);
                    }

                    elapsed += PollIntervalSeconds;
                    await UniTask.Delay(
                        TimeSpan.FromSeconds(PollIntervalSeconds),
                        cancellationToken: cancellationToken);
                }

                bool ready = IsReady(networkManager, isHostSide);
                if (ready)
                {
                    LogReady(networkManager, isHostSide, "タイムアウト直前");
                }
                else
                {
                    Debug.LogWarning(
                        "[BattlePvpMatchmaking] セッション待機タイムアウト"
                        + $" isHostSide={isHostSide}"
                        + $" isHost={networkManager.IsHost}"
                        + $" isClient={networkManager.IsClient}"
                        + $" isConnectedClient={networkManager.IsConnectedClient}"
                        + $" 接続数={networkManager.ConnectedClientsIds.Count}");
                }

                return ready;
            }
            finally
            {
                networkManager.OnClientConnectedCallback -= OnClientConnected;
                networkManager.OnClientStarted -= OnClientStarted;
                networkManager.OnServerStarted -= OnServerStarted;
            }

            void OnClientConnected(ulong clientId) => MarkReadyIfNeeded();
            void OnClientStarted() => MarkReadyIfNeeded();
            void OnServerStarted() => MarkReadyIfNeeded();
        }

        /// <summary>
        /// セッション準備完了か判定する
        /// </summary>
        public static bool IsReady(NetworkManager networkManager, bool isHostSide)
        {
            if (networkManager == null || networkManager.ShutdownInProgress)
            {
                return false;
            }

            if (isHostSide || networkManager.IsHost)
            {
                return networkManager.IsHost && networkManager.ConnectedClientsIds.Count >= 2;
            }

            return networkManager.IsConnectedClient;
        }

        private static void LogWaiting(NetworkManager networkManager, bool isHostSide)
        {
            Debug.Log(
                "[BattlePvpMatchmaking] セッション待機中"
                + $" isHostSide={isHostSide}"
                + $" isHost={networkManager.IsHost}"
                + $" isClient={networkManager.IsClient}"
                + $" isConnectedClient={networkManager.IsConnectedClient}"
                + $" isApproved={networkManager.IsApproved}"
                + $" 接続数={networkManager.ConnectedClientsIds.Count}"
                + $" singleton={(NetworkManager.Singleton == networkManager)}");
        }

        private static void LogReady(NetworkManager networkManager, bool isHostSide, string reason)
        {
            Debug.Log(
                "[BattlePvpMatchmaking] セッション準備完了"
                + $" reason={reason}"
                + $" isHostSide={isHostSide}"
                + $" isHost={networkManager.IsHost}"
                + $" isClient={networkManager.IsClient}"
                + $" isConnectedClient={networkManager.IsConnectedClient}"
                + $" 接続数={networkManager.ConnectedClientsIds.Count}");
        }
    }
}
