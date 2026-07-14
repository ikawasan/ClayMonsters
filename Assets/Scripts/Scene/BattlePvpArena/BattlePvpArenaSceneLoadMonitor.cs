using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityScene = UnityEngine.SceneManagement.Scene;

namespace Scene.BattlePvpArena
{
    /// <summary>
    /// BattlePvpArenaシーンのロードとアンロードを監視する
    /// </summary>
    public static class BattlePvpArenaSceneLoadMonitor
    {
        private const string SceneName = "BattlePvpArena";

        /// <summary>
        /// シーンイベント監視を登録する
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        /// <summary>
        /// シーンHierarchyの状態をログ出力する
        /// </summary>
        public static void LogSceneState(UnityScene scene, string phase)
        {
            if (!scene.IsValid() || scene.name != SceneName)
            {
                return;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            var builder = new StringBuilder();
            builder.Append("[BattlePvpArena] scene state");
            builder.Append($" phase={phase}");
            builder.Append($" roots={roots.Length}");
            for (int i = 0; i < roots.Length; i++)
            {
                builder.Append($" [{roots[i].name} children={roots[i].transform.childCount}]");
            }

            Debug.Log(builder.ToString());
        }

        private static void OnSceneLoaded(UnityScene scene, LoadSceneMode mode)
        {
            if (scene.name != SceneName)
            {
                return;
            }

            LogSceneState(scene, $"loaded mode={mode}");
        }

        private static void OnSceneUnloaded(UnityScene scene)
        {
            if (scene.name != SceneName)
            {
                return;
            }

            Debug.LogError("[BattlePvpArena] シーンがアンロードされました");
        }
    }
}
