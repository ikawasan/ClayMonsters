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
        "Assets/Scenes/BattlePVP.unity",
        "Assets/Scenes/BattlePvpArena.unity",
        "Assets/Scenes/Training.unity",
    };

    private const int PlayerHudRootIndex = 0;
    private const int EnemyHudRootIndex = 1;
    private const string MoveButtonsObjectName = "MoveButtons";

    /// <summary>
    /// BattleNpcのPlayerUI配下MoveButtonsからプレハブを生成する
    /// 初回レイアウト取り込み用で調整済みプレハブは上書きされる
    /// </summary>
    [MenuItem("Tools/ClayMonsters/Create Battle Move Buttons Prefab From BattleNpc")]
    public static void CreatePrefabFromBattleNpc()
    {
        if (!Application.isBatchMode
            && !EditorUtility.DisplayDialog(
                "Battle Move Buttons Prefab",
                "BattleNpcのPlayerUI/MoveButtonsからプレハブを生成します\n"
                    + "既存のBattleMoveButtons.prefabは上書きされます\n\n"
                    + "レイアウト調整後は\n"
                    + "Sync All Battle Scenes Move Buttons From Prefab を実行してください",
                "生成する",
                "キャンセル"))
        {
            return;
        }

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

            Debug.Log(
                $"[BattleMoveButtonsPrefabUtility] プレハブを生成しました: {MoveButtonsPrefabPath}");
        }
        finally
        {
            if (!string.IsNullOrEmpty(previousScenePath)
                && previousScenePath != BattleNpcScenePath
                && File.Exists(previousScenePath))
            {
                EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
            }
        }
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
    [MenuItem("Tools/ClayMonsters/Apply Battle Move Buttons Prefab To Open Scene")]
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
    [MenuItem("Tools/ClayMonsters/Sync All Battle Scenes Move Buttons From Prefab")]
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
                "戦闘関連4シーンのMoveButtonsをプレハブから再配置します\n"
                    + "シーン上のMoveButtons差分は破棄されます\n\n"
                    + "対象:\n"
                    + "- BattleNpc\n"
                    + "- BattlePVP\n"
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
