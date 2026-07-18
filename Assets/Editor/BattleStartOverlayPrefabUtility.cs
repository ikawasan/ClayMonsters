#if UNITY_EDITOR
using Scene.BattleNpcScene.View;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityScene = UnityEngine.SceneManagement.Scene;

/// <summary>
/// 対戦紹介オーバーレイUIのプレハブ生成とシーン配線
/// レイアウト調整はプレハブ編集で行いWireは参照接続のみ行う
/// </summary>
public static class BattleStartOverlayPrefabUtility
{
    public const string PrefabPath = "Assets/Resources/UI/BattleStartOverlay.prefab";
    public const string OverlayRootName = "BattleStartOverlay";

    private const string PrefabDirectory = "Assets/Resources/UI";
    private const string BattleNpcScenePath = "Assets/Scenes/BattleNpc.unity";

    private static readonly string[] OverlayScenePaths =
    {
        "Assets/Scenes/BattleNpc.unity",
        "Assets/Scenes/BattlePvpArena.unity",
        "Assets/Scenes/Training.unity",
    };

    /// <summary>
    /// BattleNpcのBattleStartOverlayから共通プレハブを生成する
    /// </summary>
    [MenuItem("Tools/ClayMonsters/Create Battle Start Overlay Prefab From BattleNpc")]
    public static void CreatePrefabFromBattleNpc()
    {
        if (!Application.isBatchMode
            && !EditorUtility.DisplayDialog(
                "Battle Start Overlay Prefab",
                "BattleNpcのBattleStartOverlayから共通プレハブを生成します\n"
                    + "既存のBattleStartOverlay.prefabは上書きされます\n\n"
                    + "生成後は Sync Battle Start Overlay To All Scenes で各シーンへ反映できます",
                "生成する",
                "キャンセル"))
        {
            return;
        }

        CreatePrefabFromBattleNpcSilent();
    }

    /// <summary>
    /// Unity.exe -batchmode -quit -projectPath ... -executeMethod BattleStartOverlayPrefabUtility.CreatePrefabFromBattleNpcSilent
    /// </summary>
    public static void CreatePrefabFromBattleNpcSilent()
    {
        UnityScene previousScene = EditorSceneManager.GetActiveScene();
        string previousScenePath = previousScene.IsValid() && !string.IsNullOrEmpty(previousScene.path)
            ? previousScene.path
            : null;

        try
        {
            UnityScene battleNpcScene = EditorSceneManager.OpenScene(BattleNpcScenePath, OpenSceneMode.Single);
            if (!ExtractPrefabFromActiveScene())
            {
                return;
            }

            EnsurePrefabInstanceInScene(battleNpcScene);
            WireOverlayReferencesInScene(battleNpcScene);
            EditorSceneManager.MarkSceneDirty(battleNpcScene);
            EditorSceneManager.SaveScene(battleNpcScene);
            Debug.Log($"[BattleStartOverlayPrefabUtility] プレハブを生成しました: {PrefabPath}");
        }
        finally
        {
            if (!Application.isBatchMode
                && !string.IsNullOrEmpty(previousScenePath)
                && previousScenePath != BattleNpcScenePath
                && File.Exists(previousScenePath))
            {
                EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
            }
        }
    }

    /// <summary>
    /// 開いているシーンのBattleStartOverlayへ参照だけ配線する
    /// RectTransformとHierarchyは変更しない
    /// </summary>
    [MenuItem("Tools/ClayMonsters/Wire Battle Start Overlay To Open Scene")]
    public static void WireOpenSceneOverlay()
    {
        UnityScene scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogWarning("[BattleStartOverlayPrefabUtility] 有効なシーンが開かれていません");
            return;
        }

        int wired = WireOverlayReferencesInScene(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[BattleStartOverlayPrefabUtility] 参照配線完了 overlayViews={wired}: {scene.path}");
    }

    /// <summary>
    /// プレハブを全戦闘シーンへ同期する
    /// </summary>
    [MenuItem("Tools/ClayMonsters/Sync Battle Start Overlay To All Scenes")]
    public static void SyncAllScenesFromPrefab()
    {
        if (!Application.isBatchMode
            && !EditorUtility.DisplayDialog(
                "Battle Start Overlay Sync",
                "BattleStartOverlay.prefabを各戦闘シーンへ同期します\n"
                    + "既存のBattleStartOverlayはプレハブインスタンスに置き換えられます",
                "同期する",
                "キャンセル"))
        {
            return;
        }

        SyncAllScenesFromPrefabSilent();
    }

    /// <summary>
    /// Unity.exe -batchmode -quit -projectPath ... -executeMethod BattleStartOverlayPrefabUtility.SyncAllScenesFromPrefabSilent
    /// </summary>
    public static void SyncAllScenesFromPrefabSilent()
    {
        if (!File.Exists(PrefabPath))
        {
            Debug.LogError(
                $"[BattleStartOverlayPrefabUtility] プレハブが見つかりません: {PrefabPath}\n"
                    + "先に Create Battle Start Overlay Prefab From BattleNpc を実行してください");
            return;
        }

        UnityScene previousScene = EditorSceneManager.GetActiveScene();
        string previousScenePath = previousScene.IsValid() && !string.IsNullOrEmpty(previousScene.path)
            ? previousScene.path
            : null;

        try
        {
            for (int i = 0; i < OverlayScenePaths.Length; i++)
            {
                string scenePath = OverlayScenePaths[i];
                if (!File.Exists(scenePath))
                {
                    Debug.LogWarning($"[BattleStartOverlayPrefabUtility] シーンが見つかりません: {scenePath}");
                    continue;
                }

                try
                {
                    UnityScene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                    EnsurePrefabInstanceInScene(scene);
                    WireOverlayReferencesInScene(scene);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    Debug.Log($"[BattleStartOverlayPrefabUtility] 同期保存完了: {scenePath}");
                }
                catch (System.Exception exception)
                {
                    Debug.LogError(
                        $"[BattleStartOverlayPrefabUtility] 同期失敗: {scenePath}\n{exception}");
                }
            }
        }
        finally
        {
            if (!Application.isBatchMode
                && !string.IsNullOrEmpty(previousScenePath)
                && File.Exists(previousScenePath))
            {
                EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
            }
        }
    }

    /// <summary>
    /// プレハブ生成と全シーン同期を連続実行する
    /// </summary>
    [MenuItem("Tools/ClayMonsters/Create And Sync Battle Start Overlay Prefab")]
    public static void CreateAndSyncPrefab()
    {
        if (!Application.isBatchMode
            && !EditorUtility.DisplayDialog(
                "Battle Start Overlay Prefab",
                "BattleNpcからプレハブを生成し全戦闘シーンへ同期します",
                "実行する",
                "キャンセル"))
        {
            return;
        }

        CreateAndSyncPrefabSilent();
    }

    /// <summary>
    /// Unity.exe -batchmode -quit -projectPath ... -executeMethod BattleStartOverlayPrefabUtility.CreateAndSyncPrefabSilent
    /// </summary>
    public static void CreateAndSyncPrefabSilent()
    {
        CreatePrefabFromBattleNpcSilent();
        SyncAllScenesFromPrefabSilent();
    }

    /// <summary>
    /// Prefab Modeで編集するためにプレハブを開く
    /// </summary>
    [MenuItem("Tools/ClayMonsters/Open Battle Start Overlay Prefab For Editing")]
    public static void OpenPrefabForEditing()
    {
        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefabAsset == null)
        {
            Debug.LogWarning(
                $"[BattleStartOverlayPrefabUtility] プレハブが見つかりません: {PrefabPath}");
            return;
        }

        AssetDatabase.OpenAsset(prefabAsset);
    }

    /// <summary>
    /// PlayerNameLabelBackdropとEnemyNameLabelBackdropをプレハブから削除する
    /// </summary>
    [MenuItem("Tools/ClayMonsters/Remove Battle Start Overlay Name Label Backdrops")]
    public static void RemoveNameLabelBackdropsFromPrefab()
    {
        if (!File.Exists(PrefabPath))
        {
            Debug.LogError(
                $"[BattleStartOverlayPrefabUtility] プレハブが見つかりません: {PrefabPath}");
            return;
        }

        if (!Application.isBatchMode
            && !EditorUtility.DisplayDialog(
                "Battle Start Overlay",
                "PlayerNameLabelBackdropとEnemyNameLabelBackdropをプレハブから削除します",
                "削除する",
                "キャンセル"))
        {
            return;
        }

        int removed = RemoveNameLabelBackdropsFromPrefabSilent();
        if (removed > 0)
        {
            Debug.Log(
                $"[BattleStartOverlayPrefabUtility] NameLabelBackdropを削除しました count={removed}: {PrefabPath}");
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog(
                    "完了",
                    $"NameLabelBackdropを{removed}件削除しました\n必要なら Sync Battle Start Overlay To All Scenes を実行してください",
                    "OK");
            }
        }
        else
        {
            Debug.Log("[BattleStartOverlayPrefabUtility] 削除対象のNameLabelBackdropはありませんでした");
        }
    }

    /// <summary>
    /// Unity.exe -batchmode -quit -projectPath ... -executeMethod BattleStartOverlayPrefabUtility.RemoveNameLabelBackdropsFromPrefabSilent
    /// </summary>
    public static int RemoveNameLabelBackdropsFromPrefabSilent()
    {
        if (!File.Exists(PrefabPath))
        {
            Debug.LogError(
                $"[BattleStartOverlayPrefabUtility] プレハブが見つかりません: {PrefabPath}");
            return 0;
        }

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
        int removed = 0;
        try
        {
            removed += DestroyNamedChildRecursive(prefabRoot.transform, "PlayerNameLabelBackdrop");
            removed += DestroyNamedChildRecursive(prefabRoot.transform, "EnemyNameLabelBackdrop");
            if (removed > 0)
            {
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        return removed;
    }

    private static int DestroyNamedChildRecursive(Transform root, string objectName)
    {
        if (root == null || string.IsNullOrEmpty(objectName))
        {
            return 0;
        }

        int removed = 0;
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = transforms.Length - 1; i >= 0; i--)
        {
            Transform current = transforms[i];
            if (current == null || current.name != objectName)
            {
                continue;
            }

            Object.DestroyImmediate(current.gameObject);
            removed++;
        }

        return removed;
    }

    /// <summary>
    /// シーン上にプレハブインスタンスを確保する
    /// </summary>
    public static GameObject EnsurePrefabInstanceInScene(UnityScene scene)
    {
        BattleStartOverlayView overlayView = FindOverlayViewInScene(scene);
        if (overlayView == null)
        {
            Debug.LogWarning(
                $"[BattleStartOverlayPrefabUtility] BattleStartOverlayViewが見つかりません: {scene.path}");
            return null;
        }

        return EnsurePrefabInstanceUnderHost(overlayView.transform);
    }

    /// <summary>
    /// 指定ホスト配下にプレハブインスタンスを確保する
    /// </summary>
    public static GameObject EnsurePrefabInstanceUnderHost(Transform host)
    {
        if (host == null)
        {
            return null;
        }

        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefabAsset == null)
        {
            Debug.LogError(
                $"[BattleStartOverlayPrefabUtility] プレハブが見つかりません: {PrefabPath}");
            return null;
        }

        Transform existing = FindOverlayRootUnder(host);
        if (existing != null && IsPrefabInstanceOfOverlay(existing.gameObject))
        {
            return existing.gameObject;
        }

        if (existing != null)
        {
            Object.DestroyImmediate(existing.gameObject);
        }

        DestroySiblingOverlayDuplicates(host, keep: null);

        GameObject instance = PrefabUtility.InstantiatePrefab(prefabAsset, host) as GameObject;
        if (instance == null)
        {
            Debug.LogError(
                $"[BattleStartOverlayPrefabUtility] プレハブのインスタンス化に失敗しました: {PrefabPath}",
                host);
            return null;
        }

        instance.name = OverlayRootName;
        instance.transform.SetAsLastSibling();
        return instance;
    }

    /// <summary>
    /// BattleStartOverlayViewへSerializeField参照だけ配線する
    /// </summary>
    public static int WireOverlayReferencesInScene(UnityScene scene)
    {
        BattleStartOverlayView[] overlayViews = FindOverlayViewsInScene(scene);
        int wired = 0;
        for (int i = 0; i < overlayViews.Length; i++)
        {
            if (WireOverlayReferences(overlayViews[i]))
            {
                wired++;
            }
        }

        return wired;
    }

    /// <summary>
    /// 単一Viewへ参照配線する
    /// </summary>
    public static bool WireOverlayReferences(BattleStartOverlayView overlayView)
    {
        if (overlayView == null)
        {
            return false;
        }

        Transform overlayRoot = FindOverlayRootUnder(overlayView.transform);
        if (overlayRoot == null)
        {
            overlayRoot = FindOverlayRootInScene(overlayView.gameObject.scene);
        }

        if (overlayRoot == null)
        {
            Debug.LogWarning(
                "[BattleStartOverlayPrefabUtility] BattleStartOverlayルートが見つかりません",
                overlayView);
            return false;
        }

        Canvas overlayCanvas = overlayRoot.GetComponent<Canvas>();
        TMP_Text vsText = FindNamedText(overlayRoot, "VS");
        TMP_Text readyText = FindNamedText(overlayRoot, "Ready");
        TMP_Text fightText = FindNamedText(overlayRoot, "Fight");
        TMP_Text finishText = FindNamedText(overlayRoot, "Finish");
        Image finishFlashImage = FindNamedImage(overlayRoot, "FinishFlash");
        Button startButton = FindNamedButton(overlayRoot, "StartButton");
        Transform vsNamePlateRoot = overlayRoot.Find("VsNamePlates");
        TMP_Text playerNameText = FindNamedText(overlayRoot, "PlayerNameLabel")
            ?? (vsNamePlateRoot != null ? FindNamedText(vsNamePlateRoot, "PlayerNameLabel") : null);
        TMP_Text enemyNameText = FindNamedText(overlayRoot, "EnemyNameLabel")
            ?? (vsNamePlateRoot != null ? FindNamedText(vsNamePlateRoot, "EnemyNameLabel") : null);
        TMP_Text victoryTitleText = FindNamedText(overlayRoot, "VictoryTitle");
        TMP_Text victoryNameText = FindNamedText(overlayRoot, "VictoryName");

        SerializedObject serializedOverlay = new SerializedObject(overlayView);
        serializedOverlay.FindProperty("overlayCanvas").objectReferenceValue = overlayCanvas;
        serializedOverlay.FindProperty("vsText").objectReferenceValue = vsText;
        serializedOverlay.FindProperty("readyText").objectReferenceValue = readyText;
        serializedOverlay.FindProperty("fightText").objectReferenceValue = fightText;
        serializedOverlay.FindProperty("finishText").objectReferenceValue = finishText;
        serializedOverlay.FindProperty("finishFlashImage").objectReferenceValue = finishFlashImage;
        serializedOverlay.FindProperty("startButton").objectReferenceValue = startButton;
        serializedOverlay.FindProperty("vsNamePlateRoot").objectReferenceValue =
            vsNamePlateRoot != null ? vsNamePlateRoot.gameObject : null;
        serializedOverlay.FindProperty("playerNameText").objectReferenceValue = playerNameText;
        serializedOverlay.FindProperty("enemyNameText").objectReferenceValue = enemyNameText;
        serializedOverlay.FindProperty("victoryTitleText").objectReferenceValue = victoryTitleText;
        serializedOverlay.FindProperty("victoryNameText").objectReferenceValue = victoryNameText;
        serializedOverlay.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(overlayView);
        return true;
    }

    private static bool ExtractPrefabFromActiveScene()
    {
        BattleStartOverlayView overlayView = Object.FindFirstObjectByType<BattleStartOverlayView>(
            FindObjectsInactive.Include);
        if (overlayView == null)
        {
            Debug.LogError("[BattleStartOverlayPrefabUtility] BattleStartOverlayViewが見つかりません");
            return false;
        }

        Transform overlayRoot = FindOverlayRootUnder(overlayView.transform);
        if (overlayRoot == null)
        {
            overlayRoot = FindOverlayRootInScene(overlayView.gameObject.scene);
        }

        if (overlayRoot == null)
        {
            Debug.LogError("[BattleStartOverlayPrefabUtility] BattleStartOverlayルートが見つかりません");
            return false;
        }

        // 抽出前はシーン上の現行Hierarchyを整備する(既存プレハブ差し替えはしない)
        SceneUiPlacementMigrator.EnsureBattleStartOverlayReferencesInActiveScene(preferPrefabInstance: false);
        overlayRoot = FindOverlayRootUnder(overlayView.transform) ?? FindOverlayRootInScene(overlayView.gameObject.scene);
        if (overlayRoot == null)
        {
            Debug.LogError("[BattleStartOverlayPrefabUtility] 整備後もBattleStartOverlayが見つかりません");
            return false;
        }

        EnsurePrefabDirectory();
        GameObject clone = Object.Instantiate(overlayRoot.gameObject);
        clone.name = OverlayRootName;
        try
        {
            PrefabUtility.SaveAsPrefabAsset(clone, PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(clone);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return File.Exists(PrefabPath);
    }

    private static void EnsurePrefabDirectory()
    {
        if (AssetDatabase.IsValidFolder(PrefabDirectory))
        {
            return;
        }

        string parent = Path.GetDirectoryName(PrefabDirectory)?.Replace('\\', '/');
        string folderName = Path.GetFileName(PrefabDirectory);
        if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(folderName))
        {
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }

    private static bool IsPrefabInstanceOfOverlay(GameObject gameObject)
    {
        if (gameObject == null || !PrefabUtility.IsPartOfPrefabInstance(gameObject))
        {
            return false;
        }

        string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameObject);
        return prefabPath == PrefabPath;
    }

    private static Transform FindOverlayRootUnder(Transform host)
    {
        if (host == null)
        {
            return null;
        }

        Transform direct = host.Find(OverlayRootName);
        if (direct != null)
        {
            return direct;
        }

        Canvas[] canvases = host.GetComponentsInChildren<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas != null && canvas.gameObject.name == OverlayRootName)
            {
                return canvas.transform;
            }
        }

        return null;
    }

    private static Transform FindOverlayRootInScene(UnityScene scene)
    {
        if (!scene.IsValid())
        {
            return null;
        }

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform found = FindNamedDescendant(roots[i].transform, OverlayRootName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static Transform FindNamedDescendant(Transform root, string objectName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == objectName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindNamedDescendant(root.GetChild(i), objectName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static void DestroySiblingOverlayDuplicates(Transform host, GameObject keep)
    {
        if (host == null)
        {
            return;
        }

        List<Transform> toDestroy = new List<Transform>();
        for (int i = 0; i < host.childCount; i++)
        {
            Transform child = host.GetChild(i);
            if (child == null || child.name != OverlayRootName)
            {
                continue;
            }

            if (keep != null && child.gameObject == keep)
            {
                continue;
            }

            toDestroy.Add(child);
        }

        for (int i = 0; i < toDestroy.Count; i++)
        {
            Object.DestroyImmediate(toDestroy[i].gameObject);
        }
    }

    private static BattleStartOverlayView FindOverlayViewInScene(UnityScene scene)
    {
        BattleStartOverlayView[] views = FindOverlayViewsInScene(scene);
        return views.Length > 0 ? views[0] : null;
    }

    private static BattleStartOverlayView[] FindOverlayViewsInScene(UnityScene scene)
    {
        BattleStartOverlayView[] all = Object.FindObjectsByType<BattleStartOverlayView>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        var matched = new List<BattleStartOverlayView>();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].gameObject.scene == scene)
            {
                matched.Add(all[i]);
            }
        }

        return matched.ToArray();
    }

    private static TMP_Text FindNamedText(Transform root, string objectName)
    {
        Transform found = FindNamedDescendant(root, objectName);
        return found != null ? found.GetComponent<TMP_Text>() : null;
    }

    private static Image FindNamedImage(Transform root, string objectName)
    {
        Transform found = FindNamedDescendant(root, objectName);
        return found != null ? found.GetComponent<Image>() : null;
    }

    private static Button FindNamedButton(Transform root, string objectName)
    {
        Transform found = FindNamedDescendant(root, objectName);
        return found != null ? found.GetComponent<Button>() : null;
    }
}
#endif
