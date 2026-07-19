using ClayEditor;
using Cysharp.Threading.Tasks;
using Extensions;
using LighthouseExtends.UIComponent.Button;
using R3;
using SaveData;
using SaveData.Interface;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace UI.ClayEditor.View
{
    /// <summary>
    /// ClayEdit作り直し用のプレイヤースロット選択View
    /// スロット選択後にGLBを読み込みボクセル化して編集開始へ進む
    /// </summary>
    public sealed class ClayEditRemakeLoadSlotView : MonoBehaviour
    {
        private const int SelectionCanvasSortingOrder = 115;
        private const int ConfirmCanvasSortingOrder = 116;

        [Inject] private readonly IClayModelSaveService saveService;
        [Inject] private readonly IClayModelImporter importer;
        [Inject] private readonly ClayEditSavedModelImporter savedModelImporter;

        [Tooltip("ONのときフォールバックUIを実行時生成しない")]
        [SerializeField] private bool useSceneCanvasLayout = true;

        [Header("セーブスロット選択")]
        [SerializeField] private ModelSaveSlotScrollListView slotScrollList;
        [SerializeField] private string emptySlotLabel = "空き";

        [Header("確認キャンバス")]
        [SerializeField] private Canvas confirmCanvas;
        [Tooltip("確認背景Image。未設定ならレイキャスト調整をスキップする")]
        [SerializeField] private Image confirmBackgroundImage;
        [SerializeField] private ModelSaveConfirmView loadConfirmView;
        [SerializeField] private LHButton selectButton;
        [SerializeField] private LHButton deleteButton;
        [SerializeField] private LHButton backButton;
        [SerializeField] private LHButton listBackButton;
        [SerializeField] private ModelSaveSlotDeletePromptView deletePromptView;

        [Header("ロード設定")]
        [SerializeField] private Transform spawnParent;
        [SerializeField] private Material clayMaterial;

        private readonly Subject<ClayEditRemakeSelection> remakeConfirmedSubject = new();
        private readonly Subject<Unit> cancelledSubject = new();
        private readonly List<Object> runtimeThumbnailObjects = new();

        private int selectedSlot = -1;
        private bool isLoading;
        private bool isInitialized;
        private bool slotScrollListInitialized;

        /// <summary>
        /// 作り直し対象が確定しボクセル化が完了した通知
        /// </summary>
        public Observable<ClayEditRemakeSelection> OnRemakeConfirmed => remakeConfirmedSubject;

        /// <summary>
        /// スロット選択をキャンセルした通知
        /// </summary>
        public Observable<Unit> OnCancelled => cancelledSubject;

        private void Start()
        {
            EnsureSlotScrollListReady();

            if (selectButton != null)
            {
                selectButton.SubscribeOnClick(OnSelectConfirmed);
            }

            if (backButton != null)
            {
                backButton.SubscribeOnClick(OnConfirmBack);
            }

            if (deleteButton != null)
            {
                deleteButton.SubscribeOnClick(OnDeleteRequested);
            }

            if (listBackButton != null)
            {
                listBackButton.SubscribeOnClick(OnListBack);
            }

            SetConfirmVisible(false);
            ValidateSceneLayout();
            isInitialized = true;
        }

        private void OnEnable()
        {
            ApplySelectionCanvasSorting();
            ModelSaveSlotScrollListView.ApplySelectionCanvasLayout(GetComponent<Canvas>());

            if (isInitialized)
            {
                TryRefreshWhenHostReady();
            }
        }

        /// <summary>
        /// 作り直し用スロット選択UIを表示する
        /// </summary>
        public void Show()
        {
            selectedSlot = -1;
            SetConfirmVisible(false);
            Canvas canvas = GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.enabled = true;
            }

            gameObject.SetActive(true);
            ApplySelectionCanvasSorting();
            ModelSaveSlotScrollListView.ApplySelectionCanvasLayout(canvas);
            if (!TryRefreshWhenHostReady())
            {
                Debug.LogWarning(
                    "[ClayEditRemakeLoadSlotView] スロット一覧のホストレイアウト未確定のため更新を延期します");
            }
        }

        /// <summary>
        /// 作り直し用スロット選択UIを隠す
        /// </summary>
        public void Hide()
        {
            selectedSlot = -1;
            isLoading = false;
            loadConfirmView?.Clear();
            deletePromptView?.Hide();
            SetConfirmVisible(false);
            Canvas canvas = GetComponent<Canvas>();
            CanvasVisibilityUtility.SetCanvasEnabled(canvas, false);
            ModelSaveSlotScrollListView.ExitFullscreenSelectionLayout();
        }

        private void RefreshSlots()
        {
            TryRefreshWhenHostReady();
        }

        private void RefreshSlotsInternal()
        {
            if (saveService == null || slotScrollList == null)
            {
                return;
            }

            ClearRuntimeThumbnails();
            slotScrollList.RefreshSlots(
                ModelSavePool.Player,
                saveService,
                emptySlotLabel,
                runtimeThumbnailObjects,
                allowEmptySlotSelection: false);
        }

        private void EnsureSlotScrollListReady()
        {
            ModelSaveSlotScrollListView previousScrollList = slotScrollList;
            EnsureSlotScrollList();
            if (slotScrollList == null)
            {
                return;
            }

            bool hostReplaced = previousScrollList != slotScrollList;
            if (!slotScrollListInitialized || hostReplaced)
            {
                slotScrollList.Initialize(OnSlotSelected);
                slotScrollListInitialized = true;
            }

            slotScrollList.RefreshHostLayout();
        }

        private bool TryRefreshWhenHostReady()
        {
            EnsureSlotScrollListReady();
            if (slotScrollList == null)
            {
                return false;
            }

            RectTransform hostRect = slotScrollList.transform as RectTransform;
            if (hostRect == null)
            {
                return false;
            }

            Canvas.ForceUpdateCanvases();
            slotScrollList.RefreshHostLayout();

            if (hostRect.rect.width <= 1f || hostRect.rect.height <= 1f)
            {
                RefreshHostLayoutDeferred();
                return false;
            }

            RefreshSlotsInternal();
            return true;
        }

        private void RefreshHostLayoutDeferred()
        {
            UniTask.Void(async () =>
            {
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
                if (this == null || !isActiveAndEnabled)
                {
                    return;
                }

                TryRefreshWhenHostReady();
            });
        }

        private void OnSlotSelected(int slotIndex)
        {
            ModelSaveSlot slot = saveService.GetSlot(ModelSavePool.Player, slotIndex);
            if (slot == null || string.IsNullOrEmpty(slot.glbFileName))
            {
                return;
            }

            selectedSlot = slotIndex;
            SetConfirmVisible(true);
            RefreshLoadConfirm(slotIndex);
        }

        private void OnConfirmBack()
        {
            selectedSlot = -1;
            loadConfirmView?.Clear();
            SetConfirmVisible(false);
        }

        private void OnListBack()
        {
            Hide();
            cancelledSubject.OnNext(Unit.Default);
        }

        private void OnSelectConfirmed()
        {
            if (isLoading || selectedSlot < 0)
            {
                return;
            }

            LoadAndImportAsync(selectedSlot, this.GetCancellationTokenOnDestroy()).Forget();
        }

        private void OnDeleteRequested()
        {
            if (isLoading || selectedSlot < 0 || saveService == null)
            {
                return;
            }

            ModelSaveSlot slot = saveService.GetSlot(ModelSavePool.Player, selectedSlot);
            if (slot == null || string.IsNullOrEmpty(slot.glbFileName))
            {
                return;
            }

            if (deletePromptView == null)
            {
                Debug.LogError("[ClayEditRemakeLoadSlotView] deletePromptViewが未設定です");
                return;
            }

            deletePromptView.Show(slot.modelName, selectedSlot, OnDeleteConfirmed);
        }

        private void OnDeleteConfirmed()
        {
            if (selectedSlot < 0 || saveService == null)
            {
                return;
            }

            saveService.DeleteSlot(ModelSavePool.Player, selectedSlot);
            selectedSlot = -1;
            loadConfirmView?.Clear();
            SetConfirmVisible(false);
            RefreshSlots();
        }

        private async UniTaskVoid LoadAndImportAsync(int slotIndex, CancellationToken cancellationToken)
        {
            ModelSaveSlot slot = saveService.GetSlot(ModelSavePool.Player, slotIndex);
            if (slot == null || string.IsNullOrEmpty(slot.glbFileName))
            {
                Debug.LogWarning("[ClayEditRemakeLoadSlotView] スロットにモデルデータがありません");
                return;
            }

            string filePath = Path.Combine(Application.persistentDataPath, slot.glbFileName);
            if (!File.Exists(filePath))
            {
                Debug.LogError($"[ClayEditRemakeLoadSlotView] glbファイルが見つかりません: {filePath}");
                return;
            }

            isLoading = true;
            try
            {
                SetConfirmVisible(false);
                SetCanvasEnabled(GetComponent<Canvas>(), false);

                (bool snapshotSuccess, string snapshotError) = await savedModelImporter.TryImportFromSnapshotAsync(
                    ModelSavePool.Player,
                    slotIndex,
                    cancellationToken);
                if (snapshotSuccess)
                {
                    remakeConfirmedSubject.OnNext(new ClayEditRemakeSelection(slotIndex, slot.modelName));
                    return;
                }

                Debug.LogWarning(
                    $"[ClayEditRemakeLoadSlotView] ボクセルスナップショット復元に失敗しました: {snapshotError} GLBからの再変換を試みます");

                GameObject imported = await importer.ImportFromGlbAsync(filePath, spawnParent, cancellationToken);
                if (imported == null)
                {
                    Debug.LogError("[ClayEditRemakeLoadSlotView] モデルのロードに失敗しました");
                    Show();
                    return;
                }

                imported.transform.localPosition = Vector3.zero;
                imported.transform.localRotation = Quaternion.identity;
                imported.transform.localScale = Vector3.one;

                ApplyClayMaterial(imported);
                SetImportedRenderersEnabled(imported, false);

                (bool importSuccess, string errorMessage) = await savedModelImporter.TryImportFromLoadedModelAsync(
                    imported,
                    cancellationToken);
                if (!importSuccess)
                {
                    Debug.LogError($"[ClayEditRemakeLoadSlotView] ボクセル化に失敗しました: {errorMessage}");
                    Destroy(imported);
                    Show();
                    return;
                }

                Destroy(imported);
                remakeConfirmedSubject.OnNext(new ClayEditRemakeSelection(slotIndex, slot.modelName));
            }
            finally
            {
                isLoading = false;
            }
        }

        private void ApplyClayMaterial(GameObject importedRoot)
        {
            if (clayMaterial == null || importedRoot == null)
            {
                return;
            }

            SkinnedMeshRenderer[] skinnedRenderers = importedRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skinnedRenderers.Length; i++)
            {
                skinnedRenderers[i].sharedMaterial = clayMaterial;
            }

            MeshRenderer[] meshRenderers = importedRoot.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < meshRenderers.Length; i++)
            {
                meshRenderers[i].sharedMaterial = clayMaterial;
            }
        }

        private static void SetImportedRenderersEnabled(GameObject importedRoot, bool enabled)
        {
            if (importedRoot == null)
            {
                return;
            }

            Renderer[] renderers = importedRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = enabled;
            }
        }

        private void RefreshLoadConfirm(int slotIndex)
        {
            if (loadConfirmView == null || saveService == null)
            {
                return;
            }

            ModelSaveSlot slot = saveService.GetSlot(ModelSavePool.Player, slotIndex);
            if (slot == null)
            {
                return;
            }

            loadConfirmView.ShowSlot(slot, ModelSaveStorage.ReadThumbnailPng(slot), slotIndex);
        }

        private void ApplySelectionCanvasSorting()
        {
            CanvasVisibilityUtility.ApplyOverrideSorting(GetComponent<Canvas>(), SelectionCanvasSortingOrder);
        }

        /// <summary>
        /// 確認キャンバスの表示を切り替え選択側レイキャストを制御する
        /// </summary>
        private void SetConfirmVisible(bool visible)
        {
            if (visible)
            {
                SetSelectionRaycastsEnabled(false);
                SetCanvasEnabled(confirmCanvas, true, ConfirmCanvasSortingOrder);
                EnsureConfirmRaycastBlocker();
                if (confirmCanvas != null)
                {
                    confirmCanvas.transform.SetAsLastSibling();
                }
                return;
            }

            SetCanvasEnabled(confirmCanvas, false);
            SetSelectionRaycastsEnabled(true);
        }

        /// <summary>
        /// 確認背景のレイキャストを有効にして背面クリックを遮断する
        /// </summary>
        private void EnsureConfirmRaycastBlocker()
        {
            if (confirmCanvas == null)
            {
                return;
            }

            ModelSaveSlotScrollListView.EnsureSelectionBackground(confirmCanvas);
            if (confirmBackgroundImage == null)
            {
                return;
            }

            confirmBackgroundImage.raycastTarget = true;
        }

        /// <summary>
        /// 選択ルートCanvasのGraphicRaycasterを切り替える
        /// </summary>
        private void SetSelectionRaycastsEnabled(bool enabled)
        {
            Canvas selectionCanvas = GetComponent<Canvas>();
            if (selectionCanvas != null && selectionCanvas.TryGetComponent(out GraphicRaycaster raycaster))
            {
                raycaster.enabled = enabled;
            }
        }

        private static void SetCanvasEnabled(Canvas canvas, bool isEnabled, int sortingOrder = 0)
        {
            if (isEnabled && sortingOrder > 0)
            {
                CanvasVisibilityUtility.SetCanvasEnabled(canvas, true, sortingOrder);
                return;
            }

            CanvasVisibilityUtility.SetCanvasEnabled(canvas, isEnabled);
        }

        private void ClearRuntimeThumbnails()
        {
            for (int i = 0; i < runtimeThumbnailObjects.Count; i++)
            {
                if (runtimeThumbnailObjects[i] != null)
                {
                    Destroy(runtimeThumbnailObjects[i]);
                }
            }

            runtimeThumbnailObjects.Clear();
        }

        private void EnsureSlotScrollList()
        {
            if (slotScrollList != null)
            {
                return;
            }

            Debug.LogError(
                "[ClayEditRemakeLoadSlotView] slotScrollListが未設定です。Editor Wireツールで参照を配線してください",
                this);
        }

        private void ValidateSceneLayout()
        {
            if (!useSceneCanvasLayout)
            {
                return;
            }

            if (slotScrollList == null
                || confirmCanvas == null
                || loadConfirmView == null
                || selectButton == null
                || deleteButton == null
                || backButton == null
                || listBackButton == null
                || deletePromptView == null)
            {
                Debug.LogError(
                    "[ClayEditRemakeLoadSlotView] シーン上のUI参照が未設定です。HierarchyでUI参照を確認してください",
                    this);
            }
        }

        private void OnDestroy()
        {
            ClearRuntimeThumbnails();
            remakeConfirmedSubject.Dispose();
            cancelledSubject.Dispose();
        }
    }
}
