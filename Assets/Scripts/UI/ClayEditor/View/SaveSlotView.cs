using ClayEditor;
using ClayEditor.Rigging;
using Cysharp.Threading.Tasks;
using Extensions;
using GameData;
using LighthouseExtends.UIComponent.Button;
using R3;
using SaveData;
using SaveData.Interface;
using SaveData.Service;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UI.ClayEditor.ViewModel;
using VContainer;

namespace UI.ClayEditor.View
{
    /// <summary>
    /// モデルのセーブUIフロー全体を制御するView
    /// プレイヤー保存または敵保存ボタン(敵はUnityエディタのみ) → スロット選択 → 名前入力 → 確認表示 → セーブ(完了をOnSavedで通知)
    /// シーン遷移や入力/カメラのロックはScene側(ClayEditPresenter)がOnSaved/IsSaveUiOpenを購読して行う
    /// (asmdefの循環参照を避けるため、このViewからは他レイヤーを直接触らない)
    /// </summary>
    public class SaveSlotView : MonoBehaviour
    {
        [Inject] private readonly IClayModelSaveService saveService;
        [Inject] private readonly ClayAutoRigController autoRigController;
        [Inject] private readonly SkeletonPartAnalyzer partAnalyzer;
        [Inject] private readonly ClayVoxelEngine voxelEngine;
        [Inject] private readonly ClayEditModeViewModel editModeViewModel;
        [Inject] private readonly ClayEditSessionContext sessionContext;

        [Header("保存UI")]
        [Tooltip("ONのとき確認画面などのフォールバックUIを実行時生成しない")]
        [SerializeField] private bool useSceneCanvasLayout = true;

        [Header("保存ボタン")]
        [Tooltip("画面上の保存ボタン。押すとプレイヤー用セーブスロット選択キャンバスを開く")]
        [SerializeField] private LHButton openSaveButton;

        [Tooltip("敵として保存ボタン。Unityエディタでのみ表示・操作可能")]
        [SerializeField] private LHButton openEnemySaveButton;

        [Tooltip("敵保存UIのルート。未設定ならopenEnemySaveButtonを非表示にする")]
        [SerializeField] private GameObject enemySaveUiRoot;

        [Tooltip("保存・戻るなど粘土編集終了UIのルート。セーブUI表示中は非表示にする")]
        [SerializeField] private GameObject openSaveUiRoot;

        [Header("セーブスロット選択キャンバス")]
        [Tooltip("スロット選択キャンバス本体")]
        [SerializeField] private Canvas slotCanvas;

        [Tooltip("スロット選択キャンバスを閉じる戻るボタン")]
        [SerializeField] private LHButton backButton;

        [Tooltip("横長のスクロールスロット一覧。未設定ならModelSaveSlotScrollListから取得する")]
        [SerializeField] private ModelSaveSlotScrollListView slotScrollList;

        [Tooltip("データが無い(空き)スロットに表示する文言")]
        [SerializeField] private string emptySlotLabel = "空き";

        [Header("スロット操作確認")]
        [Tooltip("使用中スロット選択時に表示する確認キャンバス")]
        [SerializeField] private Canvas slotActionCanvas;

        [Tooltip("スロット操作確認の内容表示")]
        [SerializeField] private ModelSaveConfirmView slotActionConfirmView;

        [Tooltip("上書き保存へ進むボタン")]
        [SerializeField] private LHButton slotActionOverwriteButton;

        [Tooltip("セーブデータ削除ボタン")]
        [SerializeField] private LHButton slotActionDeleteButton;

        [Tooltip("スロット一覧へ戻るボタン")]
        [SerializeField] private LHButton slotActionBackButton;

        [Tooltip("削除最終確認ダイアログ")]
        [SerializeField] private ModelSaveSlotDeletePromptView slotActionDeletePromptView;

        [Header("名前入力キャンバス")]
        [Tooltip("名前入力キャンバス本体")]
        [SerializeField] private Canvas nameInputCanvas;

        [Tooltip("モデル名の入力View")]
        [SerializeField] private ModelNameInputView nameInputView;

        [Tooltip("名前入力を完了して確認画面へ進むボタン")]
        [SerializeField] private LHButton confirmNameButton;

        [Tooltip("名前入力からスロット選択へ戻るボタン")]
        [SerializeField] private LHButton nameInputBackButton;

        [Header("保存確認キャンバス")]
        [Tooltip("名前・サムネイル・属性・パラメータ・攻撃をまとめて表示するキャンバス")]
        [FormerlySerializedAs("saveCompleteCanvas")]
        [SerializeField] private Canvas saveConfirmCanvas;

        [Tooltip("保存確認画面の内容表示")]
        [SerializeField] private ModelSaveConfirmView saveConfirmView;

        [Tooltip("確認画面から実際に保存するボタン")]
        [SerializeField] private LHButton confirmSaveButton;

        [Tooltip("確認画面から名前入力へ戻るボタン")]
        [SerializeField] private LHButton confirmBackButton;

        [Tooltip("保存完了後に閉じるボタン。押すとシーン遷移する")]
        [SerializeField] private LHButton closeButton;

        [Header("サムネイル撮影")]
        [Tooltip("保存時にモデルを画像化する撮影器。未設定ならサムネイルは保存しない")]
        [SerializeField] private ModelThumbnailCapturer thumbnailCapturer;

        [Header("セーブUI表示中に隠す編集UI")]
        [Tooltip("スロット選択・名前入力・完了表示中に前面へ出てしまう編集用UIのルート")]
        [SerializeField] private GameObject[] editorOverlayRoots;

        // セーブ完了通知(Scene側が購読してシーン遷移する)
        private readonly Subject<SaveCompletedInfo> savedSubject = new Subject<SaveCompletedInfo>();

        // 保存UI(スロット選択 or 名前入力)が開いているかの状態
        private readonly ReactiveProperty<bool> isSaveUiOpen = new ReactiveProperty<bool>(false);

        // 表示用に生成したサムネイル(Texture2D / Sprite)。再表示・破棄時にまとめてDestroyする
        private readonly List<Object> runtimeThumbnailObjects = new List<Object>();

        /// <summary>
        /// セーブ完了後、セーブ完了キャンバスの「閉じる」ボタンが押されたときに発火する。
        /// Scene側がこれを購読してシーン遷移する。
        /// </summary>
        public Observable<SaveCompletedInfo> OnSaved => savedSubject;

        /// <summary>
        /// 保存関連のUI(スロット選択 or 名前入力キャンバス)が開いているか。
        /// Scene側がこれを購読し、開いている間は入力やカメラ操作をロックする。
        /// </summary>
        public Observable<bool> IsSaveUiOpen => isSaveUiOpen;

        // 現在選択中のスロット番号(未選択は-1)
        private int selectedSlot = -1;

        // 現在の保存先プール
        private ModelSavePool currentSavePool = ModelSavePool.Player;

        private bool isSaving;
        private bool hasSavedOnConfirmCanvas;

        // 確認画面表示後に保存へ使う一時データ
        private string pendingModelName;
        private byte[] pendingThumbnailPng;
        private ModelStatus pendingStatus;
        private List<MotionType> pendingRegisteredAttackMotions;

        private const int SaveCanvasSortingOrder = 100;

        private bool slotScrollListInitialized;

        private void Awake()
        {
            ApplyEnemySaveUiVisibility();
        }

        private void Start()
        {
            // 保存ボタン → スロット選択キャンバスを開く
            if (openSaveButton != null)
            {
                openSaveButton.SubscribeOnClick(OnOpenPlayerSaveClicked);
            }

            if (EnemySaveAvailability.IsAvailable && openEnemySaveButton != null)
            {
                openEnemySaveButton.SubscribeOnClick(() => OpenSlotCanvas(ModelSavePool.Enemy));
            }

            // 戻るボタン → スロット選択キャンバスを閉じる
            if (backButton != null)
            {
                backButton.SubscribeOnClick(CloseSlotCanvas);
            }

            EnsureSlotScrollListReady();

            // 名前入力完了ボタン → セーブを実行する
            if (confirmNameButton != null)
            {
                confirmNameButton.SubscribeOnClick(OnConfirmName);
            }

            // 閉じるボタン → 確認キャンバスを閉じてシーン遷移する
            if (closeButton != null)
            {
                closeButton.SubscribeOnClick(OnCloseSaveComplete);
            }

            // 初期状態は各キャンバスを閉じておく
            SetCanvasEnabled(slotCanvas, false);
            SetCanvasEnabled(nameInputCanvas, false);
            SetCanvasEnabled(slotActionCanvas, false);
            SetCanvasEnabled(saveConfirmCanvas, false);
            SetSaveUiOpenState(false);

            voxelEngine.HasMeshChanged
                .Subscribe(_ => RefreshSaveButtonStates())
                .AddTo(this);

            RefreshSaveButtonStates();
            ValidateSceneLayout();

            if (confirmSaveButton != null)
            {
                confirmSaveButton.SubscribeOnClick(OnConfirmSave);
            }

            if (confirmBackButton != null)
            {
                confirmBackButton.SubscribeOnClick(OnConfirmBack);
            }

            if (nameInputBackButton != null)
            {
                nameInputBackButton.SubscribeOnClick(OnNameInputBack);
            }

            if (slotActionOverwriteButton != null)
            {
                slotActionOverwriteButton.SubscribeOnClick(OnSlotActionOverwrite);
            }

            if (slotActionDeleteButton != null)
            {
                slotActionDeleteButton.SubscribeOnClick(OnSlotActionDeleteRequested);
            }

            if (slotActionBackButton != null)
            {
                slotActionBackButton.SubscribeOnClick(OnSlotActionBack);
            }

            SetConfirmCanvasButtonsVisible(previewVisible: false, closeVisible: false);
        }

        // Unityエディタ以外では敵保存UIを隠す
        private void ApplyEnemySaveUiVisibility()
        {
            bool visible = EnemySaveAvailability.IsAvailable;

            if (enemySaveUiRoot != null)
            {
                enemySaveUiRoot.SetActive(visible);
                return;
            }

            if (openEnemySaveButton != null)
            {
                openEnemySaveButton.gameObject.SetActive(visible);
            }
        }

        // 保存ボタンが押されたとき: スロット選択キャンバスを開く
        private void OnOpenPlayerSaveClicked()
        {
            if (sessionContext != null && sessionContext.IsRemakeSave)
            {
                OpenRemakeSaveFlow(sessionContext.RemakeSlotIndex, sessionContext.RemakeModelName);
                return;
            }

            OpenSlotCanvas(ModelSavePool.Player);
        }

        /// <summary>
        /// 作り直しモード用に保存先スロットを固定して名前入力から開始する
        /// </summary>
        /// <param name="slotIndex">上書き先スロット番号</param>
        /// <param name="existingModelName">初期表示するモデル名</param>
        public void OpenRemakeSaveFlow(int slotIndex, string existingModelName)
        {
            if (!TryValidateSavableMesh())
            {
                return;
            }

            currentSavePool = ModelSavePool.Player;
            selectedSlot = slotIndex;
            if (nameInputView != null)
            {
                nameInputView.SetName(existingModelName ?? string.Empty);
            }

            SetCanvasEnabled(slotCanvas, false);
            SetCanvasEnabled(nameInputCanvas, true);
            SetSaveUiOpenState(true);
            nameInputView?.FocusInput();
        }

        // 保存ボタンが押されたとき: スロット選択キャンバスを開く
        private void OpenSlotCanvas(ModelSavePool pool)
        {
            if (!TryValidateSavableMesh())
            {
                return;
            }

            if (pool == ModelSavePool.Enemy && !EnemySaveAvailability.IsAvailable)
            {
                Debug.LogWarning("[SaveSlotView] 敵保存はUnityエディタでのみ利用できます");
                return;
            }

            currentSavePool = pool;
            selectedSlot = -1;
            slotActionConfirmView?.Clear();
            slotActionDeletePromptView?.Hide();

            EnsureSlotScrollListReady();
            if (slotScrollList == null)
            {
                Debug.LogError("[SaveSlotView] slotScrollListが見つかりません", this);
                return;
            }

            SetCanvasEnabled(slotActionCanvas, false);
            SetCanvasEnabled(nameInputCanvas, false);
            SetCanvasEnabled(slotCanvas, true);
            SetSaveUiOpenState(true);

            // 全画面レイアウト適用後にスロット表示を更新する
            RefreshSlots();
        }

        // 各スロットの表示を更新する。データがあれば名前とサムネイル、無ければ空き表示にする
        private void RefreshSlots()
        {
            if (!TryRefreshWhenHostReady() && IsSlotCanvasVisible())
            {
                Debug.LogWarning("[SaveSlotView] スロット一覧のホストレイアウト未確定のため更新を延期します");
            }
        }

        private void RefreshSlotsInternal()
        {
            if (saveService == null || slotScrollList == null)
            {
                return;
            }

            ClearRuntimeThumbnails();
            slotScrollList.RefreshSlots(
                currentSavePool,
                saveService,
                emptySlotLabel,
                runtimeThumbnailObjects,
                allowEmptySlotSelection: true);
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
                if (this == null || !IsSlotCanvasVisible())
                {
                    return;
                }

                TryRefreshWhenHostReady();
            });
        }

        private bool IsSlotCanvasVisible()
        {
            return slotCanvas != null && slotCanvas.enabled;
        }

        // 戻るボタンが押されたとき: スロット選択キャンバスを閉じる
        private void CloseSlotCanvas()
        {
            selectedSlot = -1;
            slotActionConfirmView?.Clear();
            slotActionDeletePromptView?.Hide();
            SetCanvasEnabled(slotActionCanvas, false);
            SetCanvasEnabled(slotCanvas, false);
            SetSaveUiOpenState(false);
        }

        // スロットが選択されたとき: 空きなら名前入力、使用中なら操作確認を開く
        private void OnSlotSelected(int slotIndex)
        {
            selectedSlot = slotIndex;
            ModelSaveSlot slot = saveService.GetSlot(currentSavePool, slotIndex);
            if (slot != null && !string.IsNullOrEmpty(slot.glbFileName))
            {
                OpenSlotActionConfirm(slotIndex, slot);
                return;
            }

            OpenNameInputForSlot(slot);
        }

        private void OpenNameInputForSlot(ModelSaveSlot slot)
        {
            if (nameInputView != null)
            {
                nameInputView.SetName(slot != null ? slot.modelName : string.Empty);
            }

            SetCanvasEnabled(slotActionCanvas, false);
            SetCanvasEnabled(slotCanvas, false);
            SetCanvasEnabled(nameInputCanvas, true);
            SetSaveUiOpenState(true);
            nameInputView?.FocusInput();
        }

        private void OpenSlotActionConfirm(int slotIndex, ModelSaveSlot slot)
        {
            slotActionDeletePromptView?.Hide();
            SetCanvasEnabled(slotCanvas, false);
            SetCanvasEnabled(nameInputCanvas, false);
            SetCanvasEnabled(slotActionCanvas, true);
            SetSaveUiOpenState(true);
            slotActionConfirmView?.ShowSlot(slot, LoadSlotThumbnailPng(slot), slotIndex);
        }

        private void OnSlotActionOverwrite()
        {
            if (selectedSlot < 0)
            {
                return;
            }

            ModelSaveSlot slot = saveService.GetSlot(currentSavePool, selectedSlot);
            OpenNameInputForSlot(slot);
        }

        private void OnSlotActionDeleteRequested()
        {
            if (selectedSlot < 0 || saveService == null)
            {
                return;
            }

            ModelSaveSlot slot = saveService.GetSlot(currentSavePool, selectedSlot);
            if (slot == null || string.IsNullOrEmpty(slot.glbFileName))
            {
                return;
            }

            if (slotActionDeletePromptView == null)
            {
                Debug.LogError("[SaveSlotView] slotActionDeletePromptViewが未設定です");
                return;
            }

            slotActionDeletePromptView.Show(slot.modelName, selectedSlot, OnSlotActionDeleteConfirmed);
        }

        private void OnSlotActionDeleteConfirmed()
        {
            if (selectedSlot < 0 || saveService == null)
            {
                return;
            }

            saveService.DeleteSlot(currentSavePool, selectedSlot);
            selectedSlot = -1;
            slotActionConfirmView?.Clear();
            slotActionDeletePromptView?.Hide();
            SetCanvasEnabled(slotActionCanvas, false);
            SetCanvasEnabled(slotCanvas, true);
            SetSaveUiOpenState(true);
            RefreshSlots();
        }

        private void OnSlotActionBack()
        {
            selectedSlot = -1;
            slotActionConfirmView?.Clear();
            slotActionDeletePromptView?.Hide();
            SetCanvasEnabled(slotActionCanvas, false);
            SetCanvasEnabled(slotCanvas, true);
            SetSaveUiOpenState(true);
            RefreshSlots();
        }

        private static byte[] LoadSlotThumbnailPng(ModelSaveSlot slot)
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

        // 名前入力の戻るボタンが押されたとき: スロット選択へ戻る
        private void OnNameInputBack()
        {
            if (isSaving)
            {
                return;
            }

            if (sessionContext != null && sessionContext.IsRemakeSave)
            {
                SetCanvasEnabled(nameInputCanvas, false);
                SetSaveUiOpenState(false);
                return;
            }

            selectedSlot = -1;
            SetCanvasEnabled(nameInputCanvas, false);
            SetCanvasEnabled(slotActionCanvas, false);
            SetCanvasEnabled(slotCanvas, true);
            SetSaveUiOpenState(true);
            RefreshSlots();
        }

        // 名前入力完了ボタンが押されたとき: 確認キャンバスを開く
        private void OnConfirmName()
        {
            if (isSaving)
            {
                return;
            }

            if (selectedSlot < 0)
            {
                Debug.LogWarning("[SaveSlotView] 保存先スロットが選択されていません");
                return;
            }

            if (!TryValidateSavableMesh())
            {
                return;
            }

            ShowConfirmCanvasAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        // 確認画面の保存ボタンが押されたとき: セーブを実行する
        private void OnConfirmSave()
        {
            if (isSaving || hasSavedOnConfirmCanvas)
            {
                return;
            }

            ExecuteSaveAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        // 確認画面の戻るボタンが押されたとき: 名前入力へ戻る
        private void OnConfirmBack()
        {
            if (isSaving)
            {
                return;
            }

            CloseConfirmCanvas();
            SetCanvasEnabled(nameInputCanvas, true);
            SetSaveUiOpenState(true);
            nameInputView?.FocusInput();
        }

        // セーブを実行し、成功したらOnSavedで通知する
        private async UniTaskVoid ShowConfirmCanvasAsync(CancellationToken cancellationToken)
        {
            isSaving = true;
            try
            {
                if (!TryValidateSavableMesh())
                {
                    return;
                }

                autoRigController.RebuildSkeleton();

                SkinnedMeshRenderer renderer = autoRigController.MeshRenderer;
                Transform boneRoot = autoRigController.BoneRoot;
                if (renderer == null || boneRoot == null)
                {
                    Debug.LogWarning("[SaveSlotView] 確認表示対象が不足しています(renderer / boneRoot)");
                    return;
                }

                if (autoRigController.Bones.Length == 0
                    || renderer.sharedMesh == null
                    || renderer.sharedMesh.vertexCount == 0)
                {
                    Debug.LogWarning("[SaveSlotView] メッシュが無いため確認表示できません");
                    return;
                }

                pendingThumbnailPng = null;
                if (thumbnailCapturer != null)
                {
                    pendingThumbnailPng = await thumbnailCapturer.CaptureToPngAsync(renderer, cancellationToken);
                }

                pendingRegisteredAttackMotions = ClayModelSaveService.PickRandomAttacks(
                    partAnalyzer,
                    autoRigController.Bones,
                    ClayModelSaveService.AttackMotionCount);
                pendingStatus = ModelStatusCalculator.Calculate(
                    renderer,
                    partAnalyzer,
                    autoRigController.Bones);
                pendingModelName = nameInputView != null ? nameInputView.GetConfirmedName() : "Monster";
                hasSavedOnConfirmCanvas = false;

                SetCanvasEnabled(nameInputCanvas, false);
                SetCanvasEnabled(slotCanvas, false);
                saveConfirmView?.ShowPreview(
                    pendingModelName,
                    pendingThumbnailPng,
                    selectedSlot,
                    pendingStatus,
                    pendingRegisteredAttackMotions);
                SetConfirmCanvasButtonsVisible(previewVisible: true, closeVisible: false);
                SetCanvasEnabled(saveConfirmCanvas, true);
                SetSaveUiOpenState(true);
                RestoreSculptMeshAfterPreview();
            }
            finally
            {
                isSaving = false;
            }
        }

        private async UniTaskVoid ExecuteSaveAsync(CancellationToken cancellationToken)
        {
            if (currentSavePool == ModelSavePool.Enemy && !EnemySaveAvailability.IsAvailable)
            {
                Debug.LogWarning("[SaveSlotView] 敵保存はUnityエディタでのみ利用できます");
                return;
            }

            isSaving = true;
            try
            {
                if (!TryValidateSavableMesh())
                {
                    return;
                }

                // 保存対象の形状からボーンを生成する
                autoRigController.RebuildSkeleton();

                SkinnedMeshRenderer renderer = autoRigController.MeshRenderer;
                Transform boneRoot = autoRigController.BoneRoot;
                if (renderer == null || boneRoot == null)
                {
                    Debug.LogWarning("[SaveSlotView] 保存対象が不足しています(renderer / boneRoot)");
                    return;
                }

                if (autoRigController.Bones.Length == 0
                    || renderer.sharedMesh == null
                    || renderer.sharedMesh.vertexCount == 0)
                {
                    Debug.LogWarning("[SaveSlotView] メッシュが無いため保存できません");
                    return;
                }

                byte[] thumbnailPng = pendingThumbnailPng;
                if (thumbnailPng == null && thumbnailCapturer != null)
                {
                    thumbnailPng = await thumbnailCapturer.CaptureToPngAsync(renderer, cancellationToken);
                }

                List<MotionType> attackMotions = pendingRegisteredAttackMotions;
                if (attackMotions == null || attackMotions.Count == 0)
                {
                    attackMotions = ClayModelSaveService.PickRandomAttacks(
                        partAnalyzer,
                        autoRigController.Bones,
                        ClayModelSaveService.AttackMotionCount);
                }
                else
                {
                    attackMotions = AttackMotionSelector.FilterUsableAttacks(
                        attackMotions,
                        partAnalyzer,
                        autoRigController.Bones);
                    if (attackMotions.Count == 0)
                    {
                        attackMotions = ClayModelSaveService.PickRandomAttacks(
                            partAnalyzer,
                            autoRigController.Bones,
                            ClayModelSaveService.AttackMotionCount);
                    }
                }

                string modelName = !string.IsNullOrEmpty(pendingModelName)
                    ? pendingModelName
                    : nameInputView != null ? nameInputView.GetConfirmedName() : "Monster";

                ModelStatus status = ModelStatusCalculator.Calculate(
                    renderer,
                    partAnalyzer,
                    autoRigController.Bones);

                bool success = await saveService.SaveAsync(
                    currentSavePool,
                    selectedSlot,
                    modelName,
                    status,
                    attackMotions,
                    renderer,
                    boneRoot,
                    thumbnailPng,
                    cancellationToken);

                if (!success)
                {
                    Debug.LogError("[SaveSlotView] セーブに失敗しました");
                    return;
                }

                string voxelFilePath = Path.Combine(
                    Application.persistentDataPath,
                    ModelSavePoolSettings.GetVoxelFileName(currentSavePool, selectedSlot));
                if (!ClayVoxelSnapshotFile.TryWrite(
                        voxelFilePath,
                        voxelEngine.GetVoxelData(),
                        voxelEngine.GetVoxelColors(),
                        voxelEngine.size,
                        voxelEngine.boundsSize))
                {
                    Debug.LogWarning("[SaveSlotView] ボクセルスナップショットの保存に失敗しました");
                }

                Debug.Log($"[SaveSlotView] {currentSavePool}スロット{selectedSlot}へ保存しました: {modelName}");

                hasSavedOnConfirmCanvas = true;
                ClearPendingSaveData();
                RestoreSculptMeshAfterPreview();

                // 閉じるボタンがある場合は完了表示へ切り替えて閉じる操作を待つ
                // 無い場合は確認UIを残したまま遷移し暗転で覆う
                if (closeButton != null)
                {
                    byte[] savedThumbnailPng = thumbnailPng;
                    ModelSaveSlot savedSlot = saveService.GetSlot(currentSavePool, selectedSlot);
                    if (savedSlot != null && saveConfirmView != null)
                    {
                        saveConfirmView.ShowSlot(savedSlot, savedThumbnailPng, selectedSlot);
                    }

                    SetConfirmCanvasButtonsVisible(previewVisible: false, closeVisible: true);
                }
                else
                {
                    CompleteAndTransition();
                }
            }
            finally
            {
                isSaving = false;
            }
        }

        // セーブ完了キャンバスの閉じるボタンが押されたとき: シーン遷移する
        private void OnCloseSaveComplete()
        {
            CompleteAndTransition();
        }

        // シーン遷移を開始する。
        // 確認UIは暗転開始まで残し遷移先の明転まで画面を覆う
        // モデル削除はClayEditScene.OnLeaveのResetに任せる
        private void CompleteAndTransition()
        {
            savedSubject.OnNext(new SaveCompletedInfo(currentSavePool, selectedSlot));
        }

        private void SetSaveUiOpenState(bool isOpen)
        {
            isSaveUiOpen.Value = isOpen;
            SetEditorOverlaysVisible(!isOpen);
            SetOpenSaveUiVisible(!isOpen);
            if (isOpen)
            {
                ApplySaveOverlayLayout();
            }
            else
            {
                ModelSaveSlotScrollListView.ExitFullscreenSelectionLayout();
                RestoreSculptMeshAfterPreview();
            }
        }

        private void ApplySaveOverlayLayout()
        {
            ModelSaveSlotScrollListView.EnterFullscreenSelectionLayout(slotCanvas);
            ModelSaveSlotScrollListView.EnterFullscreenSelectionLayout(slotActionCanvas);
            ModelSaveSlotScrollListView.EnterFullscreenSelectionLayout(nameInputCanvas);
            ModelSaveSlotScrollListView.EnterFullscreenSelectionLayout(saveConfirmCanvas);
            ModelSaveSlotScrollListView.EnsureSelectionBackground(slotCanvas);
            EnsureSlotScrollListReady();
        }

        private void SetEditorOverlaysVisible(bool visible)
        {
            if (editorOverlayRoots == null)
            {
                return;
            }

            for (int i = 0; i < editorOverlayRoots.Length; i++)
            {
                if (editorOverlayRoots[i] != null)
                {
                    editorOverlayRoots[i].SetActive(visible);
                }
            }
        }

        private void SetOpenSaveUiVisible(bool visible)
        {
            if (openSaveUiRoot != null)
            {
                openSaveUiRoot.SetActive(visible);
            }
        }

        private void CloseConfirmCanvas()
        {
            SetCanvasEnabled(saveConfirmCanvas, false);
            saveConfirmView?.Clear();
            SetConfirmCanvasButtonsVisible(previewVisible: false, closeVisible: false);
            hasSavedOnConfirmCanvas = false;
            ClearPendingSaveData();
            RestoreSculptMeshAfterPreview();
        }

        private void RestoreSculptMeshAfterPreview()
        {
            autoRigController.RestoreSculptEditState(editModeViewModel.CurrentMode.CurrentValue);
        }

        private void ClearPendingSaveData()
        {
            pendingModelName = null;
            pendingThumbnailPng = null;
            pendingStatus = null;
            pendingRegisteredAttackMotions = null;
        }

        private void SetConfirmCanvasButtonsVisible(bool previewVisible, bool closeVisible)
        {
            if (confirmSaveButton != null)
            {
                confirmSaveButton.gameObject.SetActive(previewVisible);
            }

            if (confirmBackButton != null)
            {
                confirmBackButton.gameObject.SetActive(previewVisible);
            }

            if (closeButton != null)
            {
                closeButton.gameObject.SetActive(closeVisible);
            }
        }

        private void ValidateSceneLayout()
        {
            if (!useSceneCanvasLayout)
            {
                return;
            }

            if (saveConfirmView == null
                || confirmSaveButton == null
                || confirmBackButton == null
                || nameInputBackButton == null
                || slotActionCanvas == null
                || slotActionConfirmView == null
                || slotActionOverwriteButton == null
                || slotActionDeleteButton == null
                || slotActionBackButton == null
                || slotActionDeletePromptView == null)
            {
                Debug.LogError(
                    "[SaveSlotView] シーン上のUI参照が未設定です。HierarchyでUI参照を確認してください",
                    this);
            }
        }

        private void EnsureSlotScrollList()
        {
            if (slotCanvas == null)
            {
                return;
            }

            slotScrollList = ModelSaveSlotScrollListRuntimeUtility.EnsureHostUnderTransform(
                slotCanvas.transform,
                slotScrollList);
        }

        private void RefreshSaveButtonStates()
        {
            bool canSave = voxelEngine.HasMesh();

            if (openSaveButton != null)
            {
                openSaveButton.interactable = canSave;
            }

            if (openEnemySaveButton != null && EnemySaveAvailability.IsAvailable)
            {
                openEnemySaveButton.interactable = canSave;
            }
        }

        private bool TryValidateSavableMesh()
        {
            if (voxelEngine.HasMesh())
            {
                return true;
            }

            Debug.LogWarning("[SaveSlotView] メッシュが無いため保存できません");
            return false;
        }

        // Canvasのenabledを安全に切り替える
        private static void SetCanvasEnabled(Canvas canvas, bool isEnabled)
        {
            if (canvas == null)
            {
                return;
            }

            canvas.enabled = isEnabled;
            if (isEnabled)
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = SaveCanvasSortingOrder;
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
            savedSubject.Dispose();
            isSaveUiOpen.Dispose();
        }
    }
}