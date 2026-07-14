using Unity.Netcode;
using UnityEngine;

namespace Scene.BattlePVPScene
{
    /// <summary>
    /// BattlePVP用NetworkManagerをシーンアンロードから切り離す
    /// </summary>
    public static class BattlePvpNetworkPersistence
    {
        /// <summary>
        /// NetworkManagerをDontDestroyOnLoadへ移しBattlePVPシーンから切り離す
        /// </summary>
        public static void EnsurePersisted(NetworkManager networkManager)
        {
            if (networkManager == null)
            {
                Debug.LogWarning("[BattlePvpNetwork] NetworkManager未設定");
                return;
            }

            string ownerScene = networkManager.gameObject.scene.name;
            if (ownerScene == "DontDestroyOnLoad")
            {
                return;
            }

            Object.DontDestroyOnLoad(networkManager.gameObject);
            BattlePvpSceneDiagnostics.LogState("NetworkManagerをDontDestroyOnLoadへ移動");
        }
    }
}
