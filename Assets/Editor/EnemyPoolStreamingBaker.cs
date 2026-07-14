#if UNITY_EDITOR
using System.IO;
using SaveData;
using UnityEditor;
using UnityEngine;

/// <summary>
/// エディタで作成した敵プールをStreamingAssetsへ焼き込むツール
/// persistentDataPathの敵セーブをビルド同梱先へコピーしRom単体でNPCを出せるようにする
/// </summary>
public static class EnemyPoolStreamingBaker
{
    private const string StreamingAssetsRoot = "Assets/StreamingAssets";

    [MenuItem("Tools/ClayMonsters/Bake Enemy Pool To StreamingAssets")]
    public static void BakeEnemyPool()
    {
        string destinationDir = Path.Combine(StreamingAssetsRoot, ModelSaveStorage.StreamingSubFolder);
        Directory.CreateDirectory(destinationDir);

        int copiedFiles = 0;
        int copiedSlots = 0;

        copiedFiles += CopyIfExists(
            ModelSavePoolSettings.GetMetadataFileName(ModelSavePool.Enemy),
            destinationDir);

        for (int i = 0; i < ModelSavePoolSettings.SlotCount; i++)
        {
            bool slotCopied = false;
            slotCopied |= CopyIfExists(ModelSavePoolSettings.GetGlbFileName(ModelSavePool.Enemy, i), destinationDir) > 0;
            slotCopied |= CopyIfExists(ModelSavePoolSettings.GetThumbnailFileName(ModelSavePool.Enemy, i), destinationDir) > 0;
            slotCopied |= CopyIfExists(ModelSavePoolSettings.GetVoxelFileName(ModelSavePool.Enemy, i), destinationDir) > 0;
            if (slotCopied)
            {
                copiedSlots++;
                copiedFiles += CountExisting(i);
            }
        }

        AssetDatabase.Refresh();

        if (copiedFiles == 0)
        {
            EditorUtility.DisplayDialog(
                "敵プールの焼き込み",
                $"コピー対象が見つかりませんでした\nエディタで敵を保存してから実行してください\n\n参照元: {ModelSaveStorage.WritableRoot}",
                "OK");
            return;
        }

        EditorUtility.DisplayDialog(
            "敵プールの焼き込み",
            $"敵プールをStreamingAssetsへ焼き込みました\n\nスロット: {copiedSlots}件\nファイル: {copiedFiles}件\n出力先: {destinationDir}",
            "OK");
    }

    private static int CountExisting(int slotIndex)
    {
        int count = 0;
        if (SourceExists(ModelSavePoolSettings.GetGlbFileName(ModelSavePool.Enemy, slotIndex)))
        {
            count++;
        }

        if (SourceExists(ModelSavePoolSettings.GetThumbnailFileName(ModelSavePool.Enemy, slotIndex)))
        {
            count++;
        }

        if (SourceExists(ModelSavePoolSettings.GetVoxelFileName(ModelSavePool.Enemy, slotIndex)))
        {
            count++;
        }

        return count;
    }

    private static bool SourceExists(string fileName) =>
        File.Exists(Path.Combine(ModelSaveStorage.WritableRoot, fileName));

    private static int CopyIfExists(string fileName, string destinationDir)
    {
        string sourcePath = Path.Combine(ModelSaveStorage.WritableRoot, fileName);
        if (!File.Exists(sourcePath))
        {
            return 0;
        }

        string destinationPath = Path.Combine(destinationDir, fileName);
        File.Copy(sourcePath, destinationPath, true);
        return 1;
    }
}
#endif
