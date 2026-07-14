#if UNITY_EDITOR
using Battle;
using ClayEditor.Rigging;
using TMPro;
using UI.Battle.View;
using UI.ClayEditor.View;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 育成リザルトと同型の攻撃スロットUIをEditor上で組み立てる
/// シーン配置済みUIのレイアウト変更はRebuildのみ。通常の配線はTryWireを使う
/// </summary>
public static class AttackSlotUiEditorUtility
{
    public const int AttackSlotCount = TrainingResumeAttacksContentView.AttackSlotCount;
    public const float AttacksHeaderHeight = 28f;
    public const float AttackSlotHeight = 108f;
    public const float AttackSlotSpacing = 8f;

    /// <summary>
    /// 既存攻撃列へ参照だけ配線する
    /// レイアウトは変更しない
    /// </summary>
    public static bool TryWireSaveSlotAttacksContent(RectTransform attacksColumn)
    {
        if (attacksColumn == null)
        {
            return false;
        }

        RemoveLegacyAttackEntries(attacksColumn);
        if (!HasCompleteAttackSlotStructure(attacksColumn))
        {
            return false;
        }

        TrainingResumeAttacksContentView contentView = attacksColumn.GetComponent<TrainingResumeAttacksContentView>();
        if (contentView == null)
        {
            contentView = attacksColumn.gameObject.AddComponent<TrainingResumeAttacksContentView>();
        }

        TrainingAttackSlotView[] slotViews = CollectAttackSlotViews(attacksColumn);
        for (int i = 0; i < slotViews.Length; i++)
        {
            if (slotViews[i] != null)
            {
                WireAttackSlotReferences(slotViews[i]);
            }
        }

        ApplyContentViewReferences(contentView, slotViews);
        return true;
    }

    /// <summary>
    /// プレハブ初回生成専用
    /// 攻撃列UIを組み立て直し既存レイアウトを上書きする
    /// </summary>
    public static void RebuildSaveSlotAttacksContent(RectTransform attacksColumn)
    {
        if (attacksColumn == null)
        {
            return;
        }

        RemoveLegacyAttackEntries(attacksColumn);

        TrainingResumeAttacksContentView contentView = attacksColumn.GetComponent<TrainingResumeAttacksContentView>();
        if (contentView == null)
        {
            contentView = attacksColumn.gameObject.AddComponent<TrainingResumeAttacksContentView>();
        }

        VerticalLayoutGroup layoutGroup = attacksColumn.GetComponent<VerticalLayoutGroup>();
        if (layoutGroup != null)
        {
            Object.DestroyImmediate(layoutGroup);
        }

        var slotViews = new TrainingAttackSlotView[AttackSlotCount];
        float slotTop = 0f;
        for (int i = 0; i < AttackSlotCount; i++)
        {
            string slotName = "AttackSlot" + (i + 1);
            Transform existing = attacksColumn.Find(slotName);
            if (existing != null)
            {
                slotViews[i] = existing.GetComponent<TrainingAttackSlotView>();
                if (slotViews[i] != null)
                {
                    WireAttackSlotReferences(slotViews[i]);
                    ConfigureAttackSlotRect(existing as RectTransform, slotTop);
                    slotTop -= AttackSlotHeight + AttackSlotSpacing;
                    continue;
                }
            }

            slotViews[i] = CreateLayoutAttackSlot(attacksColumn, slotName, slotTop);
            slotTop -= AttackSlotHeight + AttackSlotSpacing;
        }

        ApplyContentViewReferences(contentView, slotViews);
    }

    /// <summary>
    /// 表示する攻撃数から攻撃列の高さを返す
    /// </summary>
    public static float ResolveAttacksStackHeight(int attackCount)
    {
        if (attackCount <= 0)
        {
            return 0f;
        }

        int slotCount = Mathf.Min(attackCount, AttackSlotCount);
        return slotCount * AttackSlotHeight + Mathf.Max(0, slotCount - 1) * AttackSlotSpacing;
    }

    private static bool HasCompleteAttackSlotStructure(RectTransform attacksColumn)
    {
        for (int i = 0; i < AttackSlotCount; i++)
        {
            Transform slotTransform = attacksColumn.Find("AttackSlot" + (i + 1));
            if (slotTransform == null || slotTransform.GetComponent<TrainingAttackSlotView>() == null)
            {
                return false;
            }
        }

        return true;
    }

    private static TrainingAttackSlotView[] CollectAttackSlotViews(RectTransform attacksColumn)
    {
        var slotViews = new TrainingAttackSlotView[AttackSlotCount];
        for (int i = 0; i < AttackSlotCount; i++)
        {
            Transform slotTransform = attacksColumn.Find("AttackSlot" + (i + 1));
            slotViews[i] = slotTransform != null
                ? slotTransform.GetComponent<TrainingAttackSlotView>()
                : null;
        }

        return slotViews;
    }

    private static void ApplyContentViewReferences(
        TrainingResumeAttacksContentView contentView,
        TrainingAttackSlotView[] slotViews)
    {
        if (contentView == null)
        {
            return;
        }

        SerializedObject serializedContent = new SerializedObject(contentView);
        serializedContent.FindProperty("headerText").objectReferenceValue = null;
        serializedContent.FindProperty("emptyText").objectReferenceValue = null;
        SerializedProperty slotsProperty = serializedContent.FindProperty("attackSlots");
        slotsProperty.arraySize = AttackSlotCount;
        for (int i = 0; i < AttackSlotCount; i++)
        {
            slotsProperty.GetArrayElementAtIndex(i).objectReferenceValue = slotViews[i];
        }

        serializedContent.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(contentView);
    }

    private static void RemoveLegacyAttackEntries(RectTransform attacksColumn)
    {
        for (int i = attacksColumn.childCount - 1; i >= 0; i--)
        {
            Transform child = attacksColumn.GetChild(i);
            if (child.GetComponent<TrainingAttackSlotView>() != null)
            {
                continue;
            }

            if (child.name.StartsWith("AttackSlot")
                || child.name == "AttackEntry"
                || child.name == "AttackChip")
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }
    }

    private static TrainingAttackSlotView CreateLayoutAttackSlot(Transform parent, string slotName, float topOffsetY)
    {
        var slotObject = new GameObject(slotName, typeof(RectTransform), typeof(TrainingAttackSlotView));
        slotObject.transform.SetParent(parent, false);
        ConfigureAttackSlotRect(slotObject.GetComponent<RectTransform>(), topOffsetY);
        BuildAttackSlotInterior(slotObject.transform);
        TrainingAttackSlotView slotView = slotObject.GetComponent<TrainingAttackSlotView>();
        WireAttackSlotReferences(slotView);
        slotObject.SetActive(false);
        return slotView;
    }

    private static void ConfigureAttackSlotRect(RectTransform slotRect, float topOffsetY)
    {
        if (slotRect == null)
        {
            return;
        }

        slotRect.anchorMin = new Vector2(0f, 1f);
        slotRect.anchorMax = new Vector2(1f, 1f);
        slotRect.pivot = new Vector2(0.5f, 1f);
        slotRect.anchoredPosition = new Vector2(0f, topOffsetY);
        slotRect.sizeDelta = new Vector2(0f, AttackSlotHeight);
    }

    private static void BuildAttackSlotInterior(Transform slotTransform)
    {
        Image slotFrameImage = EnsureAttackSlotFrame(slotTransform);

        TMP_Text attackNameText = CreateSlotLabel(
            slotTransform,
            "AttackNameText",
            new Vector2(0f, 1f),
            Vector2.zero,
            18f,
            TextAlignmentOptions.TopLeft);
        ConfigureTopLeftRect(attackNameText.rectTransform, Vector2.zero, new Vector2(400f, 22f));
        TitleClayUiVisualUtility.ApplySlotLabel(attackNameText);

        RectTransform partInfoRow = MoveAttackPartInfoView.Create(
            slotTransform,
            32f,
            12f,
            MotionType.Punch,
            largeLayout: false);
        ConfigureTopLeftRect(partInfoRow, new Vector2(0f, -26f), new Vector2(400f, 32f));

        Image requiredPartIcon = partInfoRow.Find("RequiredPartGroup/RequiredPartIcon/Icon")?.GetComponent<Image>();
        Image targetPartIcon = partInfoRow.Find("TargetPartGroup/TargetPartIcon/Icon")?.GetComponent<Image>();

        CreateSceneAttackStatsRow(slotTransform, out TMP_Text damageText, out TMP_Text costText, out TMP_Text rangeLabelText, out MoveRangeSegmentBarView rangeBar);
        Transform statsRow = slotTransform.Find("StatsRow");
        if (statsRow is RectTransform statsRect)
        {
            ConfigureTopLeftRect(statsRect, new Vector2(0f, -62f), new Vector2(400f, 24f));
        }

        TrainingAttackSlotView slotView = slotTransform.GetComponent<TrainingAttackSlotView>();
        SerializedObject serializedSlot = new SerializedObject(slotView);
        SetObjectReference(serializedSlot, "attackNameText", attackNameText);
        SetObjectReference(serializedSlot, "requiredPartIcon", requiredPartIcon);
        SetObjectReference(serializedSlot, "targetPartIcon", targetPartIcon);
        SetObjectReference(serializedSlot, "damageText", damageText);
        SetObjectReference(serializedSlot, "costText", costText);
        SetObjectReference(serializedSlot, "rangeLabelText", rangeLabelText);
        SetObjectReference(serializedSlot, "rangeBar", rangeBar);
        serializedSlot.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void WireAttackSlotReferences(TrainingAttackSlotView slotView)
    {
        if (slotView == null)
        {
            return;
        }

        Transform slotTransform = slotView.transform;
        EnsureAttackSlotFrame(slotTransform);
        TMP_Text attackNameText = slotTransform.Find("AttackNameText")?.GetComponent<TMP_Text>();
        Transform partInfoRow = slotTransform.Find("PartInfoRow");
        Image requiredPartIcon = partInfoRow != null
            ? partInfoRow.Find("RequiredPartGroup/RequiredPartIcon/Icon")?.GetComponent<Image>()
            : null;
        Image targetPartIcon = partInfoRow != null
            ? partInfoRow.Find("TargetPartGroup/TargetPartIcon/Icon")?.GetComponent<Image>()
            : null;

        Transform statsRow = slotTransform.Find("StatsRow");
        TMP_Text damageText = FindChildText(statsRow, "DamageText");
        TMP_Text costText = FindChildText(statsRow, "CostText");
        TMP_Text rangeLabelText = FindRangeLabelText(statsRow);
        MoveRangeSegmentBarView rangeBar = statsRow != null
            ? statsRow.GetComponentInChildren<MoveRangeSegmentBarView>(true)
            : slotTransform.GetComponentInChildren<MoveRangeSegmentBarView>(true);

        SerializedObject serializedSlot = new SerializedObject(slotView);
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
        Transform existingFrame = slotTransform.Find("SlotFrame");
        if (existingFrame != null)
        {
            Image existingImage = existingFrame.GetComponent<Image>();
            if (existingImage != null)
            {
                return EnsureAttackSlotFrameExisting(existingImage);
            }
        }

        var frameObject = new GameObject("SlotFrame", typeof(RectTransform), typeof(Image));
        frameObject.transform.SetParent(slotTransform, false);
        frameObject.transform.SetAsFirstSibling();

        RectTransform frameRect = frameObject.GetComponent<RectTransform>();
        frameRect.anchorMin = Vector2.zero;
        frameRect.anchorMax = Vector2.one;
        frameRect.offsetMin = Vector2.zero;
        frameRect.offsetMax = Vector2.zero;

        Image frameImage = frameObject.GetComponent<Image>();
        frameImage.raycastTarget = false;
        ModelSaveSlotUiPrefabUtility.ApplySaveSlotAttackSlotFrameVisual(frameImage);
        return frameImage;
    }

    private static Image EnsureAttackSlotFrameExisting(Image existingImage)
    {
        existingImage.transform.SetAsFirstSibling();
        ModelSaveSlotUiPrefabUtility.ApplySaveSlotAttackSlotFrameVisual(existingImage);
        return existingImage;
    }

    private static void CreateSceneAttackStatsRow(
        Transform parent,
        out TMP_Text damageText,
        out TMP_Text costText,
        out TMP_Text rangeLabelText,
        out MoveRangeSegmentBarView rangeBar)
    {
        var rowObject = new GameObject("StatsRow", typeof(RectTransform));
        rowObject.transform.SetParent(parent, false);
        ConfigureTopLeftRect(rowObject.GetComponent<RectTransform>(), new Vector2(0f, -62f), new Vector2(400f, 24f));

        damageText = CreateSlotLabel(
            rowObject.transform,
            "DamageText",
            new Vector2(0f, 1f),
            Vector2.zero,
            16f,
            TextAlignmentOptions.MidlineLeft);
        ConfigureTopLeftRect(damageText.rectTransform, Vector2.zero, new Vector2(118f, 24f));
        TitleClayUiVisualUtility.ApplySubLabelText(damageText);

        costText = CreateSlotLabel(
            rowObject.transform,
            "CostText",
            new Vector2(0f, 1f),
            new Vector2(124f, 0f),
            16f,
            TextAlignmentOptions.MidlineLeft);
        ConfigureTopLeftRect(costText.rectTransform, new Vector2(124f, 0f), new Vector2(118f, 24f));
        TitleClayUiVisualUtility.ApplySubLabelText(costText);

        var rangeGroupObject = new GameObject("RangeGroup", typeof(RectTransform));
        rangeGroupObject.transform.SetParent(rowObject.transform, false);
        ConfigureTopLeftRect(
            rangeGroupObject.GetComponent<RectTransform>(),
            new Vector2(248f, 0f),
            new Vector2(MoveRangeSegmentBarView.CompactBarWidth + 56f, 24f));

        rangeLabelText = CreateSlotLabel(
            rangeGroupObject.transform,
            "RangeText",
            new Vector2(0f, 1f),
            Vector2.zero,
            16f,
            TextAlignmentOptions.MidlineLeft);
        ConfigureTopLeftRect(rangeLabelText.rectTransform, Vector2.zero, new Vector2(48f, 24f));
        rangeLabelText.text = ModelSaveSummaryFormatter.TrainingAttackRangeLabel;
        TitleClayUiVisualUtility.ApplySubLabelText(rangeLabelText);

        var rangeColumnObject = new GameObject("RangeColumn", typeof(RectTransform));
        rangeColumnObject.transform.SetParent(rangeGroupObject.transform, false);
        ConfigureTopLeftRect(
            rangeColumnObject.GetComponent<RectTransform>(),
            new Vector2(52f, 0f),
            new Vector2(MoveRangeSegmentBarView.CompactBarWidth, MoveRangeSegmentBarView.CompactBarHeight));
        rangeBar = MoveRangeSegmentBarView.CreateCompact(rangeColumnObject.transform);
    }

    private static TMP_Text CreateSlotLabel(
        Transform parent,
        string name,
        Vector2 anchor,
        Vector2 anchoredPosition,
        float fontSize,
        TextAlignmentOptions alignment)
    {
        var labelObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(parent, false);
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = anchoredPosition;

        TMP_Text label = labelObject.GetComponent<TextMeshProUGUI>();
        label.fontSize = fontSize;
        label.raycastTarget = false;
        return label;
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

    private static TMP_Text FindRangeLabelText(Transform statsRow)
    {
        return FindChildText(statsRow, "RangeText")
            ?? FindChildText(statsRow?.Find("RangeGroup"), "RangeText");
    }

    private static TMP_Text FindChildText(Transform parent, string childName)
    {
        return parent != null ? parent.Find(childName)?.GetComponent<TMP_Text>() : null;
    }

    private static void SetObjectReference(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }
}
#endif
