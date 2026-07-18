#if UNITY_EDITOR
using SaveData;
using System.Collections.Generic;
using System.IO;
using UI.ClayEditor.View;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// セーブスロットUIの参照配線のみを行う
/// プレハブ再生成・シーン再配置・Override破棄は行わない
/// </summary>
public static class ModelSaveSlotUiPrefabUtility
{
    public const string ScrollListPrefabPath = "Assets/Resources/UI/ModelSaveSlotScrollList.prefab";
    public const string ConfirmViewPrefabPath = "Assets/Resources/UI/ModelSaveConfirmView.prefab";

    private const string ScrollRootName = "SlotScrollList";
    private const string SlotFrameSpriteAssetPath = "Assets/Resources/Image/GameUi/GameUi_SliderFill.png";

    /// <summary>
    /// プレハブアセットの存在を確認する
    /// 欠落時は生成せずエラーを出す
    /// </summary>
    public static void EnsurePrefabAssetsExist()
    {
        if (!File.Exists(ScrollListPrefabPath))
        {
            Debug.LogError(
                $"[ModelSaveSlotUiPrefabUtility] プレハブが見つかりません: {ScrollListPrefabPath}");
        }

        if (!File.Exists(ConfirmViewPrefabPath))
        {
            Debug.LogError(
                $"[ModelSaveSlotUiPrefabUtility] プレハブが見つかりません: {ConfirmViewPrefabPath}");
        }
    }

    /// <summary>
    /// セーブスロットAttackSlotのSlotFrameへ正しいSpriteを適用する
    /// </summary>
    public static void ApplySaveSlotAttackSlotFrameVisual(Image image)
    {
        TryRestoreSaveSlotAttackSlotFrame(image, ResolveSaveSlotSlotFrameReferenceSprite());
    }

    /// <summary>
    /// 攻撃スロット枠の粘土スプライトを任意Imageへ適用する
    /// </summary>
    public static bool ApplyAttackSlotClayFrameSprite(Image image)
    {
        Sprite referenceSprite = ResolveSaveSlotSlotFrameReferenceSprite();
        if (image == null || referenceSprite == null)
        {
            return false;
        }

        image.sprite = referenceSprite;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1f;
        if (image.color.a <= 0f)
        {
            image.color = Color.white;
        }

        return true;
    }

    /// <summary>
    /// ModelSaveSlotScrollListプレハブのインスタンスかどうかを返す
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
    /// 親配下の既存ScrollListを返す
    /// 無い場合のみプレハブから1回生成する既存を破棄しない
    /// </summary>
    public static ModelSaveSlotScrollListView EnsureScrollListPrefabInstance(Transform parent)
    {
        if (parent == null)
        {
            return null;
        }

        ModelSaveSlotScrollListView existingScrollList =
            parent.GetComponentInChildren<ModelSaveSlotScrollListView>(true);
        if (existingScrollList != null)
        {
            WireScrollListReferencesOnly(existingScrollList);
            return existingScrollList;
        }

        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ScrollListPrefabPath);
        if (prefabAsset == null)
        {
            Debug.LogError(
                $"[ModelSaveSlotUiPrefabUtility] プレハブが見つかりません: {ScrollListPrefabPath}");
            return null;
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(prefabAsset, parent) as GameObject;
        if (instance == null)
        {
            Debug.LogError(
                "[ModelSaveSlotUiPrefabUtility] ModelSaveSlotScrollListの配置に失敗しました",
                parent.gameObject);
            return null;
        }

        ModelSaveSlotScrollListView scrollList = instance.GetComponent<ModelSaveSlotScrollListView>();
        WireScrollListReferencesOnly(scrollList);
        EditorUtility.SetDirty(instance);
        return scrollList;
    }

    /// <summary>
    /// スロット一覧Viewの参照を配線する
    /// HierarchyとRectTransformは変更しない
    /// </summary>
    public static void WireScrollListReferences(ModelSaveSlotScrollListView scrollList)
    {
        WireScrollListReferencesOnly(scrollList);
    }

    /// <summary>
    /// プレハブ配置済みスロット一覧の参照だけ配線する
    /// 行の生成やRectTransform変更は行わない
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
        WireScrollListReferencesOnly(scrollList);
    }

    /// <summary>
    /// 確認パネル上にModelSaveConfirmViewを確保する
    /// 未配置時のみプレハブから生成する既存は破棄しない
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
            WireConfirmViewReferences(existing);
            return existing;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ConfirmViewPrefabPath);
        if (prefab == null)
        {
            Debug.LogError(
                $"[ModelSaveSlotUiPrefabUtility] プレハブが見つかりません: {ConfirmViewPrefabPath}");
            return null;
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, panelRoot) as GameObject;
        if (instance == null)
        {
            Debug.LogError(
                "[ModelSaveSlotUiPrefabUtility] ModelSaveConfirmViewの配置に失敗しました",
                panelRoot.gameObject);
            return null;
        }

        ModelSaveConfirmView confirmView = instance.GetComponent<ModelSaveConfirmView>();
        WireConfirmViewReferences(confirmView);
        return confirmView;
    }

    /// <summary>
    /// 確認キャンバス上にModelSaveConfirmViewを確保する
    /// 未配置時のみプレハブから生成する既存は破棄しない
    /// </summary>
    public static ModelSaveConfirmView EnsureConfirmViewOnCanvas(Canvas canvas)
    {
        if (canvas == null)
        {
            return null;
        }

        return EnsureConfirmViewOnRoot(canvas.transform);
    }

    /// <summary>
    /// 確認Viewの参照だけ配線する
    /// HierarchyとRectTransformは変更しない
    /// </summary>
    public static void WireConfirmViewReferences(ModelSaveConfirmView confirmView)
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
        if (rowRefs != null)
        {
            rowRefs.EnsureConfirmPrefabLayout();
            SerializedObject serializedRowRefs = new SerializedObject(rowRefs);
            serializedRowRefs.FindProperty("preservePrefabLayout").boolValue = true;
            serializedRowRefs.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rowRefs);
        }

        SerializedObject serializedConfirm = new SerializedObject(confirmView);
        serializedConfirm.FindProperty("contentRoot").objectReferenceValue = contentRoot;
        serializedConfirm.FindProperty("rowElementRefs").objectReferenceValue = rowRefs;
        serializedConfirm.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(confirmView);

        FreezeConfirmViewPrefabLayoutDriversAsset();
    }

    /// <summary>
    /// ModelSaveConfirmViewプレハブ上のLayoutGroupを無効化して手動配置を固定する
    /// </summary>
    public static void FreezeConfirmViewPrefabLayoutDriversAsset()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(ConfirmViewPrefabPath);
        if (prefabRoot == null)
        {
            return;
        }

        try
        {
            Transform contentRoot = prefabRoot.transform.Find("ConfirmContent");
            ModelSaveConfirmLayoutUtility.FreezeManualLayoutDrivers(contentRoot);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, ConfirmViewPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    /// <summary>
    /// セーブスロット行の攻撃列参照を配線する
    /// </summary>
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
                $"[ModelSaveSlotUiPrefabUtility] {rowRoot.name} の攻撃列が未整備です"
                    + " Hierarchyで手動調整してください",
                rowRoot);
        }
    }

    /// <inheritdoc cref="WireSaveSlotAttackSlots"/>
    public static void EnsureSaveSlotAttackSlots(Transform rowRoot)
    {
        WireSaveSlotAttackSlots(rowRoot);
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
        return image != null && image.gameObject.name == "SlotFrame";
    }

    private static Sprite ResolveSaveSlotSlotFrameReferenceSprite()
    {
        GameObject scrollPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ScrollListPrefabPath);
        if (scrollPrefab != null)
        {
            Image[] images = scrollPrefab.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];
                if (image.gameObject.name == "SlotFrame" && image.sprite != null)
                {
                    return image.sprite;
                }
            }
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(SlotFrameSpriteAssetPath);
    }
}
#endif
