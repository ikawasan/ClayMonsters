#if UNITY_EDITOR
using Scene.TrainingScene.View;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityScene = UnityEngine.SceneManagement.Scene;

/// <summary>
/// 育成再開ウィンドウのプレハブ抽出ツール
/// レイアウト調整はプレハブ編集で行いシーンはプレハブインスタンスを保持する
/// </summary>
public static class TrainingResumeWindowPrefabUtility
{
    public const string PrefabPath = "Assets/Resources/UI/TrainingResumeWindow.prefab";

    private const string PrefabDirectory = "Assets/Resources/UI";
    private const string TrainingScenePath = "Assets/Scenes/Training.unity";

    /// <summary>
    /// TrainingResumeWindowプレハブのインスタンスかどうかを返す
    /// </summary>
    /// <param name="gameObject">判定対象</param>
    /// <returns>対象プレハブのインスタンスならtrue</returns>
    public static bool IsResumeWindowPrefabInstance(GameObject gameObject)
    {
        if (gameObject == null)
        {
            return false;
        }

        string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameObject);
        return prefabPath == PrefabPath;
    }

    /// <summary>
    /// 指定シーン上のTrainingResumeWindowをプレハブ状態へ同期する
    /// プレハブインスタンスはOverrideを解除しインライン配置は差し替える
    /// </summary>
    /// <param name="scene">対象シーン</param>
    /// <returns>変更があった場合true</returns>
    public static bool SyncInstancesInScene(UnityScene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return false;
        }

        bool changed = false;
        TrainingResumeWindowView[] resumeWindows =
            Object.FindObjectsByType<TrainingResumeWindowView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < resumeWindows.Length; i++)
        {
            TrainingResumeWindowView resumeWindow = resumeWindows[i];
            if (resumeWindow == null || resumeWindow.gameObject.scene != scene)
            {
                continue;
            }

            if (IsResumeWindowPrefabInstance(resumeWindow.gameObject))
            {
                GameObject instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(resumeWindow.gameObject);
                if (instanceRoot != null
                    && PrefabUtility.HasPrefabInstanceAnyOverrides(instanceRoot, false))
                {
                    PrefabUtility.RevertPrefabInstance(instanceRoot, InteractionMode.AutomatedAction);
                    changed = true;
                }

                RewireResumeWindowOwner(resumeWindow);
                continue;
            }

            if (TrainingSceneCreator.TryReplaceInlineTrainingResumeWindowWithPrefab(out _))
            {
                changed = true;
            }
        }

        return changed;
    }

    private static void RewireResumeWindowOwner(TrainingResumeWindowView resumeWindow)
    {
        if (resumeWindow == null)
        {
            return;
        }

        TrainingHudView hudView = resumeWindow.GetComponentInParent<TrainingHudView>(true);
        if (hudView != null)
        {
            TrainingSceneCreator.WireTrainingHudReferencesOnly(hudView);
        }
    }

    /// <summary>
    /// シーン上のTrainingResumeWindowをプレハブへ抽出しインスタンスとして接続する
    /// 既にプレハブインスタンスの場合はプレハブアセットを選択して知らせる
    /// </summary>
    public static void ExtractTrainingResumeWindowPrefab()
    {
        TrainingResumeWindowView windowView = FindResumeWindowView();
        if (windowView == null)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EditorSceneManager.OpenScene(TrainingScenePath, OpenSceneMode.Single);
            windowView = FindResumeWindowView();
        }

        if (windowView == null)
        {
            ShowDialog(
                "TrainingシーンにTrainingResumeWindowViewが見つかりません\n"
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
                "TrainingResumeWindowは既にプレハブインスタンスです\n"
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
                $"[TrainingResumeWindowPrefabUtility] プレハブの保存に失敗しました: {PrefabPath}",
                windowObject);
            return;
        }

        EditorSceneManager.MarkSceneDirty(windowObject.scene);
        EditorSceneManager.SaveScene(windowObject.scene);

        Selection.activeObject = prefabAsset;
        EditorGUIUtility.PingObject(prefabAsset);
        Debug.Log(
            $"[TrainingResumeWindowPrefabUtility] プレハブへ抽出しました: {PrefabPath}",
            prefabAsset);
        ShowDialog(
            "TrainingResumeWindowをプレハブへ抽出しました\n"
                + PrefabPath + "\n\n"
                + "レイアウト調整はこのプレハブをダブルクリックして編集してください\n"
                + "編集内容はシーンのインスタンスへ自動反映されます");
    }

    /// <summary>
    /// シーン上のインラインTrainingResumeWindowをプレハブインスタンスへ差し替える
    /// プレハブを編集済みだがPlayに反映されない場合に使う
    /// </summary>
    public static void ApplyTrainingResumeWindowPrefabToScene()
    {
        TrainingResumeWindowView windowView = FindResumeWindowView();
        if (windowView == null)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EditorSceneManager.OpenScene(TrainingScenePath, OpenSceneMode.Single);
        }

        if (!Application.isBatchMode
            && !EditorUtility.DisplayDialog(
                "Training Resume Window Prefab",
                "シーン上のTrainingResumeWindowを削除し\n"
                    + PrefabPath + " から再配置します\n\n"
                    + "インライン配置のレイアウトは失われます\n"
                    + "プレハブのレイアウトがPlay時に使われます",
                "差し替える",
                "キャンセル"))
        {
            return;
        }

        bool success = TrainingSceneCreator.TryReplaceInlineTrainingResumeWindowWithPrefab(out string message);
        if (!success)
        {
            ShowDialog(message);
            return;
        }

        TrainingResumeWindowView newWindow = FindResumeWindowView();
        if (newWindow != null)
        {
            Selection.activeGameObject = newWindow.gameObject;
            EditorGUIUtility.PingObject(newWindow.gameObject);
        }

        Debug.Log($"[TrainingResumeWindowPrefabUtility] {message}", newWindow);
        ShowDialog(message);
    }

    private static TrainingResumeWindowView FindResumeWindowView()
    {
        return Object.FindFirstObjectByType<TrainingResumeWindowView>(FindObjectsInactive.Include);
    }

    private static void ShowDialog(string message)
    {
        if (Application.isBatchMode)
        {
            Debug.Log($"[TrainingResumeWindowPrefabUtility] {message}");
            return;
        }

        EditorUtility.DisplayDialog("Training Resume Window Prefab", message, "OK");
    }
}
#endif
