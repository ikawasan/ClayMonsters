using Cysharp.Threading.Tasks;

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



        private bool hasSpawnedPlayers;

        private bool isSubscribed;



        private void OnDisable()

        {

            UnsubscribeClientConnected();

        }



        private void Update()

        {

            if (hasSpawnedPlayers || playerRelayPrefab == null)

            {

                return;

            }



            NetworkManager manager = NetworkManager.Singleton;

            if (manager == null || !manager.IsServer || manager.ConnectedClientsIds.Count < 2)

            {

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



            NetworkObject localPlayer = manager.SpawnManager.GetLocalPlayerObject();

            return localPlayer != null

                ? localPlayer.GetComponent<BattlePvpInputRelay>()

                : null;

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

                    TrySpawnPlayersIfNeeded();

                    relay = GetLocalRelay();

                    if (relay == null && Time.realtimeSinceStartup >= nextLogTime)

                    {

                        nextLogTime = Time.realtimeSinceStartup + 1f;

                        NetworkManager manager = NetworkManager.Singleton;

                        NetworkObject localPlayer = manager != null

                            ? manager.SpawnManager?.GetLocalPlayerObject()

                            : null;

                        Debug.Log(

                            "[BattlePvpSpawner] リレー待機中"

                            + $" IsServer={(manager != null && manager.IsServer)}"

                            + $" IsClient={(manager != null && manager.IsClient)}"

                            + $" 接続数={(manager != null ? manager.ConnectedClientsIds.Count : -1)}"

                            + $" localPlayer={(localPlayer != null)}"

                            + $" hasSpawned={hasSpawnedPlayers}");

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

            return relay;

        }



        /// <summary>

        /// 接続完了直後にサーバー側でプレイヤーリレーをスポーンする

        /// </summary>

        public void TrySpawnPlayersIfNeeded()

        {

            if (hasSpawnedPlayers || playerRelayPrefab == null)

            {

                return;

            }



            NetworkManager manager = NetworkManager.Singleton;

            if (manager == null || !manager.IsServer || manager.ConnectedClientsIds.Count < 2)

            {

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

            if (manager == null || !manager.IsServer)

            {

                return;

            }



            bool spawnedAny = false;

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

                networkObject.SpawnAsPlayerObject(clientId, true);

                spawnedAny = true;

                Debug.Log($"[BattlePvpSpawner] プレイヤーリレーをスポーン clientId={clientId}");

            }



            if (spawnedAny || AllClientsHaveRelay(manager))

            {

                hasSpawnedPlayers = true;

            }

        }



        private static bool AllClientsHaveRelay(NetworkManager manager)

        {

            foreach (ulong clientId in manager.ConnectedClientsIds)

            {

                if (!TryGetRelayForClient(clientId, out _))

                {

                    return false;

                }

            }



            return manager.ConnectedClientsIds.Count >= 2;

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

            return relay != null;

        }

    }

}

