using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public class CodePacker : EditorWindow
{
    private string googleDrivePath = "D:\\SyncDriveScript\\ClayMonsters/";
    private string targetFolder = "Assets/";

    [MenuItem("Tools/Code Packer to Drive")]
    public static void ShowWindow()
    {
        GetWindow<CodePacker>("Code Packer");
    }

    private void OnGUI()
    {
        GUILayout.Label("Unity Code Packer for Google Drive", EditorStyles.boldLabel);

        EditorGUILayout.Space();
        targetFolder = EditorGUILayout.TextField("対象フォルダ (読み込み元)", targetFolder);
        googleDrivePath = EditorGUILayout.TextField("Googleドライブ出力パス", googleDrivePath);

        EditorGUILayout.Space();
        if (GUILayout.Button("ソースコードを結合してドライブに保存", GUILayout.Height(40)))
        {
            PackAndSave();
        }
    }

    private void PackAndSave()
    {
        // 出力先フォルダが存在しない場合は作成
        try
        {
            if (!Directory.Exists(googleDrivePath))
            {
                Directory.CreateDirectory(googleDrivePath);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Googleドライブのパスが無効、またはアクセスできません: {e.Message}");
            return;
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"=========================================");
        sb.AppendLine($"Unity Source Code Archive");
        sb.AppendLine($"出力日時: {DateTime.Now}");
        sb.AppendLine($"対象フォルダ: {targetFolder}");
        sb.AppendLine($"=========================================\n");

        int fileCount = 0;

        // .cs と .shader ファイルを再帰的に取得
        string[] extensions = { "*.cs", "*.shader" };
        foreach (var ext in extensions)
        {
            if (!Directory.Exists(targetFolder)) continue;

            string[] files = Directory.GetFiles(targetFolder, ext, SearchOption.AllDirectories);
            foreach (string file in files)
            {
                // .metaファイルなどは除外（GetFilesの仕様上含まれないが念のため）
                if (file.EndsWith(".meta")) continue;

                sb.AppendLine($"// =========================================");
                sb.AppendLine($"// FILE: {file}");
                sb.AppendLine($"// =========================================");

                try
                {
                    string content = File.ReadAllText(file);
                    sb.AppendLine(content);
                }
                catch (Exception e)
                {
                    sb.AppendLine($"/* ファイルの読み込みに失敗しました: {e.Message} */");
                }

                sb.AppendLine("\n\n");
                fileCount++;
            }
        }

        // ファイル名の決定（例: Backup_20260605_180000.txt）
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fileName = $"CodeBackup_{timestamp}.txt";
        string fullOutputPath = Path.Combine(googleDrivePath, fileName);

        try
        {
            File.WriteAllText(fullOutputPath, sb.ToString(), Encoding.UTF8);
            Debug.Log($"成功: {fileCount} 個のファイルを結合し、Googleドライブに保存しました。\nパス: {fullOutputPath}");
            EditorUtility.DisplayDialog("成功", $"{fileCount} 個のファイルを結合し、Googleドライブに保存しました。\nPC版Googleドライブが自動同期を開始します。", "OK");
        }
        catch (Exception e)
        {
            Debug.LogError($"ファイルの書き込みに失敗しました: {e.Message}");
            EditorUtility.DisplayDialog("エラー", $"ファイルの保存に失敗しました:\n{e.Message}", "OK");
        }
    }
}