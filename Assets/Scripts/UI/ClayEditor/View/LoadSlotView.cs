using Battle.Interface;
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
using UnityEngine.Serialization;
using UnityEngine.UI;
using VContainer;

namespace UI.ClayEditor.View
{
    /// <summary>
    /// プレイヤー用セーブデータから1つを選んでモデルをロードするView。
    /// 横長のスクロール一覧でサムネイルと名前・ステータスを表示し、スロットを選ぶと確認Canvasを表示する。
    /// 「ロードする」でglbを読み込み、「戻る」で確認Canvasを閉じる。
    /// </summary>
    public class LoadSlotView : MonoBehaviour, IMonsterSelection
    {
        private const int SelectionCanvasSortingOrder = 500;
        private const int ConfirmCanvasSortingOrder = SelectionCanvasSortingOrder + 1;

        [Inject] private readonly IClayModelSaveService saveService;
        [Inject] private readonly IClayModelImporter importer;
        [Inject] private readonly IBattleCanvasTransition canvasTransition;

        [Header("セーブスロット選択")]
        [Tooltip("横長のスクロールスロット一覧。未設定ならModelSaveSlotScrollListから取得する")]
        [SerializeField] private ModelSaveSlotScrollListView slotScrollList;

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

        /// <inheritdoc />
        public int SelectedSlotIndex => LoadedSlotIndex;

        /// <inheritdoc />
        public void RestoreAfterParticipantFailure()
        {
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

        // 表示用に生成したサムネイル(Texture2D / Sprite)。再表示・破棄時にまとめてDestroyする
        private readonly List<Object> runtimeThumbnailObjects = new List<Object>();

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

            EnsureRootCanvasEnabled();
        }

        /// <summary>
        /// スロット選択UI全体を非表示にする
        /// GameObjectは無効化しない
        /// </summary>
        public void HideSelectionUi()
        {
            EnsureSelectionCanvas();
            SetConfirmPanelActive(false);
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
            selectedSlot = -1;
            isLoading = false;
            loadConfirmView?.Clear();
            SetConfirmPanelActive(false);
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
            selectedSlot = -1;
            isLoading = false;
            loadConfirmView?.Clear();
            LoadedModel = null;
            LoadedSlotIndex = -1;
            ShowSelectionUi();
        }

        /// <summary>
        /// 明転前に全画面背景付きで選択UIのレイアウトだけ整える
        /// </summary>
        public void PrepareLayout(bool reparentToUiRoot = false)
        {
            EnsureSlotScrollList();
            ApplySelectionCanvasSorting();
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
            EnsureSlotScrollList();
            if (slotScrollList == null)
            {
                return;
            }

            if (saveService == null)
            {
                Debug.LogError("[LoadSlotView] saveService未注入のためスロット内容を更新できません");
                slotScrollList.RefreshHostLayout();
                return;
            }

            ClearRuntimeThumbnails();
            slotScrollList.RefreshSlots(
                savePool,
                saveService,
                emptySlotLabel,
                runtimeThumbnailObjects,
                allowEmptySlotSelection: false);
            slotScrollList.RefreshHostLayout();
        }

        // スロットが選択されたとき: 確認キャンバスを開く
        private void OnSlotSelected(int slotIndex)
        {
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
            loadConfirmView?.Clear();
            SetConfirmPanelActive(false);

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
            EnsureConfirmPanelRoot();
            if (loadConfirmView != null || confirmPanelRoot == null)
            {
                return;
            }

            loadConfirmView = confirmPanelRoot.GetComponentInChildren<ModelSaveConfirmView>(true);
            if (loadConfirmView == null)
            {
                Debug.LogError(
                    "[LoadSlotView] ModelSaveConfirmViewが未配置です。Tools/ClayMonsters/Migrate Dynamic UI To Scenesを実行してください",
                    this);
            }
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
                return;
            }

            loadConfirmView.ShowSlot(slot, LoadThumbnailPng(slot), slotIndex);
        }

        private static byte[] LoadThumbnailPng(ModelSaveSlot slot)
        {
            if (slot == null || string.IsNullOrEmpty(slot.thumbnailFileName))
            {
                return null;
            }

            string path = Path.Combine(Application.persistentDataPath, slot.thumbnailFileName);
            if (!File.Exists(path))
            {
                return null;
            }

            return File.ReadAllBytes(path);
        }

        private void EnsureSlotScrollList()
        {
            slotScrollList = ModelSaveSlotScrollListRuntimeUtility.EnsureHostUnderTransform(
                transform,
                slotScrollList);
        }

        private void ApplySelectionCanvasSorting()
        {
            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                return;
            }

            canvas.overrideSorting = true;
            canvas.sortingOrder = SelectionCanvasSortingOrder;
        }

        /// <summary>
        /// 確認パネルを選択UIより前面に配置する
        /// </summary>
        private void EnsureConfirmPanelInFront()
        {
            EnsureConfirmPanelRoot();
            if (confirmPanelRoot == null)
            {
                return;
            }

            if (confirmPanelRoot.transform.parent != transform)
            {
                confirmPanelRoot.transform.SetParent(transform, false);
            }
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

                CanvasVisibilityUtility.SetUiVisible(child.gameObject, visible);
            }
        }

        private void EnsureSelectionCanvas()
        {
            if (selectionCanvas == null)
            {
                selectionCanvas = GetComponent<Canvas>();
            }
        }

        private void SetConfirmPanelActive(bool visible)
        {
            EnsureConfirmPanelRoot();
            if (confirmPanelRoot == null)
            {
                return;
            }

            Canvas confirmCanvas = confirmPanelRoot.GetComponent<Canvas>();
            if (confirmCanvas == null)
            {
                Debug.LogError("[LoadSlotView] ConfirmPanelにCanvasがありません", confirmPanelRoot);
                return;
            }

            if (visible)
            {
                confirmCanvas.overrideSorting = true;
                confirmCanvas.sortingOrder = ConfirmCanvasSortingOrder;
            }

            CanvasVisibilityUtility.SetCanvasEnabled(confirmCanvas, visible);
        }

        private void EnsureConfirmPanelRoot()
        {
            if (confirmPanelRoot)
            {
                return;
            }

            confirmPanelRoot = null;
            Transform confirmRoot = FindConfirmPanelTransform();
            if (confirmRoot == null)
            {
                return;
            }

            confirmPanelRoot = confirmRoot.gameObject;
        }

        private Transform FindConfirmPanelTransform()
        {
            Transform confirmRoot = transform.Find("ConfirmPanel")
                ?? transform.Find("ConfirmSaveSlotCanvas");
            if (confirmRoot != null)
            {
                return confirmRoot;
            }

            Transform parent = transform.parent;
            if (parent != null)
            {
                confirmRoot = parent.Find("ConfirmPanel")
                    ?? parent.Find("ConfirmSaveSlotCanvas");
                if (confirmRoot != null)
                {
                    return confirmRoot;
                }

                for (int i = 0; i < parent.childCount; i++)
                {
                    Transform sibling = parent.GetChild(i);
                    if (sibling.name == "ConfirmPanel" || sibling.name == "ConfirmSaveSlotCanvas")
                    {
                        return sibling;
                    }
                }
            }

            Transform sceneRoot = transform.root;
            return FindDeepChildByName(sceneRoot, "ConfirmPanel")
                ?? FindDeepChildByName(sceneRoot, "ConfirmSaveSlotCanvas");
        }

        private static Transform FindDeepChildByName(Transform root, string objectName)
        {
            if (root == null || string.IsNullOrEmpty(objectName))
            {
                return null;
            }

            if (root.name == objectName)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeepChildByName(root.GetChild(i), objectName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
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
                loadButton = FindConfirmButton("LoadConfirmButton");
            }

            if (backButton == null)
            {
                backButton = FindConfirmButton("LoadConfirmBackButton");
            }
        }

        private LHButton FindConfirmButton(string buttonName)
        {
            Transform searchRoot = confirmPanelRoot != null ? confirmPanelRoot.transform : transform;
            Transform found = searchRoot.Find(buttonName);
            if (found == null)
            {
                found = transform.Find("ConfirmPanel/" + buttonName);
            }

            return found != null ? found.GetComponent<LHButton>() : null;
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
            if (selectionCanvas != null && !selectionCanvas.enabled)
            {
                selectionCanvas.enabled = true;
            }
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

