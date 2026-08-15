using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Localization;
using UnityEngine;

namespace Scene.DesktopPet
{
    /// <summary>
    /// 軽量デスクトップペット外部プロセスの起動
    /// </summary>
    public static class DesktopPetExternalProcess
    {
        private const string ViewerFileName = "ClayMonstersPet.exe";
        private const string ViewerFolderName = "ClayMonstersPet";
        private const string ViewerProcessName = "ClayMonstersPet";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CloseRunningViewersOnGameStart()
        {
            DesktopPetSpriteCache.ClearLauncherKeepAliveMarker();
            CloseRunningViewers();
        }

        /// <summary>
        /// 起動中のデスクトップペット外部プロセスを終了する
        /// </summary>
        public static void CloseRunningViewers()
        {
            Process[] processes;
            try
            {
                processes = Process.GetProcessesByName(ViewerProcessName);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning(
                    "[DesktopPetExternalProcess] ペットプロセス検索失敗: " + exception.Message);
                return;
            }

            if (processes == null || processes.Length == 0)
            {
                return;
            }

            for (int i = 0; i < processes.Length; i++)
            {
                Process process = processes[i];
                try
                {
                    if (process.HasExited)
                    {
                        continue;
                    }

                    // 通常終了を試しだめなら強制終了する
                    if (!process.CloseMainWindow())
                    {
                        process.Kill();
                    }
                    else if (!process.WaitForExit(1500))
                    {
                        process.Kill();
                    }
                }
                catch (Exception exception)
                {
                    UnityEngine.Debug.LogWarning(
                        "[DesktopPetExternalProcess] ペット終了失敗: " + exception.Message);
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill();
                        }
                    }
                    catch
                    {
                        // 終了不能な場合は次のプロセスへ進む
                    }
                }
                finally
                {
                    process.Dispose();
                }
            }

            UnityEngine.Debug.Log(
                "[DesktopPetExternalProcess] 起動中のデスクトップペットを終了しました");
        }

        /// <summary>
        /// 外部ビューアを起動できたらtrue
        /// </summary>
        public static bool TryStart(IReadOnlyList<string> cacheDirectories, bool stayOnTop)
        {
#if !UNITY_STANDALONE_WIN || UNITY_EDITOR
            return false;
#else
            if (cacheDirectories == null || cacheDirectories.Count == 0)
            {
                return false;
            }

            List<string> valid = new List<string>(cacheDirectories.Count);
            for (int i = 0; i < cacheDirectories.Count; i++)
            {
                string directory = cacheDirectories[i];
                if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                {
                    valid.Add(directory);
                }
            }

            if (valid.Count == 0)
            {
                return false;
            }

            string viewerPath = ResolveViewerPath();
            if (string.IsNullOrEmpty(viewerPath) || !File.Exists(viewerPath))
            {
                return false;
            }

            string gameExe = ResolveGameExecutablePath();
            string languageCode = ResolveLanguageCode();
            string textTablesDirectory = ResolveTextTablesDirectory();
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = viewerPath,
                    Arguments = BuildArguments(
                        valid,
                        gameExe,
                        languageCode,
                        textTablesDirectory,
                        stayOnTop),
                    UseShellExecute = false,
                    WorkingDirectory = Path.GetDirectoryName(viewerPath) ?? string.Empty
                };
                Process process = Process.Start(startInfo);
                if (process == null)
                {
                    return false;
                }

                DesktopPetSpriteCache.WriteLauncherKeepAliveMarker();
                DesktopPetSpriteCache.WriteActiveMarker(valid);
                DesktopPetSpriteCache.WriteLanguageMarker(languageCode);
                return true;
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning(
                    "[DesktopPetExternalProcess] 外部ビューア起動失敗: " + exception.Message);
                return false;
            }
#endif
        }

        private static string BuildArguments(
            IReadOnlyList<string> cacheDirectories,
            string gameExe,
            string languageCode,
            string textTablesDirectory,
            bool stayOnTop)
        {
            StringBuilder args = new StringBuilder(256);
            for (int i = 0; i < cacheDirectories.Count; i++)
            {
                if (i > 0)
                {
                    args.Append(' ');
                }

                args.Append("--cache \"").Append(cacheDirectories[i]).Append('"');
            }

            if (!string.IsNullOrEmpty(gameExe))
            {
                args.Append(" --game \"").Append(gameExe).Append('"');
            }

            if (!string.IsNullOrEmpty(languageCode))
            {
                args.Append(" --lang \"").Append(languageCode).Append('"');
            }

            if (!string.IsNullOrEmpty(textTablesDirectory))
            {
                args.Append(" --text-dir \"").Append(textTablesDirectory).Append('"');
            }

            args.Append(" --topmost ").Append(stayOnTop ? "1" : "0");
            return args.ToString();
        }

        private static string ResolveLanguageCode()
        {
            string code = LocalizedText.CurrentLanguageCode;
            if (string.IsNullOrWhiteSpace(code))
            {
                return GameLanguageCodes.Default;
            }

            return code;
        }

        private static string ResolveTextTablesDirectory()
        {
            string directory = Path.Combine(Application.streamingAssetsPath, "TextTables");
            if (Directory.Exists(directory))
            {
                return directory;
            }

            return null;
        }

        private static string ResolveViewerPath()
        {
            string streaming = Path.Combine(
                Application.streamingAssetsPath,
                ViewerFolderName,
                ViewerFileName);
            if (File.Exists(streaming))
            {
                return streaming;
            }

            string besideData = Path.Combine(
                Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty,
                ViewerFileName);
            if (File.Exists(besideData))
            {
                return besideData;
            }

            string repoViewer = Path.GetFullPath(
                Path.Combine(
                    Application.dataPath,
                    "..",
                    ViewerFolderName,
                    "bin",
                    "Release",
                    "net9.0-windows",
                    "win-x64",
                    ViewerFileName));
            if (File.Exists(repoViewer))
            {
                return repoViewer;
            }

            string repoViewerAnyCpu = Path.GetFullPath(
                Path.Combine(
                    Application.dataPath,
                    "..",
                    ViewerFolderName,
                    "bin",
                    "Release",
                    "net9.0-windows",
                    ViewerFileName));
            if (File.Exists(repoViewerAnyCpu))
            {
                return repoViewerAnyCpu;
            }

            return null;
        }

        private static string ResolveGameExecutablePath()
        {
#if UNITY_EDITOR
            return null;
#else
            try
            {
                return Process.GetCurrentProcess().MainModule?.FileName;
            }
            catch
            {
                return null;
            }
#endif
        }
    }
}
