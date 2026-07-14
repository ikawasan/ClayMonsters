#if UNITY_EDITOR
using Scene.TrainingScene.View;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 育成完了リザルトウィンドウのプレハブ抽出ツール
/// レイアウト調整はプレハブ編集で行いシーンはプレハブインスタンスを保持する
/// </summary>
public static class TrainingAutoResultWindowPrefabUtility
{
    public const string PrefabPath = "Assets/Resources/UI/TrainingAutoResultWindow.prefab";

    private const string PrefabDirectory = "Assets/Resources/UI";
    private const string TrainingScenePath = "Assets/Scenes/Training.unity";

    /// <summary>
    /// シーン上のTrainingAutoResultWindowをプレハブへ抽出しインスタンスとして接続する
    /// 既にプレハブインスタンスの場合はプレハブアセットを選択して知らせる
    /// </summary>
    [MenuItem("Tools/ClayMonsters/Extract Training Auto Result Window Prefab")]
    public static void ExtractTrainingAutoResultWindowPrefab()
    {
        TrainingAutoResultView windowView = FindAutoResultWindowView();
        if (windowView == null)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EditorSceneManager.OpenScene(TrainingScenePath, OpenSceneMode.Single);
            windowView = FindAutoResultWindowView();
        }

        if (windowView == null)
        {
            ShowDialog(
                "TrainingシーンにTrainingAutoResultViewが見つかりません\n"
                    + "Tools/ClayMonsters/Wire Training Scene References を先に実行してください");
            return;
        }

        GameObject windowObject = windowView.gameObject;

        if (PrefabUtility.IsPartOfPrefabInstance(windowObject))
        {
            GameObject existingAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existingAsset != null)
            {
                Selection.activeObject = existingAsset;
                EditorGUIUtility.PingObject(existingAsset);
            }

            ShowDialog(
                "TrainingAutoResultWindowは既にプレハブインスタンスです\n"
                    + "レイアウト調整はプレハブを直接編集してください\n"
                    + PrefabPath);
            return;
        }

        Directory.CreateDirectory(PrefabDirectory);

        GameObject prefabAsset = PrefabUtility.SaveAsPrefabAssetAndConnect(
            windowObject,
            PrefabPath,
            InteractionMode.AutomatedAction,
            out bool success);
        if (!success || prefabAsset == null)
        {
            Debug.LogError(
                $"[TrainingAutoResultWindowPrefabUtility] プレハブの保存に失敗しました: {PrefabPath}",
                windowObject);
            return;
        }

        EditorSceneManager.MarkSceneDirty(windowObject.scene);
        EditorSceneManager.SaveScene(windowObject.scene);

        Selection.activeObject = prefabAsset;
        EditorGUIUtility.PingObject(prefabAsset);
        Debug.Log(
            $"[TrainingAutoResultWindowPrefabUtility] プレハブへ抽出しました: {PrefabPath}",
            prefabAsset);
        ShowDialog(
            "TrainingAutoResultWindowをプレハブへ抽出しました\n"
                + PrefabPath + "\n\n"
                + "レイアウト調整はこのプレハブをダブルクリックして編集してください\n"
                + "編集内容はシーンのインスタンスへ自動反映されます");
    }

    private static TrainingAutoResultView FindAutoResultWindowView()
    {
        return Object.FindFirstObjectByType<TrainingAutoResultView>(FindObjectsInactive.Include);
    }

    private static void ShowDialog(string message)
    {
        if (Application.isBatchMode)
        {
            Debug.Log($"[TrainingAutoResultWindowPrefabUtility] {message}");
            return;
        }

        EditorUtility.DisplayDialog("Training Auto Result Window Prefab", message, "OK");
    }
}
#endif
