using ClayEditor;
using Cysharp.Threading.Tasks;
using Extensions;
using LighthouseExtends.UIComponent.Button;
using Localization;
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
        [Inject] private readonly ClayVoxelEngine voxelEngine;

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
        [Tooltip("一時インポート親未設定時はシーンルートへ置く")]
        [SerializeField] private Transform spawnParent;

        private readonly Subject<ClayEditRemakeSelection> remakeConfirmedSubject = new();
        private readonly Subject<Unit> cancelledSubject = new();
        private readonly List<Object> runtimeThumbnailObjects = new();

        private int selectedSlot = -1;
        private ModelSavePool currentPool = ModelSavePool.Player;
#if UNITY_EDITOR
        private ModelSavePool? pendingPoolSwitch;
#endif
        private bool isLoading;
        private bool isInitialized;
        private bool slotScrollListInitialized;
        private bool isSelectionVisible;

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
            // VContainer未登録の重複インスタンスは初期化しない
            if (saveService == null || importer == null || savedModelImporter == null)
            {
                enabled = false;
                return;
            }

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

        private void Update()
        {
#if UNITY_EDITOR
            if (pendingPoolSwitch == null || isLoading)
            {
                return;
            }

            ModelSavePool nextPool = pendingPoolSwitch.Value;
            pendingPoolSwitch = null;
            SwitchPool(nextPool);
#endif
        }

        /// <summary>
        /// 作り直し用スロット選択UIを表示する
        /// </summary>
        public void Show()
        {
            Show(ModelSavePool.Player);
        }

        /// <summary>
        /// 指定プールの作り直し用スロット選択UIを表示する
        /// </summary>
        /// <param name="pool">セーブプール</param>
        public void Show(ModelSavePool pool)
        {
            if (!CanSelectPool(pool))
            {
                Debug.LogError(
                    $"[ClayEditRemakeLoadSlotView] プール{pool}は作り直し対象にできません",
                    this);
                return;
            }

            currentPool = pool;
            selectedSlot = -1;
            isSelectionVisible = true;
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
            isSelectionVisible = false;
            currentPool = ModelSavePool.Player;
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
                currentPool,
                saveService,
                LocalizedText.GetOrFallback(GameTextKeys.CommonEmpty, emptySlotLabel),
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
            ModelSaveSlot slot = saveService.GetSlot(currentPool, slotIndex);
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

            ModelSaveSlot slot = saveService.GetSlot(currentPool, selectedSlot);
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

            saveService.DeleteSlot(currentPool, selectedSlot);
            selectedSlot = -1;
            loadConfirmView?.Clear();
            SetConfirmVisible(false);
            RefreshSlots();
        }

        private async UniTaskVoid LoadAndImportAsync(int slotIndex, CancellationToken cancellationToken)
        {
            ModelSavePool pool = currentPool;
            ModelSaveSlot slot = saveService.GetSlot(pool, slotIndex);
            if (slot == null)
            {
                Debug.LogWarning("[ClayEditRemakeLoadSlotView] スロットにモデルデータがありません");
                return;
            }

            isLoading = true;
            GameObject importHolder = null;
            bool completed = false;
            try
            {
                // 変換中にUIを消すと背景だけに見えるので確認パネルだけ閉じる
                SetConfirmVisible(false);
                SetSelectionInteractable(false);

                string voxelFileName = ModelSavePoolSettings.GetVoxelFileName(pool, slotIndex);
                bool voxelExists = ModelSaveStorage.Exists(voxelFileName);
                Debug.Log(
                    $"[ClayEditRemakeLoadSlotView] 作り直し開始 pool={pool} slot={slotIndex}"
                    + $" name={slot.modelName} voxelExists={voxelExists} file={voxelFileName}");

                // 敵は過去の誤変換voxelが残るとサイズ色が壊れたままになるため常にGLBから再変換する
                // 未育成プレイヤーは従来どおりvoxelスナップショットを優先する
                bool preferSnapshot = pool != ModelSavePool.Enemy;
                if (preferSnapshot)
                {
                    (bool snapshotSuccess, string snapshotError) = await savedModelImporter.TryImportFromSnapshotAsync(
                        pool,
                        slotIndex,
                        cancellationToken);
                    if (snapshotSuccess)
                    {
                        if (!HasRestoredMesh())
                        {
                            Debug.LogError(
                                "[ClayEditRemakeLoadSlotView] ボクセル復元後にメッシュがありません",
                                this);
                            return;
                        }

                        CompleteRemake(slotIndex, slot.modelName);
                        completed = true;
                        return;
                    }

                    Debug.LogWarning(
                        $"[ClayEditRemakeLoadSlotView] ボクセルスナップショット復元に失敗しました: {snapshotError}"
                        + " GLBからの再変換を試みます");
                }
                else
                {
                    Debug.Log(
                        "[ClayEditRemakeLoadSlotView] 敵スロットはGLBからボクセル化します(スナップショットは使いません)");
                }

                if (string.IsNullOrEmpty(slot.glbFileName))
                {
                    Debug.LogError("[ClayEditRemakeLoadSlotView] glbファイル名が空です");
                    return;
                }

                string filePath = ModelSaveStorage.ResolveReadPath(slot.glbFileName);
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    Debug.LogError($"[ClayEditRemakeLoadSlotView] glbファイルが見つかりません: {slot.glbFileName}");
                    return;
                }

                // シーンに出さず非アクティブの一時親へ取り込む
                importHolder = new GameObject("ClayEditRemakeImportHolder");
                importHolder.SetActive(false);
                GameObject imported = await importer.ImportFromGlbAsync(
                    filePath,
                    importHolder.transform,
                    cancellationToken);
                if (imported == null)
                {
                    Debug.LogError("[ClayEditRemakeLoadSlotView] モデルのロードに失敗しました");
                    return;
                }

                SetImportedRenderersEnabled(imported, false);
                imported.transform.localPosition = Vector3.zero;
                // 敵GLBの向きを造形空間に合わせる
                imported.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                imported.transform.localScale = Vector3.one;

                // 変換用に一時有効化するが表示はレンダラー無効のまま
                importHolder.SetActive(true);
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);

                Debug.Log($"[ClayEditRemakeLoadSlotView] GLBボクセル化開始 slot={slotIndex}");
                (bool importSuccess, string errorMessage) = await savedModelImporter.TryImportFromLoadedModelAsync(
                    imported,
                    cancellationToken);
                if (!importSuccess)
                {
                    Debug.LogError($"[ClayEditRemakeLoadSlotView] ボクセル化に失敗しました: {errorMessage}");
                    return;
                }

                PersistVoxelSnapshot(pool, slotIndex);
                if (!HasRestoredMesh())
                {
                    Debug.LogError(
                        "[ClayEditRemakeLoadSlotView] ボクセル化後にメッシュがありません",
                        this);
                    return;
                }

                Debug.Log($"[ClayEditRemakeLoadSlotView] GLBボクセル化完了 slot={slotIndex}");
                CompleteRemake(slotIndex, slot.modelName);
                completed = true;
            }
            catch (System.OperationCanceledException)
            {
                Debug.LogWarning("[ClayEditRemakeLoadSlotView] 作り直しがキャンセルされました");
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError($"[ClayEditRemakeLoadSlotView] 作り直し中に例外: {exception.Message}", this);
            }
            finally
            {
                if (importHolder != null)
                {
                    Destroy(importHolder);
                }

                RestoreEditCamera();
                isLoading = false;

                if (!completed && isSelectionVisible)
                {
                    SetSelectionInteractable(true);
                    SetCanvasEnabled(GetComponent<Canvas>(), true, SelectionCanvasSortingOrder);
                    SetConfirmVisible(selectedSlot >= 0);
                }
            }
        }

        private void SetSelectionInteractable(bool interactable)
        {
            SetSelectionRaycastsEnabled(interactable);
            if (slotScrollList != null)
            {
                CanvasGroup listGroup = slotScrollList.GetComponent<CanvasGroup>();
                if (listGroup != null)
                {
                    listGroup.interactable = interactable;
                    listGroup.blocksRaycasts = interactable;
                }
            }
        }

        private void CompleteRemake(int slotIndex, string modelName)
        {
            RestoreEditCamera();
            remakeConfirmedSubject.OnNext(new ClayEditRemakeSelection(slotIndex, modelName, currentPool));
        }

        private void PersistVoxelSnapshot(ModelSavePool pool, int slotIndex)
        {
            if (voxelEngine == null || !voxelEngine.HasMesh())
            {
                return;
            }

            string voxelFilePath = Path.Combine(
                Application.persistentDataPath,
                ModelSavePoolSettings.GetVoxelFileName(pool, slotIndex));
            if (!ClayVoxelSnapshotFile.TryWrite(
                    voxelFilePath,
                    voxelEngine.GetVoxelData(),
                    voxelEngine.GetVoxelColors(),
                    voxelEngine.size,
                    voxelEngine.boundsSize))
            {
                Debug.LogWarning(
                    $"[ClayEditRemakeLoadSlotView] 作り直し用ボクセルスナップショットの保存に失敗しました slot={slotIndex}");
            }
        }

        private static void RestoreEditCamera()
        {
            // 本格的な編集カメラ復帰はClayEditPresenter側で行う
            UnityEngine.Camera mainCamera = UnityEngine.Camera.main;
            if (mainCamera != null && !mainCamera.enabled)
            {
                mainCamera.enabled = true;
            }
        }

        private bool HasRestoredMesh()
        {
            return voxelEngine != null && voxelEngine.HasMesh();
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

            ModelSaveSlot slot = saveService.GetSlot(currentPool, slotIndex);
            if (slot == null)
            {
                return;
            }

            loadConfirmView.ShowSlot(slot, ModelSaveStorage.ReadThumbnailPng(slot), slotIndex);
        }

        private static bool CanSelectPool(ModelSavePool pool)
        {
            if (pool == ModelSavePool.Player)
            {
                return true;
            }

            return pool == ModelSavePool.Enemy && EnemySaveAvailability.IsAvailable;
        }

#if UNITY_EDITOR
        private void OnGUI()
        {
            if (!isSelectionVisible || isLoading || !Application.isPlaying)
            {
                return;
            }

            if (!EnemySaveAvailability.IsAvailable)
            {
                return;
            }

            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null || !canvas.enabled)
            {
                return;
            }

            const float width = 280f;
            const float height = 34f;
            var area = new Rect(16f, 16f, width, height);
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label("作り直し元", GUILayout.Width(72f));
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = currentPool == ModelSavePool.Player
                ? new Color(0.95f, 0.85f, 0.35f, 1f)
                : Color.white;
            if (GUILayout.Button("未育成"))
            {
                pendingPoolSwitch = ModelSavePool.Player;
            }

            GUI.backgroundColor = currentPool == ModelSavePool.Enemy
                ? new Color(0.95f, 0.85f, 0.35f, 1f)
                : Color.white;
            if (GUILayout.Button("敵"))
            {
                pendingPoolSwitch = ModelSavePool.Enemy;
            }

            GUI.backgroundColor = previous;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void SwitchPool(ModelSavePool pool)
        {
            if (!CanSelectPool(pool) || currentPool == pool || isLoading)
            {
                return;
            }

            currentPool = pool;
            selectedSlot = -1;
            loadConfirmView?.Clear();
            SetConfirmVisible(false);

            Canvas canvas = GetComponent<Canvas>();
            CanvasVisibilityUtility.SetCanvasEnabled(canvas, true, SelectionCanvasSortingOrder);
            gameObject.SetActive(true);
            isSelectionVisible = true;
            ApplySelectionCanvasSorting();
            ModelSaveSlotScrollListView.ApplySelectionCanvasLayout(canvas);
            RefreshSlots();
        }
#endif

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

            // spawnParentは旧実装のGLB直出し先で現在は未使用(シーン参照を維持する)
            _ = spawnParent;
        }

        private void OnDestroy()
        {
            ClearRuntimeThumbnails();
            remakeConfirmedSubject.Dispose();
            cancelledSubject.Dispose();
        }
    }
}
