#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace SteamIntegration.Editor
{
    /// <summary>
    /// 製品ビルドから開発用steam_appid.txtを除去する
    /// </summary>
    public static class SteamBuildAppIdGuard
    {
        private const string SteamAppIdFileName = "steam_appid.txt";

        [PostProcessBuild(1)]
        private static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.StandaloneWindows
                && target != BuildTarget.StandaloneWindows64
                && target != BuildTarget.StandaloneLinux64
                && target != BuildTarget.StandaloneOSX)
            {
                return;
            }

            string buildDirectory = Path.GetDirectoryName(pathToBuiltProject);
            if (string.IsNullOrEmpty(buildDirectory))
            {
                return;
            }

            TryDelete(Path.Combine(buildDirectory, SteamAppIdFileName));

            string dataDirectory = Path.Combine(
                buildDirectory,
                Path.GetFileNameWithoutExtension(pathToBuiltProject) + "_Data");
            TryDelete(Path.Combine(dataDirectory, SteamAppIdFileName));
        }

        private static void TryDelete(string path)
        {
            if (!File.Exists(path))
            {
                return;
            }

            try
            {
                File.Delete(path);
                Debug.Log($"[SteamBuildAppIdGuard] 製品ビルドから削除しました: {path}");
            }
            catch (IOException exception)
            {
                Debug.LogWarning(
                    $"[SteamBuildAppIdGuard] 削除に失敗しました: {path} {exception.Message}");
            }
        }
    }
}
#endif
