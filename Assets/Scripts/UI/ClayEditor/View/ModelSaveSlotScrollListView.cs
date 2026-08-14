using Extensions;

using GameData;

using LighthouseExtends.UIComponent.Button;

using SaveData;

using SaveData.Interface;

using System;

using System.Collections.Generic;

using UnityEngine;

using UnityEngine.UI;



namespace UI.ClayEditor.View

{

    /// <summary>

    /// セーブスロット一覧を縦スクロール表示するView

    /// レイアウトはプレハブまたはシーン配置を使い実行時はデータ反映のみ行う

    /// </summary>

    public sealed class ModelSaveSlotScrollListView : MonoBehaviour, Localization.ILanguageAwareUi

    {

        private const string ScrollRootName = "SlotScrollList";



        [SerializeField] private ScrollRect scrollRect;

        [SerializeField] private RectTransform scrollContent;

        [SerializeField] private List<ModelSaveSlotRowElementRefs> rowElementRefs = new List<ModelSaveSlotRowElementRefs>();



        private readonly List<SlotRow> rows = new List<SlotRow>(ModelSavePoolSettings.PlayerSlotCount);



        private Action<int> onSlotSelected;

        private bool isBuilt;

        private bool isClickBound;

        private bool hasLastRefresh;
        private ModelSavePool lastRefreshPool;
        private IClayModelSaveService lastRefreshSaveService;
        private string lastRefreshEmptyLabel;
        private IList<UnityEngine.Object> lastRefreshThumbnails;
        private bool lastRefreshAllowEmpty;
        private ModelSaveSlotListContentMode lastRefreshContentMode;



        /// <summary>

        /// スロット選択キャンバスの表示順とスケールを整える

        /// </summary>

        public static void EnterFullscreenSelectionLayout(Canvas canvas, bool reparentToUiRoot = true)

        {

            if (canvas == null)

            {

                return;

            }



            FixCanvasScaleHierarchy(canvas);

            canvas.GetComponent<RectTransform>()?.SetAsLastSibling();

        }



        /// <summary>

        /// スロット選択キャンバス背面に不透明な全画面Imageがあることを確認する
        /// </summary>
        /// <param name="root">背景を付けるUIルート</param>
        public static void EnsureSelectionBackground(Transform root)
        {
            if (root == null)
            {
                return;
            }

            Transform backgroundTransform = root.Find("BackGround");

            if (backgroundTransform == null)

            {

                var backgroundObject = new GameObject("BackGround", typeof(RectTransform), typeof(Image));

                backgroundObject.transform.SetParent(root, false);

                backgroundObject.transform.SetAsFirstSibling();

                RectTransform rect = backgroundObject.GetComponent<RectTransform>();

                rect.anchorMin = Vector2.zero;

                rect.anchorMax = Vector2.one;

                rect.offsetMin = Vector2.zero;

                rect.offsetMax = Vector2.zero;

                backgroundTransform = backgroundObject.transform;

            }



            Image backgroundImage = backgroundTransform.GetComponent<Image>();

            if (backgroundImage == null)

            {

                backgroundImage = backgroundTransform.gameObject.AddComponent<Image>();

            }



            backgroundImage.raycastTarget = true;
            if (backgroundImage.color.a < 0.01f)
            {
                Color color = backgroundImage.color;
                color.a = 1f;
                backgroundImage.color = color;
            }
        }

        /// <summary>
        /// スロット選択キャンバス背面に不透明な全画面Imageがあることを確認する
        /// </summary>
        /// <param name="canvas">背景を付けるCanvas</param>
        public static void EnsureSelectionBackground(Canvas canvas)
        {
            EnsureSelectionBackground(canvas != null ? canvas.transform : null);
        }

        /// <summary>
        /// スロット選択用に変更したキャンバス配置を元に戻す
        /// </summary>

        public static void ExitFullscreenSelectionLayout()

        {

        }



        /// <summary>
        /// スロット一覧の参照配線を確実にする
        /// レイアウトはプレハブ配置を変更しない
        /// </summary>
        public void RefreshHostLayout()
        {
            EnsureBuilt();
            FixZeroScaleAncestors(transform as RectTransform);
            Canvas.ForceUpdateCanvases();
        }

        /// <summary>
        /// シーン参照の再解決が必要か
        /// </summary>
        public bool NeedsReferenceResolve()
        {
            return scrollRect == null || scrollContent == null || rows.Count == 0;
        }

        /// <summary>
        /// スロット行クリックを再配線する
        /// </summary>
        public void EnsureClickBinding()
        {
            EnsureBuilt();

            if (onSlotSelected == null || !isBuilt || isClickBound)
            {
                return;
            }

            BindRowClicks();
            isClickBound = true;
        }

        /// <summary>
        /// 選択可能なスロット行数を返す
        /// </summary>
        public int CountInteractableRows()
        {
            int count = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Button != null && rows[i].Button.interactable)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// 選択UI表示時にスクロール位置だけ更新する
        /// </summary>
        public void ForceSelectionLayout()
        {
            EnsureBuilt();
            FixZeroScaleAncestors(transform as RectTransform);

            if (scrollRect != null)
            {
                scrollRect.verticalNormalizedPosition = 1f;
            }
        }



        /// <summary>

        /// スロット選択キャンバスと背景パネルを前面へ出す

        /// </summary>

        public static void ApplySelectionCanvasLayout(Canvas canvas)

        {

            EnterFullscreenSelectionLayout(canvas);

            EnsureSelectionBackground(canvas);

        }



        /// <summary>

        /// スロット行が選択されたときに呼ばれるコールバックを登録する

        /// </summary>

        public void Initialize(Action<int> slotSelectedHandler)

        {

            onSlotSelected = slotSelectedHandler;

            EnsureBuilt();

            if (!isClickBound && onSlotSelected != null && isBuilt)

            {

                BindRowClicks();

                isClickBound = true;

            }

        }



        /// <summary>
        /// 複数選択の選択マークを各行へ反映する
        /// </summary>
        /// <param name="selectedSlotIndices">選択中スロット番号</param>
        public void SetSelectionMarks(ICollection<int> selectedSlotIndices)
        {
            EnsureBuilt();
            if (!isBuilt)
            {
                return;
            }

            for (int i = 0; i < rowElementRefs.Count; i++)
            {
                ModelSaveSlotRowElementRefs refs = rowElementRefs[i];
                if (refs == null)
                {
                    continue;
                }

                bool marked = selectedSlotIndices != null && selectedSlotIndices.Contains(i);
                refs.SetSelectionMarked(marked);
            }
        }

        /// <summary>
        /// 各スロットの表示を最新のセーブ内容に更新する
        /// </summary>
        public void RefreshSlots(
            ModelSavePool pool,
            IClayModelSaveService saveService,
            string emptySlotLabel,
            IList<UnityEngine.Object> runtimeThumbnailObjects,
            bool allowEmptySlotSelection)
        {
            RefreshSlots(
                pool,
                saveService,
                emptySlotLabel,
                runtimeThumbnailObjects,
                allowEmptySlotSelection,
                TrainedSaveSlotListPresentation.ResolveContentMode(pool));
        }

        /// <summary>
        /// 各スロットの表示を最新のセーブ内容に更新する
        /// </summary>
        /// <param name="pool">セーブプール</param>
        /// <param name="saveService">セーブサービス</param>
        /// <param name="emptySlotLabel">空スロット文言</param>
        /// <param name="runtimeThumbnailObjects">実行時サムネイル破棄用</param>
        /// <param name="allowEmptySlotSelection">空スロット選択を許可するか</param>
        /// <param name="contentMode">行の表示内容</param>
        public void RefreshSlots(
            ModelSavePool pool,
            IClayModelSaveService saveService,
            string emptySlotLabel,
            IList<UnityEngine.Object> runtimeThumbnailObjects,
            bool allowEmptySlotSelection,
            ModelSaveSlotListContentMode contentMode)
        {
            hasLastRefresh = true;
            lastRefreshPool = pool;
            lastRefreshSaveService = saveService;
            lastRefreshEmptyLabel = emptySlotLabel;
            lastRefreshThumbnails = runtimeThumbnailObjects;
            lastRefreshAllowEmpty = allowEmptySlotSelection;
            lastRefreshContentMode = contentMode;
            EnsureBuilt();
            if (!isBuilt || saveService == null)
            {
                return;
            }

            int slotCount = ModelSavePoolSettings.GetSlotCount(pool);
            if (rows.Count < slotCount)
            {
                Debug.LogError(
                    "[ModelSaveSlotScrollListView] スロット行が不足しています"
                    + $" need={slotCount} found={rows.Count}"
                    + " ModelSaveSlotScrollListプレハブの行数を増やしてください",
                    this);
            }

            for (int i = 0; i < rows.Count; i++)
            {
                bool visible = i < slotCount;
                if (rows[i].RowWrapper != null)
                {
                    rows[i].RowWrapper.gameObject.SetActive(visible);
                }

                if (!visible)
                {
                    continue;
                }

                ModelSaveSlot slot = saveService.GetSlot(pool, i);
                bool used = IsLoadableSlot(slot);
                ApplyRowContent(rows[i], slot, used, i, emptySlotLabel, contentMode, pool);
                ApplyThumbnail(rows[i].Elements, used, pool, i, saveService, runtimeThumbnailObjects);
                rows[i].Button.interactable = used || allowEmptySlotSelection;
            }
        }

        
        /// <inheritdoc/>
        public void RefreshLocalizedUi()
        {
            if (!hasLastRefresh || lastRefreshSaveService == null)
            {
                return;
            }

            RefreshSlots(
                lastRefreshPool,
                lastRefreshSaveService,
                lastRefreshEmptyLabel,
                lastRefreshThumbnails,
                lastRefreshAllowEmpty,
                lastRefreshContentMode);
        }

        private static bool IsLoadableSlot(ModelSaveSlot slot)
        {
            return slot != null
                && slot.isUsed
                && !string.IsNullOrEmpty(slot.glbFileName);
        }

        private void Awake()
        {
            DisableLegacyGridLayout();
            HideLegacySlotButtonChildren();
        }

        private static void ApplyRowContent(
            SlotRow row,
            ModelSaveSlot slot,
            bool used,
            int slotIndex,
            string emptySlotLabel,
            ModelSaveSlotListContentMode contentMode,
            ModelSavePool pool)
        {
            ModelSaveSlotRowUiBuilder.RowElements elements = row.Elements;
            if (!used)
            {
                ModelSaveSlotRowUiBuilder.BindScrollListEmpty(elements, emptySlotLabel, slotIndex);
            }
            else
            {
                ModelSaveSlotRowUiBuilder.BindScrollListFromSlot(
                    elements,
                    slot,
                    slotIndex,
                    contentMode,
                    pool);
            }
        }

        private void EnsureBuilt()
        {
            if (isBuilt && rows.Count > 0)

            {

                return;

            }



            DisposeAllRowClickSubscriptions();
            rows.Clear();

            isClickBound = false;

            DisableLegacyGridLayout();

            ResolveSerializedReferencesFromHierarchy();

            BindSceneLayout();



            if (rows.Count > 0)

            {

                HideLegacySlotButtonChildren();

            }



            isBuilt = rows.Count > 0;

            if (!isBuilt)

            {

                Debug.LogError(

                    "[ModelSaveSlotScrollListView] スロット行が未配置です。ModelSaveSlotScrollListプレハブを配置してください",

                    this);

            }



            if (onSlotSelected != null && isBuilt)

            {

                BindRowClicks();

                isClickBound = true;

            }

        }



        private void BindSceneLayout()

        {

            if (scrollRect == null)

            {

                scrollRect = ResolveScrollRect();

            }



            if (scrollContent == null && scrollRect != null)

            {

                scrollContent = scrollRect.content;

            }



            if (scrollContent == null)

            {

                Transform content = FindScrollRoot()?.Find("Viewport/Content");

                scrollContent = content as RectTransform;

            }



            if (rowElementRefs == null || rowElementRefs.Count == 0)

            {

                rowElementRefs = new List<ModelSaveSlotRowElementRefs>(

                    GetComponentsInChildren<ModelSaveSlotRowElementRefs>(true));

            }



            for (int i = 0; i < rowElementRefs.Count; i++)

            {

                ModelSaveSlotRowElementRefs refs = rowElementRefs[i];

                if (refs == null)

                {

                    continue;

                }



                LHButton button = refs.GetComponent<LHButton>();

                if (button == null)

                {

                    button = refs.GetComponentInParent<LHButton>();

                }



                if (button == null)

                {

                    continue;

                }



                RectTransform rowWrapper = refs.transform.parent as RectTransform;

                rows.Add(new SlotRow(button, refs.ToRowElements())

                {

                    RowWrapper = rowWrapper

                });

            }

        }



        private void DisableLegacyGridLayout()

        {

            GridLayoutGroup gridLayout = GetComponent<GridLayoutGroup>();

            if (gridLayout != null)

            {

                gridLayout.enabled = false;

            }

        }



        /// <summary>

        /// スロット選択キャンバスとその親のscale=0を補正する

        /// </summary>

        public static void FixCanvasScaleHierarchy(Canvas canvas)

        {

            if (canvas == null)

            {

                return;

            }



            RectTransform canvasRect = canvas.GetComponent<RectTransform>();

            if (canvasRect == null)

            {

                return;

            }



            FixZeroScaleAncestors(canvasRect);

            if (canvasRect.localScale.sqrMagnitude < 0.001f)

            {

                canvasRect.localScale = Vector3.one;

            }

        }



        private static void FixZeroScaleAncestors(RectTransform canvasRect)

        {

            Transform current = canvasRect;

            while (current != null)

            {

                if (current is RectTransform rect && rect.localScale.sqrMagnitude < 0.001f)

                {

                    rect.localScale = Vector3.one;

                }



                current = current.parent;

            }

        }



        /// <summary>
        /// 指定RectTransformを親いっぱいに広げる
        /// </summary>
        public static void EnsureRectFullscreen(RectTransform rect)
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
            rect.localScale = Vector3.one;
        }

        private ScrollRect ResolveScrollRect()
        {
            if (scrollRect != null)
            {
                return scrollRect;
            }

            Transform scrollRoot = FindScrollRoot();
            return scrollRoot != null ? scrollRoot.GetComponent<ScrollRect>() : null;
        }

        private Transform FindScrollRoot()
        {
            Transform direct = transform.Find(ScrollRootName);
            if (direct != null)
            {
                return direct;
            }

            ScrollRect nestedScrollRect = GetComponentInChildren<ScrollRect>(true);
            return nestedScrollRect != null ? nestedScrollRect.transform : null;
        }

        /// <summary>
        /// レイアウト診断用のメトリクスを返す
        /// </summary>
        public void GetLayoutMetrics(
            out int builtRows,
            out float hostWidth,
            out float hostHeight,
            out float viewportHeight,
            out float contentHeight)
        {
            builtRows = rows.Count;
            RectTransform host = transform as RectTransform;
            hostWidth = host != null ? host.rect.width : 0f;
            hostHeight = host != null ? host.rect.height : 0f;
            RectTransform viewport = scrollRect != null ? scrollRect.viewport : null;
            viewportHeight = viewport != null ? viewport.rect.height : 0f;
            contentHeight = scrollContent != null ? scrollContent.rect.height : 0f;
        }

        public void HideLegacySlotButtonChildren()

        {

            DisableLegacyGridLayout();



            for (int i = 0; i < transform.childCount; i++)

            {

                Transform child = transform.GetChild(i);

                if (child.name == ScrollRootName)

                {

                    continue;

                }



                child.gameObject.SetActive(false);

            }

        }



        /// <summary>
        /// 旧式SlotButtons構成かどうかを返す
        /// </summary>
        /// <returns>Resourcesプレハブへ差し替えが必要ならtrue</returns>
        public bool NeedsResourcePrefabHostSync()
        {
            if (GetComponent<GridLayoutGroup>() != null)
            {
                return true;
            }

            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child.name != ScrollRootName)
                {
                    return true;
                }
            }

            Transform scrollRoot = FindScrollRoot();
            if (scrollRoot == null)
            {
                return true;
            }

            Transform content = scrollRoot.Find("Viewport/Content");
            return content == null || content.childCount < ModelSavePoolSettings.PlayerSlotCount;
        }

        /// <summary>
        /// SerializeField参照をHierarchyから再解決する
        /// </summary>
        public void ResolveSerializedReferencesFromHierarchy()
        {
            scrollRect = ResolveScrollRect();
            if (scrollRect != null)
            {
                scrollContent = scrollRect.content;
            }

            if (scrollContent == null)
            {
                Transform content = FindScrollRoot()?.Find("Viewport/Content");
                scrollContent = content as RectTransform;
            }

            rowElementRefs = new List<ModelSaveSlotRowElementRefs>(
                GetComponentsInChildren<ModelSaveSlotRowElementRefs>(true));
            DisposeAllRowClickSubscriptions();
            rows.Clear();
            isBuilt = false;
            isClickBound = false;
        }



        private void DisposeAllRowClickSubscriptions()
        {
            for (int i = 0; i < rows.Count; i++)
            {
                rows[i]?.DisposeClickSubscription();
            }
        }

        private void BindRowClicks()

        {

            if (!isBuilt)

            {

                return;

            }



            for (int i = 0; i < rows.Count; i++)

            {

                int slotIndex = i;

                SlotRow row = rows[i];

                if (row.Button == null)

                {

                    continue;

                }



                row.DisposeClickSubscription();

                row.ClickSubscription = row.Button.SubscribeOnClick(() => onSlotSelected?.Invoke(slotIndex));

            }

        }



        private static void ApplyThumbnail(

            ModelSaveSlotRowUiBuilder.RowElements elements,

            bool used,

            ModelSavePool pool,

            int slotIndex,

            IClayModelSaveService saveService,

            IList<UnityEngine.Object> runtimeThumbnailObjects)

        {

            if (elements?.ThumbnailImage == null)

            {

                return;

            }



            if (!used)

            {

                ModelSaveSlotRowUiBuilder.ApplyThumbnailSprite(elements, null);

                return;

            }



            Texture2D texture = saveService.LoadThumbnail(pool, slotIndex);

            if (texture == null)

            {

                ModelSaveSlotRowUiBuilder.ApplyThumbnailSprite(elements, null);

                return;

            }



            Sprite sprite = Sprite.Create(

                texture,

                new Rect(0f, 0f, texture.width, texture.height),

                new Vector2(0.5f, 0.5f));



            runtimeThumbnailObjects.Add(texture);

            runtimeThumbnailObjects.Add(sprite);

            ModelSaveSlotRowUiBuilder.ApplyThumbnailSprite(elements, sprite);

        }



        private sealed class SlotRow

        {

            public SlotRow(LHButton button, ModelSaveSlotRowUiBuilder.RowElements elements)

            {

                Button = button;

                Elements = elements;

            }



            public LHButton Button { get; }



            public ModelSaveSlotRowUiBuilder.RowElements Elements { get; }



            public RectTransform RowWrapper { get; set; }

            public System.IDisposable ClickSubscription { get; set; }

            public void DisposeClickSubscription()
            {
                ClickSubscription?.Dispose();
                ClickSubscription = null;
            }

        }

    }

}


