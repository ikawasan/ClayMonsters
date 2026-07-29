using Battle.Interface;
using Cysharp.Threading.Tasks;
using Extensions;
using LighthouseExtends.UIComponent.Button;
using R3;
using SaveData;
using SaveData.Interface;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using VContainer;

namespace UI.ClayEditor.View
{
    /// <summary>
    /// プレイヤー用セーブデータから1つを選んでモデルをロードするView。
    /// 育成済みプールでは5×10の名前とサムネイルボタン一覧を表示し選択後に確認Canvasでステータスを表示する。
    /// 「ロードする」でglbを読み込み、「戻る」で確認Canvasを閉じる。
    /// </summary>
    public class LoadSlotView : MonoBehaviour, IMonsterSelection
    {
        private const int SelectionCanvasSortingOrder = 500;
        private const int ConfirmCanvasSortingOrder = SelectionCanvasSortingOrder + 1;
        private const string PlayerSelectionInstruction =
            "あなたのモンスターを選択してください";
        private const string EnemySelectionInstruction =
            "敵のモンスターを選択してください";

        [Inject] private readonly IClayModelSaveService saveService;
        [Inject] private readonly IClayModelImporter importer;
        [Inject] private readonly IBattleCanvasTransition canvasTransition;
        [Inject] private readonly INpcBattleProgressService npcBattleProgress;

        [Header("セーブスロット選択")]
        [Tooltip("横長のスクロールスロット一覧")]
        [SerializeField] private ModelSaveSlotScrollListView slotScrollList;

        [Tooltip("育成済み用5×10グリッド一覧")]
        [SerializeField] private TrainedSaveSlotGridView trainedSlotGrid;

        [Tooltip("選択画面上部の案内テキスト")]
        [SerializeField] private TMP_Text selectionInstructionText;

        [Tooltip("全画面入力ブロッカー背景。未設定ならブロッカー設定をスキップする")]
        [SerializeField] private Image inputBlocker;

        [Tooltip("データが無い(空き)スロットに表示する文言")]
        [SerializeField] private string emptySlotLabel = "空き";

        [Header("確認パネル")]
        [Tooltip("ロード確認用パネル")]
        [FormerlySerializedAs("confirmCanvas")]
        [SerializeField] private GameObject confirmPanelRoot;

        [Tooltip("スロット選択Canvas。未設定時は同一GameObjectのCanvasを使う")]
        [FormerlySerializedAs("rootGroup")]
        [SerializeField] private Canvas selectionCanvas;

        [Tooltip("ロード確認で選択スロットを表示するView")]
        [SerializeField] private ModelSaveConfirmView loadConfirmView;

        [Tooltip("敵選択確認で強さを切り替えるUI")]
        [SerializeField] private EnemyStrengthSelectView confirmStrengthSelect;

        [Tooltip("「ロードする」ボタン")]
        [SerializeField] private LHButton loadButton;

        [Tooltip("「戻る」ボタン")]
        [SerializeField] private LHButton backButton;

        [Header("ロード設定")]
        [Tooltip("表示・ロード対象のセーブプール")]
        [SerializeField] private ModelSavePool savePool = ModelSavePool.Player;

        [Tooltip("読み込んだモデルの親Transform(未指定ならルートに生成)")]
        [SerializeField] private Transform spawnParent;

        [Tooltip("読み込んだモデルへ適用するマテリアル(ClayEditで使うCustom/ClayMonster)")]
        [SerializeField] private Material clayMaterial;

        // ロード完了通知(読み込んだモデルのルートを渡す)。シーン側が購読して次の処理へ進める
        private readonly Subject<GameObject> loadedSubject = new Subject<GameObject>();

        /// <summary>
        /// スロット選択UIの表示制御
        /// </summary>
        public Canvas SelectionCanvas => selectionCanvas;

        /// <inheritdoc />
        public ModelSavePool SavePool => savePool;

        /// <inheritdoc />
        public Observable<GameObject> OnModelLoaded => loadedSubject;

        /// <summary>
        /// 現在読み込まれているモデルのルート。未ロードならnull
        /// </summary>
        public GameObject LoadedModel { get; private set; }

        /// <summary>
        /// 直近にロードしたスロット番号(未ロードは-1)。
        /// </summary>
        public int LoadedSlotIndex { get; private set; } = -1;

        /// <summary>
        /// 敵選択確認で選ばれた強さ段階
        /// </summary>
        public EnemyStrengthTier SelectedStrengthTier { get; private set; } = EnemyStrengthTier.Normal;

        /// <inheritdoc />
        public int SelectedSlotIndex => LoadedSlotIndex;

        /// <inheritdoc />
        public void RestoreAfterParticipantFailure()
        {
            ClearLoadedModelReference();
            ShowSelectionUi();
            Refresh();
        }

        /// <summary>
        /// 読み込み済みモデルの管理権を呼び出し側へ渡す
        /// </summary>
        public GameObject DetachLoadedModel()
        {
            GameObject model = LoadedModel;
            LoadedModel = null;
            LoadedSlotIndex = -1;
            return model;
        }

        /// <summary>
        /// シーン退場や再入場前に選択UIと保持モデルを破棄する
        /// </summary>
        public void HideForLeave()
        {
            DestroyOwnedLoadedModel();
            selectedSlot = -1;
            isLoading = false;
            SelectedStrengthTier = EnemyStrengthTier.Normal;
            confirmStrengthSelect?.Hide();
            loadConfirmView?.Clear();
            SetConfirmPanelActive(false);
            HideSelectionUi();
        }

        /// <summary>
        /// 新しい選択待ちに入る前に前回ロード結果を破棄する
        /// </summary>
        public void ClearLoadedModelForNewSelection()
        {
            DestroyOwnedLoadedModel();
            selectedSlot = -1;
            isLoading = false;
            SelectedStrengthTier = EnemyStrengthTier.Normal;
            confirmStrengthSelect?.Hide();
            loadConfirmView?.Clear();
        }

        // 表示用に生成したサムネイル(Texture2D / Sprite)。再表示・破棄時にまとめてDestroyする
        private readonly List<UnityEngine.Object> runtimeThumbnailObjects = new List<UnityEngine.Object>();

        // 現在選択中のスロット番号(未選択は-1)
        private int selectedSlot = -1;

        private bool isLoading;

        private bool isInitialized;

        /// <summary>
        /// 表示・ロード対象のセーブプールを切り替える
        /// </summary>
        /// <param name="pool">対象プール</param>
        /// <param name="emptyLabel">空スロット表示文言(省略時は現状維持)</param>
        public void ConfigureSavePool(ModelSavePool pool, string emptyLabel = null)
        {
            savePool = pool;
            if (!string.IsNullOrEmpty(emptyLabel))
            {
                emptySlotLabel = emptyLabel;
            }

            ApplySelectionInstructionText();
            if (isInitialized)
            {
                RefreshSlots();
            }
        }

        /// <summary>
        /// 選択UIをscale=0親Canvas配下からシーンルートへ移して表示可能にする
        /// </summary>
        public void DetachSelectionUiToSceneRoot(Transform sceneRoot)
        {
            if (sceneRoot == null)
            {
                return;
            }

            Canvas selection = GetComponent<Canvas>();
            if (selection != null)
            {
                if (selection.transform.parent != sceneRoot)
                {
                    selection.transform.SetParent(sceneRoot, false);
                }

                selection.renderMode = RenderMode.ScreenSpaceOverlay;
                selection.worldCamera = null;
                selection.overrideSorting = true;
                selection.sortingOrder = SelectionCanvasSortingOrder;
                ModelSaveSlotScrollListView.FixCanvasScaleHierarchy(selection);
                selection.transform.SetAsLastSibling();
            }

            // Battle系はConfirmSaveSlotCanvasが兄弟配置のため選択と同じ親へ移す
            DetachSiblingConfirmPanelTo(sceneRoot);
            EnsureRootCanvasEnabled();
        }

        /// <summary>
        /// スロット選択UI全体を非表示にする
        /// GameObjectは無効化しない
        /// </summary>
        public void HideSelectionUi()
        {
            EnsureSelectionCanvas();
            confirmStrengthSelect?.Hide();
            SetConfirmPanelActive(false);
            trainedSlotGrid?.Hide();
            CanvasVisibilityUtility.SetCanvasEnabled(selectionCanvas, false);
        }

        /// <summary>
        /// スロット選択UIを再表示する
        /// </summary>
        public void ShowSelectionUi()
        {
            EnsureSelectionCanvas();
            SetConfirmPanelActive(false);
            EnsureRootCanvasEnabled();
            SetSelectionContentVisible(true);
        }

        /// <summary>
        /// 選択UIの初期化とスロット一覧更新を確実に行う
        /// </summary>
        public void EnsureSelectionReady()
        {
            RestoreSelectionInteractable();
            ApplySelectionInstructionText();

            if (UsesTrainedSlotGrid())
            {
                EnsureSelectionInitializedForTrainedGrid();
                RefreshSlots();
                Debug.Log(
                    "[LoadSlotView] EnsureSelectionReady完了"
                    + $" saveService={(saveService != null)}"
                    + " mode=TrainedSlotGrid");
                return;
            }

            EnsureSlotScrollList();
            if (slotScrollList == null)
            {
                Debug.LogError("[LoadSlotView] slotScrollListが見つかりません");
                return;
            }

            if (!isInitialized)
            {
                EnsureLoadConfirmButtons();

                slotScrollList.Initialize(OnSlotSelected);

                if (loadButton != null)
                {
                    loadButton.SubscribeOnClick(OnLoadConfirmed);
                }

                if (backButton != null)
                {
                    backButton.SubscribeOnClick(OnBack);
                }

                SetConfirmPanelActive(false);
                isInitialized = true;
            }

            if (!TryRefreshWhenHostReady())
            {
                Debug.LogWarning("[LoadSlotView] スロット一覧のホストレイアウト未確定のため更新を延期します");
                return;
            }

            slotScrollList.GetLayoutMetrics(
                out int builtRows,
                out float hostWidth,
                out float hostHeight,
                out float viewportHeight,
                out float contentHeight);
            Debug.Log(
                "[LoadSlotView] EnsureSelectionReady完了"
                + $" saveService={(saveService != null)}"
                + $" builtRows={builtRows}"
                + $" host=({hostWidth:F0},{hostHeight:F0})"
                + $" viewportH={viewportHeight:F0}"
                + $" contentH={contentHeight:F0}");
        }

        private bool TryRefreshWhenHostReady()
        {
            if (UsesTrainedSlotGrid())
            {
                RefreshSlots();
                return true;
            }

            EnsureSlotScrollList();
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
                return false;
            }

            RefreshSlots();
            return true;
        }

        private void EnsureSelectionInitializedForTrainedGrid()
        {
            if (isInitialized)
            {
                return;
            }

            EnsureLoadConfirmButtons();
            if (EnsureTrainedSlotGrid())
            {
                trainedSlotGrid.Initialize(OnSlotSelected);
            }

            if (loadButton != null)
            {
                loadButton.SubscribeOnClick(OnLoadConfirmed);
            }

            if (backButton != null)
            {
                backButton.SubscribeOnClick(OnBack);
            }

            SetConfirmPanelActive(false);
            isInitialized = true;
        }

        private bool UsesTrainedSlotGrid()
        {
            return (savePool == ModelSavePool.TrainedPlayer || savePool == ModelSavePool.Enemy)
                && EnsureTrainedSlotGrid();
        }

        // 画面が表示されるたびに最新のセーブ内容を読み直す。
        private void OnEnable()
        {
            ApplySelectionCanvasSorting();
            if (isInitialized)
            {
                TryRefreshWhenHostReady();
            }
        }

        /// <inheritdoc />
        public void Refresh()
        {
            RestoreSelectionInteractable();
            RefreshSlots();
        }

        /// <inheritdoc />
        public void PrepareForSelectionWait()
        {
            ClearLoadedModelForNewSelection();
            SetConfirmPanelActive(false);
            ApplySelectionInstructionText();
            SetSelectionContentVisible(true);
            PrepareLayout();
            EnsureSelectionInputEnabled();
            RefreshSlots();

            if (isInitialized && slotScrollList != null)
            {
                slotScrollList.Initialize(OnSlotSelected);
                slotScrollList.ForceSelectionLayout();
                slotScrollList.EnsureClickBinding();
            }
        }

        /// <inheritdoc />
        public void EnsureSelectionInputEnabled()
        {
            EnsureSelectionCanvas();
            EnsureRootCanvasEnabled();
        }

        /// <summary>
        /// 再戦などで選択UIを再表示するとき入力状態を復元する
        /// </summary>
        private void RestoreSelectionInteractable()
        {
            ClearLoadedModelReference();
            selectedSlot = -1;
            isLoading = false;
            loadConfirmView?.Clear();
            ShowSelectionUi();
        }

        private void DestroyOwnedLoadedModel()
        {
            if (LoadedModel != null)
            {
                Destroy(LoadedModel);
            }

            ClearLoadedModelReference();
        }

        private void ClearLoadedModelReference()
        {
            LoadedModel = null;
            LoadedSlotIndex = -1;
        }

        /// <summary>
        /// 明転前に全画面背景付きで選択UIのレイアウトだけ整える
        /// </summary>
        public void PrepareLayout(bool reparentToUiRoot = false)
        {
            if (!UsesTrainedSlotGrid())
            {
                EnsureSlotScrollList();
            }

            ApplySelectionCanvasSorting();
            EnsureInputBlockerConfigured();
            Canvas canvas = GetComponent<Canvas>();
            if (canvas != null)
            {
                ModelSaveSlotScrollListView.ApplySelectionCanvasLayout(canvas);
            }

            slotScrollList?.RefreshHostLayout();
        }

        /// <summary>
        /// 明転前に全画面背景付きで選択UIを表示する
        /// </summary>
        public void PrepareForDisplay(bool reparentToUiRoot = false)
        {
            PrepareLayout(reparentToUiRoot);
            EnsureRootCanvasEnabled();
        }

        // 各スロットの表示を更新する。データがあれば名前とサムネイル、無ければ空き表示・選択不可にする
        private void RefreshSlots()
        {
            if (saveService == null)
            {
                Debug.LogError("[LoadSlotView] saveService未注入のためスロット内容を更新できません");
                return;
            }

            if (savePool == ModelSavePool.TrainedPlayer || savePool == ModelSavePool.Enemy)
            {
                if (EnsureTrainedSlotGrid())
                {
                    SetScrollListVisible(false);
                    ClearRuntimeThumbnails();
                    trainedSlotGrid.Show();
                    trainedSlotGrid.Initialize(OnSlotSelected);
                    trainedSlotGrid.Refresh(
                        saveService,
                        savePool,
                        emptySlotLabel,
                        allowEmptySlotSelection: false,
                        isSlotUnlocked: ResolveEnemySlotUnlockPredicate());
                    return;
                }

                if (savePool == ModelSavePool.TrainedPlayer)
                {
                    Debug.LogError(
                        "[LoadSlotView] trainedSlotGridが未配置ですResources/UI/TrainedSaveSlotGridをHierarchyへ配置してください",
                        this);
                }
            }
            else
            {
                trainedSlotGrid?.Hide();
                SetScrollListVisible(true);
            }

            EnsureSlotScrollList();
            if (slotScrollList == null)
            {
                return;
            }

            ClearRuntimeThumbnails();
            slotScrollList.RefreshSlots(
                savePool,
                saveService,
                emptySlotLabel,
                runtimeThumbnailObjects,
                allowEmptySlotSelection: false,
                TrainedSaveSlotListPresentation.ResolveContentMode(savePool));
            slotScrollList.RefreshHostLayout();
        }

        private bool EnsureTrainedSlotGrid()
        {
            if (trainedSlotGrid != null)
            {
                return true;
            }

            trainedSlotGrid = GetComponentInChildren<TrainedSaveSlotGridView>(true);
            return trainedSlotGrid != null;
        }

        private void SetScrollListVisible(bool visible)
        {
            if (slotScrollList == null)
            {
                return;
            }

            if (slotScrollList.gameObject.activeSelf != visible)
            {
                slotScrollList.gameObject.SetActive(visible);
            }
        }

        // スロットが選択されたとき: 確認キャンバスを開く
        private void OnSlotSelected(int slotIndex)
        {
            if (savePool == ModelSavePool.Enemy && !IsEnemySlotSelectable(slotIndex))
            {
                Debug.LogWarning($"[LoadSlotView] スロット{slotIndex}は未開放です");
                return;
            }

            ModelSaveSlot slot = saveService.GetSlot(savePool, slotIndex);
            if (slot == null || string.IsNullOrEmpty(slot.glbFileName))
            {
                Debug.LogWarning($"[LoadSlotView] スロット{slotIndex}はロード不可です");
                return;
            }

            selectedSlot = slotIndex;
            if (!IsConfirmUiReady())
            {
                Debug.LogWarning(
                    "[LoadSlotView] ConfirmPanel未配置のため確認をスキップしてロードします",
                    this);
                LoadAsync(slotIndex, this.GetCancellationTokenOnDestroy()).Forget();
                return;
            }

            OpenConfirmAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        private async UniTask OpenConfirmAsync(CancellationToken cancellationToken)
        {
            if (!IsConfirmUiReady())
            {
                Debug.LogError(
                    "[LoadSlotView] ConfirmPanelが未配置です。LoadSlotView配下のHierarchyを確認してください",
                    this);
                return;
            }

            if (canvasTransition != null)
            {
                await canvasTransition.FadeOutAsync(cancellationToken);
            }

            EnsureConfirmPanelInFront();
            SetSelectionContentVisible(false);
            SetConfirmPanelActive(true);
            ModelSaveSlotScrollListView.EnsureSelectionBackground(
                confirmPanelRoot != null ? confirmPanelRoot.transform : null);
            RefreshLoadConfirm(selectedSlot);
            if (confirmPanelRoot != null)
            {
                confirmPanelRoot.transform.SetAsLastSibling();
            }

            if (canvasTransition != null)
            {
                await canvasTransition.FadeInAsync(cancellationToken);
            }
        }

        // 戻るボタンが押されたとき: 確認キャンバスを閉じる
        private void OnBack()
        {
            CloseConfirmAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        private async UniTask CloseConfirmAsync(CancellationToken cancellationToken)
        {
            if (canvasTransition != null)
            {
                await canvasTransition.FadeOutAsync(cancellationToken);
            }

            selectedSlot = -1;
            confirmStrengthSelect?.Hide();
            loadConfirmView?.Clear();
            SetConfirmPanelActive(false);
            SetSelectionContentVisible(true);

            if (canvasTransition != null)
            {
                await canvasTransition.FadeInAsync(cancellationToken);
            }
        }

        // ロードするボタンが押されたとき: 選択中スロットのglbを読み込む
        private void OnLoadConfirmed()
        {
            if (isLoading)
            {
                return;
            }

            if (selectedSlot < 0)
            {
                Debug.LogWarning("[LoadSlotView] ロード対象スロットが選択されていません");
                return;
            }

            LoadAsync(selectedSlot, this.GetCancellationTokenOnDestroy()).Forget();
        }

        private async UniTaskVoid LoadAsync(int slotIndex, CancellationToken cancellationToken)
        {
            if (savePool == ModelSavePool.Enemy)
            {
                if (!IsEnemySlotSelectable(slotIndex))
                {
                    Debug.LogWarning($"[LoadSlotView] 未開放の敵スロットです slot={slotIndex}");
                    return;
                }

                if (!IsEnemyStrengthSelectable(slotIndex, SelectedStrengthTier))
                {
                    Debug.LogWarning(
                        "[LoadSlotView] 未開放の強さです"
                        + $" slot={slotIndex}"
                        + $" tier={SelectedStrengthTier}");
                    return;
                }
            }

            ModelSaveSlot slot = saveService.GetSlot(savePool, slotIndex);
            if (slot == null || string.IsNullOrEmpty(slot.glbFileName))
            {
                Debug.LogWarning("[LoadSlotView] スロットにモデルデータがありません");
                return;
            }

            string filePath = ModelSaveStorage.ResolveReadPath(slot.glbFileName);
            if (!File.Exists(filePath))
            {
                Debug.LogError($"[LoadSlotView] glbファイルが見つかりません: {filePath}");
                return;
            }

            isLoading = true;
            try
            {
                if (canvasTransition != null)
                {
                    await canvasTransition.FadeOutAsync(cancellationToken);
                }

                if (LoadedModel != null)
                {
                    Destroy(LoadedModel);
                    LoadedModel = null;
                }

                GameObject imported = await importer.ImportFromGlbAsync(filePath, spawnParent, cancellationToken);
                if (imported == null)
                {
                    Debug.LogError("[LoadSlotView] モデルのロードに失敗しました");
                    await RestoreSelectionAfterLoadFailureAsync(cancellationToken);
                    return;
                }

                ApplyClayMaterial(imported);

                LoadedModel = imported;
                LoadedSlotIndex = slotIndex;
                Debug.Log($"[LoadSlotView] スロット{slotIndex}のモデルをロードしました: {slot.modelName}");

                SetConfirmPanelActive(false);
                SetSelectionContentVisible(true);
                selectedSlot = -1;
                loadConfirmView?.Clear();
                HideSelectionUi();

                loadedSubject.OnNext(imported);
            }
            finally
            {
                isLoading = false;
            }
        }

        private async UniTask RestoreSelectionAfterLoadFailureAsync(CancellationToken cancellationToken)
        {
            if (canvasTransition != null)
            {
                await canvasTransition.FadeInAsync(cancellationToken);
            }

            RestoreSelectionInteractable();
        }

        // 読み込んだモデル配下のレンダラーへ ClayEditと同じマテリアルを適用する
        private void ApplyClayMaterial(GameObject importedRoot)
        {
            if (clayMaterial == null || importedRoot == null)
            {
                return;
            }

            var skinnedRenderers = importedRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skinnedRenderers.Length; i++)
            {
                skinnedRenderers[i].sharedMaterial = clayMaterial;
            }

            var meshRenderers = importedRoot.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < meshRenderers.Length; i++)
            {
                meshRenderers[i].sharedMaterial = clayMaterial;
            }
        }

        private void EnsureLoadConfirmView()
        {
            if (loadConfirmView != null)
            {
                return;
            }

            Debug.LogError(
                "[LoadSlotView] loadConfirmViewが未設定です。Editor Wireツールで参照を配線してください",
                this);
        }

        private void RefreshLoadConfirm(int slotIndex)
        {
            EnsureLoadConfirmView();
            if (loadConfirmView == null || saveService == null)
            {
                return;
            }

            ModelSaveSlot slot = saveService.GetSlot(savePool, slotIndex);
            if (slot == null)
            {
                confirmStrengthSelect?.Hide();
                return;
            }

            byte[] thumbnailPng = ModelSaveStorage.ReadThumbnailPng(slot);
            if (savePool == ModelSavePool.Enemy)
            {
                EnsureConfirmStrengthSelect();
                EnemyStrengthTier preferredTier = npcBattleProgress != null
                    ? npcBattleProgress.GetHighestUnlockedStrength(slotIndex)
                    : EnemyStrengthTier.Weak;
                if (confirmStrengthSelect != null)
                {
                    confirmStrengthSelect.Show(
                        preferredTier,
                        OnConfirmStrengthSelected,
                        tier => IsEnemyStrengthSelectable(slotIndex, tier));
                    SelectedStrengthTier = confirmStrengthSelect.CurrentTier;
                }
                else
                {
                    SelectedStrengthTier = preferredTier;
                }

                ModelStatus status = EnemyStrengthStatusCatalog.Resolve(slot, SelectedStrengthTier);
                loadConfirmView.ShowSlot(slot, thumbnailPng, slotIndex, status);
                return;
            }

            confirmStrengthSelect?.Hide();
            loadConfirmView.ShowSlot(slot, thumbnailPng, slotIndex);
        }

        private void OnConfirmStrengthSelected(EnemyStrengthTier tier)
        {
            if (savePool == ModelSavePool.Enemy
                && selectedSlot >= 0
                && !IsEnemyStrengthSelectable(selectedSlot, tier))
            {
                return;
            }

            SelectedStrengthTier = tier;
            if (selectedSlot < 0 || loadConfirmView == null || saveService == null)
            {
                return;
            }

            ModelSaveSlot slot = saveService.GetSlot(savePool, selectedSlot);
            if (slot == null)
            {
                return;
            }

            ModelStatus status = EnemyStrengthStatusCatalog.Resolve(slot, tier);
            loadConfirmView.ShowSlot(
                slot,
                ModelSaveStorage.ReadThumbnailPng(slot),
                selectedSlot,
                status);
        }

        private Func<int, bool> ResolveEnemySlotUnlockPredicate()
        {
            if (savePool != ModelSavePool.Enemy)
            {
                return null;
            }

            if (npcBattleProgress == null)
            {
                Debug.LogError(
                    "[LoadSlotView] npcBattleProgressが未注入のため敵スロットを全て選択不可にします",
                    this);
                return _ => false;
            }

            return npcBattleProgress.IsEnemySlotUnlocked;
        }

        private bool IsEnemySlotSelectable(int slotIndex)
        {
            if (npcBattleProgress == null)
            {
                return false;
            }

            return npcBattleProgress.IsEnemySlotUnlocked(slotIndex);
        }

        private bool IsEnemyStrengthSelectable(int slotIndex, EnemyStrengthTier tier)
        {
            if (npcBattleProgress == null)
            {
                return false;
            }

            return npcBattleProgress.IsEnemyStrengthUnlocked(slotIndex, tier);
        }

        private void EnsureConfirmStrengthSelect()
        {
            if (confirmStrengthSelect != null)
            {
                return;
            }

            if (confirmPanelRoot != null)
            {
                confirmStrengthSelect = confirmPanelRoot.GetComponentInChildren<EnemyStrengthSelectView>(true);
            }

            if (confirmStrengthSelect == null)
            {
                Debug.LogError(
                    "[LoadSlotView] confirmStrengthSelectが未配線ですConfirmSaveSlotCanvasへEnemyStrengthSelectを配置し接続してください",
                    this);
            }
        }

        private void EnsureSlotScrollList()
        {
            if (slotScrollList != null || UsesTrainedSlotGrid())
            {
                return;
            }

            Debug.LogError(
                "[LoadSlotView] slotScrollListが未設定ですHierarchyで接続してください",
                this);
        }

        private void ApplySelectionCanvasSorting()
        {
            CanvasVisibilityUtility.ApplyOverrideSorting(GetComponent<Canvas>(), SelectionCanvasSortingOrder);
        }

        /// <summary>
        /// 確認パネルを選択UIより前面に配置する
        /// TrainingはLoadSlotView直下ConfirmPanel
        /// Battle系は兄弟ConfirmSaveSlotCanvasを選択と同じ親へ揃える
        /// </summary>
        private void EnsureConfirmPanelInFront()
        {
            EnsureConfirmPanelRoot();
            if (confirmPanelRoot == null)
            {
                return;
            }

            Transform confirmTransform = confirmPanelRoot.transform;
            if (confirmTransform.parent == transform)
            {
                confirmTransform.SetAsLastSibling();
                return;
            }

            Transform targetParent = transform.parent != null ? transform.parent : transform.root;
            DetachSiblingConfirmPanelTo(targetParent);
            confirmTransform.SetAsLastSibling();
        }

        /// <summary>
        /// LoadSlotViewの子でない確認Canvasを表示可能な親へ移す
        /// </summary>
        private void DetachSiblingConfirmPanelTo(Transform targetParent)
        {
            EnsureConfirmPanelRoot();
            if (confirmPanelRoot == null || targetParent == null)
            {
                return;
            }

            Transform confirmTransform = confirmPanelRoot.transform;
            if (confirmTransform.parent == transform)
            {
                return;
            }

            if (confirmTransform.parent != targetParent)
            {
                confirmTransform.SetParent(targetParent, false);
            }

            Canvas confirmCanvas = confirmPanelRoot.GetComponent<Canvas>();
            if (confirmCanvas == null)
            {
                return;
            }

            confirmCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            confirmCanvas.worldCamera = null;
            confirmCanvas.overrideSorting = true;
            confirmCanvas.sortingOrder = ConfirmCanvasSortingOrder;
            ModelSaveSlotScrollListView.FixCanvasScaleHierarchy(confirmCanvas);
        }

        private void SetSelectionContentVisible(bool visible)
        {
            Transform confirmRoot = confirmPanelRoot != null ? confirmPanelRoot.transform : null;
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (confirmRoot != null && child == confirmRoot)
                {
                    continue;
                }

                if (inputBlocker != null && child == inputBlocker.transform)
                {
                    TitleClayUiVisualUtility.ConfigureInputBlocker(inputBlocker, visible);
                    continue;
                }

                CanvasVisibilityUtility.SetUiVisible(child.gameObject, visible);
            }
        }

        private void EnsureInputBlockerConfigured()
        {
            if (inputBlocker == null)
            {
                return;
            }

            TitleClayUiVisualUtility.ApplyBlocker(inputBlocker);
        }

        private void EnsureSelectionCanvas()
        {
            if (selectionCanvas == null)
            {
                selectionCanvas = GetComponent<Canvas>();
            }
        }

        private void ApplySelectionInstructionText()
        {
            if (selectionInstructionText == null)
            {
                // 育成など対戦以外では任意参照
                return;
            }

            // 対戦の育成済み選択と敵選択のみ案内文を出す
            bool showInstruction = savePool == ModelSavePool.TrainedPlayer
                || savePool == ModelSavePool.Enemy;
            if (!showInstruction)
            {
                selectionInstructionText.enabled = false;
                return;
            }

            selectionInstructionText.text = savePool == ModelSavePool.Enemy
                ? EnemySelectionInstruction
                : PlayerSelectionInstruction;
            TitleClayUiVisualUtility.EnsureTextFontOnly(selectionInstructionText);
            if (!selectionInstructionText.gameObject.activeSelf)
            {
                selectionInstructionText.gameObject.SetActive(true);
            }

            selectionInstructionText.enabled = true;
        }

        private void SetConfirmPanelActive(bool visible)
        {
            EnsureConfirmPanelRoot();
            if (confirmPanelRoot == null)
            {
                return;
            }

            // 非アクティブのままだと表示できないため表示時に有効化する
            if (visible && !confirmPanelRoot.activeSelf)
            {
                confirmPanelRoot.SetActive(true);
            }

            Canvas confirmCanvas = confirmPanelRoot.GetComponent<Canvas>();
            if (confirmCanvas != null)
            {
                if (visible)
                {
                    confirmCanvas.overrideSorting = true;
                    confirmCanvas.sortingOrder = ConfirmCanvasSortingOrder;
                }

                CanvasVisibilityUtility.SetCanvasEnabled(confirmCanvas, visible);
                return;
            }

            CanvasVisibilityUtility.SetUiVisible(confirmPanelRoot, visible);
        }

        private void EnsureConfirmPanelRoot()
        {
            if (confirmPanelRoot != null)
            {
                return;
            }

            Debug.LogError(
                "[LoadSlotView] confirmPanelRootが未設定です。Editor Wireツールで参照を配線してください",
                this);
        }

        private void EnsureLoadConfirmButtons()
        {
            EnsureConfirmPanelRoot();
            if (confirmPanelRoot == null)
            {
                return;
            }

            if (loadButton == null)
            {
                Debug.LogError(
                    "[LoadSlotView] loadButtonが未設定です。Editor Wireツールで参照を配線してください",
                    this);
            }

            if (backButton == null)
            {
                Debug.LogError(
                    "[LoadSlotView] backButtonが未設定です。Editor Wireツールで参照を配線してください",
                    this);
            }
        }

        private bool IsConfirmUiReady()
        {
            EnsureLoadConfirmButtons();
            EnsureLoadConfirmView();
            return confirmPanelRoot != null && loadButton != null;
        }

        private void EnsureRootCanvasEnabled()
        {
            EnsureSelectionCanvas();
            CanvasVisibilityUtility.SetCanvasEnabled(selectionCanvas, true);
        }

        // 生成済みのサムネイル(Texture2D / Sprite)を破棄する
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

        private void OnDestroy()
        {
            ClearRuntimeThumbnails();
            loadedSubject.Dispose();
        }
    }
}

