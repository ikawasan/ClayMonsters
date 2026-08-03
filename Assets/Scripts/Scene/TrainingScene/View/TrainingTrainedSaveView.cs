using ClayEditor.Rigging;
using Cysharp.Threading.Tasks;
using Extensions;
using LighthouseExtends.UIComponent.Button;
using Localization;
using SaveData;
using SaveData.Interface;
using Scene.TrainingScene.Domain;
using Scene.TrainingScene.Interface;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UI.ClayEditor.View;
using UnityEngine;
using UnityEngine.Serialization;
using VContainer;

namespace Scene.TrainingScene.View
{
    /// <summary>
    /// 育成完了保存と育成開始時の継承元選択を担うUI
    /// </summary>
    public sealed class TrainingTrainedSaveView :
        MonoBehaviour,
        ITrainingTrainedSaveView,
        ITrainingInheritanceSelectView,
        ILanguageAwareUi
    {
        private enum UiMode
        {
            Idle,
            Save,
            Inheritance
        }

        [FormerlySerializedAs("selectionCanvas")]
        [FormerlySerializedAs("rootGroup")]
        [SerializeField] private Canvas rootCanvas;
        [SerializeField] private TMP_Text headerText;
        [SerializeField] private ModelSaveSlotScrollListView slotScrollList;
        [SerializeField] private TrainedSaveSlotGridView trainedSlotGrid;

        [FormerlySerializedAs("confirmCanvas")]
        [FormerlySerializedAs("confirmPanel")]
        [SerializeField] private GameObject confirmPanelRoot;
        [SerializeField] private ModelSaveConfirmView confirmView;
        [SerializeField] private TMP_Text confirmMessageText;
        [SerializeField] private LHButton saveButton;
        [SerializeField] private TMP_Text saveButtonLabel;
        [SerializeField] private LHButton backButton;
        [SerializeField] private TMP_Text backButtonLabel;
        [SerializeField] private LHButton backToTitleButton;
        [SerializeField] private TMP_Text backToTitleButtonLabel;

        [Inject] private readonly IClayModelSaveService saveService;

        private readonly List<Object> runtimeThumbnailObjects = new List<Object>();

        private bool isInitialized;
        private UiMode uiMode = UiMode.Idle;
        private bool isConfirmed;
        private bool isCancelled;
        private int confirmedSlotIndex = -1;
        private int selectedSlotIndex = -1;
        private int inheritanceParentA = -1;
        private int inheritanceParentB = -1;
        private string inheritanceHeaderPrompt = string.Empty;

        private string pendingModelName;
        private ModelStatus pendingStatus;
        private IReadOnlyList<MotionType> pendingAttacks;
        private byte[] pendingThumbnailPng;

        private void Awake()
        {
            Initialize();
        }

        /// <inheritdoc />
        public async UniTask<int> WaitForConfirmedSlotAsync(
            string modelName,
            ModelStatus status,
            IReadOnlyList<MotionType> attackMotions,
            byte[] thumbnailPng,
            CancellationToken cancellationToken)
        {
            Initialize();

            uiMode = UiMode.Save;
            pendingModelName = modelName;
            pendingStatus = status;
            pendingAttacks = attackMotions;
            pendingThumbnailPng = thumbnailPng;
            isConfirmed = false;
            isCancelled = false;
            confirmedSlotIndex = -1;
            selectedSlotIndex = -1;
            inheritanceParentA = -1;
            inheritanceParentB = -1;

            HideHeaderText();
            ApplySaveModeButtonLabels();
            ClearInheritanceHoverHandlers();
            RefreshSlotList(allowEmptySlotSelection: true);
            ShowSelection();

            await UniTask.WaitUntil(
                () => isConfirmed || isCancelled,
                cancellationToken: cancellationToken);

            HideAll();
            uiMode = UiMode.Idle;
            return isCancelled ? -1 : confirmedSlotIndex;
        }

        /// <inheritdoc />
        public async UniTask<(int parentSlotA, int parentSlotB)> WaitForParentsAsync(
            CancellationToken cancellationToken)
        {
            Initialize();

            uiMode = UiMode.Inheritance;
            isConfirmed = false;
            isCancelled = false;
            confirmedSlotIndex = -1;
            selectedSlotIndex = -1;
            inheritanceParentA = -1;
            inheritanceParentB = -1;
            pendingModelName = null;
            pendingStatus = null;
            pendingAttacks = null;
            pendingThumbnailPng = null;

            SetInheritanceHeaderPrompt(
                LocalizedText.GetOrFallback(
                    GameTextKeys.TrainingInheritancePickHeader1,
                    "継承する育成済みモンスターを2体選んでください(1体目)"));
            ApplyInheritanceModeButtonLabels();
            RefreshSlotList(allowEmptySlotSelection: false);
            BindInheritanceHoverHandlers();
            ShowSelection();

            await UniTask.WaitUntil(
                () => isConfirmed || isCancelled,
                cancellationToken: cancellationToken);

            ClearInheritanceHoverHandlers();
            // 暗転前にCanvasを落とすと背景が露出するためHideはFlow側のFadeOut後に行う
            if (isCancelled || inheritanceParentA < 0 || inheritanceParentB < 0)
            {
                return (-1, -1);
            }

            return (inheritanceParentA, inheritanceParentB);
        }

        /// <inheritdoc />
        public void HideForLeave()
        {
            HideAll();
            uiMode = UiMode.Idle;
        }

        private void OnSlotSelected(int slotIndex)
        {
            if (uiMode == UiMode.Inheritance)
            {
                OnInheritanceSlotSelected(slotIndex);
                return;
            }

            selectedSlotIndex = slotIndex;
            OpenSaveConfirm();
        }

        private void OnInheritanceSlotSelected(int slotIndex)
        {
            if (saveService == null)
            {
                return;
            }

            ModelSaveSlot slot = saveService.GetSlot(ModelSavePool.TrainedPlayer, slotIndex);
            if (slot == null || !slot.isUsed)
            {
                return;
            }

            if (inheritanceParentA >= 0 && slotIndex == inheritanceParentA)
            {
                return;
            }

            selectedSlotIndex = slotIndex;
            OpenInheritanceParentStatusConfirm(slotIndex);
        }

        private void OnSaveClicked()
        {
            if (uiMode == UiMode.Inheritance)
            {
                OnInheritanceConfirmClicked();
                return;
            }

            if (selectedSlotIndex < 0)
            {
                return;
            }

            confirmedSlotIndex = selectedSlotIndex;
            isConfirmed = true;
        }

        private void OnInheritanceConfirmClicked()
        {
            if (selectedSlotIndex < 0)
            {
                return;
            }

            if (inheritanceParentA < 0)
            {
                inheritanceParentA = selectedSlotIndex;
                selectedSlotIndex = -1;
                confirmView?.Clear();
                CanvasVisibilityUtility.SetPanelActive(confirmPanelRoot, false);
                SetInheritanceHeaderPrompt(
                    LocalizedText.GetOrFallback(
                        GameTextKeys.TrainingInheritancePickHeader2,
                        "継承する育成済みモンスターを2体選んでください(2体目)"));
                SetSelectionContentVisible(true);
                return;
            }

            if (selectedSlotIndex == inheritanceParentA)
            {
                return;
            }

            inheritanceParentB = selectedSlotIndex;
            isConfirmed = true;
        }

        private void OnBackClicked()
        {
            if (uiMode == UiMode.Inheritance)
            {
                OnInheritanceBackClicked();
                return;
            }

            selectedSlotIndex = -1;
            confirmView?.Clear();
            CanvasVisibilityUtility.SetPanelActive(confirmPanelRoot, false);
            SetSelectionContentVisible(true);
        }

        private void OnInheritanceBackClicked()
        {
            bool confirmVisible = confirmPanelRoot != null && confirmPanelRoot.activeSelf;
            if (confirmVisible)
            {
                selectedSlotIndex = -1;
                confirmView?.Clear();
                CanvasVisibilityUtility.SetPanelActive(confirmPanelRoot, false);
                SetInheritanceHeaderPrompt(
                    inheritanceParentA < 0
                        ? LocalizedText.GetOrFallback(
                            GameTextKeys.TrainingInheritancePickHeader1,
                            "継承する育成済みモンスターを2体選んでください(1体目)")
                        : LocalizedText.GetOrFallback(
                            GameTextKeys.TrainingInheritancePickHeader2,
                            "継承する育成済みモンスターを2体選んでください(2体目)"));
                SetSelectionContentVisible(true);
                return;
            }

            if (inheritanceParentA >= 0)
            {
                inheritanceParentA = -1;
                inheritanceParentB = -1;
                selectedSlotIndex = -1;
                SetInheritanceHeaderPrompt(
                    LocalizedText.GetOrFallback(
                        GameTextKeys.TrainingInheritancePickHeader1,
                        "継承する育成済みモンスターを2体選んでください(1体目)"));
                return;
            }

            isCancelled = true;
        }

        private void OnBackToTitleClicked()
        {
            isCancelled = true;
        }

        private void OpenSaveConfirm()
        {
            if (selectedSlotIndex < 0 || saveService == null)
            {
                return;
            }

            EnsureVisibleRoot();

            ModelSaveSlot existingSlot = saveService.GetSlot(ModelSavePool.TrainedPlayer, selectedSlotIndex);
            bool willOverwrite = existingSlot != null;

            if (confirmMessageText != null)
            {
                int slotNumber = selectedSlotIndex + 1;
                confirmMessageText.text = willOverwrite
                    ? LocalizedText.Get(GameTextKeys.TrainingSaveOverwrite, "slot", slotNumber)
                    : LocalizedText.Get(GameTextKeys.TrainingSaveTo, "slot", slotNumber);
            }

            SetSelectionContentVisible(false);
            CanvasVisibilityUtility.SetPanelActive(confirmPanelRoot, true);
            if (confirmPanelRoot != null)
            {
                confirmPanelRoot.transform.SetAsLastSibling();
            }

            ApplySaveModeButtonLabels();
            confirmView?.ShowPreview(
                pendingModelName,
                pendingStatus,
                pendingAttacks,
                pendingThumbnailPng);
        }

        private void OpenInheritanceParentStatusConfirm(int slotIndex)
        {
            if (slotIndex < 0 || saveService == null)
            {
                return;
            }

            ModelSaveSlot slot = saveService.GetSlot(ModelSavePool.TrainedPlayer, slotIndex);
            if (slot == null)
            {
                return;
            }

            EnsureVisibleRoot();

            int pickNumber = inheritanceParentA < 0 ? 1 : 2;
            if (confirmMessageText != null)
            {
                confirmMessageText.text = LocalizedText.GetOrFallback(
                    GameTextKeys.TrainingInheritanceConfirmPick,
                    "継承元{pickNumber}体目\nステータスを確認して決定してください",
                    "pickNumber",
                    pickNumber);
            }

            SetSelectionContentVisible(false);
            CanvasVisibilityUtility.SetPanelActive(confirmPanelRoot, true);
            if (confirmPanelRoot != null)
            {
                confirmPanelRoot.transform.SetAsLastSibling();
            }

            ApplyInheritanceModeButtonLabels();
            byte[] thumbnailPng = ModelSaveStorage.ReadThumbnailPng(slot);
            confirmView?.ShowSlot(slot, thumbnailPng, slotIndex);
        }

        private void ApplySaveModeButtonLabels()
        {
            LhButtonLabelUtility.SetLabel(
                saveButtonLabel,
                LocalizedText.GetOrFallback(GameTextKeys.CommonSave, "保存"));
            LhButtonLabelUtility.SetLabel(
                backButtonLabel,
                LocalizedText.GetOrFallback(GameTextKeys.CommonReturn, "戻る"));
            LhButtonLabelUtility.SetLabel(
                backToTitleButtonLabel,
                LocalizedText.GetOrFallback(GameTextKeys.TrainingHudBackToTitle, "タイトル"));
        }

        /// <inheritdoc/>
        public void RefreshLocalizedUi()
        {
            if (uiMode == UiMode.Save)
            {
                ApplySaveModeButtonLabels();
                RefreshSlotList(allowEmptySlotSelection: true);
                if (confirmPanelRoot != null && confirmPanelRoot.activeSelf)
                {
                    OpenSaveConfirm();
                }

                return;
            }

            if (uiMode == UiMode.Inheritance)
            {
                ApplyInheritanceModeButtonLabels();
                SetInheritanceHeaderPrompt(
                    inheritanceParentA < 0
                        ? LocalizedText.GetOrFallback(
                            GameTextKeys.TrainingInheritancePickHeader1,
                            "継承する育成済みモンスターを2体選んでください(1体目)")
                        : LocalizedText.GetOrFallback(
                            GameTextKeys.TrainingInheritancePickHeader2,
                            "継承する育成済みモンスターを2体選んでください(2体目)"));
                RefreshSlotList(allowEmptySlotSelection: false);
                if (confirmPanelRoot != null
                    && confirmPanelRoot.activeSelf
                    && selectedSlotIndex >= 0)
                {
                    OpenInheritanceParentStatusConfirm(selectedSlotIndex);
                }
            }
        }

        private void ApplyInheritanceModeButtonLabels()
        {
            LhButtonLabelUtility.SetLabel(
                saveButtonLabel,
                LocalizedText.GetOrFallback(GameTextKeys.CommonDecide, "決定"));
            LhButtonLabelUtility.SetLabel(
                backButtonLabel,
                LocalizedText.GetOrFallback(GameTextKeys.CommonReturn, "戻る"));
            LhButtonLabelUtility.SetLabel(
                backToTitleButtonLabel,
                LocalizedText.GetOrFallback(GameTextKeys.CommonReturn, "戻る"));
        }

        private void RefreshSlotList(bool allowEmptySlotSelection)
        {
            if (saveService == null)
            {
                return;
            }

            if (!EnsureTrainedSlotGrid())
            {
                Debug.LogError(
                    "[TrainingTrainedSaveView] trainedSlotGridが未配置ですResources/UI/TrainedSaveSlotGridをHierarchyへ配置してください",
                    this);
                return;
            }

            HideLegacySlotScrollList();
            ClearRuntimeThumbnails();
            trainedSlotGrid.Show();
            trainedSlotGrid.Initialize(OnSlotSelected);
            trainedSlotGrid.Refresh(
                saveService,
                Localization.LocalizedText.GetOrFallback(
                    Localization.GameTextKeys.CommonEmpty,
                    "空き"),
                allowEmptySlotSelection);
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

        private void HideLegacySlotScrollList()
        {
            if (slotScrollList == null)
            {
                return;
            }

            slotScrollList.gameObject.SetActive(false);
        }

        private void ShowSelection()
        {
            EnsureVisibleRoot();
            confirmView?.Clear();
            CanvasVisibilityUtility.SetPanelActive(confirmPanelRoot, false);
            SetSelectionContentVisible(true);
            if (uiMode == UiMode.Inheritance)
            {
                ApplyHeaderVisible();
            }
        }

        private void HideAll()
        {
            ClearInheritanceHoverHandlers();
            trainedSlotGrid?.Hide();
            HideLegacySlotScrollList();
            confirmView?.Clear();
            CanvasVisibilityUtility.SetPanelActive(confirmPanelRoot, false);
            SetSelectionContentVisible(false);
            CanvasVisibilityUtility.SetCanvasEnabled(rootCanvas, false);
            gameObject.SetActive(false);
        }

        private void BindInheritanceHoverHandlers()
        {
            if (!EnsureTrainedSlotGrid())
            {
                return;
            }

            trainedSlotGrid.SetHoverHandlers(OnInheritanceSlotHovered, OnInheritanceSlotHoverExited);
        }

        private void ClearInheritanceHoverHandlers()
        {
            trainedSlotGrid?.SetHoverHandlers(null, null);
        }

        private void OnInheritanceSlotHovered(int slotIndex)
        {
            if (uiMode != UiMode.Inheritance || saveService == null)
            {
                return;
            }

            if (inheritanceParentA >= 0 && slotIndex == inheritanceParentA)
            {
                return;
            }

            ModelSaveSlot slot = saveService.GetSlot(ModelSavePool.TrainedPlayer, slotIndex);
            string preview = TrainingInheritanceResolver.FormatParentStatGainPreview(slot);
            if (string.IsNullOrEmpty(preview))
            {
                return;
            }

            SetHeaderText(preview);
        }

        private void OnInheritanceSlotHoverExited()
        {
            if (uiMode != UiMode.Inheritance)
            {
                return;
            }

            SetHeaderText(inheritanceHeaderPrompt);
        }

        private void SetInheritanceHeaderPrompt(string text)
        {
            inheritanceHeaderPrompt = text ?? string.Empty;
            SetHeaderText(inheritanceHeaderPrompt);
        }

        private void EnsureVisibleRoot()
        {
            if (transform is RectTransform rectTransform)
            {
                if (rectTransform.localScale == Vector3.zero)
                {
                    rectTransform.localScale = Vector3.one;
                }

                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
                rectTransform.anchoredPosition = Vector2.zero;
                rectTransform.offsetMin = Vector2.zero;
                rectTransform.offsetMax = Vector2.zero;
            }

            EnsureRootReferences();
            EnsureRootCanvasEnabled();
            CanvasVisibilityUtility.SetCanvasEnabled(rootCanvas, true);
            gameObject.SetActive(true);
        }

        private void SetSelectionContentVisible(bool visible)
        {
            Transform confirmRoot = confirmPanelRoot != null ? confirmPanelRoot.transform : null;
            Transform scrollListRoot = slotScrollList != null ? slotScrollList.transform : null;
            Transform gridRoot = trainedSlotGrid != null ? trainedSlotGrid.transform : null;
            bool useGrid = EnsureTrainedSlotGrid();

            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (confirmRoot != null && child == confirmRoot)
                {
                    continue;
                }

                if (headerText != null && child == headerText.transform)
                {
                    continue;
                }

                // グリッド利用時は旧スクロール一覧を常に非表示にする
                if (useGrid && scrollListRoot != null && child == scrollListRoot)
                {
                    child.gameObject.SetActive(false);
                    continue;
                }

                // グリッド本体はShow/Hideで制御する
                if (useGrid && gridRoot != null && child == gridRoot)
                {
                    continue;
                }

                child.gameObject.SetActive(visible);
            }

            if (useGrid)
            {
                if (visible)
                {
                    trainedSlotGrid.Show();
                }
                else
                {
                    trainedSlotGrid.Hide();
                }
            }

            if (uiMode == UiMode.Inheritance)
            {
                ApplyHeaderVisible();
            }
            else
            {
                HideHeaderText();
            }
        }

        private void SetHeaderText(string text)
        {
            if (headerText == null)
            {
                return;
            }

            headerText.text = text ?? string.Empty;
            ApplyHeaderVisible();
        }

        private void ApplyHeaderVisible()
        {
            if (headerText == null)
            {
                return;
            }

            bool hasText = !string.IsNullOrEmpty(headerText.text);
            headerText.enabled = hasText;
            headerText.gameObject.SetActive(hasText);
        }

        private void HideHeaderText()
        {
            if (headerText == null)
            {
                return;
            }

            headerText.text = string.Empty;
            headerText.enabled = false;
            headerText.gameObject.SetActive(false);
        }

        private void Initialize()
        {
            if (isInitialized)
            {
                return;
            }

            EnsureRootReferences();
            HideHeaderText();
            if (EnsureTrainedSlotGrid())
            {
                trainedSlotGrid.Initialize(OnSlotSelected);
            }

            saveButton?.SubscribeOnClick(OnSaveClicked);
            backButton?.SubscribeOnClick(OnBackClicked);
            backToTitleButton?.SubscribeOnClick(OnBackToTitleClicked);
            ValidateSerializedReferences();
            isInitialized = true;
        }

        private void EnsureRootReferences()
        {
            if (rootCanvas == null)
            {
                rootCanvas = GetComponent<Canvas>();
            }
        }

        private void ValidateSerializedReferences()
        {
            if (rootCanvas == null)
            {
                Debug.LogError(
                    "[TrainingTrainedSaveView] rootCanvasが未設定ですHierarchyで接続してください",
                    this);
            }

            if (trainedSlotGrid == null)
            {
                Debug.LogError(
                    "[TrainingTrainedSaveView] trainedSlotGridが未設定ですHierarchyで接続してください",
                    this);
            }

            if (confirmPanelRoot == null)
            {
                Debug.LogError(
                    "[TrainingTrainedSaveView] confirmPanelRootが未設定ですHierarchyで接続してください",
                    this);
            }

            if (confirmView == null)
            {
                Debug.LogError(
                    "[TrainingTrainedSaveView] confirmViewが未設定ですHierarchyで接続してください",
                    this);
            }

            if (confirmMessageText == null)
            {
                Debug.LogError(
                    "[TrainingTrainedSaveView] confirmMessageTextが未設定ですHierarchyで接続してください",
                    this);
            }

            if (saveButton == null)
            {
                Debug.LogError(
                    "[TrainingTrainedSaveView] saveButtonが未設定ですHierarchyで接続してください",
                    this);
            }

            if (saveButtonLabel == null)
            {
                Debug.LogError(
                    "[TrainingTrainedSaveView] saveButtonLabelが未設定ですHierarchyで接続してください",
                    this);
            }

            if (backButton == null)
            {
                Debug.LogError(
                    "[TrainingTrainedSaveView] backButtonが未設定ですHierarchyで接続してください",
                    this);
            }

            if (backButtonLabel == null)
            {
                Debug.LogError(
                    "[TrainingTrainedSaveView] backButtonLabelが未設定ですHierarchyで接続してください",
                    this);
            }

            if (backToTitleButton == null)
            {
                Debug.LogError(
                    "[TrainingTrainedSaveView] backToTitleButtonが未設定ですHierarchyで接続してください",
                    this);
            }

            if (backToTitleButtonLabel == null)
            {
                Debug.LogError(
                    "[TrainingTrainedSaveView] backToTitleButtonLabelが未設定ですHierarchyで接続してください",
                    this);
            }
        }

        private void EnsureRootCanvasEnabled()
        {
            if (rootCanvas != null && !rootCanvas.enabled)
            {
                rootCanvas.enabled = true;
            }
        }

        private void ClearRuntimeThumbnails()
        {
            for (int i = 0; i < runtimeThumbnailObjects.Count; i++)
            {
                Object obj = runtimeThumbnailObjects[i];
                if (obj != null)
                {
                    Destroy(obj);
                }
            }

            runtimeThumbnailObjects.Clear();
        }
    }
}
