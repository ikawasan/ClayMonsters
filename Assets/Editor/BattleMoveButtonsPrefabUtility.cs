#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UI.Battle.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityScene = UnityEngine.SceneManagement.Scene;

/// <summary>
/// 戦闘シーンの攻撃ボタンUIプレハブ生成とシーン配線
/// レイアウト調整はプレハブ編集で行い実行時はデータ反映のみ行う
/// </summary>
public static class BattleMoveButtonsPrefabUtility
{
    public const string MoveButtonsPrefabPath = "Assets/Resources/UI/BattleMoveButtons.prefab";

    private const string BattleNpcScenePath = "Assets/Scenes/BattleNpc.unity";

    private static readonly string[] BattleMoveButtonsScenePaths =
    {
        "Assets/Scenes/BattleNpc.unity",
        "Assets/Scenes/BattlePvpArena.unity",
        "Assets/Scenes/Training.unity",
    };

    private const int PlayerHudRootIndex = 0;
    private const int EnemyHudRootIndex = 1;
    private const string MoveButtonsObjectName = "MoveButtons";

    /// <summary>
    /// BattleNpcのPlayerUI配下MoveButtonsから見た目付きプレハブを生成する
    /// 初回レイアウト取り込み用で調整済みプレハブは上書きされる
    /// </summary>
    [MenuItem("Tools/ClayMonsters/Create Battle Move Buttons Prefab From BattleNpc")]
    public static void CreatePrefabFromBattleNpc()
    {
        if (!Application.isBatchMode
            && !EditorUtility.DisplayDialog(
                "Battle Move Buttons Prefab",
                "BattleNpcのPlayerUI/MoveButtonsからプレハブを生成します\n"
                    + "見た目を適用してからBattleMoveButtons.prefabへ保存します\n"
                    + "既存のBattleMoveButtons.prefabは上書きされます\n\n"
                    + "生成後は Sync Battle Move Buttons To All Scenes で各シーンへ反映できます",
                "生成する",
                "キャンセル"))
        {
            return;
        }

        CreatePrefabFromBattleNpcSilent();
    }

    /// <summary>
    /// Unity.exe -batchmode -quit -projectPath ... -executeMethod BattleMoveButtonsPrefabUtility.CreatePrefabFromBattleNpcSilent
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
            if (!ExtractMoveButtonsPrefabFromScene(battleNpcScene))
            {
                return;
            }

            WireBattleMoveButtonsReferencesInScene(battleNpcScene);
            EditorSceneManager.MarkSceneDirty(battleNpcScene);
            EditorSceneManager.SaveScene(battleNpcScene);
            Debug.Log(
                $"[BattleMoveButtonsPrefabUtility] プレハブを生成しました: {MoveButtonsPrefabPath}");
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
    /// 開いているシーンのMoveButtonsへ参照とスタイルだけを適用する
    /// RectTransformは変更しない
    /// </summary>
    [MenuItem("Tools/ClayMonsters/Wire Battle Move Buttons To Open Scene")]
    public static void WireOpenSceneBattleMoveButtons()
    {
        UnityScene scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogWarning("[BattleMoveButtonsPrefabUtility] 有効なシーンが開かれていません");
            return;
        }

        int wired = WireMoveButtonsInScene(scene);
        if (wired > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
        }

        Debug.Log(
            $"[BattleMoveButtonsPrefabUtility] 配線完了 moveButtons={wired}: {scene.path}");
    }

    /// <summary>
    /// プレハブへ参照だけを適用して全戦闘シーンへ同期する
    /// プレハブのRectTransformと見た目は変更しない
    /// </summary>
    [MenuItem("Tools/ClayMonsters/Wire Battle Move Buttons And Sync")]
    public static void WireBattleMoveButtonsPrefabAndSyncAllScenes()
    {
        if (!WireBattleMoveButtonsPrefabAsset())
        {
            return;
        }

        SyncAllBattleScenesMoveButtonsFromPrefabSilent();
        Debug.Log("[BattleMoveButtonsPrefabUtility] 参照配線と全シーン同期が完了しました");
    }

    /// <summary>
    /// BattleMoveButtons.prefabをPrefab Modeで開く
    /// </summary>
    [MenuItem("Tools/ClayMonsters/Open Battle Move Buttons Prefab For Editing")]
    public static void OpenBattleMoveButtonsPrefabForEditing()
    {
        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(MoveButtonsPrefabPath);
        if (prefabAsset == null)
        {
            Debug.LogError(
                $"[BattleMoveButtonsPrefabUtility] プレハブが見つかりません: {MoveButtonsPrefabPath}");
            return;
        }

        AssetDatabase.OpenAsset(prefabAsset);
        Selection.activeObject = prefabAsset;
    }

    /// <summary>
    /// 開いているシーンのMoveButtonsレイアウトを初回用に組み立て直す
    /// </summary>
    [MenuItem("Tools/ClayMonsters/Rebuild Battle Move Buttons Layout To Open Scene")]
    public static void RebuildLayoutInOpenScene()
    {
        if (!Application.isBatchMode
            && !EditorUtility.DisplayDialog(
                "Rebuild Battle Move Buttons Layout",
                "開いているシーンのMoveButtonsレイアウトを組み立て直します\n"
                    + "RectTransformの手動調整は上書きされます",
                "実行する",
                "キャンセル"))
        {
            return;
        }

        UnityScene scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogWarning("[BattleMoveButtonsPrefabUtility] 有効なシーンが開かれていません");
            return;
        }

        int rebuilt = RebuildMoveButtonsInScene(scene);
        if (rebuilt > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
        }

        Debug.Log(
            $"[BattleMoveButtonsPrefabUtility] レイアウト再構築完了 moveButtons={rebuilt}: {scene.path}");
    }

    /// <summary>
    /// プレハブのレイアウトを組み立て直して全戦闘シーンへ同期する
    /// </summary>
    [MenuItem("Tools/ClayMonsters/Rebuild Battle Move Buttons Layout And Sync")]
    public static void RebuildLayoutInPrefabAndSyncAllScenes()
    {
        if (!Application.isBatchMode
            && !EditorUtility.DisplayDialog(
                "Rebuild Battle Move Buttons Layout And Sync",
                "BattleMoveButtons.prefabのレイアウトを組み立て直し\n"
                    + "戦闘関連3シーンへ同期します\n"
                    + "プレハブと各シーンのRectTransform差分は破棄されます",
                "実行する",
                "キャンセル"))
        {
            return;
        }

        if (!RebuildLayoutInPrefabAsset())
        {
            return;
        }

        SyncAllBattleScenesMoveButtonsFromPrefabSilent();
        Debug.Log("[BattleMoveButtonsPrefabUtility] レイアウト再構築と全シーン同期が完了しました");
    }

    /// <summary>
    /// Unity.exe -batchmode -quit -projectPath ... -executeMethod BattleMoveButtonsPrefabUtility.RebuildLayoutInPrefabAndSyncAllScenesSilent
    /// </summary>
    public static void RebuildLayoutInPrefabAndSyncAllScenesSilent()
    {
        if (!RebuildLayoutInPrefabAsset())
        {
            EditorApplication.Exit(1);
            return;
        }

        SyncAllBattleScenesMoveButtonsFromPrefabSilent();
        Debug.Log("[BattleMoveButtonsPrefabUtility] レイアウト再構築と全シーン同期が完了しました");
    }

    /// <summary>
    /// 開いているシーンのMoveButtonsへスタイルだけを適用する
    /// RectTransformは変更しない
    /// </summary>
    [MenuItem("Tools/ClayMonsters/Apply Battle Move Buttons Visuals To Open Scene")]
    public static void ApplyVisualsToOpenScene()
    {
        UnityScene scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogWarning("[BattleMoveButtonsPrefabUtility] 有効なシーンが開かれていません");
            return;
        }

        int styled = ApplyMoveButtonsStylesInScene(scene);
        if (styled > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
        }

        Debug.Log(
            $"[BattleMoveButtonsPrefabUtility] スタイル適用完了 moveButtons={styled}: {scene.path}");
    }

    /// <summary>
    /// プレハブへスタイルだけを適用して全戦闘シーンへ同期する
    /// RectTransformは変更しない
    /// </summary>
    [MenuItem("Tools/ClayMonsters/Apply Battle Move Buttons Visuals And Sync")]
    public static void ApplyVisualsToPrefabAndSyncAllScenes()
    {
        if (!ApplyStylesToPrefabAsset())
        {
            return;
        }

        SyncAllBattleScenesMoveButtonsFromPrefabSilent();
        Debug.Log("[BattleMoveButtonsPrefabUtility] スタイル適用と全シーン同期が完了しました");
    }

    /// <summary>
    /// BattleMoveButtons.prefabへ参照だけを適用する
    /// </summary>
    public static bool WireBattleMoveButtonsPrefabAsset()
    {
        if (!File.Exists(MoveButtonsPrefabPath))
        {
            Debug.LogError(
                $"[BattleMoveButtonsPrefabUtility] プレハブが見つかりません: {MoveButtonsPrefabPath}");
            return false;
        }

        GameObject prefabContents = PrefabUtility.LoadPrefabContents(MoveButtonsPrefabPath);
        try
        {
            BattleMoveButtonVisualUtility.WireMoveButtonsRootReferences(prefabContents.transform);
            PrefabUtility.SaveAsPrefabAsset(prefabContents, MoveButtonsPrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log(
                $"[BattleMoveButtonsPrefabUtility] プレハブへ参照配線を適用しました: {MoveButtonsPrefabPath}");
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabContents);
        }
    }

    /// <summary>
    /// BattleMoveButtons.prefabへスタイルだけを適用する
    /// </summary>
    public static bool ApplyStylesToPrefabAsset()
    {
        if (!File.Exists(MoveButtonsPrefabPath))
        {
            Debug.LogError(
                $"[BattleMoveButtonsPrefabUtility] プレハブが見つかりません: {MoveButtonsPrefabPath}");
            return false;
        }

        GameObject prefabContents = PrefabUtility.LoadPrefabContents(MoveButtonsPrefabPath);
        try
        {
            BattleMoveButtonVisualUtility.ApplyMoveButtonsStylesRoot(prefabContents.transform);
            PrefabUtility.SaveAsPrefabAsset(prefabContents, MoveButtonsPrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log(
                $"[BattleMoveButtonsPrefabUtility] プレハブへスタイルを適用しました: {MoveButtonsPrefabPath}");
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabContents);
        }
    }

    /// <summary>
    /// BattleMoveButtons.prefabのレイアウトを組み立て直す
    /// </summary>
    public static bool RebuildLayoutInPrefabAsset()
    {
        if (!File.Exists(MoveButtonsPrefabPath))
        {
            Debug.LogError(
                $"[BattleMoveButtonsPrefabUtility] プレハブが見つかりません: {MoveButtonsPrefabPath}");
            return false;
        }

        GameObject prefabContents = PrefabUtility.LoadPrefabContents(MoveButtonsPrefabPath);
        try
        {
            BattleMoveButtonVisualUtility.RebuildMoveButtonsRoot(prefabContents.transform);
            PrefabUtility.SaveAsPrefabAsset(prefabContents, MoveButtonsPrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log(
                $"[BattleMoveButtonsPrefabUtility] プレハブのレイアウトを再構築しました: {MoveButtonsPrefabPath}");
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabContents);
        }
    }

    /// <summary>
    /// BattleMoveButtons.prefabのHierarchy重複を検査する
    /// </summary>
    [MenuItem("Tools/ClayMonsters/Validate Battle Move Buttons Hierarchy")]
    public static void ValidateBattleMoveButtonsHierarchyMenu()
    {
        if (!File.Exists(MoveButtonsPrefabPath))
        {
            Debug.LogError(
                $"[BattleMoveButtonsPrefabUtility] プレハブが見つかりません: {MoveButtonsPrefabPath}");
            return;
        }

        GameObject prefabContents = PrefabUtility.LoadPrefabContents(MoveButtonsPrefabPath);
        try
        {
            bool ok = BattleMoveButtonVisualUtility.TryValidateMoveButtonsRoot(
                prefabContents.transform,
                out string report);
            if (ok)
            {
                Debug.Log($"[BattleMoveButtonsPrefabUtility] {report}");
            }
            else
            {
                Debug.LogWarning($"[BattleMoveButtonsPrefabUtility]\n{report}");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabContents);
        }
    }

    /// <summary>
    /// プレハブ生成後に戦闘関連3シーンへ同期する
    /// </summary>
    [MenuItem("Tools/ClayMonsters/Sync Battle Move Buttons To All Scenes")]
    public static void SyncAllBattleScenesMoveButtonsFromPrefabMenu()
    {
        SyncAllBattleScenesMoveButtonsFromPrefab();
    }

    /// <summary>
    /// 見た目適用→プレハブ生成→全シーン同期を一括実行する
    /// </summary>
    [MenuItem("Tools/ClayMonsters/Create And Sync Battle Move Buttons Prefab")]
    public static void CreateAndSyncBattleMoveButtonsPrefab()
    {
        if (!Application.isBatchMode
            && !EditorUtility.DisplayDialog(
                "Create And Sync Battle Move Buttons",
                "BattleNpcから見た目付きプレハブを生成し\n"
                    + "戦闘関連3シーンへ同期します\n\n"
                    + "各シーンのMoveButtons差分は破棄されます",
                "実行する",
                "キャンセル"))
        {
            return;
        }

        CreateAndSyncBattleMoveButtonsPrefabSilent();
    }

    /// <summary>
    /// Unity.exe -batchmode -quit -projectPath ... -executeMethod BattleMoveButtonsPrefabUtility.CreateAndSyncBattleMoveButtonsPrefabSilent
    /// </summary>
    public static void CreateAndSyncBattleMoveButtonsPrefabSilent()
    {
        CreatePrefabFromBattleNpcSilent();
        SyncAllBattleScenesMoveButtonsFromPrefabSilent();
    }

    /// <summary>
    /// 開いているシーンのBattleView攻撃ボタン参照だけを配線する
    /// </summary>
    [MenuItem("Tools/ClayMonsters/Wire Battle Move Buttons References")]
    public static void WireOpenSceneBattleMoveButtonsReferences()
    {
        UnityScene scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogWarning("[BattleMoveButtonsPrefabUtility] 有効なシーンが開かれていません");
            return;
        }

        int wiredViews = WireBattleMoveButtonsReferencesInScene(scene);
        if (wiredViews > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
        }

        Debug.Log(
            $"[BattleMoveButtonsPrefabUtility] 参照配線完了 battleView={wiredViews}: {scene.path}");
    }

    /// <summary>
    /// 開いているシーンのMoveButtonsをプレハブインスタンスへ置き換える
    /// 初回移行時はシーン上のRectTransformを維持する
    /// </summary>
    public static void ApplyPrefabToOpenScene()
    {
        GameObject prefabAsset = LoadMoveButtonsPrefabAsset();
        if (prefabAsset == null)
        {
            return;
        }

        UnityScene scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogWarning("[BattleMoveButtonsPrefabUtility] 有効なシーンが開かれていません");
            return;
        }

        int replaced = ReplaceMoveButtonsInScene(scene, prefabAsset, preserveSceneRect: true);
        if (replaced > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
        }

        Debug.Log(
            $"[BattleMoveButtonsPrefabUtility] 適用完了 moveButtons={replaced}: {scene.path}");
    }

    /// <summary>
    /// 戦闘関連シーンのMoveButtonsをプレハブから再配置する
    /// プレハブ側のレイアウトを各シーンへ反映する
    /// </summary>
    public static void SyncAllBattleScenesMoveButtonsFromPrefab()
    {
        GameObject prefabAsset = LoadMoveButtonsPrefabAsset();
        if (prefabAsset == null)
        {
            return;
        }

        if (!Application.isBatchMode
            && !EditorUtility.DisplayDialog(
                "Sync Battle Move Buttons",
                "戦闘関連3シーンのMoveButtonsをプレハブから再配置します\n"
                    + "シーン上のMoveButtons差分は破棄されます\n\n"
                    + "対象:\n"
                    + "- BattleNpc\n"
                    + "- BattlePvpArena\n"
                    + "- Training",
                "同期する",
                "キャンセル"))
        {
            return;
        }

        SyncAllBattleScenesMoveButtonsFromPrefabSilent();
    }

    /// <summary>
    /// Unity.exe -batchmode -quit -projectPath ... -executeMethod BattleMoveButtonsPrefabUtility.SyncAllBattleScenesMoveButtonsFromPrefabSilent
    /// </summary>
    public static void SyncAllBattleScenesMoveButtonsFromPrefabSilent()
    {
        GameObject prefabAsset = LoadMoveButtonsPrefabAsset();
        if (prefabAsset == null)
        {
            return;
        }

        for (int i = 0; i < BattleMoveButtonsScenePaths.Length; i++)
        {
            string scenePath = BattleMoveButtonsScenePaths[i];
            if (!File.Exists(scenePath))
            {
                Debug.LogWarning(
                    $"[BattleMoveButtonsPrefabUtility] シーンが見つかりません: {scenePath}");
                continue;
            }

            try
            {
                UnityScene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                int replaced = ReplaceMoveButtonsInScene(scene, prefabAsset, preserveSceneRect: false);
                WireBattleMoveButtonsReferencesInScene(scene);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log(
                    $"[BattleMoveButtonsPrefabUtility] 同期保存完了"
                        + $" moveButtons={replaced}"
                        + $": {scenePath}");
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[BattleMoveButtonsPrefabUtility] 同期失敗: {scenePath}\n{exception}");
            }
        }
    }

    private static GameObject LoadMoveButtonsPrefabAsset()
    {
        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(MoveButtonsPrefabPath);
        if (prefabAsset != null)
        {
            return prefabAsset;
        }

        Debug.LogError(
            $"[BattleMoveButtonsPrefabUtility] プレハブが見つかりません: {MoveButtonsPrefabPath}\n"
                + "先に Create Battle Move Buttons Prefab From BattleNpc を実行してください");
        return null;
    }

    private static bool ExtractMoveButtonsPrefabFromScene(UnityScene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("[BattleMoveButtonsPrefabUtility] シーンが読み込まれていません");
            return false;
        }

        Transform source = FindMoveButtonsUnderHudRoot(scene, PlayerHudRootIndex);
        if (source == null)
        {
            Debug.LogError(
                "[BattleMoveButtonsPrefabUtility] PlayerUI/MoveButtons が見つかりません");
            return false;
        }

        EnsurePrefabDirectory();

        GameObject clone = UnityEngine.Object.Instantiate(source.gameObject);
        clone.name = MoveButtonsObjectName;
        BattleMoveButtonVisualUtility.RebuildMoveButtonsRoot(clone.transform);
        try
        {
            PrefabUtility.SaveAsPrefabAsset(clone, MoveButtonsPrefabPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return true;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(clone);
        }
    }

    private static int ReplaceMoveButtonsInScene(
        UnityScene scene,
        GameObject prefabAsset,
        bool preserveSceneRect)
    {
        if (!scene.IsValid() || !scene.isLoaded || prefabAsset == null)
        {
            return 0;
        }

        List<ReplaceTarget> targets = CollectReplaceTargets(scene);
        int replacedCount = 0;
        for (int i = 0; i < targets.Count; i++)
        {
            ReplaceTarget target = targets[i];
            if (target.Parent == null || target.InstanceRoot == null)
            {
                continue;
            }

            RectTransform sourceRect = preserveSceneRect
                ? target.InstanceRoot.GetComponent<RectTransform>()
                : null;

            UnityEngine.Object.DestroyImmediate(target.InstanceRoot);
            GameObject instance = PrefabUtility.InstantiatePrefab(prefabAsset, target.Parent) as GameObject;
            if (instance == null)
            {
                Debug.LogError(
                    "[BattleMoveButtonsPrefabUtility] MoveButtonsの再配置に失敗しました",
                    target.Parent);
                continue;
            }

            instance.name = MoveButtonsObjectName;
            instance.transform.SetSiblingIndex(target.SiblingIndex);
            if (sourceRect != null)
            {
                CopyRectTransform(sourceRect, instance.GetComponent<RectTransform>());
            }

            EditorUtility.SetDirty(instance);
            replacedCount++;
        }

        WireBattleMoveButtonsReferencesInScene(scene);
        return replacedCount;
    }

    private static List<ReplaceTarget> CollectReplaceTargets(UnityScene scene)
    {
        List<ReplaceTarget> targets = new List<ReplaceTarget>();
        BattleView[] battleViews = UnityEngine.Object.FindObjectsByType<BattleView>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < battleViews.Length; i++)
        {
            BattleView battleView = battleViews[i];
            if (battleView == null || battleView.gameObject.scene != scene)
            {
                continue;
            }

            AddReplaceTargetIfFound(targets, FindMoveButtonsUnderHudRoot(battleView, PlayerHudRootIndex));
            AddReplaceTargetIfFound(targets, FindMoveButtonsUnderHudRoot(battleView, EnemyHudRootIndex));
        }

        return targets;
    }

    private static void AddReplaceTargetIfFound(List<ReplaceTarget> targets, Transform moveButtons)
    {
        if (moveButtons == null)
        {
            return;
        }

        GameObject instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(moveButtons.gameObject);
        if (instanceRoot == null)
        {
            instanceRoot = moveButtons.gameObject;
        }

        string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instanceRoot);
        if (!string.IsNullOrEmpty(prefabPath) && prefabPath != MoveButtonsPrefabPath)
        {
            instanceRoot = moveButtons.gameObject;
        }

        targets.Add(new ReplaceTarget
        {
            Parent = instanceRoot.transform.parent,
            SiblingIndex = instanceRoot.transform.GetSiblingIndex(),
            InstanceRoot = instanceRoot,
        });
    }

    private static int WireBattleMoveButtonsReferencesInScene(UnityScene scene)
    {
        int wiredViews = 0;
        BattleView[] battleViews = UnityEngine.Object.FindObjectsByType<BattleView>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < battleViews.Length; i++)
        {
            BattleView battleView = battleViews[i];
            if (battleView == null || battleView.gameObject.scene != scene)
            {
                continue;
            }

            WireBattleViewMoveButtons(battleView);
            wiredViews++;
        }

        return wiredViews;
    }

    /// <summary>
    /// BattleViewのmoveButtonsとenemyMoveButtonsをHierarchyから配線する
    /// RectTransformやHierarchyは変更しない
    /// </summary>
    public static void WireBattleViewMoveButtons(BattleView battleView)
    {
        if (battleView == null)
        {
            return;
        }

        MoveButtonView[] playerButtons = CollectMoveButtonsUnderHudRoot(battleView, PlayerHudRootIndex);
        MoveButtonView[] enemyButtons = CollectMoveButtonsUnderHudRoot(battleView, EnemyHudRootIndex);

        SerializedObject serializedBattleView = new SerializedObject(battleView);
        SerializedProperty moveButtonsProperty = serializedBattleView.FindProperty("moveButtons");
        SerializedProperty enemyMoveButtonsProperty = serializedBattleView.FindProperty("enemyMoveButtons");
        AssignMoveButtonArray(moveButtonsProperty, playerButtons);
        AssignMoveButtonArray(enemyMoveButtonsProperty, enemyButtons);
        serializedBattleView.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(battleView);
    }

    private static void AssignMoveButtonArray(SerializedProperty arrayProperty, MoveButtonView[] buttons)
    {
        if (arrayProperty == null)
        {
            return;
        }

        arrayProperty.arraySize = buttons != null ? buttons.Length : 0;
        if (buttons == null)
        {
            return;
        }

        for (int i = 0; i < buttons.Length; i++)
        {
            arrayProperty.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
        }
    }

    private static MoveButtonView[] CollectMoveButtonsUnderHudRoot(BattleView battleView, int hudRootIndex)
    {
        Transform moveButtons = FindMoveButtonsUnderHudRoot(battleView, hudRootIndex);
        if (moveButtons == null)
        {
            return Array.Empty<MoveButtonView>();
        }

        List<MoveButtonView> buttons = new List<MoveButtonView>(moveButtons.childCount);
        for (int i = 0; i < moveButtons.childCount; i++)
        {
            MoveButtonView moveButton = moveButtons.GetChild(i).GetComponent<MoveButtonView>();
            if (moveButton != null)
            {
                buttons.Add(moveButton);
            }
        }

        return buttons.ToArray();
    }

    private static Transform FindMoveButtonsUnderHudRoot(UnityScene scene, int hudRootIndex)
    {
        BattleView battleView = UnityEngine.Object.FindFirstObjectByType<BattleView>();
        return battleView != null ? FindMoveButtonsUnderHudRoot(battleView, hudRootIndex) : null;
    }

    private static Transform FindMoveButtonsUnderHudRoot(BattleView battleView, int hudRootIndex)
    {
        if (battleView == null)
        {
            return null;
        }

        SerializedObject serializedBattleView = new SerializedObject(battleView);
        SerializedProperty hudRootsProperty = serializedBattleView.FindProperty("battleHudRoots");
        if (hudRootsProperty == null || hudRootIndex < 0 || hudRootIndex >= hudRootsProperty.arraySize)
        {
            return null;
        }

        GameObject hudRoot = hudRootsProperty.GetArrayElementAtIndex(hudRootIndex).objectReferenceValue as GameObject;
        return hudRoot != null ? hudRoot.transform.Find(MoveButtonsObjectName) : null;
    }

    private static int WireMoveButtonsInScene(UnityScene scene)
    {
        return ProcessMoveButtonsInScene(scene, BattleMoveButtonVisualUtility.WireMoveButtonsRootReferences);
    }

    private static int ApplyMoveButtonsStylesInScene(UnityScene scene)
    {
        return ProcessMoveButtonsInScene(scene, BattleMoveButtonVisualUtility.ApplyMoveButtonsStylesRoot);
    }

    private static int RebuildMoveButtonsInScene(UnityScene scene)
    {
        return ProcessMoveButtonsInScene(scene, BattleMoveButtonVisualUtility.RebuildMoveButtonsRoot);
    }

    private static int ProcessMoveButtonsInScene(UnityScene scene, System.Action<Transform> processRoot)
    {
        if (!scene.IsValid() || !scene.isLoaded || processRoot == null)
        {
            return 0;
        }

        int processedCount = 0;
        BattleView[] battleViews = UnityEngine.Object.FindObjectsByType<BattleView>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < battleViews.Length; i++)
        {
            BattleView battleView = battleViews[i];
            if (battleView == null || battleView.gameObject.scene != scene)
            {
                continue;
            }

            Transform playerMoveButtons = FindMoveButtonsUnderHudRoot(battleView, PlayerHudRootIndex);
            if (playerMoveButtons != null)
            {
                processRoot(playerMoveButtons);
                processedCount++;
            }

            Transform enemyMoveButtons = FindMoveButtonsUnderHudRoot(battleView, EnemyHudRootIndex);
            if (enemyMoveButtons != null)
            {
                processRoot(enemyMoveButtons);
                processedCount++;
            }
        }

        WireBattleMoveButtonsReferencesInScene(scene);
        return processedCount;
    }

    private static void EnsurePrefabDirectory()
    {
        string directory = Path.GetDirectoryName(MoveButtonsPrefabPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static void CopyRectTransform(RectTransform source, RectTransform destination)
    {
        if (source == null || destination == null)
        {
            return;
        }

        destination.anchorMin = source.anchorMin;
        destination.anchorMax = source.anchorMax;
        destination.pivot = source.pivot;
        destination.anchoredPosition = source.anchoredPosition;
        destination.sizeDelta = source.sizeDelta;
        destination.localRotation = source.localRotation;
        destination.localScale = source.localScale;
    }

    private struct ReplaceTarget
    {
        public Transform Parent;
        public int SiblingIndex;
        public GameObject InstanceRoot;
    }
}
#endif
