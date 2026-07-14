using Cysharp.Threading.Tasks;
using Scene.BattlePVPScene.Interface;
using Scene.BattlePVPScene.Network;
using System;
using System.Collections.Generic;
using System.Threading;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using VContainer;

namespace Scene.BattlePVPScene.Service
{
    /// <summary>
    /// Unity RelayとLobbyを使った別PC間PvPマッチング実装
    /// </summary>
    public sealed class NetcodeBattlePvpMatchmakingService : IBattlePvpMatchmakingService, IDisposable
    {
        private const float HostWaitSeconds = 60f;
        private const float LobbyPollSeconds = 1f;
        private const float LobbyHeartbeatSeconds = 15f;
        private const string LobbyName = "ClayMonstersPvp";
        private const string RelayJoinCodeKey = "relayJoinCode";

        private readonly NetworkManager networkManager;
        private readonly UnityTransport transport;
        private CancellationTokenSource operationCts;
        private CancellationTokenSource heartbeatCts;
        private string currentLobbyId = string.Empty;

        /// <summary>
        /// シーン上のNetworkManagerからマッチングサービスを生成する
        /// </summary>
        [Inject]
        public NetcodeBattlePvpMatchmakingService(NetworkManager networkManager)
        {
            this.networkManager = networkManager;
            transport = ResolveTransport(networkManager);
        }

        private static UnityTransport ResolveTransport(NetworkManager networkManager)
        {
            if (networkManager == null)
            {
                return null;
            }

            UnityTransport resolved = networkManager.NetworkConfig.NetworkTransport as UnityTransport;
            if (resolved != null)
            {
                return resolved;
            }

            resolved = networkManager.GetComponent<UnityTransport>();
            if (resolved != null)
            {
                networkManager.NetworkConfig.NetworkTransport = resolved;
            }

            return resolved;
        }

        /// <inheritdoc/>
        public event Action<string> StatusChanged;

        /// <inheritdoc/>
        public bool IsSessionReady =>
            BattlePvpNetworkSessionWaiter.IsReady(ResolveActiveNetworkManager(), IsHost);

        /// <inheritdoc/>
        public bool IsHost
        {
            get
            {
                NetworkManager activeManager = ResolveActiveNetworkManager();
                return activeManager != null && activeManager.IsHost;
            }
        }

        /// <inheritdoc/>
        public string CurrentRoomCode { get; private set; } = string.Empty;

        /// <inheritdoc/>
        public async UniTask<BattlePvpMatchmakingResult> CreateDirectRoomAsync(CancellationToken cancellationToken)
        {
            if (!TryValidateNetwork(out string validationError))
            {
                return BattlePvpMatchmakingResult.Failed(validationError);
            }

            try
            {
                BeginOperation(cancellationToken);
                await BattlePvpUnityServicesInitializer.EnsureInitializedAsync(operationCts.Token);
                PublishStatus("ルームを作成しています…");

                NetworkManager activeManager = ResolveActiveNetworkManager();
                string joinCode = await BattlePvpRelayConnection.StartHostAsync(
                    activeManager,
                    ResolveTransport(activeManager),
                    operationCts.Token);
                CurrentRoomCode = joinCode;

                PublishStatus($"参加コード: {joinCode}\n相手にこのコードを伝えてください");
                bool matched = await WaitUntilSessionReadyAsync(true, HostWaitSeconds, operationCts.Token);
                if (!matched)
                {
                    CancelInternal();
                    return BattlePvpMatchmakingResult.Failed("相手が参加しませんでした");
                }

                PublishStatus("対戦相手と接続しました");
                TrySpawnSessionPlayers();
                return BattlePvpMatchmakingResult.Succeeded(true, joinCode);
            }
            catch (Exception exception)
            {
                CancelInternal();
                return BattlePvpMatchmakingResult.Failed(ToUserMessage(exception));
            }
        }

        /// <inheritdoc/>
        public async UniTask<BattlePvpMatchmakingResult> JoinDirectRoomAsync(
            string roomCode,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(roomCode))
            {
                return BattlePvpMatchmakingResult.Failed("参加コードを入力してください");
            }

            if (!TryValidateNetwork(out string validationError))
            {
                return BattlePvpMatchmakingResult.Failed(validationError);
            }

            string joinCode = roomCode.Trim().ToUpperInvariant();
            try
            {
                BeginOperation(cancellationToken);
                await BattlePvpUnityServicesInitializer.EnsureInitializedAsync(operationCts.Token);
                CurrentRoomCode = joinCode;
                PublishStatus("ルームへ接続しています…");

                NetworkManager activeManager = ResolveActiveNetworkManager();
                Debug.Log($"[BattlePvpMatchmaking] JoinDirectRoom開始 code={joinCode}");
                await BattlePvpRelayConnection.StartClientAsync(
                    activeManager,
                    ResolveTransport(activeManager),
                    joinCode,
                    operationCts.Token);
                Debug.Log(
                    "[BattlePvpMatchmaking] StartClient完了"
                    + $" isClient={activeManager.IsClient}"
                    + $" isConnectedClient={activeManager.IsConnectedClient}");

                bool matched = await WaitUntilSessionReadyAsync(false, HostWaitSeconds, operationCts.Token);
                if (!matched)
                {
                    CancelInternal();
                    return BattlePvpMatchmakingResult.Failed("ルームが見つからないか満員です");
                }

                PublishStatus("対戦相手と接続しました");
                TrySpawnSessionPlayers();
                return BattlePvpMatchmakingResult.Succeeded(false, joinCode);
            }
            catch (Exception exception)
            {
                CancelInternal();
                return BattlePvpMatchmakingResult.Failed(ToUserMessage(exception));
            }
        }

        /// <inheritdoc/>
        public async UniTask<BattlePvpMatchmakingResult> StartRandomMatchAsync(CancellationToken cancellationToken)
        {
            if (!TryValidateNetwork(out string validationError))
            {
                return BattlePvpMatchmakingResult.Failed(validationError);
            }

            try
            {
                BeginOperation(cancellationToken);
                await BattlePvpUnityServicesInitializer.EnsureInitializedAsync(operationCts.Token);
                PublishStatus("対戦相手を探しています…");

                Lobby joinedLobby = await TryQuickJoinLobbyAsync(operationCts.Token);
                if (joinedLobby == null)
                {
                    joinedLobby = await CreateWaitingLobbyAsync(operationCts.Token);
                }

                currentLobbyId = joinedLobby.Id;
                bool isLobbyHost = joinedLobby.HostId == AuthenticationService.Instance.PlayerId;
                if (isLobbyHost)
                {
                    StartLobbyHeartbeat(joinedLobby.Id);
                    PublishStatus("対戦相手の参加を待っています…");
                    string joinCode = await HostRelayForLobbyAsync(joinedLobby.Id, operationCts.Token);
                    CurrentRoomCode = joinCode;
                    bool matched = await WaitUntilSessionReadyAsync(true, HostWaitSeconds, operationCts.Token);
                    if (!matched)
                    {
                        CancelInternal();
                        return BattlePvpMatchmakingResult.Failed("対戦相手が見つかりませんでした");
                    }

                    PublishStatus("対戦相手と接続しました");
                    TrySpawnSessionPlayers();
                    return BattlePvpMatchmakingResult.Succeeded(true, joinCode);
                }

                PublishStatus("ホストの準備を待っています…");
                await JoinRelayFromLobbyAsync(joinedLobby.Id, operationCts.Token);
                bool clientMatched = await WaitUntilSessionReadyAsync(false, HostWaitSeconds, operationCts.Token);
                if (!clientMatched)
                {
                    CancelInternal();
                    return BattlePvpMatchmakingResult.Failed("接続がタイムアウトしました");
                }

                PublishStatus("対戦相手と接続しました");
                TrySpawnSessionPlayers();
                return BattlePvpMatchmakingResult.Succeeded(false, CurrentRoomCode);
            }
            catch (Exception exception)
            {
                CancelInternal();
                return BattlePvpMatchmakingResult.Failed(ToUserMessage(exception));
            }
        }

        /// <inheritdoc/>
        public void Cancel()
        {
            CancelInternal();
            PublishStatus(string.Empty);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            CancelInternal();
        }

        private void BeginOperation(CancellationToken cancellationToken)
        {
            CancelInternal();
            operationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        }

        private NetworkManager ResolveActiveNetworkManager()
        {
            NetworkManager singleton = NetworkManager.Singleton;
            if (networkManager != null
                && (singleton == null
                    || singleton.ShutdownInProgress
                    || singleton == networkManager))
            {
                return networkManager;
            }

            return singleton ?? networkManager;
        }

        private bool TryValidateNetwork(out string errorMessage)
        {
            NetworkManager activeManager = ResolveActiveNetworkManager();
            if (activeManager == null)
            {
                errorMessage = "NetworkManagerが見つかりません";
                return false;
            }

            if (ResolveTransport(activeManager) == null)
            {
                errorMessage = "UnityTransportが設定されていません";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        private async UniTask<Lobby> TryQuickJoinLobbyAsync(CancellationToken cancellationToken)
        {
            try
            {
                QuickJoinLobbyOptions options = new QuickJoinLobbyOptions
                {
                    Filter = new List<QueryFilter>
                    {
                        new QueryFilter(
                            QueryFilter.FieldOptions.AvailableSlots,
                            "0",
                            QueryFilter.OpOptions.GT)
                    }
                };
                return await LobbyService.Instance.QuickJoinLobbyAsync(options);
            }
            catch (LobbyServiceException)
            {
                return null;
            }
        }

        private async UniTask<Lobby> CreateWaitingLobbyAsync(CancellationToken cancellationToken)
        {
            CreateLobbyOptions options = new CreateLobbyOptions
            {
                IsPrivate = false,
                Player = new Player
                {
                    Data = new Dictionary<string, PlayerDataObject>()
                },
                Data = new Dictionary<string, DataObject>()
            };
            return await LobbyService.Instance.CreateLobbyAsync(LobbyName, 2, options);
        }

        private void StartLobbyHeartbeat(string lobbyId)
        {
            heartbeatCts?.Cancel();
            heartbeatCts?.Dispose();
            heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(operationCts.Token);
            RunLobbyHeartbeatAsync(lobbyId, heartbeatCts.Token).Forget();
        }

        private async UniTaskVoid RunLobbyHeartbeatAsync(string lobbyId, CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await LobbyService.Instance.SendHeartbeatPingAsync(lobbyId);
                    await UniTask.Delay(TimeSpan.FromSeconds(LobbyHeartbeatSeconds), cancellationToken: cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[BattlePvpMatchmaking] heartbeat failed: {exception.Message}");
            }
        }

        private async UniTask<string> HostRelayForLobbyAsync(string lobbyId, CancellationToken cancellationToken)
        {
            float elapsed = 0f;
            while (elapsed < HostWaitSeconds && !cancellationToken.IsCancellationRequested)
            {
                Lobby lobby = await LobbyService.Instance.GetLobbyAsync(lobbyId);
                if (lobby.Players.Count >= 2)
                {
                    NetworkManager activeManager = ResolveActiveNetworkManager();
                    string joinCode = await BattlePvpRelayConnection.StartHostAsync(
                        activeManager,
                        ResolveTransport(activeManager),
                        cancellationToken);

                    UpdateLobbyOptions updateOptions = new UpdateLobbyOptions
                    {
                        Data = new Dictionary<string, DataObject>
                        {
                            {
                                RelayJoinCodeKey,
                                new DataObject(
                                    visibility: DataObject.VisibilityOptions.Member,
                                    value: joinCode)
                            }
                        }
                    };
                    await LobbyService.Instance.UpdateLobbyAsync(lobbyId, updateOptions);
                    return joinCode;
                }

                elapsed += LobbyPollSeconds;
                await UniTask.Delay(TimeSpan.FromSeconds(LobbyPollSeconds), cancellationToken: cancellationToken);
            }

            throw new TimeoutException("対戦相手の参加待ちがタイムアウトしました");
        }

        private async UniTask JoinRelayFromLobbyAsync(string lobbyId, CancellationToken cancellationToken)
        {
            float elapsed = 0f;
            while (elapsed < HostWaitSeconds && !cancellationToken.IsCancellationRequested)
            {
                Lobby lobby = await LobbyService.Instance.GetLobbyAsync(lobbyId);
                if (lobby.Data != null
                    && lobby.Data.TryGetValue(RelayJoinCodeKey, out DataObject relayData)
                    && !string.IsNullOrWhiteSpace(relayData.Value))
                {
                    CurrentRoomCode = relayData.Value;
                    NetworkManager activeManager = ResolveActiveNetworkManager();
                    await BattlePvpRelayConnection.StartClientAsync(
                        activeManager,
                        ResolveTransport(activeManager),
                        relayData.Value,
                        cancellationToken);
                    return;
                }

                elapsed += LobbyPollSeconds;
                await UniTask.Delay(TimeSpan.FromSeconds(LobbyPollSeconds), cancellationToken: cancellationToken);
            }

            throw new TimeoutException("ホストの準備がタイムアウトしました");
        }

        private async UniTask<bool> WaitUntilSessionReadyAsync(
            bool isHostSide,
            float timeoutSeconds,
            CancellationToken cancellationToken)
        {
            NetworkManager activeManager = ResolveActiveNetworkManager();
            if (activeManager == null)
            {
                return false;
            }

            return await BattlePvpNetworkSessionWaiter.WaitUntilReadyAsync(
                activeManager,
                isHostSide,
                timeoutSeconds,
                cancellationToken);
        }

        private void TrySpawnSessionPlayers()
        {
            NetworkManager activeManager = ResolveActiveNetworkManager();
            if (activeManager == null || !activeManager.IsServer)
            {
                return;
            }

            BattlePvpSessionSpawner spawner = UnityEngine.Object.FindFirstObjectByType<BattlePvpSessionSpawner>();
            spawner?.TrySpawnPlayersIfNeeded();
        }

        private void CancelInternal()
        {
            operationCts?.Cancel();
            operationCts?.Dispose();
            operationCts = null;

            heartbeatCts?.Cancel();
            heartbeatCts?.Dispose();
            heartbeatCts = null;

            CurrentRoomCode = string.Empty;
            LeaveLobbyIfNeeded().Forget();

            NetworkManager activeManager = ResolveActiveNetworkManager();
            if (activeManager != null && (activeManager.IsClient || activeManager.IsServer))
            {
                activeManager.Shutdown();
            }
        }

        private async UniTaskVoid LeaveLobbyIfNeeded()
        {
            if (string.IsNullOrEmpty(currentLobbyId))
            {
                return;
            }

            string lobbyId = currentLobbyId;
            currentLobbyId = string.Empty;
            try
            {
                await LobbyService.Instance.RemovePlayerAsync(lobbyId, AuthenticationService.Instance.PlayerId);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[BattlePvpMatchmaking] leave lobby failed: {exception.Message}");
            }
        }

        private void PublishStatus(string message) => StatusChanged?.Invoke(message);

        private static string ToUserMessage(Exception exception) =>
            BattlePvpMatchmakingErrorFormatter.Format(exception);
    }
}
