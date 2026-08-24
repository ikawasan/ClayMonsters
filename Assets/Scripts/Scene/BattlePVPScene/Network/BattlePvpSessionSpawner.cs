using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using Unity.Netcode;
using UnityEngine;

namespace Scene.BattlePVPScene.Network
{
    /// <summary>
    /// 接続完了後にプレイヤー用NetworkObjectをスポーンする
    /// </summary>
    public sealed class BattlePvpSessionSpawner : MonoBehaviour
    {
        private const float RelayWaitTimeoutSeconds = 30f;

        [SerializeField] private BattlePvpInputRelay playerRelayPrefab;

        private bool isSubscribed;
        private bool relaysReady;

        private void OnDisable()
        {
            UnsubscribeClientConnected();
        }

        private void Update()
        {
            if (relaysReady || playerRelayPrefab == null)
            {
                return;
            }

            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer || manager.ConnectedClientsIds.Count < 2)
            {
                return;
            }

            if (AllClientsHaveRelay(manager))
            {
                relaysReady = true;
                enabled = false;
                return;
            }

            SpawnPlayers();
        }

        /// <summary>
        /// ローカルプレイヤーの入力リレーを取得する
        /// </summary>
        public BattlePvpInputRelay GetLocalRelay()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null)
            {
                return null;
            }

            NetworkObject localPlayer = null;
            if (manager.SpawnManager != null)
            {
                localPlayer = manager.SpawnManager.GetLocalPlayerObject();
            }

            if (localPlayer == null
                && manager.ConnectedClients.TryGetValue(manager.LocalClientId, out NetworkClient localClient))
            {
                localPlayer = localClient.PlayerObject;
            }

            if (localPlayer != null)
            {
                BattlePvpInputRelay fromPlayerObject = localPlayer.GetComponent<BattlePvpInputRelay>();
                if (fromPlayerObject != null)
                {
                    return fromPlayerObject;
                }
            }

            return FindOwnedRelay();
        }

        /// <summary>
        /// 入力リレーのスポーン完了を待つ
        /// </summary>
        public async UniTask<BattlePvpInputRelay> WaitForLocalRelayAsync(CancellationToken cancellationToken)
        {
            EnsureSubscribed();
            TrySpawnPlayersIfNeeded();

            BattlePvpInputRelay relay = null;
            float nextLogTime = 0f;
            float startedAt = Time.realtimeSinceStartup;
            await UniTask.WaitUntil(
                () =>
                {
                    NetworkManager manager = NetworkManager.Singleton;
                    bool networkActive = manager != null
                        && !manager.ShutdownInProgress
                        && (manager.IsServer || manager.IsClient || manager.IsListening);

                    if (!networkActive
                        && Time.realtimeSinceStartup - startedAt >= 3f)
                    {
                        Debug.LogError(
                            "[BattlePvpSpawner] NetworkManagerが未接続のためリレー待機を中断します"
                            + $" manager={(manager != null)}"
                            + $" IsServer={(manager != null && manager.IsServer)}"
                            + $" IsClient={(manager != null && manager.IsClient)}");
                        return true;
                    }

                    TrySpawnPlayersIfNeeded();
                    relay = GetLocalRelay();
                    if (relay == null && Time.realtimeSinceStartup >= nextLogTime)
                    {
                        nextLogTime = Time.realtimeSinceStartup + 1f;
                        int relayCount = CountSpawnedRelays();
                        Debug.Log(
                            "[BattlePvpSpawner] リレー待機中"
                            + $" IsServer={(manager != null && manager.IsServer)}"
                            + $" IsClient={(manager != null && manager.IsClient)}"
                            + $" 接続数={(manager != null ? manager.ConnectedClientsIds.Count : -1)}"
                            + $" localPlayer={(manager != null && manager.SpawnManager != null && manager.SpawnManager.GetLocalPlayerObject() != null)}"
                            + $" ownedRelay={(FindOwnedRelay() != null)}"
                            + $" spawnedRelays={relayCount}"
                            + $" allHaveRelay={(manager != null && AllClientsHaveRelay(manager))}");
                    }

                    if (relay == null
                        && Time.realtimeSinceStartup - startedAt >= RelayWaitTimeoutSeconds)
                    {
                        Debug.LogError("[BattlePvpSpawner] ローカルリレーの待機がタイムアウトしました");
                        return true;
                    }

                    return relay != null;
                },
                cancellationToken: cancellationToken);
            if (relay == null)
            {
                NetworkManager manager = NetworkManager.Singleton;
                bool networkActive = manager != null
                    && !manager.ShutdownInProgress
                    && (manager.IsServer || manager.IsClient || manager.IsListening);
                if (!networkActive)
                {
                    throw new InvalidOperationException(
                        "[BattlePvpSpawner] NetworkManagerが未接続のためリレー待機を中断しました");
                }

                throw new TimeoutException(
                    $"[BattlePvpSpawner] ローカルリレーの待機がタイムアウトしました ({RelayWaitTimeoutSeconds}秒)");
            }

            return relay;
        }

        /// <summary>
        /// 接続完了直後にサーバー側でプレイヤーリレーをスポーンする
        /// </summary>
        public void TrySpawnPlayersIfNeeded()
        {
            if (playerRelayPrefab == null)
            {
                return;
            }

            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer || manager.ConnectedClientsIds.Count < 2)
            {
                return;
            }

            if (AllClientsHaveRelay(manager))
            {
                relaysReady = true;
                enabled = false;
                return;
            }

            EnsureSubscribed();
            SpawnPlayers();
        }

        private void EnsureSubscribed()
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

            manager.OnClientConnectedCallback += OnClientConnected;
            isSubscribed = true;
        }

        private void UnsubscribeClientConnected()
        {
            if (!isSubscribed)
            {
                return;
            }

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            }

            isSubscribed = false;
        }

        private void OnClientConnected(ulong clientId)
        {
            Debug.Log($"[BattlePvpSpawner] クライアント接続 clientId={clientId}");
            TrySpawnPlayersIfNeeded();
        }

        private void SpawnPlayers()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer || playerRelayPrefab == null)
            {
                return;
            }

            foreach (ulong clientId in manager.ConnectedClientsIds)
            {
                if (TryGetRelayForClient(clientId, out _))
                {
                    continue;
                }

                if (!manager.ConnectedClients.TryGetValue(clientId, out NetworkClient client))
                {
                    continue;
                }

                if (client.PlayerObject != null)
                {
                    Debug.LogWarning(
                        $"[BattlePvpSpawner] リレー無しPlayerObjectを破棄 clientId={clientId}");
                    client.PlayerObject.Despawn(true);
                }

                BattlePvpInputRelay relay = Instantiate(playerRelayPrefab);
                NetworkObject networkObject = relay.GetComponent<NetworkObject>();
                // シーン遷移後も残すためdestroyWithSceneはfalse
                networkObject.SpawnAsPlayerObject(clientId, false);
                Debug.Log($"[BattlePvpSpawner] プレイヤーリレーをスポーン clientId={clientId}");
            }
        }

        private static bool AllClientsHaveRelay(NetworkManager manager)
        {
            if (manager == null || manager.ConnectedClientsIds.Count < 2)
            {
                return false;
            }

            foreach (ulong clientId in manager.ConnectedClientsIds)
            {
                if (!TryGetRelayForClient(clientId, out _))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryGetRelayForClient(ulong clientId, out BattlePvpInputRelay relay)
        {
            relay = null;
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null
                || !manager.ConnectedClients.TryGetValue(clientId, out NetworkClient client)
                || client.PlayerObject == null)
            {
                return false;
            }

            relay = client.PlayerObject.GetComponent<BattlePvpInputRelay>();
            return relay != null && relay.IsSpawned;
        }

        private static BattlePvpInputRelay FindOwnedRelay()
        {
            BattlePvpInputRelay[] relays = FindObjectsByType<BattlePvpInputRelay>(FindObjectsSortMode.None);
            for (int i = 0; i < relays.Length; i++)
            {
                BattlePvpInputRelay candidate = relays[i];
                if (candidate != null && candidate.IsSpawned && candidate.IsOwner)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static int CountSpawnedRelays()
        {
            int count = 0;
            BattlePvpInputRelay[] relays = FindObjectsByType<BattlePvpInputRelay>(FindObjectsSortMode.None);
            for (int i = 0; i < relays.Length; i++)
            {
                if (relays[i] != null && relays[i].IsSpawned)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
