using Extensions;
using Unity.Netcode;
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

            if (!manager.IsListening && !manager.IsClient && !manager.IsServer && !manager.IsHost)
            {
                return;
            }

            manager.Shutdown();
        }
    }
}
