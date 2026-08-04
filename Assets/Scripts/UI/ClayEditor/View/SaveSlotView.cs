using ClayEditor;
using ClayEditor.Rigging;
using Cysharp.Threading.Tasks;
using Extensions;
using GameData;
using LighthouseExtends.UIComponent.Button;
using Localization;
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
    /// プレイヤー保存または敵保存ボタン(敵はEditorまたはROM敵保存Build Profileのみ) → スロット選択 → 名前入力 → 確認表示 → セーブ → 完了ウィンドウ → 閉じるでOnSaved通知
    /// シーン遷移や入力/カメラのロックはScene側(ClayEditPresenter)がOnSaved/IsSaveUiOpenを購読して行う
    /// (asmdefの循環参照を避けるため、このViewからは他レイヤーを直接触らない)
    /// </summary>
    public class SaveSlotView : MonoBehaviour, ILanguageAwareUi
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

        [Tooltip("敵として保存ボタン。EditorまたはROM敵保存Build Profileでのみ表示・操作可能")]
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
        [Tooltip("名前・サムネイル・パラメータ・攻撃をまとめて表示するキャンバス")]
        [FormerlySerializedAs("saveCompleteCanvas")]
        [SerializeField] private Canvas saveConfirmCanvas;

        [Tooltip("保存確認画面の内容表示")]
        [SerializeField] private ModelSaveConfirmView saveConfirmView;

        [Tooltip("確認画面から実際に保存するボタン")]
        [SerializeField] private LHButton confirmSaveButton;

        [Tooltip("確認画面から名前入力へ戻るボタン")]
        [SerializeField] private LHButton confirmBackButton;

        [Header("保存完了")]
        [Tooltip("セーブ完了専用ウィンドウ")]
        [SerializeField] private ModelSaveCompleteView saveCompleteView;

        [Tooltip("保存完了メッセージ文言")]
        [SerializeField] private string saveCompleteMessage = "セーブが完了しました";

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
        /// セーブ完了後、完了ウィンドウの閉じるボタンが押されたときに発火する。
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

            if (saveCompleteView != null)
            {
                saveCompleteView.OnClosed
                    .Subscribe(_ => OnSaveCompleteClosed())
                    .AddTo(this);
                saveCompleteView.Hide();
            }

            // 名前入力完了ボタン → セーブを実行する
            if (confirmNameButton != null)
            {
                confirmNameButton.SubscribeOnClick(OnConfirmName);
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

            ApplyLocalizedLabels();
            SetConfirmCanvasButtonsVisible(previewVisible: false);
        }


        /// <inheritdoc/>
        public void RefreshLocalizedUi()
        {
            ApplyLocalizedLabels();
            if (slotCanvas != null && slotCanvas.enabled)
            {
                RefreshSlots();
            }
        }

        private void ApplyLocalizedLabels()
        {
            LhButtonLabelUtility.SetLabel(
                openSaveButton,
                LocalizedText.GetOrFallback(GameTextKeys.ClayEditSave, "保存"));
            LhButtonLabelUtility.SetLabel(
                openEnemySaveButton,
                LocalizedText.GetOrFallback(GameTextKeys.ClayEditSaveAsEnemy, "敵保存"));
            LhButtonLabelUtility.SetLabel(
                backButton,
                LocalizedText.GetOrFallback(GameTextKeys.CommonReturn, "戻る"));
            LhButtonLabelUtility.SetLabel(
                slotActionOverwriteButton,
                LocalizedText.GetOrFallback(GameTextKeys.ClayEditOverwriteSave, "上書き"));
            LhButtonLabelUtility.SetLabel(
                slotActionDeleteButton,
                LocalizedText.GetOrFallback(GameTextKeys.ClayEditDelete, "削除"));
            LhButtonLabelUtility.SetLabel(
                slotActionBackButton,
                LocalizedText.GetOrFallback(GameTextKeys.CommonReturn, "戻る"));
            LhButtonLabelUtility.SetLabel(
                confirmNameButton,
                LocalizedText.GetOrFallback(GameTextKeys.CommonDecide, "決定"));
            LhButtonLabelUtility.SetLabel(
                nameInputBackButton,
                LocalizedText.GetOrFallback(GameTextKeys.CommonReturn, "戻る"));
            LhButtonLabelUtility.SetLabel(
                confirmSaveButton,
                LocalizedText.GetOrFallback(GameTextKeys.CommonSave, "保存"));
            LhButtonLabelUtility.SetLabel(
                confirmBackButton,
                LocalizedText.GetOrFallback(GameTextKeys.CommonReturn, "戻る"));

            EnsureBakedChromeLabels();
            bakedChromeLabelApplier?.Apply();
        }

        private LocalizedBakedTextApplier bakedChromeLabelApplier;

        private void EnsureBakedChromeLabels()
        {
            if (bakedChromeLabelApplier != null)
            {
                return;
            }

            bakedChromeLabelApplier = new LocalizedBakedTextApplier();
            bakedChromeLabelApplier.Register(GameTextKeys.ClayEditNamePrompt, "名前を付けてください");
            bakedChromeLabelApplier.Register(GameTextKeys.ClayEditNamePrompt, "モデル名を入力");
            bakedChromeLabelApplier.Register(GameTextKeys.ClayEditSaveConfirm, "保存しますか？");
            bakedChromeLabelApplier.Register(GameTextKeys.SaveComplete, "セーブが完了しました");
            bakedChromeLabelApplier.Register(GameTextKeys.ClayEditDeleteConfirm, "削除");
            bakedChromeLabelApplier.Register(GameTextKeys.CommonYes, "はい");
            bakedChromeLabelApplier.Register(GameTextKeys.CommonNo, "いいえ");
            // ExitSceneUI配下の兄弟SaveCanvas/InputNameも含めて差し替える
            bakedChromeLabelApplier.Capture(ResolveChromeCaptureRoot());
        }

        private Transform ResolveChromeCaptureRoot()
        {
            if (openSaveUiRoot != null && openSaveUiRoot.transform.parent != null)
            {
                return openSaveUiRoot.transform.parent;
            }

            if (openSaveUiRoot != null)
            {
                return openSaveUiRoot.transform;
            }

            return transform;
        }

        /// <summary>
        /// シーン退場再入場向けに保存フローUIだけ初期化する
        /// 編集オーバーレイと保存ボタンの表示はeditorUiGate側に任せる
        /// </summary>
        public void HideForLeave()
        {
            isSaving = false;
            selectedSlot = -1;
            slotActionConfirmView?.Clear();
            slotActionDeletePromptView?.Hide();
            saveCompleteView?.Hide();
            CloseConfirmCanvas();
            SetCanvasEnabled(slotCanvas, false);
            SetCanvasEnabled(nameInputCanvas, false);
            SetCanvasEnabled(slotActionCanvas, false);
            SetCanvasEnabled(saveConfirmCanvas, false);
            SetConfirmCanvasButtonsVisible(previewVisible: false);
            isSaveUiOpen.Value = false;
            ModelSaveSlotScrollListView.ExitFullscreenSelectionLayout();
        }

        /// <summary>
        /// 編集開始時に保存ボタンと編集オーバーレイを再表示する
        /// </summary>
        public void ShowEditorChrome()
        {
            if (isSaveUiOpen.Value)
            {
                return;
            }

            SetEditorOverlaysVisible(true);
            SetOpenSaveUiVisible(true);
        }

        // EditorまたはROM敵保存Build Profile以外では敵保存UIを隠す
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
        // 作り直し時も保存先スロットを選べる
        private void OnOpenPlayerSaveClicked()
        {
            if (sessionContext != null
                && sessionContext.IsRemake
                && sessionContext.RemakePool == ModelSavePool.Enemy
                && EnemySaveAvailability.IsAvailable)
            {
                OpenSlotCanvas(ModelSavePool.Enemy);
                return;
            }

            OpenSlotCanvas(ModelSavePool.Player);
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
                Debug.LogWarning("[SaveSlotView] 敵保存はEditorまたはROM敵保存Build Profileでのみ利用できます");
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
                ResolveEmptySlotLabel(),
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
                string initialName = ResolveInitialModelName(slot);
                nameInputView.SetName(initialName);
            }

            SetCanvasEnabled(slotActionCanvas, false);
            SetCanvasEnabled(slotCanvas, false);
            SetCanvasEnabled(nameInputCanvas, true);
            SetSaveUiOpenState(true);
            nameInputView?.FocusInput();
        }

        // 空きスロットは作り直し元の名前を優先し使用中スロットは既存名を使う
        private string ResolveInitialModelName(ModelSaveSlot slot)
        {
            if (slot != null && !string.IsNullOrEmpty(slot.modelName))
            {
                return slot.modelName;
            }

            if (sessionContext != null
                && sessionContext.IsRemake
                && !string.IsNullOrEmpty(sessionContext.RemakeModelName))
            {
                return sessionContext.RemakeModelName;
            }

            return string.Empty;
        }

        private void OpenSlotActionConfirm(int slotIndex, ModelSaveSlot slot)
        {
            slotActionDeletePromptView?.Hide();
            SetCanvasEnabled(slotCanvas, false);
            SetCanvasEnabled(nameInputCanvas, false);
            SetCanvasEnabled(slotActionCanvas, true);
            SetSaveUiOpenState(true);
            slotActionConfirmView?.ShowSlot(slot, ModelSaveStorage.ReadThumbnailPng(slot), slotIndex);
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

        // 名前入力の戻るボタンが押されたとき: スロット選択へ戻る
        private void OnNameInputBack()
        {
            if (isSaving)
            {
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
                if (saveConfirmView != null)
                {
                    saveConfirmView.gameObject.SetActive(true);
                }

                saveConfirmView?.ShowPreview(
                    pendingModelName,
                    pendingStatus,
                    pendingRegisteredAttackMotions,
                    pendingThumbnailPng);
                SetConfirmCanvasButtonsVisible(previewVisible: true);
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
                Debug.LogWarning("[SaveSlotView] 敵保存はEditorまたはROM敵保存Build Profileでのみ利用できます");
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

                Transform[] bones = autoRigController.Bones;
                List<MotionType> usableAttacks = AttackMotionSelector.CollectUsableAttacks(
                    partAnalyzer,
                    bones);
                List<MotionType> attackMotions = pendingRegisteredAttackMotions;
                if (attackMotions == null || attackMotions.Count == 0)
                {
                    attackMotions = AttackMotionSelector.PickInitialSaveAttacks(
                        usableAttacks,
                        ClayModelSaveService.AttackMotionCount);
                }
                else
                {
                    attackMotions = AttackMotionSelector.FilterUsableAttacks(
                        attackMotions,
                        partAnalyzer,
                        bones);
                }

                // 使用可能攻撃から必ずスロット数を埋める埋められない場合はエラー
                // 生成時補充は星2まで星3は継承か育成でのみ
                attackMotions = AttackMotionSelector.EnsureAttackSlots(
                    attackMotions,
                    usableAttacks,
                    ClayModelSaveService.AttackMotionCount,
                    maxStrengthRankForFill: 2);

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
                ShowSaveCompletedWindow();
            }
            finally
            {
                isSaving = false;
            }
        }

        // セーブ完了専用ウィンドウを表示し閉じる操作を待つ
        private void ShowSaveCompletedWindow()
        {
            if (saveCompleteView == null)
            {
                Debug.LogError(
                    "[SaveSlotView] saveCompleteViewが未設定ですSaveCompletePromptCanvasを配置して配線してください",
                    this);
                return;
            }

            SetCanvasEnabled(nameInputCanvas, false);
            SetCanvasEnabled(slotCanvas, false);
            SetCanvasEnabled(slotActionCanvas, false);
            SetCanvasEnabled(saveConfirmCanvas, false);
            SetConfirmCanvasButtonsVisible(previewVisible: false);
            saveCompleteView.Show(ResolveSaveCompleteMessage());
            SetSaveUiOpenState(true);
        }

        // 完了ウィンドウの閉じる後にシーン遷移する
        private void OnSaveCompleteClosed()
        {
            if (!hasSavedOnConfirmCanvas)
            {
                return;
            }

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
            if (saveConfirmView != null)
            {
                saveConfirmView.gameObject.SetActive(true);
            }

            saveConfirmView?.Clear();
            SetConfirmCanvasButtonsVisible(previewVisible: false);
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

        private void SetConfirmCanvasButtonsVisible(bool previewVisible)
        {
            if (confirmSaveButton != null)
            {
                confirmSaveButton.gameObject.SetActive(previewVisible);
            }

            if (confirmBackButton != null)
            {
                confirmBackButton.gameObject.SetActive(previewVisible);
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
                || saveCompleteView == null
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
            CanvasVisibilityUtility.SetCanvasEnabled(canvas, isEnabled, SaveCanvasSortingOrder);
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

        private string ResolveEmptySlotLabel()
        {
            return LocalizedText.GetOrFallback(GameTextKeys.CommonEmpty, emptySlotLabel);
        }

        private string ResolveSaveCompleteMessage()
        {
            return LocalizedText.GetOrFallback(GameTextKeys.SaveComplete, saveCompleteMessage);
        }

        private void OnDestroy()
        {
            ClearRuntimeThumbnails();
            savedSubject.Dispose();
            isSaveUiOpen.Dispose();
        }
    }
}