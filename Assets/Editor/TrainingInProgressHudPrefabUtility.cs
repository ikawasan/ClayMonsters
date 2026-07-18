#if UNITY_EDITOR
using Scene.TrainingScene.View;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 育成中HUDコンテンツのプレハブ抽出ツール
/// 曜日・体力・ステータス・行き先・ログ等のみをプレハブ化する
/// </summary>
public static class TrainingInProgressHudPrefabUtility
{
    public const string PrefabPath = "Assets/Resources/UI/TrainingInProgressHud.prefab";
    public const string ContentRootName = "TrainingInProgressHud";

    private const string PrefabDirectory = "Assets/Resources/UI";
    private const string TrainingScenePath = "Assets/Scenes/Training.unity";

    private static readonly HashSet<string> ModalChildNames = new HashSet<string>
    {
        "TrainingResumeWindow",
        "TrainingModeSelectWindow",
        "TrainingAutoResultWindow",
        "ResumeContinueButton",
        "ResumeRestartButton",
    };

    /// <summary>
    /// TrainingHudViewのSerializeField参照だけ配線する
    /// RectTransformとHierarchyは変更しない
    /// </summary>
    public static void FixTrainingInProgressHudPanelTexts()
    {
        TrainingHudView hudView = FindTrainingHudView();
        if (hudView != null)
        {
            TrainingSceneCreator.WireTrainingHudReferencesOnly(hudView);
            EditorSceneManager.MarkSceneDirty(hudView.gameObject.scene);
        }

        ShowDialog(
            "TrainingHudViewの参照配線のみ更新しました\n"
                + "TrainingInProgressHud.prefabのRectTransformとHierarchyは変更していません\n"
                + PrefabPath);
    }

    /// <summary>
    /// シーン上の育成中HUDコンテンツをプレハブへ抽出しインスタンスとして接続する
    /// </summary>
    public static void ExtractTrainingInProgressHudPrefab()
    {
        TrainingHudView hudView = FindTrainingHudView();
        if (hudView == null)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EditorSceneManager.OpenScene(TrainingScenePath, OpenSceneMode.Single);
            hudView = FindTrainingHudView();
        }

        if (hudView == null)
        {
            ShowDialog(
                "TrainingシーンにTrainingHudViewが見つかりません\n"
                    + "Tools/ClayMonsters/Wire Training Scene References を先に実行してください");
            return;
        }

        MigrateLegacyWholeCanvasPrefab(hudView);

        Transform contentRoot = EnsureContentRoot(hudView, migrateExistingChildren: true);
        if (contentRoot == null)
        {
            ShowDialog("TrainingInProgressHudの生成に失敗しました");
            return;
        }

        if (contentRoot == hudView.transform)
        {
            ShowDialog(
                "育成中UIのコンテンツルートを分離できませんでした\n"
                    + "Tools/ClayMonsters/Migrate Training Hud Canvas Split を実行してください");
            return;
        }

        if (!PrefabUtility.IsPartOfPrefabInstance(hudView.gameObject)
            && PrefabUtility.IsPartOfPrefabInstance(contentRoot.gameObject))
        {
            GameObject existingAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existingAsset != null)
            {
                Selection.activeObject = existingAsset;
                EditorGUIUtility.PingObject(existingAsset);
            }

            ShowDialog(
                "TrainingInProgressHudは既にプレハブインスタンスです\n"
                    + "レイアウト調整はプレハブを直接編集してください\n"
                    + PrefabPath);
            return;
        }

        StripCanvasHostComponentsFromContentRoot(contentRoot);

        if (PrefabUtility.IsPartOfPrefabInstance(contentRoot.gameObject))
        {
            PrefabUtility.UnpackPrefabInstance(
                contentRoot.gameObject,
                PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction);
            StripCanvasHostComponentsFromContentRoot(contentRoot);
        }

        Directory.CreateDirectory(PrefabDirectory);

        GameObject prefabAsset = PrefabUtility.SaveAsPrefabAssetAndConnect(
            contentRoot.gameObject,
            PrefabPath,
            InteractionMode.AutomatedAction,
            out bool success);
        if (!success || prefabAsset == null)
        {
            Debug.LogError(
                $"[TrainingInProgressHudPrefabUtility] プレハブの保存に失敗しました: {PrefabPath}",
                contentRoot.gameObject);
            return;
        }

        EditorSceneManager.MarkSceneDirty(hudView.gameObject.scene);
        EditorSceneManager.SaveScene(hudView.gameObject.scene);

        Selection.activeObject = prefabAsset;
        EditorGUIUtility.PingObject(prefabAsset);
        Debug.Log(
            $"[TrainingInProgressHudPrefabUtility] プレハブへ抽出しました: {PrefabPath}",
            prefabAsset);
        ShowDialog(
            "育成中UI(TrainingInProgressHud)をプレハブへ抽出しました\n"
                + PrefabPath + "\n\n"
                + "曜日・体力・ステータス・行き先・ログ等のレイアウトは\n"
                + "このプレハブをダブルクリックして編集してください\n\n"
                + "TrainingHudCanvasと各モーダルはシーン側に残ります");
    }

    /// <summary>
    /// Canvas全体プレハブ化の旧構成をシーンCanvas+コンテンツ子プレハブへ移行する
    /// </summary>
    public static void MigrateTrainingHudCanvasSplitMenu()
    {
        TrainingHudView hudView = FindTrainingHudView();
        if (hudView == null)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EditorSceneManager.OpenScene(TrainingScenePath, OpenSceneMode.Single);
            hudView = FindTrainingHudView();
        }

        if (hudView == null)
        {
            ShowDialog("TrainingシーンにTrainingHudViewが見つかりません");
            return;
        }

        if (!MigrateLegacyWholeCanvasPrefab(hudView))
        {
            ShowDialog(
                "移行対象の旧構成は見つかりませんでした\n"
                    + "TrainingHudCanvas配下にTrainingInProgressHudがある場合は\n"
                    + "Hierarchyで構成を確認してください");
            return;
        }

        EditorSceneManager.MarkSceneDirty(hudView.gameObject.scene);
        ShowDialog(
            "TrainingHudCanvasをシーン配置へ移行しました\n"
                + "育成中UIはTrainingInProgressHud配下に集約しました\n\n"
                + "続けてHierarchyで参照とプレハブ構成を確認してください");
    }

    /// <summary>
    /// Canvas全体プレハブの旧構成を分離する
    /// </summary>
    /// <param name="hudView">HUDビュー</param>
    /// <returns>変更があればtrue</returns>
    public static bool MigrateLegacyWholeCanvasPrefab(TrainingHudView hudView)
    {
        if (hudView == null)
        {
            return false;
        }

        bool changed = false;
        GameObject hudObject = hudView.gameObject;
        if (PrefabUtility.IsPartOfPrefabInstance(hudObject))
        {
            PrefabUtility.UnpackPrefabInstance(
                hudObject,
                PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction);
            changed = true;
        }

        Transform hudRoot = hudView.transform;
        if (hudRoot.name == ContentRootName)
        {
            hudRoot.name = "TrainingHudCanvas";
            changed = true;
        }

        Transform contentRoot = hudRoot.Find(ContentRootName);
        if (contentRoot == null)
        {
            var rootObject = new GameObject(ContentRootName, typeof(RectTransform));
            contentRoot = rootObject.transform;
            contentRoot.SetParent(hudRoot, false);
            StretchRect(contentRoot as RectTransform);
            contentRoot.SetAsFirstSibling();
            changed = true;
        }

        int movedCount = MigrateContentChildrenToRoot(hudRoot, contentRoot);
        if (movedCount > 0)
        {
            changed = true;
        }

        StripCanvasHostComponentsFromContentRoot(contentRoot);
        EditorUtility.SetDirty(hudObject);
        return changed;
    }

    /// <summary>
    /// 育成中UIコンテンツのルートを取得または生成する
    /// </summary>
    /// <param name="hudView">HUDビュー</param>
    /// <param name="migrateExistingChildren">Canvas直下の育成中UIをルート配下へ移すか</param>
    /// <returns>コンテンツルート</returns>
    public static Transform EnsureContentRoot(TrainingHudView hudView, bool migrateExistingChildren)
    {
        if (hudView == null)
        {
            return null;
        }

        Transform hudRoot = hudView.transform;
        Transform contentRoot = hudRoot.Find(ContentRootName);
        if (contentRoot == null)
        {
            var rootObject = new GameObject(ContentRootName, typeof(RectTransform));
            contentRoot = rootObject.transform;
            contentRoot.SetParent(hudRoot, false);
            StretchRect(contentRoot as RectTransform);
            contentRoot.SetAsFirstSibling();
            EditorUtility.SetDirty(hudView.gameObject);
        }

        if (migrateExistingChildren)
        {
            MigrateContentChildrenToRoot(hudRoot, contentRoot);
        }

        return contentRoot;
    }

    /// <summary>
    /// モーダルウィンドウかどうかを返す
    /// </summary>
    /// <param name="objectName">GameObject名</param>
    /// <returns>モーダルならtrue</returns>
    public static bool IsModalHudChildName(string objectName)
    {
        return !string.IsNullOrEmpty(objectName) && ModalChildNames.Contains(objectName);
    }

    private static int MigrateContentChildrenToRoot(Transform hudRoot, Transform contentRoot)
    {
        if (hudRoot == null || contentRoot == null)
        {
            return 0;
        }

        var children = new List<Transform>();
        for (int i = 0; i < hudRoot.childCount; i++)
        {
            Transform child = hudRoot.GetChild(i);
            if (child == contentRoot || IsModalHudChildName(child.name))
            {
                continue;
            }

            children.Add(child);
        }

        for (int i = 0; i < children.Count; i++)
        {
            children[i].SetParent(contentRoot, true);
        }

        if (children.Count > 0)
        {
            EditorUtility.SetDirty(contentRoot.gameObject);
        }

        return children.Count;
    }

    private static void StripCanvasHostComponentsFromContentRoot(Transform contentRoot)
    {
        if (contentRoot == null)
        {
            return;
        }

        GameObject contentObject = contentRoot.gameObject;
        TrainingHudView hudViewOnContent = contentObject.GetComponent<TrainingHudView>();
        if (hudViewOnContent != null)
        {
            Object.DestroyImmediate(hudViewOnContent);
        }

        Canvas canvas = contentObject.GetComponent<Canvas>();
        if (canvas != null)
        {
            Object.DestroyImmediate(canvas);
        }

        CanvasScaler scaler = contentObject.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            Object.DestroyImmediate(scaler);
        }

        GraphicRaycaster raycaster = contentObject.GetComponent<GraphicRaycaster>();
        if (raycaster != null)
        {
            Object.DestroyImmediate(raycaster);
        }

        CanvasGroup canvasGroup = contentObject.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            Object.DestroyImmediate(canvasGroup);
        }
    }

    private static TrainingHudView FindTrainingHudView()
    {
        return Object.FindFirstObjectByType<TrainingHudView>(FindObjectsInactive.Include);
    }

    private static void StretchRect(RectTransform rect)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static void ShowDialog(string message)
    {
        if (Application.isBatchMode)
        {
            Debug.Log($"[TrainingInProgressHudPrefabUtility] {message}");
            return;
        }

        EditorUtility.DisplayDialog("Training In Progress HUD Prefab", message, "OK");
    }
}
#endif
