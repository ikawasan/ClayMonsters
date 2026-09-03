using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Steam提出用Windows64リリースビルド
/// </summary>
public static class SteamReleaseBuilder
{
    private const string DefaultOutputRelativePath =
        @"ClayMonstersSteam\ClayMonsters.exe";

    /// <summary>
    /// バッチモードから呼び出すSteamリリースビルド
    /// </summary>
    public static void BuildWindows64()
    {
        string outputPath = ResolveOutputPath();
        string outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            throw new InvalidOperationException(
                "[SteamReleaseBuilder] Build Settingsに有効なシーンがありません");
        }

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.CompressWithLz4HC,
        };

        Debug.Log($"[SteamReleaseBuilder] Build start -> {outputPath}");
        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        if (summary.result != BuildResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"[SteamReleaseBuilder] Build failed: {summary.result}");
        }

        Debug.Log(
            $"[SteamReleaseBuilder] Build succeeded size={summary.totalSize} bytes path={outputPath}");
    }

    private static string ResolveOutputPath()
    {
        string fromEnv = Environment.GetEnvironmentVariable("CLAYMONSTERS_STEAM_BUILD_PATH");
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return fromEnv;
        }

        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        return Path.Combine(desktop, DefaultOutputRelativePath);
    }
}
