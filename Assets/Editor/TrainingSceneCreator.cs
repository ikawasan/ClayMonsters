#if UNITY_EDITOR
using ClayEditor.Rigging;
using Extensions;
using SaveData;
using Scene.BattleNpcScene;
using Scene.BattleNpcScene.View;
using Scene.TrainingScene;
using Scene.TrainingScene.Domain;
using Scene.TrainingScene.View;
using System.IO;
using TMPro;
using UI.Battle.View;
using UI.ClayEditor.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityScene = UnityEngine.SceneManagement.Scene;

/// <summary>
/// TrainingシーンをBattleNpcベースで生成しコンポーネントを配線する
/// </summary>
public static class TrainingSceneCreator
{
    private const string SourceScenePath = "Assets/Scenes/BattleNpc.unity";
    private const string TargetScenePath = "Assets/Scenes/Training.unity";
    private const string BattleNpcSceneTypeName = "Scene.BattleNpcScene.BattleNpcScene, Scene";
    private const string BattleNpcLifetimeScopeTypeName = "Scene.BattleNpcScene.BattleNpcLifetimeScope, Scene";
    private const string TrainingSceneTypeName = "Scene.TrainingScene.TrainingScene, Scene";
    private const string TrainingLifetimeScopeTypeName = "Scene.TrainingScene.TrainingLifetimeScope, Scene";

    private static bool bootstrapNewSceneFile;

    public static void RepairTrainingSceneAndSave()
    {
        ConfigureTrainingSceneSilent(saveScene: true);
        if (!Application.isBatchMode)
        {
            EditorUtility.DisplayDialog(
                "Training Scene",
                "Trainingシーンの修復と参照配線を完了し保存しました",
                "OK");
        }
    }

    [MenuItem("Tools/ClayMonsters/Wire Training Scene References")]
    public static void WireTrainingSceneReferences()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        ConfigureTrainingSceneSilent(saveScene: false);
        if (!Application.isBatchMode)
        {
            LoadSlotView loadSlotView = Object.FindFirstObjectByType<LoadSlotView>(FindObjectsInactive.Include);
            SerializedObject serializedLoadSlot = loadSlotView != null
                ? new SerializedObject(loadSlotView)
                : null;
            bool confirmMissing = serializedLoadSlot == null
                || serializedLoadSlot.FindProperty("confirmPanelRoot").objectReferenceValue == null;
            EditorUtility.DisplayDialog(
                "Training Scene",
                confirmMissing
                    ? "Trainingシーンの参照配線を更新しました\n"
                        + "ConfirmPanel参照が未設定ですHierarchyで確認してください\n"
                        + "シーンは未保存です。Ctrl+Sで保存してください"
                    : "Trainingシーンの参照配線を更新しました\n"
                        + "UIレイアウトは一切変更していません\n"
                        + "育成中UI: Resources/UI/TrainingInProgressHud.prefab\n"
                        + "育成途中確認: Resources/UI/TrainingResumeWindow.prefab\n"
                        + "レイアウト調整は各プレハブを手動編集してください\n"
                        + "シーンは未保存です。Ctrl+Sで保存してください",
                "OK");
        }
    }

    public static void RebuildTrainingSceneUi()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        if (!Application.isBatchMode
            && !EditorUtility.DisplayDialog(
                "Training Scene UI",
                "TrainingシーンのUIを削除して初期レイアウトで再生成します\n"
                    + "TrainingHudCanvas / LoadSlotCanvas / 育成完了保存UI などが対象です\n\n"
                    + "調整済みのRectTransformは失われます\n"
                    + "参照配線のみ必要な場合は Wire Training Scene References を使ってください",
                "再生成する",
                "キャンセル"))
        {
            return;
        }

        EnsureSceneFileExists();
        UnityScene scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);
        LoadSlotPersistedRefs loadSlotPersistedRefs = CaptureLoadSlotPersistedRefs();
        DestroyTrainingUiRoots();
        ConfigureTrainingHud(SceneUpdateMode.RebuildLayout);
        ConfigureTrainingTrainedSave(SceneUpdateMode.RebuildLayout);
        ConfigureLifetimeScope();
        NormalizeTrainingSceneCanvasLayout(SceneUpdateMode.RebuildLayout);
        ConfigureLoadSlotSelection(SceneUpdateMode.RebuildLayout, loadSlotPersistedRefs);
        ValidateLoadSlotConfirmUiWired();
        SceneUiEditorSavePolicy.MarkDirty(scene);
        EnsureBuildSettings();
        bootstrapNewSceneFile = false;

        if (!Application.isBatchMode)
        {
            EditorUtility.DisplayDialog(
                "Training Scene UI",
                "TrainingシーンのUI再生成を完了しました\n"
                    + "位置やサイズはHierarchy上で調整してください\n"
                    + "シーンは未保存です。必要に応じて手動で保存してください",
                "OK");
        }
    }

    /// <summary>
    /// バッチモード向けにTrainingシーンの参照だけ配線する
    /// </summary>
    public static void ConfigureTrainingSceneSilent(bool saveScene = false)
    {
        EnsureSceneFileExists();
        UnityScene scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);
        ConfigureScene(SceneUpdateMode.WireReferences);
        if (saveScene)
        {
            SceneUiEditorSavePolicy.MarkDirtyAndSave(scene);
        }
        else
        {
            SceneUiEditorSavePolicy.MarkDirty(scene);
        }

        EnsureBuildSettings();
        bootstrapNewSceneFile = false;
    }

    private static void EnsureSceneFileExists()
    {
        if (File.Exists(TargetScenePath))
        {
            return;
        }

        if (!File.Exists(SourceScenePath))
        {
            Debug.LogError($"[TrainingSceneCreator] source scene not found: {SourceScenePath}");
            return;
        }

        File.Copy(SourceScenePath, TargetScenePath, overwrite: false);
        AssetDatabase.ImportAsset(TargetScenePath);
        bootstrapNewSceneFile = true;
    }

    private enum SceneUpdateMode
    {
        WireReferences,
        RebuildLayout,
    }

    private static void ConfigureScene(SceneUpdateMode updateMode)
    {
        if (bootstrapNewSceneFile)
        {
            updateMode = SceneUpdateMode.RebuildLayout;
        }

        if (updateMode == SceneUpdateMode.WireReferences)
        {
            ConfigureSceneReferencesOnly();
            return;
        }

        DisableBattleOnlyObjects();
        ConfigureRootSceneComponents();
        ConfigureTrainingDisplay();
        ConfigureTrainingBackground();
        ConfigureTrainingLocationCamera();
        ConfigureTrainingHud(updateMode);
        ConfigureTrainingTrainedSave(updateMode);
        ConfigureTrainingBattle();
        ConfigureLifetimeScope();
        MigrateTrainingNestedCanvasesToGroups();
        NormalizeTrainingSceneCanvasLayout(updateMode);
        EnsureBattleStartOverlayReferences();
        ConfigureLoadSlotSelection(updateMode, CaptureLoadSlotPersistedRefs());
        ValidateLoadSlotConfirmUiWired();
    }

    /// <summary>
    /// TrainingシーンのSerializeField参照だけ配線する
    /// RectTransform・Hierarchy・UI生成は一切行わない
    /// </summary>
    private static void ConfigureSceneReferencesOnly()
    {
        TrainingHudView hudView = Object.FindFirstObjectByType<TrainingHudView>(FindObjectsInactive.Include);
        if (hudView != null)
        {
            WireTrainingHudReferencesOnly(hudView);
            EditorUtility.SetDirty(hudView);
        }

        WireTrainingTrainedSaveReferencesOnly();

        LoadSlotPersistedRefs persistedRefs = CaptureLoadSlotPersistedRefs();
        LoadSlotView[] loadSlotViews =
            Object.FindObjectsByType<LoadSlotView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < loadSlotViews.Length; i++)
        {
            WireLoadSlotReferencesOnly(loadSlotViews[i], persistedRefs);
        }

        WireLifetimeScopeReferences();
        ValidateLoadSlotConfirmUiWired();
    }

    /// <summary>
    /// LoadSlotViewの確認パネルとボタンを配線する
    /// </summary>
    public static void WireLoadSlotViewReferences(LoadSlotView loadSlotView)
    {
        WireLoadSlotReferences(loadSlotView, CaptureLoadSlotPersistedRefs());
    }

    private static void ValidateLoadSlotConfirmUiWired()
    {
        LoadSlotView loadSlotView = Object.FindFirstObjectByType<LoadSlotView>(FindObjectsInactive.Include);
        if (loadSlotView == null)
        {
            return;
        }

        SerializedObject serializedLoadSlot = new SerializedObject(loadSlotView);
        if (serializedLoadSlot.FindProperty("confirmPanelRoot").objectReferenceValue == null
            || serializedLoadSlot.FindProperty("loadButton").objectReferenceValue == null)
        {
            Debug.LogError(
                "[TrainingSceneCreator] LoadSlotViewのConfirmPanelまたはLoadConfirmButtonが未配線です"
                + " Hierarchyを確認して手動で配線してください",
                loadSlotView);
        }
    }

    private static void EnsureBattleStartOverlayReferences()
    {
        if (System.IO.File.Exists(BattleStartOverlayPrefabUtility.PrefabPath))
        {
            BattleStartOverlayView overlayView =
                Object.FindFirstObjectByType<BattleStartOverlayView>(FindObjectsInactive.Include);
            if (overlayView != null)
            {
                BattleStartOverlayPrefabUtility.EnsurePrefabInstanceUnderHost(overlayView.transform);
                BattleStartOverlayPrefabUtility.WireOverlayReferences(overlayView);
                return;
            }
        }

        SceneUiPlacementMigrator.EnsureBattleStartOverlayReferencesInActiveScene();
    }

    private static void DisableBattleOnlyObjects()
    {
        DisableComponentsInScene<BattleFlowRunner>();
        DisableComponentsInScene<BattleNpcView>();
        DisableComponentsInScene<BattleStartOverlayView>();
        DisableComponentsInScene<BattleNpcStaging>();
        DisableComponentsInScene<BattleMatchupBackgroundView>();
        RemoveComponentsInScene(BattleNpcSceneTypeName);
        RemoveComponentsInScene(BattleNpcLifetimeScopeTypeName);

        GameObject battleCanvas = GameObject.Find("BattleCanvas");
        if (battleCanvas != null)
        {
            battleCanvas.SetActive(false);
        }
    }

    private static void DisableComponentsInScene<T>() where T : Behaviour
    {
        T[] components = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < components.Length; i++)
        {
            components[i].enabled = false;
        }
    }

    private static void RemoveComponentsInScene(string typeName)
    {
        System.Type componentType = System.Type.GetType(typeName);
        if (componentType == null)
        {
            return;
        }

        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            Component[] components = roots[rootIndex].GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component != null && componentType.IsInstanceOfType(component))
                {
                    Object.DestroyImmediate(component);
                }
            }
        }
    }

    private static void ConfigureRootSceneComponents()
    {
        ConsolidateTrainingSceneRootsInEditor();

        GameObject root = FindPrimaryTrainingSceneRoot();
        if (root == null)
        {
            root = new GameObject("TrainingScene");
        }

        Component trainingScene = GetOrAddComponent(root, TrainingSceneTypeName);
        Component lifetimeScope = GetOrAddComponent(root, TrainingLifetimeScopeTypeName);
        GetOrAddComponent(root, "Scene.TrainingScene.TrainingFlowRunner, Scene");

        if (lifetimeScope != null && trainingScene != null)
        {
            SerializedObject serializedScope = new SerializedObject(lifetimeScope);
            SetObjectReference(serializedScope, "trainingScene", trainingScene);
            serializedScope.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorUtility.SetDirty(root);
    }

    private static void ConsolidateTrainingSceneRootsInEditor()
    {
        System.Type trainingSceneType = ResolveType(TrainingSceneTypeName);
        if (trainingSceneType == null)
        {
            return;
        }

        UnityEngine.Object[] sceneComponents = Object.FindObjectsByType(
            trainingSceneType,
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        if (sceneComponents.Length <= 1)
        {
            return;
        }

        Component primary = null;
        int bestScore = int.MinValue;
        for (int i = 0; i < sceneComponents.Length; i++)
        {
            if (sceneComponents[i] is not Component component)
            {
                continue;
            }

            int score = ScoreTrainingSceneRootInEditor(component.gameObject);
            if (score > bestScore)
            {
                bestScore = score;
                primary = component;
            }
        }

        if (primary == null)
        {
            return;
        }

        primary.gameObject.SetActive(true);
        for (int i = 0; i < sceneComponents.Length; i++)
        {
            if (sceneComponents[i] is not Component component || component == primary)
            {
                continue;
            }

            Debug.LogWarning(
                $"[TrainingSceneCreator] 重複TrainingSceneルートを削除しました: {component.gameObject.name}");
            Object.DestroyImmediate(component.gameObject);
        }
    }

    private static int ScoreTrainingSceneRootInEditor(GameObject root)
    {
        int score = root.GetComponentsInChildren<Transform>(true).Length;
        if (root.GetComponent<TrainingBattleRunner>() != null)
        {
            score += 1000;
        }

        System.Type lifetimeScopeType = ResolveType(TrainingLifetimeScopeTypeName);
        if (lifetimeScopeType != null && root.GetComponent(lifetimeScopeType) != null)
        {
            score += 500;
        }

        return score;
    }

    private static GameObject FindPrimaryTrainingSceneRoot()
    {
        System.Type trainingSceneType = ResolveType(TrainingSceneTypeName);
        if (trainingSceneType == null)
        {
            return GameObject.Find("TrainingScene");
        }

        UnityEngine.Object[] sceneComponents = Object.FindObjectsByType(
            trainingSceneType,
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        if (sceneComponents.Length == 0)
        {
            return null;
        }

        Component primary = null;
        int bestScore = int.MinValue;
        for (int i = 0; i < sceneComponents.Length; i++)
        {
            if (sceneComponents[i] is not Component component)
            {
                continue;
            }

            int score = ScoreTrainingSceneRootInEditor(component.gameObject);
            if (score > bestScore)
            {
                bestScore = score;
                primary = component;
            }
        }

        return primary != null ? primary.gameObject : null;
    }

    private static void ConfigureTrainingDisplay()
    {
        TrainingDisplay display = Object.FindFirstObjectByType<TrainingDisplay>(FindObjectsInactive.Include);
        if (display == null)
        {
            var displayObject = new GameObject("TrainingDisplay");
            display = displayObject.AddComponent<TrainingDisplay>();
        }

        LoadedModelConfigurator configurator = Object.FindFirstObjectByType<LoadedModelConfigurator>(FindObjectsInactive.Include);
        Transform displayAnchor = EnsureTrainingModelDisplayPoint();
        Transform importRoot = EnsureTrainingModelImportRoot(displayAnchor);
        Transform fieldRoot = GameObject.Find("Field")?.transform;

        SerializedObject serializedDisplay = new SerializedObject(display);
        serializedDisplay.FindProperty("spawnParent").objectReferenceValue = importRoot;
        serializedDisplay.FindProperty("displayAnchor").objectReferenceValue = displayAnchor;
        serializedDisplay.FindProperty("configurator").objectReferenceValue = configurator;
        serializedDisplay.FindProperty("fieldRoot").objectReferenceValue = fieldRoot;
        serializedDisplay.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(display);
    }

    private static Transform EnsureTrainingModelDisplayPoint()
    {
        GameObject existing = GameObject.Find("TrainingModelDisplayPoint");
        if (existing != null)
        {
            return existing.transform;
        }

        Transform layoutRoot = GameObject.Find("TrainingScene")?.transform;
        var anchorObject = new GameObject("TrainingModelDisplayPoint");
        if (layoutRoot != null)
        {
            anchorObject.transform.SetParent(layoutRoot, false);
        }

        anchorObject.transform.localPosition = Vector3.zero;
        anchorObject.transform.localRotation = Quaternion.identity;
        return anchorObject.transform;
    }

    private static Transform EnsureTrainingModelImportRoot(Transform displayAnchor)
    {
        if (displayAnchor == null)
        {
            return null;
        }

        Transform existing = displayAnchor.Find("TrainingModelImportRoot");
        if (existing != null)
        {
            return existing;
        }

        var importRootObject = new GameObject("TrainingModelImportRoot");
        importRootObject.transform.SetParent(displayAnchor, false);
        importRootObject.transform.localPosition = Vector3.zero;
        importRootObject.transform.localRotation = Quaternion.identity;
        return importRootObject.transform;
    }

    private static void ConfigureTrainingBackground()
    {
        TrainingBackgroundView backgroundView =
            Object.FindFirstObjectByType<TrainingBackgroundView>(FindObjectsInactive.Include);
        if (backgroundView == null)
        {
            var backgroundObject = new GameObject("TrainingBackground");
            backgroundView = backgroundObject.AddComponent<TrainingBackgroundView>();
        }

        Transform root = backgroundView.transform;
        GameObject trainingSceneRoot = GameObject.Find("TrainingScene");
        if (trainingSceneRoot != null && root.parent != trainingSceneRoot.transform)
        {
            root.SetParent(trainingSceneRoot.transform, false);
        }

        GameObject defaultBackground = EnsureBackgroundSlot(root, "DefaultBackground", active: true);
        GameObject restBackground = EnsureBackgroundSlot(root, "RestBackground", active: false);

        TrainingLocation[] locations = (TrainingLocation[])System.Enum.GetValues(typeof(TrainingLocation));

        SerializedObject serializedBackground = new SerializedObject(backgroundView);
        serializedBackground.FindProperty("backgroundRoot").objectReferenceValue = root;
        serializedBackground.FindProperty("defaultBackground").objectReferenceValue = defaultBackground;
        serializedBackground.FindProperty("restBackground").objectReferenceValue = restBackground;

        SerializedProperty entries = serializedBackground.FindProperty("locationBackgrounds");
        entries.arraySize = locations.Length;
        for (int i = 0; i < locations.Length; i++)
        {
            TrainingLocation location = locations[i];
            GameObject slot = EnsureBackgroundSlot(root, $"Background_{location}", active: false);
            SerializedProperty element = entries.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("location").enumValueIndex = (int)location;
            element.FindPropertyRelative("backgroundObject").objectReferenceValue = slot;
        }

        serializedBackground.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(backgroundView);
    }

    private static void ConfigureTrainingLocationCamera()
    {
        TrainingLocationCameraView locationCameraView =
            Object.FindFirstObjectByType<TrainingLocationCameraView>(FindObjectsInactive.Include);
        bool isNew = locationCameraView == null;
        if (isNew)
        {
            GameObject trainingSceneRoot = GameObject.Find("TrainingScene");
            Transform parent = trainingSceneRoot != null ? trainingSceneRoot.transform : null;
            var cameraObject = new GameObject("TrainingLocationCamera");
            if (parent != null)
            {
                cameraObject.transform.SetParent(parent, false);
            }

            locationCameraView = cameraObject.AddComponent<TrainingLocationCameraView>();
        }

        SerializedObject serializedCamera = new SerializedObject(locationCameraView);
        if (isNew)
        {
            ApplyTrainingOrbitSettings(serializedCamera.FindProperty("defaultView"), 0f, 20f, 15f);
            ApplyTrainingOrbitSettings(serializedCamera.FindProperty("restView"), 0f, 20f, 15f);
        }

        TrainingLocation[] locations = (TrainingLocation[])System.Enum.GetValues(typeof(TrainingLocation));
        SerializedProperty entries = serializedCamera.FindProperty("locationViews");
        int previousSize = entries.arraySize;
        entries.arraySize = locations.Length;
        for (int i = 0; i < locations.Length; i++)
        {
            TrainingLocation location = locations[i];
            SerializedProperty element = entries.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("location").enumValueIndex = (int)location;

            if (i >= previousSize)
            {
                ApplyTrainingOrbitSettings(
                    element.FindPropertyRelative("orbitSettings"),
                    i * 25f,
                    20f,
                    15f);
            }
        }

        serializedCamera.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(locationCameraView);
    }

    private static void ApplyTrainingOrbitSettings(
        SerializedProperty orbitProperty,
        float horizontalAngle,
        float verticalAngle,
        float distance,
        float focusHeightOffset = 0f,
        float focusSideOffset = 0f)
    {
        if (orbitProperty == null)
        {
            return;
        }

        orbitProperty.FindPropertyRelative("horizontalAngle").floatValue = horizontalAngle;
        orbitProperty.FindPropertyRelative("verticalAngle").floatValue = verticalAngle;
        orbitProperty.FindPropertyRelative("distance").floatValue = distance;
        orbitProperty.FindPropertyRelative("focusHeightOffset").floatValue = focusHeightOffset;
        orbitProperty.FindPropertyRelative("focusSideOffset").floatValue = focusSideOffset;
    }

    private static GameObject EnsureBackgroundSlot(Transform root, string slotName, bool active)
    {
        Transform existing = root.Find(slotName);
        GameObject slot = existing != null ? existing.gameObject : new GameObject(slotName);
        if (existing == null)
        {
            slot.transform.SetParent(root, false);
        }

        slot.SetActive(active);
        return slot;
    }


    private static void ConfigureTrainingHud(SceneUpdateMode updateMode)
    {
        TrainingHudView existing = Object.FindFirstObjectByType<TrainingHudView>(FindObjectsInactive.Include);
        if (existing != null)
        {
            WireTrainingHudReferences(existing, updateMode);
            EditorUtility.SetDirty(existing);
            return;
        }

        if (updateMode == SceneUpdateMode.WireReferences)
        {
            Debug.LogWarning("[TrainingSceneCreator] TrainingHudViewが未配置のため初期レイアウトで生成します");
        }

        var hudObject = new GameObject(
            "TrainingHudCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(CanvasGroup),
            typeof(TrainingHudView));

        Transform uiCanvasRoot = FindSceneUiCanvasParent();
        if (uiCanvasRoot != null)
        {
            hudObject.transform.SetParent(uiCanvasRoot, false);
            ConfigureCanvasHostUnderUiRoot(hudObject.GetComponent<RectTransform>());
        }

        Canvas canvas = hudObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 240;
        canvas.enabled = true;

        CanvasScaler scaler = hudObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        TrainingHudView hudView = hudObject.GetComponent<TrainingHudView>();
        Transform contentRoot = TrainingInProgressHudPrefabUtility.EnsureContentRoot(hudView, migrateExistingChildren: false);
        bool usedContentPrefab = TryInstantiateInProgressHudContentPrefab(hudView);

        SerializedObject serializedHud = new SerializedObject(hudView);
        serializedHud.FindProperty("rootCanvas").objectReferenceValue = canvas;

        if (!usedContentPrefab)
        {
            WireTrainingPanels(contentRoot, serializedHud);

            Transform headerPanel = contentRoot.Find("HudHeaderPanel");
            Transform movePowerPanel = contentRoot.Find("MovePowerPanel");
            Transform statusPanel = contentRoot.Find("StatusPanel");
            Transform logPanel = contentRoot.Find("LogPanel");

            serializedHud.FindProperty("dayText").objectReferenceValue = CreateLabel(
                headerPanel,
                "DayText",
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -16f),
                40f,
                new Vector2(280f, 44f));
            serializedHud.FindProperty("periodText").objectReferenceValue = CreateLabel(
                headerPanel,
                "PeriodText",
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -64f),
                32f,
                new Vector2(280f, 36f));
            serializedHud.FindProperty("turnText").objectReferenceValue = CreateLabel(
                headerPanel,
                "TurnText",
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -104f),
                24f,
                new Vector2(280f, 28f));
            serializedHud.FindProperty("staminaText").objectReferenceValue = CreateLabel(
                movePowerPanel,
                "StaminaText",
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(12f, -12f),
                26f,
                new Vector2(-24f, 32f));
            TMP_Text statsText = CreateStretchPanelLabel(
                statusPanel,
                "StatsText",
                24f);
            if (statsText != null)
            {
                statsText.enableWordWrapping = true;
            }

            serializedHud.FindProperty("statsText").objectReferenceValue = statsText;
            serializedHud.FindProperty("logText").objectReferenceValue = CreateLogPanelLabel(logPanel);
            serializedHud.FindProperty("continueButton").objectReferenceValue = CreateButton(contentRoot, "ContinueButton", "続ける", new Vector2(0.5f, 0f), new Vector2(0f, 24f));
            serializedHud.FindProperty("backToTitleButton").objectReferenceValue = CreateButton(contentRoot, "BackToTitleButton", "タイトルへ戻る", new Vector2(1f, 0f), new Vector2(-28f, 28f));
            serializedHud.FindProperty("interruptButton").objectReferenceValue =
                CreateButton(contentRoot, "InterruptButton", "中断して保存", new Vector2(1f, 1f), new Vector2(-28f, -28f));

            var locationButtons = new Component[3];
            locationButtons[0] = CreateButton(contentRoot, "LocationButton0", "候補1", new Vector2(0.5f, 0f), new Vector2(-296f, 292f));
            locationButtons[1] = CreateButton(contentRoot, "LocationButton1", "候補2", new Vector2(0.5f, 0f), new Vector2(0f, 292f));
            locationButtons[2] = CreateButton(contentRoot, "LocationButton2", "候補3", new Vector2(0.5f, 0f), new Vector2(296f, 292f));
            serializedHud.FindProperty("locationButtons").arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                serializedHud.FindProperty("locationButtons").GetArrayElementAtIndex(i).objectReferenceValue = locationButtons[i];
            }

            serializedHud.FindProperty("restButton").objectReferenceValue =
                CreateButton(contentRoot, "RestButton", "休憩", new Vector2(0.5f, 0f), new Vector2(0f, 380f));

            TrainingAttackSwapChoicesView attackSwapChoicesView =
                CreateAttackSwapChoicesPanelForScene(contentRoot);
            SetObjectReference(serializedHud, "attackSwapChoicesView", attackSwapChoicesView);

            serializedHud.ApplyModifiedPropertiesWithoutUndo();
        }

        WireTrainingHudReferences(hudView, updateMode);
        EditorUtility.SetDirty(hudView);
    }

    private static void WireTrainingHudReferences(
        TrainingHudView hudView,
        SceneUpdateMode updateMode = SceneUpdateMode.WireReferences)
    {
        if (hudView == null)
        {
            return;
        }

        if (updateMode == SceneUpdateMode.WireReferences)
        {
            WireTrainingHudReferencesOnly(hudView);
            return;
        }

        if (updateMode == SceneUpdateMode.RebuildLayout)
        {
            TrainingInProgressHudPrefabUtility.MigrateLegacyWholeCanvasPrefab(hudView);
        }

        // 再生成時は欠落UIのみ復元する
        TrainingInProgressHudPrefabUtility.EnsureContentRoot(hudView, migrateExistingChildren: false);
        if (!HasInProgressHudContent(hudView))
        {
            TryInstantiateInProgressHudContentPrefab(hudView);
        }

        WireTrainingHudContentReferences(hudView);
        EnsureAttackSwapChoicesPanel(hudView);
        EnsureRestButton(hudView);
        EnsureInterruptButton(hudView);
        EnsureTrainingPanels(hudView);
        EnsureResumeWindow(hudView);
        EnsureModeSelectWindow(hudView);
        EnsureAutoResultWindow(hudView);
        EnsureSelectionBackToTitleButton();
        EnsureTrainingHudCanvasHost(hudView);
    }

    private static void EnsureTrainingHudCanvasHost(TrainingHudView hudView)
    {
        if (hudView == null)
        {
            return;
        }

        // Rectが潰れているとHUD配下のウィンドウが画面隅に寄って見切れるため全面ストレッチへ補正する
        RectTransform hudRect = hudView.transform as RectTransform;
        if (hudRect != null && hudRect.parent is RectTransform)
        {
            ConfigureCanvasHostUnderUiRoot(hudRect);
            EditorUtility.SetDirty(hudRect.gameObject);
        }
        else if (hudRect != null && hudRect.localScale.sqrMagnitude < 0.001f)
        {
            hudRect.localScale = Vector3.one;
            EditorUtility.SetDirty(hudRect.gameObject);
        }

        SerializedObject serializedHud = new SerializedObject(hudView);
        SerializedProperty rootCanvasProperty = serializedHud.FindProperty("rootCanvas");
        Canvas rootCanvas = rootCanvasProperty != null
            ? rootCanvasProperty.objectReferenceValue as Canvas
            : hudView.GetComponent<Canvas>();
        if (rootCanvas == null)
        {
            rootCanvas = hudView.GetComponent<Canvas>();
            if (rootCanvasProperty != null)
            {
                rootCanvasProperty.objectReferenceValue = rootCanvas;
                serializedHud.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        CanvasGroup legacyRootGroup = hudView.GetComponent<CanvasGroup>();
        if (legacyRootGroup != null)
        {
            Object.DestroyImmediate(legacyRootGroup);
            EditorUtility.SetDirty(hudView.gameObject);
        }

        if (rootCanvas != null)
        {
            rootCanvas.enabled = true;
            GraphicRaycaster raycaster = hudView.GetComponent<GraphicRaycaster>();
            if (raycaster != null)
            {
                raycaster.enabled = true;
            }

            EditorUtility.SetDirty(rootCanvas);
        }
    }

    private static void EnsureTrainingPanels(TrainingHudView hudView)
    {
        if (hudView == null)
        {
            return;
        }

        Transform contentRoot = TrainingInProgressHudPrefabUtility.EnsureContentRoot(hudView, migrateExistingChildren: false);
        if (contentRoot == null)
        {
            return;
        }

        SerializedObject serializedHud = new SerializedObject(hudView);
        WireTrainingPanelReferences(contentRoot, serializedHud);
        serializedHud.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(hudView);
    }

    /// <summary>
    /// 育成HUDとオーバーレイウィンドウのSerializeField参照だけ配線する
    /// RectTransform・Hierarchy・UI生成は一切行わない
    /// </summary>
    public static void WireTrainingHudReferencesOnly(TrainingHudView hudView)
    {
        if (hudView == null)
        {
            return;
        }

        WireTrainingHudContentReferences(hudView);
        WireOverlayWindowsReferences(hudView);

        SerializedObject serializedHud = new SerializedObject(hudView);
        WireObjectReferenceIfFound(serializedHud, "rootCanvas", hudView.GetComponent<Canvas>());
        serializedHud.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(hudView);
    }

    /// <summary>
    /// シーン上のインラインTrainingResumeWindowをプレハブインスタンスへ差し替える
    /// 参照配線のみ行いRectTransformはプレハブ値を使う
    /// </summary>
    /// <param name="message">結果メッセージ</param>
    /// <returns>差し替え成功時true</returns>
    public static bool TryReplaceInlineTrainingResumeWindowWithPrefab(out string message)
    {
        TrainingHudView hudView = Object.FindFirstObjectByType<TrainingHudView>(FindObjectsInactive.Include);
        if (hudView == null)
        {
            message = "TrainingHudViewが見つかりません\nTrainingシーンを開いてから実行してください";
            return false;
        }

        TrainingResumeWindowView existingWindow =
            hudView.GetComponentInChildren<TrainingResumeWindowView>(true);
        if (existingWindow == null)
        {
            message = "TrainingResumeWindowViewが見つかりません\n"
                + "Tools/ClayMonsters/Wire Training Scene References を先に実行してください";
            return false;
        }

        if (PrefabUtility.IsPartOfPrefabInstance(existingWindow.gameObject))
        {
            message = "TrainingResumeWindowは既にプレハブインスタンスです\n"
                + "レイアウト調整は " + TrainingResumeWindowPrefabUtility.PrefabPath + " を編集してください";
            return false;
        }

        int siblingIndex = existingWindow.transform.GetSiblingIndex();
        Object.DestroyImmediate(existingWindow.gameObject);

        TrainingResumeWindowView prefabWindow = InstantiateResumeWindowPrefab(hudView);
        if (prefabWindow == null)
        {
            message = "プレハブのインスタンス化に失敗しました\n" + TrainingResumeWindowPrefabUtility.PrefabPath;
            return false;
        }

        prefabWindow.transform.SetSiblingIndex(siblingIndex);
        WireTrainingHudReferencesOnly(hudView);
        EditorUtility.SetDirty(hudView);
        EditorUtility.SetDirty(prefabWindow);
        EditorSceneManager.MarkSceneDirty(hudView.gameObject.scene);

        message = "TrainingResumeWindowをプレハブインスタンスへ差し替えました\n"
            + TrainingResumeWindowPrefabUtility.PrefabPath + "\n\n"
            + "プレハブ編集内容がPlay時に反映されます\n"
            + "Ctrl+Sでシーンを保存してください";
        return true;
    }

    /// <summary>
    /// 育成HUDのSerializeField参照だけ配線する
    /// RectTransformとHierarchyは変更しない
    /// </summary>
    public static void WireTrainingHudContentReferencesOnly(TrainingHudView hudView)
    {
        if (hudView == null)
        {
            return;
        }

        Transform contentRoot = TrainingInProgressHudPrefabUtility.EnsureContentRoot(hudView, migrateExistingChildren: false);
        if (contentRoot == null)
        {
            return;
        }

        SerializedObject serializedHud = new SerializedObject(hudView);
        WireTrainingPanelReferences(contentRoot, serializedHud);
        serializedHud.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(hudView);
    }

    private static bool HasInProgressHudContent(TrainingHudView hudView)
    {
        if (hudView == null)
        {
            return false;
        }

        Transform contentRoot = hudView.transform.Find(TrainingInProgressHudPrefabUtility.ContentRootName);
        if (contentRoot == null)
        {
            return false;
        }

        return PrefabUtility.IsPartOfPrefabInstance(contentRoot.gameObject) || contentRoot.childCount > 0;
    }

    private static bool TryInstantiateInProgressHudContentPrefab(TrainingHudView hudView)
    {
        if (hudView == null || HasInProgressHudContent(hudView))
        {
            return false;
        }

        GameObject prefabAsset =
            AssetDatabase.LoadAssetAtPath<GameObject>(TrainingInProgressHudPrefabUtility.PrefabPath);
        if (prefabAsset == null)
        {
            return false;
        }

        Transform legacyRoot = hudView.transform.Find(TrainingInProgressHudPrefabUtility.ContentRootName);
        if (legacyRoot != null)
        {
            Object.DestroyImmediate(legacyRoot.gameObject);
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(prefabAsset, hudView.transform) as GameObject;
        if (instance == null)
        {
            Debug.LogError(
                $"[TrainingSceneCreator] TrainingInProgressHudプレハブのインスタンス化に失敗しました: {TrainingInProgressHudPrefabUtility.PrefabPath}",
                hudView);
            return false;
        }

        instance.name = TrainingInProgressHudPrefabUtility.ContentRootName;
        StretchRect(instance.transform as RectTransform);
        instance.transform.SetAsFirstSibling();
        EditorUtility.SetDirty(hudView);
        return true;
    }

    private static void WireTrainingHudContentReferences(TrainingHudView hudView)
    {
        if (hudView == null)
        {
            return;
        }

        Transform contentRoot = TrainingInProgressHudPrefabUtility.EnsureContentRoot(hudView, migrateExistingChildren: false);
        if (contentRoot == null)
        {
            return;
        }

        SerializedObject serializedHud = new SerializedObject(hudView);
        WireObjectReferenceIfFound(serializedHud, "dayText", FindChildText(contentRoot, "DayText"));
        WireObjectReferenceIfFound(serializedHud, "periodText", FindChildText(contentRoot, "PeriodText"));
        WireObjectReferenceIfFound(serializedHud, "turnText", FindChildText(contentRoot, "TurnText"));
        WireObjectReferenceIfFound(serializedHud, "staminaText", FindChildText(contentRoot, "StaminaText"));
        WireObjectReferenceIfFound(serializedHud, "statsText", FindChildText(contentRoot, "StatsText"));
        WireObjectReferenceIfFound(serializedHud, "logText", FindChildText(contentRoot, "LogText"));
        WireObjectReferenceIfFound(serializedHud, "continueButton", FindButton(contentRoot, "ContinueButton"));
        WireObjectReferenceIfFound(serializedHud, "backToTitleButton", FindButton(contentRoot, "BackToTitleButton"));
        WireObjectReferenceIfFound(serializedHud, "interruptButton", FindButton(contentRoot, "InterruptButton"));
        WireObjectReferenceIfFound(serializedHud, "restButton", FindButton(contentRoot, "RestButton"));

        SerializedProperty locationButtonsProperty = serializedHud.FindProperty("locationButtons");
        if (locationButtonsProperty != null)
        {
            locationButtonsProperty.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                Component button = FindButton(contentRoot, "LocationButton" + i);
                if (button != null)
                {
                    locationButtonsProperty.GetArrayElementAtIndex(i).objectReferenceValue = button;
                }
            }
        }

        WireObjectReferenceIfFound(
            serializedHud,
            "staminaFill",
            FindDeepChild(contentRoot, "StaminaFill")?.GetComponent<Image>());
        WireObjectReferenceIfFound(
            serializedHud,
            "locationChoicePanelRoot",
            FindDeepChild(contentRoot, "LocationChoicePanel")?.gameObject);

        Transform attackSwapPanelTransform = FindDeepChild(contentRoot, "AttackSwapPanel");
        WireObjectReferenceIfFound(
            serializedHud,
            "attackSwapChoicesView",
            attackSwapPanelTransform?.GetComponent<TrainingAttackSwapChoicesView>());
        WireObjectReferenceIfFound(serializedHud, "attackSwapPanel", attackSwapPanelTransform?.gameObject);

        WireTrainingPanelReferences(contentRoot, serializedHud);
        serializedHud.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(hudView);
    }

    private static void WireObjectReferenceIfFound(SerializedObject serializedObject, string propertyName, Object value)
    {
        if (value == null)
        {
            return;
        }

        SetObjectReference(serializedObject, propertyName, value);
    }

    private static Component FindButton(Transform root, string name)
    {
        Transform child = FindDeepChild(root, name);
        if (child == null)
        {
            return null;
        }

        return SceneUiLhButtonUtility.FindInChildren(child)
            ?? SceneUiLhButtonUtility.GetComponent(child.gameObject);
    }

    private static void WireTrainingPanelReferences(Transform hudRoot, SerializedObject serializedHud)
    {
        if (hudRoot == null || serializedHud == null)
        {
            return;
        }

        SetObjectReference(serializedHud, "hudHeaderPanel", FindDeepChild(hudRoot, "HudHeaderPanel")?.gameObject);
        SetObjectReference(serializedHud, "movePowerPanel", FindDeepChild(hudRoot, "MovePowerPanel")?.gameObject);
        SetObjectReference(serializedHud, "statusPanel", FindDeepChild(hudRoot, "StatusPanel")?.gameObject);
        SetObjectReference(serializedHud, "logPanel", FindDeepChild(hudRoot, "LogPanel")?.gameObject);
        SetObjectReference(serializedHud, "staminaFill", FindDeepChild(hudRoot, "StaminaFill")?.GetComponent<Image>());
    }

    private static void WireTrainingPanels(Transform hudRoot, SerializedObject serializedHud)
    {
        EnsureTrainingPanelHosts(hudRoot, serializedHud);
    }

    private static void EnsureTrainingPanelHosts(Transform hudRoot)
    {
        EnsureTrainingPanelHosts(hudRoot, null);
    }

    private static void EnsureTrainingPanelHosts(Transform hudRoot, SerializedObject serializedHud)
    {
        if (hudRoot == null)
        {
            return;
        }

        Image headerPanel = EnsurePanelImage(
            hudRoot,
            "HudHeaderPanel",
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(24f, -24f),
            new Vector2(300f, 300f));

        Image movePowerPanel = EnsurePanelImage(
            hudRoot,
            "MovePowerPanel",
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(24f, -334f),
            new Vector2(300f, 150f));

        Image statusPanel = EnsurePanelImage(
            hudRoot,
            "StatusPanel",
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(24f, -508f),
            new Vector2(300f, 360f));

        Image logPanel = EnsurePanelImage(
            hudRoot,
            "LogPanel",
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 96f),
            new Vector2(1040f, 180f));

        Transform movePowerParent = movePowerPanel != null ? movePowerPanel.transform : hudRoot;
        Image staminaTrack = EnsureStretchPanelImage(
            movePowerParent,
            "StaminaTrack",
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(24f, 16f),
            new Vector2(-24f, 40f));

        Transform trackParent = staminaTrack != null ? staminaTrack.transform : movePowerParent;
        bool createdStaminaFill = trackParent != null && trackParent.Find("StaminaFill") == null;
        Image staminaFill = EnsurePanelImage(
            trackParent,
            "StaminaFill",
            Vector2.zero,
            Vector2.one,
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero);
        if (createdStaminaFill && staminaFill != null)
        {
            RectTransform fillRect = staminaFill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(4f, 4f);
            fillRect.offsetMax = new Vector2(-4f, -4f);
            staminaFill.type = Image.Type.Filled;
            staminaFill.fillMethod = Image.FillMethod.Horizontal;
            staminaFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        }

        if (serializedHud == null)
        {
            return;
        }

        SetObjectReference(serializedHud, "hudHeaderPanel", EnsurePanelRoot(headerPanel));
        SetObjectReference(serializedHud, "movePowerPanel", EnsurePanelRoot(movePowerPanel));
        SetObjectReference(serializedHud, "statusPanel", EnsurePanelRoot(statusPanel));
        SetObjectReference(serializedHud, "logPanel", EnsurePanelRoot(logPanel));
        SetObjectReference(serializedHud, "staminaFill", staminaFill);
    }

    private static GameObject EnsurePanelRoot(Image panel)
    {
        if (panel == null)
        {
            return null;
        }

        RemoveLegacyPanelCanvas(panel.gameObject);
        RemoveVisibilityCanvasGroup(panel.gameObject);
        return panel.gameObject;
    }

    private static CanvasGroup EnsurePanelGroup(Image panel)
    {
        GameObject panelRoot = EnsurePanelRoot(panel);
        return panelRoot != null ? panelRoot.GetComponent<CanvasGroup>() : null;
    }

    private static void RemoveLegacyPanelCanvas(GameObject panelObject)
    {
        if (panelObject == null)
        {
            return;
        }

        DestroyComponentIfRemovable(panelObject.GetComponent<CanvasScaler>());
        DestroyComponentIfRemovable(panelObject.GetComponent<GraphicRaycaster>());
        DestroyComponentIfRemovable(panelObject.GetComponent<Canvas>());
    }

    private static void DestroyComponentIfRemovable(Component component)
    {
        if (component == null)
        {
            return;
        }

        if (PrefabUtility.IsPartOfPrefabInstance(component)
            && !PrefabUtility.IsAddedComponentOverride(component))
        {
            Debug.LogWarning(
                $"[TrainingSceneCreator] プレハブインスタンス上の{component.GetType().Name}は削除できません プレハブ側で削除してください",
                component);
            return;
        }

        Object.DestroyImmediate(component);
    }

    private static bool IsAliveUnityObject(Object unityObject)
    {
        return unityObject != null;
    }

    private static CanvasGroup EnsureCanvasGroupOn(GameObject host, bool hidden)
    {
        if (host == null)
        {
            return null;
        }

        CanvasGroup group = host.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = host.AddComponent<CanvasGroup>();
        }

        SetPanelGroupVisible(group, !hidden);
        return group;
    }

    private static void MigrateTrainingNestedCanvasesToGroups()
    {
        LoadSlotView[] loadSlotViews =
            Object.FindObjectsByType<LoadSlotView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < loadSlotViews.Length; i++)
        {
            MigrateLoadSlotConfirmPanel(loadSlotViews[i]);
        }

        MigrateTrainedSaveConfirmPanel();
        MigrateTrainingOverlayWindowCanvases();
    }

    private static void MigrateLoadSlotConfirmPanel(LoadSlotView loadSlotView)
    {
        if (loadSlotView == null)
        {
            return;
        }

        Transform loadSlotRoot = loadSlotView.transform;
        Transform legacyConfirm = loadSlotRoot.Find("ConfirmSaveSlotCanvas");
        if (legacyConfirm == null)
        {
            legacyConfirm = loadSlotRoot.parent != null
                ? loadSlotRoot.parent.Find("ConfirmSaveSlotCanvas")
                : null;
        }

        if (legacyConfirm == null)
        {
            legacyConfirm = FindSceneTransformByName("ConfirmSaveSlotCanvas");
        }

        if (legacyConfirm != null)
        {
            legacyConfirm.name = "ConfirmPanel";
            if (legacyConfirm.parent != loadSlotRoot)
            {
                legacyConfirm.SetParent(loadSlotRoot, false);
            }

            RemoveLegacyPanelCanvas(legacyConfirm.gameObject);
            ConfigureCanvasHostUnderUiRoot(legacyConfirm as RectTransform);
        }

        WireLoadSlotReferences(loadSlotView, CaptureLoadSlotPersistedRefs());
    }

    private static void MigrateTrainedSaveConfirmPanel()
    {
        TrainingTrainedSaveView trainedSaveView =
            Object.FindFirstObjectByType<TrainingTrainedSaveView>(FindObjectsInactive.Include);
        if (trainedSaveView == null)
        {
            return;
        }

        Transform trainedSaveRoot = trainedSaveView.transform;
        Transform legacyConfirm = trainedSaveRoot.Find("TrainingTrainedSaveConfirmCanvas");
        if (legacyConfirm == null)
        {
            legacyConfirm = FindSceneTransformByName("TrainingTrainedSaveConfirmCanvas");
        }

        if (legacyConfirm != null)
        {
            legacyConfirm.name = "ConfirmPanel";
            if (legacyConfirm.parent != trainedSaveRoot)
            {
                legacyConfirm.SetParent(trainedSaveRoot, false);
            }

            RemoveLegacyPanelCanvas(legacyConfirm.gameObject);
            ConfigureCanvasHostUnderUiRoot(legacyConfirm as RectTransform);
        }

        ConfigureTrainingTrainedSave(SceneUpdateMode.WireReferences);
    }

    private static void MigrateTrainingOverlayWindowCanvases()
    {
        TrainingResumeWindowView[] resumeWindows =
            Object.FindObjectsByType<TrainingResumeWindowView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < resumeWindows.Length; i++)
        {
            EnsureOverlayWindowGroup(resumeWindows[i]);
        }

        TrainingModeSelectView[] modeSelectViews =
            Object.FindObjectsByType<TrainingModeSelectView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < modeSelectViews.Length; i++)
        {
            EnsureOverlayWindowGroup(modeSelectViews[i]);
        }

        TrainingAutoResultView[] autoResultViews =
            Object.FindObjectsByType<TrainingAutoResultView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < autoResultViews.Length; i++)
        {
            EnsureOverlayWindowGroup(autoResultViews[i]);
        }
    }

    private static void SetPanelGroupVisible(CanvasGroup group, bool visible)
    {
        if (group == null)
        {
            return;
        }

        group.gameObject.SetActive(visible);
    }

    private static void SetCanvasHostHidden(GameObject host, bool hidden)
    {
        if (host == null)
        {
            return;
        }

        Canvas canvas = host.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.enabled = !hidden;
            GraphicRaycaster raycaster = host.GetComponent<GraphicRaycaster>();
            if (raycaster != null)
            {
                raycaster.enabled = !hidden;
            }

            return;
        }

        host.SetActive(!hidden);
    }

    private static void SetPanelHostHidden(GameObject panel, bool hidden)
    {
        if (panel == null)
        {
            return;
        }

        panel.SetActive(!hidden);
    }

    private static void RemoveVisibilityCanvasGroup(GameObject host)
    {
        if (host == null)
        {
            return;
        }

        DestroyComponentIfRemovable(host.GetComponent<CanvasGroup>());
    }

    private static void ApplyTrainingPanelVisual(Image image, string objectName)
    {
        if (image == null)
        {
            return;
        }

        switch (objectName)
        {
            case "StaminaTrack":
                ClayEditUiVisualUtility.ApplyTrainingStaminaTrackForEditorBake(image);
                break;
            case "StaminaFill":
                ClayEditUiVisualUtility.ApplyTrainingStaminaFillForEditorBake(image);
                break;
            default:
                TitleClayUiVisualUtility.ApplyPanelForEditorBake(image);
                break;
        }
    }

    private static Image EnsurePanelImage(
        Transform parent,
        string objectName,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        if (parent == null)
        {
            return null;
        }

        Transform found = parent.Find(objectName);
        Image image = found != null ? found.GetComponent<Image>() : null;
        if (image != null)
        {
            return image;
        }

        var panelObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(parent, false);
        image = panelObject.GetComponent<Image>();

        RectTransform rect = image.rectTransform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        ApplyTrainingPanelVisual(image, objectName);
        image.raycastTarget = false;
        image.transform.SetAsFirstSibling();
        return image;
    }

    private static Image EnsureStretchPanelImage(
        Transform parent,
        string objectName,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        if (parent == null)
        {
            return null;
        }

        Transform found = parent.Find(objectName);
        Image image = found != null ? found.GetComponent<Image>() : null;
        if (image != null)
        {
            return image;
        }

        var panelObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(parent, false);
        image = panelObject.GetComponent<Image>();

        RectTransform rect = image.rectTransform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;

        ApplyTrainingPanelVisual(image, objectName);
        image.raycastTarget = false;
        image.transform.SetAsFirstSibling();
        return image;
    }

    private static void EnsureRestButton(TrainingHudView hudView)
    {
        SerializedObject serializedHud = new SerializedObject(hudView);
        SerializedProperty property = serializedHud.FindProperty("restButton");
        if (property != null && property.objectReferenceValue != null)
        {
            return;
        }

        Transform contentRoot = TrainingInProgressHudPrefabUtility.EnsureContentRoot(hudView, migrateExistingChildren: false);
        Component button = CreateButton(
            contentRoot,
            "RestButton",
            "休憩",
            new Vector2(0.5f, 0f),
            new Vector2(0f, 340f));
        property.objectReferenceValue = button;
        serializedHud.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(hudView);
    }

    private static void ConfigureTrainingTrainedSave(SceneUpdateMode updateMode)
    {
        TrainingTrainedSaveView trainedSaveView =
            Object.FindFirstObjectByType<TrainingTrainedSaveView>(FindObjectsInactive.Include);
        bool createdView = trainedSaveView == null;
        if (createdView)
        {
            // 配線時でも欠落していれば初期レイアウトで復元する
            var createdCanvasObject = new GameObject(
                "TrainingTrainedSaveCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(TrainingTrainedSaveView));
            Transform uiCanvasRoot = FindSceneUiCanvasParent();
            if (uiCanvasRoot != null)
            {
                createdCanvasObject.transform.SetParent(uiCanvasRoot, false);
                ConfigureCanvasHostUnderUiRoot(createdCanvasObject.GetComponent<RectTransform>());
            }

            trainedSaveView = createdCanvasObject.GetComponent<TrainingTrainedSaveView>();
        }

        GameObject canvasObject = trainedSaveView.gameObject;
        bool rebuildLayout = updateMode == SceneUpdateMode.RebuildLayout || createdView;

        EnsureTrainedSaveSelectionCanvas(canvasObject, applyLayout: rebuildLayout);
        Canvas selectionCanvas = canvasObject.GetComponent<Canvas>();
        TMP_Text headerText = EnsureTrainedSaveHeader(selectionCanvas.transform);
        ModelSaveSlotScrollListView slotScrollList =
            EnsureTrainedSaveSlotScrollList(selectionCanvas.transform);
        ModelSaveSlotScrollListView.EnsureSelectionBackground(selectionCanvas);

        GameObject confirmPanelRoot = EnsureTrainedSaveConfirmPanel(canvasObject.transform, applyLayout: rebuildLayout);
        ModelSaveConfirmView confirmView = ModelSaveSlotUiPrefabUtility.EnsureConfirmViewOnRoot(confirmPanelRoot.transform);

        ModelSaveSlotScrollListView.EnsureSelectionBackground(confirmPanelRoot.transform);
        TMP_Text confirmMessageText = EnsureTrainedSaveConfirmMessage(confirmPanelRoot.transform);
        Component saveButton = EnsureTrainedSaveConfirmButton(
            confirmPanelRoot.transform,
            "SaveButton",
            "保存する",
            -190f,
            applyLayout: rebuildLayout);
        Component backButton = EnsureTrainedSaveConfirmButton(
            confirmPanelRoot.transform,
            "BackButton",
            "戻る",
            190f,
            applyLayout: rebuildLayout);
        Component backToTitleButton = EnsureTrainedSaveSelectionBackToTitleButton(selectionCanvas.transform);

        SerializedObject serializedTrainedSave = new SerializedObject(trainedSaveView);
        serializedTrainedSave.FindProperty("rootCanvas").objectReferenceValue = selectionCanvas;
        SetCanvasHostHidden(canvasObject, hidden: true);
        RemoveVisibilityCanvasGroup(canvasObject);
        serializedTrainedSave.FindProperty("headerText").objectReferenceValue = headerText;
        serializedTrainedSave.FindProperty("slotScrollList").objectReferenceValue = slotScrollList;
        serializedTrainedSave.FindProperty("confirmPanelRoot").objectReferenceValue = confirmPanelRoot;
        serializedTrainedSave.FindProperty("confirmView").objectReferenceValue = confirmView;
        serializedTrainedSave.FindProperty("confirmMessageText").objectReferenceValue = confirmMessageText;
        serializedTrainedSave.FindProperty("saveButton").objectReferenceValue = saveButton;
        serializedTrainedSave.FindProperty("backButton").objectReferenceValue = backButton;
        serializedTrainedSave.FindProperty("backToTitleButton").objectReferenceValue = backToTitleButton;
        serializedTrainedSave.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(trainedSaveView);
        canvasObject.SetActive(false);
    }

    private const int TrainedSaveSelectionSortingOrder = 520;
    private const int TrainedSaveConfirmSortingOrder = 530;

    private static void EnsureTrainedSaveSelectionCanvas(GameObject canvasObject, bool applyLayout)
    {
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = canvasObject.AddComponent<Canvas>();
            canvasObject.AddComponent<GraphicRaycaster>();
            applyLayout = true;
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = TrainedSaveSelectionSortingOrder;
        canvas.enabled = true;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = canvasObject.AddComponent<CanvasScaler>();
            applyLayout = true;
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        if (!applyLayout)
        {
            return;
        }

        RectTransform rect = canvasObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static TMP_Text EnsureTrainedSaveHeader(Transform selectionRoot)
    {
        Transform existing = selectionRoot.Find("HeaderText");
        if (existing != null)
        {
            return existing.GetComponent<TMP_Text>();
        }

        return CreateLabel(
            selectionRoot,
            "HeaderText",
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -8f),
            34f);
    }

    private static TMP_Text EnsureTrainedSaveConfirmMessage(Transform confirmRoot)
    {
        Transform existing = confirmRoot.Find("ConfirmMessageText");
        if (existing != null)
        {
            return existing.GetComponent<TMP_Text>();
        }

        TMP_Text label = CreateLabel(
            confirmRoot,
            "ConfirmMessageText",
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -24f),
            30f);

        return label;
    }

    private static ModelSaveSlotScrollListView EnsureTrainedSaveSlotScrollList(Transform selectionRoot)
    {
        return ModelSaveSlotUiPrefabUtility.EnsureScrollListPrefabInstance(selectionRoot);
    }

    private static GameObject EnsureTrainedSaveConfirmPanel(Transform trainedSaveRoot, bool applyLayout)
    {
        Transform existing = trainedSaveRoot.Find("ConfirmPanel");
        if (existing == null)
        {
            existing = trainedSaveRoot.Find("TrainingTrainedSaveConfirmCanvas");
        }

        GameObject confirmObject;
        if (existing != null)
        {
            confirmObject = existing.gameObject;
            confirmObject.name = "ConfirmPanel";
        }
        else
        {
            confirmObject = new GameObject("ConfirmPanel", typeof(RectTransform));
            confirmObject.transform.SetParent(trainedSaveRoot, false);
            applyLayout = true;
        }

        if (confirmObject.transform.parent != trainedSaveRoot)
        {
            confirmObject.transform.SetParent(trainedSaveRoot, false);
            applyLayout = true;
        }

        RemoveLegacyPanelCanvas(confirmObject);
        RemoveVisibilityCanvasGroup(confirmObject);

        if (applyLayout)
        {
            ConfigureCanvasHostUnderUiRoot(confirmObject.GetComponent<RectTransform>());
        }

        SetPanelHostHidden(confirmObject, hidden: true);
        return confirmObject;
    }

    private static Component EnsureTrainedSaveConfirmButton(
        Transform parent,
        string objectName,
        string label,
        float horizontalOffset,
        bool applyLayout)
    {
        Transform existing = parent.Find(objectName);
        if (existing != null)
        {
            Component existingButton = SceneUiLhButtonUtility.GetComponent(existing.gameObject);
            if (existingButton != null)
            {
                return existingButton;
            }
        }

        Component button = CreateButton(parent, objectName, label, new Vector2(0.5f, 0f), new Vector2(horizontalOffset, 72f));
        RectTransform rect = button != null ? button.transform as RectTransform : null;
        if (rect != null && applyLayout)
        {
            rect.sizeDelta = new Vector2(340f, 80f);
        }

        return button;
    }

    private static Component EnsureTrainedSaveSelectionBackToTitleButton(Transform selectionRoot)
    {
        Transform existing = selectionRoot.Find("BackToTitleButton");
        if (existing != null)
        {
            Component existingButton = SceneUiLhButtonUtility.GetComponent(existing.gameObject);
            if (existingButton != null)
            {
                return existingButton;
            }
        }

        Component button = CreateButton(
            selectionRoot,
            "BackToTitleButton",
            "タイトルへ戻る",
            new Vector2(1f, 0f),
            new Vector2(-28f, 28f));
        RectTransform rect = button != null ? button.transform as RectTransform : null;
        if (rect != null)
        {
            rect.sizeDelta = new Vector2(280f, 72f);
        }

        return button;
    }

    private static void EnsureInterruptButton(TrainingHudView hudView)
    {
        SerializedObject serializedHud = new SerializedObject(hudView);
        SerializedProperty property = serializedHud.FindProperty("interruptButton");
        if (property != null && property.objectReferenceValue != null)
        {
            return;
        }

        Transform contentRoot = TrainingInProgressHudPrefabUtility.EnsureContentRoot(hudView, migrateExistingChildren: false);
        Component button = CreateButton(
            contentRoot,
            "InterruptButton",
            "中断して保存",
            new Vector2(1f, 1f),
            new Vector2(-28f, -28f));
        property.objectReferenceValue = button;
        serializedHud.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(hudView);
    }

    private static void EnsureResumeWindow(TrainingHudView hudView, bool createIfMissing = true)
    {
        if (hudView == null)
        {
            return;
        }

        TrainingResumeWindowView existingWindow =
            hudView.GetComponentInChildren<TrainingResumeWindowView>(true);
        if (existingWindow != null)
        {
            WireResumeWindow(hudView, existingWindow);
            WireResumeWindowContentReferences(existingWindow);
            DisableLegacyResumeButtons(hudView.transform, existingWindow.transform);
            EditorUtility.SetDirty(existingWindow);
            EditorUtility.SetDirty(hudView);
            return;
        }

        if (!createIfMissing)
        {
            return;
        }

        TrainingResumeWindowView prefabWindow = InstantiateResumeWindowPrefab(hudView);
        if (prefabWindow != null)
        {
            WireResumeWindow(hudView, prefabWindow);
            DisableLegacyResumeButtons(hudView.transform, prefabWindow.transform);
            EditorUtility.SetDirty(prefabWindow);
            EditorUtility.SetDirty(hudView);
            return;
        }

        var windowObject = new GameObject(
            "TrainingResumeWindow",
            typeof(RectTransform),
            typeof(TrainingResumeWindowView));
        windowObject.transform.SetParent(hudView.transform, false);
        RectTransform windowRect = windowObject.GetComponent<RectTransform>();
        windowRect.anchorMin = Vector2.zero;
        windowRect.anchorMax = Vector2.one;
        windowRect.offsetMin = Vector2.zero;
        windowRect.offsetMax = Vector2.zero;

        SetPanelHostHidden(windowObject, hidden: true);

        Image blocker = CreateStretchImage(windowObject.transform, "Blocker");
        Image panel = EnsurePanelImage(
            windowObject.transform,
            "WindowPanel",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(960f, 760f));

        blocker.transform.SetAsFirstSibling();
        if (panel != null)
        {
            panel.transform.SetAsLastSibling();
        }

        Transform panelParent = panel != null ? panel.transform : windowObject.transform;
        TMP_Text titleText = CreateResumeLabel(panelParent, "TitleText", new Vector2(0.5f, 1f), new Vector2(0f, -28f), 34f, TextAlignmentOptions.Center);
        titleText.text = "育成途中のデータがあります";

        TMP_Text modelNameText = CreateResumeLabel(panelParent, "ModelNameText", new Vector2(0.5f, 1f), new Vector2(0f, -84f), 28f, TextAlignmentOptions.Center);
        Image thumbnailImage = EnsureResumeThumbnailImage(panelParent);
        TMP_Text progressText = CreateResumeLabel(panelParent, "ProgressText", new Vector2(0f, 1f), new Vector2(260f, -140f), 24f, TextAlignmentOptions.TopLeft);
        TMP_Text staminaText = CreateResumeLabel(panelParent, "StaminaText", new Vector2(0f, 1f), new Vector2(260f, -200f), 24f, TextAlignmentOptions.TopLeft);
        TMP_Text statsText = CreateResumeLabel(panelParent, "StatsText", new Vector2(0f, 1f), new Vector2(32f, -360f), 22f, TextAlignmentOptions.TopLeft);
        TrainingResumeAttacksContentView attacksPanel = EnsureAttacksPanel(
            panelParent,
            new Vector2(290f, -130f),
            new Vector2(620f, 500f),
            showHeader: false);
        ConfigureResumeBodyLabel(progressText, new Vector2(660f, 60f));
        ConfigureResumeBodyLabel(staminaText, new Vector2(660f, 40f));
        ConfigureResumeBodyLabel(statsText, new Vector2(240f, 220f));

        Component restartButton = CreateButton(
            panelParent,
            "ResumeRestartButton",
            "最初から育成",
            new Vector2(0.5f, 0f),
            new Vector2(-170f, 28f));
        Component continueButton = CreateButton(
            panelParent,
            "ResumeContinueButton",
            "続きから育成",
            new Vector2(0.5f, 0f),
            new Vector2(170f, 28f));

        TrainingResumeWindowView windowView = windowObject.GetComponent<TrainingResumeWindowView>();
        SerializedObject serializedWindow = new SerializedObject(windowView);
        SetObjectReference(serializedWindow, "windowRoot", windowObject);
        SetObjectReference(serializedWindow, "blocker", blocker);
        SetObjectReference(serializedWindow, "thumbnailImage", thumbnailImage);
        SetObjectReference(serializedWindow, "titleText", titleText);
        SetObjectReference(serializedWindow, "modelNameText", modelNameText);
        SetObjectReference(serializedWindow, "progressText", progressText);
        SetObjectReference(serializedWindow, "staminaText", staminaText);
        SetObjectReference(serializedWindow, "statsText", statsText);
        SetObjectReference(serializedWindow, "attacksPanel", attacksPanel);
        SetObjectReference(serializedWindow, "restartButton", restartButton);
        SetObjectReference(serializedWindow, "continueButton", continueButton);
        serializedWindow.ApplyModifiedPropertiesWithoutUndo();

        WireResumeWindow(hudView, windowView);
        DisableLegacyResumeButtons(hudView.transform, windowObject.transform);
        EditorUtility.SetDirty(windowView);
        EditorUtility.SetDirty(hudView);
    }

    private static TrainingResumeWindowView InstantiateResumeWindowPrefab(TrainingHudView hudView)
    {
        GameObject prefabAsset =
            AssetDatabase.LoadAssetAtPath<GameObject>(TrainingResumeWindowPrefabUtility.PrefabPath);
        if (prefabAsset == null)
        {
            return null;
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(prefabAsset, hudView.transform) as GameObject;
        if (instance == null)
        {
            Debug.LogError(
                $"[TrainingSceneCreator] TrainingResumeWindowプレハブのインスタンス化に失敗しました: {TrainingResumeWindowPrefabUtility.PrefabPath}",
                hudView);
            return null;
        }

        instance.SetActive(false);
        return instance.GetComponent<TrainingResumeWindowView>();
    }

    private static TrainingAutoResultView InstantiateAutoResultWindowPrefab(TrainingHudView hudView)
    {
        GameObject prefabAsset =
            AssetDatabase.LoadAssetAtPath<GameObject>(TrainingAutoResultWindowPrefabUtility.PrefabPath);
        if (prefabAsset == null)
        {
            return null;
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(prefabAsset, hudView.transform) as GameObject;
        if (instance == null)
        {
            Debug.LogError(
                $"[TrainingSceneCreator] TrainingAutoResultWindowプレハブのインスタンス化に失敗しました: {TrainingAutoResultWindowPrefabUtility.PrefabPath}",
                hudView);
            return null;
        }

        instance.SetActive(false);
        return instance.GetComponent<TrainingAutoResultView>();
    }

    private static void EnsureResumeThumbnail(TrainingResumeWindowView windowView)
    {
        if (windowView == null)
        {
            return;
        }

        Transform panel = windowView.transform.Find("WindowPanel");
        if (panel == null)
        {
            return;
        }

        Image thumbnailImage = panel.Find("ThumbnailFrame/ThumbnailImage")?.GetComponent<Image>();
        if (thumbnailImage == null)
        {
            thumbnailImage = panel.Find("ThumbnailImage")?.GetComponent<Image>();
        }

        if (thumbnailImage == null)
        {
            thumbnailImage = TrainingModalUiBuilder.CreateResumeThumbnailImage(panel);
        }

        SerializedObject serializedWindow = new SerializedObject(windowView);
        SetObjectReference(serializedWindow, "thumbnailImage", thumbnailImage);
        serializedWindow.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Image EnsureResumeThumbnailImage(Transform panelParent)
    {
        Image existing = panelParent.Find("ThumbnailImage")?.GetComponent<Image>();
        if (existing != null)
        {
            return existing;
        }

        return TrainingModalUiBuilder.CreateResumeThumbnailImage(panelParent);
    }

    private static void WireResumeWindow(TrainingHudView hudView, TrainingResumeWindowView windowView)
    {
        SerializedObject serializedHud = new SerializedObject(hudView);
        SetObjectReference(serializedHud, "resumeWindowView", windowView);
        serializedHud.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void WireOverlayWindowsReferences(TrainingHudView hudView)
    {
        if (hudView == null)
        {
            return;
        }

        TrainingResumeWindowView resumeWindow =
            hudView.GetComponentInChildren<TrainingResumeWindowView>(true);
        if (resumeWindow != null)
        {
            WireResumeWindow(hudView, resumeWindow);
            WireResumeWindowContentReferences(resumeWindow);
            DisableLegacyResumeButtons(hudView.transform, resumeWindow.transform);
            EditorUtility.SetDirty(resumeWindow);
        }
    }

    private static void WireResumeWindowContentReferences(TrainingResumeWindowView windowView)
    {
        if (windowView == null)
        {
            return;
        }

        Transform root = windowView.transform;
        Transform panel = FindResumeWindowPanel(root);
        SerializedObject serializedWindow = new SerializedObject(windowView);
        SetObjectReference(serializedWindow, "windowRoot", windowView.gameObject);
        WireObjectReferenceIfFound(serializedWindow, "blocker", FindDeepChild(root, "Blocker")?.GetComponent<Image>());

        Transform thumbnailFrame = panel != null ? panel.Find("ThumbnailFrame") : null;
        Image thumbnailImage = thumbnailFrame != null
            ? FindDeepChild(thumbnailFrame, "ThumbnailImage")?.GetComponent<Image>()
            : FindDeepChild(panel, "ThumbnailImage")?.GetComponent<Image>();
        WireObjectReferenceIfFound(serializedWindow, "thumbnailImage", thumbnailImage);
        WireObjectReferenceIfFound(serializedWindow, "titleText", FindChildText(panel, "TitleText"));
        WireObjectReferenceIfFound(serializedWindow, "modelNameText", FindChildText(panel, "ModelNameText"));
        WireObjectReferenceIfFound(serializedWindow, "progressText", FindChildText(panel, "ProgressText"));
        WireObjectReferenceIfFound(serializedWindow, "staminaText", FindChildText(panel, "StaminaText"));
        WireObjectReferenceIfFound(serializedWindow, "statsText", FindChildText(panel, "StatsText"));
        WireObjectReferenceIfFound(serializedWindow, "continueButton", FindButton(root, "ResumeContinueButton"));
        WireObjectReferenceIfFound(serializedWindow, "restartButton", FindButton(root, "ResumeRestartButton"));

        Transform attacksPanelTransform = panel != null ? panel.Find("AttacksPanel") : null;
        TrainingResumeAttacksContentView attacksPanel =
            attacksPanelTransform != null
                ? attacksPanelTransform.GetComponent<TrainingResumeAttacksContentView>()
                : null;
        if (attacksPanel != null)
        {
            WireAttacksPanelReferences(attacksPanel);
            WireObjectReferenceIfFound(serializedWindow, "attacksPanel", attacksPanel);
        }

        serializedWindow.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(windowView);
    }

    private static Transform FindResumeWindowPanel(Transform windowRoot)
    {
        if (windowRoot == null)
        {
            return null;
        }

        Transform panel = windowRoot.Find("WindowPanel");
        return panel != null ? panel : windowRoot;
    }

    private static void EnsureModeSelectWindow(TrainingHudView hudView, bool createIfMissing = true)
    {
        if (hudView == null)
        {
            return;
        }

        TrainingModeSelectView existingWindow =
            hudView.GetComponentInChildren<TrainingModeSelectView>(true);
        if (existingWindow != null)
        {
            EnsureOverlayWindowGroup(existingWindow);
            EditorUtility.SetDirty(existingWindow);
            return;
        }

        if (!createIfMissing)
        {
            return;
        }

        var windowObject = new GameObject(
            "TrainingModeSelectWindow",
            typeof(RectTransform),
            typeof(TrainingModeSelectView));
        windowObject.transform.SetParent(hudView.transform, false);
        StretchRect(windowObject.GetComponent<RectTransform>());

        SetPanelHostHidden(windowObject, hidden: true);

        Image blocker = CreateStretchImage(windowObject.transform, "Blocker");
        Image panel = EnsurePanelImage(
            windowObject.transform,
            "WindowPanel",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(760f, 420f));
        blocker.transform.SetAsFirstSibling();
        panel?.transform.SetAsLastSibling();

        Transform panelParent = panel != null ? panel.transform : windowObject.transform;
        TMP_Text titleText = CreateResumeLabel(panelParent, "TitleText", new Vector2(0.5f, 1f), new Vector2(0f, -28f), 34f, TextAlignmentOptions.Center);
        TMP_Text modelNameText = CreateResumeLabel(panelParent, "ModelNameText", new Vector2(0.5f, 1f), new Vector2(0f, -84f), 28f, TextAlignmentOptions.Center);
        TMP_Text descriptionText = CreateResumeLabel(panelParent, "DescriptionText", new Vector2(0.5f, 1f), new Vector2(0f, -150f), 22f, TextAlignmentOptions.Center);
        ConfigureResumeBodyLabel(descriptionText, new Vector2(640f, 80f));

        Component manualButton = CreateButton(
            panelParent,
            "ManualTrainingButton",
            "じっくり育成",
            new Vector2(0.5f, 0f),
            new Vector2(-170f, 28f));
        Component autoButton = CreateButton(
            panelParent,
            "AutoTrainingButton",
            "自動育成",
            new Vector2(0.5f, 0f),
            new Vector2(170f, 28f));

        TrainingModeSelectView windowView = windowObject.GetComponent<TrainingModeSelectView>();
        SerializedObject serializedWindow = new SerializedObject(windowView);
        SetObjectReference(serializedWindow, "windowRoot", windowObject);
        SetObjectReference(serializedWindow, "blocker", blocker);
        SetObjectReference(serializedWindow, "titleText", titleText);
        SetObjectReference(serializedWindow, "modelNameText", modelNameText);
        SetObjectReference(serializedWindow, "descriptionText", descriptionText);
        SetObjectReference(serializedWindow, "manualButton", manualButton);
        SetObjectReference(serializedWindow, "autoButton", autoButton);
        serializedWindow.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(windowView);
    }

    private static void EnsureAutoResultWindow(TrainingHudView hudView, bool createIfMissing = true)
    {
        if (hudView == null)
        {
            return;
        }

        TrainingAutoResultView existingWindow =
            hudView.GetComponentInChildren<TrainingAutoResultView>(true);
        if (existingWindow != null)
        {
            EnsureOverlayWindowGroup(existingWindow);
            EnsureAutoResultThumbnail(existingWindow);
            EnsureAutoResultAttacksPanel(existingWindow);
            EditorUtility.SetDirty(existingWindow);
            return;
        }

        if (!createIfMissing)
        {
            return;
        }

        TrainingAutoResultView prefabWindow = InstantiateAutoResultWindowPrefab(hudView);
        if (prefabWindow != null)
        {
            EnsureOverlayWindowGroup(prefabWindow);
            EnsureAutoResultThumbnail(prefabWindow);
            EnsureAutoResultAttacksPanel(prefabWindow);
            EditorUtility.SetDirty(prefabWindow);
            return;
        }

        var windowObject = new GameObject(
            "TrainingAutoResultWindow",
            typeof(RectTransform),
            typeof(TrainingAutoResultView));
        windowObject.transform.SetParent(hudView.transform, false);
        StretchRect(windowObject.GetComponent<RectTransform>());

        SetPanelHostHidden(windowObject, hidden: true);

        Image blocker = CreateStretchImage(windowObject.transform, "Blocker");
        Image panel = EnsurePanelImage(
            windowObject.transform,
            "WindowPanel",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(960f, 760f));
        blocker.transform.SetAsFirstSibling();
        panel?.transform.SetAsLastSibling();

        Transform panelParent = panel != null ? panel.transform : windowObject.transform;
        TMP_Text titleText = CreateResumeLabel(panelParent, "TitleText", new Vector2(0.5f, 1f), new Vector2(0f, -28f), 34f, TextAlignmentOptions.Center);
        titleText.text = "育成完了";
        TMP_Text modelNameText = CreateResumeLabel(panelParent, "ModelNameText", new Vector2(0.5f, 1f), new Vector2(0f, -84f), 28f, TextAlignmentOptions.Center);
        Image thumbnailImage = EnsureAutoResultThumbnailImage(panelParent);
        TMP_Text statsText = CreateResumeLabel(panelParent, "StatsText", new Vector2(0f, 1f), new Vector2(32f, -360f), 22f, TextAlignmentOptions.TopLeft);
        TrainingResumeAttacksContentView attacksPanel = EnsureAttacksPanel(
            panelParent,
            new Vector2(470f, -140f),
            new Vector2(440f, 480f));
        TMP_Text saveResultText = CreateResumeLabel(panelParent, "SaveResultText", new Vector2(0.5f, 0f), new Vector2(0f, 96f), 20f, TextAlignmentOptions.Center);
        ConfigureResumeBodyLabel(statsText, new Vector2(240f, 220f));
        ConfigureResumeBodyLabel(saveResultText, new Vector2(860f, 80f));

        Component backToTitleButton = CreateButton(
            panelParent,
            "BackToTitleButton",
            "保存先を選ぶ",
            new Vector2(0.5f, 0f),
            new Vector2(0f, 28f));

        TrainingAutoResultView windowView = windowObject.GetComponent<TrainingAutoResultView>();
        SerializedObject serializedWindow = new SerializedObject(windowView);
        SetObjectReference(serializedWindow, "windowRoot", windowObject);
        SetObjectReference(serializedWindow, "blocker", blocker);
        SetObjectReference(serializedWindow, "thumbnailImage", thumbnailImage);
        SetObjectReference(serializedWindow, "titleText", titleText);
        SetObjectReference(serializedWindow, "modelNameText", modelNameText);
        SetObjectReference(serializedWindow, "statsText", statsText);
        SetObjectReference(serializedWindow, "attacksPanel", attacksPanel);
        SetObjectReference(serializedWindow, "saveResultText", saveResultText);
        SetObjectReference(serializedWindow, "backToTitleButton", backToTitleButton);
        serializedWindow.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(windowView);
    }

    private static void EnsureAutoResultThumbnail(TrainingAutoResultView windowView)
    {
        if (windowView == null)
        {
            return;
        }

        Transform panel = windowView.transform.Find("WindowPanel");
        if (panel == null)
        {
            return;
        }

        Image thumbnailImage = EnsureAutoResultThumbnailImage(panel);
        SerializedObject serializedWindow = new SerializedObject(windowView);
        SetObjectReference(serializedWindow, "thumbnailImage", thumbnailImage);
        serializedWindow.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Image EnsureAutoResultThumbnailImage(Transform panelParent)
    {
        Transform existingFrame = panelParent.Find("ThumbnailFrame");
        if (existingFrame != null)
        {
            Image existing = existingFrame.Find("ThumbnailImage")?.GetComponent<Image>();
            if (existing != null)
            {
                return existing;
            }
        }

        Image existingDirect = panelParent.Find("ThumbnailImage")?.GetComponent<Image>();
        if (existingDirect != null)
        {
            return existingDirect;
        }

        return TrainingModalUiBuilder.CreateResumeThumbnailImage(panelParent);
    }

    private static void EnsureOverlayWindowGroup(MonoBehaviour windowView)
    {
        if (windowView == null)
        {
            return;
        }

        RemoveLegacyPanelCanvas(windowView.gameObject);
        RemoveVisibilityCanvasGroup(windowView.gameObject);
        SetPanelHostHidden(windowView.gameObject, hidden: true);

        SerializedObject serializedWindow = new SerializedObject(windowView);
        SetObjectReference(serializedWindow, "windowRoot", windowView.gameObject);
        serializedWindow.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(windowView);
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
    }

    private static void ApplyTrainingLabelStyle(TMP_Text text, float fontSize, TextAlignmentOptions alignment)
    {
        if (text == null)
        {
            return;
        }

        AppTmpFontUtility.ApplyDefaultFont(text);
    }

    private static TMP_Text CreateResumeLabel(
        Transform parent,
        string name,
        Vector2 anchor,
        Vector2 anchoredPosition,
        float fontSize,
        TextAlignmentOptions alignment)
    {
        TMP_Text label = CreateLabel(parent, name, anchor, anchor, anchoredPosition, fontSize);
        ApplyTrainingLabelStyle(label, fontSize, alignment);
        return label;
    }

    private static void ConfigureResumeBodyLabel(TMP_Text label, Vector2 size)
    {
        if (label == null)
        {
            return;
        }

        RectTransform rect = label.rectTransform;
        rect.sizeDelta = size;
        label.enableWordWrapping = true;
        label.overflowMode = TextOverflowModes.Overflow;
    }

    private static Image CreateStretchImage(Transform parent, string name)
    {
        var imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = imageObject.GetComponent<Image>();
        TitleClayUiVisualUtility.ApplyBlockerForEditorBake(image);
        return image;
    }

    private static void DisableLegacyResumeButtons(Transform hudRoot, Transform resumeWindowRoot)
    {
        if (hudRoot == null)
        {
            return;
        }

        for (int i = 0; i < hudRoot.childCount; i++)
        {
            Transform child = hudRoot.GetChild(i);
            if (child == resumeWindowRoot)
            {
                continue;
            }

            if (child.name is "ResumeContinueButton" or "ResumeRestartButton")
            {
                child.gameObject.SetActive(false);
            }
        }
    }

    private static void EnsureResumeButtons(TrainingHudView hudView)
    {
        EnsureResumeWindow(hudView);
    }

    private static void ConfigureTrainingBattle()
    {
        GameObject root = FindPrimaryTrainingSceneRoot();
        if (root == null)
        {
            return;
        }

        TrainingBattleRunner battleRunner = root.GetComponent<TrainingBattleRunner>();
        if (battleRunner == null)
        {
            battleRunner = root.AddComponent<TrainingBattleRunner>();
        }

        BattleFlowRunner source = Object.FindFirstObjectByType<BattleFlowRunner>(FindObjectsInactive.Include);
        BattleNpcStaging staging = Object.FindFirstObjectByType<BattleNpcStaging>(FindObjectsInactive.Include);
        GameObject battleCanvas = GameObject.Find("BattleCanvas");
        if (source == null)
        {
            return;
        }

        SerializedObject serializedSource = new SerializedObject(source);
        SerializedObject serializedBattle = new SerializedObject(battleRunner);
        CopyObjectReference(serializedSource, serializedBattle, "battleView");
        CopyObjectReference(serializedSource, serializedBattle, "battleUiCanvas");
        CopyObjectReference(serializedSource, serializedBattle, "playerSpawn");
        CopyObjectReference(serializedSource, serializedBattle, "enemySpawn");
        CopyObjectReference(serializedSource, serializedBattle, "configurator");
        CopyObjectReference(serializedSource, serializedBattle, "battleCamera");

        SerializedProperty sourceProfile = serializedSource.FindProperty("cameraProfile");
        SerializedProperty battleProfile = serializedBattle.FindProperty("cameraProfile");
        if (sourceProfile != null && battleProfile != null)
        {
            CopySerializedPropertyValues(sourceProfile, battleProfile);
        }

        if (staging != null)
        {
            SerializedObject serializedStaging = new SerializedObject(staging);
            SetObjectReference(
                serializedBattle,
                "overlayView",
                serializedStaging.FindProperty("overlayView")?.objectReferenceValue);
            SetObjectReference(serializedBattle, "staging", staging);
        }

        SetObjectReference(serializedBattle, "battleCanvasRoot", battleCanvas);
        serializedBattle.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(battleRunner);
    }

    private static void CopySerializedPropertyValues(SerializedProperty source, SerializedProperty destination)
    {
        if (source == null || destination == null)
        {
            return;
        }

        switch (source.propertyType)
        {
            case SerializedPropertyType.Integer:
            case SerializedPropertyType.LayerMask:
            case SerializedPropertyType.Character:
                destination.intValue = source.intValue;
                break;
            case SerializedPropertyType.Boolean:
                destination.boolValue = source.boolValue;
                break;
            case SerializedPropertyType.Float:
                destination.floatValue = source.floatValue;
                break;
            case SerializedPropertyType.String:
                destination.stringValue = source.stringValue;
                break;
            case SerializedPropertyType.Color:
                destination.colorValue = source.colorValue;
                break;
            case SerializedPropertyType.ObjectReference:
                destination.objectReferenceValue = source.objectReferenceValue;
                break;
            case SerializedPropertyType.Enum:
                destination.enumValueIndex = source.enumValueIndex;
                break;
            case SerializedPropertyType.Vector2:
                destination.vector2Value = source.vector2Value;
                break;
            case SerializedPropertyType.Vector3:
                destination.vector3Value = source.vector3Value;
                break;
            case SerializedPropertyType.Vector4:
                destination.vector4Value = source.vector4Value;
                break;
            case SerializedPropertyType.Rect:
                destination.rectValue = source.rectValue;
                break;
            case SerializedPropertyType.Bounds:
                destination.boundsValue = source.boundsValue;
                break;
            case SerializedPropertyType.Quaternion:
                destination.quaternionValue = source.quaternionValue;
                break;
            case SerializedPropertyType.ArraySize:
                destination.arraySize = source.arraySize;
                break;
            case SerializedPropertyType.Generic:
                SerializedProperty sourceChild = source.Copy();
                SerializedProperty destinationChild = destination.Copy();
                SerializedProperty end = sourceChild.GetEndProperty();
                if (sourceChild.Next(true) && destinationChild.Next(true))
                {
                    do
                    {
                        CopySerializedPropertyValues(sourceChild, destinationChild);
                    }
                    while (sourceChild.Next(false) && destinationChild.Next(false)
                        && !SerializedProperty.EqualContents(sourceChild, end));
                }

                break;
        }
    }

    private static void CopyObjectReference(
        SerializedObject source,
        SerializedObject destination,
        string propertyName)
    {
        SerializedProperty sourceProperty = source.FindProperty(propertyName);
        SerializedProperty destinationProperty = destination.FindProperty(propertyName);
        if (sourceProperty == null || destinationProperty == null)
        {
            return;
        }

        destinationProperty.objectReferenceValue = sourceProperty.objectReferenceValue;
    }

    private static void EnsureAttackSwapChoicesPanel(TrainingHudView hudView, bool createIfMissing = true)
    {
        if (hudView == null)
        {
            return;
        }

        Transform hudTransform = hudView.transform;
        Transform contentRoot = TrainingInProgressHudPrefabUtility.EnsureContentRoot(hudView, migrateExistingChildren: false);
        TrainingAttackSwapChoicesView choicesView =
            FindDeepChild(contentRoot, "AttackSwapPanel")?.GetComponent<TrainingAttackSwapChoicesView>()
            ?? FindDeepChild(hudTransform, "AttackSwapPanel")?.GetComponent<TrainingAttackSwapChoicesView>();
        if (choicesView == null)
        {
            if (!createIfMissing)
            {
                return;
            }

            choicesView = CreateAttackSwapChoicesPanelForScene(contentRoot);
        }
        else
        {
            WireAttackSwapChoicesReferences(choicesView);
        }

        DisableLegacyAttackSwapButtons(hudTransform);

        GameObject attackSwapPanel = EnsureAttackSwapPanelRoot(choicesView);

        SerializedObject serializedHud = new SerializedObject(hudView);
        SetObjectReference(serializedHud, "attackSwapChoicesView", choicesView);
        SetObjectReference(serializedHud, "attackSwapPanel", attackSwapPanel);
        serializedHud.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(hudView);
    }

    private static GameObject EnsureAttackSwapPanelRoot(TrainingAttackSwapChoicesView choicesView)
    {
        if (choicesView == null)
        {
            return null;
        }

        GameObject panelObject = choicesView.gameObject;
        Image panelImage = panelObject.GetComponent<Image>();
        if (panelImage == null)
        {
            panelImage = panelObject.AddComponent<Image>();
            panelImage.raycastTarget = false;
            TitleClayUiVisualUtility.ApplyPanelForEditorBake(panelImage);
        }

        if (!panelObject.activeSelf)
        {
            panelObject.SetActive(true);
        }

        return EnsurePanelRoot(panelImage);
    }

    private static CanvasGroup EnsureAttackSwapPanelGroup(TrainingAttackSwapChoicesView choicesView)
    {
        GameObject panelRoot = EnsureAttackSwapPanelRoot(choicesView);
        return panelRoot != null ? panelRoot.GetComponent<CanvasGroup>() : null;
    }

    private static void DisableLegacyAttackSwapButtons(Transform hudTransform)
    {
        for (int i = 0; i < 5; i++)
        {
            Transform legacyButton = hudTransform.Find("AttackSwapButton" + i);
            if (legacyButton != null)
            {
                legacyButton.gameObject.SetActive(false);
            }
        }
    }

    private static TrainingAttackSwapChoicesView CreateAttackSwapChoicesPanelForScene(Transform hudTransform)
    {
        var panelObject = new GameObject(
            "AttackSwapPanel",
            typeof(RectTransform),
            typeof(Image),
            typeof(TrainingAttackSwapChoicesView));
        panelObject.transform.SetParent(hudTransform, false);
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(920f, 900f);

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.raycastTarget = false;
        TitleClayUiVisualUtility.ApplyPanelForEditorBake(panelImage);
        EnsurePanelGroup(panelImage);

        TMP_Text learnedHeaderText = CreateResumeLabel(
            panelObject.transform,
            "LearnedHeaderText",
            new Vector2(0f, 1f),
            Vector2.zero,
            22f,
            TextAlignmentOptions.TopLeft);
        ConfigureTopLeftRect(learnedHeaderText.rectTransform, Vector2.zero, new Vector2(900f, AttacksHeaderHeight));
        learnedHeaderText.text = "▼習得する技";

        TrainingAttackSlotView learnedAttackSlot = CreateAttackSlotForScene(
            panelObject.transform,
            "LearnedAttackSlot",
            -(AttacksHeaderHeight + AttackSlotSpacing));
        learnedAttackSlot.name = "LearnedAttackSlot";

        TMP_Text choicesHeaderText = CreateResumeLabel(
            panelObject.transform,
            "ChoicesHeaderText",
            new Vector2(0f, 1f),
            Vector2.zero,
            22f,
            TextAlignmentOptions.TopLeft);
        float choicesHeaderTop = -(AttacksHeaderHeight + AttackSlotSpacing + AttackSlotHeight + 16f);
        ConfigureTopLeftRect(
            choicesHeaderText.rectTransform,
            new Vector2(0f, choicesHeaderTop),
            new Vector2(900f, AttacksHeaderHeight));
        choicesHeaderText.text = "▼入れ替えるスロット";

        var swapSlots = new TrainingAttackSwapSlotView[TrainingAttackSwapChoicesView.SwapSlotCount];
        float swapSlotTop = choicesHeaderTop - (AttacksHeaderHeight + AttackSlotSpacing);
        const float swapSlotBlockHeight = 140f;
        for (int i = 0; i < TrainingAttackSwapChoicesView.SwapSlotCount; i++)
        {
            swapSlots[i] = CreateAttackSwapSlotForScene(
                panelObject.transform,
                "SwapChoice" + (i + 1),
                swapSlotTop - i * (swapSlotBlockHeight + AttackSlotSpacing));
        }

        Component skipButton = CreateButton(
            panelObject.transform,
            "SkipButton",
            "入れ替えない",
            new Vector2(0.5f, 0f),
            new Vector2(0f, 24f));
        RectTransform skipRect = skipButton.transform as RectTransform;
        if (skipRect != null)
        {
            skipRect.sizeDelta = new Vector2(320f, 52f);
        }

        TrainingAttackSwapChoicesView choicesView = panelObject.GetComponent<TrainingAttackSwapChoicesView>();
        SerializedObject serializedChoices = new SerializedObject(choicesView);
        SetObjectReference(serializedChoices, "learnedHeaderText", learnedHeaderText);
        SetObjectReference(serializedChoices, "learnedAttackSlot", learnedAttackSlot);
        SetObjectReference(serializedChoices, "choicesHeaderText", choicesHeaderText);
        SetObjectReference(serializedChoices, "skipButton", skipButton);
        SerializedProperty slotsProperty = serializedChoices.FindProperty("swapSlots");
        slotsProperty.arraySize = TrainingAttackSwapChoicesView.SwapSlotCount;
        for (int i = 0; i < swapSlots.Length; i++)
        {
            slotsProperty.GetArrayElementAtIndex(i).objectReferenceValue = swapSlots[i];
        }

        serializedChoices.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(choicesView);
        return choicesView;
    }

    private static TrainingAttackSwapSlotView CreateAttackSwapSlotForScene(
        Transform parent,
        string slotName,
        float topOffsetY)
    {
        var slotObject = new GameObject(
            slotName,
            typeof(RectTransform),
            typeof(Image),
            typeof(TrainingAttackSwapSlotView));
        slotObject.transform.SetParent(parent, false);
        ConfigureTopLeftRect(
            slotObject.GetComponent<RectTransform>(),
            new Vector2(0f, topOffsetY),
            new Vector2(900f, 132f));

        Image slotImage = slotObject.GetComponent<Image>();
        Component selectButton = SceneUiLhButtonUtility.AddComponent(slotObject);
        SceneUiLhButtonUtility.ApplyPrimaryButton(selectButton);
        TMP_Text titleText = CreateResumeLabel(
            slotObject.transform,
            "TitleText",
            new Vector2(0f, 1f),
            Vector2.zero,
            16f,
            TextAlignmentOptions.TopLeft);
        ConfigureTopLeftRect(titleText.rectTransform, new Vector2(12f, -8f), new Vector2(860f, 22f));

        TrainingAttackSlotView currentAttackSlot = CreateAttackSlotForScene(
            slotObject.transform,
            "CurrentAttackSlot",
            -34f);
        RectTransform currentRect = currentAttackSlot.transform as RectTransform;
        if (currentRect != null)
        {
            currentRect.offsetMin = new Vector2(12f, currentRect.offsetMin.y);
            currentRect.offsetMax = new Vector2(-12f, currentRect.offsetMax.y);
        }

        TrainingAttackSwapSlotView slotView = slotObject.GetComponent<TrainingAttackSwapSlotView>();
        SerializedObject serializedSlot = new SerializedObject(slotView);
        SetObjectReference(serializedSlot, "selectButton", selectButton);
        SetObjectReference(serializedSlot, "titleText", titleText);
        SetObjectReference(serializedSlot, "currentAttackSlot", currentAttackSlot);
        serializedSlot.ApplyModifiedPropertiesWithoutUndo();
        slotObject.SetActive(false);
        return slotView;
    }

    private static void WireAttackSwapChoicesReferences(TrainingAttackSwapChoicesView choicesView)
    {
        if (choicesView == null)
        {
            return;
        }

        Transform panelTransform = choicesView.transform;
        TMP_Text learnedHeaderText = panelTransform.Find("LearnedHeaderText")?.GetComponent<TMP_Text>();
        TrainingAttackSlotView learnedAttackSlot =
            panelTransform.Find("LearnedAttackSlot")?.GetComponent<TrainingAttackSlotView>();
        if (learnedAttackSlot != null)
        {
            WireAttackSlotReferences(learnedAttackSlot);
        }

        TMP_Text choicesHeaderText = panelTransform.Find("ChoicesHeaderText")?.GetComponent<TMP_Text>();
        var swapSlots = new TrainingAttackSwapSlotView[TrainingAttackSwapChoicesView.SwapSlotCount];
        for (int i = 0; i < swapSlots.Length; i++)
        {
            Transform slotTransform = panelTransform.Find("SwapChoice" + (i + 1));
            swapSlots[i] = slotTransform?.GetComponent<TrainingAttackSwapSlotView>();
            if (swapSlots[i] == null)
            {
                continue;
            }

            WireAttackSwapSlotReferences(swapSlots[i]);
        }

        Component skipButton = panelTransform.Find("SkipButton") != null
            ? SceneUiLhButtonUtility.GetComponent(panelTransform.Find("SkipButton").gameObject)
            : null;
        SerializedObject serializedChoices = new SerializedObject(choicesView);
        SetObjectReference(serializedChoices, "learnedHeaderText", learnedHeaderText);
        SetObjectReference(serializedChoices, "learnedAttackSlot", learnedAttackSlot);
        SetObjectReference(serializedChoices, "choicesHeaderText", choicesHeaderText);
        SetObjectReference(serializedChoices, "skipButton", skipButton);
        SerializedProperty slotsProperty = serializedChoices.FindProperty("swapSlots");
        slotsProperty.arraySize = TrainingAttackSwapChoicesView.SwapSlotCount;
        for (int i = 0; i < swapSlots.Length; i++)
        {
            slotsProperty.GetArrayElementAtIndex(i).objectReferenceValue = swapSlots[i];
        }

        serializedChoices.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void WireAttackSwapSlotReferences(TrainingAttackSwapSlotView slotView)
    {
        if (slotView == null)
        {
            return;
        }

        Transform slotTransform = slotView.transform;
        Component selectButton = SceneUiLhButtonUtility.GetComponent(slotView.gameObject);
        TMP_Text titleText = slotTransform.Find("TitleText")?.GetComponent<TMP_Text>();
        TrainingAttackSlotView currentAttackSlot =
            slotTransform.Find("CurrentAttackSlot")?.GetComponent<TrainingAttackSlotView>();
        if (currentAttackSlot != null)
        {
            WireAttackSlotReferences(currentAttackSlot);
        }

        SerializedObject serializedSlot = new SerializedObject(slotView);
        SetObjectReference(serializedSlot, "selectButton", selectButton);
        SetObjectReference(serializedSlot, "titleText", titleText);
        SetObjectReference(serializedSlot, "currentAttackSlot", currentAttackSlot);
        serializedSlot.ApplyModifiedPropertiesWithoutUndo();
    }

    private const int LoadSlotSelectionSortingOrder = 500;
    private const int LoadSlotConfirmSortingOrder = 510;

    private readonly struct LoadSlotPersistedRefs
    {
        public readonly Transform SpawnParent;
        public readonly Material ClayMaterial;

        public LoadSlotPersistedRefs(Transform spawnParent, Material clayMaterial)
        {
            SpawnParent = spawnParent;
            ClayMaterial = clayMaterial;
        }
    }

    private static void DestroyTrainingUiRoots()
    {
        DestroySceneObjectByName("TrainingHudCanvas");
        DestroySceneObjectByName("TrainingTrainedSaveCanvas");
        DestroySceneObjectByName("TrainingTrainedSaveConfirmCanvas");
        DestroySceneObjectByName("LoadSlotCanvas");
        DestroySceneObjectByName("ConfirmSaveSlotCanvas");
    }

    private static void DestroySceneObjectByName(string objectName)
    {
        Transform target = FindSceneTransformByName(objectName);
        if (target != null)
        {
            Object.DestroyImmediate(target.gameObject);
        }
    }

    private static Transform FindSceneTransformByName(string objectName)
    {
        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform found = FindDeepChild(roots[i].transform, objectName);
            if (found != null)
            {
                return found;
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

    private static LoadSlotPersistedRefs CaptureLoadSlotPersistedRefs()
    {
        LoadSlotView loadSlotView = Object.FindFirstObjectByType<LoadSlotView>(FindObjectsInactive.Include);
        if (loadSlotView == null)
        {
            return new LoadSlotPersistedRefs(null, null);
        }

        SerializedObject serializedLoadSlot = new SerializedObject(loadSlotView);
        return new LoadSlotPersistedRefs(
            serializedLoadSlot.FindProperty("spawnParent").objectReferenceValue as Transform,
            serializedLoadSlot.FindProperty("clayMaterial").objectReferenceValue as Material);
    }

    private static Transform FindSceneUiCanvasParent()
    {
        return EnsureTrainingSceneUiCanvasRoot(createIfMissing: false);
    }

    private static Transform EnsureTrainingSceneUiCanvasRoot(bool createIfMissing)
    {
        GameObject trainingRoot = FindPrimaryTrainingSceneRoot();
        if (trainingRoot != null)
        {
            Transform nestedCanvas = trainingRoot.transform.Find("Canvas");
            if (nestedCanvas != null)
            {
                return nestedCanvas;
            }
        }

        GameObject canvasObject = GameObject.Find("Canvas");
        if (canvasObject != null)
        {
            return canvasObject.transform;
        }

        if (!createIfMissing || trainingRoot == null)
        {
            return null;
        }

        var createdCanvasObject = new GameObject(
            "Canvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        createdCanvasObject.transform.SetParent(trainingRoot.transform, false);
        ConfigureCanvasHostUnderUiRoot(createdCanvasObject.GetComponent<RectTransform>());

        Canvas canvas = createdCanvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.enabled = false;

        CanvasScaler scaler = createdCanvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        System.Type initializerType = ResolveType("Lighthouse.Scene.SceneCamera.SceneCanvasInitializer, Lighthouse.Runtime");
        if (initializerType != null && createdCanvasObject.GetComponent(initializerType) == null)
        {
            createdCanvasObject.AddComponent(initializerType);
        }

        return createdCanvasObject.transform;
    }

    private static void NormalizeTrainingSceneCanvasLayout(SceneUpdateMode updateMode)
    {
        Transform uiCanvasRoot = EnsureTrainingSceneUiCanvasRoot(updateMode == SceneUpdateMode.RebuildLayout);
        if (uiCanvasRoot == null)
        {
            Debug.LogWarning("[TrainingSceneCreator] TrainingScene/Canvas が見つかりません");
            return;
        }

        if (uiCanvasRoot is RectTransform uiRootRect && uiRootRect.localScale.sqrMagnitude < 0.001f)
        {
            uiRootRect.localScale = Vector3.one;
            EditorUtility.SetDirty(uiRootRect.gameObject);
        }

        if (updateMode == SceneUpdateMode.RebuildLayout)
        {
            ReparentTrainingCanvasHostsUnderUiRoot(uiCanvasRoot);
        }

        FixZeroScaleCanvasHostsUnder(uiCanvasRoot);
        NormalizeTrainingSceneCanvasVisibility();
    }

    private static void FixZeroScaleCanvasHostsUnder(Transform uiCanvasRoot)
    {
        if (uiCanvasRoot == null)
        {
            return;
        }

        RectTransform[] rects = uiCanvasRoot.GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < rects.Length; i++)
        {
            RectTransform rect = rects[i];
            if (rect == null || rect == uiCanvasRoot as RectTransform)
            {
                continue;
            }

            if (rect.GetComponent<Canvas>() == null && rect.GetComponentInParent<Canvas>(true) == null)
            {
                continue;
            }

            if (rect.localScale.sqrMagnitude >= 0.001f)
            {
                continue;
            }

            rect.localScale = Vector3.one;
            EditorUtility.SetDirty(rect.gameObject);
        }
    }

    private static void ReparentTrainingCanvasHostsUnderUiRoot(Transform uiCanvasRoot)
    {
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null)
            {
                continue;
            }

            Transform canvasTransform = canvas.transform;
            if (canvasTransform == uiCanvasRoot || canvasTransform.IsChildOf(uiCanvasRoot))
            {
                continue;
            }

            Transform host = FindTrainingCanvasHostRoot(canvasTransform);
            if (host == null || host.parent == uiCanvasRoot)
            {
                continue;
            }

            host.SetParent(uiCanvasRoot, false);
            ConfigureCanvasHostUnderUiRoot(host as RectTransform);
            EditorUtility.SetDirty(host.gameObject);
        }
    }

    private static Transform FindTrainingCanvasHostRoot(Transform canvasTransform)
    {
        Transform current = canvasTransform;
        while (current.parent != null)
        {
            if (current.parent.GetComponent<Canvas>() != null)
            {
                current = current.parent;
                continue;
            }

            break;
        }

        return current;
    }

    private static void ConfigureCanvasHostUnderUiRoot(RectTransform rect)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        if (rect.localScale.sqrMagnitude < 0.001f)
        {
            rect.localScale = Vector3.one;
        }
    }

    private static void NormalizeTrainingSceneCanvasVisibility()
    {
        string[] rootCanvasNames =
        {
            "LoadSlotCanvas",
            "TrainingHudCanvas",
            "TrainingTrainedSaveCanvas",
        };

        for (int i = 0; i < rootCanvasNames.Length; i++)
        {
            Transform root = FindSceneTransformByName(rootCanvasNames[i]);
            if (root == null)
            {
                continue;
            }

            Canvas canvas = root.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.enabled = false;
            }

            SetCanvasHostHidden(root.gameObject, hidden: true);
            RemoveVisibilityCanvasGroup(root.gameObject);
        }

        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null || IsTrainingRootCanvasHost(canvas.gameObject))
            {
                continue;
            }

            if (!IsLegacyNestedPanelCanvasHost(canvas.gameObject))
            {
                continue;
            }

            GameObject panelObject = canvas.gameObject;
            SetPanelHostHidden(panelObject, hidden: true);
            RemoveLegacyPanelCanvas(panelObject);
            RemoveVisibilityCanvasGroup(panelObject);
            EditorUtility.SetDirty(panelObject);
        }
    }

    private static bool IsLegacyNestedPanelCanvasHost(GameObject canvasObject)
    {
        if (canvasObject == null)
        {
            return false;
        }

        string name = canvasObject.name;
        return name == "ConfirmPanel"
            || name == "ConfirmSaveSlotCanvas"
            || name == "TrainingTrainedSaveConfirmCanvas"
            || name == "TrainingResumeWindow"
            || name == "TrainingModeSelectWindow"
            || name == "TrainingAutoResultWindow";
    }

    private static bool IsTrainingRootCanvasHost(GameObject canvasObject)
    {
        if (canvasObject == null)
        {
            return false;
        }

        string name = canvasObject.name;
        return name == "LoadSlotCanvas"
            || name == "TrainingHudCanvas"
            || name == "TrainingTrainedSaveCanvas";
    }

    private static void ConfigureLoadSlotSelection(SceneUpdateMode updateMode, LoadSlotPersistedRefs persistedRefs)
    {
        LoadSlotView existing = Object.FindFirstObjectByType<LoadSlotView>(FindObjectsInactive.Include);
        if (existing != null)
        {
            WireLoadSlotReferences(existing, persistedRefs);
            WireSelectionBackToTitleButtonReferences();
            return;
        }

        if (updateMode == SceneUpdateMode.WireReferences)
        {
            Debug.LogWarning("[TrainingSceneCreator] LoadSlotViewが未配置のため初期レイアウトで生成します");
        }

        Transform canvasParent = FindSceneUiCanvasParent();
        if (canvasParent == null)
        {
            Debug.LogError("[TrainingSceneCreator] TrainingScene/Canvas が見つかりません");
            return;
        }

        var loadSlotObject = new GameObject(
            "LoadSlotCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(LoadSlotView));
        loadSlotObject.transform.SetParent(canvasParent, false);

        RectTransform loadSlotRect = loadSlotObject.GetComponent<RectTransform>();
        loadSlotRect.anchorMin = Vector2.zero;
        loadSlotRect.anchorMax = Vector2.one;
        loadSlotRect.offsetMin = Vector2.zero;
        loadSlotRect.offsetMax = Vector2.zero;

        Canvas selectionCanvas = loadSlotObject.GetComponent<Canvas>();
        selectionCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        selectionCanvas.overrideSorting = true;
        selectionCanvas.sortingOrder = LoadSlotSelectionSortingOrder;
        selectionCanvas.enabled = true;

        CanvasScaler scaler = loadSlotObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        LoadSlotView loadSlotView = loadSlotObject.GetComponent<LoadSlotView>();
        SetCanvasHostHidden(loadSlotObject, hidden: true);
        ModelSaveSlotScrollListView scrollList = EnsureLoadSlotScrollList(loadSlotObject.transform);
        ModelSaveSlotScrollListView.EnsureSelectionBackground(selectionCanvas);

        GameObject confirmPanelRoot = EnsureLoadSlotConfirmPanel(loadSlotObject.transform);
        ModelSaveSlotScrollListView.EnsureSelectionBackground(confirmPanelRoot.transform);
        ModelSaveConfirmView confirmView = ModelSaveSlotUiPrefabUtility.EnsureConfirmViewOnRoot(confirmPanelRoot.transform);

        SerializedObject serializedLoadSlot = new SerializedObject(loadSlotView);
        serializedLoadSlot.FindProperty("selectionCanvas").objectReferenceValue = selectionCanvas;
        serializedLoadSlot.FindProperty("slotScrollList").objectReferenceValue = scrollList;
        serializedLoadSlot.FindProperty("confirmPanelRoot").objectReferenceValue = confirmPanelRoot;
        serializedLoadSlot.FindProperty("loadConfirmView").objectReferenceValue = confirmView;
        Transform importRoot = EnsureTrainingModelImportRoot(EnsureTrainingModelDisplayPoint());
        if (importRoot != null)
        {
            serializedLoadSlot.FindProperty("spawnParent").objectReferenceValue = importRoot;
        }
        else if (persistedRefs.SpawnParent != null)
        {
            serializedLoadSlot.FindProperty("spawnParent").objectReferenceValue = persistedRefs.SpawnParent;
        }

        if (persistedRefs.ClayMaterial != null)
        {
            serializedLoadSlot.FindProperty("clayMaterial").objectReferenceValue = persistedRefs.ClayMaterial;
        }

        EnsureLoadConfirmButtons(confirmPanelRoot.transform, serializedLoadSlot);
        serializedLoadSlot.ApplyModifiedPropertiesWithoutUndo();
        ModelSaveSlotUiPrefabUtility.EnsureScrollListWired(scrollList);
        EnsureSelectionBackToTitleButton();
        EditorUtility.SetDirty(loadSlotView);
    }

    private static void WireLoadSlotReferencesOnly(LoadSlotView loadSlotView, LoadSlotPersistedRefs persistedRefs)
    {
        if (loadSlotView == null)
        {
            return;
        }

        SerializedObject serializedLoadSlot = new SerializedObject(loadSlotView);
        ModelSaveSlotScrollListView scrollList =
            serializedLoadSlot.FindProperty("slotScrollList").objectReferenceValue as ModelSaveSlotScrollListView;
        if (scrollList == null)
        {
            scrollList = loadSlotView.GetComponentInChildren<ModelSaveSlotScrollListView>(true);
        }

        if (scrollList != null)
        {
            ModelSaveSlotUiPrefabUtility.WireScrollListReferencesOnly(scrollList);
            serializedLoadSlot.FindProperty("slotScrollList").objectReferenceValue = scrollList;
        }

        Canvas selectionCanvas = loadSlotView.GetComponent<Canvas>();
        if (selectionCanvas == null)
        {
            selectionCanvas = serializedLoadSlot.FindProperty("selectionCanvas").objectReferenceValue as Canvas;
        }

        WireObjectReferenceIfFound(serializedLoadSlot, "selectionCanvas", selectionCanvas);

        Transform confirmPanel = loadSlotView.transform.Find("ConfirmPanel");
        if (confirmPanel == null)
        {
            confirmPanel = loadSlotView.transform.Find("ConfirmSaveSlotCanvas");
        }

        if (confirmPanel != null)
        {
            WireObjectReferenceIfFound(serializedLoadSlot, "confirmPanelRoot", confirmPanel.gameObject);
            WireObjectReferenceIfFound(
                serializedLoadSlot,
                "loadConfirmView",
                confirmPanel.GetComponentInChildren<ModelSaveConfirmView>(true));
            WireObjectReferenceIfFound(serializedLoadSlot, "loadButton", FindButton(confirmPanel, "LoadConfirmButton"));
            WireObjectReferenceIfFound(serializedLoadSlot, "backButton", FindButton(confirmPanel, "BackButton"));
        }

        if (persistedRefs.SpawnParent != null
            && serializedLoadSlot.FindProperty("spawnParent").objectReferenceValue == null)
        {
            serializedLoadSlot.FindProperty("spawnParent").objectReferenceValue = persistedRefs.SpawnParent;
        }

        if (persistedRefs.ClayMaterial != null
            && serializedLoadSlot.FindProperty("clayMaterial").objectReferenceValue == null)
        {
            serializedLoadSlot.FindProperty("clayMaterial").objectReferenceValue = persistedRefs.ClayMaterial;
        }

        serializedLoadSlot.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(loadSlotView);
    }

    private static void WireLoadSlotReferences(LoadSlotView loadSlotView, LoadSlotPersistedRefs persistedRefs)
    {
        if (loadSlotView == null)
        {
            return;
        }

        SerializedObject serializedLoadSlot = new SerializedObject(loadSlotView);
        ModelSaveSlotScrollListView scrollList = serializedLoadSlot.FindProperty("slotScrollList").objectReferenceValue
            as ModelSaveSlotScrollListView;
        if (scrollList == null
            || !ModelSaveSlotUiPrefabUtility.IsScrollListPrefabInstance(scrollList.gameObject))
        {
            scrollList = EnsureLoadSlotScrollList(loadSlotView.transform);
            serializedLoadSlot.FindProperty("slotScrollList").objectReferenceValue = scrollList;
        }
        else
        {
            ModelSaveSlotUiPrefabUtility.WireScrollListReferencesOnly(scrollList);
        }

        Canvas selectionCanvas = loadSlotView.GetComponent<Canvas>();
        if (selectionCanvas == null)
        {
            selectionCanvas = serializedLoadSlot.FindProperty("selectionCanvas").objectReferenceValue as Canvas;
        }

        if (selectionCanvas != null)
        {
            serializedLoadSlot.FindProperty("selectionCanvas").objectReferenceValue = selectionCanvas;
            selectionCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            selectionCanvas.worldCamera = null;
            selectionCanvas.overrideSorting = true;
            selectionCanvas.sortingOrder = LoadSlotSelectionSortingOrder;
            selectionCanvas.enabled = false;
            RemoveVisibilityCanvasGroup(loadSlotView.gameObject);
            ModelSaveSlotScrollListView.EnsureSelectionBackground(selectionCanvas);
        }

        GameObject confirmPanelRoot = serializedLoadSlot.FindProperty("confirmPanelRoot").objectReferenceValue as GameObject;
        if (!IsAliveUnityObject(confirmPanelRoot))
        {
            confirmPanelRoot = EnsureLoadSlotConfirmPanel(loadSlotView.transform);
            serializedLoadSlot.FindProperty("confirmPanelRoot").objectReferenceValue = confirmPanelRoot;
        }

        if (confirmPanelRoot != null)
        {
            ModelSaveSlotScrollListView.EnsureSelectionBackground(confirmPanelRoot.transform);
            ModelSaveConfirmView confirmView = ModelSaveSlotUiPrefabUtility.EnsureConfirmViewOnRoot(confirmPanelRoot.transform);
            serializedLoadSlot.FindProperty("loadConfirmView").objectReferenceValue = confirmView;
            EnsureLoadConfirmButtons(confirmPanelRoot.transform, serializedLoadSlot);
        }

        Transform importRoot = EnsureTrainingModelImportRoot(EnsureTrainingModelDisplayPoint());
        if (importRoot != null)
        {
            serializedLoadSlot.FindProperty("spawnParent").objectReferenceValue = importRoot;
        }
        else if (persistedRefs.SpawnParent != null
            && serializedLoadSlot.FindProperty("spawnParent").objectReferenceValue == null)
        {
            serializedLoadSlot.FindProperty("spawnParent").objectReferenceValue = persistedRefs.SpawnParent;
        }

        if (persistedRefs.ClayMaterial != null
            && serializedLoadSlot.FindProperty("clayMaterial").objectReferenceValue == null)
        {
            serializedLoadSlot.FindProperty("clayMaterial").objectReferenceValue = persistedRefs.ClayMaterial;
        }

        serializedLoadSlot.ApplyModifiedPropertiesWithoutUndo();
        if (scrollList != null)
        {
            ModelSaveSlotUiPrefabUtility.WireScrollListReferences(scrollList);
        }

        EditorUtility.SetDirty(loadSlotView);
    }

    private static ModelSaveSlotScrollListView EnsureLoadSlotScrollList(Transform loadSlotRoot)
    {
        return ModelSaveSlotUiPrefabUtility.EnsureScrollListPrefabInstance(loadSlotRoot);
    }

    private static GameObject EnsureLoadSlotConfirmPanel(Transform loadSlotRoot)
    {
        Transform existing = loadSlotRoot.Find("ConfirmPanel");
        if (existing == null)
        {
            existing = loadSlotRoot.Find("ConfirmSaveSlotCanvas");
        }

        if (existing == null)
        {
            existing = FindSceneTransformByName("ConfirmSaveSlotCanvas");
        }

        GameObject confirmObject;
        if (existing != null)
        {
            confirmObject = existing.gameObject;
            confirmObject.name = "ConfirmPanel";
        }
        else
        {
            confirmObject = new GameObject("ConfirmPanel", typeof(RectTransform));
            confirmObject.transform.SetParent(loadSlotRoot, false);
        }

        if (confirmObject.transform.parent != loadSlotRoot)
        {
            confirmObject.transform.SetParent(loadSlotRoot, false);
        }

        RemoveLegacyPanelCanvas(confirmObject);
        RemoveVisibilityCanvasGroup(confirmObject);
        ConfigureCanvasHostUnderUiRoot(confirmObject.GetComponent<RectTransform>());
        SetPanelHostHidden(confirmObject, hidden: true);
        return confirmObject;
    }

    private static void EnsureLoadConfirmButtons(Transform parent, SerializedObject serializedLoadSlot)
    {
        SerializedProperty loadButtonProperty = serializedLoadSlot.FindProperty("loadButton");
        if (loadButtonProperty != null && loadButtonProperty.objectReferenceValue == null)
        {
            loadButtonProperty.objectReferenceValue = CreateButton(
                parent,
                "LoadConfirmButton",
                "ロードする",
                new Vector2(0.5f, 0f),
                new Vector2(-190f, 72f));
        }

        SerializedProperty backButtonProperty = serializedLoadSlot.FindProperty("backButton");
        if (backButtonProperty != null && backButtonProperty.objectReferenceValue == null)
        {
            backButtonProperty.objectReferenceValue = CreateButton(
                parent,
                "LoadConfirmBackButton",
                "戻る",
                new Vector2(0.5f, 0f),
                new Vector2(190f, 72f));
        }
    }

    private static void WireTrainingSaveSlotUiReferences()
    {
        ModelSaveSlotScrollListView[] scrollLists =
            Object.FindObjectsByType<ModelSaveSlotScrollListView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < scrollLists.Length; i++)
        {
            ModelSaveSlotUiPrefabUtility.WireScrollListReferencesOnly(scrollLists[i]);
        }
    }

    private static void SyncTrainingSaveSlotUiFromPrefab()
    {
        // セーブスロットUIのプレハブ再配置は禁止する
        WireTrainingSaveSlotUiReferences();
    }

    private static void WireLifetimeScopeReferences()
    {
        System.Type lifetimeScopeType = ResolveType(TrainingLifetimeScopeTypeName);
        if (lifetimeScopeType == null)
        {
            return;
        }

        Component scope = Object.FindFirstObjectByType(lifetimeScopeType, FindObjectsInactive.Include) as Component;
        if (scope == null)
        {
            return;
        }

        System.Type trainingSceneType = ResolveType(TrainingSceneTypeName);
        Component trainingScene = trainingSceneType != null
            ? Object.FindFirstObjectByType(trainingSceneType, FindObjectsInactive.Include) as Component
            : null;

        SerializedObject serializedScope = new SerializedObject(scope);
        SetObjectReference(serializedScope, "trainingScene", trainingScene);
        SetObjectReference(serializedScope, "trainedSaveView",
            Object.FindFirstObjectByType<TrainingTrainedSaveView>(FindObjectsInactive.Include));
        SetObjectReference(serializedScope, "modeSelectView",
            Object.FindFirstObjectByType<TrainingModeSelectView>(FindObjectsInactive.Include));
        SetObjectReference(serializedScope, "autoResultView",
            Object.FindFirstObjectByType<TrainingAutoResultView>(FindObjectsInactive.Include));
        SetObjectReference(serializedScope, "flowRunner",
            Object.FindFirstObjectByType<TrainingFlowRunner>(FindObjectsInactive.Include));
        SetObjectReference(serializedScope, "battleRunner",
            Object.FindFirstObjectByType<TrainingBattleRunner>(FindObjectsInactive.Include));
        SetObjectReference(serializedScope, "hudView",
            Object.FindFirstObjectByType<TrainingHudView>(FindObjectsInactive.Include));
        SetObjectReference(serializedScope, "loadSlotView",
            Object.FindFirstObjectByType<LoadSlotView>(FindObjectsInactive.Include));
        SetObjectReference(serializedScope, "selectionCanvas", FindSelectionCanvas());
        SetObjectReference(serializedScope, "cameraView",
            Object.FindFirstObjectByType<Camera.View.ClayEditCameraView>(FindObjectsInactive.Include));
        SetObjectReference(serializedScope, "trainingDisplay",
            Object.FindFirstObjectByType<TrainingDisplay>(FindObjectsInactive.Include));
        SetObjectReference(serializedScope, "backgroundView",
            Object.FindFirstObjectByType<TrainingBackgroundView>(FindObjectsInactive.Include));
        SetObjectReference(serializedScope, "locationCameraView",
            Object.FindFirstObjectByType<TrainingLocationCameraView>(FindObjectsInactive.Include));
        SetObjectReference(serializedScope, "postProcessView",
            Object.FindFirstObjectByType<BattleNpcPostProcessView>(FindObjectsInactive.Include));
        serializedScope.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(scope);

        TrainingFlowRunner flowRunner = Object.FindFirstObjectByType<TrainingFlowRunner>(FindObjectsInactive.Include);
        if (flowRunner == null)
        {
            return;
        }

        SerializedObject serializedRunner = new SerializedObject(flowRunner);
        serializedRunner.FindProperty("loadSlotView").objectReferenceValue =
            Object.FindFirstObjectByType<LoadSlotView>(FindObjectsInactive.Include);
        serializedRunner.FindProperty("selectionCanvas").objectReferenceValue = FindSelectionCanvas();
        serializedRunner.FindProperty("hudView").objectReferenceValue =
            Object.FindFirstObjectByType<TrainingHudView>(FindObjectsInactive.Include);
        serializedRunner.FindProperty("locationCameraView").objectReferenceValue =
            Object.FindFirstObjectByType<TrainingLocationCameraView>(FindObjectsInactive.Include);
        WireSelectionBackToTitleButtonReferences(serializedRunner);
        serializedRunner.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(flowRunner);
    }

    private static void WireTrainingTrainedSaveReferencesOnly()
    {
        TrainingTrainedSaveView trainedSaveView =
            Object.FindFirstObjectByType<TrainingTrainedSaveView>(FindObjectsInactive.Include);
        if (trainedSaveView == null)
        {
            return;
        }

        Transform root = trainedSaveView.transform;
        SerializedObject serializedTrainedSave = new SerializedObject(trainedSaveView);
        WireObjectReferenceIfFound(serializedTrainedSave, "rootCanvas", trainedSaveView.GetComponent<Canvas>());
        WireObjectReferenceIfFound(serializedTrainedSave, "headerText", FindChildText(root, "HeaderText"));
        WireObjectReferenceIfFound(
            serializedTrainedSave,
            "slotScrollList",
            root.GetComponentInChildren<ModelSaveSlotScrollListView>(true));

        Transform confirmPanel = root.Find("ConfirmPanel");
        if (confirmPanel != null)
        {
            WireObjectReferenceIfFound(serializedTrainedSave, "confirmPanelRoot", confirmPanel.gameObject);
            WireObjectReferenceIfFound(
                serializedTrainedSave,
                "confirmView",
                confirmPanel.GetComponentInChildren<ModelSaveConfirmView>(true));
            WireObjectReferenceIfFound(
                serializedTrainedSave,
                "confirmMessageText",
                FindChildText(confirmPanel, "ConfirmMessageText"));
            WireObjectReferenceIfFound(serializedTrainedSave, "saveButton", FindButton(confirmPanel, "SaveButton"));
            WireObjectReferenceIfFound(serializedTrainedSave, "backButton", FindButton(confirmPanel, "BackButton"));
        }

        WireObjectReferenceIfFound(
            serializedTrainedSave,
            "backToTitleButton",
            FindButton(root, "BackToTitleButton")
                ?? FindButton(root, "SelectionBackToTitleButton"));
        serializedTrainedSave.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(trainedSaveView);
    }

    private static void ConfigureLifetimeScope()
    {
        WireLifetimeScopeReferences();
        EnsureSelectionBackToTitleButton();
    }

    private static void WireSelectionBackToTitleButtonReferences()
    {
        TrainingFlowRunner flowRunner = Object.FindFirstObjectByType<TrainingFlowRunner>(FindObjectsInactive.Include);
        if (flowRunner == null)
        {
            return;
        }

        SerializedObject serializedRunner = new SerializedObject(flowRunner);
        WireSelectionBackToTitleButtonReferences(serializedRunner);
        serializedRunner.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(flowRunner);
    }

    private static void WireSelectionBackToTitleButtonReferences(SerializedObject serializedRunner)
    {
        if (serializedRunner == null)
        {
            return;
        }

        SerializedProperty property = serializedRunner.FindProperty("selectionBackToTitleButton");
        if (property == null || property.objectReferenceValue != null)
        {
            return;
        }

        LoadSlotView loadSlotView = Object.FindFirstObjectByType<LoadSlotView>(FindObjectsInactive.Include);
        if (loadSlotView == null)
        {
            return;
        }

        Transform existing = loadSlotView.transform.Find("SelectionBackToTitleButton");
        if (existing == null)
        {
            return;
        }

        Component button = SceneUiLhButtonUtility.GetComponent(existing.gameObject);
        if (button != null)
        {
            property.objectReferenceValue = button;
        }
    }

    private static void EnsureSelectionBackToTitleButton()
    {
        TrainingFlowRunner flowRunner = Object.FindFirstObjectByType<TrainingFlowRunner>(FindObjectsInactive.Include);
        if (flowRunner == null)
        {
            return;
        }

        SerializedObject serializedRunner = new SerializedObject(flowRunner);
        EnsureSelectionBackToTitleButton(serializedRunner);
        serializedRunner.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(flowRunner);
    }

    private static void EnsureSelectionBackToTitleButton(SerializedObject serializedRunner)
    {
        if (serializedRunner == null)
        {
            return;
        }

        SerializedProperty property = serializedRunner.FindProperty("selectionBackToTitleButton");
        if (property == null)
        {
            return;
        }

        if (property.objectReferenceValue != null)
        {
            return;
        }

        LoadSlotView loadSlotView = Object.FindFirstObjectByType<LoadSlotView>(FindObjectsInactive.Include);
        if (loadSlotView == null)
        {
            return;
        }

        Transform existing = loadSlotView.transform.Find("SelectionBackToTitleButton");
        Component button;
        if (existing != null)
        {
            button = SceneUiLhButtonUtility.GetComponent(existing.gameObject);
            if (button == null)
            {
                button = SceneUiLhButtonUtility.AddComponent(existing.gameObject);
                SceneUiLhButtonUtility.ApplyPrimaryButton(button);
            }
        }
        else
        {
            button = CreateButton(
                loadSlotView.transform,
                "SelectionBackToTitleButton",
                "タイトルへ戻る",
                new Vector2(1f, 0f),
                new Vector2(-28f, 28f));
        }

        property.objectReferenceValue = button;
    }

    private static Canvas FindSelectionCanvas()
    {
        LoadSlotView loadSlotView = Object.FindFirstObjectByType<LoadSlotView>(FindObjectsInactive.Include);
        if (loadSlotView == null)
        {
            return null;
        }

        Canvas canvas = loadSlotView.SelectionCanvas;
        if (canvas != null)
        {
            return canvas;
        }

        canvas = loadSlotView.GetComponent<Canvas>();
        if (canvas != null)
        {
            SetCanvasHostHidden(loadSlotView.gameObject, hidden: true);
        }

        return canvas;
    }

    private static TMP_Text CreateLabel(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 anchoredPosition,
        float fontSize)
    {
        return CreateLabel(parent, name, anchorMin, anchorMax, anchoredPosition, fontSize, new Vector2(900f, 60f));
    }

    private static TMP_Text CreateLabel(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 anchoredPosition,
        float fontSize,
        Vector2 sizeDelta)
    {
        if (parent == null)
        {
            return null;
        }

        var labelObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(parent, false);
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(anchorMin.x, anchorMin.y);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        TextMeshProUGUI text = labelObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        AppTmpFontUtility.ApplyDefaultFont(text);
        return text;
    }

    private static TMP_Text CreateLogPanelLabel(Transform logPanel)
    {
        return CreateStretchPanelLabel(logPanel, "LogText", 26f);
    }

    private static TMP_Text CreateStretchPanelLabel(
        Transform parent,
        string name,
        float fontSize)
    {
        if (parent == null)
        {
            return null;
        }

        var labelObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(parent, false);
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(12f, 12f);
        rect.offsetMax = new Vector2(-12f, -12f);

        TextMeshProUGUI text = labelObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.enableWordWrapping = true;
        AppTmpFontUtility.ApplyDefaultFont(text);
        return text;
    }

    private static Component CreateButton(
        Transform parent,
        string name,
        string label,
        Vector2 anchor,
        Vector2 anchoredPosition)
    {
        var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(280f, 72f);

        Component button = SceneUiLhButtonUtility.AddComponent(buttonObject);

        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = labelObject.GetComponent<TextMeshProUGUI>();
        text.text = label;
        text.raycastTarget = false;
        if (button != null)
        {
            SceneUiLhButtonUtility.ApplyMenuButton(button);
        }

        return button;
    }

    private static void SetObjectReference(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            Debug.LogWarning(
                $"[TrainingSceneCreator] property not found: {propertyName} on {serializedObject.targetObject.GetType().Name}");
            return;
        }

        property.objectReferenceValue = value;
    }

    private static System.Type ResolveType(string typeName)
    {
        System.Type type = System.Type.GetType(typeName);
        if (type != null)
        {
            return type;
        }

        Debug.LogError($"[TrainingSceneCreator] type not found: {typeName}");
        return null;
    }

    private static Component GetOrAddComponent(GameObject target, string typeName)
    {
        System.Type type = ResolveType(typeName);
        if (type == null)
        {
            return null;
        }

        Component component = target.GetComponent(type);
        if (component == null)
        {
            component = target.AddComponent(type);
        }

        return component;
    }

    private static void EnsureBuildSettings()
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        for (int i = 0; i < scenes.Length; i++)
        {
            if (scenes[i].path == TargetScenePath)
            {
                return;
            }
        }

        var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(scenes)
        {
            new EditorBuildSettingsScene(TargetScenePath, true)
        };
        EditorBuildSettings.scenes = list.ToArray();
    }

    private const float AttacksHeaderHeight = 28f;
    private const float AttackSlotHeight = 108f;
    private const float AttackSlotSpacing = 8f;

    private static void EnsureResumeAttacksPanel(TrainingResumeWindowView windowView)
    {
        if (windowView == null)
        {
            return;
        }

        Transform panel = windowView.transform.Find("WindowPanel");
        if (panel == null)
        {
            return;
        }

        TrainingResumeAttacksContentView attacksPanel = EnsureAttacksPanel(
            panel,
            new Vector2(290f, -130f),
            new Vector2(620f, 500f),
            showHeader: false);
        SerializedObject serializedWindow = new SerializedObject(windowView);
        SetObjectReference(serializedWindow, "attacksPanel", attacksPanel);
        serializedWindow.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureAutoResultAttacksPanel(TrainingAutoResultView windowView)
    {
        if (windowView == null)
        {
            return;
        }

        Transform panel = windowView.transform.Find("WindowPanel");
        if (panel == null)
        {
            return;
        }

        TrainingResumeAttacksContentView attacksPanel = EnsureAttacksPanel(
            panel,
            new Vector2(470f, -140f),
            new Vector2(440f, 480f));
        SerializedObject serializedWindow = new SerializedObject(windowView);
        SetObjectReference(serializedWindow, "attacksPanel", attacksPanel);
        serializedWindow.ApplyModifiedPropertiesWithoutUndo();
    }

    private static TrainingResumeAttacksContentView EnsureAttacksPanel(
        Transform panelParent,
        Vector2 anchoredPosition,
        Vector2 size,
        bool showHeader = true)
    {
        Transform legacyText = panelParent.Find("AttacksText");
        if (legacyText != null
            && !PrefabUtility.IsPartOfPrefabInstance(legacyText.gameObject))
        {
            Object.DestroyImmediate(legacyText.gameObject);
        }

        Transform existingPanel = panelParent.Find("AttacksPanel");
        if (existingPanel != null)
        {
            TrainingResumeAttacksContentView existingView =
                existingPanel.GetComponent<TrainingResumeAttacksContentView>();
            if (existingView != null)
            {
                WireAttacksPanelReferences(existingView);
                ApplyAttacksPanelHeaderVisibility(existingView, showHeader);
                return existingView;
            }
        }

        var panelObject = new GameObject(
            "AttacksPanel",
            typeof(RectTransform),
            typeof(TrainingResumeAttacksContentView));
        panelObject.transform.SetParent(panelParent, false);
        ConfigureTopLeftRect(panelObject.GetComponent<RectTransform>(), anchoredPosition, size);

        TMP_Text headerText = CreateResumeLabel(
            panelObject.transform,
            "HeaderText",
            new Vector2(0f, 1f),
            Vector2.zero,
            22f,
            TextAlignmentOptions.TopLeft);
        ConfigureTopLeftRect(headerText.rectTransform, Vector2.zero, new Vector2(size.x, AttacksHeaderHeight));
        headerText.text = showHeader ? "▼技構成" : string.Empty;
        headerText.gameObject.SetActive(showHeader);

        TMP_Text emptyText = CreateResumeLabel(
            panelObject.transform,
            "EmptyText",
            new Vector2(0f, 1f),
            new Vector2(0f, -AttacksHeaderHeight),
            16f,
            TextAlignmentOptions.TopLeft);
        ConfigureTopLeftRect(emptyText.rectTransform, new Vector2(0f, -AttacksHeaderHeight), new Vector2(size.x, 48f));
        TitleClayUiVisualUtility.ApplySubLabelText(emptyText);
        emptyText.gameObject.SetActive(false);

        var slotViews = new TrainingAttackSlotView[TrainingResumeAttacksContentView.AttackSlotCount];
        float slotTop = -(AttacksHeaderHeight + AttackSlotSpacing);
        for (int i = 0; i < TrainingResumeAttacksContentView.AttackSlotCount; i++)
        {
            slotViews[i] = CreateAttackSlotForScene(
                panelObject.transform,
                "AttackSlot" + (i + 1),
                slotTop - i * (AttackSlotHeight + AttackSlotSpacing));
        }

        TrainingResumeAttacksContentView attacksPanel = panelObject.GetComponent<TrainingResumeAttacksContentView>();
        SerializedObject serializedPanel = new SerializedObject(attacksPanel);
        SetObjectReference(serializedPanel, "headerText", headerText);
        SetObjectReference(serializedPanel, "emptyText", emptyText);
        SerializedProperty slotsProperty = serializedPanel.FindProperty("attackSlots");
        slotsProperty.arraySize = TrainingResumeAttacksContentView.AttackSlotCount;
        for (int i = 0; i < slotViews.Length; i++)
        {
            slotsProperty.GetArrayElementAtIndex(i).objectReferenceValue = slotViews[i];
        }

        serializedPanel.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(attacksPanel);
        return attacksPanel;
    }

    private static void ApplyAttacksPanelHeaderVisibility(
        TrainingResumeAttacksContentView attacksPanel,
        bool showHeader)
    {
        if (attacksPanel == null)
        {
            return;
        }

        TMP_Text headerText = attacksPanel.transform.Find("HeaderText")?.GetComponent<TMP_Text>();
        if (headerText == null)
        {
            return;
        }

        headerText.text = showHeader ? "▼技構成" : string.Empty;
        headerText.gameObject.SetActive(showHeader);
    }

    private static void WireAttacksPanelReferences(TrainingResumeAttacksContentView attacksPanel)
    {
        if (attacksPanel == null)
        {
            return;
        }

        Transform panelTransform = attacksPanel.transform;
        TMP_Text headerText = panelTransform.Find("HeaderText")?.GetComponent<TMP_Text>();
        TMP_Text emptyText = panelTransform.Find("EmptyText")?.GetComponent<TMP_Text>();
        var slotViews = new TrainingAttackSlotView[TrainingResumeAttacksContentView.AttackSlotCount];
        for (int i = 0; i < slotViews.Length; i++)
        {
            slotViews[i] = panelTransform.Find("AttackSlot" + (i + 1))?.GetComponent<TrainingAttackSlotView>();
            if (slotViews[i] != null)
            {
                WireAttackSlotReferences(slotViews[i]);
            }
        }

        SerializedObject serializedPanel = new SerializedObject(attacksPanel);
        SetObjectReference(serializedPanel, "headerText", headerText);
        SetObjectReference(serializedPanel, "emptyText", emptyText);
        SerializedProperty slotsProperty = serializedPanel.FindProperty("attackSlots");
        slotsProperty.arraySize = TrainingResumeAttacksContentView.AttackSlotCount;
        for (int i = 0; i < slotViews.Length; i++)
        {
            slotsProperty.GetArrayElementAtIndex(i).objectReferenceValue = slotViews[i];
        }

        serializedPanel.ApplyModifiedPropertiesWithoutUndo();
    }

    private static TrainingAttackSlotView CreateAttackSlotForScene(
        Transform parent,
        string slotName,
        float topOffsetY)
    {
        var slotObject = new GameObject(slotName, typeof(RectTransform), typeof(TrainingAttackSlotView));
        slotObject.transform.SetParent(parent, false);
        ConfigureTopLeftRect(
            slotObject.GetComponent<RectTransform>(),
            new Vector2(0f, topOffsetY),
            new Vector2(0f, AttackSlotHeight));

        Image slotFrameImage = EnsureAttackSlotFrame(slotObject.transform);

        TMP_Text attackNameText = CreateResumeLabel(
            slotObject.transform,
            "AttackNameText",
            new Vector2(0f, 1f),
            Vector2.zero,
            18f,
            TextAlignmentOptions.TopLeft);
        ConfigureTopLeftRect(attackNameText.rectTransform, Vector2.zero, new Vector2(400f, 22f));
        TitleClayUiVisualUtility.ApplyTitleText(attackNameText);

        RectTransform partInfoRow = MoveAttackPartInfoView.Create(
            slotObject.transform,
            32f,
            12f,
            MotionType.Punch,
            largeLayout: false);
        ConfigureTopLeftRect(partInfoRow, new Vector2(0f, -26f), new Vector2(400f, 32f));

        Image requiredPartIcon = partInfoRow.Find("RequiredPartGroup/RequiredPartIcon/Icon")?.GetComponent<Image>();
        Image targetPartIcon = partInfoRow.Find("TargetPartGroup/TargetPartIcon/Icon")?.GetComponent<Image>();

        TrainingAttackStatsRowView statsRowView = CreateSceneAttackStatsRow(slotObject.transform);
        ConfigureTopLeftRect(
            statsRowView.transform as RectTransform,
            new Vector2(0f, -62f),
            new Vector2(400f, 24f));

        TMP_Text damageText = statsRowView.transform.Find("DamageText")?.GetComponent<TMP_Text>();
        TMP_Text costText = statsRowView.transform.Find("CostText")?.GetComponent<TMP_Text>();
        TMP_Text rangeLabelText = FindRangeLabelText(statsRowView.transform);
        MoveRangeSegmentBarView rangeBar = statsRowView.GetComponentInChildren<MoveRangeSegmentBarView>(true);

        TrainingAttackSlotView slotView = slotObject.GetComponent<TrainingAttackSlotView>();
        SerializedObject serializedSlot = new SerializedObject(slotView);
        SetObjectReference(serializedSlot, "slotFrameImage", slotFrameImage);
        SetObjectReference(serializedSlot, "attackNameText", attackNameText);
        SetObjectReference(serializedSlot, "requiredPartIcon", requiredPartIcon);
        SetObjectReference(serializedSlot, "targetPartIcon", targetPartIcon);
        SetObjectReference(serializedSlot, "damageText", damageText);
        SetObjectReference(serializedSlot, "costText", costText);
        SetObjectReference(serializedSlot, "rangeLabelText", rangeLabelText);
        SetObjectReference(serializedSlot, "rangeBar", rangeBar);
        serializedSlot.ApplyModifiedPropertiesWithoutUndo();
        slotObject.SetActive(false);
        return slotView;
    }

    private static void WireAttackSlotReferences(TrainingAttackSlotView slotView)
    {
        if (slotView == null)
        {
            return;
        }

        Transform slotTransform = slotView.transform;
        Image slotFrameImage = EnsureAttackSlotFrame(slotTransform);
        TMP_Text attackNameText = slotTransform.Find("AttackNameText")?.GetComponent<TMP_Text>();
        Transform partInfoRow = slotTransform.Find("PartInfoRow");
        Image requiredPartIcon = partInfoRow != null
            ? partInfoRow.Find("RequiredPartGroup/RequiredPartIcon/Icon")?.GetComponent<Image>()
            : null;
        Image targetPartIcon = partInfoRow != null
            ? partInfoRow.Find("TargetPartGroup/TargetPartIcon/Icon")?.GetComponent<Image>()
            : null;

        Transform statsRow = slotTransform.Find("StatsRow");
        TMP_Text damageText = FindChildText(statsRow, "DamageText")
            ?? FindChildText(slotTransform, "DamageText")
            ?? FindChildText(statsRow, "StatsText")
            ?? FindChildText(slotTransform, "StatsText");
        TMP_Text costText = FindChildText(statsRow, "CostText")
            ?? FindChildText(slotTransform, "CostText");
        TMP_Text rangeLabelText = FindRangeLabelText(statsRow);
        MoveRangeSegmentBarView rangeBar = statsRow != null
            ? statsRow.GetComponentInChildren<MoveRangeSegmentBarView>(true)
            : slotTransform.GetComponentInChildren<MoveRangeSegmentBarView>(true);

        SerializedObject serializedSlot = new SerializedObject(slotView);
        SetObjectReference(serializedSlot, "slotFrameImage", slotFrameImage);
        SetObjectReference(serializedSlot, "attackNameText", attackNameText);
        SetObjectReference(serializedSlot, "requiredPartIcon", requiredPartIcon);
        SetObjectReference(serializedSlot, "targetPartIcon", targetPartIcon);
        SetObjectReference(serializedSlot, "damageText", damageText);
        SetObjectReference(serializedSlot, "costText", costText);
        SetObjectReference(serializedSlot, "rangeLabelText", rangeLabelText);
        SetObjectReference(serializedSlot, "rangeBar", rangeBar);
        serializedSlot.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Image EnsureAttackSlotFrame(Transform slotTransform)
    {
        if (slotTransform == null)
        {
            return null;
        }

        Transform existingFrame = slotTransform.Find("SlotFrame");
        if (existingFrame != null)
        {
            Image existingImage = existingFrame.GetComponent<Image>();
            if (existingImage != null)
            {
                existingFrame.SetAsFirstSibling();
                return existingImage;
            }
        }

        var frameObject = new GameObject("SlotFrame", typeof(RectTransform), typeof(Image));
        frameObject.transform.SetParent(slotTransform, false);
        frameObject.transform.SetAsFirstSibling();

        RectTransform frameRect = frameObject.GetComponent<RectTransform>();
        frameRect.anchorMin = Vector2.zero;
        frameRect.anchorMax = Vector2.one;
        frameRect.pivot = new Vector2(0.5f, 0.5f);
        frameRect.offsetMin = Vector2.zero;
        frameRect.offsetMax = Vector2.zero;

        Image frameImage = frameObject.GetComponent<Image>();
        frameImage.raycastTarget = false;
        TitleClayUiVisualUtility.ApplyPanelForEditorBake(frameImage);
        return frameImage;
    }

    private static TrainingAttackStatsRowView CreateSceneAttackStatsRow(Transform parent)
    {
        var rowObject = new GameObject("StatsRow", typeof(RectTransform), typeof(TrainingAttackStatsRowView));
        rowObject.transform.SetParent(parent, false);

        TMP_Text damageText = CreateResumeLabel(
            rowObject.transform,
            "DamageText",
            new Vector2(0f, 1f),
            Vector2.zero,
            16f,
            TextAlignmentOptions.MidlineLeft);
        ConfigureTopLeftRect(damageText.rectTransform, Vector2.zero, new Vector2(118f, 24f));
        TitleClayUiVisualUtility.ApplyBodyText(damageText);

        TMP_Text costText = CreateResumeLabel(
            rowObject.transform,
            "CostText",
            new Vector2(0f, 1f),
            new Vector2(124f, 0f),
            16f,
            TextAlignmentOptions.MidlineLeft);
        ConfigureTopLeftRect(costText.rectTransform, new Vector2(124f, 0f), new Vector2(118f, 24f));
        TitleClayUiVisualUtility.ApplyBodyText(costText);

        var rangeGroupObject = new GameObject("RangeGroup", typeof(RectTransform));
        rangeGroupObject.transform.SetParent(rowObject.transform, false);
        ConfigureTopLeftRect(
            rangeGroupObject.GetComponent<RectTransform>(),
            new Vector2(248f, 0f),
            new Vector2(MoveRangeSegmentBarView.CompactBarWidth + 56f, 24f));

        TMP_Text rangeLabelText = CreateResumeLabel(
            rangeGroupObject.transform,
            "RangeText",
            new Vector2(0f, 1f),
            Vector2.zero,
            16f,
            TextAlignmentOptions.MidlineLeft);
        ConfigureTopLeftRect(rangeLabelText.rectTransform, Vector2.zero, new Vector2(48f, 24f));
        rangeLabelText.text = TrainingAttackTeacher.ResumeAttackRangeLabel;
        TitleClayUiVisualUtility.ApplyBodyText(rangeLabelText);

        var rangeColumnObject = new GameObject("RangeColumn", typeof(RectTransform));
        rangeColumnObject.transform.SetParent(rangeGroupObject.transform, false);
        ConfigureTopLeftRect(
            rangeColumnObject.GetComponent<RectTransform>(),
            new Vector2(52f, 0f),
            new Vector2(MoveRangeSegmentBarView.CompactBarWidth, MoveRangeSegmentBarView.CompactBarHeight));
        MoveRangeSegmentBarView rangeBar = MoveRangeSegmentBarView.CreateCompact(rangeColumnObject.transform);

        TrainingAttackStatsRowView statsRowView = rowObject.GetComponent<TrainingAttackStatsRowView>();
        SerializedObject serializedRow = new SerializedObject(statsRowView);
        SetObjectReference(serializedRow, "statsText", damageText);
        SetObjectReference(serializedRow, "rangeBar", rangeBar);
        serializedRow.ApplyModifiedPropertiesWithoutUndo();
        return statsRowView;
    }

    private static TMP_Text FindRangeLabelText(Transform statsRow)
    {
        if (statsRow == null)
        {
            return null;
        }

        return FindChildText(statsRow, "RangeText")
            ?? FindChildText(statsRow.Find("RangeGroup"), "RangeText");
    }

    private static TMP_Text FindChildText(Transform parent, string childName)
    {
        if (parent == null)
        {
            return null;
        }

        return FindDeepChild(parent, childName)?.GetComponent<TMP_Text>();
    }

    private static void ConfigureTopLeftRect(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(0f, size.y);
        if (size.x > 0f)
        {
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = size;
        }
    }
}
#endif
