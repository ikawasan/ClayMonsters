#if UNITY_EDITOR
using Scene.BattlePVPScene.View;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// BattlePVP向け補助UIをシーンCanvasへ配置する
/// Disconnect/OpponentWait/VictoryReturnをHierarchy上で編集可能にする
/// </summary>
public static class BattlePvpAuxiliaryUiSceneBuilder
{
    private const string DisconnectCanvasName = "BattlePvpDisconnectCanvas";
    private const string OpponentWaitCanvasName = "BattlePvpOpponentWaitCanvas";
    private const string VictoryReturnCanvasName = "BattlePvpVictoryReturnCanvas";

    /// <summary>
    /// 補助UI Canvasを配置しLifetimeScopeへ配線する
    /// </summary>
    public static void EnsureOnScene(Transform sceneRoot, Component lifetimeScope)
    {
        if (sceneRoot == null)
        {
            return;
        }

        BattlePvpDisconnectView disconnectView = EnsureDisconnectView(sceneRoot);
        BattlePvpOpponentWaitView opponentWaitView = EnsureOpponentWaitView(sceneRoot);
        BattlePvpVictoryReturnView victoryReturnView = EnsureVictoryReturnView(sceneRoot);

        if (lifetimeScope != null)
        {
            SerializedObject serializedScope = new SerializedObject(lifetimeScope);
            serializedScope.FindProperty("pvpDisconnectView").objectReferenceValue = disconnectView;
            serializedScope.FindProperty("pvpOpponentWaitView").objectReferenceValue = opponentWaitView;
            serializedScope.FindProperty("pvpVictoryReturnView").objectReferenceValue = victoryReturnView;
            serializedScope.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(lifetimeScope);
        }
    }

    private static BattlePvpDisconnectView EnsureDisconnectView(Transform sceneRoot)
    {
        BattlePvpDisconnectView existing = sceneRoot.GetComponentInChildren<BattlePvpDisconnectView>(true);
        if (existing != null)
        {
            WireDisconnectView(existing);
            return existing;
        }

        GameObject canvasObject = CreateOverlayCanvas(sceneRoot, DisconnectCanvasName, 350);
        BattlePvpDisconnectView view = canvasObject.AddComponent<BattlePvpDisconnectView>();

        Image dimImage = CreateFullscreenImage(canvasObject.transform, "Dim", new Color(0f, 0f, 0f, 0.55f), raycast: true);
        Image panelImage = CreatePanelImage(canvasObject.transform, "Panel", 520f, 280f);
        TMP_Text messageText = CreateCenteredText(panelImage.transform, "Message", "通信が切れました", 34f, new Vector2(0f, 36f), new Vector2(472f, 120f));
        Component returnButton = CreateBottomButton(panelImage.transform, "TitleReturnButton", "タイトルへ戻る", 260f, 64f, 0.28f);

        WireDisconnectView(view, canvasObject.GetComponent<Canvas>(), panelImage, messageText, returnButton);
        canvasObject.SetActive(false);
        return view;
    }

    private static BattlePvpOpponentWaitView EnsureOpponentWaitView(Transform sceneRoot)
    {
        BattlePvpOpponentWaitView existing = sceneRoot.GetComponentInChildren<BattlePvpOpponentWaitView>(true);
        if (existing != null)
        {
            WireOpponentWaitView(existing);
            return existing;
        }

        GameObject canvasObject = CreateOverlayCanvas(sceneRoot, OpponentWaitCanvasName, 320);
        BattlePvpOpponentWaitView view = canvasObject.AddComponent<BattlePvpOpponentWaitView>();

        Image bannerImage = CreateAnchoredPanel(
            canvasObject.transform,
            "Banner",
            new Vector2(0.5f, 0.12f),
            new Vector2(560f, 72f));
        TMP_Text messageText = CreateStretchText(bannerImage.transform, "Message", "相手の応答を待っています", 30f);

        WireOpponentWaitView(view, canvasObject.GetComponent<Canvas>(), bannerImage, messageText);
        canvasObject.SetActive(false);
        return view;
    }

    private static BattlePvpVictoryReturnView EnsureVictoryReturnView(Transform sceneRoot)
    {
        BattlePvpVictoryReturnView existing = sceneRoot.GetComponentInChildren<BattlePvpVictoryReturnView>(true);
        if (existing != null)
        {
            WireVictoryReturnView(existing);
            return existing;
        }

        GameObject canvasObject = CreateOverlayCanvas(sceneRoot, VictoryReturnCanvasName, 320);
        BattlePvpVictoryReturnView view = canvasObject.AddComponent<BattlePvpVictoryReturnView>();

        Component rematchButton = CreateCornerButton(canvasObject.transform, "RematchButton", "再戦", 180f, 64f, -40f);
        Component titleButton = CreateCornerButton(canvasObject.transform, "TitleReturnButton", "タイトルへ戻る", 260f, 64f, -(40f + 180f + 16f));

        WireVictoryReturnView(view, canvasObject.GetComponent<Canvas>(), rematchButton, titleButton);
        canvasObject.SetActive(false);
        return view;
    }

    private static void WireDisconnectView(BattlePvpDisconnectView view)
    {
        Canvas canvas = view.GetComponent<Canvas>();
        Image panel = view.transform.Find("Panel")?.GetComponent<Image>();
        TMP_Text message = panel != null ? panel.transform.Find("Message")?.GetComponent<TMP_Text>() : null;
        Component button = SceneUiLhButtonUtility.FindInChildren(view.transform);
        WireDisconnectView(view, canvas, panel, message, button);
    }

    private static void WireDisconnectView(
        BattlePvpDisconnectView view,
        Canvas canvas,
        Image panel,
        TMP_Text message,
        Component returnButton)
    {
        SerializedObject serialized = new SerializedObject(view);
        serialized.FindProperty("rootCanvas").objectReferenceValue = canvas;
        serialized.FindProperty("panelBackground").objectReferenceValue = panel;
        serialized.FindProperty("messageText").objectReferenceValue = message;
        serialized.FindProperty("titleReturnButton").objectReferenceValue = returnButton;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(view);
    }

    private static void WireOpponentWaitView(BattlePvpOpponentWaitView view)
    {
        Canvas canvas = view.GetComponent<Canvas>();
        Image banner = view.transform.Find("Banner")?.GetComponent<Image>();
        TMP_Text message = banner != null ? banner.transform.Find("Message")?.GetComponent<TMP_Text>() : null;
        WireOpponentWaitView(view, canvas, banner, message);
    }

    private static void WireOpponentWaitView(
        BattlePvpOpponentWaitView view,
        Canvas canvas,
        Image banner,
        TMP_Text message)
    {
        SerializedObject serialized = new SerializedObject(view);
        serialized.FindProperty("rootCanvas").objectReferenceValue = canvas;
        serialized.FindProperty("bannerBackground").objectReferenceValue = banner;
        serialized.FindProperty("messageText").objectReferenceValue = message;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(view);
    }

    private static void WireVictoryReturnView(BattlePvpVictoryReturnView view)
    {
        Canvas canvas = view.GetComponent<Canvas>();
        Component rematch = view.transform.Find("RematchButton") != null
            ? SceneUiLhButtonUtility.GetComponent(view.transform.Find("RematchButton").gameObject)
            : null;
        Component title = view.transform.Find("TitleReturnButton") != null
            ? SceneUiLhButtonUtility.GetComponent(view.transform.Find("TitleReturnButton").gameObject)
            : null;
        WireVictoryReturnView(view, canvas, rematch, title);
    }

    private static void WireVictoryReturnView(
        BattlePvpVictoryReturnView view,
        Canvas canvas,
        Component rematchButton,
        Component titleReturnButton)
    {
        SerializedObject serialized = new SerializedObject(view);
        serialized.FindProperty("rootCanvas").objectReferenceValue = canvas;
        serialized.FindProperty("rematchButton").objectReferenceValue = rematchButton;
        serialized.FindProperty("titleReturnButton").objectReferenceValue = titleReturnButton;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(view);
    }

    private static GameObject CreateOverlayCanvas(Transform parent, string name, int sortingOrder)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            return existing.gameObject;
        }

        var canvasObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(parent, false);
        StretchRect(canvasObject.GetComponent<RectTransform>());

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600f, 900f);
        scaler.matchWidthOrHeight = 0.5f;
        return canvasObject;
    }

    private static Image CreateFullscreenImage(Transform parent, string name, Color color, bool raycast)
    {
        var imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        StretchRect(imageObject.GetComponent<RectTransform>());
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = raycast;
        return image;
    }

    private static Image CreatePanelImage(Transform parent, string name, float width, float height)
    {
        var panelObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(parent, false);
        RectTransform rect = panelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(width, height);
        return panelObject.GetComponent<Image>();
    }

    private static Image CreateAnchoredPanel(Transform parent, string name, Vector2 anchor, Vector2 size)
    {
        var panelObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(parent, false);
        RectTransform rect = panelObject.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        return panelObject.GetComponent<Image>();
    }

    private static TMP_Text CreateCenteredText(
        Transform parent,
        string name,
        string text,
        float fontSize,
        Vector2 anchoredPosition,
        Vector2 size)
    {
        var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        TMP_Text label = textObject.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.raycastTarget = false;
        return label;
    }

    private static TMP_Text CreateStretchText(Transform parent, string name, string text, float fontSize)
    {
        var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        StretchRect(textObject.GetComponent<RectTransform>());

        TMP_Text label = textObject.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        return label;
    }

    private static Component CreateBottomButton(
        Transform parent,
        string name,
        string labelText,
        float width,
        float height,
        float anchorY)
    {
        var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, anchorY);
        rect.anchorMax = new Vector2(0.5f, anchorY);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(width, height);

        Component button = SceneUiLhButtonUtility.AddComponent(buttonObject);
        SceneUiLhButtonUtility.ApplyPrimaryButton(button);
        CreateButtonLabel(buttonObject.transform, labelText, 28f);
        return button;
    }

    private static Component CreateCornerButton(
        Transform parent,
        string name,
        string labelText,
        float width,
        float height,
        float anchoredRight)
    {
        var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(anchoredRight, 36f);

        Component button = SceneUiLhButtonUtility.AddComponent(buttonObject);
        SceneUiLhButtonUtility.ApplyPrimaryButton(button);
        CreateButtonLabel(buttonObject.transform, labelText, 28f);
        return button;
    }

    private static void CreateButtonLabel(Transform parent, string text, float fontSize)
    {
        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(parent, false);
        StretchRect(labelObject.GetComponent<RectTransform>());

        TMP_Text label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
    }

    private static void StretchRect(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
    }
}
#endif
