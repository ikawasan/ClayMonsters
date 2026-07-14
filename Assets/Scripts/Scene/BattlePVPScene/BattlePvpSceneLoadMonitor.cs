using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityScene = UnityEngine.SceneManagement.Scene;

namespace Scene.BattlePVPScene
{
    /// <summary>
    /// BattlePVPシーンのロードとアンロードを起動直後から監視する
    /// </summary>
    public static class BattlePvpSceneLoadMonitor
    {
        private const string SceneName = "BattlePVP";

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

        private static void OnSceneLoaded(UnityScene scene, LoadSceneMode mode)
        {
            if (scene.name != SceneName)
            {
                return;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            var builder = new StringBuilder();
            builder.Append("[BattlePvpScene] シーンロード検知");
            builder.Append($" mode={mode}");
            builder.Append($" roots={roots.Length}");
            for (int i = 0; i < roots.Length; i++)
            {
                builder.Append($" [{roots[i].name}]");
            }

            Debug.LogWarning(builder.ToString());
        }

        private static void OnSceneUnloaded(UnityScene scene)
        {
            if (scene.name != SceneName)
            {
                return;
            }

            Debug.LogError(
                "[BattlePvpScene] シーンアンロード検知"
                + $" isLoaded={scene.isLoaded}"
                + $" BattlePVPSceneFound={(BattlePvpSceneDiagnostics.FindBattlePvpSceneRoot() != null)}");
        }
    }
}
