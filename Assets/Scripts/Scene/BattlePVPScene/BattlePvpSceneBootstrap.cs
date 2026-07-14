using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityScene = UnityEngine.SceneManagement.Scene;

namespace Scene.BattlePVPScene
{
    /// <summary>
    /// BattlePVPシーン読込直後にLifetimeScope分離とDDOL永続化を行う
    /// LighthouseのOnLoadより前にscopeがビルドされルートが破棄されるのを防ぐ
    /// </summary>
    public static class BattlePvpSceneBootstrap
    {
        private const string SceneName = "BattlePVP";

        /// <summary>
        /// シーン読込監視を登録する
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(UnityScene scene, LoadSceneMode mode)
        {
            if (scene.name != SceneName)
            {
                return;
            }

            RunDeferredSetup(scene).Forget();
        }

        private static async UniTaskVoid RunDeferredSetup(UnityScene scene)
        {
            await UniTask.Yield(PlayerLoopTiming.Initialization);
            EnsureSceneReady(scene, "sceneLoaded+1f");
            await UniTask.Yield(PlayerLoopTiming.Initialization);
            EnsureSceneReady(scene, "sceneLoaded+2f");
        }

        /// <summary>
        /// 指定Unityシーン内のBattlePVPコンテンツを保護する
        /// </summary>
        public static void EnsureSceneReady(UnityScene unityScene, string phase)
        {
            if (!unityScene.IsValid() || !unityScene.isLoaded)
            {
                return;
            }

            GameObject[] roots = unityScene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                BattlePVPScene scene = root.GetComponent<BattlePVPScene>();
                if (scene == null)
                {
                    scene = root.GetComponentInChildren<BattlePVPScene>(true);
                }

                if (scene == null)
                {
                    continue;
                }

                BattlePvpLifetimeScopeSetup.EnsureDedicatedScope(scene.transform);
                BattlePvpContentPersistence.EnsurePersisted(scene.gameObject);
            }

            NetworkManager[] networkManagers = FindNetworkManagersInScene(unityScene);
            for (int i = 0; i < networkManagers.Length; i++)
            {
                BattlePvpNetworkPersistence.EnsurePersisted(networkManagers[i]);
            }

            Debug.Log(
                "[BattlePvpScene] Bootstrap完了"
                + $" phase={phase}"
                + $" unityScene={unityScene.name}"
                + $" roots={roots.Length}");
            BattlePvpSceneDiagnostics.LogState($"Bootstrap-{phase}");
        }

        private static NetworkManager[] FindNetworkManagersInScene(UnityScene unityScene)
        {
            GameObject[] roots = unityScene.GetRootGameObjects();
            var managers = new System.Collections.Generic.List<NetworkManager>();
            for (int i = 0; i < roots.Length; i++)
            {
                managers.AddRange(roots[i].GetComponentsInChildren<NetworkManager>(true));
            }

            return managers.ToArray();
        }
    }
}
