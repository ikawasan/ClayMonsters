#if UNITY_EDITOR
using Extensions;
using TMPro;
using UI.ClayEditor.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// ClayEditシーンに操作説明用Canvasを配置する
/// </summary>
public static class ClayEditOperationGuideSceneSetup
{
    private const string ScenePath = "Assets/Scenes/ClayEdit.unity";
    private const string CanvasName = "OperationGuideCanvas";

    private static bool isSettingUp;

    [MenuItem("Tools/ClayEdit/Update Operation Guide Chevron")]
    public static void UpdateChevron()
    {
        ClayEditOperationGuideView guideView = FindOperationGuideCanvas();
        if (guideView == null)
        {
            EditorUtility.DisplayDialog("Operation Guide", "OperationGuideCanvas が見つかりません", "OK");
            return;
        }

        SerializedObject serializedGuideView = new SerializedObject(guideView);
        Toggle visibilityToggle = serializedGuideView.FindProperty("visibilityToggle").objectReferenceValue as Toggle;
        if (visibilityToggle == null)
        {
            EditorUtility.DisplayDialog("Operation Guide", "visibilityToggle が見つかりません", "OK");
            return;
        }

        Transform background = visibilityToggle.transform.Find("Background");
        if (background == null)
        {
            EditorUtility.DisplayDialog("Operation Guide", "Background が見つかりません", "OK");
            return;
        }

        Transform legacyCheckmark = background.Find("Checkmark");
        if (legacyCheckmark != null)
        {
            Object.DestroyImmediate(legacyCheckmark.gameObject);
        }

        Transform existingChevron = background.Find("Chevron");
        if (existingChevron == null)
        {
            CreateChevronLabel(background);
        }

        visibilityToggle.graphic = null;
        visibilityToggle.toggleTransition = Toggle.ToggleTransition.None;

        RectTransform chevronIcon = background.Find("Chevron") as RectTransform;
        serializedGuideView.FindProperty("chevronIcon").objectReferenceValue = chevronIcon;
        serializedGuideView.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(guideView);
        SceneUiEditorSavePolicy.MarkActiveDirty();
        EditorUtility.DisplayDialog(
            "Operation Guide",
            "Chevron表示を更新しました\nシーンは未保存です。必要に応じて手動で保存してください",
            "OK");
    }

    [MenuItem("Tools/ClayEdit/Setup Operation Guide Canvas")]
    public static void Setup()
    {
        SetupInternal(showDialog: true);
    }

    /// <summary>
    /// バッチモード実行用
    /// </summary>
    public static void SetupSilent()
    {
        SetupInternal(showDialog: false);
    }

    private static void SetupInternal(bool showDialog)
    {
        if (isSettingUp)
        {
            return;
        }

        isSettingUp = true;
        try
        {
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    return;
                }

                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            if (FindOperationGuideCanvas() != null)
            {
                if (showDialog)
                {
                    EditorUtility.DisplayDialog(
                        "Operation Guide",
                        "OperationGuideCanvas は既に配置されています",
                        "OK");
                }

                return;
            }

            Transform clayEditView = FindClayEditView();
            if (clayEditView == null)
            {
                Debug.LogError("ClayEditView が見つかりません");
                return;
            }

            RemoveLegacyObjects(clayEditView);

            GameObject canvasObject = CreateCanvas(clayEditView);
            RectTransform panelRect = CreatePanel(canvasObject.transform);
            Toggle visibilityToggle = CreateToggleRow(panelRect);
            RectTransform chevronIcon = visibilityToggle.transform.Find("Chevron") as RectTransform;
            GameObject clayGuidePanel = CreateGuideTextPanel(panelRect, "ClayGuidePanel", "ClayGuideText", GetClayGuideText());
            GameObject paintGuidePanel = CreateGuideTextPanel(panelRect, "PaintGuidePanel", "PaintGuideText", GetPaintGuideText());

            ClayEditOperationGuideView guideView = canvasObject.GetComponent<ClayEditOperationGuideView>();
            if (guideView == null)
            {
                guideView = canvasObject.AddComponent<ClayEditOperationGuideView>();
            }

            SerializedObject serializedGuideView = new SerializedObject(guideView);
            serializedGuideView.FindProperty("canvas").objectReferenceValue = canvasObject.GetComponent<Canvas>();
            serializedGuideView.FindProperty("visibilityToggle").objectReferenceValue = visibilityToggle;
            serializedGuideView.FindProperty("chevronIcon").objectReferenceValue = chevronIcon;
            serializedGuideView.FindProperty("clayGuidePanel").objectReferenceValue = clayGuidePanel;
            serializedGuideView.FindProperty("paintGuidePanel").objectReferenceValue = paintGuidePanel;
            serializedGuideView.ApplyModifiedPropertiesWithoutUndo();

            Scene.ClayEditScene.ClayEditLifetimeScope lifetimeScope =
                Object.FindFirstObjectByType<Scene.ClayEditScene.ClayEditLifetimeScope>();
            if (lifetimeScope != null)
            {
                SerializedObject serializedScope = new SerializedObject(lifetimeScope);
                serializedScope.FindProperty("clayEditOperationGuideView").objectReferenceValue = guideView;
                serializedScope.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(lifetimeScope);
            }

            EditorUtility.SetDirty(canvasObject);
            if (showDialog)
            {
                SceneUiEditorSavePolicy.MarkDirty(scene);
            }
            else
            {
                SceneUiEditorSavePolicy.MarkDirtyAndSave(scene);
            }

            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "Operation Guide",
                    "操作説明Canvasを配置しました\n"
                        + "OperationGuideCanvas のRectTransformとTMP_Textを編集してください\n"
                        + "シーンは未保存です。必要に応じて手動で保存してください",
                    "OK");
            }
        }
        finally
        {
            isSettingUp = false;
        }
    }

    private static ClayEditOperationGuideView FindOperationGuideCanvas()
    {
        return Object.FindFirstObjectByType<ClayEditOperationGuideView>();
    }

    private static Transform FindClayEditView()
    {
        foreach (GameObject rootObject in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            Transform found = FindChildRecursive(rootObject.transform, "ClayEditView");
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static Transform FindChildRecursive(Transform parent, string objectName)
    {
        if (parent.name == objectName)
        {
            return parent;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindChildRecursive(parent.GetChild(i), objectName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static void RemoveLegacyObjects(Transform clayEditView)
    {
        Transform legacyLayout = clayEditView.Find("OperationGuideLayout");
        if (legacyLayout != null)
        {
            Object.DestroyImmediate(legacyLayout.gameObject);
        }

        ClayEditOperationGuideView legacyView = clayEditView.GetComponent<ClayEditOperationGuideView>();
        if (legacyView != null)
        {
            Object.DestroyImmediate(legacyView);
        }
    }

    private static GameObject CreateCanvas(Transform parent)
    {
        Transform existing = parent.Find(CanvasName);
        if (existing != null)
        {
            Object.DestroyImmediate(existing.gameObject);
        }

        var canvasObject = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(parent, false);
        canvasObject.layer = LayerMask.NameToLayer("UI");

        RectTransform rectTransform = canvasObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.localScale = Vector3.one;

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600f, 900f);
        scaler.matchWidthOrHeight = 0.5f;

        return canvasObject;
    }

    private static RectTransform CreatePanel(Transform parent)
    {
        var panelObject = new GameObject("GuidePanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panelObject.transform.SetParent(parent, false);
        panelObject.layer = parent.gameObject.layer;

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.anchoredPosition = new Vector2(-16f, -16f);
        panelRect.sizeDelta = new Vector2(380f, 0f);

        Image image = panelObject.GetComponent<Image>();
        image.color = new Color(0.18f, 0.20f, 0.26f, 0.94f);
        image.raycastTarget = true;

        VerticalLayoutGroup layout = panelObject.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 12, 12);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = panelObject.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        return panelRect;
    }

    private static Toggle CreateToggleRow(RectTransform panelRect)
    {
        var rowObject = new GameObject("GuideToggleRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        rowObject.transform.SetParent(panelRect, false);
        rowObject.layer = panelRect.gameObject.layer;

        HorizontalLayoutGroup layout = rowObject.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        Toggle toggle = CreateToggle(rowObject.transform);
        CreateLabel(rowObject.transform, "ToggleLabel", "操作説明を表示", 22f, FontStyles.Bold);
        return toggle;
    }

    private static Toggle CreateToggle(Transform parent)
    {
        var toggleObject = new GameObject("GuideVisibilityToggle", typeof(RectTransform), typeof(Toggle), typeof(LayoutElement));
        toggleObject.transform.SetParent(parent, false);
        toggleObject.layer = parent.gameObject.layer;

        LayoutElement layoutElement = toggleObject.GetComponent<LayoutElement>();
        layoutElement.minWidth = 24f;
        layoutElement.minHeight = 24f;
        layoutElement.preferredWidth = 24f;
        layoutElement.preferredHeight = 24f;

        var backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(Image));
        backgroundObject.transform.SetParent(toggleObject.transform, false);
        Image backgroundImage = backgroundObject.GetComponent<Image>();
        backgroundImage.color = new Color(0.18f, 0.20f, 0.26f, 0.94f);
        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        TextMeshProUGUI chevronText = CreateChevronLabel(backgroundObject.transform);

        Toggle toggle = toggleObject.GetComponent<Toggle>();
        toggle.targetGraphic = backgroundImage;
        toggle.graphic = null;
        toggle.toggleTransition = Toggle.ToggleTransition.None;
        toggle.isOn = true;
        return toggle;
    }

    private static TextMeshProUGUI CreateChevronLabel(Transform parent)
    {
        var chevronObject = new GameObject("Chevron", typeof(RectTransform), typeof(TextMeshProUGUI));
        chevronObject.transform.SetParent(parent, false);
        chevronObject.layer = parent.gameObject.layer;

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

    private static GameObject CreateGuideTextPanel(Transform parent, string panelName, string textName, string content)
    {
        var panelObject = new GameObject(panelName, typeof(RectTransform), typeof(LayoutElement));
        panelObject.transform.SetParent(parent, false);
        panelObject.layer = parent.gameObject.layer;

        LayoutElement layoutElement = panelObject.GetComponent<LayoutElement>();
        layoutElement.minWidth = 356f;

        CreateLabel(panelObject.transform, textName, content, 20f, FontStyles.Normal);
        return panelObject;
    }

    private static TextMeshProUGUI CreateLabel(Transform parent, string objectName, string content, float fontSize, FontStyles fontStyle)
    {
        var textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(ContentSizeFitter));
        textObject.transform.SetParent(parent, false);
        textObject.layer = parent.gameObject.layer;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        TMP_FontAsset fontAsset = AppTmpFontUtility.DefaultFont;
        if (fontAsset != null)
        {
            text.font = fontAsset;
        }

        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = new Color(0.94f, 0.93f, 0.90f, 1f);
        text.raycastTarget = false;

        ContentSizeFitter fitter = textObject.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return text;
    }

    private static string GetClayGuideText()
    {
        return
            "【成形操作】\n" +
            "左ドラッグ: 盛る\n" +
            "Ctrl+左ドラッグ: 削る\n" +
            "右ドラッグ: 削る\n" +
            "ホイール: ブラシサイズ変更\n" +
            "Shift+ドラッグ: 深度固定\n" +
            "Alt+ドラッグ: カメラ操作\n" +
            "Ctrl+Z: 取り消し\n" +
            "Ctrl+Y: やり直し\n" +
            "Delete: 全削除";
    }

    private static string GetPaintGuideText()
    {
        return
            "【ペイント操作】\n" +
            "左ドラッグ: 塗る\n" +
            "ホイール: ブラシサイズ変更\n" +
            "Shift+ドラッグ: 深度固定\n" +
            "Alt+ドラッグ: カメラ操作\n" +
            "Ctrl+Z: 取り消し\n" +
            "Ctrl+Y: やり直し\n" +
            "色選択: カラーピッカー";
    }
}
#endif
