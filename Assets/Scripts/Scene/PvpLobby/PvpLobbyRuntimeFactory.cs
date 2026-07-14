using Scene.BattlePVPScene.Network;
using Scene.BattlePVPScene.View;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer.Unity;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
using UnityScene = UnityEngine.SceneManagement.Scene;
#endif

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
#if UNITY_EDITOR
        private const string BattlePvpScenePath = "Assets/Scenes/BattlePVP.unity";
#endif

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
            if (lobbyPrefab != null)
            {
                if (!EnsureNetworkHost())
                {
                    Debug.LogError(
                        "[PvpLobby] NetworkHostプレハブ未配置"
                        + $" path=Resources/{NetworkHostResourcePath}"
                        + " Tools/ClayMonsters/Create PvpLobby Prefab Onlyを実行してください");
                    return false;
                }

                instance = InstantiateLobby(lobbyPrefab);
                return instance != null;
            }

#if UNITY_EDITOR
            if (TryBuildFromBattlePvpScene(out instance))
            {
                return true;
            }
#endif

            return false;
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

#if UNITY_EDITOR
        private static bool TryBuildFromBattlePvpScene(out GameObject instance)
        {
            instance = null;
            if (!System.IO.File.Exists(BattlePvpScenePath))
            {
                return false;
            }

            UnityScene referenceScene = EditorSceneManager.LoadSceneInPlayMode(
                BattlePvpScenePath,
                new LoadSceneParameters(LoadSceneMode.Additive));
            if (!referenceScene.IsValid())
            {
                return false;
            }

            Canvas matchmakingCanvas = FindCanvasInScene(referenceScene, "MatchmakingCanvas");
            GameObject networkObject = FindRootInScene(referenceScene, "BattlePvpNetworkManager");
            if (matchmakingCanvas == null || networkObject == null)
            {
                Debug.LogError(
                    "[PvpLobby] BattlePVPシーンからロビーUIまたはNetworkManagerが見つかりません"
                    + $" canvas={(matchmakingCanvas != null)}"
                    + $" network={(networkObject != null)}");
                EditorSceneManager.UnloadSceneAsync(referenceScene);
                return false;
            }

            GameObject networkCopy = Object.Instantiate(networkObject);
            networkCopy.name = "BattlePvpNetworkManager";
            Object.DontDestroyOnLoad(networkCopy);

            var host = new GameObject("PvpLobbyHost");
            host.AddComponent<PvpLobby>();

            GameObject canvasCopy = Object.Instantiate(matchmakingCanvas.gameObject, host.transform);
            canvasCopy.name = "MatchmakingCanvas";
            RectTransform canvasRect = canvasCopy.GetComponent<RectTransform>();
            if (canvasRect != null)
            {
                canvasRect.anchorMin = Vector2.zero;
                canvasRect.anchorMax = Vector2.one;
                canvasRect.offsetMin = Vector2.zero;
                canvasRect.offsetMax = Vector2.zero;
                canvasRect.localScale = Vector3.one;
            }

            RepairTmpMaterials(host);

            GameObject diObject = new GameObject("PvpLobbyDi");
            diObject.transform.SetParent(host.transform, false);
            PvpLobbyLifetimeScope.SetPendingRuntimeConfiguration(
                host.GetComponent<PvpLobby>(),
                canvasCopy.GetComponentInChildren<BattlePVPView>(true),
                networkCopy.GetComponent<NetworkManager>(),
                networkCopy.GetComponent<BattlePvpSessionSpawner>());
            diObject.AddComponent<PvpLobbyLifetimeScope>();

            PvpLobby lobby = host.GetComponent<PvpLobby>();
            lobby.ConfigureForRuntime(canvasCopy);

            if (parentScope != null)
            {
                using (LifetimeScope.EnqueueParent(parentScope))
                {
                    host.SetActive(true);
                }
            }
            else
            {
                host.SetActive(true);
            }

            lobby.Hide();
            Object.DontDestroyOnLoad(host);
            EditorSceneManager.UnloadSceneAsync(referenceScene);
            instance = host;
            Debug.Log("[PvpLobby] EditorフォールバックでDDOLロビーを生成しました");
            return true;
        }

        private static Canvas FindCanvasInScene(UnityScene scene, string canvasName)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Canvas[] canvases = roots[i].GetComponentsInChildren<Canvas>(true);
                for (int j = 0; j < canvases.Length; j++)
                {
                    if (canvases[j].name == canvasName)
                    {
                        return canvases[j];
                    }
                }
            }

            return null;
        }

        private static GameObject FindRootInScene(UnityScene scene, string objectName)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == objectName)
                {
                    return roots[i];
                }

                Transform[] transforms = roots[i].GetComponentsInChildren<Transform>(true);
                for (int j = 0; j < transforms.Length; j++)
                {
                    if (transforms[j].name == objectName)
                    {
                        return transforms[j].gameObject;
                    }
                }
            }

            return null;
        }
#endif
    }
}
