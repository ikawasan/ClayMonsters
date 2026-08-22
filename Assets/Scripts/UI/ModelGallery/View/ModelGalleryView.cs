using Localization;
using Cysharp.Threading.Tasks;
using Extensions;
using LighthouseExtends.UIComponent.Button;
using SaveData;
using SaveData.Interface;
using System;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UI.ClayEditor.View;
using UI.ModelGallery.Data;
using UI.ModelGallery.Interface;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace UI.ModelGallery.View
{
    /// <summary>
    /// 展示室本体UI
    /// Canvas.enabledで表示切替する
    /// </summary>
    public sealed class ModelGalleryView : MonoBehaviour, IModelGalleryView, ILanguageAwareUi
    {
        [SerializeField] private Canvas canvas;
        [SerializeField] private Canvas postTabCanvas;
        [SerializeField] private Canvas browseTabCanvas;
        [SerializeField] private Canvas slotListHostCanvas;
        [SerializeField] private Canvas pointsInsufficientCanvas;
        [SerializeField] private Canvas downloadSlotSelectCanvas;
        [SerializeField] private Canvas postConfirmCanvas;
        [SerializeField] private LHButton closeButton;
        [SerializeField] private Toggle postTabToggle;
        [SerializeField] private Toggle browseTabToggle;
        [SerializeField] private ToggleGroup tabToggleGroup;
        [SerializeField] private LHButton postConfirmPublishButton;
        [SerializeField] private LHButton postConfirmCloseButton;
        [SerializeField] private Canvas postConfirmPublishButtonCanvas;
        [SerializeField] private Canvas postConfirmCloseButtonCanvas;
        [SerializeField] private LHButton postConfirmResultCloseButton;
        [SerializeField] private Canvas postConfirmResultCloseButtonCanvas;
        [SerializeField] private Toggle randomSortToggle;
        [SerializeField] private Toggle monthlyRankingToggle;
        [SerializeField] private Toggle overallRankingToggle;
        [SerializeField] private Toggle latestSortToggle;
        [SerializeField] private ToggleGroup browseSortToggleGroup;
        [SerializeField] private LHButton browseRefreshButton;
        [SerializeField] private LHButton pointsInsufficientCloseButton;
        [SerializeField] private LHButton downloadSlotSelectCloseButton;
        [SerializeField] private Canvas browseRefreshButtonCanvas;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text pointsText;
        [SerializeField] private TMP_Text pointsInsufficientMessageText;
        [SerializeField] private TMP_Text pointsInsufficientHeldPointsText;
        [SerializeField] private TMP_Text pointsInsufficientCostPointsText;
        [SerializeField] private TMP_Text postConfirmNameText;
        [SerializeField] private TMP_Text postConfirmMessageText;
        [SerializeField] private Image postConfirmThumbnailImage;
        [SerializeField] private Canvas downloadConfirmCanvas;
        [SerializeField] private LHButton downloadConfirmSaveButton;
        [SerializeField] private LHButton downloadConfirmCloseButton;
        [SerializeField] private Canvas downloadConfirmSaveButtonCanvas;
        [SerializeField] private Canvas downloadConfirmCloseButtonCanvas;
        [SerializeField] private LHButton downloadConfirmResultCloseButton;
        [SerializeField] private Canvas downloadConfirmResultCloseButtonCanvas;
        [SerializeField] private TMP_Text downloadConfirmNameText;
        [SerializeField] private TMP_Text downloadConfirmMessageText;
        [SerializeField] private TMP_Text downloadConfirmCostText;
        [SerializeField] private Canvas downloadConfirmCostTextCanvas;
        [SerializeField] private Image downloadConfirmThumbnailImage;
        [SerializeField] private ModelSaveSlotScrollListView postSlotScrollList;
        [SerializeField] private ScrollRect browseScrollRect;
        [SerializeField] private RectTransform browseViewport;
        [SerializeField] private ModelGalleryItemCellView[] browseItemCells;

        private enum SlotListMode
        {
            Post,
            Download
        }

        private static readonly Color LabelActiveColor = new Color(0.36f, 0.24f, 0.18f, 1f);
        private static readonly Color LabelInactiveColor = new Color(0.55f, 0.5f, 0.45f, 0.7f);
        private const string ToggleOffSpritePath = "Image/GameUi/GalleryToggle_Off";
        // Onは押下用で暗いため選択中は明るいHighlightedを使う
        private const string ToggleOnSpritePath = "Image/GameUi/GalleryToggle_Highlighted";
        private const string TogglePressedSpritePath = "Image/GameUi/GalleryToggle_On";

        private readonly List<UnityEngine.Object> postSlotRuntimeThumbnails = new();
        private readonly Vector3[] worldCorners = new Vector3[4];
        private bool[] browseCellHasContent;
        private UnityAction<int> postSlotSelectedAction;
        private UnityAction<int> downloadSlotSelectedAction;
        private UnityAction<int> browseItemSelectedAction;
        private UnityAction<int> browseFavoriteAction;
        private SlotListMode slotListMode = SlotListMode.Post;
        private Sprite toggleOffSprite;
        private Sprite toggleOnSprite;
        private Sprite togglePressedSprite;
        private bool isClickBound;
        private bool isPostSlotScrollInitialized;
        private bool isBrowseScrollBound;
        private Texture2D ownedPostConfirmThumbnail;
        private Sprite ownedPostConfirmSprite;
        private Texture2D ownedDownloadConfirmThumbnail;
        private Sprite ownedDownloadConfirmSprite;
        private CancellationTokenSource browseClipCts;
        private IClayModelSaveService cachedSaveService;
        private LocalizedBakedTextApplier bakedLabelApplier;
        private int cachedPoints;
        private bool postConfirmVisible;
        private string cachedPostConfirmName = string.Empty;
        private string postConfirmResultKey = string.Empty;
        private string postConfirmResultFallback = string.Empty;
        private PostConfirmDisplayMode postConfirmDisplayMode = PostConfirmDisplayMode.Confirm;
        private bool downloadConfirmVisible;
        private string cachedDownloadConfirmName = string.Empty;
        private bool downloadConfirmIsOverwrite;
        private string downloadConfirmResultKey = string.Empty;
        private string downloadConfirmResultFallback = string.Empty;
        private DownloadConfirmDisplayMode downloadConfirmDisplayMode = DownloadConfirmDisplayMode.Confirm;

        private void Awake()
        {
            ValidateReferences();
            CacheToggleSprites();
            ConfigureTabAndSortTogglesNoSelected();
            BindCellClicks();
            BindBrowseScroll();
            // 翻訳適用前に日本語原文を採取する
            EnsureBakedLabels();
            CaptureChromeOriginalsIfNeeded();
            Hide();
        }

        /// <inheritdoc/>
        public void RefreshLocalizedUi()
        {
            ApplyChromeLabels();
            SetPoints(cachedPoints);
            if (postConfirmVisible)
            {
                if (postConfirmNameText != null)
                {
                    LocalizedFont.SetText(postConfirmNameText, cachedPostConfirmName);
                }

                ApplyPostConfirmMessageText();
                ApplyPostConfirmButtonLabels();
            }

            if (downloadConfirmVisible)
            {
                if (downloadConfirmNameText != null)
                {
                    LocalizedFont.SetText(downloadConfirmNameText, cachedDownloadConfirmName);
                }

                ApplyDownloadConfirmMessageText();
                ApplyDownloadConfirmCostText();
                ApplyDownloadConfirmButtonLabels();
            }

            // 投稿/取得スロットと確認文を現在言語で再組み立て
            if (cachedSaveService != null)
            {
                RefreshSharedSlotList(cachedSaveService, slotListMode);
            }

            // 未バインドでもプレハブ焼き込みの範囲/技名を再適用
            RefreshAttackSlotsUnderThis();
            // 言語差替後もタブ/ソートの選択Boldを維持する
            RestyleAllToggles();
        }

        private void RestyleAllToggles()
        {
            ApplyToggleActiveVisual(postTabToggle);
            ApplyToggleActiveVisual(browseTabToggle);
            ApplyToggleActiveVisual(randomSortToggle);
            ApplyToggleActiveVisual(monthlyRankingToggle);
            ApplyToggleActiveVisual(overallRankingToggle);
            ApplyToggleActiveVisual(latestSortToggle);
        }

        private void RefreshAttackSlotsUnderThis()
        {
            TrainingAttackSlotView[] attackSlots = GetComponentsInChildren<TrainingAttackSlotView>(true);
            for (int i = 0; i < attackSlots.Length; i++)
            {
                TrainingAttackSlotView slot = attackSlots[i];
                if (slot == null)
                {
                    continue;
                }

                slot.RefreshLocalizedUi();
            }
        }

        private void EnsureBakedLabels()
        {
            if (bakedLabelApplier != null)
            {
                return;
            }

            bakedLabelApplier = new LocalizedBakedTextApplier();
            bakedLabelApplier.Register(GameTextKeys.TitleModelGallery, "展示室");
            bakedLabelApplier.Register(GameTextKeys.ModelGalleryTabPost, "投稿");
            bakedLabelApplier.Register(GameTextKeys.ModelGalleryTabBrowse, "閲覧");
            bakedLabelApplier.Register(GameTextKeys.ModelGallerySortRandom, "ランダム");
            bakedLabelApplier.Register(GameTextKeys.ModelGallerySortMonthly, "月間");
            bakedLabelApplier.Register(GameTextKeys.ModelGallerySortMonthly, "月間ランキング");
            bakedLabelApplier.Register(GameTextKeys.ModelGallerySortOverall, "総合");
            bakedLabelApplier.Register(GameTextKeys.ModelGallerySortOverall, "総合ランキング");
            bakedLabelApplier.Register(GameTextKeys.ModelGallerySortLatest, "最新");
            bakedLabelApplier.Register(GameTextKeys.ModelGalleryPrev, "前へ");
            bakedLabelApplier.Register(GameTextKeys.ModelGalleryNext, "次へ");
            bakedLabelApplier.Register(GameTextKeys.ModelGalleryRefresh, "更新");
            bakedLabelApplier.Register(GameTextKeys.ModelGalleryPublish, "投稿");
            bakedLabelApplier.Register(GameTextKeys.ModelGalleryPublish, "投稿する");
            bakedLabelApplier.Register(GameTextKeys.CommonSave, "保存");
            bakedLabelApplier.Register(GameTextKeys.CommonSave, "保存する");
            bakedLabelApplier.Register(GameTextKeys.CommonClose, "閉じる");
            bakedLabelApplier.Register(GameTextKeys.ModelGalleryDownload, "取得");
            bakedLabelApplier.Register(GameTextKeys.ModelGalleryDownload, "ダウンロード");
            bakedLabelApplier.Register(GameTextKeys.ModelGalleryModelName, "モデル名");
            bakedLabelApplier.Register(GameTextKeys.ModelGalleryPointsInsufficient, "ポイントが不足しています");
            bakedLabelApplier.Register(GameTextKeys.ModelGallerySaveDestEmpty, "保存先: 空きスロット");
            bakedLabelApplier.Register(GameTextKeys.ModelGallerySelectSaveSlot, "保存先スロットを選択");
            bakedLabelApplier.Register(GameTextKeys.ModelGalleryPostConfirm, "投稿しますか？");
            bakedLabelApplier.Register(GameTextKeys.ModelGalleryPublishing, "投稿中…");
            bakedLabelApplier.Register(GameTextKeys.ModelGalleryPublishSuccess, "投稿が完了しました");
            bakedLabelApplier.Register(
                GameTextKeys.ModelGalleryOverwriteConfirm,
                "既存のセーブデータに上書き保存しますか？");
            bakedLabelApplier.Register(
                GameTextKeys.ModelGallerySaveToSlotConfirm,
                "このスロットに保存しますか？");
            bakedLabelApplier.Register(GameTextKeys.ModelGallerySaving, "保存中…");
            bakedLabelApplier.Register(GameTextKeys.ModelGallerySaveSuccess, "保存が完了しました");
            bakedLabelApplier.Capture(transform);
        }

        private void ApplyChromeLabels()
        {
            // 原文採取は翻訳より先に行う(翻訳後採取だと日本語時フォールバックが崩れる)
            EnsureBakedLabels();
            CaptureChromeOriginalsIfNeeded();
            bakedLabelApplier?.Apply();

            if (titleText != null)
            {
                LocalizedFont.SetText(
                    titleText,
                    SceneLocalizedLabel.Resolve(GameTextKeys.TitleModelGallery, titleOriginal));
            }

            LhButtonLabelUtility.SetLabel(
                closeButton,
                SceneLocalizedLabel.Resolve(GameTextKeys.CommonClose, closeOriginal));
            LhButtonLabelUtility.SetLabel(
                postConfirmPublishButton,
                SceneLocalizedLabel.Resolve(GameTextKeys.ModelGalleryPublish, publishOriginal));
            LhButtonLabelUtility.SetLabel(
                postConfirmCloseButton,
                SceneLocalizedLabel.Resolve(GameTextKeys.CommonClose, postConfirmCloseOriginal));
            LhButtonLabelUtility.SetLabel(
                browseRefreshButton,
                SceneLocalizedLabel.Resolve(GameTextKeys.ModelGalleryRefresh, refreshOriginal));
            LhButtonLabelUtility.SetLabel(
                pointsInsufficientCloseButton,
                SceneLocalizedLabel.Resolve(GameTextKeys.CommonClose, pointsCloseOriginal));
            LhButtonLabelUtility.SetLabel(
                downloadSlotSelectCloseButton,
                SceneLocalizedLabel.Resolve(GameTextKeys.CommonClose, downloadSelectCloseOriginal));
            LhButtonLabelUtility.SetLabel(
                downloadConfirmSaveButton,
                SceneLocalizedLabel.Resolve(GameTextKeys.CommonSave, downloadSaveOriginal));
            LhButtonLabelUtility.SetLabel(
                downloadConfirmCloseButton,
                SceneLocalizedLabel.Resolve(GameTextKeys.CommonClose, downloadConfirmCloseOriginal));
            LhButtonLabelUtility.SetLabel(
                postTabToggle,
                SceneLocalizedLabel.Resolve(GameTextKeys.ModelGalleryTabPost, postTabOriginal));
            LhButtonLabelUtility.SetLabel(
                browseTabToggle,
                SceneLocalizedLabel.Resolve(GameTextKeys.ModelGalleryTabBrowse, browseTabOriginal));
            LhButtonLabelUtility.SetLabel(
                randomSortToggle,
                SceneLocalizedLabel.Resolve(GameTextKeys.ModelGallerySortRandom, randomSortOriginal));
            LhButtonLabelUtility.SetLabel(
                monthlyRankingToggle,
                SceneLocalizedLabel.Resolve(GameTextKeys.ModelGallerySortMonthly, monthlySortOriginal));
            LhButtonLabelUtility.SetLabel(
                overallRankingToggle,
                SceneLocalizedLabel.Resolve(GameTextKeys.ModelGallerySortOverall, overallSortOriginal));
            LhButtonLabelUtility.SetLabel(
                latestSortToggle,
                SceneLocalizedLabel.Resolve(GameTextKeys.ModelGallerySortLatest, latestSortOriginal));
        }

        private bool chromeOriginalsCaptured;
        private string titleOriginal = "展示室";
        private string closeOriginal = "閉じる";
        private string publishOriginal = "投稿";
        private string postConfirmCloseOriginal = "閉じる";
        private string refreshOriginal = "更新";
        private string pointsCloseOriginal = "閉じる";
        private string downloadSelectCloseOriginal = "閉じる";
        private string downloadSaveOriginal = "保存";
        private string downloadConfirmCloseOriginal = "閉じる";
        private string postTabOriginal = "投稿";
        private string browseTabOriginal = "閲覧";
        private string randomSortOriginal = "ランダム";
        private string monthlySortOriginal = "月間ランキング";
        private string overallSortOriginal = "総合ランキング";
        private string latestSortOriginal = "最新";

        private void CaptureChromeOriginalsIfNeeded()
        {
            if (chromeOriginalsCaptured)
            {
                return;
            }

            // 既に他言語へ差し替わっている場合は配置文言を原文扱いしない
            titleOriginal = CaptureJapaneseOriginal(titleText, titleOriginal);
            closeOriginal = CaptureJapaneseOriginal(closeButton, closeOriginal);
            publishOriginal = CaptureJapaneseOriginal(postConfirmPublishButton, publishOriginal);
            postConfirmCloseOriginal = CaptureJapaneseOriginal(
                postConfirmCloseButton,
                postConfirmCloseOriginal);
            refreshOriginal = CaptureJapaneseOriginal(browseRefreshButton, refreshOriginal);
            pointsCloseOriginal = CaptureJapaneseOriginal(
                pointsInsufficientCloseButton,
                pointsCloseOriginal);
            downloadSelectCloseOriginal = CaptureJapaneseOriginal(
                downloadSlotSelectCloseButton,
                downloadSelectCloseOriginal);
            downloadSaveOriginal = CaptureJapaneseOriginal(
                downloadConfirmSaveButton,
                downloadSaveOriginal);
            downloadConfirmCloseOriginal = CaptureJapaneseOriginal(
                downloadConfirmCloseButton,
                downloadConfirmCloseOriginal);
            postTabOriginal = CaptureJapaneseOriginal(postTabToggle, postTabOriginal);
            browseTabOriginal = CaptureJapaneseOriginal(browseTabToggle, browseTabOriginal);
            randomSortOriginal = CaptureJapaneseOriginal(randomSortToggle, randomSortOriginal);
            monthlySortOriginal = CaptureJapaneseOriginal(monthlyRankingToggle, monthlySortOriginal);
            overallSortOriginal = CaptureJapaneseOriginal(overallRankingToggle, overallSortOriginal);
            latestSortOriginal = CaptureJapaneseOriginal(latestSortToggle, latestSortOriginal);
            chromeOriginalsCaptured = true;
        }

        // 日本語原文フォールバックを守るため漢字かなを含む配置文だけを原文として採用する
        private static string CaptureJapaneseOriginal(TMP_Text text, string fallback)
        {
            string captured = SceneLocalizedLabel.Capture(text, fallback);
            return PreferJapaneseOriginal(captured, fallback);
        }

        private static string CaptureJapaneseOriginal(Component component, string fallback)
        {
            string captured = SceneLocalizedLabel.Capture(component, fallback);
            return PreferJapaneseOriginal(captured, fallback);
        }

        private static string CaptureJapaneseOriginal(Toggle toggle, string fallback)
        {
            string captured = SceneLocalizedLabel.Capture(toggle, fallback);
            return PreferJapaneseOriginal(captured, fallback);
        }

        private static string PreferJapaneseOriginal(string captured, string fallback)
        {
            if (string.IsNullOrEmpty(captured))
            {
                return fallback ?? string.Empty;
            }

            if (ContainsJapaneseScript(captured))
            {
                return captured;
            }

            return string.IsNullOrEmpty(fallback) ? captured : fallback;
        }

        private static bool ContainsJapaneseScript(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c >= 0x3040 && c <= 0x30FF)
                {
                    return true;
                }

                if (c >= 0x3400 && c <= 0x9FFF)
                {
                    return true;
                }

                if (c >= 0xFF66 && c <= 0xFF9D)
                {
                    return true;
                }
            }

            return false;
        }

        /// <inheritdoc />
        public int BrowseItemCellCount => browseItemCells?.Length ?? 0;

        /// <inheritdoc />
        public void Show()
        {
            if (canvas == null)
            {
                return;
            }

            ApplyChromeLabels();
            canvas.enabled = true;
            RefreshAttackSlotsUnderThis();
        }

        /// <inheritdoc />
        public void Hide()
        {
            if (canvas == null)
            {
                return;
            }

            canvas.enabled = false;
            SetCanvasEnabled(postTabCanvas, false);
            SetCanvasEnabled(browseTabCanvas, false);
            SetSlotListHostVisible(false);
            HidePointsInsufficient();
            HideDownloadSlotSelect();
            HidePostConfirm();
            HideDownloadConfirm();
            SetBrowseRefreshButtonVisible(false);
            DisableAllCellCanvases();
            ClearPostSlotRuntimeThumbnails();
        }

        /// <inheritdoc />
        public void ShowPostTab()
        {
            slotListMode = SlotListMode.Post;
            SetCanvasEnabled(postTabCanvas, true);
            SetCanvasEnabled(browseTabCanvas, false);
            SetSlotListHostVisible(true);
            SetTabToggle(isPostTab: true);
            SetBrowseRefreshButtonVisible(false);
            HidePointsInsufficient();
            HideDownloadConfirm();
            HideDownloadSlotSelect();
            if (postSlotScrollList != null)
            {
                postSlotScrollList.RefreshHostLayout();
            }
        }

        /// <inheritdoc />
        public void ShowBrowseTab()
        {
            slotListMode = SlotListMode.Post;
            SetCanvasEnabled(postTabCanvas, false);
            SetSlotListHostVisible(false);
            SetCanvasEnabled(browseTabCanvas, true);
            SetTabToggle(isPostTab: false);
            HidePointsInsufficient();
            HideDownloadConfirm();
            HideDownloadSlotSelect();
            ApplyBrowseViewportClip();
        }

        /// <inheritdoc />
        public void SetPoints(int points)
        {
            cachedPoints = Mathf.Max(0, points);
            if (pointsText != null)
            {
                LocalizedFont.SetText(
                    pointsText,
                    LocalizedText.GetOrFallback(
                        GameTextKeys.ModelGalleryPoints,
                        "{points} ポイント",
                        "points",
                        cachedPoints));
            }

            ApplyPointsInsufficientDynamicTexts();
        }

        /// <inheritdoc />
        public void SetBrowseSortMode(ModelGalleryBrowseSortMode sortMode)
        {
            SetToggleIsOnWithoutNotify(randomSortToggle, sortMode == ModelGalleryBrowseSortMode.Random);
            SetToggleIsOnWithoutNotify(
                monthlyRankingToggle,
                sortMode == ModelGalleryBrowseSortMode.MonthlyRanking);
            SetToggleIsOnWithoutNotify(
                overallRankingToggle,
                sortMode == ModelGalleryBrowseSortMode.OverallRanking);
            SetToggleIsOnWithoutNotify(latestSortToggle, sortMode == ModelGalleryBrowseSortMode.Latest);

            bool browseTabVisible = browseTabCanvas != null && browseTabCanvas.enabled;
            SetBrowseRefreshButtonVisible(
                browseTabVisible && sortMode == ModelGalleryBrowseSortMode.Random);
        }

        /// <inheritdoc />
        public void RefreshPostSlotList(IClayModelSaveService saveService)
        {
            RefreshSharedSlotList(saveService, SlotListMode.Post);
        }

        /// <inheritdoc />
        public void RefreshDownloadSlotList(IClayModelSaveService saveService)
        {
            RefreshSharedSlotList(saveService, SlotListMode.Download);
        }

        /// <inheritdoc />
        public void BindBrowseItems(Action<int> bindCell, int visibleCount)
        {
            if (browseItemCells == null)
            {
                return;
            }

            EnsureBrowseContentFlags();
            int count = Mathf.Min(visibleCount, browseItemCells.Length);
            for (int i = 0; i < browseItemCells.Length; i++)
            {
                ModelGalleryItemCellView cell = browseItemCells[i];
                if (cell == null)
                {
                    continue;
                }

                bool hasContent = i < count;
                browseCellHasContent[i] = hasContent;
                if (hasContent)
                {
                    bindCell?.Invoke(i);
                    cell.SetVisible(true);
                }
                else
                {
                    cell.SetVisible(false);
                }
            }

            ResetBrowseScrollToTop();
            ApplyBrowseViewportClip();
            ScheduleBrowseViewportClip();
        }

        /// <inheritdoc />
        public void SetBrowseItemCell(
            int cellIndex,
            string title,
            int favoriteCount,
            bool isFavorited,
            Texture2D thumbnail,
            bool isSelected)
        {
            if (browseItemCells == null || cellIndex < 0 || cellIndex >= browseItemCells.Length)
            {
                return;
            }

            browseItemCells[cellIndex]?.SetContent(
                title,
                favoriteCount,
                isFavorited,
                thumbnail,
                isSelected);
        }

        /// <inheritdoc />
        public void ShowPointsInsufficient()
        {
            if (pointsInsufficientCanvas == null)
            {
                Debug.LogError("[ModelGalleryView] pointsInsufficientCanvasが未配線です", this);
                return;
            }

            ApplyPointsInsufficientDynamicTexts();
            if (pointsInsufficientHeldPointsText != null)
            {
                pointsInsufficientHeldPointsText.gameObject.SetActive(true);
            }

            if (pointsInsufficientCostPointsText != null)
            {
                pointsInsufficientCostPointsText.gameObject.SetActive(true);
            }

            pointsInsufficientCanvas.enabled = true;
        }

        /// <inheritdoc />
        public void HidePointsInsufficient()
        {
            if (pointsInsufficientCanvas != null)
            {
                pointsInsufficientCanvas.enabled = false;
            }
        }

        /// <inheritdoc />
        public void ShowDownloadSlotSelect()
        {
            if (downloadSlotSelectCanvas == null)
            {
                Debug.LogError("[ModelGalleryView] downloadSlotSelectCanvasが未配線です", this);
                return;
            }

            slotListMode = SlotListMode.Download;
            SetSlotListHostVisible(true);
            downloadSlotSelectCanvas.enabled = true;
            if (postSlotScrollList != null)
            {
                postSlotScrollList.RefreshHostLayout();
            }
        }

        /// <inheritdoc />
        public void HideDownloadSlotSelect()
        {
            if (downloadSlotSelectCanvas != null)
            {
                downloadSlotSelectCanvas.enabled = false;
            }

            if (slotListMode == SlotListMode.Download)
            {
                SetSlotListHostVisible(false);
                slotListMode = SlotListMode.Post;
            }
        }

        /// <inheritdoc />
        public void ShowPostConfirm(string modelName, Texture2D thumbnail)
        {
            if (postConfirmCanvas == null)
            {
                Debug.LogError("[ModelGalleryView] postConfirmCanvasが未配線です", this);
                return;
            }

            HideDownloadConfirm();
            postConfirmVisible = true;
            postConfirmDisplayMode = PostConfirmDisplayMode.Confirm;
            postConfirmResultKey = string.Empty;
            postConfirmResultFallback = string.Empty;
            cachedPostConfirmName = modelName ?? string.Empty;

            if (postConfirmNameText != null)
            {
                postConfirmNameText.text = cachedPostConfirmName;
            }

            ApplyPostConfirmMessageText();
            ApplyPostConfirmButtonLabels();
            SetPostConfirmButtonVisible(publishVisible: true, closeVisible: true, resultCloseVisible: false);

            ReplacePostConfirmThumbnail(thumbnail);
            if (postConfirmThumbnailImage != null)
            {
                postConfirmThumbnailImage.enabled = ownedPostConfirmSprite != null;
                postConfirmThumbnailImage.sprite = ownedPostConfirmSprite;
            }

            postConfirmCanvas.enabled = true;
        }

        /// <inheritdoc />
        public void HidePostConfirm()
        {
            postConfirmVisible = false;
            postConfirmDisplayMode = PostConfirmDisplayMode.Confirm;
            postConfirmResultKey = string.Empty;
            postConfirmResultFallback = string.Empty;
            if (postConfirmCanvas != null)
            {
                postConfirmCanvas.enabled = false;
            }

            ReplacePostConfirmThumbnail(null);
        }

        /// <inheritdoc />
        public void ShowPostConfirmPublishing()
        {
            if (postConfirmCanvas == null)
            {
                Debug.LogError("[ModelGalleryView] postConfirmCanvasが未配線です", this);
                return;
            }

            postConfirmVisible = true;
            postConfirmDisplayMode = PostConfirmDisplayMode.Publishing;
            postConfirmResultKey = string.Empty;
            postConfirmResultFallback = string.Empty;
            ApplyPostConfirmMessageText();
            SetPostConfirmButtonVisible(publishVisible: false, closeVisible: false, resultCloseVisible: false);
            postConfirmCanvas.enabled = true;
        }

        /// <inheritdoc />
        public void ShowPostConfirmResult(string messageKey, string messageFallback)
        {
            if (postConfirmCanvas == null)
            {
                Debug.LogError("[ModelGalleryView] postConfirmCanvasが未配線です", this);
                return;
            }

            postConfirmVisible = true;
            postConfirmDisplayMode = PostConfirmDisplayMode.Result;
            postConfirmResultKey = messageKey ?? string.Empty;
            postConfirmResultFallback = messageFallback ?? string.Empty;
            ApplyPostConfirmMessageText();
            ApplyPostConfirmButtonLabels();
            SetPostConfirmButtonVisible(publishVisible: false, closeVisible: false, resultCloseVisible: true);
            postConfirmCanvas.enabled = true;
        }

        /// <inheritdoc />
        public void ShowDownloadConfirm(string modelName, bool isOverwrite, Texture2D thumbnail)
        {
            if (downloadConfirmCanvas == null)
            {
                Debug.LogError("[ModelGalleryView] downloadConfirmCanvasが未配線です", this);
                return;
            }

            HidePostConfirm();
            downloadConfirmVisible = true;
            downloadConfirmDisplayMode = DownloadConfirmDisplayMode.Confirm;
            downloadConfirmResultKey = string.Empty;
            downloadConfirmResultFallback = string.Empty;
            cachedDownloadConfirmName = modelName ?? string.Empty;
            downloadConfirmIsOverwrite = isOverwrite;

            if (downloadConfirmNameText != null)
            {
                downloadConfirmNameText.text = cachedDownloadConfirmName;
            }

            ApplyDownloadConfirmMessageText();
            ApplyDownloadConfirmButtonLabels();
            SetDownloadConfirmButtonVisible(saveVisible: true, closeVisible: true, resultCloseVisible: false);

            ReplaceDownloadConfirmThumbnail(thumbnail);
            if (downloadConfirmThumbnailImage != null)
            {
                downloadConfirmThumbnailImage.enabled = ownedDownloadConfirmSprite != null;
                downloadConfirmThumbnailImage.sprite = ownedDownloadConfirmSprite;
            }

            downloadConfirmCanvas.enabled = true;
        }

        /// <inheritdoc />
        public void ShowDownloadConfirmSaving()
        {
            if (downloadConfirmCanvas == null)
            {
                Debug.LogError("[ModelGalleryView] downloadConfirmCanvasが未配線です", this);
                return;
            }

            downloadConfirmVisible = true;
            downloadConfirmDisplayMode = DownloadConfirmDisplayMode.Saving;
            downloadConfirmResultKey = string.Empty;
            downloadConfirmResultFallback = string.Empty;
            ApplyDownloadConfirmMessageText();
            SetDownloadConfirmButtonVisible(saveVisible: false, closeVisible: false, resultCloseVisible: false);
            downloadConfirmCanvas.enabled = true;
        }

        /// <inheritdoc />
        public void ShowDownloadConfirmResult(string messageKey, string messageFallback)
        {
            if (downloadConfirmCanvas == null)
            {
                Debug.LogError("[ModelGalleryView] downloadConfirmCanvasが未配線です", this);
                return;
            }

            downloadConfirmVisible = true;
            downloadConfirmDisplayMode = DownloadConfirmDisplayMode.Result;
            downloadConfirmResultKey = messageKey ?? string.Empty;
            downloadConfirmResultFallback = messageFallback ?? string.Empty;
            ApplyDownloadConfirmMessageText();
            ApplyDownloadConfirmButtonLabels();
            SetDownloadConfirmButtonVisible(saveVisible: false, closeVisible: false, resultCloseVisible: true);
            downloadConfirmCanvas.enabled = true;
        }

        /// <inheritdoc />
        public void HideDownloadConfirm()
        {
            downloadConfirmVisible = false;
            downloadConfirmDisplayMode = DownloadConfirmDisplayMode.Confirm;
            downloadConfirmResultKey = string.Empty;
            downloadConfirmResultFallback = string.Empty;
            if (downloadConfirmCanvas != null)
            {
                downloadConfirmCanvas.enabled = false;
            }

            ReplaceDownloadConfirmThumbnail(null);
        }

        /// <inheritdoc />
        public IDisposable SubscribeCloseButtonClick(UnityAction action) =>
            SubscribeButton(closeButton, action, nameof(closeButton));

        /// <inheritdoc />
        public IDisposable SubscribePostTabButtonClick(UnityAction action) =>
            SubscribeToggleOn(postTabToggle, action, nameof(postTabToggle));

        /// <inheritdoc />
        public IDisposable SubscribeBrowseTabButtonClick(UnityAction action) =>
            SubscribeToggleOn(browseTabToggle, action, nameof(browseTabToggle));

        /// <inheritdoc />
        public IDisposable SubscribePostConfirmPublishButtonClick(UnityAction action) =>
            SubscribeButton(postConfirmPublishButton, action, nameof(postConfirmPublishButton));

        /// <inheritdoc />
        public IDisposable SubscribePostConfirmCloseButtonClick(UnityAction action)
        {
            IDisposable confirmClose = SubscribeButton(
                postConfirmCloseButton,
                action,
                nameof(postConfirmCloseButton));
            IDisposable resultClose = SubscribeButton(
                postConfirmResultCloseButton,
                action,
                nameof(postConfirmResultCloseButton));
            return new CompositeDisposable(confirmClose, resultClose);
        }

        /// <inheritdoc />
        public IDisposable SubscribeDownloadConfirmSaveButtonClick(UnityAction action) =>
            SubscribeButton(downloadConfirmSaveButton, action, nameof(downloadConfirmSaveButton));

        /// <inheritdoc />
        public IDisposable SubscribeDownloadConfirmCloseButtonClick(UnityAction action)
        {
            IDisposable confirmClose = SubscribeButton(
                downloadConfirmCloseButton,
                action,
                nameof(downloadConfirmCloseButton));
            IDisposable resultClose = SubscribeButton(
                downloadConfirmResultCloseButton,
                action,
                nameof(downloadConfirmResultCloseButton));
            return new CompositeDisposable(confirmClose, resultClose);
        }

        /// <inheritdoc />
        public IDisposable SubscribeRandomSortButtonClick(UnityAction action) =>
            SubscribeToggleOn(randomSortToggle, action, nameof(randomSortToggle));

        /// <inheritdoc />
        public IDisposable SubscribeMonthlyRankingButtonClick(UnityAction action) =>
            SubscribeToggleOn(monthlyRankingToggle, action, nameof(monthlyRankingToggle));

        /// <inheritdoc />
        public IDisposable SubscribeOverallRankingButtonClick(UnityAction action) =>
            SubscribeToggleOn(overallRankingToggle, action, nameof(overallRankingToggle));

        /// <inheritdoc />
        public IDisposable SubscribeLatestSortButtonClick(UnityAction action) =>
            SubscribeToggleOn(latestSortToggle, action, nameof(latestSortToggle));

        /// <inheritdoc />
        public IDisposable SubscribeBrowseRefreshButtonClick(UnityAction action) =>
            SubscribeButton(browseRefreshButton, action, nameof(browseRefreshButton));

        /// <inheritdoc />
        public IDisposable SubscribePostSlotSelected(UnityAction<int> action)
        {
            postSlotSelectedAction = action;
            EnsurePostSlotScrollInitialized();
            return new ActionClearDisposable(() => postSlotSelectedAction = null);
        }

        /// <inheritdoc />
        public IDisposable SubscribeDownloadSlotSelected(UnityAction<int> action)
        {
            downloadSlotSelectedAction = action;
            EnsurePostSlotScrollInitialized();
            return new ActionClearDisposable(() => downloadSlotSelectedAction = null);
        }

        /// <inheritdoc />
        public IDisposable SubscribeBrowseItemSelected(UnityAction<int> action)
        {
            browseItemSelectedAction = action;
            return new ActionClearDisposable(() => browseItemSelectedAction = null);
        }

        /// <inheritdoc />
        public IDisposable SubscribeBrowseFavoriteClicked(UnityAction<int> action)
        {
            browseFavoriteAction = action;
            return new ActionClearDisposable(() => browseFavoriteAction = null);
        }

        /// <inheritdoc />
        public IDisposable SubscribePointsInsufficientCloseButtonClick(UnityAction action) =>
            SubscribeButton(pointsInsufficientCloseButton, action, nameof(pointsInsufficientCloseButton));

        /// <inheritdoc />
        public IDisposable SubscribeDownloadSlotSelectCloseButtonClick(UnityAction action) =>
            SubscribeButton(downloadSlotSelectCloseButton, action, nameof(downloadSlotSelectCloseButton));

        private void RefreshSharedSlotList(IClayModelSaveService saveService, SlotListMode mode)
        {
            if (postSlotScrollList == null)
            {
                Debug.LogError("[ModelGalleryView] postSlotScrollListが未配線です", this);
                return;
            }

            if (saveService == null)
            {
                Debug.LogError("[ModelGalleryView] saveServiceがnullです", this);
                return;
            }

            slotListMode = mode;
            cachedSaveService = saveService;
            EnsurePostSlotScrollInitialized();
            ClearPostSlotRuntimeThumbnails();
            postSlotScrollList.RefreshSlots(
                ModelSavePool.Player,
                saveService,
                LocalizedText.Get(GameTextKeys.ModelGallerySlot),
                postSlotRuntimeThumbnails,
                allowEmptySlotSelection: true,
                ModelSaveSlotListContentMode.Full);
            postSlotScrollList.RefreshHostLayout();
            postSlotScrollList.ForceSelectionLayout();
        }

        private void ValidateReferences()
        {
            if (canvas == null || postTabCanvas == null || browseTabCanvas == null)
            {
                Debug.LogError("[ModelGalleryView] 主要Canvasが未配線です", this);
            }

            if (slotListHostCanvas == null)
            {
                Debug.LogError("[ModelGalleryView] slotListHostCanvasが未配線です", this);
            }

            if (pointsInsufficientCanvas == null)
            {
                Debug.LogError("[ModelGalleryView] pointsInsufficientCanvasが未配線です", this);
            }

            if (downloadSlotSelectCanvas == null)
            {
                Debug.LogError("[ModelGalleryView] downloadSlotSelectCanvasが未配線です", this);
            }

            if (postConfirmCanvas == null
                || postConfirmPublishButton == null
                || postConfirmCloseButton == null
                || postConfirmResultCloseButton == null
                || postConfirmPublishButtonCanvas == null
                || postConfirmCloseButtonCanvas == null
                || postConfirmResultCloseButtonCanvas == null)
            {
                Debug.LogError("[ModelGalleryView] postConfirm関連参照が未配線です", this);
            }

            if (downloadConfirmCanvas == null
                || downloadConfirmSaveButton == null
                || downloadConfirmCloseButton == null
                || downloadConfirmResultCloseButton == null
                || downloadConfirmSaveButtonCanvas == null
                || downloadConfirmCloseButtonCanvas == null
                || downloadConfirmResultCloseButtonCanvas == null
                || downloadConfirmCostText == null
                || downloadConfirmCostTextCanvas == null)
            {
                Debug.LogError("[ModelGalleryView] downloadConfirm関連参照が未配線です", this);
            }

            if (postSlotScrollList == null)
            {
                Debug.LogError("[ModelGalleryView] postSlotScrollListが未配線です", this);
            }

            if (browseItemCells == null || browseItemCells.Length == 0)
            {
                Debug.LogError("[ModelGalleryView] browseItemCellsが未配線です", this);
            }

            if (browseScrollRect == null)
            {
                Debug.LogError("[ModelGalleryView] browseScrollRectが未配線です", this);
            }

            if (browseViewport == null)
            {
                Debug.LogError("[ModelGalleryView] browseViewportが未配線です", this);
            }

            if (browseRefreshButton == null || browseRefreshButtonCanvas == null)
            {
                Debug.LogError("[ModelGalleryView] browseRefreshButtonが未配線です", this);
            }

            if (pointsInsufficientCloseButton == null)
            {
                Debug.LogError("[ModelGalleryView] pointsInsufficientCloseButtonが未配線です", this);
            }

            if (downloadSlotSelectCloseButton == null)
            {
                Debug.LogError("[ModelGalleryView] downloadSlotSelectCloseButtonが未配線です", this);
            }

            if (postTabToggle == null || browseTabToggle == null || tabToggleGroup == null)
            {
                Debug.LogError("[ModelGalleryView] タブToggleが未配線です", this);
            }

            if (randomSortToggle == null
                || monthlyRankingToggle == null
                || overallRankingToggle == null
                || latestSortToggle == null
                || browseSortToggleGroup == null)
            {
                Debug.LogError("[ModelGalleryView] 並び替えToggleが未配線です", this);
            }

            if (titleText == null)
            {
                Debug.LogError("[ModelGalleryView] titleTextが未配線です", this);
            }
        }

        private void SetTabToggle(bool isPostTab)
        {
            SetToggleIsOnWithoutNotify(postTabToggle, isPostTab);
            SetToggleIsOnWithoutNotify(browseTabToggle, !isPostTab);
        }

        /// <summary>
        /// タブと並び替えToggleでSelectableのSelected/dark tintを使わない
        /// 選択中は明るいHighlightスプライトと太字ラベルで示す
        /// </summary>
        private void ConfigureTabAndSortTogglesNoSelected()
        {
            ConfigureToggleNoSelected(postTabToggle);
            ConfigureToggleNoSelected(browseTabToggle);
            ConfigureToggleNoSelected(randomSortToggle);
            ConfigureToggleNoSelected(monthlyRankingToggle);
            ConfigureToggleNoSelected(overallRankingToggle);
            ConfigureToggleNoSelected(latestSortToggle);
        }

        private void CacheToggleSprites()
        {
            toggleOffSprite = Resources.Load<Sprite>(ToggleOffSpritePath);
            toggleOnSprite = Resources.Load<Sprite>(ToggleOnSpritePath);
            togglePressedSprite = Resources.Load<Sprite>(TogglePressedSpritePath);
            if (toggleOffSprite == null)
            {
                Debug.LogError($"[ModelGalleryView] ToggleOffスプライトがありません: {ToggleOffSpritePath}", this);
            }

            if (toggleOnSprite == null)
            {
                Debug.LogError(
                    $"[ModelGalleryView] Toggle選択スプライトがありません: {ToggleOnSpritePath}",
                    this);
            }

            if (togglePressedSprite == null)
            {
                Debug.LogError(
                    $"[ModelGalleryView] Toggle押下スプライトがありません: {TogglePressedSpritePath}",
                    this);
            }
        }

        private void ConfigureToggleNoSelected(Toggle toggle)
        {
            if (toggle == null)
            {
                return;
            }

            toggle.navigation = new Navigation { mode = Navigation.Mode.None };
            // 色乗算で暗くなるのを避けスプライト差し替えのみ使う
            toggle.transition = Selectable.Transition.SpriteSwap;
            toggle.graphic = null;
            ColorBlock colors = toggle.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.disabledColor = Color.white;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0f;
            toggle.colors = colors;
            toggle.spriteState = new SpriteState
            {
                highlightedSprite = toggleOnSprite,
                pressedSprite = togglePressedSprite != null ? togglePressedSprite : toggleOnSprite,
                selectedSprite = toggleOnSprite,
                disabledSprite = toggleOffSprite
            };
            toggle.onValueChanged.AddListener(_ => ApplyToggleActiveVisual(toggle));
            ApplyToggleActiveVisual(toggle);
        }

        private void SetToggleIsOnWithoutNotify(Toggle toggle, bool isOn)
        {
            if (toggle == null)
            {
                return;
            }

            if (toggle.isOn != isOn)
            {
                toggle.SetIsOnWithoutNotify(isOn);
            }

            ApplyToggleActiveVisual(toggle);
        }

        private void ApplyToggleActiveVisual(Toggle toggle)
        {
            if (toggle == null)
            {
                return;
            }

            bool isOn = toggle.isOn;
            if (toggle.targetGraphic is Image image)
            {
                // 選択中は暗いOnではなく明るいHighlight
                Sprite sprite = isOn ? toggleOnSprite : toggleOffSprite;
                if (sprite != null)
                {
                    image.sprite = sprite;
                    image.type = Image.Type.Sliced;
                }

                image.color = Color.white;
                image.CrossFadeColor(Color.white, 0f, true, true);
            }

            TMP_Text label = toggle.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.color = isOn ? LabelActiveColor : LabelInactiveColor;
                label.fontStyle = isOn ? FontStyles.Bold : FontStyles.Normal;
            }
        }

        private void ApplyPointsInsufficientDynamicTexts()
        {
            if (pointsInsufficientMessageText != null)
            {
                LocalizedFont.SetText(
                    pointsInsufficientMessageText,
                    LocalizedText.GetOrFallback(
                        GameTextKeys.ModelGalleryPointsInsufficient,
                        "ポイントが不足しています"));
            }

            if (pointsInsufficientHeldPointsText != null)
            {
                LocalizedFont.SetText(
                    pointsInsufficientHeldPointsText,
                    LocalizedText.GetOrFallback(
                        GameTextKeys.ModelGalleryHeldPoints,
                        "所持: {points} ポイント",
                        "points",
                        cachedPoints));
            }

            if (pointsInsufficientCostPointsText != null)
            {
                LocalizedFont.SetText(
                    pointsInsufficientCostPointsText,
                    LocalizedText.GetOrFallback(
                        GameTextKeys.ModelGalleryCostPoints,
                        "消費: {points} ポイント",
                        "points",
                        ModelGalleryDownloadSettings.DownloadCostPoints));
            }
        }

        private void SetBrowseRefreshButtonVisible(bool visible)
        {
            SetCanvasEnabled(browseRefreshButtonCanvas, visible);
        }


        private void SetDownloadConfirmButtonVisible(
            bool saveVisible,
            bool closeVisible,
            bool resultCloseVisible)
        {
            SetCanvasEnabled(downloadConfirmSaveButtonCanvas, saveVisible);
            SetCanvasEnabled(downloadConfirmCloseButtonCanvas, closeVisible);
            SetCanvasEnabled(downloadConfirmResultCloseButtonCanvas, resultCloseVisible);
            // 確認中のみ消費ポイントを表示する
            SetCanvasEnabled(
                downloadConfirmCostTextCanvas,
                saveVisible && closeVisible && !resultCloseVisible);
            ApplyDownloadConfirmCostText();
        }


        private void ApplyDownloadConfirmCostText()
        {
            if (downloadConfirmCostText == null)
            {
                return;
            }

            if (downloadConfirmDisplayMode != DownloadConfirmDisplayMode.Confirm)
            {
                return;
            }

            LocalizedFont.SetText(
                downloadConfirmCostText,
                LocalizedText.GetOrFallback(
                    GameTextKeys.ModelGalleryCostPoints,
                    "消費: {points} ポイント",
                    "points",
                    ModelGalleryDownloadSettings.DownloadCostPoints));
        }

        private void ApplyDownloadConfirmMessageText()
        {
            if (downloadConfirmMessageText == null)
            {
                return;
            }

            switch (downloadConfirmDisplayMode)
            {
                case DownloadConfirmDisplayMode.Saving:
                    downloadConfirmMessageText.text = LocalizedText.GetOrFallback(
                        GameTextKeys.ModelGallerySaving,
                        "保存中…");
                    break;
                case DownloadConfirmDisplayMode.Result:
                    downloadConfirmMessageText.text = LocalizedText.GetOrFallback(
                        downloadConfirmResultKey,
                        downloadConfirmResultFallback);
                    break;
                default:
                    downloadConfirmMessageText.text = downloadConfirmIsOverwrite
                        ? LocalizedText.GetOrFallback(
                            GameTextKeys.ModelGalleryOverwriteConfirm,
                            "既存のセーブデータに上書き保存しますか？")
                        : LocalizedText.GetOrFallback(
                            GameTextKeys.ModelGallerySaveToSlotConfirm,
                            "このスロットに保存しますか？");
                    break;
            }
        }

        private void ApplyDownloadConfirmButtonLabels()
        {
            LhButtonLabelUtility.SetLabel(
                downloadConfirmSaveButton,
                SceneLocalizedLabel.Resolve(GameTextKeys.CommonSave, downloadSaveOriginal));
            LhButtonLabelUtility.SetLabel(
                downloadConfirmCloseButton,
                SceneLocalizedLabel.Resolve(GameTextKeys.CommonClose, downloadConfirmCloseOriginal));
            LhButtonLabelUtility.SetLabel(
                downloadConfirmResultCloseButton,
                SceneLocalizedLabel.Resolve(GameTextKeys.CommonClose, downloadConfirmCloseOriginal));
        }

        private void SetPostConfirmButtonVisible(
            bool publishVisible,
            bool closeVisible,
            bool resultCloseVisible)
        {
            SetCanvasEnabled(postConfirmPublishButtonCanvas, publishVisible);
            SetCanvasEnabled(postConfirmCloseButtonCanvas, closeVisible);
            SetCanvasEnabled(postConfirmResultCloseButtonCanvas, resultCloseVisible);
        }

        private void ApplyPostConfirmMessageText()
        {
            if (postConfirmMessageText == null)
            {
                return;
            }

            switch (postConfirmDisplayMode)
            {
                case PostConfirmDisplayMode.Publishing:
                    postConfirmMessageText.text = LocalizedText.GetOrFallback(
                        GameTextKeys.ModelGalleryPublishing,
                        "投稿中…");
                    break;
                case PostConfirmDisplayMode.Result:
                    postConfirmMessageText.text = LocalizedText.GetOrFallback(
                        postConfirmResultKey,
                        postConfirmResultFallback);
                    break;
                default:
                    postConfirmMessageText.text = LocalizedText.GetOrFallback(
                        GameTextKeys.ModelGalleryPostConfirm,
                        "投稿しますか？");
                    break;
            }
        }

        private void ApplyPostConfirmButtonLabels()
        {
            LhButtonLabelUtility.SetLabel(
                postConfirmPublishButton,
                SceneLocalizedLabel.Resolve(GameTextKeys.ModelGalleryPublish, publishOriginal));
            LhButtonLabelUtility.SetLabel(
                postConfirmCloseButton,
                SceneLocalizedLabel.Resolve(GameTextKeys.CommonClose, postConfirmCloseOriginal));
            LhButtonLabelUtility.SetLabel(
                postConfirmResultCloseButton,
                SceneLocalizedLabel.Resolve(GameTextKeys.CommonClose, postConfirmCloseOriginal));
        }

        private void BindBrowseScroll()
        {
            if (isBrowseScrollBound || browseScrollRect == null)
            {
                return;
            }

            browseScrollRect.onValueChanged.AddListener(OnBrowseScrollValueChanged);
            isBrowseScrollBound = true;
        }

        private void OnBrowseScrollValueChanged(Vector2 _)
        {
            ApplyBrowseViewportClip();
        }

        private void EnsureBrowseContentFlags()
        {
            int length = browseItemCells?.Length ?? 0;
            if (browseCellHasContent == null || browseCellHasContent.Length != length)
            {
                browseCellHasContent = new bool[length];
            }
        }

        private void ResetBrowseScrollToTop()
        {
            if (browseScrollRect == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            browseScrollRect.StopMovement();
            browseScrollRect.verticalNormalizedPosition = 1f;
            browseScrollRect.horizontalNormalizedPosition = 0f;
        }

        private void ApplyBrowseViewportClip()
        {
            if (browseItemCells == null || browseViewport == null)
            {
                return;
            }

            EnsureBrowseContentFlags();
            Canvas.ForceUpdateCanvases();
            Rect viewportRect = GetWorldRect(browseViewport);
            bool viewportValid = viewportRect.width > 1f && viewportRect.height > 1f;
            for (int i = 0; i < browseItemCells.Length; i++)
            {
                ModelGalleryItemCellView cell = browseItemCells[i];
                if (cell == null)
                {
                    continue;
                }

                bool hasContent = i < browseCellHasContent.Length && browseCellHasContent[i];
                if (!hasContent)
                {
                    cell.SetVisible(false);
                    continue;
                }

                if (!viewportValid)
                {
                    cell.SetVisible(true);
                    continue;
                }

                RectTransform cellRect = cell.transform as RectTransform;
                if (cellRect == null)
                {
                    cell.SetVisible(true);
                    continue;
                }

                Rect cellWorldRect = GetWorldRect(cellRect);
                bool cellLayoutReady = cellWorldRect.width > 1f && cellWorldRect.height > 1f;
                bool overlaps = !cellLayoutReady || viewportRect.Overlaps(cellWorldRect, true);
                cell.SetVisible(overlaps);
            }
        }

        private void ScheduleBrowseViewportClip()
        {
            browseClipCts?.Cancel();
            browseClipCts?.Dispose();
            browseClipCts = new CancellationTokenSource();
            CancellationToken token = browseClipCts.Token;
            UniTask.Void(async () =>
            {
                try
                {
                    await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, token);
                    if (this == null)
                    {
                        return;
                    }

                    ApplyBrowseViewportClip();
                }
                catch (OperationCanceledException)
                {
                }
            });
        }

        private void SetSlotListHostVisible(bool visible)
        {
            if (slotListHostCanvas == null)
            {
                Debug.LogError("[ModelGalleryView] slotListHostCanvasが未配線です", this);
                return;
            }

            slotListHostCanvas.enabled = visible;
        }

        private Rect GetWorldRect(RectTransform rectTransform)
        {
            rectTransform.GetWorldCorners(worldCorners);
            Vector3 min = worldCorners[0];
            Vector3 max = worldCorners[2];
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private void EnsurePostSlotScrollInitialized()
        {
            if (isPostSlotScrollInitialized || postSlotScrollList == null)
            {
                return;
            }

            postSlotScrollList.Initialize(OnSharedSlotSelected);
            isPostSlotScrollInitialized = true;
        }

        private void OnSharedSlotSelected(int slotIndex)
        {
            if (slotListMode == SlotListMode.Download)
            {
                downloadSlotSelectedAction?.Invoke(slotIndex);
                return;
            }

            postSlotSelectedAction?.Invoke(slotIndex);
        }

        private void BindCellClicks()
        {
            if (isClickBound)
            {
                return;
            }

            if (browseItemCells != null)
            {
                for (int i = 0; i < browseItemCells.Length; i++)
                {
                    int cellIndex = i;
                    browseItemCells[i]?.SubscribeClick(() => browseItemSelectedAction?.Invoke(cellIndex));
                    browseItemCells[i]?.SubscribeFavoriteClick(() => browseFavoriteAction?.Invoke(cellIndex));
                }
            }

            isClickBound = true;
        }

        private void DisableAllCellCanvases()
        {
            EnsureBrowseContentFlags();
            if (browseCellHasContent != null)
            {
                for (int i = 0; i < browseCellHasContent.Length; i++)
                {
                    browseCellHasContent[i] = false;
                }
            }

            if (browseItemCells != null)
            {
                for (int i = 0; i < browseItemCells.Length; i++)
                {
                    browseItemCells[i]?.SetVisible(false);
                }
            }
        }

        private void ClearPostSlotRuntimeThumbnails()
        {
            for (int i = 0; i < postSlotRuntimeThumbnails.Count; i++)
            {
                UnityEngine.Object obj = postSlotRuntimeThumbnails[i];
                if (obj != null)
                {
                    Destroy(obj);
                }
            }

            postSlotRuntimeThumbnails.Clear();
        }

        private IDisposable SubscribeButton(LHButton button, UnityAction action, string fieldName)
        {
            if (button == null)
            {
                Debug.LogError($"[ModelGalleryView] {fieldName}が未配線です", this);
                return EmptyDisposable.Instance;
            }

            return button.SubscribeOnClick(action);
        }

        private IDisposable SubscribeToggleOn(Toggle toggle, UnityAction action, string fieldName)
        {
            if (toggle == null)
            {
                Debug.LogError($"[ModelGalleryView] {fieldName}が未配線です", this);
                return EmptyDisposable.Instance;
            }

            if (action == null)
            {
                return EmptyDisposable.Instance;
            }

            UnityAction<bool> listener = isOn =>
            {
                if (isOn)
                {
                    action.Invoke();
                }
            };
            toggle.onValueChanged.AddListener(listener);
            return new ActionClearDisposable(() => toggle.onValueChanged.RemoveListener(listener));
        }

        private static void SetCanvasEnabled(Canvas target, bool enabled)
        {
            if (target != null)
            {
                target.enabled = enabled;
            }
        }

        private void ReplacePostConfirmThumbnail(Texture2D thumbnail)
        {
            if (ownedPostConfirmSprite != null)
            {
                Destroy(ownedPostConfirmSprite);
                ownedPostConfirmSprite = null;
            }

            if (ownedPostConfirmThumbnail != null)
            {
                Destroy(ownedPostConfirmThumbnail);
                ownedPostConfirmThumbnail = null;
            }

            ownedPostConfirmThumbnail = thumbnail;
            if (thumbnail != null)
            {
                ownedPostConfirmSprite = Sprite.Create(
                    thumbnail,
                    new Rect(0f, 0f, thumbnail.width, thumbnail.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
            }
        }

        private void ReplaceDownloadConfirmThumbnail(Texture2D thumbnail)
        {
            if (ownedDownloadConfirmSprite != null)
            {
                Destroy(ownedDownloadConfirmSprite);
                ownedDownloadConfirmSprite = null;
            }

            if (ownedDownloadConfirmThumbnail != null)
            {
                Destroy(ownedDownloadConfirmThumbnail);
                ownedDownloadConfirmThumbnail = null;
            }

            ownedDownloadConfirmThumbnail = thumbnail;
            if (thumbnail != null)
            {
                ownedDownloadConfirmSprite = Sprite.Create(
                    thumbnail,
                    new Rect(0f, 0f, thumbnail.width, thumbnail.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
            }
        }

        private void OnDestroy()
        {
            if (browseScrollRect != null && isBrowseScrollBound)
            {
                browseScrollRect.onValueChanged.RemoveListener(OnBrowseScrollValueChanged);
                isBrowseScrollBound = false;
            }

            browseClipCts?.Cancel();
            browseClipCts?.Dispose();
            browseClipCts = null;
            ReplacePostConfirmThumbnail(null);
            ReplaceDownloadConfirmThumbnail(null);
            ClearPostSlotRuntimeThumbnails();
        }

        private sealed class EmptyDisposable : IDisposable
        {
            public static readonly EmptyDisposable Instance = new();

            public void Dispose()
            {
            }
        }

        private enum PostConfirmDisplayMode
        {
            Confirm = 0,
            Publishing = 1,
            Result = 2
        }

        private enum DownloadConfirmDisplayMode
        {
            Confirm = 0,
            Saving = 1,
            Result = 2
        }

        private sealed class CompositeDisposable : IDisposable
        {
            private readonly IDisposable first;
            private readonly IDisposable second;

            public CompositeDisposable(IDisposable first, IDisposable second)
            {
                this.first = first;
                this.second = second;
            }

            public void Dispose()
            {
                first?.Dispose();
                second?.Dispose();
            }
        }

        private sealed class ActionClearDisposable : IDisposable
        {
            private Action clearAction;

            public ActionClearDisposable(Action clearAction)
            {
                this.clearAction = clearAction;
            }

            public void Dispose()
            {
                clearAction?.Invoke();
                clearAction = null;
            }
        }
    }
}
