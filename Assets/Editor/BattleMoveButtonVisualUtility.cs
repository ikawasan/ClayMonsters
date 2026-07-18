#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using Extensions;
using TMPro;
using UI.Battle.View;
using UI.ClayEditor.View;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 戦闘攻撃ボタンUIのEditor Bake
/// WireReferencesは参照配線のみApplyStylesはスタイルのみRebuildは初回レイアウト生成
/// </summary>
public static class BattleMoveButtonVisualUtility
{
    /// <summary>
    /// Editor Bakeの適用範囲
    /// </summary>
    public enum BakeMode
    {
        /// <summary>
        /// SerializeField参照配線のみ
        /// </summary>
        WireReferences,

        /// <summary>
        /// 参照配線とスタイル適用
        /// RectTransformは変更しない
        /// </summary>
        ApplyStyles,

        /// <summary>
        /// Rebuild後の仕上げ
        /// </summary>
        RebuildFinalize,
    }
    private const string SlotFrameObjectName = "SlotFrame";
    private const string HeaderBandObjectName = "HeaderBand";
    private const string BodyPanelObjectName = "BodyPanel";
    private const string MoveNameObjectName = "MoveNameText";
    private const string RequiredPartObjectName = "RequiredPartIcon";
    private const string RequiredPartLabelObjectName = "RequiredPartLabelText";
    private const string TargetPartObjectName = "TargetPartIcon";
    private const string TargetPartLabelObjectName = "TargetPartLabelText";
    private const string StatsRowObjectName = "StatsRow";
    private const string RangeFooterObjectName = "RangeFooter";
    private const string PowerLabelObjectName = "PowerLabelText";
    private const string CostLabelObjectName = "CostLabelText";
    private const string PowerValueObjectName = "PowerValueText";
    private const string CostValueObjectName = "CostValueText";
    private const string RangeLabelObjectName = "RangeLabelText";
    private const string PowerColumnObjectName = "PowerColumn";
    private const string CostColumnObjectName = "CostColumn";
    private const string RangeRowObjectName = "RangeRow";
    private const float ButtonWidth = 124f;
    private const float ButtonHeight = 176f;
    private const float ButtonSpacing = 8f;
    private const float ContentInset = 6f;
    private const float HeaderBandHeight = 34f;
    private const float BodyPanelHeight = 78f;
    private const float StatsRowHeight = 38f;
    private const float RangeFooterHeight = 26f;
    private const float PartIconSize = 26f;
    private const float PartLabelHeight = 12f;
    private const float PartLabelWidth = 48f;
    private const float MoveIconSize = 52f;
    private static readonly Color MoveButtonTextColor = new Color(0x5C / 255f, 0x3D / 255f, 0x2E / 255f, 1f);
    private static readonly string[] PlaceholderMoveNames = { "ストレート", "フック", "アッパー", "必殺技" };
    private static readonly string[] PlaceholderPowerValues = { "50", "40", "60", "80" };
    private static readonly string[] PlaceholderCostValues = { "10", "15", "20", "25" };
    private static readonly string[] PlaceholderRequiredPartLabels = { "要:腕", "要:脚", "要:前", "要:体" };
    private static readonly string[] PlaceholderTargetPartLabels = { "破:脚", "破:腕", "破:後", "破:任意" };
    private const string PlaceholderPowerLabel = "威力";
    private const string PlaceholderCostLabel = "コスト";
    private const string PlaceholderRangeLabel = "射程";

    /// <summary>
    /// 参照配線のみ適用する
    /// RectTransform・Hierarchy・テキスト内容・スプライトは変更しない
    /// </summary>
    public static void WireMoveButtonsRootReferences(Transform moveButtonsRoot)
    {
        ProcessMoveButtonsRoot(moveButtonsRoot, BakeMode.WireReferences);
    }

    /// <summary>
    /// 参照配線とスタイルのみ適用する
    /// RectTransformは変更しない
    /// </summary>
    public static void ApplyMoveButtonsStylesRoot(Transform moveButtonsRoot)
    {
        ProcessMoveButtonsRoot(moveButtonsRoot, BakeMode.ApplyStyles);
    }

    /// <summary>
    /// 初回用にレイアウトを組み立ててから配線する
    /// </summary>
    public static void RebuildMoveButtonsRoot(Transform moveButtonsRoot)
    {
        if (moveButtonsRoot == null)
        {
            return;
        }

        RebuildMoveButtonsContainer(moveButtonsRoot);
        for (int i = 0; i < moveButtonsRoot.childCount; i++)
        {
            MoveButtonView moveButton = moveButtonsRoot.GetChild(i).GetComponent<MoveButtonView>();
            if (moveButton != null)
            {
                RebuildMoveButton(moveButton);
            }
        }
    }

    private static void ProcessMoveButtonsRoot(Transform moveButtonsRoot, BakeMode bakeMode)
    {
        if (moveButtonsRoot == null)
        {
            return;
        }

        for (int i = 0; i < moveButtonsRoot.childCount; i++)
        {
            MoveButtonView moveButton = moveButtonsRoot.GetChild(i).GetComponent<MoveButtonView>();
            if (moveButton != null)
            {
                WireMoveButton(moveButton, bakeMode);
            }
        }
    }

    private static void RebuildMoveButtonsContainer(Transform moveButtonsRoot)
    {
        RectTransform rect = moveButtonsRoot as RectTransform;
        if (rect != null)
        {
            int childCount = Mathf.Max(1, moveButtonsRoot.childCount);
            float width = childCount * ButtonWidth + Mathf.Max(0, childCount - 1) * ButtonSpacing;
            rect.sizeDelta = new Vector2(width, ButtonHeight);
        }

        HorizontalLayoutGroup layoutGroup = moveButtonsRoot.GetComponent<HorizontalLayoutGroup>();
        if (layoutGroup == null)
        {
            layoutGroup = moveButtonsRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
        }

        layoutGroup.padding = new RectOffset(0, 0, 0, 0);
        layoutGroup.spacing = ButtonSpacing;
        layoutGroup.childAlignment = TextAnchor.MiddleCenter;
        layoutGroup.childControlWidth = false;
        layoutGroup.childControlHeight = false;
        layoutGroup.childForceExpandWidth = false;
        layoutGroup.childForceExpandHeight = false;
    }

    private static void WireMoveButton(MoveButtonView moveButton, BakeMode bakeMode, bool applyLayout = false)
    {
        if (moveButton == null)
        {
            return;
        }

        SerializedObject serializedMoveButton = new SerializedObject(moveButton);
        WireMissingReferences(moveButton.transform, serializedMoveButton);

        if (applyLayout)
        {
            ApplyStretchRect(moveButton.transform.Find("Button") as RectTransform);
            ApplyContainedLayout(moveButton.transform, serializedMoveButton);
            serializedMoveButton.ApplyModifiedPropertiesWithoutUndo();
        }

        if (bakeMode == BakeMode.WireReferences)
        {
            WireButtonBackgroundReference(moveButton.transform, serializedMoveButton);
            WireRangeSegmentReferences(moveButton, serializedMoveButton, applyLayoutTuning: false, ensureSprites: false);
            serializedMoveButton.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(moveButton);
            return;
        }

        ApplyButtonBackgroundForBake(moveButton.transform, serializedMoveButton, bakeMode);

        if (bakeMode == BakeMode.RebuildFinalize)
        {
            ClearBandBackgrounds(moveButton.transform);
            ApplyPlaceholderTexts(moveButton.transform, ResolveMoveButtonIndex(moveButton));
            WireRangeSegmentReferences(moveButton, serializedMoveButton, applyLayoutTuning: true, ensureSprites: true);
        }
        else
        {
            WireRangeSegmentReferences(moveButton, serializedMoveButton, applyLayoutTuning: false, ensureSprites: true);
        }

        ApplyTextAndImageStyles(serializedMoveButton);
        serializedMoveButton.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(moveButton);
    }

    private static void RebuildMoveButton(MoveButtonView moveButton)
    {
        if (moveButton == null)
        {
            return;
        }

        RectTransform rect = moveButton.transform as RectTransform;
        if (rect != null)
        {
            rect.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);
        }

        LayoutElement layoutElement = moveButton.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = moveButton.gameObject.AddComponent<LayoutElement>();
        }

        layoutElement.preferredWidth = ButtonWidth;
        layoutElement.preferredHeight = ButtonHeight;
        layoutElement.minWidth = ButtonWidth;
        layoutElement.minHeight = ButtonHeight;
        layoutElement.flexibleWidth = 0f;
        layoutElement.flexibleHeight = 0f;

        SerializedObject serializedMoveButton = new SerializedObject(moveButton);
        EnsureRebuildHierarchy(root: moveButton.transform, serializedMoveButton);
        WireMoveButton(moveButton, BakeMode.RebuildFinalize, applyLayout: false);
    }

    private static void EnsureRebuildHierarchy(Transform root, SerializedObject serializedMoveButton)
    {
        StripRebuildableChildren(root);
        EnsureBandObject(root, HeaderBandObjectName);
        EnsureBandObject(root, BodyPanelObjectName);
        EnsureMoveNameText(root, serializedMoveButton);
        EnsureFramedPartIcon(root, RequiredPartObjectName, -14f);
        EnsureFramedPartIcon(root, TargetPartObjectName, -52f);
        EnsureIconImage(root, serializedMoveButton);
        EnsurePartLabelText(root, RequiredPartLabelObjectName, serializedMoveButton, "requiredPartLabelText");
        EnsurePartLabelText(root, TargetPartLabelObjectName, serializedMoveButton, "targetPartLabelText");
        EnsureBandObject(root, StatsRowObjectName);
        EnsureBandObject(root, RangeFooterObjectName);
        EnsureRangeLabelText(root, serializedMoveButton);
        EnsureRangeSegments(root);
        ApplyStretchRect(ResolveButtonTransform(root) as RectTransform);
        ApplyContainedLayout(root, serializedMoveButton);
        serializedMoveButton.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Rebuild時にボタン本体以外の重複Hierarchyを除去する
    /// </summary>
    private static void StripRebuildableChildren(Transform root)
    {
        Transform button = ResolveButtonTransform(root);
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            if (button != null && child == button)
            {
                continue;
            }

            Object.DestroyImmediate(child.gameObject);
        }
    }

    private static Transform ResolveButtonTransform(Transform root)
    {
        Component button = SceneUiLhButtonUtility.FindInChildren(root, true);
        if (button != null)
        {
            return button.transform;
        }

        return root.Find("Button");
    }

    private static void ApplyContainedLayout(Transform root, SerializedObject serializedMoveButton)
    {
        float bodyTop = HeaderBandHeight;
        float statsTop = bodyTop + BodyPanelHeight;

        Transform headerBand = root.Find(HeaderBandObjectName);
        if (headerBand is RectTransform headerRect)
        {
            ApplyTopStretchRect(headerRect, 0f, HeaderBandHeight);
        }

        TMP_Text moveNameText = serializedMoveButton.FindProperty("moveNameText").objectReferenceValue as TMP_Text;
        if (moveNameText != null && headerBand != null)
        {
            moveNameText.transform.SetParent(headerBand, false);
            ApplyStretchRect(moveNameText.rectTransform);
        }

        Transform bodyPanel = root.Find(BodyPanelObjectName);
        if (bodyPanel is RectTransform bodyRect)
        {
            ApplyAnchoredRect(
                bodyRect,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -bodyTop),
                new Vector2(0f, BodyPanelHeight));
        }

        Image requiredPartImage = ResolvePartIconFrameImage(root, RequiredPartObjectName, "RequiredPartImage");
        if (requiredPartImage != null)
        {
            ParentTo(bodyPanel ?? root, requiredPartImage.transform);
            ApplyPartIconFrameRect(requiredPartImage.rectTransform, -14f);
        }

        serializedMoveButton.FindProperty("requiredPartImage").objectReferenceValue = requiredPartImage;

        TMP_Text requiredPartLabelText = serializedMoveButton.FindProperty("requiredPartLabelText").objectReferenceValue as TMP_Text;
        if (requiredPartLabelText != null)
        {
            ParentTo(bodyPanel ?? root, requiredPartLabelText.transform);
            ApplyAnchoredRect(
                requiredPartLabelText.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(ContentInset, -2f),
                new Vector2(PartLabelWidth, PartLabelHeight));
        }

        Image attributeImage = ResolvePartIconFrameImage(root, TargetPartObjectName, "AttributeImage");
        if (attributeImage != null)
        {
            ParentTo(bodyPanel ?? root, attributeImage.transform);
            ApplyPartIconFrameRect(attributeImage.rectTransform, -52f);
        }

        serializedMoveButton.FindProperty("attributeImage").objectReferenceValue = attributeImage;

        TMP_Text targetPartLabelText = serializedMoveButton.FindProperty("targetPartLabelText").objectReferenceValue as TMP_Text;
        if (targetPartLabelText != null)
        {
            ParentTo(bodyPanel ?? root, targetPartLabelText.transform);
            ApplyAnchoredRect(
                targetPartLabelText.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(ContentInset, -40f),
                new Vector2(PartLabelWidth, PartLabelHeight));
        }

        Image iconImage = serializedMoveButton.FindProperty("iconImage").objectReferenceValue as Image;
        if (iconImage != null)
        {
            ParentTo(bodyPanel ?? root, iconImage.transform);
            ApplyAnchoredRect(
                iconImage.rectTransform,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-ContentInset, -10f),
                new Vector2(MoveIconSize, MoveIconSize));
        }

        Transform statsRow = root.Find(StatsRowObjectName);
        if (statsRow is RectTransform statsRect)
        {
            ApplyAnchoredRect(
                statsRect,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -statsTop),
                new Vector2(0f, StatsRowHeight));
        }

        Transform powerColumn = EnsureChild(statsRow, PowerColumnObjectName);
        ApplyAnchoredRect(
            powerColumn as RectTransform,
            Vector2.zero,
            new Vector2(0.5f, 1f),
            new Vector2(0f, 1f),
            new Vector2(ContentInset, 0f),
            new Vector2(-ContentInset, 0f));
        TMP_Text powerLabelText = FindNamedTmpText(powerColumn, PowerLabelObjectName)
            ?? CreateLabelText(powerColumn, PowerLabelObjectName, "威力");
        TMP_Text powerValueText = FindNamedTmpText(powerColumn, PowerValueObjectName)
            ?? CreateValueText(powerColumn, PowerValueObjectName);
        ApplyColumnLabelRect(powerLabelText.rectTransform);
        ApplyColumnValueRect(powerValueText.rectTransform);
        serializedMoveButton.FindProperty("powerText").objectReferenceValue = powerValueText;

        Transform costColumn = EnsureChild(statsRow, CostColumnObjectName);
        ApplyAnchoredRect(
            costColumn as RectTransform,
            new Vector2(0.5f, 0f),
            Vector2.one,
            new Vector2(1f, 1f),
            new Vector2(-ContentInset, 0f),
            new Vector2(-ContentInset, 0f));
        TMP_Text costLabelText = FindNamedTmpText(costColumn, CostLabelObjectName)
            ?? CreateLabelText(costColumn, CostLabelObjectName, "コスト");
        TMP_Text costValueText = FindNamedTmpText(costColumn, CostValueObjectName)
            ?? CreateValueText(costColumn, CostValueObjectName);
        ApplyColumnLabelRect(costLabelText.rectTransform);
        ApplyColumnValueRect(costValueText.rectTransform);
        serializedMoveButton.FindProperty("gutsText").objectReferenceValue = costValueText;

        Transform rangeFooter = root.Find(RangeFooterObjectName);
        if (rangeFooter is RectTransform rangeFooterRect)
        {
            ApplyAnchoredRect(
                rangeFooterRect,
                Vector2.zero,
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                Vector2.zero,
                new Vector2(0f, RangeFooterHeight));
        }

        Transform rangeRow = EnsureChild(rangeFooter, RangeRowObjectName);
        ApplyStretchRect(rangeRow as RectTransform);

        TMP_Text rangeLabelText = serializedMoveButton.FindProperty("rangeLabelText").objectReferenceValue as TMP_Text;
        if (rangeLabelText != null)
        {
            rangeLabelText.transform.SetParent(rangeRow, false);
            ApplyAnchoredRect(
                rangeLabelText.rectTransform,
                Vector2.zero,
                new Vector2(0f, 1f),
                new Vector2(0f, 0.5f),
                new Vector2(ContentInset, 0f),
                new Vector2(34f, RangeFooterHeight));
        }

        Transform rangeRoot = FindNamedTransform(root, "RangeSegments");
        if (rangeRoot != null)
        {
            rangeRoot.SetParent(rangeRow, false);
            ApplyAnchoredRect(
                rangeRoot as RectTransform,
                new Vector2(0f, 0f),
                Vector2.one,
                new Vector2(1f, 0.5f),
                new Vector2(40f, 0f),
                new Vector2(-ContentInset, 0f));
            if (rangeRoot is RectTransform rangeRect)
            {
                rangeRect.sizeDelta = new Vector2(
                    MoveRangeSegmentBarView.BattleBarWidth,
                    MoveRangeSegmentBarView.BattleBarHeight);
            }
        }
    }

    private static void ParentTo(Transform parent, Transform child)
    {
        if (parent != null && child != null)
        {
            child.SetParent(parent, false);
        }
    }

    private static Transform EnsureChild(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child != null)
        {
            return child;
        }

        var childObject = new GameObject(childName, typeof(RectTransform));
        childObject.transform.SetParent(parent, false);
        return childObject.transform;
    }

    private static void ApplyColumnLabelRect(RectTransform rectTransform)
    {
        ApplyAnchoredRect(
            rectTransform,
            new Vector2(0f, 0.5f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            Vector2.zero,
            new Vector2(0f, 14f));
    }

    private static void ApplyColumnValueRect(RectTransform rectTransform)
    {
        ApplyAnchoredRect(
            rectTransform,
            Vector2.zero,
            new Vector2(1f, 0.5f),
            new Vector2(0.5f, 0f),
            Vector2.zero,
            new Vector2(0f, 18f));
    }

    private static TMP_Text CreateLabelText(Transform parent, string objectName, string label)
    {
        TMP_Text text = CreateValueText(parent, objectName);
        text.text = label;
        return text;
    }

    private static TMP_Text CreateValueText(Transform parent, string objectName)
    {
        var textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        return textObject.GetComponent<TMP_Text>();
    }

    private static void ClearBandBackgrounds(Transform root)
    {
        ClearBandBackground(root, HeaderBandObjectName);
        ClearBandBackground(root, BodyPanelObjectName);
        ClearBandBackground(root, StatsRowObjectName);
        ClearBandBackground(root, RangeFooterObjectName);
    }

    private static void ClearBandBackground(Transform root, string objectName)
    {
        Transform band = root.Find(objectName);
        if (band == null)
        {
            return;
        }

        if (!band.TryGetComponent(out Image image))
        {
            return;
        }

        image.sprite = null;
        image.color = Color.clear;
        image.raycastTarget = false;
    }

    private static Image EnsureSlotFrameImage(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        Transform existing = root.Find(SlotFrameObjectName);
        if (existing == null)
        {
            var frameObject = new GameObject(SlotFrameObjectName, typeof(RectTransform), typeof(Image));
            frameObject.transform.SetParent(root, false);
            existing = frameObject.transform;
        }

        existing.SetAsFirstSibling();
        ApplyStretchRect(existing as RectTransform);
        return existing.GetComponent<Image>();
    }

    private static void EnsureBandObject(Transform root, string objectName)
    {
        if (root.Find(objectName) != null)
        {
            return;
        }

        var bandObject = new GameObject(objectName, typeof(RectTransform));
        bandObject.transform.SetParent(root, false);
        int buttonSiblingIndex = root.Find("Button") != null ? root.Find("Button").GetSiblingIndex() : 0;
        bandObject.transform.SetSiblingIndex(buttonSiblingIndex + 1);
    }

    private static void EnsureMoveNameText(Transform root, SerializedObject serializedMoveButton)
    {
        Transform parent = root.Find(HeaderBandObjectName) ?? root;
        TMP_Text moveNameText = parent.Find(MoveNameObjectName)?.GetComponent<TMP_Text>();
        if (moveNameText == null)
        {
            var nameObject = new GameObject(MoveNameObjectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            nameObject.transform.SetParent(parent, false);
            moveNameText = nameObject.GetComponent<TMP_Text>();
        }

        serializedMoveButton.FindProperty("moveNameText").objectReferenceValue = moveNameText;
    }

    private static void EnsureRangeLabelText(Transform root, SerializedObject serializedMoveButton)
    {
        Transform parent = EnsureRangeRow(root) ?? root;
        TMP_Text rangeLabelText = parent.Find(RangeLabelObjectName)?.GetComponent<TMP_Text>();
        if (rangeLabelText == null)
        {
            var labelObject = new GameObject(RangeLabelObjectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(parent, false);
            rangeLabelText = labelObject.GetComponent<TMP_Text>();
        }

        serializedMoveButton.FindProperty("rangeLabelText").objectReferenceValue = rangeLabelText;
    }

    private static void EnsurePartLabelText(
        Transform root,
        string objectName,
        SerializedObject serializedMoveButton,
        string propertyName)
    {
        Transform parent = root.Find(BodyPanelObjectName) ?? root;
        TMP_Text labelText = parent.Find(objectName)?.GetComponent<TMP_Text>();
        if (labelText == null)
        {
            var labelObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(parent, false);
            labelText = labelObject.GetComponent<TMP_Text>();
        }

        serializedMoveButton.FindProperty(propertyName).objectReferenceValue = labelText;
    }

    private static void EnsureFramedPartIcon(Transform root, string objectName, float topOffset)
    {
        Transform parent = root.Find(BodyPanelObjectName) ?? root;
        Transform existing = parent.Find(objectName);
        if (existing == null)
        {
            existing = FindNamedTransform(root, objectName);
        }

        if (existing == null)
        {
            var frameObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            frameObject.transform.SetParent(parent, false);

            var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(frameObject.transform, false);
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            float inset = 2f;
            iconRect.offsetMin = new Vector2(inset, inset);
            iconRect.offsetMax = new Vector2(-inset, -inset);
            Image iconImage = iconObject.GetComponent<Image>();
            iconImage.raycastTarget = false;
            iconImage.preserveAspect = true;
            existing = frameObject.transform;
        }
        else if (existing.parent != parent)
        {
            existing.SetParent(parent, false);
        }

        ApplyPartIconFrameRect(existing as RectTransform, topOffset);

        if (existing.TryGetComponent(out Image frameImage))
        {
            TitleClayUiVisualUtility.ApplySlotThumbnailFrameForEditorBake(frameImage);
            frameImage.color = Color.white;
            frameImage.preserveAspect = true;
        }
    }

    private static void ApplyPartIconFrameRect(RectTransform rectTransform, float topOffset)
    {
        if (rectTransform == null)
        {
            return;
        }

        ApplyAnchoredRect(
            rectTransform,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(ContentInset, topOffset),
            new Vector2(PartIconSize, PartIconSize));
    }

    private static void EnsureIconImage(Transform root, SerializedObject serializedMoveButton)
    {
        Transform parent = root.Find(BodyPanelObjectName) ?? root;
        Image iconImage = parent.Find("IconImage")?.GetComponent<Image>();
        if (iconImage == null)
        {
            var iconObject = new GameObject("IconImage", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(parent, false);
            iconImage = iconObject.GetComponent<Image>();
            iconImage.raycastTarget = false;
            iconImage.preserveAspect = true;
        }

        serializedMoveButton.FindProperty("iconImage").objectReferenceValue = iconImage;
    }

    private static Transform EnsureRangeRow(Transform root)
    {
        Transform rangeFooter = root.Find(RangeFooterObjectName);
        if (rangeFooter == null)
        {
            return null;
        }

        return EnsureChild(rangeFooter, RangeRowObjectName);
    }

    private static void EnsureRangeSegments(Transform root)
    {
        if (FindNamedTransform(root, "RangeSegments") != null)
        {
            return;
        }

        Transform rangeRow = EnsureRangeRow(root);
        if (rangeRow == null)
        {
            return;
        }

        MoveRangeSegmentBarView.CreateBattleUi(rangeRow);
    }

    private static void ApplyButtonBackgroundForBake(
        Transform root,
        SerializedObject serializedMoveButton,
        BakeMode bakeMode)
    {
        Component button = serializedMoveButton.FindProperty("button").objectReferenceValue as Component;
        if (button == null)
        {
            button = SceneUiLhButtonUtility.FindInChildren(root, true);
        }

        if (button == null)
        {
            return;
        }

        if (bakeMode == BakeMode.RebuildFinalize)
        {
            Image backgroundImage = ApplyBattleButtonBackground(button);
            if (backgroundImage != null)
            {
                serializedMoveButton.FindProperty("buttonBackground").objectReferenceValue = backgroundImage;
            }

            return;
        }

        ApplyBattleButtonBackgroundIfMissing(button);
        WireButtonBackgroundReference(root, serializedMoveButton);
    }

    private static Image ApplyBattleButtonBackgroundIfMissing(Component buttonComponent)
    {
        if (buttonComponent is not Selectable selectable || selectable.targetGraphic is not Image buttonImage)
        {
            return null;
        }

        Transform root = buttonComponent.transform.parent;
        Image slotFrameImage = root != null ? root.Find(SlotFrameObjectName)?.GetComponent<Image>() : null;
        if (slotFrameImage != null && slotFrameImage.sprite == null)
        {
            ModelSaveSlotUiPrefabUtility.ApplyAttackSlotClayFrameSprite(slotFrameImage);
            slotFrameImage.color = Color.white;
            slotFrameImage.raycastTarget = false;
        }

        if (slotFrameImage == null && buttonImage.sprite == null)
        {
            ModelSaveSlotUiPrefabUtility.ApplyAttackSlotClayFrameSprite(buttonImage);
            buttonImage.color = Color.white;
        }

        ConfigureBattleButtonInteraction(selectable, buttonImage);
        return ResolveButtonBackgroundImage(root, buttonComponent);
    }

    private static void WireButtonBackgroundReference(Transform root, SerializedObject serializedMoveButton)
    {
        Component button = serializedMoveButton.FindProperty("button").objectReferenceValue as Component;
        if (button == null)
        {
            button = SceneUiLhButtonUtility.FindInChildren(root, true);
            AssignIfNull(serializedMoveButton, "button", button);
        }

        Image backgroundImage = ResolveButtonBackgroundImage(root, button);
        AssignIfNull(serializedMoveButton, "buttonBackground", backgroundImage);
    }

    private static Image ResolveButtonBackgroundImage(Transform root, Component buttonComponent)
    {
        if (root != null)
        {
            Image slotFrameImage = root.Find(SlotFrameObjectName)?.GetComponent<Image>();
            if (slotFrameImage != null)
            {
                return slotFrameImage;
            }
        }

        if (buttonComponent is Selectable selectable && selectable.targetGraphic is Image buttonImage)
        {
            return buttonImage;
        }

        return null;
    }

    private static void ConfigureBattleButtonInteraction(Selectable selectable, Image buttonImage)
    {
        if (selectable == null || buttonImage == null)
        {
            return;
        }

        selectable.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = selectable.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.04f, 1.02f, 0.98f, 1f);
        colors.pressedColor = new Color(0.9f, 0.88f, 0.84f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = TitleClayUiVisualUtility.DisabledColor;
        colors.fadeDuration = 0.08f;
        selectable.colors = colors;
        buttonImage.raycastTarget = true;
    }

    private static Image ApplyBattleButtonBackground(Component buttonComponent)
    {
        if (buttonComponent is not Selectable selectable)
        {
            return null;
        }

        if (selectable.targetGraphic is not Image buttonImage)
        {
            return null;
        }

        Transform root = buttonComponent.transform.parent;
        Image slotFrameImage = EnsureSlotFrameImage(root);
        if (slotFrameImage != null)
        {
            ModelSaveSlotUiPrefabUtility.ApplyAttackSlotClayFrameSprite(slotFrameImage);
            slotFrameImage.color = Color.white;
            slotFrameImage.raycastTarget = false;
        }

        buttonImage.sprite = null;
        buttonImage.color = Color.clear;
        buttonImage.raycastTarget = true;
        ConfigureBattleButtonInteraction(selectable, buttonImage);
        return slotFrameImage ?? buttonImage;
    }

    private static void WireMissingReferences(Transform moveButtonRoot, SerializedObject serializedMoveButton)
    {
        AssignIfNull(serializedMoveButton, "button", SceneUiLhButtonUtility.FindInChildren(moveButtonRoot, true));
        AssignIfNull(serializedMoveButton, "moveNameText", moveButtonRoot.Find(MoveNameObjectName)?.GetComponent<TMP_Text>());
        AssignIfNull(
            serializedMoveButton,
            "requiredPartImage",
            ResolvePartIconFrameImage(moveButtonRoot, RequiredPartObjectName, "RequiredPartImage"));
        AssignIfNull(
            serializedMoveButton,
            "attributeImage",
            ResolvePartIconFrameImage(moveButtonRoot, TargetPartObjectName, "AttributeImage"));
        AssignIfNull(serializedMoveButton, "powerText", FindNamedTmpText(moveButtonRoot, PowerValueObjectName));
        AssignIfNull(serializedMoveButton, "gutsText", FindNamedTmpText(moveButtonRoot, CostValueObjectName));
        AssignIfNull(serializedMoveButton, "rangeLabelText", moveButtonRoot.Find(RangeLabelObjectName)?.GetComponent<TMP_Text>());
        AssignIfNull(serializedMoveButton, "iconImage", FindImage(moveButtonRoot, "IconImage"));
        AssignIfNull(
            serializedMoveButton,
            "requiredPartLabelText",
            FindNamedTmpText(moveButtonRoot, RequiredPartLabelObjectName));
        AssignIfNull(
            serializedMoveButton,
            "targetPartLabelText",
            FindNamedTmpText(moveButtonRoot, TargetPartLabelObjectName));
    }

    private static void ApplyTextAndImageStyles(SerializedObject serializedMoveButton)
    {
        WireText(serializedMoveButton, "moveNameText", ApplyMoveNameStyle);
        WireText(serializedMoveButton, "powerText", ApplyStatValueStyle);
        WireText(serializedMoveButton, "gutsText", ApplyStatValueStyle);
        WireText(serializedMoveButton, "rangeLabelText", ApplyRangeLabelStyle);
        WireText(serializedMoveButton, "requiredPartLabelText", ApplyPartLabelStyle);
        WireText(serializedMoveButton, "targetPartLabelText", ApplyPartLabelStyle);
        WireStatLabelTexts(serializedMoveButton);
        WireImage(serializedMoveButton, "iconImage");
        ApplyPartIconFrameStyle(serializedMoveButton, "requiredPartImage");
        ApplyPartIconFrameStyle(serializedMoveButton, "attributeImage");
    }

    private static int ResolveMoveButtonIndex(MoveButtonView moveButton)
    {
        Transform parent = moveButton != null ? moveButton.transform.parent : null;
        if (parent == null)
        {
            return 0;
        }

        int index = 0;
        for (int i = 0; i < parent.childCount; i++)
        {
            MoveButtonView sibling = parent.GetChild(i).GetComponent<MoveButtonView>();
            if (sibling == null)
            {
                continue;
            }

            if (sibling == moveButton)
            {
                return index;
            }

            index++;
        }

        return 0;
    }

    private static void ApplyPlaceholderTexts(Transform root, int buttonIndex)
    {
        if (root == null)
        {
            return;
        }

        int index = Mathf.Clamp(buttonIndex, 0, PlaceholderMoveNames.Length - 1);
        SetPlaceholderText(FindNamedTmpText(root, MoveNameObjectName), PlaceholderMoveNames[index]);
        SetPlaceholderText(FindNamedTmpText(root, RequiredPartLabelObjectName), PlaceholderRequiredPartLabels[index]);
        SetPlaceholderText(FindNamedTmpText(root, TargetPartLabelObjectName), PlaceholderTargetPartLabels[index]);
        SetPlaceholderText(FindNamedTmpText(root, PowerLabelObjectName), PlaceholderPowerLabel);
        SetPlaceholderText(FindNamedTmpText(root, CostLabelObjectName), PlaceholderCostLabel);
        SetPlaceholderText(FindNamedTmpText(root, PowerValueObjectName), PlaceholderPowerValues[index]);
        SetPlaceholderText(FindNamedTmpText(root, CostValueObjectName), PlaceholderCostValues[index]);
        SetPlaceholderText(FindNamedTmpText(root, RangeLabelObjectName), PlaceholderRangeLabel);
    }

    private static void SetPlaceholderText(TMP_Text text, string value)
    {
        if (text != null)
        {
            text.text = value;
        }
    }

    private static void WireStatLabelTexts(SerializedObject serializedMoveButton)
    {
        Transform root = (serializedMoveButton.targetObject as Component)?.transform;
        if (root == null)
        {
            return;
        }

        TMP_Text powerLabel = FindNamedTmpText(root, PowerLabelObjectName);
        if (powerLabel != null)
        {
            ApplyStatLabelStyle(powerLabel);
            powerLabel.raycastTarget = false;
        }

        TMP_Text costLabel = FindNamedTmpText(root, CostLabelObjectName);
        if (costLabel != null)
        {
            ApplyStatLabelStyle(costLabel);
            costLabel.raycastTarget = false;
        }
    }

    private static void ApplyPartIconFrameStyle(SerializedObject serializedMoveButton, string propertyName)
    {
        SerializedProperty property = serializedMoveButton.FindProperty(propertyName);
        if (property?.objectReferenceValue is not Image image)
        {
            return;
        }

        Image frameImage = image.gameObject.name == "Icon" && image.transform.parent != null
            ? image.transform.parent.GetComponent<Image>()
            : image;
        if (frameImage != null)
        {
            TitleClayUiVisualUtility.ApplySlotThumbnailFrameForEditorBake(frameImage);
            frameImage.color = Color.white;
        }

        Image iconImage = image.gameObject.name == "Icon" ? image : image.transform.Find("Icon")?.GetComponent<Image>();
        if (iconImage != null)
        {
            iconImage.raycastTarget = false;
            iconImage.preserveAspect = true;
        }
    }

    private static void AssignIfNull(SerializedObject serializedObject, string propertyName, UnityEngine.Object value)
    {
        if (value == null)
        {
            return;
        }

        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || property.objectReferenceValue != null)
        {
            return;
        }

        property.objectReferenceValue = value;
    }

    private static TMP_Text FindNamedTmpText(Transform root, string objectName)
    {
        if (root == null)
        {
            return null;
        }

        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text != null && text.name == objectName)
            {
                return text;
            }
        }

        return null;
    }

    private static Image FindImage(Transform root, string objectName)
    {
        Transform target = root.Find(objectName);
        return target != null ? target.GetComponent<Image>() : null;
    }

    private static void WireText(
        SerializedObject serializedMoveButton,
        string propertyName,
        System.Action<TMP_Text> applyStyle)
    {
        SerializedProperty property = serializedMoveButton.FindProperty(propertyName);
        if (property == null)
        {
            return;
        }

        TMP_Text text = property.objectReferenceValue as TMP_Text;
        if (text == null)
        {
            return;
        }

        applyStyle(text);
        text.raycastTarget = false;
    }

    private static void WireImage(SerializedObject serializedMoveButton, string propertyName)
    {
        SerializedProperty property = serializedMoveButton.FindProperty(propertyName);
        if (property == null)
        {
            return;
        }

        if (property.objectReferenceValue is not Image image)
        {
            return;
        }

        image.raycastTarget = false;
        image.preserveAspect = true;
    }

    private static void WireRangeSegmentReferences(
        MoveButtonView moveButton,
        SerializedObject serializedMoveButton,
        bool applyLayoutTuning,
        bool ensureSprites)
    {
        Transform rangeRoot = moveButton.transform.Find("RangeSegments");
        if (rangeRoot == null)
        {
            rangeRoot = FindNamedTransform(moveButton.transform, "RangeSegments");
        }

        if (rangeRoot == null)
        {
            return;
        }

        MoveRangeSegmentBarView rangeBar = rangeRoot.GetComponent<MoveRangeSegmentBarView>();
        if (rangeBar == null)
        {
            rangeBar = rangeRoot.gameObject.AddComponent<MoveRangeSegmentBarView>();
        }

        Image[] segments = rangeRoot.GetComponent<HorizontalLayoutGroup>() != null
            ? CollectRangeSegmentImages(rangeRoot)
            : null;

        SerializedProperty rangeSegmentsProperty = serializedMoveButton.FindProperty("rangeSegments");
        if (rangeSegmentsProperty != null && segments != null)
        {
            rangeSegmentsProperty.arraySize = segments.Length;
            for (int i = 0; i < segments.Length; i++)
            {
                rangeSegmentsProperty.GetArrayElementAtIndex(i).objectReferenceValue = segments[i];
            }
        }

        if (applyLayoutTuning)
        {
            HorizontalLayoutGroup layoutGroup = rangeRoot.GetComponent<HorizontalLayoutGroup>();
            if (layoutGroup != null)
            {
                layoutGroup.padding = new RectOffset(0, 0, 0, 0);
                layoutGroup.spacing = MoveRangeSegmentBarView.BattleSegmentSpacing;
                layoutGroup.childAlignment = TextAnchor.MiddleCenter;
                layoutGroup.childControlWidth = false;
                layoutGroup.childControlHeight = false;
                layoutGroup.childForceExpandWidth = false;
                layoutGroup.childForceExpandHeight = false;
            }
        }

        rangeBar.Bind(segments);
        if (ensureSprites)
        {
            rangeBar.EnsureSprites();
        }

        EditorUtility.SetDirty(rangeBar);
    }

    private static Transform FindNamedTransform(Transform root, string objectName)
    {
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child != null && child.name == objectName)
            {
                return child;
            }
        }

        return null;
    }

    private static Image[] CollectRangeSegmentImages(Transform rangeRoot)
    {
        Image[] segments = new Image[MoveRangeSegmentBarView.SegmentCount];
        int index = 0;
        for (int i = 0; i < rangeRoot.childCount && index < segments.Length; i++)
        {
            Image image = rangeRoot.GetChild(i).GetComponent<Image>();
            if (image == null)
            {
                continue;
            }

            segments[index] = image;
            index++;
        }

        return segments;
    }

    private static void ApplyStretchRect(RectTransform rectTransform)
    {
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;
    }

    private static void ApplyTopStretchRect(RectTransform rectTransform, float topOffset, float height)
    {
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(1f, 1f);
        rectTransform.pivot = new Vector2(0.5f, 1f);
        rectTransform.anchoredPosition = new Vector2(0f, -topOffset);
        rectTransform.sizeDelta = new Vector2(0f, height);
    }

    private static void ApplyAnchoredRect(
        RectTransform rectTransform,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.pivot = pivot;
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = sizeDelta;
    }

    private static Image ResolvePartIconFrameImage(Transform root, string framedObjectName, string legacyObjectName)
    {
        Transform framed = FindNamedTransform(root, framedObjectName);
        if (framed != null && framed.TryGetComponent(out Image frameImage))
        {
            return frameImage;
        }

        Transform legacy = FindNamedTransform(root, legacyObjectName);
        if (legacy != null && legacy.TryGetComponent(out Image legacyImage))
        {
            return legacyImage;
        }

        return null;
    }

    private static void ApplyMoveNameStyle(TMP_Text text)
    {
        TitleClayUiVisualUtility.ApplySlotLabel(text);
        text.fontSize = 16f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = MoveButtonTextColor;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.enableWordWrapping = false;
        ApplyClayOutline(text, 0.24f);
    }

    private static void ApplyStatLabelStyle(TMP_Text text)
    {
        ApplyClayOutlinedText(text, 10f, FontStyles.Bold, TextAlignmentOptions.Center, 0.22f);
        text.enableWordWrapping = false;
    }

    private static void ApplyStatValueStyle(TMP_Text text)
    {
        ApplyClayOutlinedText(text, 14f, FontStyles.Bold, TextAlignmentOptions.Center, 0.26f);
        text.enableWordWrapping = false;
    }

    private static void ApplyPartLabelStyle(TMP_Text text)
    {
        ApplyClayOutlinedText(text, 9f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, 0.2f);
        text.enableWordWrapping = false;
    }

    private static void ApplyRangeLabelStyle(TMP_Text text)
    {
        ApplyClayOutlinedText(text, 10f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, 0.22f);
    }

    private static void ApplyClayOutlinedText(
        TMP_Text text,
        float fontSize,
        FontStyles fontStyle,
        TextAlignmentOptions alignment,
        float outlineWidth)
    {
        TitleClayUiVisualUtility.ApplySubLabelText(text);
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = MoveButtonTextColor;
        ApplyClayOutline(text, outlineWidth);
    }

    private static readonly string[] SingletonObjectNamesPerMoveButton =
    {
        SlotFrameObjectName,
        HeaderBandObjectName,
        BodyPanelObjectName,
        StatsRowObjectName,
        RangeFooterObjectName,
        MoveNameObjectName,
        RequiredPartObjectName,
        TargetPartObjectName,
        RequiredPartLabelObjectName,
        TargetPartLabelObjectName,
        "IconImage",
        PowerColumnObjectName,
        CostColumnObjectName,
        PowerLabelObjectName,
        PowerValueObjectName,
        CostLabelObjectName,
        CostValueObjectName,
        RangeRowObjectName,
        RangeLabelObjectName,
        "RangeSegments",
    };

    private static readonly string[] LegacyObjectNames =
    {
        "StatsFooter",
        "StatsGrid",
        "ContentInner",
        "MoveNameEnText",
        "PowerText",
        "CostText",
        "AttributeImage",
        "RequiredPartImage",
        "Power",
        "Cost",
    };

    /// <summary>
    /// MoveButtons配下の重複オブジェクトと旧レイアウト残骸を検査する
    /// </summary>
    public static bool TryValidateMoveButtonsRoot(Transform moveButtonsRoot, out string report)
    {
        report = string.Empty;
        if (moveButtonsRoot == null)
        {
            report = "MoveButtons root is null";
            return false;
        }

        var issues = new List<string>();
        int moveButtonIndex = 0;
        for (int i = 0; i < moveButtonsRoot.childCount; i++)
        {
            MoveButtonView moveButton = moveButtonsRoot.GetChild(i).GetComponent<MoveButtonView>();
            if (moveButton == null)
            {
                continue;
            }

            moveButtonIndex++;
            CollectHierarchyIssues(moveButton.transform, moveButtonIndex, issues);
        }

        if (moveButtonIndex == 0)
        {
            report = "MoveButtonView が見つかりません";
            return false;
        }

        if (issues.Count == 0)
        {
            report = $"OK: MoveButtonView={moveButtonIndex} 重複と旧オブジェクトはありません";
            return true;
        }

        var builder = new StringBuilder();
        builder.AppendLine($"NG: MoveButtonView={moveButtonIndex} 問題={issues.Count}");
        for (int i = 0; i < issues.Count; i++)
        {
            builder.AppendLine(issues[i]);
        }

        report = builder.ToString().TrimEnd();
        return false;
    }

    private static void CollectHierarchyIssues(Transform moveButtonRoot, int moveButtonIndex, List<string> issues)
    {
        string buttonLabel = $"MoveButtonView[{moveButtonIndex}] ({moveButtonRoot.name})";
        Dictionary<string, int> nameCounts = CountNamedObjects(moveButtonRoot);

        for (int i = 0; i < SingletonObjectNamesPerMoveButton.Length; i++)
        {
            string objectName = SingletonObjectNamesPerMoveButton[i];
            if (!nameCounts.TryGetValue(objectName, out int count))
            {
                continue;
            }

            if (count != 1)
            {
                issues.Add($"{buttonLabel}: {objectName} x{count} (expected 1)");
            }
        }

        for (int i = 0; i < LegacyObjectNames.Length; i++)
        {
            string objectName = LegacyObjectNames[i];
            if (nameCounts.TryGetValue(objectName, out int count) && count > 0)
            {
                issues.Add($"{buttonLabel}: legacy {objectName} x{count}");
            }
        }
    }

    private static Dictionary<string, int> CountNamedObjects(Transform root)
    {
        var counts = new Dictionary<string, int>();
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null)
            {
                continue;
            }

            if (!counts.TryGetValue(child.name, out int count))
            {
                count = 0;
            }

            counts[child.name] = count + 1;
        }

        return counts;
    }

    private static void ApplyClayOutline(TMP_Text text, float outlineWidth)
    {
        AppTmpFontUtility.ApplyOutline(
            text,
            outlineWidth,
            new Color(0.30f, 0.14f, 0.10f, 1f));
    }
}
#endif
