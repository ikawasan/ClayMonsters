using Cysharp.Threading.Tasks;
using System.Threading;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

namespace Scene.BattlePVPScene.Service
{
    /// <summary>
    /// Unity Relay経由でNetcodeのホストとクライアント接続を構成する
    /// </summary>
    public static class BattlePvpRelayConnection
    {
        private const int MaxRelayConnections = 1;
        private const string RelayConnectionType = "dtls";

        /// <summary>
        /// Relayホストとして接続を開始する
        /// </summary>
        public static async UniTask<string> StartHostAsync(
            NetworkManager networkManager,
            UnityTransport transport,
            CancellationToken cancellationToken)
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(MaxRelayConnections);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            RelayServerData relayServerData = new RelayServerData(allocation, RelayConnectionType);
            transport.SetRelayServerData(relayServerData);
            ShutdownIfNeeded(networkManager);
            DisableNetworkSceneManagement(networkManager);
            if (!networkManager.StartHost())
            {
                throw new System.InvalidOperationException(
                    Localization.LocalizedText.GetOrFallback(
                        Localization.GameTextKeys.BattlePvpRelayHostFailed,
                        "Relayホストの開始に失敗しました"));
            }

            return joinCode;
        }

        /// <summary>
        /// Relay参加コードでクライアント接続を開始する
        /// </summary>
        public static async UniTask StartClientAsync(
            NetworkManager networkManager,
            UnityTransport transport,
            string joinCode,
            CancellationToken cancellationToken)
        {
            JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            RelayServerData relayServerData = new RelayServerData(allocation, RelayConnectionType);
            transport.SetRelayServerData(relayServerData);
            ShutdownIfNeeded(networkManager);
            DisableNetworkSceneManagement(networkManager);
            if (!networkManager.StartClient())
            {
                throw new System.InvalidOperationException(
                    Localization.LocalizedText.GetOrFallback(
                        Localization.GameTextKeys.BattlePvpRelayClientFailed,
                        "Relayクライアントの開始に失敗しました"));
            }
        }

        private static void ShutdownIfNeeded(NetworkManager networkManager)
        {
            if (networkManager.IsClient || networkManager.IsServer)
            {
                networkManager.Shutdown();
            }
        }

        // 独自のシーン管理を使うためNGOのシーン同期を確実に無効化し接続時のシーンリロードを防ぐ
        private static void DisableNetworkSceneManagement(NetworkManager networkManager)
        {
            if (networkManager != null && networkManager.NetworkConfig != null)
            {
                networkManager.NetworkConfig.EnableSceneManagement = false;
            }
        }
    }
}
