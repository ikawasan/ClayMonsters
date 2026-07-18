#if UNITY_EDITOR
using ClayEditor;
using Extensions;
using SaveData;
using Scene.BattleNpcScene.View;
using Scene.ClayEditScene;
using Scene.ClayEditScene.View;
using Scene.TitleScene;
using Scene.TitleScene.View;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UI.ClayEditor.View;
using UI.Option.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityScene = UnityEngine.SceneManagement.Scene;

/// <summary>
/// 実行時生成されていたUIをシーンとプレハブへ配置する
/// Toolsメニューから一括実行する
/// </summary>
public static class SceneUiPlacementMigrator
{
    private const string TitleScenePath = "Assets/Scenes/Title.unity";
    private const string ClayEditScenePath = "Assets/Scenes/ClayEdit.unity";
    private const string BattleNpcScenePath = "Assets/Scenes/BattleNpc.unity";
    private const string OptionPrefabPath = "Assets/Scripts/StaticResources/ClayMonstersLifetimeScope.prefab";
    private const string LogoSpritePath = "Assets/Resources/Image/Title/ClayMonsters_Logo_Title.png";
    private const string PanelSpritePath = "Assets/Resources/Image/Title/TitleOptionPanel.png";
    private const string ButtonNormalSpritePath = "Assets/Resources/Image/Title/TitleMenuButton_Normal.png";
    private const string ButtonHighlightedSpritePath = "Assets/Resources/Image/Title/TitleMenuButton_Highlighted.png";
    private const string ButtonPressedSpritePath = "Assets/Resources/Image/Title/TitleMenuButton_Pressed.png";
    private const string ClayMaterialPath = "Assets/Resources/Material/ClayMonster/M_ClayMonster.mat";

    /// <summary>
    /// シーン・プレハブに未配置の動的UI参照が残っているか判定する
    /// </summary>
    public static bool NeedsMigration()
    {
        return SceneHasNullRef(TitleScenePath, "titleLogoImage: {fileID: 0}")
            || SceneHasNullRef(TitleScenePath, "battlePvpButton: {fileID: 0}")
            || SceneHasNullRef(TitleScenePath, "trainingButton: {fileID: 0}")
            || !SceneContains(TitleScenePath, "m_Name: TitleMessageWindowCanvas")
            || SceneHasNullRef(TitleScenePath, "messageText: {fileID: 0}")
            || SceneHasNullRef(ClayEditScenePath, "saveConfirmView: {fileID: 0}")
            || SceneHasNullRef(ClayEditScenePath, "confirmSaveButton: {fileID: 0}")
            || SceneHasNullRef(ClayEditScenePath, "nameInputBackButton: {fileID: 0}")
            || !SceneContains(ClayEditScenePath, "m_Name: SlotScrollList")
            || SceneHasNullRef(BattleNpcScenePath, "overlayCanvas: {fileID: 0}")
            || !SceneContains(BattleNpcScenePath, "m_Name: SlotScrollList")
            || PrefabHasNullPanelImage(OptionPrefabPath);
    }

    public static void MigrateAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        ModelSaveSlotUiPrefabUtility.EnsurePrefabAssetsExist();
        MigrateTitleScene();
        MigrateOptionPrefab();
        MigrateClayEditScene();
        MigrateBattleNpcScene();
        MigrateBattlePvpAuxiliaryUi();
        MigrateBattleStartOverlayExtrasInAllScenes();

        EditorUtility.DisplayDialog(
            "Scene UI Migration",
            "未配置UIの補完と参照配線を完了しました\n"
                + "既存のレイアウトは変更していません\n"
                + "PvP補助UI・BattleOverlay拡張も含みます\n"
                + "各シーンを開いてHierarchyを確認してください\n"
                + "変更したシーンは未保存です。必要に応じて手動で保存してください",
            "OK");
    }

    public static void MigrateTitleScene()
    {
        UnityScene scene = EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);
        TitleView titleView = Object.FindFirstObjectByType<TitleView>();
        if (titleView == null)
        {
            Debug.LogError("[SceneUiPlacementMigrator] TitleView not found");
            return;
        }

        Transform titleViewTransform = titleView.transform;
        Image titleLogo = EnsureTitleLogo(titleViewTransform);
        Component battlePvpButton = EnsureBattlePvpButton(titleViewTransform);
        Component trainingButton = EnsureTrainingButton(titleViewTransform);
        TitleMessageWindowView messageWindowView = EnsureTitleMessageWindow(titleViewTransform);

        SerializedObject serializedTitleView = new SerializedObject(titleView);
        serializedTitleView.FindProperty("titleLogoImage").objectReferenceValue = titleLogo;
        serializedTitleView.FindProperty("battlePvpButton").objectReferenceValue = battlePvpButton;
        serializedTitleView.FindProperty("trainingButton").objectReferenceValue = trainingButton;
        serializedTitleView.ApplyModifiedPropertiesWithoutUndo();
        ApplyTitleMenuUi(titleView);

        TitleLifetimeScope lifetimeScope = Object.FindFirstObjectByType<TitleLifetimeScope>();
        if (lifetimeScope != null)
        {
            SerializedObject serializedLifetimeScope = new SerializedObject(lifetimeScope);
            serializedLifetimeScope.FindProperty("messageWindowView").objectReferenceValue = messageWindowView;
            serializedLifetimeScope.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(lifetimeScope);
        }

        EditorUtility.SetDirty(titleView);
        SceneUiEditorSavePolicy.MarkDirty(scene);
    }

    /// <summary>
    /// バッチモード用のTitleシーンUI配置
    /// Unity.exe -batchmode -quit -projectPath ... -executeMethod SceneUiPlacementMigrator.MigrateTitleSceneSilent
    /// </summary>
    public static void MigrateTitleSceneSilent()
    {
        MigrateTitleScene();
        SceneUiEditorSavePolicy.MarkDirtyAndSave(
            EditorSceneManager.GetSceneByPath(TitleScenePath));
        AssetDatabase.SaveAssets();
    }

    public static void MigrateOptionPrefab()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(OptionPrefabPath);
        OptionView optionView = prefabRoot.GetComponentInChildren<OptionView>(true);
        if (optionView == null)
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
            Debug.LogError("[SceneUiPlacementMigrator] OptionView not found");
            return;
        }

        Transform optionViewTransform = optionView.transform;
        Transform existingPanel = optionViewTransform.Find("OptionPanel");
        if (existingPanel == null)
        {
            var panelObject = new GameObject(
                "OptionPanel",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            panelObject.transform.SetParent(optionViewTransform, false);
            panelObject.transform.SetSiblingIndex(1);

            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(560f, 860f);
            panelRect.anchoredPosition = Vector2.zero;

            Image panelImage = panelObject.GetComponent<Image>();
            Sprite panelSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PanelSpritePath);
            if (panelSprite != null)
            {
                panelImage.sprite = panelSprite;
                panelImage.type = Image.Type.Sliced;
            }

            panelImage.color = Color.white;
            panelImage.raycastTarget = false;
            existingPanel = panelObject.transform;
        }

        SerializedObject serializedOptionView = new SerializedObject(optionView);
        serializedOptionView.FindProperty("panelImage").objectReferenceValue = existingPanel.GetComponent<Image>();
        serializedOptionView.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(optionView);
        PrefabUtility.SaveAsPrefabAsset(prefabRoot, OptionPrefabPath);
        PrefabUtility.UnloadPrefabContents(prefabRoot);
    }

    public static void MigrateClayEditScene()
    {
        ModelSaveSlotUiPrefabUtility.EnsurePrefabAssetsExist();
        UnityScene scene = EditorSceneManager.OpenScene(ClayEditScenePath, OpenSceneMode.Single);
        MigrateClayEditSaveSlotUiOnly();
        MigrateOperationGuideChevron();
        MigrateClayEditEntryFlowUi();
        SceneUiEditorSavePolicy.MarkDirty(scene);
    }

    /// <summary>
    /// ClayEditのSaveSlotCanvasとRemakeLoadSlotCanvasのSlotButtonsのみ移行する
    /// </summary>
    public static void MigrateClayEditSaveSlotUiOnly()
    {
        MigrateSaveSlotUi();
        SaveSlotView saveSlotView = Object.FindFirstObjectByType<SaveSlotView>();
        if (saveSlotView != null)
        {
            Transform uiRoot = saveSlotView.transform.parent;
            if (uiRoot != null)
            {
                MigrateClayEditRemakeLoadSlotCanvas(uiRoot);
            }
        }
    }

    /// <summary>
    /// バッチモード用のClayEditシーンUI移行
    /// Unity.exe -batchmode -quit -projectPath ... -executeMethod SceneUiPlacementMigrator.MigrateClayEditSceneSilent
    /// </summary>
    public static void MigrateClayEditSceneSilent()
    {
        MigrateClayEditScene();
        SceneUiEditorSavePolicy.MarkDirtyAndSave(
            EditorSceneManager.GetSceneByPath(ClayEditScenePath));
        AssetDatabase.SaveAssets();
    }

    public static void MigrateBattleNpcScene()
    {
        UnityScene scene = EditorSceneManager.OpenScene(BattleNpcScenePath, OpenSceneMode.Single);
        EnsureBattleStartOverlayReferencesInActiveScene();
        MigrateLoadSlotUi();
        EnsureBattleNpcTitleReturnButton();
        SceneUiEditorSavePolicy.MarkDirty(scene);
    }

    private static void EnsureBattleNpcTitleReturnButton()
    {
        var flowRunner = Object.FindFirstObjectByType<Scene.BattleNpcScene.BattleFlowRunner>(FindObjectsInactive.Include);
        if (flowRunner == null)
        {
            Debug.LogError("[SceneUiPlacementMigrator] BattleFlowRunner not found");
            return;
        }

        SerializedObject serializedRunner = new SerializedObject(flowRunner);
        Canvas selectionCanvas = serializedRunner.FindProperty("selectionCanvas").objectReferenceValue as Canvas;
        if (selectionCanvas == null)
        {
            LoadSlotView loadSlotView = Object.FindFirstObjectByType<LoadSlotView>(FindObjectsInactive.Include);
            selectionCanvas = loadSlotView != null ? loadSlotView.GetComponent<Canvas>() : null;
        }

        if (selectionCanvas == null)
        {
            Debug.LogError("[SceneUiPlacementMigrator] BattleNpc selection canvas not found");
            return;
        }

        Component titleReturnButton = CreateActionButton(
            selectionCanvas.transform,
            "TitleReturnButton",
            "タイトルへ戻る",
            Vector2.zero,
            primary: false);
        ApplyTopLeftButtonLayout(titleReturnButton);

        serializedRunner.FindProperty("titleReturnButton").objectReferenceValue = titleReturnButton;
        serializedRunner.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(flowRunner);
    }

    private static void ApplyTopLeftButtonLayout(Component button)
    {
        if (button == null)
        {
            return;
        }

        RectTransform rectTransform = button.transform as RectTransform;
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(0f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.anchoredPosition = new Vector2(40f, -40f);
        rectTransform.sizeDelta = new Vector2(240f, 64f);
    }

    private static void MigrateLoadSlotUi()
    {
        LoadSlotView loadSlotView = Object.FindFirstObjectByType<LoadSlotView>(FindObjectsInactive.Include);
        if (loadSlotView == null)
        {
            Debug.LogError("[SceneUiPlacementMigrator] LoadSlotView not found");
            return;
        }

        SerializedObject serializedLoadSlot = new SerializedObject(loadSlotView);
        SerializedProperty slotScrollListProperty = serializedLoadSlot.FindProperty("slotScrollList");
        ModelSaveSlotScrollListView scrollList = slotScrollListProperty != null
            ? slotScrollListProperty.objectReferenceValue as ModelSaveSlotScrollListView
            : null;
        if (scrollList == null
            || !ModelSaveSlotUiPrefabUtility.IsScrollListPrefabInstance(scrollList.gameObject))
        {
            scrollList = ModelSaveSlotUiPrefabUtility.EnsureScrollListPrefabInstance(
                loadSlotView.transform);
            if (slotScrollListProperty != null)
            {
                slotScrollListProperty.objectReferenceValue = scrollList;
            }
        }
        else
        {
            ModelSaveSlotUiPrefabUtility.WireScrollListReferencesOnly(scrollList);
        }

        SerializedProperty selectionCanvasProperty = serializedLoadSlot.FindProperty("selectionCanvas");
        Canvas selectionCanvas = selectionCanvasProperty != null
            ? selectionCanvasProperty.objectReferenceValue as Canvas
            : null;
        if (selectionCanvas == null)
        {
            selectionCanvas = loadSlotView.GetComponent<Canvas>();
            if (selectionCanvasProperty != null)
            {
                selectionCanvasProperty.objectReferenceValue = selectionCanvas;
            }
        }

        if (selectionCanvas != null)
        {
            ModelSaveSlotScrollListView.EnsureSelectionBackground(selectionCanvas);
        }

        SerializedProperty confirmPanelRootProperty = serializedLoadSlot.FindProperty("confirmPanelRoot");
        GameObject confirmPanelRoot = confirmPanelRootProperty != null
            ? confirmPanelRootProperty.objectReferenceValue as GameObject
            : null;
        if (confirmPanelRoot == null)
        {
            Transform confirmRoot = loadSlotView.transform.Find("ConfirmPanel")
                ?? loadSlotView.transform.Find("ConfirmSaveSlotCanvas");
            confirmPanelRoot = confirmRoot != null ? confirmRoot.gameObject : null;
            if (confirmPanelRootProperty != null && confirmPanelRoot != null)
            {
                confirmPanelRootProperty.objectReferenceValue = confirmPanelRoot;
            }
        }

        if (confirmPanelRoot != null)
        {
            ModelSaveSlotScrollListView.EnsureSelectionBackground(confirmPanelRoot.transform);
            ModelSaveConfirmView confirmView =
                ModelSaveSlotUiPrefabUtility.EnsureConfirmViewOnRoot(confirmPanelRoot.transform);
            SerializedProperty loadConfirmViewProperty = serializedLoadSlot.FindProperty("loadConfirmView");
            if (loadConfirmViewProperty != null)
            {
                loadConfirmViewProperty.objectReferenceValue = confirmView;
            }

            EnsureLoadConfirmButtons(confirmPanelRoot.transform, serializedLoadSlot);
        }

        CanvasGroup legacyRootGroup = loadSlotView.GetComponent<CanvasGroup>();
        if (legacyRootGroup != null)
        {
            Object.DestroyImmediate(legacyRootGroup);
        }

        serializedLoadSlot.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(loadSlotView);
    }

    private static void MigrateSaveSlotUi()
    {
        SaveSlotView saveSlotView = Object.FindFirstObjectByType<SaveSlotView>();
        if (saveSlotView == null)
        {
            Debug.LogError("[SceneUiPlacementMigrator] SaveSlotView not found");
            return;
        }

        SerializedObject serializedSaveSlot = new SerializedObject(saveSlotView);
        Canvas saveConfirmCanvas = serializedSaveSlot.FindProperty("saveConfirmCanvas").objectReferenceValue as Canvas;
        Canvas slotCanvas = serializedSaveSlot.FindProperty("slotCanvas").objectReferenceValue as Canvas;
        Canvas nameInputCanvas = serializedSaveSlot.FindProperty("nameInputCanvas").objectReferenceValue as Canvas;

        if (saveConfirmCanvas != null)
        {
            ModelSaveConfirmView confirmView = saveConfirmCanvas.GetComponentInChildren<ModelSaveConfirmView>(true);
            if (confirmView == null)
            {
                var viewObject = new GameObject(
                    "ModelSaveConfirmView",
                    typeof(RectTransform),
                    typeof(ModelSaveConfirmView));
                viewObject.transform.SetParent(saveConfirmCanvas.transform, false);
                RectTransform rect = viewObject.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                confirmView = viewObject.GetComponent<ModelSaveConfirmView>();
            }

            EnsureConfirmContent(confirmView);
            serializedSaveSlot.FindProperty("saveConfirmView").objectReferenceValue = confirmView;
            EnsureConfirmButtons(saveConfirmCanvas.transform, serializedSaveSlot);
        }

        if (nameInputCanvas != null)
        {
            EnsureNameInputButtons(nameInputCanvas.transform, serializedSaveSlot);
        }

        if (slotCanvas != null)
        {
            ModelSaveSlotScrollListView scrollList = serializedSaveSlot.FindProperty("slotScrollList").objectReferenceValue
                as ModelSaveSlotScrollListView;
            if (scrollList == null
                || !ModelSaveSlotUiPrefabUtility.IsScrollListPrefabInstance(scrollList.gameObject))
            {
                scrollList = ModelSaveSlotUiPrefabUtility.EnsureScrollListPrefabInstance(
                    slotCanvas.transform);
                serializedSaveSlot.FindProperty("slotScrollList").objectReferenceValue = scrollList;
            }
            else
            {
                ModelSaveSlotUiPrefabUtility.WireScrollListReferencesOnly(scrollList);
            }

            ModelSaveSlotScrollListView.EnsureSelectionBackground(slotCanvas);
        }

        Canvas slotActionCanvas = EnsureChildCanvas(saveSlotView.transform, "SlotActionCanvas", 101, enabled: false);
        ModelSaveSlotScrollListView.EnsureSelectionBackground(slotActionCanvas);
        ModelSaveConfirmView slotActionConfirmView = ModelSaveSlotUiPrefabUtility.EnsureConfirmViewOnCanvas(slotActionCanvas);
        Component slotActionOverwriteButton = CreateActionButton(
            slotActionCanvas.transform,
            "SlotActionOverwriteButton",
            "上書き保存",
            new Vector2(320f, -310f),
            true);
        Component slotActionDeleteButton = CreateActionButton(
            slotActionCanvas.transform,
            "SlotActionDeleteButton",
            "削除",
            new Vector2(0f, -310f),
            primary: false,
            danger: true);
        Component slotActionBackButton = CreateActionButton(
            slotActionCanvas.transform,
            "SlotActionBackButton",
            "戻る",
            new Vector2(-320f, -310f),
            false);
        ModelSaveSlotDeletePromptView slotActionDeletePromptView =
            EnsureModelSaveSlotDeletePrompt(saveSlotView.transform, "SlotActionDeletePromptCanvas", 102);

        serializedSaveSlot.FindProperty("slotActionCanvas").objectReferenceValue = slotActionCanvas;
        serializedSaveSlot.FindProperty("slotActionConfirmView").objectReferenceValue = slotActionConfirmView;
        serializedSaveSlot.FindProperty("slotActionOverwriteButton").objectReferenceValue = slotActionOverwriteButton;
        serializedSaveSlot.FindProperty("slotActionDeleteButton").objectReferenceValue = slotActionDeleteButton;
        serializedSaveSlot.FindProperty("slotActionBackButton").objectReferenceValue = slotActionBackButton;
        serializedSaveSlot.FindProperty("slotActionDeletePromptView").objectReferenceValue = slotActionDeletePromptView;

        if (nameInputCanvas != null)
        {
            ModelSaveSlotScrollListView.EnsureSelectionBackground(nameInputCanvas);
        }

        if (saveConfirmCanvas != null)
        {
            ModelSaveSlotScrollListView.EnsureSelectionBackground(saveConfirmCanvas);
        }

        serializedSaveSlot.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(saveSlotView);
    }

    private static void MigrateOperationGuideChevron()
    {
        ClayEditOperationGuideView guideView = Object.FindFirstObjectByType<ClayEditOperationGuideView>();
        if (guideView == null)
        {
            return;
        }

        SerializedObject serializedGuide = new SerializedObject(guideView);
        Toggle visibilityToggle = serializedGuide.FindProperty("visibilityToggle").objectReferenceValue as Toggle;
        if (visibilityToggle == null)
        {
            return;
        }

        Transform background = visibilityToggle.transform.Find("Background");
        if (background == null)
        {
            return;
        }

        Transform chevron = background.Find("Chevron");
        if (chevron == null)
        {
            chevron = CreateChevron(background).transform;
        }

        serializedGuide.FindProperty("chevronIcon").objectReferenceValue = chevron as RectTransform;
        serializedGuide.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(guideView);
    }

    private static Image EnsureTitleLogo(Transform titleViewTransform)
    {
        Transform existing = titleViewTransform.Find("TitleLogo");
        GameObject logoObject;
        if (existing != null)
        {
            logoObject = existing.gameObject;
        }
        else
        {
            logoObject = new GameObject("TitleLogo", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            logoObject.transform.SetParent(titleViewTransform, false);
            logoObject.transform.SetAsFirstSibling();
        }

        Image image = logoObject.GetComponent<Image>();
        Sprite logoSprite = AssetDatabase.LoadAssetAtPath<Sprite>(LogoSpritePath);
        if (logoSprite != null)
        {
            image.sprite = logoSprite;
        }

        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.color = new Color(1f, 0.965f, 0.92f, 1f);

        Shadow shadow = logoObject.GetComponent<Shadow>();
        if (shadow == null)
        {
            shadow = logoObject.AddComponent<Shadow>();
        }

        shadow.effectColor = new Color(0.16f, 0.1f, 0.06f, 0.52f);
        shadow.effectDistance = new Vector2(10f, -14f);
        shadow.useGraphicAlpha = true;

        Outline outline = logoObject.GetComponent<Outline>();
        if (outline == null)
        {
            outline = logoObject.AddComponent<Outline>();
        }

        outline.effectColor = new Color(0.2f, 0.13f, 0.08f, 0.28f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        outline.useGraphicAlpha = true;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(40f, -28f);
        float aspect = logoSprite != null && logoSprite.rect.height > 0f
            ? logoSprite.rect.width / logoSprite.rect.height
            : 3.28f;
        float width = 760f;
        rect.sizeDelta = new Vector2(width, width / aspect);
        return image;
    }

    private static TitleMessageWindowView EnsureTitleMessageWindow(Transform titleViewTransform)
    {
        TitleMessageWindowView view = titleViewTransform.GetComponent<TitleMessageWindowView>();
        if (view == null)
        {
            view = titleViewTransform.gameObject.AddComponent<TitleMessageWindowView>();
        }

        GameObject rootObject = EnsureTitleMessageWindowRoot(titleViewTransform);
        Canvas canvas = rootObject.GetComponent<Canvas>();
        EnsureTitleMessageWindowBlocker(rootObject.transform);
        RectTransform panelRect = EnsureTitleMessageWindowPanel(rootObject.transform);
        TMP_Text messageText = EnsureTitleMessageWindowText(panelRect);
        Component okButton = EnsureTitleMessageWindowOkButton(panelRect);

        SerializedObject serializedView = new SerializedObject(view);
        serializedView.FindProperty("canvas").objectReferenceValue = canvas;
        serializedView.FindProperty("messageText").objectReferenceValue = messageText;
        serializedView.FindProperty("okButton").objectReferenceValue = okButton;
        serializedView.ApplyModifiedPropertiesWithoutUndo();

        canvas.enabled = false;
        EditorUtility.SetDirty(view);
        return view;
    }

    private static GameObject EnsureTitleMessageWindowRoot(Transform titleViewTransform)
    {
        const int canvasSortingOrder = 150;
        Transform existing = titleViewTransform.Find("TitleMessageWindowCanvas");
        GameObject rootObject;
        if (existing != null)
        {
            rootObject = existing.gameObject;
        }
        else
        {
            rootObject = new GameObject(
                "TitleMessageWindowCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            rootObject.transform.SetParent(titleViewTransform, false);
        }

        RectTransform rootRect = rootObject.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Canvas canvas = rootObject.GetComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = canvasSortingOrder;

        CanvasScaler scaler = rootObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        return rootObject;
    }

    private static void EnsureTitleMessageWindowBlocker(Transform parent)
    {
        const float blockerAlpha = 0.72f;
        Transform existing = parent.Find("BackgroundBlocker");
        GameObject blockerObject = existing != null
            ? existing.gameObject
            : new GameObject("BackgroundBlocker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));

        if (existing == null)
        {
            blockerObject.transform.SetParent(parent, false);
        }

        RectTransform blockerRect = blockerObject.GetComponent<RectTransform>();
        blockerRect.anchorMin = Vector2.zero;
        blockerRect.anchorMax = Vector2.one;
        blockerRect.offsetMin = Vector2.zero;
        blockerRect.offsetMax = Vector2.zero;

        Image blockerImage = blockerObject.GetComponent<Image>();
        blockerImage.color = new Color(0.12f, 0.08f, 0.06f, blockerAlpha);
        blockerImage.raycastTarget = true;
    }

    private static RectTransform EnsureTitleMessageWindowPanel(Transform parent)
    {
        const float panelWidth = 720f;
        const float panelHeight = 280f;
        Transform existing = parent.Find("MessagePanel");
        GameObject panelObject = existing != null
            ? existing.gameObject
            : new GameObject("MessagePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));

        if (existing == null)
        {
            panelObject.transform.SetParent(parent, false);
        }

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(panelWidth, panelHeight);

        Image panelImage = panelObject.GetComponent<Image>();
        Sprite panelSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PanelSpritePath)
            ?? AssetDatabase.LoadAssetAtPath<Sprite>(ButtonNormalSpritePath);
        if (panelSprite != null)
        {
            panelImage.sprite = panelSprite;
            panelImage.type = Image.Type.Sliced;
        }

        panelImage.color = Color.white;
        panelImage.raycastTarget = true;
        return panelRect;
    }

    private static TMP_Text EnsureTitleMessageWindowText(RectTransform panelRect)
    {
        const float fontSize = 30f;
        Transform existing = panelRect.Find("MessageText");
        GameObject textObject = existing != null
            ? existing.gameObject
            : new GameObject("MessageText", typeof(RectTransform), typeof(TextMeshProUGUI));

        if (existing == null)
        {
            textObject.transform.SetParent(panelRect, false);
        }

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 0.35f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = new Vector2(40f, 0f);
        textRect.offsetMax = new Vector2(-40f, -32f);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        TMP_FontAsset fontAsset = AppTmpFontUtility.DefaultFont;
        if (fontAsset != null)
        {
            text.font = fontAsset;
        }

        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.color = new Color(0.36f, 0.24f, 0.18f, 1f);
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    private static Component EnsureTitleMessageWindowOkButton(RectTransform panelRect)
    {
        Transform existing = panelRect.Find("OkButton");
        GameObject buttonObject;
        if (existing != null)
        {
            Component existingButton = SceneUiLhButtonUtility.GetComponent(existing.gameObject);
            if (existingButton != null)
            {
                return existingButton;
            }

            buttonObject = existing.gameObject;
        }
        else
        {
            buttonObject = new GameObject(
                "OkButton",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            buttonObject.transform.SetParent(panelRect, false);
        }

        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0f);
        buttonRect.anchorMax = new Vector2(0.5f, 0f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(0f, 48f);
        buttonRect.sizeDelta = new Vector2(240f, 64f);

        Component button = SceneUiLhButtonUtility.GetComponent(buttonObject)
            ?? SceneUiLhButtonUtility.AddComponent(buttonObject);
        Image image = buttonObject.GetComponent<Image>();
        Sprite normalSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ButtonNormalSpritePath);
        Sprite highlightedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ButtonHighlightedSpritePath) ?? normalSprite;
        Sprite pressedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ButtonPressedSpritePath) ?? normalSprite;
        if (normalSprite != null)
        {
            image.sprite = normalSprite;
            image.type = Image.Type.Sliced;
        }

        image.color = Color.white;
        ApplyTitleMenuButtonStyle(button, image, normalSprite, highlightedSprite, pressedSprite);

        Transform labelTransform = buttonObject.transform.Find("Label");
        GameObject labelObject = labelTransform != null
            ? labelTransform.gameObject
            : new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        if (labelTransform == null)
        {
            labelObject.transform.SetParent(buttonObject.transform, false);
        }

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI labelText = labelObject.GetComponent<TextMeshProUGUI>();
        TMP_FontAsset fontAsset = AppTmpFontUtility.DefaultFont;
        if (fontAsset != null)
        {
            labelText.font = fontAsset;
        }

        labelText.text = "OK";
        labelText.fontSize = 24f;
        labelText.fontStyle = FontStyles.Bold;
        labelText.color = new Color(0.36f, 0.24f, 0.18f, 1f);
        labelText.raycastTarget = false;
        return button;
    }

    private static void ApplyTitleMenuButtonStyle(
        Component button,
        Image image,
        Sprite normalSprite,
        Sprite highlightedSprite,
        Sprite pressedSprite)
    {
        if (button == null)
        {
            return;
        }

        UnityEngine.UI.Selectable selectable = button as UnityEngine.UI.Selectable;
        if (selectable == null || image == null)
        {
            return;
        }

        selectable.targetGraphic = image;
        selectable.transition = UnityEngine.UI.Selectable.Transition.SpriteSwap;
        SpriteState spriteState = selectable.spriteState;
        spriteState.highlightedSprite = highlightedSprite;
        spriteState.pressedSprite = pressedSprite;
        spriteState.selectedSprite = highlightedSprite;
        spriteState.disabledSprite = normalSprite;
        selectable.spriteState = spriteState;

        ColorBlock colors = selectable.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.pressedColor = Color.white;
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(1f, 1f, 1f, 0.55f);
        colors.fadeDuration = 0.08f;
        selectable.colors = colors;
    }

    private static void ApplyTitleMenuUi(TitleView titleView)
    {
        SerializedObject serializedTitleView = new SerializedObject(titleView);
        Component quitGameButton = serializedTitleView.FindProperty("quitGameButton").objectReferenceValue as Component;
        Component optionButton = serializedTitleView.FindProperty("optionButton").objectReferenceValue as Component;
        Component battlePvpButton = serializedTitleView.FindProperty("battlePvpButton").objectReferenceValue as Component;
        Component battleNpcButton = serializedTitleView.FindProperty("battleNpcButton").objectReferenceValue as Component;
        Component trainingButton = serializedTitleView.FindProperty("trainingButton").objectReferenceValue as Component;
        Component clayEditButton = serializedTitleView.FindProperty("clayEditButton").objectReferenceValue as Component;
        Image titleLogoImage = serializedTitleView.FindProperty("titleLogoImage").objectReferenceValue as Image;

        ApplyTitleMenuButtonLayout(quitGameButton, 0);
        ApplyTitleMenuButtonLayout(optionButton, 1);
        ApplyTitleMenuButtonLayout(battlePvpButton, 2);
        ApplyTitleMenuButtonLayout(battleNpcButton, 3);
        ApplyTitleMenuButtonLayout(trainingButton, 4);
        ApplyTitleMenuButtonLayout(clayEditButton, 5);

        SetTitleMenuButtonLabel(clayEditButton, "モンスターエディット");
        SetTitleMenuButtonLabel(trainingButton, "育成");
        SetTitleMenuButtonLabel(battleNpcButton, "CPU戦");
        SetTitleMenuButtonLabel(battlePvpButton, "対人戦");

        Sprite normalSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ButtonNormalSpritePath);
        Sprite highlightedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ButtonHighlightedSpritePath) ?? normalSprite;
        Sprite pressedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ButtonPressedSpritePath) ?? normalSprite;
        if (normalSprite == null)
        {
            return;
        }

        ApplyTitleMenuButtonVisual(clayEditButton, normalSprite, highlightedSprite, pressedSprite);
        ApplyTitleMenuButtonVisual(trainingButton, normalSprite, highlightedSprite, pressedSprite);
        ApplyTitleMenuButtonVisual(battleNpcButton, normalSprite, highlightedSprite, pressedSprite);
        ApplyTitleMenuButtonVisual(battlePvpButton, normalSprite, highlightedSprite, pressedSprite);
        ApplyTitleMenuButtonVisual(optionButton, normalSprite, highlightedSprite, pressedSprite);
        ApplyTitleMenuButtonVisual(quitGameButton, normalSprite, highlightedSprite, pressedSprite);

        if (titleLogoImage != null)
        {
            titleLogoImage.raycastTarget = false;
        }
    }

    private static void ApplyTitleMenuButtonLayout(Component button, int indexFromBottom)
    {
        const float leftMargin = 48f;
        const float bottomMargin = 48f;
        const float buttonSpacing = 76f;
        const float buttonWidth = 440f;
        const float buttonHeight = 72f;

        if (button == null)
        {
            return;
        }

        var container = button.transform.parent as RectTransform;
        if (container == null)
        {
            return;
        }

        container.anchorMin = new Vector2(0f, 0f);
        container.anchorMax = new Vector2(0f, 0f);
        container.pivot = new Vector2(0f, 0f);
        container.sizeDelta = new Vector2(buttonWidth, buttonHeight);
        container.anchoredPosition = new Vector2(
            leftMargin,
            bottomMargin + buttonSpacing * indexFromBottom);
    }

    private static void SetTitleMenuButtonLabel(Component button, string text)
    {
        if (button == null)
        {
            return;
        }

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.text = text;
        }
    }

    private static void ApplyTitleMenuButtonVisual(
        Component button,
        Sprite normalSprite,
        Sprite highlightedSprite,
        Sprite pressedSprite)
    {
        if (button == null)
        {
            return;
        }

        Image image = button.GetComponent<Image>();
        if (image == null)
        {
            return;
        }

        image.sprite = normalSprite;
        image.type = Image.Type.Sliced;
        image.color = Color.white;
        image.pixelsPerUnitMultiplier = 1f;
        ApplyTitleMenuButtonStyle(button, image, normalSprite, highlightedSprite, pressedSprite);

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.color = new Color(0.36f, 0.24f, 0.18f, 1f);
            label.fontStyle = FontStyles.Bold;
            label.margin = new Vector4(28f, 0f, 20f, 0f);
            label.raycastTarget = false;
        }
    }

    private static Component EnsureBattlePvpButton(Transform titleViewTransform)
    {
        Transform existing = titleViewTransform.Find("BattlePvpButton");
        if (existing != null)
        {
            return SceneUiLhButtonUtility.FindInChildren(existing);
        }

        Transform template = titleViewTransform.Find("ContinueButton");
        if (template == null)
        {
            for (int i = 0; i < titleViewTransform.childCount; i++)
            {
                Transform child = titleViewTransform.GetChild(i);
                if (SceneUiLhButtonUtility.FindInChildren(child) != null)
                {
                    template = child;
                    break;
                }
            }
        }

        if (template == null)
        {
            Debug.LogError("[SceneUiPlacementMigrator] button template not found");
            return null;
        }

        GameObject clone = Object.Instantiate(template.gameObject, titleViewTransform);
        clone.name = "BattlePvpButton";
        clone.transform.SetSiblingIndex(template.GetSiblingIndex() + 1);

        TMP_Text label = clone.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.text = "対人戦";
        }

        return SceneUiLhButtonUtility.FindInChildren(clone.transform);
    }

    private static Component EnsureTrainingButton(Transform titleViewTransform)
    {
        Transform existing = titleViewTransform.Find("TrainingButton");
        if (existing != null)
        {
            return SceneUiLhButtonUtility.FindInChildren(existing);
        }

        Transform template = titleViewTransform.Find("BattleNpcButton");
        if (template == null)
        {
            template = titleViewTransform.Find("ClayEditButton");
        }

        if (template == null)
        {
            Debug.LogError("[SceneUiPlacementMigrator] button template not found for Training");
            return null;
        }

        GameObject clone = Object.Instantiate(template.gameObject, titleViewTransform);
        clone.name = "TrainingButton";
        clone.transform.SetSiblingIndex(template.GetSiblingIndex() + 1);

        TMP_Text label = clone.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.text = "育成";
        }

        return SceneUiLhButtonUtility.FindInChildren(clone.transform);
    }

    private static void EnsureConfirmContent(ModelSaveConfirmView confirmView)
    {
        Transform viewTransform = confirmView.transform;
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

            ModelSaveSlotUiPrefabUtility.EnsureSaveSlotAttackSlots(rowTransform);
        }

        ModelSaveSlotRowElementRefs rowRefs = rowTransform.GetComponent<ModelSaveSlotRowElementRefs>();
        if (rowRefs == null)
        {
            rowRefs = rowTransform.gameObject.AddComponent<ModelSaveSlotRowElementRefs>();
        }

        rowRefs.EnsureConfirmPrefabLayout();
        if (!rowRefs.HasWiredReferences())
        {
            rowRefs.CaptureFromHierarchy(rowTransform);
        }

        SerializedObject serializedConfirm = new SerializedObject(confirmView);
        serializedConfirm.FindProperty("contentRoot").objectReferenceValue = contentRoot as RectTransform;
        serializedConfirm.FindProperty("rowElementRefs").objectReferenceValue = rowRefs;
        serializedConfirm.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(confirmView);
    }

    /// <summary>
    /// スロット一覧UIをシーンへ構築し参照を配線する
    /// </summary>
    public static void EnsureSlotScrollList(ModelSaveSlotScrollListView scrollList)
    {
        ModelSaveSlotUiPrefabUtility.EnsureScrollListWired(scrollList);
    }

    private static void EnsureConfirmButtons(Transform parent, SerializedObject serializedSaveSlot)
    {
        Component backButton = serializedSaveSlot.FindProperty("confirmBackButton").objectReferenceValue as Component;
        Component saveButton = serializedSaveSlot.FindProperty("confirmSaveButton").objectReferenceValue as Component;
        if (backButton == null)
        {
            backButton = CreateActionButton(parent, "ConfirmBackButton", "戻る", new Vector2(-140f, -120f), false);
            serializedSaveSlot.FindProperty("confirmBackButton").objectReferenceValue = backButton;
        }

        if (saveButton == null)
        {
            saveButton = CreateActionButton(parent, "ConfirmSaveButton", "保存する", new Vector2(140f, -120f), true);
            serializedSaveSlot.FindProperty("confirmSaveButton").objectReferenceValue = saveButton;
        }
    }

    private static void EnsureLoadConfirmButtons(Transform parent, SerializedObject serializedLoadSlot)
    {
        SerializedProperty loadButtonProperty = serializedLoadSlot.FindProperty("loadButton");
        Component loadButton = loadButtonProperty != null
            ? loadButtonProperty.objectReferenceValue as Component
            : null;
        if (loadButton == null)
        {
            loadButton = CreateActionButton(
                parent,
                "LoadConfirmButton",
                "ロードする",
                new Vector2(-190f, 72f),
                true,
                anchorBottom: true);
            if (loadButtonProperty != null)
            {
                loadButtonProperty.objectReferenceValue = loadButton;
            }
        }
        else
        {
            ApplyBottomActionButtonLayout(loadButton, -190f, 72f);
        }

        SerializedProperty backButtonProperty = serializedLoadSlot.FindProperty("backButton");
        Component backButton = backButtonProperty != null
            ? backButtonProperty.objectReferenceValue as Component
            : null;
        if (backButton == null)
        {
            backButton = CreateActionButton(
                parent,
                "LoadConfirmBackButton",
                "戻る",
                new Vector2(190f, 72f),
                false,
                anchorBottom: true);
            if (backButtonProperty != null)
            {
                backButtonProperty.objectReferenceValue = backButton;
            }
        }
        else
        {
            ApplyBottomActionButtonLayout(backButton, 190f, 72f);
        }
    }

    private static void ApplyBottomActionButtonLayout(Component button, float horizontalOffset, float bottomOffset, float width = 340f)
    {
        if (button == null)
        {
            return;
        }

        RectTransform rectTransform = button.transform as RectTransform;
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchorMin = new Vector2(0.5f, 0f);
        rectTransform.anchorMax = new Vector2(0.5f, 0f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = new Vector2(horizontalOffset, bottomOffset);
        rectTransform.sizeDelta = new Vector2(width, 80f);
    }

    private static ModelSaveSlotDeletePromptView EnsureModelSaveSlotDeletePrompt(
        Transform parent,
        string canvasObjectName,
        int sortingOrder)
    {
        Canvas promptCanvas = EnsureChildCanvas(parent, canvasObjectName, sortingOrder, enabled: false);
        ModelSaveSlotScrollListView.EnsureSelectionBackground(promptCanvas);

        ModelSaveSlotDeletePromptView promptView = promptCanvas.GetComponent<ModelSaveSlotDeletePromptView>();
        if (promptView == null)
        {
            promptView = promptCanvas.gameObject.AddComponent<ModelSaveSlotDeletePromptView>();
        }

        Transform panelTransform = promptCanvas.transform.Find("DeletePromptPanel");
        RectTransform panelRect;
        if (panelTransform == null)
        {
            var panelObject = new GameObject(
                "DeletePromptPanel",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            panelObject.transform.SetParent(promptCanvas.transform, false);
            panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(760f, 280f);
            TitleClayUiVisualUtility.ApplyPanelForEditorBake(panelObject.GetComponent<Image>());
        }
        else
        {
            panelRect = panelTransform as RectTransform;
        }

        TMP_Text messageText = panelRect.GetComponentInChildren<TMP_Text>(true);
        if (messageText == null)
        {
            var textObject = new GameObject(
                "DeletePromptMessage",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(panelRect, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0f, 0.35f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.offsetMin = new Vector2(32f, 0f);
            textRect.offsetMax = new Vector2(-32f, -24f);
            messageText = textObject.GetComponent<TextMeshProUGUI>();
            messageText.fontSize = 30f;
            messageText.color = TitleClayUiVisualUtility.LabelColor;
            messageText.textWrappingMode = TextWrappingModes.Normal;
        }

        Component confirmButton = panelRect.Find("DeleteConfirmButton") != null
            ? SceneUiLhButtonUtility.FindInChildren(panelRect.Find("DeleteConfirmButton"))
            : null;
        if (confirmButton == null)
        {
            confirmButton = CreateActionButton(
                panelRect,
                "DeleteConfirmButton",
                "削除する",
                new Vector2(-160f, -88f),
                primary: true,
                danger: true);
        }
        else
        {
            ApplyActionButtonVisual(confirmButton, primary: true, danger: true);
        }

        Component cancelButton = panelRect.Find("DeleteCancelButton") != null
            ? SceneUiLhButtonUtility.FindInChildren(panelRect.Find("DeleteCancelButton"))
            : null;
        if (cancelButton == null)
        {
            cancelButton = CreateActionButton(
                panelRect,
                "DeleteCancelButton",
                "キャンセル",
                new Vector2(160f, -88f),
                primary: false);
        }
        else
        {
            ApplyActionButtonVisual(cancelButton, primary: false);
        }

        Image panelImage = panelRect.GetComponent<Image>();
        if (panelImage != null)
        {
            TitleClayUiVisualUtility.ApplyPanelForEditorBake(panelImage);
        }

        if (messageText != null)
        {
            messageText.color = TitleClayUiVisualUtility.LabelColor;
        }

        SerializedObject serializedPrompt = new SerializedObject(promptView);
        serializedPrompt.FindProperty("canvas").objectReferenceValue = promptCanvas;
        serializedPrompt.FindProperty("messageText").objectReferenceValue = messageText;
        serializedPrompt.FindProperty("confirmButton").objectReferenceValue = confirmButton;
        serializedPrompt.FindProperty("cancelButton").objectReferenceValue = cancelButton;
        serializedPrompt.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(promptView);
        return promptView;
    }

    private static void EnsureNameInputButtons(Transform parent, SerializedObject serializedSaveSlot)
    {
        Component backButton = serializedSaveSlot.FindProperty("nameInputBackButton").objectReferenceValue as Component;
        if (backButton == null)
        {
            backButton = CreateActionButton(
                parent,
                "NameInputBackButton",
                "戻る",
                new Vector2(-160f, -120f),
                false);
            serializedSaveSlot.FindProperty("nameInputBackButton").objectReferenceValue = backButton;
        }

        Component confirmButton = serializedSaveSlot.FindProperty("confirmNameButton").objectReferenceValue as Component;
        if (confirmButton != null)
        {
            ApplyNameInputButtonLayout(confirmButton, 160f);
        }

        if (backButton != null)
        {
            ApplyNameInputButtonLayout(backButton, -160f);
        }
    }

    private static void ApplyNameInputButtonLayout(Component button, float horizontalOffset)
    {
        if (button == null)
        {
            return;
        }

        RectTransform rectTransform = button.transform as RectTransform;
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = new Vector2(horizontalOffset, -120f);
        rectTransform.sizeDelta = new Vector2(240f, 52f);
    }

    private static Component CreateActionButton(
        Transform parent,
        string objectName,
        string label,
        Vector2 anchoredPosition,
        bool primary,
        bool anchorBottom = false,
        bool danger = false)
    {
        Transform existing = parent.Find(objectName);
        if (existing != null)
        {
            Component existingButton = SceneUiLhButtonUtility.GetComponent(existing.gameObject)
                ?? SceneUiLhButtonUtility.AddComponent(existing.gameObject);
            ApplyActionButtonVisual(existingButton, primary, danger);
            EnsureActionButtonLabel(existingButton, label, 24f, danger);
            if (anchorBottom)
            {
                ApplyBottomActionButtonLayout(existingButton, anchoredPosition.x, anchoredPosition.y);
            }

            return existingButton;
        }

        var buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
        if (anchorBottom)
        {
            rectTransform.anchorMin = new Vector2(0.5f, 0f);
            rectTransform.anchorMax = new Vector2(0.5f, 0f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = new Vector2(340f, 80f);
        }
        else
        {
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = new Vector2(200f, 52f);
        }

        Component button = SceneUiLhButtonUtility.AddComponent(buttonObject);
        ApplyActionButtonVisual(button, primary, danger);
        EnsureActionButtonLabel(button, label, 24f, danger);
        return button;
    }

    private static void ApplyActionButtonVisual(Component button, bool primary, bool danger = false)
    {
        if (button == null)
        {
            return;
        }

        if (danger)
        {
            SceneUiLhButtonUtility.ApplyDangerButton(button);
            return;
        }

        if (primary)
        {
            SceneUiLhButtonUtility.ApplyPrimaryButton(button);
            return;
        }

        SceneUiLhButtonUtility.ApplySecondaryButton(button);
    }

    private static void EnsureActionButtonLabel(
        Component button,
        string label,
        float fontSize,
        bool danger = false)
    {
        if (button == null)
        {
            return;
        }

        Transform labelTransform = button.transform.Find("Label");
        GameObject labelObject;
        if (labelTransform != null)
        {
            labelObject = labelTransform.gameObject;
        }
        else
        {
            labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(button.transform, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
        }

        TextMeshProUGUI labelText = labelObject.GetComponent<TextMeshProUGUI>();
        TMP_FontAsset fontAsset = AppTmpFontUtility.DefaultFont;
        if (fontAsset != null)
        {
            labelText.font = fontAsset;
        }

        labelText.text = label;
        labelText.fontSize = fontSize;
        labelText.color = danger
            ? TitleClayUiVisualUtility.DangerLabelColor
            : TitleClayUiVisualUtility.LabelColor;
        labelText.raycastTarget = false;
    }

    private static void EnsureBattleStartOverlayCanvasComponents(GameObject root)
    {
        if (root == null)
        {
            return;
        }

        Canvas canvas = root.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
        }

        if (root.GetComponent<CanvasScaler>() == null)
        {
            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        }

        if (root.GetComponent<GraphicRaycaster>() == null)
        {
            root.AddComponent<GraphicRaycaster>();
        }

        RemoveVisibilityCanvasGroup(root);
    }

    private static void RemoveVisibilityCanvasGroup(GameObject host)
    {
        if (host == null)
        {
            return;
        }

        CanvasGroup group = host.GetComponent<CanvasGroup>();
        if (group != null)
        {
            Object.DestroyImmediate(group);
        }
    }

    private static GameObject CreateBattleStartOverlayRoot(Transform host)
    {
        var root = new GameObject("BattleStartOverlay", typeof(RectTransform));
        root.transform.SetParent(host, false);
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        root.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        root.AddComponent<GraphicRaycaster>();

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        rootRect.localScale = Vector3.one;
        return root;
    }

    private static void FixBattleStartOverlayLayout(GameObject root)
    {
        if (root == null)
        {
            return;
        }

        RectTransform rect = root.GetComponent<RectTransform>();
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
    }

    private static TMP_Text FindOrCreateLabel(Transform parent, string textValue, Vector2 anchoredPosition, float fontSize)
    {
        Transform existing = parent.Find(textValue);
        if (existing != null)
        {
            return existing.GetComponent<TMP_Text>();
        }

        var labelObject = new GameObject(textValue, typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(parent, false);
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(520f, 120f);
        rect.anchoredPosition = anchoredPosition;

        TextMeshProUGUI text = labelObject.GetComponent<TextMeshProUGUI>();
        TMP_FontAsset fontAsset = AppTmpFontUtility.DefaultFont;
        if (fontAsset != null)
        {
            text.font = fontAsset;
        }

        text.text = textValue;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.color = Color.white;
        text.outlineWidth = 0.2f;
        text.outlineColor = Color.black;
        return text;
    }

    private static Image EnsureFinishFlash(Transform parent)
    {
        Transform existing = parent.Find("FinishFlash");
        GameObject flashObject;
        if (existing != null)
        {
            flashObject = existing.gameObject;
        }
        else
        {
            flashObject = new GameObject("FinishFlash", typeof(RectTransform), typeof(Image));
            flashObject.transform.SetParent(parent, false);
            flashObject.transform.SetAsFirstSibling();
            RectTransform flashRect = flashObject.GetComponent<RectTransform>();
            flashRect.anchorMin = Vector2.zero;
            flashRect.anchorMax = Vector2.one;
            flashRect.offsetMin = Vector2.zero;
            flashRect.offsetMax = Vector2.zero;
        }

        Image image = flashObject.GetComponent<Image>();
        image.color = new Color(1f, 0.25f, 0.15f, 0f);
        image.raycastTarget = false;
        return image;
    }

    private static Button EnsureStartButton(Transform parent)
    {
        Transform existing = parent.Find("StartButton");
        if (existing != null)
        {
            return existing.GetComponent<Button>();
        }

        var buttonObject = new GameObject("StartButton", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(280f, 64f);
        rect.anchoredPosition = new Vector2(0f, 168f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.15f, 0.55f, 0.95f, 0.95f);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        TMP_FontAsset fontAsset = AppTmpFontUtility.DefaultFont;
        if (fontAsset != null)
        {
            label.font = fontAsset;
        }

        label.text = "戦闘開始";
        label.fontSize = 30f;
        label.fontStyle = FontStyles.Bold;
        label.color = Color.white;
        return button;
    }

    private static bool SceneHasNullRef(string scenePath, string marker)
    {
        return SceneContains(scenePath, marker);
    }

    private static bool SceneContains(string scenePath, string marker)
    {
        string fullPath = System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(Application.dataPath) ?? string.Empty,
            scenePath);
        if (!System.IO.File.Exists(fullPath))
        {
            return false;
        }

        return System.IO.File.ReadAllText(fullPath).Contains(marker);
    }

    private static bool PrefabHasNullPanelImage(string prefabPath)
    {
        string fullPath = System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(Application.dataPath) ?? string.Empty,
            prefabPath);
        if (!System.IO.File.Exists(fullPath))
        {
            return false;
        }

        string text = System.IO.File.ReadAllText(fullPath);
        if (!text.Contains("panelImage:"))
        {
            return true;
        }

        return text.Contains("panelImage: {fileID: 0}");
    }

    private static TextMeshProUGUI CreateChevron(Transform parent)
    {
        var chevronObject = new GameObject("Chevron", typeof(RectTransform), typeof(TextMeshProUGUI));
        chevronObject.transform.SetParent(parent, false);
        RectTransform chevronRect = chevronObject.GetComponent<RectTransform>();
        chevronRect.anchorMin = Vector2.zero;
        chevronRect.anchorMax = Vector2.one;
        chevronRect.offsetMin = Vector2.zero;
        chevronRect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = chevronObject.GetComponent<TextMeshProUGUI>();
        TMP_FontAsset fontAsset = AppTmpFontUtility.DefaultFont;
        if (fontAsset != null)
        {
            text.font = fontAsset;
        }

        text.text = ">";
        text.fontSize = 20f;
        text.fontStyle = FontStyles.Bold;
        text.color = new Color(0.98f, 0.97f, 0.94f, 1f);
        text.raycastTarget = false;
        return text;
    }

    /// <summary>
    /// ClayEdit入場フロー用CanvasをClayEditView配下へ配置する
    /// </summary>
    public static void MigrateClayEditEntryFlowUi()
    {
        ClayEditView clayEditView = Object.FindFirstObjectByType<ClayEditView>();
        if (clayEditView == null)
        {
            Debug.LogError("[SceneUiPlacementMigrator] ClayEditView not found");
            return;
        }

        Transform uiRoot = clayEditView.transform;
        ClayEditEntryView entryView = MigrateClayEditEntryCanvas(uiRoot);
        ClayEditRemakeLoadSlotView remakeView = MigrateClayEditRemakeLoadSlotCanvas(uiRoot);

        ClayEditLifetimeScope lifetimeScope = Object.FindFirstObjectByType<ClayEditLifetimeScope>();
        if (lifetimeScope == null)
        {
            Debug.LogError("[SceneUiPlacementMigrator] ClayEditLifetimeScope not found");
            return;
        }

        ClayEditEditorUiGate editorUiGate = MigrateClayEditEditorUiGate(lifetimeScope.gameObject);
        SerializedObject serializedScope = new SerializedObject(lifetimeScope);
        serializedScope.FindProperty("clayEditEntryView").objectReferenceValue = entryView;
        serializedScope.FindProperty("clayEditRemakeLoadSlotView").objectReferenceValue = remakeView;
        serializedScope.FindProperty("clayEditEditorUiGate").objectReferenceValue = editorUiGate;
        serializedScope.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(lifetimeScope);
    }

    private static ClayEditEntryView MigrateClayEditEntryCanvas(Transform uiRoot)
    {
        GameObject canvasObject = EnsureOverlayCanvas(uiRoot, "ClayEditEntryCanvas", 120, enabled: false);
        ClayEditEntryView entryView = canvasObject.GetComponent<ClayEditEntryView>();
        if (entryView == null)
        {
            entryView = canvasObject.AddComponent<ClayEditEntryView>();
        }

        Transform blockerRoot = canvasObject.transform.Find("EntryPanel");
        if (blockerRoot == null)
        {
            var blockerObject = new GameObject("EntryPanel", typeof(RectTransform), typeof(Image));
            blockerObject.transform.SetParent(canvasObject.transform, false);
            RectTransform blockerRect = blockerObject.GetComponent<RectTransform>();
            blockerRect.anchorMin = Vector2.zero;
            blockerRect.anchorMax = Vector2.one;
            blockerRect.offsetMin = Vector2.zero;
            blockerRect.offsetMax = Vector2.zero;
            TitleClayUiVisualUtility.ApplyBlockerForEditorBake(blockerObject.GetComponent<Image>());
            blockerRoot = blockerObject.transform;
        }

        Transform panelRoot = blockerRoot.Find("EntryDialogPanel");
        if (panelRoot == null)
        {
            var dialogObject = new GameObject("EntryDialogPanel", typeof(RectTransform), typeof(Image));
            dialogObject.transform.SetParent(blockerRoot, false);
            RectTransform dialogRect = dialogObject.GetComponent<RectTransform>();
            dialogRect.anchorMin = new Vector2(0.5f, 0.5f);
            dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
            dialogRect.pivot = new Vector2(0.5f, 0.5f);
            dialogRect.sizeDelta = new Vector2(560f, 380f);
            dialogRect.anchoredPosition = Vector2.zero;
            TitleClayUiVisualUtility.ApplyPanelForEditorBake(dialogObject.GetComponent<Image>());
            panelRoot = dialogObject.transform;
        }

        EnsureEntryDialogTitle(panelRoot, "モンスターエディット");

        Component newCreateButton = CreateEntryFlowButton(panelRoot, "NewCreateButton", "新規作成", new Vector2(0f, 40f), true);
        Component remakeButton = CreateEntryFlowButton(panelRoot, "RemakeButton", "モンスターを作り直す", new Vector2(0f, -56f), true);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        SerializedObject serializedEntry = new SerializedObject(entryView);
        serializedEntry.FindProperty("useSceneCanvasLayout").boolValue = true;
        serializedEntry.FindProperty("entryCanvas").objectReferenceValue = canvas;
        serializedEntry.FindProperty("newCreateButton").objectReferenceValue = newCreateButton;
        serializedEntry.FindProperty("remakeButton").objectReferenceValue = remakeButton;
        serializedEntry.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(entryView);
        return entryView;
    }

    private static ClayEditRemakeLoadSlotView MigrateClayEditRemakeLoadSlotCanvas(Transform uiRoot)
    {
        GameObject canvasObject = EnsureOverlayCanvas(uiRoot, "ClayEditRemakeLoadSlotCanvas", 115, enabled: false);
        ClayEditRemakeLoadSlotView remakeView = canvasObject.GetComponent<ClayEditRemakeLoadSlotView>();
        if (remakeView == null)
        {
            remakeView = canvasObject.AddComponent<ClayEditRemakeLoadSlotView>();
        }

        ModelSaveSlotScrollListView scrollList =
            ModelSaveSlotUiPrefabUtility.EnsureScrollListPrefabInstance(canvasObject.transform);
        ModelSaveSlotScrollListView.EnsureSelectionBackground(canvasObject.GetComponent<Canvas>());

        Canvas confirmCanvas = EnsureChildCanvas(canvasObject.transform, "RemakeConfirmCanvas", 116, enabled: false);
        ModelSaveSlotScrollListView.EnsureSelectionBackground(confirmCanvas);
        ModelSaveConfirmView confirmView = ModelSaveSlotUiPrefabUtility.EnsureConfirmViewOnCanvas(confirmCanvas);
        Component selectButton = CreateActionButton(
            confirmCanvas.transform,
            "RemakeSelectButton",
            "このモンスターを作り直す",
            new Vector2(-320f, 72f),
            true,
            anchorBottom: true);
        ApplyBottomActionButtonLayout(selectButton, -320f, 72f, 280f);
        Component deleteButton = CreateActionButton(
            confirmCanvas.transform,
            "RemakeDeleteButton",
            "削除",
            new Vector2(0f, 72f),
            primary: false,
            anchorBottom: true,
            danger: true);
        ApplyBottomActionButtonLayout(deleteButton, 0f, 72f, 280f);
        Component confirmBackButton = CreateActionButton(
            confirmCanvas.transform,
            "RemakeConfirmBackButton",
            "戻る",
            new Vector2(320f, 72f),
            false,
            anchorBottom: true);
        ApplyBottomActionButtonLayout(confirmBackButton, 320f, 72f, 280f);
        Component listBackButton = CreateActionButton(
            canvasObject.transform,
            "RemakeListBackButton",
            "戻る",
            new Vector2(0f, 24f),
            false,
            anchorBottom: true);
        ApplyBottomActionButtonLayout(listBackButton, 0f, 24f);
        ModelSaveSlotDeletePromptView deletePromptView =
            EnsureModelSaveSlotDeletePrompt(canvasObject.transform, "RemakeDeletePromptCanvas", 117);

        SerializedObject serializedRemake = new SerializedObject(remakeView);
        serializedRemake.FindProperty("useSceneCanvasLayout").boolValue = true;
        serializedRemake.FindProperty("slotScrollList").objectReferenceValue = scrollList;
        serializedRemake.FindProperty("confirmCanvas").objectReferenceValue = confirmCanvas;
        serializedRemake.FindProperty("loadConfirmView").objectReferenceValue = confirmView;
        serializedRemake.FindProperty("selectButton").objectReferenceValue = selectButton;
        serializedRemake.FindProperty("deleteButton").objectReferenceValue = deleteButton;
        serializedRemake.FindProperty("backButton").objectReferenceValue = confirmBackButton;
        serializedRemake.FindProperty("listBackButton").objectReferenceValue = listBackButton;
        serializedRemake.FindProperty("deletePromptView").objectReferenceValue = deletePromptView;
        serializedRemake.FindProperty("clayMaterial").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Material>(ClayMaterialPath);
        ClayModel clayModel = Object.FindFirstObjectByType<ClayModel>();
        if (clayModel != null)
        {
            serializedRemake.FindProperty("spawnParent").objectReferenceValue = clayModel.transform;
        }
        serializedRemake.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(remakeView);
        return remakeView;
    }

    private static ClayEditEditorUiGate MigrateClayEditEditorUiGate(GameObject host)
    {
        ClayEditEditorUiGate gate = host.GetComponent<ClayEditEditorUiGate>();
        if (gate == null)
        {
            gate = host.AddComponent<ClayEditEditorUiGate>();
        }

        var roots = new List<GameObject>();
        AddRootIfFound(roots, "ExitCanvas");
        AddRootIfFound(roots, "OperationGuideCanvas");
        AddRootIfFound(roots, "AnimationButtonParent");

        ClayEditModeView modeView = Object.FindFirstObjectByType<ClayEditModeView>();
        if (modeView != null)
        {
            roots.Add(modeView.gameObject);
        }

        SerializedObject serializedGate = new SerializedObject(gate);
        serializedGate.FindProperty("editorUiRoots").arraySize = roots.Count;
        for (int i = 0; i < roots.Count; i++)
        {
            serializedGate.FindProperty("editorUiRoots").GetArrayElementAtIndex(i).objectReferenceValue = roots[i];
        }

        serializedGate.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(gate);
        return gate;
    }

    private static GameObject EnsureOverlayCanvas(Transform parent, string objectName, int sortingOrder, bool enabled)
    {
        Transform existing = parent.Find(objectName);
        GameObject canvasObject;
        if (existing != null)
        {
            canvasObject = existing.gameObject;
        }
        else
        {
            canvasObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(GraphicRaycaster),
                typeof(CanvasScaler));
            canvasObject.transform.SetParent(parent, false);
        }

        RectTransform rectTransform = canvasObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = sortingOrder;
        canvas.enabled = enabled;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600f, 900f);
        TitleClayUiVisualUtility.NormalizeCanvasScaler(scaler);

        if (canvasObject.GetComponent<GraphicRaycaster>() == null)
        {
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        return canvasObject;
    }

    private static Canvas EnsureChildCanvas(Transform parent, string objectName, int sortingOrder, bool enabled)
    {
        Transform existing = parent.Find(objectName);
        GameObject canvasObject;
        if (existing != null)
        {
            canvasObject = existing.gameObject;
        }
        else
        {
            canvasObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(parent, false);
            RectTransform rectTransform = canvasObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = sortingOrder;
        canvas.enabled = enabled;
        if (canvasObject.GetComponent<GraphicRaycaster>() == null)
        {
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        return canvas;
    }

    private static Component CreateEntryFlowButton(
        Transform parent,
        string objectName,
        string label,
        Vector2 anchoredPosition,
        bool primary)
    {
        Transform existing = parent.Find(objectName);
        if (existing != null)
        {
            Component existingButton = SceneUiLhButtonUtility.GetComponent(existing.gameObject)
                ?? SceneUiLhButtonUtility.AddComponent(existing.gameObject);
            RectTransform rectTransform = existing as RectTransform;
            if (rectTransform != null)
            {
                rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
                rectTransform.anchoredPosition = anchoredPosition;
                rectTransform.sizeDelta = new Vector2(440f, 72f);
            }

            ApplyActionButtonVisual(existingButton, primary);
            EnsureActionButtonLabel(existingButton, label, 36f);
            return existingButton;
        }

        var buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(440f, 72f);

        Component button = SceneUiLhButtonUtility.AddComponent(buttonObject);
        ApplyActionButtonVisual(button, primary);
        EnsureActionButtonLabel(button, label, 36f);
        return button;
    }

    private static void EnsureEntryDialogTitle(Transform dialogRoot, string title)
    {
        Transform existing = dialogRoot.Find("EntryTitle");
        GameObject titleObject;
        if (existing != null)
        {
            titleObject = existing.gameObject;
        }
        else
        {
            titleObject = new GameObject("EntryTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleObject.transform.SetParent(dialogRoot, false);
            RectTransform titleRect = titleObject.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 0.5f);
            titleRect.anchorMax = new Vector2(0.5f, 0.5f);
            titleRect.pivot = new Vector2(0.5f, 0.5f);
            titleRect.sizeDelta = new Vector2(480f, 56f);
            titleRect.anchoredPosition = new Vector2(0f, 132f);
        }

        TextMeshProUGUI titleText = titleObject.GetComponent<TextMeshProUGUI>();
        titleText.text = title;
        titleText.fontSize = 36f;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = TitleClayUiVisualUtility.SectionColor;
        titleText.raycastTarget = false;
    }

    private static TextMeshProUGUI CreateButtonLabel(Transform parent, string label, float fontSize)
    {
        Transform existing = parent.Find("Label");
        GameObject labelObject;
        if (existing != null)
        {
            labelObject = existing.gameObject;
        }
        else
        {
            labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(parent, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
        }

        TextMeshProUGUI labelText = labelObject.GetComponent<TextMeshProUGUI>();
        TMP_FontAsset fontAsset = AppTmpFontUtility.DefaultFont;
        if (fontAsset != null)
        {
            labelText.font = fontAsset;
        }

        labelText.text = label;
        labelText.fontSize = fontSize;
        labelText.color = TitleClayUiVisualUtility.LabelColor;
        labelText.raycastTarget = false;
        return labelText;
    }

    public static void MigrateBattlePvpAuxiliaryUi()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        MigrateBattlePvpAuxiliaryUiInScene("Assets/Scenes/BattlePvpArena.unity", "BattlePvpArenaScene", "Scene.BattlePvpArena.BattlePvpArenaLifetimeScope, Scene");
    }

    /// <summary>
    /// アクティブシーンのBattleStartOverlayViewへUI参照を配線する
    /// プレハブがある場合はインスタンスを優先し既存オブジェクトのRectTransformは変更しない
    /// </summary>
    public static void EnsureBattleStartOverlayReferencesInActiveScene()
    {
        EnsureBattleStartOverlayReferencesInActiveScene(preferPrefabInstance: true);
    }

    /// <summary>
    /// アクティブシーンのBattleStartOverlayを整備する
    /// </summary>
    /// <param name="preferPrefabInstance">既存プレハブを優先して配置するか</param>
    public static void EnsureBattleStartOverlayReferencesInActiveScene(bool preferPrefabInstance)
    {
        BattleStartOverlayView overlayView = Object.FindFirstObjectByType<BattleStartOverlayView>(FindObjectsInactive.Include);
        if (overlayView == null)
        {
            return;
        }

        if (preferPrefabInstance && File.Exists(BattleStartOverlayPrefabUtility.PrefabPath))
        {
            BattleStartOverlayPrefabUtility.EnsurePrefabInstanceUnderHost(overlayView.transform);
            BattleStartOverlayPrefabUtility.WireOverlayReferences(overlayView);
            return;
        }

        GameObject root = ResolveBattleStartOverlayRoot(overlayView);
        EnsureBattleStartOverlayCanvasComponents(root);
        FixBattleStartOverlayLayout(root);

        Canvas overlayCanvas = root.GetComponent<Canvas>();
        TMP_Text vsText = FindOrCreateLabel(root.transform, "VS", new Vector2(0f, 80f), 88f);
        TMP_Text readyText = FindOrCreateLabel(root.transform, "Ready", Vector2.zero, 72f);
        TMP_Text fightText = FindOrCreateLabel(root.transform, "Fight", Vector2.zero, 88f);
        TMP_Text finishText = FindOrCreateLabel(root.transform, "Finish", new Vector2(0f, 24f), 104f);
        finishText.color = new Color(1f, 0.82f, 0.2f, 1f);
        finishText.outlineWidth = 0.28f;
        finishText.outlineColor = new Color(0.45f, 0.05f, 0.02f, 1f);
        Image finishFlashImage = EnsureFinishFlash(root.transform);
        Button startButton = EnsureStartButton(root.transform);

        SerializedObject serializedOverlay = new SerializedObject(overlayView);
        serializedOverlay.FindProperty("overlayCanvas").objectReferenceValue = overlayCanvas;
        serializedOverlay.FindProperty("vsText").objectReferenceValue = vsText;
        serializedOverlay.FindProperty("readyText").objectReferenceValue = readyText;
        serializedOverlay.FindProperty("fightText").objectReferenceValue = fightText;
        serializedOverlay.FindProperty("finishText").objectReferenceValue = finishText;
        serializedOverlay.FindProperty("finishFlashImage").objectReferenceValue = finishFlashImage;
        serializedOverlay.FindProperty("startButton").objectReferenceValue = startButton;
        serializedOverlay.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(overlayView);

        MigrateBattleStartOverlayExtrasInScene();
        RemoveDuplicateBattleStartOverlays(root);
    }

    private static GameObject ResolveBattleStartOverlayRoot(BattleStartOverlayView overlayView)
    {
        SerializedObject serializedOverlay = new SerializedObject(overlayView);
        Canvas wiredCanvas = serializedOverlay.FindProperty("overlayCanvas").objectReferenceValue as Canvas;
        if (wiredCanvas != null)
        {
            return wiredCanvas.gameObject;
        }

        Transform underHost = overlayView.transform.Find("BattleStartOverlay");
        if (underHost != null)
        {
            return underHost.gameObject;
        }

        List<GameObject> existingRoots = FindBattleStartOverlayRoots();
        if (existingRoots.Count > 0)
        {
            return existingRoots[0];
        }

        return CreateBattleStartOverlayRoot(overlayView.transform);
    }

    private static List<GameObject> FindBattleStartOverlayRoots()
    {
        var roots = new List<GameObject>();
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas != null && canvas.gameObject.name == "BattleStartOverlay")
            {
                roots.Add(canvas.gameObject);
            }
        }

        return roots;
    }

    private static void RemoveDuplicateBattleStartOverlays(GameObject keepRoot)
    {
        GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < allObjects.Length; i++)
        {
            GameObject root = allObjects[i];
            if (root == null || root == keepRoot || root.name != "BattleStartOverlay")
            {
                continue;
            }

            Object.DestroyImmediate(root);
        }
    }

    public static void MigrateBattleStartOverlayExtrasInAllScenes()
    {
        string[] scenePaths =
        {
            BattleNpcScenePath,
            "Assets/Scenes/BattlePvpArena.unity",
            "Assets/Scenes/Training.unity",
        };

        string originalScene = SceneManager.GetActiveScene().path;
        for (int i = 0; i < scenePaths.Length; i++)
        {
            if (!System.IO.File.Exists(scenePaths[i]))
            {
                continue;
            }

            EditorSceneManager.OpenScene(scenePaths[i], OpenSceneMode.Single);
            EnsureBattleStartOverlayReferencesInActiveScene();
            SceneUiEditorSavePolicy.MarkDirty(SceneManager.GetActiveScene());
        }

        if (!string.IsNullOrEmpty(originalScene))
        {
            EditorSceneManager.OpenScene(originalScene, OpenSceneMode.Single);
        }
    }

    private static void MigrateBattlePvpAuxiliaryUiInScene(string scenePath, string sceneRootName, string lifetimeScopeTypeName)
    {
        if (!System.IO.File.Exists(scenePath))
        {
            return;
        }

        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        GameObject sceneRoot = GameObject.Find(sceneRootName);
        if (sceneRoot == null)
        {
            Debug.LogWarning($"[SceneUiPlacementMigrator] scene root not found: {sceneRootName} in {scenePath}");
            return;
        }

        System.Type lifetimeScopeType = System.Type.GetType(lifetimeScopeTypeName);
        Component lifetimeScope = lifetimeScopeType != null
            ? Object.FindFirstObjectByType(lifetimeScopeType, FindObjectsInactive.Include) as Component
            : null;
        BattlePvpAuxiliaryUiSceneBuilder.EnsureOnScene(sceneRoot.transform, lifetimeScope);
        SceneUiEditorSavePolicy.MarkDirty(SceneManager.GetActiveScene());
    }

    private static void MigrateBattleStartOverlayExtrasInScene()
    {
        BattleStartOverlayView overlayView = Object.FindFirstObjectByType<BattleStartOverlayView>(FindObjectsInactive.Include);
        if (overlayView == null)
        {
            return;
        }

        Canvas overlayCanvas = overlayView.GetComponentInChildren<Canvas>(true);
        if (overlayCanvas == null)
        {
            GameObject root = ResolveBattleStartOverlayRoot(overlayView);
            EnsureBattleStartOverlayCanvasComponents(root);
            overlayCanvas = root.GetComponent<Canvas>();
        }

        if (overlayCanvas == null)
        {
            return;
        }

        Transform overlayRoot = overlayCanvas.transform;
        GameObject vsNamePlateRoot = EnsureChildObject(overlayRoot, "VsNamePlates");
        StretchFullRect(vsNamePlateRoot.GetComponent<RectTransform>());

        TMP_Text playerNameText = EnsureOverlayNameLabel(vsNamePlateRoot.transform, "PlayerNameLabel", true);
        TMP_Text enemyNameText = EnsureOverlayNameLabel(vsNamePlateRoot.transform, "EnemyNameLabel", false);
        TMP_Text victoryTitleText = EnsureOverlayCenterLabel(overlayRoot, "VictoryTitle", "勝利！", 96f, 120f);
        victoryTitleText.color = new Color(1f, 0.88f, 0.28f, 1f);
        TMP_Text victoryNameText = EnsureOverlayCenterLabel(overlayRoot, "VictoryName", string.Empty, 44f, -40f);
        victoryNameText.color = new Color(1f, 0.98f, 0.9f, 1f);

        SerializedObject serializedOverlay = new SerializedObject(overlayView);
        serializedOverlay.FindProperty("vsNamePlateRoot").objectReferenceValue = vsNamePlateRoot;
        serializedOverlay.FindProperty("playerNameText").objectReferenceValue = playerNameText;
        serializedOverlay.FindProperty("enemyNameText").objectReferenceValue = enemyNameText;
        serializedOverlay.FindProperty("victoryTitleText").objectReferenceValue = victoryTitleText;
        serializedOverlay.FindProperty("victoryNameText").objectReferenceValue = victoryNameText;
        serializedOverlay.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(overlayView);
    }

    private static GameObject EnsureChildObject(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            return existing.gameObject;
        }

        var childObject = new GameObject(name, typeof(RectTransform));
        childObject.transform.SetParent(parent, false);
        return childObject;
    }

    private static TMP_Text EnsureOverlayNameLabel(Transform parent, string objectName, bool isLeft)
    {
        Transform existing = parent.Find(objectName);
        if (existing != null)
        {
            return existing.GetComponent<TMP_Text>();
        }

        var host = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        host.transform.SetParent(parent, false);
        RectTransform rect = host.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(isLeft ? 0.04f : 0.56f, 0f);
        rect.anchorMax = new Vector2(isLeft ? 0.46f : 0.96f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 72f);
        rect.sizeDelta = new Vector2(0f, 72f);

        TMP_Text label = host.GetComponent<TextMeshProUGUI>();
        label.fontSize = 36f;
        label.fontStyle = FontStyles.Bold;
        label.color = isLeft
            ? new Color(0.72f, 0.9f, 1f, 1f)
            : new Color(1f, 0.78f, 0.45f, 1f);
        label.raycastTarget = false;
        host.SetActive(false);
        return label;
    }

    private static TMP_Text EnsureOverlayCenterLabel(
        Transform parent,
        string objectName,
        string text,
        float fontSize,
        float yOffset)
    {
        Transform existing = parent.Find(objectName);
        if (existing != null)
        {
            TMP_Text existingText = existing.GetComponent<TMP_Text>();
            if (existingText != null)
            {
                existingText.text = text;
            }

            return existingText;
        }

        var host = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        host.transform.SetParent(parent, false);
        RectTransform rect = host.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, yOffset);
        rect.sizeDelta = new Vector2(900f, 160f);

        TMP_Text label = host.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = FontStyles.Bold;
        label.raycastTarget = false;
        host.SetActive(false);
        return label;
    }

    private static void StretchFullRect(RectTransform rect)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
    }

    private static void AddRootIfFound(List<GameObject> roots, string objectName)
    {
        GameObject found = GameObject.Find(objectName);
        if (found != null)
        {
            roots.Add(found);
        }
    }
}
#endif
