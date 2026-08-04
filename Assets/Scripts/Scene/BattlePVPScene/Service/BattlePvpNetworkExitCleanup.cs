using Extensions;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Scene.BattlePVPScene.Service
{
    /// <summary>
    /// ROM終了時にNetcodeセッションを停止しプロセス残留を防ぐ
    /// </summary>
    public static class BattlePvpNetworkExitCleanup
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            ApplicationQuitGuard.ExitCleanup -= ShutdownNetworks;
            ApplicationQuitGuard.ExitCleanup += ShutdownNetworks;
        }

        private static void ShutdownNetworks()
        {
            try
            {
                TryShutdown(NetworkManager.Singleton);

                NetworkManager[] managers = Object.FindObjectsByType<NetworkManager>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

                for (int i = 0; i < managers.Length; i++)
                {
                    TryShutdown(managers[i]);
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning(
                    $"[BattlePvpNetworkExitCleanup] Network shutdown failed: {exception.Message}");
            }
        }

        private static void TryShutdown(NetworkManager manager)
        {
            if (manager == null || manager.ShutdownInProgress)
            {
                return;
            }

            try
            {
                // ソケット側の切断でネイティブ待機を早めに解放する
                if (manager.NetworkConfig != null
                    && manager.NetworkConfig.NetworkTransport is UnityTransport unityTransport)
                {
                    unityTransport.DisconnectLocalClient();
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning(
                    $"[BattlePvpNetworkExitCleanup] Transport disconnect failed: {exception.Message}");
            }

            if (!manager.IsListening && !manager.IsClient && !manager.IsServer && !manager.IsHost)
            {
                return;
            }

            // discardMessageQueueで同期待ちを避けて止める
            manager.Shutdown(true);
        }
    }
}
