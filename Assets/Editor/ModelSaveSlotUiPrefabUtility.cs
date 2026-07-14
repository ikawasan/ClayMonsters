#if UNITY_EDITOR
using SaveData;
using Scene.TrainingScene.View;
using System.Collections.Generic;
using System.IO;
using UI.ClayEditor.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityScene = UnityEngine.SceneManagement.Scene;

/// <summary>
/// セーブスロット一覧と確認行UIのプレハブ生成とシーン配線
/// レイアウト調整はプレハブ編集で行い実行時はデータ反映のみ行う
/// </summary>
public static class ModelSaveSlotUiPrefabUtility
{
    public const string ScrollListPrefabPath = "Assets/Resources/UI/ModelSaveSlotScrollList.prefab";
    public const string ConfirmViewPrefabPath = "Assets/Resources/UI/ModelSaveConfirmView.prefab";

    private static readonly string[] SaveSlotUiScenePaths =
    {
        "Assets/Scenes/ClayEdit.unity",
        "Assets/Scenes/Training.unity",
        "Assets/Scenes/BattleNpc.unity",
        "Assets/Scenes/BattlePVP.unity",
        "Assets/Scenes/BattlePvpArena.unity",
    };

    private const string ScrollRootName = "SlotScrollList";
    private const string SlotFrameSpriteAssetPath = "Assets/Resources/Image/Training/TrainingUi_StaminaFill.png";

    /// <summary>
    /// セーブスロットUIプレハブを初期生成する
    /// 既存プレハブのレイアウトを上書きするため初回以外は使わない
    /// </summary>
    [MenuItem("Tools/ClayMonsters/Create Save Slot UI Prefabs")]
    public static void CreateOrUpdatePrefabs()
    {
        if (!Application.isBatchMode
            && !EditorUtility.DisplayDialog(
                "Save Slot UI Prefabs",
                "セーブスロットUIプレハブを初期レイアウトで再生成します\n"
                    + "調整済みプレハブは上書きされます\n\n"
                    + "参照配線だけ必要な場合はシーン上のViewを選択して\n"
                    + "Wire Save Slot UI References を使ってください",
                "再生成する",
                "キャンセル"))
        {
            return;
        }

        CreateOrUpdatePrefabsInternal();
    }

    /// <summary>
    /// プレハブアセットが無いときだけ生成する
    /// </summary>
    public static void EnsurePrefabAssetsExist()
    {
        if (File.Exists(ScrollListPrefabPath) && File.Exists(ConfirmViewPrefabPath))
        {
            return;
        }

        CreateOrUpdatePrefabsInternal();
    }

    /// <summary>
    /// 繧ｻ繝ｼ繝悶せ繝ｭ繝・ヨAttackSlot縺ｮSlotFrame縺ｸ豁｣縺励＞Sprite繧帝←逕ｨ縺吶ｋ
    /// </summary>
    public static void ApplySaveSlotAttackSlotFrameVisual(Image image)
    {
        TryRestoreSaveSlotAttackSlotFrame(image, ResolveSaveSlotSlotFrameReferenceSprite());
    }

    /// <summary>
    /// ModelSaveSlotScrollList繝励Ξ繝上ヶ縺ｮ繧､繝ｳ繧ｹ繧ｿ繝ｳ繧ｹ縺九←縺・°繧定ｿ斐☆
    /// </summary>
    public static bool IsScrollListPrefabInstance(GameObject gameObject)
    {
        if (gameObject == null)
        {
            return false;
        }

        string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameObject);
        return prefabPath == ScrollListPrefabPath;
    }

    /// <summary>
    /// SlotButtons配下へModelSaveSlotScrollListプレハブインスタンスを配置する
    /// 既にプレハブインスタンスの場合は参照配線のみ行う
    /// </summary>
    public static ModelSaveSlotScrollListView EnsureScrollListPrefabInstance(Transform parent)
    {
        if (parent == null)
        {
            return null;
        }

        EnsurePrefabAssetsExist();
        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ScrollListPrefabPath);
        if (prefabAsset == null)
        {
            Debug.LogError(
                $"[ModelSaveSlotUiPrefabUtility] プレハブが見つかりません: {ScrollListPrefabPath}");
            return null;
        }

        ModelSaveSlotScrollListView existingScrollList =
            parent.GetComponentInChildren<ModelSaveSlotScrollListView>(true);
        if (existingScrollList != null)
        {
            if (IsScrollListPrefabInstance(existingScrollList.gameObject))
            {
                WireScrollListReferencesOnly(existingScrollList);
                return existingScrollList;
            }

            SceneUiEditorSelectionUtility.DestroyImmediateSafe(existingScrollList.gameObject);
        }
        else
        {
            CleanupLegacySlotButtonChildren(parent);
        }

        return CreateScrollListPrefabInstance(parent, prefabAsset);
    }

    /// <summary>
    /// 譌ｧ蠑輯lotButtons繧偵・繝ｬ繝上ヶ繧､繝ｳ繧ｹ繧ｿ繝ｳ繧ｹ縺ｸ荳蠎ｦ縺縺醍ｧｻ陦後☆繧・
    /// </summary>
    public static ModelSaveSlotScrollListView ResyncScrollListHostFromPrefab(ModelSaveSlotScrollListView scrollList)
    {
        if (scrollList == null)
        {
            return null;
        }

        if (IsScrollListPrefabInstance(scrollList.gameObject))
        {
            GameObject instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(scrollList.gameObject);
            if (instanceRoot != null
                && PrefabUtility.HasPrefabInstanceAnyOverrides(instanceRoot, false))
            {
                PrefabUtility.RevertPrefabInstance(instanceRoot, InteractionMode.AutomatedAction);
            }

            WireScrollListReferencesOnly(scrollList);
            return scrollList;
        }

        return EnsureScrollListPrefabInstance(scrollList.transform.parent);
    }

    private static ModelSaveSlotScrollListView CreateScrollListPrefabInstance(
        Transform parent,
        GameObject prefabAsset)
    {
        GameObject instance = PrefabUtility.InstantiatePrefab(prefabAsset, parent) as GameObject;
        if (instance == null)
        {
            Debug.LogError(
                "[ModelSaveSlotUiPrefabUtility] ModelSaveSlotScrollListプレハブのインスタンス化に失敗しました",
                parent.gameObject);
            return null;
        }

        ModelSaveSlotScrollListView scrollList = instance.GetComponent<ModelSaveSlotScrollListView>();
        WireScrollListReferencesOnly(scrollList);
        EditorUtility.SetDirty(instance);
        return scrollList;
    }

    /// <summary>
    /// 遒ｺ隱阪ヱ繝阪Ν荳翫・ModelSaveConfirmView繧偵・繝ｬ繝上ヶ繧､繝ｳ繧ｹ繧ｿ繝ｳ繧ｹ縺ｸ蟾ｮ縺玲崛縺医ｋ
    /// </summary>
    public static ModelSaveConfirmView ResyncConfirmViewFromPrefab(Transform panelRoot)
    {
        if (panelRoot == null)
        {
            return null;
        }

        ModelSaveConfirmView existing = panelRoot.GetComponentInChildren<ModelSaveConfirmView>(true);
        if (existing != null)
        {
            SceneUiEditorSelectionUtility.DestroyImmediateSafe(existing.gameObject);
        }

        return EnsureConfirmViewOnRoot(panelRoot);
    }

    /// <summary>
    /// SlotButtons蟾ｮ縺玲崛縺亥ｾ後↓LoadSlotView遲峨・蜿ら・繧貞・驟咲ｷ壹☆繧・
    /// </summary>
    public static void RewireSaveSlotOwnerReferencesInOpenScene()
    {
        SaveSlotView[] saveSlotViews =
            Object.FindObjectsByType<SaveSlotView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < saveSlotViews.Length; i++)
        {
            RewireSaveSlotViewReferences(saveSlotViews[i]);
        }

        ClayEditRemakeLoadSlotView[] remakeLoadSlotViews =
            Object.FindObjectsByType<ClayEditRemakeLoadSlotView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < remakeLoadSlotViews.Length; i++)
        {
            RewireClayEditRemakeLoadSlotViewReferences(remakeLoadSlotViews[i]);
        }

        LoadSlotView[] loadSlotViews =
            Object.FindObjectsByType<LoadSlotView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < loadSlotViews.Length; i++)
        {
            RewireLoadSlotViewReferences(loadSlotViews[i]);
        }

        TrainingTrainedSaveView[] trainedSaveViews =
            Object.FindObjectsByType<TrainingTrainedSaveView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < trainedSaveViews.Length; i++)
        {
            RewireTrainedSaveViewReferences(trainedSaveViews[i]);
        }
    }

    /// <summary>
    /// 繧ｷ繝ｼ繝ｳ荳翫・SlotButtons繧偵・繝ｬ繝上ヶ繧､繝ｳ繧ｹ繧ｿ繝ｳ繧ｹ縺ｸ謠・∴繧・
    /// 譌｢蟄倥・繝ｬ繝上ヶ繧､繝ｳ繧ｹ繧ｿ繝ｳ繧ｹ縺ｯ邯ｭ謖√＠蜿ら・驟咲ｷ壹・縺ｿ陦後≧
    /// </summary>
    public static void EnsureScrollListHostMatchesPrefab(ModelSaveSlotScrollListView scrollList)
    {
        if (scrollList == null)
        {
            return;
        }

        if (IsScrollListPrefabInstance(scrollList.gameObject))
        {
            GameObject instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(scrollList.gameObject);
            if (instanceRoot != null
                && PrefabUtility.HasPrefabInstanceAnyOverrides(instanceRoot, false))
            {
                PrefabUtility.RevertPrefabInstance(instanceRoot, InteractionMode.AutomatedAction);
            }

            WireScrollListReferencesOnly(scrollList);
            return;
        }

        EnsureScrollListPrefabInstance(
            scrollList.transform.parent);
    }

    /// <summary>
    /// 指定シーン上のセーブスロット一覧をプレハブ状態へ同期する
    /// Override解除では残る差分があるため削除して再配置する
    /// </summary>
    /// <param name="scene">対象シーン</param>
    /// <returns>変更があった場合true</returns>
    public static bool SyncScrollListInstancesInScene(UnityScene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return false;
        }

        EnsurePrefabAssetsExist();
        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ScrollListPrefabPath);
        if (prefabAsset == null)
        {
            return false;
        }

        return ReplaceScrollListInstancesInScene(scene, prefabAsset) > 0;
    }

    /// <summary>
    /// 指定シーン上のSaveSlot系View参照を再配線する
    /// </summary>
    /// <param name="scene">対象シーン</param>
    public static void RewireSaveSlotOwnerReferencesInScene(UnityScene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        SaveSlotView[] saveSlotViews =
            Object.FindObjectsByType<SaveSlotView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < saveSlotViews.Length; i++)
        {
            SaveSlotView saveSlotView = saveSlotViews[i];
            if (saveSlotView != null && saveSlotView.gameObject.scene == scene)
            {
                RewireSaveSlotViewReferences(saveSlotView);
            }
        }

        ClayEditRemakeLoadSlotView[] remakeLoadSlotViews =
            Object.FindObjectsByType<ClayEditRemakeLoadSlotView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < remakeLoadSlotViews.Length; i++)
        {
            ClayEditRemakeLoadSlotView remakeLoadSlotView = remakeLoadSlotViews[i];
            if (remakeLoadSlotView != null && remakeLoadSlotView.gameObject.scene == scene)
            {
                RewireClayEditRemakeLoadSlotViewReferences(remakeLoadSlotView);
            }
        }

        LoadSlotView[] loadSlotViews =
            Object.FindObjectsByType<LoadSlotView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < loadSlotViews.Length; i++)
        {
            LoadSlotView loadSlotView = loadSlotViews[i];
            if (loadSlotView != null && loadSlotView.gameObject.scene == scene)
            {
                RewireLoadSlotViewReferences(loadSlotView);
            }
        }

        TrainingTrainedSaveView[] trainedSaveViews =
            Object.FindObjectsByType<TrainingTrainedSaveView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < trainedSaveViews.Length; i++)
        {
            TrainingTrainedSaveView trainedSaveView = trainedSaveViews[i];
            if (trainedSaveView != null && trainedSaveView.gameObject.scene == scene)
            {
                RewireTrainedSaveViewReferences(trainedSaveView);
            }
        }
    }

    /// <summary>
    /// 髢九＞縺ｦ縺・ｋ繧ｷ繝ｼ繝ｳ荳翫・譌ｧ蠑輯lotButtons繧偵・繝ｬ繝上ヶ繧､繝ｳ繧ｹ繧ｿ繝ｳ繧ｹ縺ｸ遘ｻ陦後☆繧・
    /// </summary>
    public static void MigrateLooseScrollListHostsInOpenScene()
    {
        ModelSaveSlotScrollListView[] scrollLists =
            Object.FindObjectsByType<ModelSaveSlotScrollListView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < scrollLists.Length; i++)
        {
            ModelSaveSlotScrollListView scrollList = scrollLists[i];
            if (scrollList == null)
            {
                continue;
            }

            EnsureScrollListHostMatchesPrefab(scrollList);
        }
    }

    /// <summary>
    /// 開いているシーンのセーブスロットUIをプレハブから再配置する
    /// </summary>
    public static void SyncOpenSceneSaveSlotUiFromPrefabs()
    {
        EnsurePrefabAssetsExist();
        GameObject scrollListPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ScrollListPrefabPath);
        if (scrollListPrefab == null)
        {
            Debug.LogError(
                $"[ModelSaveSlotUiPrefabUtility] プレハブが見つかりません: {ScrollListPrefabPath}");
            return;
        }

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            UnityScene scene = SceneManager.GetSceneAt(i);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                continue;
            }

            ReplaceScrollListInstancesInScene(scene, scrollListPrefab);
            TryRewireSaveSlotOwnerReferencesInScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        SceneUiEditorSavePolicy.MarkActiveDirty();
    }

    /// <summary>
    /// 全対象シーンのセーブスロットUIをプレハブへ同期して保存する
    /// 実行時に古いOverrideが残る場合はこれを1回実行する
    /// </summary>
    [MenuItem("Tools/ClayMonsters/Sync All Scenes Save Slot UI From Prefabs")]
    public static void SyncAllScenesSaveSlotUiFromPrefabs()
    {
        if (!Application.isBatchMode
            && !EditorUtility.DisplayDialog(
                "Sync All Scenes Save Slot UI",
                "対象シーンのModelSaveSlotScrollListを削除し\n"
                    + "最新プレハブから再配置して保存します\n"
                    + "シーン上のOverrideは破棄されます\n\n"
                    + string.Join("\n", SaveSlotUiScenePaths),
                "再配置して保存",
                "キャンセル"))
        {
            return;
        }

        SyncAllScenesSaveSlotUiFromPrefabsInternal();
        if (!Application.isBatchMode)
        {
            EditorUtility.DisplayDialog(
                "Sync All Scenes Save Slot UI",
                "全対象シーンの再配置と保存が完了しました\n"
                    + "Consoleの scrollList=件数 を確認してください",
                "OK");
        }
    }

    /// <summary>
    /// バッチモード用全シーンセーブスロットUI同期
    /// Unity.exe -batchmode -quit -projectPath ... -executeMethod ModelSaveSlotUiPrefabUtility.SyncAllScenesSaveSlotUiFromPrefabsSilent
    /// </summary>
    public static void SyncAllScenesSaveSlotUiFromPrefabsSilent()
    {
        SyncAllScenesSaveSlotUiFromPrefabsInternal();
        AssetDatabase.SaveAssets();
    }

    private static void SyncAllScenesSaveSlotUiFromPrefabsInternal()
    {
        EnsurePrefabAssetsExist();
        GameObject scrollListPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ScrollListPrefabPath);
        if (scrollListPrefab == null)
        {
            Debug.LogError(
                $"[ModelSaveSlotUiPrefabUtility] プレハブが見つかりません: {ScrollListPrefabPath}");
            return;
        }

        for (int i = 0; i < SaveSlotUiScenePaths.Length; i++)
        {
            string scenePath = SaveSlotUiScenePaths[i];
            if (!File.Exists(scenePath))
            {
                Debug.LogWarning(
                    $"[ModelSaveSlotUiPrefabUtility] シーンが見つかりません: {scenePath}");
                continue;
            }

            try
            {
                UnityScene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                int replacedScrollLists = ReplaceScrollListInstancesInScene(scene, scrollListPrefab);
                TryRewireSaveSlotOwnerReferencesInScene(scene);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log(
                    $"[ModelSaveSlotUiPrefabUtility] 再配置保存完了"
                        + $" scrollList={replacedScrollLists}"
                        + $": {scenePath}");
            }
            catch (System.Exception exception)
            {
                Debug.LogError(
                    $"[ModelSaveSlotUiPrefabUtility] 同期失敗: {scenePath}\n{exception}");
            }
        }
    }

    /// <summary>
    /// 指定シーン上のModelSaveSlotScrollListを削除してプレハブから再配置する
    /// Override解除では残る差分を確実に捨てる
    /// </summary>
    /// <param name="scene">対象シーン</param>
    /// <param name="prefabAsset">配置するプレハブ</param>
    /// <returns>再配置した数</returns>
    private static int ReplaceScrollListInstancesInScene(UnityScene scene, GameObject prefabAsset)
    {
        if (!scene.IsValid() || !scene.isLoaded || prefabAsset == null)
        {
            return 0;
        }

        ModelSaveSlotScrollListView[] scrollLists =
            Object.FindObjectsByType<ModelSaveSlotScrollListView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        System.Collections.Generic.List<ReplaceTarget> targets =
            new System.Collections.Generic.List<ReplaceTarget>();

        for (int i = 0; i < scrollLists.Length; i++)
        {
            ModelSaveSlotScrollListView scrollList = scrollLists[i];
            if (scrollList == null || scrollList.gameObject.scene != scene)
            {
                continue;
            }

            GameObject instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(scrollList.gameObject);
            if (instanceRoot == null)
            {
                instanceRoot = scrollList.gameObject;
            }

            string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instanceRoot);
            if (!string.IsNullOrEmpty(prefabPath) && prefabPath != ScrollListPrefabPath)
            {
                instanceRoot = scrollList.gameObject;
            }

            targets.Add(new ReplaceTarget
            {
                Parent = instanceRoot.transform.parent,
                SiblingIndex = instanceRoot.transform.GetSiblingIndex(),
                InstanceRoot = instanceRoot,
            });
        }

        int replacedCount = 0;
        for (int i = 0; i < targets.Count; i++)
        {
            ReplaceTarget target = targets[i];
            if (target.Parent == null || target.InstanceRoot == null)
            {
                continue;
            }

            Object.DestroyImmediate(target.InstanceRoot);
            GameObject instance = PrefabUtility.InstantiatePrefab(prefabAsset, target.Parent) as GameObject;
            if (instance == null)
            {
                Debug.LogError(
                    "[ModelSaveSlotUiPrefabUtility] ModelSaveSlotScrollListの再配置に失敗しました",
                    target.Parent);
                continue;
            }

            instance.transform.SetSiblingIndex(target.SiblingIndex);
            ModelSaveSlotScrollListView scrollList = instance.GetComponent<ModelSaveSlotScrollListView>();
            WireScrollListReferencesOnly(scrollList);
            EditorUtility.SetDirty(instance);
            replacedCount++;
        }

        return replacedCount;
    }

    /// <summary>
    /// 指定シーン上のModelSaveConfirmViewを削除してプレハブから再配置する
    /// </summary>
    /// <param name="scene">対象シーン</param>
    /// <param name="prefabAsset">配置するプレハブ</param>
    /// <returns>再配置した数</returns>
    private static int ReplaceConfirmViewInstancesInScene(UnityScene scene, GameObject prefabAsset)
    {
        if (!scene.IsValid() || !scene.isLoaded || prefabAsset == null)
        {
            return 0;
        }

        ModelSaveConfirmView[] confirmViews =
            Object.FindObjectsByType<ModelSaveConfirmView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        System.Collections.Generic.List<ReplaceTarget> targets =
            new System.Collections.Generic.List<ReplaceTarget>();

        for (int i = 0; i < confirmViews.Length; i++)
        {
            ModelSaveConfirmView confirmView = confirmViews[i];
            if (confirmView == null || confirmView.gameObject.scene != scene)
            {
                continue;
            }

            GameObject instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(confirmView.gameObject);
            if (instanceRoot == null)
            {
                instanceRoot = confirmView.gameObject;
            }

            string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instanceRoot);
            if (!string.IsNullOrEmpty(prefabPath) && prefabPath != ConfirmViewPrefabPath)
            {
                instanceRoot = confirmView.gameObject;
            }

            targets.Add(new ReplaceTarget
            {
                Parent = instanceRoot.transform.parent,
                SiblingIndex = instanceRoot.transform.GetSiblingIndex(),
                InstanceRoot = instanceRoot,
            });
        }

        int replacedCount = 0;
        for (int i = 0; i < targets.Count; i++)
        {
            ReplaceTarget target = targets[i];
            if (target.Parent == null || target.InstanceRoot == null)
            {
                continue;
            }

            Object.DestroyImmediate(target.InstanceRoot);
            GameObject instance = PrefabUtility.InstantiatePrefab(prefabAsset, target.Parent) as GameObject;
            if (instance == null)
            {
                Debug.LogError(
                    "[ModelSaveSlotUiPrefabUtility] ModelSaveConfirmViewの再配置に失敗しました",
                    target.Parent);
                continue;
            }

            instance.transform.SetSiblingIndex(target.SiblingIndex);
            ModelSaveConfirmView confirmView = instance.GetComponent<ModelSaveConfirmView>();
            WireConfirmViewReferences(confirmView);
            EditorUtility.SetDirty(instance);
            replacedCount++;
        }

        return replacedCount;
    }

    private struct ReplaceTarget
    {
        public Transform Parent;
        public int SiblingIndex;
        public GameObject InstanceRoot;
    }

    private static void TryRewireSaveSlotOwnerReferencesInScene(UnityScene scene)
    {
        try
        {
            RewireSaveSlotOwnerReferencesInScene(scene);
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning(
                $"[ModelSaveSlotUiPrefabUtility] 参照配線をスキップしました: {scene.path}\n{exception}");
        }
    }

    /// <summary>
    /// バッチモード用ClayEditセーブスロットUI配線
    /// Unity.exe -batchmode -quit -projectPath ... -executeMethod ModelSaveSlotUiPrefabUtility.WireClayEditSaveSlotUiSilent
    /// </summary>
    public static void WireClayEditSaveSlotUiSilent()
    {
        const string clayEditScenePath = "Assets/Scenes/ClayEdit.unity";
        UnityScene scene = EditorSceneManager.OpenScene(clayEditScenePath, OpenSceneMode.Single);
        SyncOpenSceneSaveSlotUiFromPrefabs();
        SceneUiEditorSavePolicy.MarkDirtyAndSave(scene);
        AssetDatabase.SaveAssets();
    }

    private static void CleanupLegacySlotButtonChildren(Transform host)
    {
        if (host == null)
        {
            return;
        }

        GridLayoutGroup gridLayout = host.GetComponent<GridLayoutGroup>();
        if (gridLayout != null)
        {
            SceneUiEditorSelectionUtility.DestroyImmediateSafe(gridLayout);
        }

        for (int i = host.childCount - 1; i >= 0; i--)
        {
            Transform child = host.GetChild(i);
            if (child != null && child.name != ScrollRootName)
            {
                SceneUiEditorSelectionUtility.DestroyImmediateSafe(child.gameObject);
            }
        }
    }

    /// <summary>
    /// シーン配置済みSaveSlot UIへ参照だけ配線する
    /// </summary>
    [MenuItem("Tools/ClayMonsters/Wire Save Slot UI References")]
    public static void WireSceneReferencesMenu()
    {
        SyncOpenSceneSaveSlotUiFromPrefabs();
        SceneUiEditorSelectionUtility.ResetInspectorSelectionSafe();
        Debug.Log(
            "[ModelSaveSlotUiPrefabUtility] シーン上のセーブスロットUI参照を配線しました"
            + " シーンは未保存です。確認後に手動で保存してください");
    }

    private static bool TryRestoreSaveSlotAttackSlotFrame(Image image, Sprite referenceSprite)
    {
        if (image == null || referenceSprite == null || !IsSaveSlotAttackSlotFrame(image))
        {
            return false;
        }

        if (image.sprite == referenceSprite
            && image.type == Image.Type.Sliced
            && image.color == Color.white)
        {
            return false;
        }

        Undo.RecordObject(image, "Restore Save Slot SlotFrame Visual");
        image.sprite = referenceSprite;
        image.type = Image.Type.Sliced;
        image.color = Color.white;
        EditorUtility.SetDirty(image);
        return true;
    }

    private static bool IsSaveSlotAttackSlotFrame(Image image)
    {
        if (image == null || image.gameObject.name != "SlotFrame")
        {
            return false;
        }

        Transform attackSlot = image.transform.parent;
        return attackSlot != null && attackSlot.name.StartsWith("AttackSlot");
    }

    private static Sprite ResolveSaveSlotSlotFrameReferenceSprite()
    {
        Sprite assetSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SlotFrameSpriteAssetPath);
        if (assetSprite != null)
        {
            return assetSprite;
        }

        GameObject scrollPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ScrollListPrefabPath);
        if (scrollPrefab == null)
        {
            return null;
        }

        Image[] images = scrollPrefab.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image.gameObject.name == "SlotFrame" && image.sprite != null)
            {
                return image.sprite;
            }
        }

        return null;
    }

    private static void CreateOrUpdatePrefabsInternal()
    {
        EnsurePrefabDirectory();
        SaveScrollListPrefab(BuildScrollListHost());
        SaveConfirmViewPrefab(BuildConfirmViewRoot());
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[ModelSaveSlotUiPrefabUtility] プレハブを更新しました");
    }

    /// <summary>
    /// スロット一覧Viewの参照を配線する
    /// 譌｢蟄路ierarchy縺ｮ繝ｬ繧､繧｢繧ｦ繝医・螟画峩縺励↑縺・
    /// </summary>
    public static void WireScrollListReferences(ModelSaveSlotScrollListView scrollList)
    {
        if (scrollList == null)
        {
            return;
        }

        Transform host = scrollList.transform;
        Transform scrollRoot = host.Find(ScrollRootName);
        if (scrollRoot == null)
        {
            scrollRoot = TryInstantiateScrollListChild(host);
            if (scrollRoot == null)
            {
                scrollRoot = BuildScrollListRoot(host).transform;
                EnsureSlotRows(scrollRoot.Find("Viewport/Content"));
            }
        }

        RectTransform content = scrollRoot != null ? scrollRoot.Find("Viewport/Content") as RectTransform : null;
        if (content != null && content.childCount < ModelSavePoolSettings.SlotCount)
        {
            for (int i = content.childCount; i < ModelSavePoolSettings.SlotCount; i++)
            {
                CreateSlotRow(content, i);
            }
        }

        WireScrollListReferencesOnly(scrollList);
    }

    /// <summary>
    /// 繝励Ξ繝上ヶ驟咲ｽｮ貂医∩繧ｹ繝ｭ繝・ヨ荳隕ｧ縺ｮ蜿ら・縺縺鷹・邱壹☆繧・
    /// 陦後・逕滓・繧СectTransform螟画峩縺ｯ陦後ｏ縺ｪ縺・
    /// </summary>
    public static void WireScrollListReferencesOnly(ModelSaveSlotScrollListView scrollList)
    {
        if (scrollList == null)
        {
            return;
        }

        Transform host = scrollList.transform;
        Transform scrollRoot = host.Find(ScrollRootName);
        if (scrollRoot == null)
        {
            ScrollRect nestedScrollRect = scrollList.GetComponentInChildren<ScrollRect>(true);
            scrollRoot = nestedScrollRect != null ? nestedScrollRect.transform : null;
        }

        RectTransform content = scrollRoot != null ? scrollRoot.Find("Viewport/Content") as RectTransform : null;
        ScrollRect scrollRect = scrollRoot != null ? scrollRoot.GetComponent<ScrollRect>() : null;
        List<ModelSaveSlotRowElementRefs> rowRefsList = CollectRowRefs(content);
        WireSaveSlotAttackSlotsOnRows(content);

        SerializedObject serializedScroll = new SerializedObject(scrollList);
        serializedScroll.FindProperty("scrollRect").objectReferenceValue = scrollRect;
        serializedScroll.FindProperty("scrollContent").objectReferenceValue = content;
        SerializedProperty rowRefsProperty = serializedScroll.FindProperty("rowElementRefs");
        rowRefsProperty.arraySize = rowRefsList.Count;
        for (int i = 0; i < rowRefsList.Count; i++)
        {
            rowRefsProperty.GetArrayElementAtIndex(i).objectReferenceValue = rowRefsList[i];
        }

        serializedScroll.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(scrollList);
    }

    /// <inheritdoc cref="WireScrollListReferences"/>
    public static void EnsureScrollListWired(ModelSaveSlotScrollListView scrollList)
    {
        WireScrollListReferences(scrollList);
    }

    private static void WireSaveSlotAttackSlotsOnRows(RectTransform content)
    {
        if (content == null)
        {
            return;
        }

        for (int i = 0; i < content.childCount && i < ModelSavePoolSettings.SlotCount; i++)
        {
            Transform wrapper = content.GetChild(i);
            Transform row = wrapper.Find("SlotRow_" + (i + 1)) ?? (wrapper.childCount > 0 ? wrapper.GetChild(0) : null);
            if (row != null)
            {
                WireSaveSlotAttackSlots(row);
            }
        }
    }

    /// <summary>
    /// 遒ｺ隱阪ヱ繝阪Ν荳翫↓ModelSaveConfirmView繧帝・鄂ｮ縺吶ｋ
    /// 譛ｪ驟咲ｽｮ譎ゅ・縺ｿ繝励Ξ繝上ヶ縺九ｉ逕滓・縺吶ｋ
    /// </summary>
    public static ModelSaveConfirmView EnsureConfirmViewOnRoot(Transform panelRoot)
    {
        if (panelRoot == null)
        {
            return null;
        }

        ModelSaveConfirmView existing = panelRoot.GetComponentInChildren<ModelSaveConfirmView>(true);
        if (existing != null)
        {
            WireConfirmViewRefs(existing);
            return existing;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ConfirmViewPrefabPath);
        if (prefab != null)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, panelRoot) as GameObject;
            if (instance != null)
            {
                ModelSaveConfirmView confirmView = instance.GetComponent<ModelSaveConfirmView>();
                WireConfirmViewRefs(confirmView);
                return confirmView;
            }
        }

        ModelSaveConfirmView built = BuildConfirmViewRoot();
        built.transform.SetParent(panelRoot, false);
        WireConfirmViewRefs(built);
        return built;
    }

    /// <summary>
    /// 遒ｺ隱阪く繝｣繝ｳ繝舌せ荳翫↓ModelSaveConfirmView繧帝・鄂ｮ縺吶ｋ
    /// 譛ｪ驟咲ｽｮ譎ゅ・縺ｿ繝励Ξ繝上ヶ縺九ｉ逕滓・縺吶ｋ
    /// </summary>
    public static ModelSaveConfirmView EnsureConfirmViewOnCanvas(Canvas canvas)
    {
        if (canvas == null)
        {
            return null;
        }

        return EnsureConfirmViewOnRoot(canvas.transform);
    }

    private static void EnsurePrefabDirectory()
    {
        const string directoryPath = "Assets/Resources/UI";
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }
    }

    private static void SaveScrollListPrefab(GameObject host)
    {
        TitleClayUiVisualUtility.ApplySlotRowDecorations(host.transform);
        PrefabUtility.SaveAsPrefabAsset(host, ScrollListPrefabPath);
        SceneUiEditorSelectionUtility.DestroyImmediateSafe(host);
    }

    private static void SaveConfirmViewPrefab(ModelSaveConfirmView confirmView)
    {
        TitleClayUiVisualUtility.ApplySlotRowDecorations(confirmView.transform);
        PrefabUtility.SaveAsPrefabAsset(confirmView.gameObject, ConfirmViewPrefabPath);
        SceneUiEditorSelectionUtility.DestroyImmediateSafe(confirmView.gameObject);
    }

    private static GameObject BuildScrollListHost()
    {
        var host = new GameObject("ModelSaveSlotScrollList", typeof(RectTransform), typeof(ModelSaveSlotScrollListView));
        RectTransform hostRect = host.GetComponent<RectTransform>();
        hostRect.anchorMin = Vector2.zero;
        hostRect.anchorMax = Vector2.one;
        hostRect.pivot = new Vector2(0.5f, 0.5f);
        hostRect.anchoredPosition = Vector2.zero;
        hostRect.offsetMin = new Vector2(16f, 72f);
        hostRect.offsetMax = new Vector2(-16f, -40f);

        GameObject scrollRoot = BuildScrollListRoot(host.transform);
        EnsureSlotRows(scrollRoot.transform.Find("Viewport/Content"));
        ModelSaveSlotScrollListView scrollList = host.GetComponent<ModelSaveSlotScrollListView>();
        EnsureScrollListWired(scrollList);
        return host;
    }

    private static ModelSaveConfirmView BuildConfirmViewRoot()
    {
        var viewObject = new GameObject(
            "ModelSaveConfirmView",
            typeof(RectTransform),
            typeof(ModelSaveConfirmView));
        RectTransform viewRect = viewObject.GetComponent<RectTransform>();
        viewRect.anchorMin = Vector2.zero;
        viewRect.anchorMax = Vector2.one;
        viewRect.offsetMin = Vector2.zero;
        viewRect.offsetMax = Vector2.zero;

        ModelSaveConfirmView confirmView = viewObject.GetComponent<ModelSaveConfirmView>();
        EnsureConfirmContentHierarchy(confirmView.transform);
        WireConfirmViewRefs(confirmView);
        return confirmView;
    }

    private static Transform TryInstantiateScrollListChild(Transform host)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ScrollListPrefabPath);
        if (prefab == null)
        {
            return null;
        }

        Transform prefabScrollRoot = prefab.transform.Find(ScrollRootName);
        if (prefabScrollRoot == null)
        {
            return null;
        }

        GameObject scrollInstance = PrefabUtility.InstantiatePrefab(prefabScrollRoot.gameObject, host) as GameObject;
        return scrollInstance != null ? scrollInstance.transform : null;
    }

    private static GameObject BuildScrollListRoot(Transform host)
    {
        var scrollRoot = new GameObject(ScrollRootName, typeof(RectTransform), typeof(ScrollRect));
        scrollRoot.transform.SetParent(host, false);
        RectTransform scrollRectTransform = scrollRoot.GetComponent<RectTransform>();
        scrollRectTransform.anchorMin = Vector2.zero;
        scrollRectTransform.anchorMax = Vector2.one;
        scrollRectTransform.offsetMin = Vector2.zero;
        scrollRectTransform.offsetMax = Vector2.zero;

        var viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        viewportObject.transform.SetParent(scrollRoot.transform, false);
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
        viewportObject.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);

        var contentObject = new GameObject(
            "Content",
            typeof(RectTransform),
            typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter));
        contentObject.transform.SetParent(viewportObject.transform, false);
        RectTransform scrollContent = contentObject.GetComponent<RectTransform>();
        scrollContent.anchorMin = new Vector2(0f, 1f);
        scrollContent.anchorMax = new Vector2(1f, 1f);
        scrollContent.pivot = new Vector2(0.5f, 1f);
        scrollContent.anchoredPosition = Vector2.zero;
        scrollContent.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup contentLayout = contentObject.GetComponent<VerticalLayoutGroup>();
        contentLayout.childAlignment = TextAnchor.UpperCenter;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        contentLayout.spacing = 0f;

        ContentSizeFitter contentSizeFitter = contentObject.GetComponent<ContentSizeFitter>();
        contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scrollRect = scrollRoot.GetComponent<ScrollRect>();
        scrollRect.viewport = viewportRect;
        scrollRect.content = scrollContent;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 24f;
        return scrollRoot;
    }

    private static void EnsureSlotRows(Transform content)
    {
        if (content == null)
        {
            return;
        }

        for (int i = content.childCount; i < ModelSavePoolSettings.SlotCount; i++)
        {
            CreateSlotRow(content, i);
        }

        for (int i = 0; i < content.childCount && i < ModelSavePoolSettings.SlotCount; i++)
        {
            Transform wrapper = content.GetChild(i);
            Transform row = wrapper.Find("SlotRow_" + (i + 1)) ?? (wrapper.childCount > 0 ? wrapper.GetChild(0) : null);
            if (row != null)
            {
                WireSaveSlotAttackSlots(row);
            }
        }
    }

    private static void CreateSlotRow(Transform content, int slotIndex)
    {
        float rowWidth = ModelSaveSlotRowUiBuilder.RowMaxWidth;
        var wrapperObject = new GameObject(
            "SlotRowWrapper_" + (slotIndex + 1),
            typeof(RectTransform),
            typeof(RectMask2D));
        wrapperObject.transform.SetParent(content, false);
        RectTransform wrapperRect = wrapperObject.GetComponent<RectTransform>();
        wrapperRect.anchorMin = new Vector2(0.5f, 1f);
        wrapperRect.anchorMax = new Vector2(0.5f, 1f);
        wrapperRect.pivot = new Vector2(0.5f, 1f);
        wrapperRect.sizeDelta = new Vector2(rowWidth, ModelSaveSlotRowUiBuilder.RowMinHeight);

        var rowObject = new GameObject(
            "SlotRow_" + (slotIndex + 1),
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(HorizontalLayoutGroup));
        rowObject.transform.SetParent(wrapperObject.transform, false);
        RectTransform rowRect = rowObject.GetComponent<RectTransform>();
        rowRect.anchorMin = Vector2.zero;
        rowRect.anchorMax = Vector2.one;
        rowRect.offsetMin = Vector2.zero;
        rowRect.offsetMax = Vector2.zero;

        Component button = SceneUiLhButtonUtility.AddComponent(rowObject);
        SceneUiLhButtonUtility.ApplySlotButton(button);

        HorizontalLayoutGroup horizontalLayout = rowObject.GetComponent<HorizontalLayoutGroup>();
        horizontalLayout.padding = new RectOffset(12, 12, 12, 12);
        horizontalLayout.spacing = 12f;
        horizontalLayout.childAlignment = TextAnchor.UpperLeft;
        horizontalLayout.childControlWidth = true;
        horizontalLayout.childControlHeight = true;
        horizontalLayout.childForceExpandWidth = false;
        horizontalLayout.childForceExpandHeight = false;

        ModelSaveSlotRowUiBuilder.BuildConfirmContent(
            rowObject.transform,
            rowWidth,
            ModelSaveSlotRowUiBuilder.ScrollListThumbnailColumnWidth,
            ModelSaveSlotRowUiBuilder.ConfirmLayoutSize.Standard);

        RebuildSaveSlotAttackSlots(rowObject.transform);

        ModelSaveSlotRowElementRefs rowRefs = rowObject.GetComponent<ModelSaveSlotRowElementRefs>();
        if (rowRefs == null)
        {
            rowRefs = rowObject.AddComponent<ModelSaveSlotRowElementRefs>();
        }

        rowRefs.EnsureScrollListLayout();
        rowRefs.CaptureFromHierarchy(rowObject.transform);
    }

    private static List<ModelSaveSlotRowElementRefs> CollectRowRefs(RectTransform content)
    {
        var rowRefsList = new List<ModelSaveSlotRowElementRefs>(ModelSavePoolSettings.SlotCount);
        if (content == null)
        {
            return rowRefsList;
        }

        for (int i = 0; i < content.childCount && i < ModelSavePoolSettings.SlotCount; i++)
        {
            Transform wrapper = content.GetChild(i);
            Transform row = wrapper.Find("SlotRow_" + (i + 1)) ?? wrapper.GetChild(0);
            ModelSaveSlotRowElementRefs refs = row.GetComponent<ModelSaveSlotRowElementRefs>();
            if (refs == null)
            {
                refs = row.gameObject.AddComponent<ModelSaveSlotRowElementRefs>();
            }

            refs.EnsureScrollListLayout();
            if (!refs.HasWiredReferences())
            {
                refs.CaptureFromHierarchy(row);
            }

            rowRefsList.Add(refs);
        }

        return rowRefsList;
    }

    private static void EnsureConfirmContentHierarchy(Transform viewTransform)
    {
        Transform contentRoot = viewTransform.Find("ConfirmContent");
        if (contentRoot == null)
        {
            var rootObject = new GameObject("ConfirmContent", typeof(RectTransform));
            rootObject.transform.SetParent(viewTransform, false);
            RectTransform rootRect = rootObject.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = new Vector2(0f, 96f);
            contentRoot = rootObject.transform;
        }

        Transform rowTransform = contentRoot.Find("ConfirmSlotRow");
        if (rowTransform == null)
        {
            var rowObject = new GameObject(
                "ConfirmSlotRow",
                typeof(RectTransform),
                typeof(HorizontalLayoutGroup));
            rowObject.transform.SetParent(contentRoot, false);
            HorizontalLayoutGroup layout = rowObject.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 24, 24);
            layout.spacing = 24f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            rowTransform = rowObject.transform;

            RectTransform rowRect = rowObject.GetComponent<RectTransform>();
            rowRect.anchorMin = Vector2.zero;
            rowRect.anchorMax = Vector2.one;
            rowRect.offsetMin = Vector2.zero;
            rowRect.offsetMax = Vector2.zero;

            ModelSaveSlotRowUiBuilder.BuildConfirmContent(
                rowTransform,
                ModelSaveSlotRowUiBuilder.ConfirmPreviewRowWidth,
                ModelSaveSlotRowUiBuilder.ConfirmPreviewThumbnailColumnWidth,
                ModelSaveSlotRowUiBuilder.ConfirmLayoutSize.Preview);

            RebuildSaveSlotAttackSlots(rowTransform);
        }

        ModelSaveSlotRowElementRefs rowRefs = rowTransform.GetComponent<ModelSaveSlotRowElementRefs>();
        if (rowRefs == null)
        {
            rowRefs = rowTransform.gameObject.AddComponent<ModelSaveSlotRowElementRefs>();
        }

        rowRefs.EnsureClayEditPreviewLayout();
        rowRefs.CaptureFromHierarchy(rowTransform);
    }

    /// <summary>
    /// 遒ｺ隱崎｡祁iew縺ｮ蜿ら・繧帝・邱壹☆繧・
    /// 譌｢蟄路ierarchy縺ｮ繝ｬ繧､繧｢繧ｦ繝医・螟画峩縺励↑縺・
    /// </summary>
    public static void WireConfirmViewReferences(ModelSaveConfirmView confirmView)
    {
        WireConfirmViewRefs(confirmView);
    }

    private static void WireConfirmViewRefs(ModelSaveConfirmView confirmView)
    {
        if (confirmView == null)
        {
            return;
        }

        Transform viewTransform = confirmView.transform;
        Transform contentRoot = viewTransform.Find("ConfirmContent");
        Transform rowTransform = contentRoot != null ? contentRoot.Find("ConfirmSlotRow") : null;
        ModelSaveSlotRowElementRefs rowRefs = rowTransform != null
            ? rowTransform.GetComponent<ModelSaveSlotRowElementRefs>()
            : null;

        SerializedObject serializedConfirm = new SerializedObject(confirmView);
        serializedConfirm.FindProperty("contentRoot").objectReferenceValue = contentRoot;
        serializedConfirm.FindProperty("rowElementRefs").objectReferenceValue = rowRefs;
        serializedConfirm.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(confirmView);
    }

    public static void WireSaveSlotAttackSlots(Transform rowRoot)
    {
        if (rowRoot == null)
        {
            return;
        }

        RectTransform attacksColumn = ResolveAttacksColumn(rowRoot);
        if (attacksColumn == null)
        {
            return;
        }

        if (!AttackSlotUiEditorUtility.TryWireSaveSlotAttacksContent(attacksColumn))
        {
            Debug.LogWarning(
                $"[ModelSaveSlotUiPrefabUtility] {rowRoot.name} の攻撃列が未整備です。"
                    + "Hierarchyで手動調整するか、初回のみ Create Save Slot UI Prefabs を実行してください",
                rowRoot);
        }
    }

    /// <inheritdoc cref="WireSaveSlotAttackSlots"/>
    public static void EnsureSaveSlotAttackSlots(Transform rowRoot)
    {
        WireSaveSlotAttackSlots(rowRoot);
    }

    private static void RebuildSaveSlotAttackSlots(Transform rowRoot)
    {
        RectTransform attacksColumn = ResolveAttacksColumn(rowRoot);
        if (attacksColumn != null)
        {
            AttackSlotUiEditorUtility.RebuildSaveSlotAttacksContent(attacksColumn);
        }
    }

    private static RectTransform ResolveAttacksColumn(Transform rowRoot)
    {
        if (rowRoot == null)
        {
            return null;
        }

        Transform dataRow = rowRoot.Find("DataRow");
        RectTransform attacksColumn = dataRow != null
            ? dataRow.Find("AttacksColumn") as RectTransform
            : rowRoot.Find("AttacksColumn") as RectTransform;
        if (attacksColumn == null)
        {
            attacksColumn = rowRoot.Find("AttacksContainer") as RectTransform;
        }

        return attacksColumn;
    }

    private static void ResyncConfirmViewsInOpenScene()
    {
        ModelSaveConfirmView[] confirmViews =
            Object.FindObjectsByType<ModelSaveConfirmView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var processedPanelRoots = new HashSet<int>();
        for (int i = 0; i < confirmViews.Length; i++)
        {
            ModelSaveConfirmView confirmView = confirmViews[i];
            if (confirmView == null)
            {
                continue;
            }

            Transform panelRoot = confirmView.transform.parent;
            if (panelRoot == null || !processedPanelRoots.Add(panelRoot.GetInstanceID()))
            {
                continue;
            }

            ResyncConfirmViewFromPrefab(panelRoot);
        }
    }

    private static void RewireSaveSlotViewReferences(SaveSlotView saveSlotView)
    {
        if (saveSlotView == null)
        {
            return;
        }

        SerializedObject serializedSaveSlot = new SerializedObject(saveSlotView);
        Canvas slotCanvas = serializedSaveSlot.FindProperty("slotCanvas").objectReferenceValue as Canvas;
        Canvas saveConfirmCanvas = serializedSaveSlot.FindProperty("saveConfirmCanvas").objectReferenceValue as Canvas;
        Canvas slotActionCanvas = serializedSaveSlot.FindProperty("slotActionCanvas").objectReferenceValue as Canvas;
        Canvas nameInputCanvas = serializedSaveSlot.FindProperty("nameInputCanvas").objectReferenceValue as Canvas;

        if (slotCanvas != null)
        {
            ModelSaveSlotScrollListView scrollList = ResolveScrollListUnderCanvas(slotCanvas.transform);
            SetObjectReference(serializedSaveSlot, "slotScrollList", scrollList);
        }

        if (saveConfirmCanvas != null)
        {
            ModelSaveConfirmView confirmView =
                saveConfirmCanvas.GetComponentInChildren<ModelSaveConfirmView>(true);
            SetObjectReference(serializedSaveSlot, "saveConfirmView", confirmView);
            if (confirmView != null)
            {
                WireConfirmViewReferences(confirmView);
            }

            WireLhButtonReference(serializedSaveSlot, "confirmSaveButton", saveConfirmCanvas.transform, "ConfirmSaveButton");
            WireLhButtonReference(serializedSaveSlot, "confirmBackButton", saveConfirmCanvas.transform, "ConfirmBackButton");
            WireLhButtonReference(serializedSaveSlot, "closeButton", saveConfirmCanvas.transform, "CloseButton");
        }

        if (slotActionCanvas != null)
        {
            ModelSaveConfirmView slotActionConfirmView =
                slotActionCanvas.GetComponentInChildren<ModelSaveConfirmView>(true);
            SetObjectReference(serializedSaveSlot, "slotActionConfirmView", slotActionConfirmView);
            if (slotActionConfirmView != null)
            {
                WireConfirmViewReferences(slotActionConfirmView);
            }

            WireLhButtonReference(
                serializedSaveSlot,
                "slotActionOverwriteButton",
                slotActionCanvas.transform,
                "SlotActionOverwriteButton");
            WireLhButtonReference(
                serializedSaveSlot,
                "slotActionDeleteButton",
                slotActionCanvas.transform,
                "SlotActionDeleteButton");
            WireLhButtonReference(
                serializedSaveSlot,
                "slotActionBackButton",
                slotActionCanvas.transform,
                "SlotActionBackButton");
        }

        if (nameInputCanvas != null)
        {
            WireLhButtonReference(
                serializedSaveSlot,
                "nameInputBackButton",
                nameInputCanvas.transform,
                "NameInputBackButton");
        }

        serializedSaveSlot.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(saveSlotView);
    }

    private static void RewireClayEditRemakeLoadSlotViewReferences(ClayEditRemakeLoadSlotView remakeLoadSlotView)
    {
        if (remakeLoadSlotView == null)
        {
            return;
        }

        SerializedObject serializedRemake = new SerializedObject(remakeLoadSlotView);
        ModelSaveSlotScrollListView scrollList = ResolveScrollListUnderCanvas(remakeLoadSlotView.transform);
        SetObjectReference(serializedRemake, "slotScrollList", scrollList);

        Canvas confirmCanvas = serializedRemake.FindProperty("confirmCanvas").objectReferenceValue as Canvas;
        if (confirmCanvas != null)
        {
            ModelSaveConfirmView confirmView =
                confirmCanvas.GetComponentInChildren<ModelSaveConfirmView>(true);
            SetObjectReference(serializedRemake, "loadConfirmView", confirmView);
            if (confirmView != null)
            {
                WireConfirmViewReferences(confirmView);
            }

            WireLhButtonReference(serializedRemake, "selectButton", confirmCanvas.transform, "RemakeSelectButton");
            WireLhButtonReference(serializedRemake, "deleteButton", confirmCanvas.transform, "RemakeDeleteButton");
            WireLhButtonReference(serializedRemake, "backButton", confirmCanvas.transform, "RemakeConfirmBackButton");
        }

        WireLhButtonReference(serializedRemake, "listBackButton", remakeLoadSlotView.transform, "RemakeListBackButton");
        ModelSaveSlotDeletePromptView deletePromptView =
            remakeLoadSlotView.GetComponentInChildren<ModelSaveSlotDeletePromptView>(true);
        SetObjectReference(serializedRemake, "deletePromptView", deletePromptView);

        serializedRemake.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(remakeLoadSlotView);
    }

    private static void SetObjectReference(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            return;
        }

        property.objectReferenceValue = value;
    }

    private static void WireLhButtonReference(
        SerializedObject serializedObject,
        string propertyName,
        Transform searchRoot,
        string buttonName)
    {
        if (searchRoot == null)
        {
            return;
        }

        Transform buttonTransform = FindDeepChild(searchRoot, buttonName);
        Component button = FindLhButtonComponent(buttonTransform);
        SetObjectReference(serializedObject, propertyName, button);
    }

    private static Component FindLhButtonComponent(Transform buttonTransform)
    {
        if (buttonTransform == null)
        {
            return null;
        }

        MonoBehaviour[] behaviours = buttonTransform.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour != null && behaviour.GetType().Name == "LHButton")
            {
                return behaviour;
            }
        }

        return null;
    }

    private static Transform FindDeepChild(Transform parent, string objectName)
    {
        if (parent == null)
        {
            return null;
        }

        if (parent.name == objectName)
        {
            return parent;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDeepChild(parent.GetChild(i), objectName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static ModelSaveSlotScrollListView ResolveScrollListUnderCanvas(Transform canvasRoot)
    {
        if (canvasRoot == null)
        {
            return null;
        }

        return canvasRoot.GetComponentInChildren<ModelSaveSlotScrollListView>(true);
    }

    private static void RewireLoadSlotViewReferences(LoadSlotView loadSlotView)
    {
        TrainingSceneCreator.WireLoadSlotViewReferences(loadSlotView);
    }

    private static void RewireTrainedSaveViewReferences(TrainingTrainedSaveView trainedSaveView)
    {
        if (trainedSaveView == null)
        {
            return;
        }

        ModelSaveSlotScrollListView scrollList =
            trainedSaveView.GetComponentInChildren<ModelSaveSlotScrollListView>(true);
        ModelSaveConfirmView confirmView =
            trainedSaveView.GetComponentInChildren<ModelSaveConfirmView>(true);
        SerializedObject serializedTrainedSave = new SerializedObject(trainedSaveView);
        serializedTrainedSave.FindProperty("slotScrollList").objectReferenceValue = scrollList;
        serializedTrainedSave.FindProperty("confirmView").objectReferenceValue = confirmView;
        serializedTrainedSave.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(trainedSaveView);
    }
}
#endif
