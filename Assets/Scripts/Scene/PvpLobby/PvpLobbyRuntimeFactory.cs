using TMPro;
using Unity.Netcode;
using UnityEngine;
using VContainer.Unity;

namespace Scene.PvpLobby
{
    /// <summary>
    /// PvpLobbyHostとBattlePvpNetworkHostをDDOLへ生成する
    /// NetworkManagerはNetcode制約によりルート直下のみ配置可能
    /// </summary>
    public static class PvpLobbyRuntimeFactory
    {
        private const string LobbyPrefabResourcePath = "Pvp/PvpLobbyHost";
        private const string NetworkHostResourcePath = "Pvp/BattlePvpNetworkHost";

        private static GameObject fallbackLobbyPrefab;
        private static LifetimeScope parentScope;

        /// <summary>
        /// ClayMonstersLifetimeScopeから渡す予備ロビープレハブを設定する
        /// </summary>
        public static void SetFallbackPrefab(GameObject prefab)
        {
            fallbackLobbyPrefab = prefab;
        }

        /// <summary>
        /// 子LifetimeScopeの親スコープを設定する
        /// </summary>
        public static void SetParentScope(LifetimeScope scope)
        {
            parentScope = scope;
            PvpLobbyLifetimeScope.SetPendingParentScope(scope);
        }

        /// <summary>
        /// DDOLロビーインスタンスを生成する
        /// </summary>
        /// <returns>生成に成功した場合true</returns>
        public static bool TryCreate(out GameObject instance)
        {
            instance = null;
            GameObject lobbyPrefab = ResolveLobbyPrefab();
            if (lobbyPrefab == null)
            {
                Debug.LogError(
                    "[PvpLobby] ロビープレハブが見つかりません"
                    + $" path=Resources/{LobbyPrefabResourcePath}");
                return false;
            }

            if (!EnsureNetworkHost())
            {
                Debug.LogError(
                    "[PvpLobby] NetworkHostプレハブ未配置"
                    + $" path=Resources/{NetworkHostResourcePath}"
                    + " PvpLobbyプレハブ配置を確認してください");
                return false;
            }

            instance = InstantiateLobby(lobbyPrefab);
            return instance != null;
        }

        private static GameObject InstantiateLobby(GameObject lobbyPrefab)
        {
            GameObject lobbyInstance;
            if (parentScope != null)
            {
                using (LifetimeScope.EnqueueParent(parentScope))
                {
                    lobbyInstance = CreateLobbyInstance(lobbyPrefab);
                }
            }
            else
            {
                lobbyInstance = CreateLobbyInstance(lobbyPrefab);
            }

            return lobbyInstance;
        }

        private static GameObject CreateLobbyInstance(GameObject lobbyPrefab)
        {
            GameObject instance = Object.Instantiate(lobbyPrefab);
            instance.name = lobbyPrefab.name;
            StripLegacyNestedNetworkManager(instance);
            RepairTmpMaterials(instance);
            Object.DontDestroyOnLoad(instance);
            instance.SetActive(true);
            PvpLobby lobbyComponent = instance.GetComponent<PvpLobby>();
            lobbyComponent?.Hide();
            return instance;
        }

        private static GameObject ResolveLobbyPrefab()
        {
            GameObject prefab = Resources.Load<GameObject>(LobbyPrefabResourcePath);
            if (prefab != null)
            {
                return prefab;
            }

            return fallbackLobbyPrefab;
        }

        private static bool EnsureNetworkHost()
        {
            if (Object.FindFirstObjectByType<NetworkManager>(FindObjectsInactive.Include) != null)
            {
                return true;
            }

            GameObject networkPrefab = Resources.Load<GameObject>(NetworkHostResourcePath);
            if (networkPrefab == null)
            {
                return false;
            }

            GameObject networkInstance = Object.Instantiate(networkPrefab);
            networkInstance.name = networkPrefab.name;
            Object.DontDestroyOnLoad(networkInstance);
            return true;
        }

        private static void RepairTmpMaterials(GameObject root)
        {
            TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (text.font == null || text.fontSharedMaterial != null)
                {
                    continue;
                }

                text.fontSharedMaterial = text.font.material;
            }
        }

        private static void StripLegacyNestedNetworkManager(GameObject lobbyHost)
        {
            Transform nestedNetwork = lobbyHost.transform.Find("BattlePvpNetworkManager");
            if (nestedNetwork == null)
            {
                return;
            }

            Object.Destroy(nestedNetwork.gameObject);
        }
    }
}
