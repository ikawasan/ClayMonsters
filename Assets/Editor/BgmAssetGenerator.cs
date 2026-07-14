using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// BGMのWAVアセットを生成する
/// </summary>
public static class BgmAssetGenerator
{
    private const string ScriptPath = "Tools/generate_bgm.py";

    [MenuItem("Tools/ClayMonsters/Generate BGM Assets")]
    public static void Generate()
    {
        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        string scriptFullPath = Path.Combine(projectRoot, ScriptPath);
        if (!File.Exists(scriptFullPath))
        {
            UnityEngine.Debug.LogError($"[BgmAssetGenerator] スクリプトが見つかりません: {scriptFullPath}");
            return;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "py",
            Arguments = $"\"{scriptFullPath}\"",
            WorkingDirectory = projectRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using Process process = Process.Start(startInfo);
        if (process == null)
        {
            UnityEngine.Debug.LogError("[BgmAssetGenerator] Pythonプロセスの起動に失敗しました");
            return;
        }

        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            UnityEngine.Debug.LogError($"[BgmAssetGenerator] 生成に失敗しました\n{error}");
            return;
        }

        if (!string.IsNullOrEmpty(output))
        {
            UnityEngine.Debug.Log(output.Trim());
        }

        AssetDatabase.Refresh();
        UnityEngine.Debug.Log("[BgmAssetGenerator] BGMアセットの生成が完了しました");
    }
}
