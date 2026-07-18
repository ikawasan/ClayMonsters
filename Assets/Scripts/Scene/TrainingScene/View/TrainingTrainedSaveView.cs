using ClayEditor.Rigging;

using Cysharp.Threading.Tasks;

using Extensions;

using LighthouseExtends.UIComponent.Button;

using SaveData;

using SaveData.Interface;

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

    /// 育成完了後に育成済みスロットを選んで保存するUI

    /// </summary>

    public sealed class TrainingTrainedSaveView : MonoBehaviour, ITrainingTrainedSaveView

    {

        [FormerlySerializedAs("selectionCanvas")]
        [FormerlySerializedAs("rootGroup")]
        [SerializeField] private Canvas rootCanvas;

        [SerializeField] private TMP_Text headerText;

        [SerializeField] private ModelSaveSlotScrollListView slotScrollList;

        [FormerlySerializedAs("confirmCanvas")]
        [FormerlySerializedAs("confirmPanel")]
        [SerializeField] private GameObject confirmPanelRoot;

        [SerializeField] private ModelSaveConfirmView confirmView;

        [SerializeField] private TMP_Text confirmMessageText;

        [SerializeField] private LHButton saveButton;

        [SerializeField] private LHButton backButton;

        [SerializeField] private LHButton backToTitleButton;



        [Inject] private readonly IClayModelSaveService saveService;



        private readonly List<Object> runtimeThumbnailObjects = new List<Object>();



        private bool isInitialized;

        private bool isConfirmed;

        private bool isBackToTitleRequested;

        private int confirmedSlotIndex = -1;

        private int selectedSlotIndex = -1;



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

            pendingModelName = modelName;

            pendingStatus = status;

            pendingAttacks = attackMotions;

            pendingThumbnailPng = thumbnailPng;

            isConfirmed = false;

            isBackToTitleRequested = false;

            confirmedSlotIndex = -1;

            selectedSlotIndex = -1;



            HideHeaderText();

            RefreshSlotList();

            ShowSelection();



            await UniTask.WaitUntil(() => isConfirmed || isBackToTitleRequested, cancellationToken: cancellationToken);



            HideAll();

            return isBackToTitleRequested ? -1 : confirmedSlotIndex;

        }



        /// <inheritdoc />

        public void HideForLeave()

        {

            HideAll();

        }



        private void OnSlotSelected(int slotIndex)

        {

            selectedSlotIndex = slotIndex;

            OpenConfirm();

        }



        private void OnSaveClicked()

        {

            if (selectedSlotIndex < 0)

            {

                return;

            }



            confirmedSlotIndex = selectedSlotIndex;

            isConfirmed = true;

        }



        private void OnBackClicked()

        {

            selectedSlotIndex = -1;

            confirmView?.Clear();

            CanvasVisibilityUtility.SetPanelActive(confirmPanelRoot, false);

            SetSelectionContentVisible(true);

        }



        private void OnBackToTitleClicked()

        {

            isBackToTitleRequested = true;

        }



        private void OpenConfirm()

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

                    ? $"育成済みスロット{slotNumber}のデータを上書き保存します"

                    : $"育成済みスロット{slotNumber}へ保存します";

            }



            SetSelectionContentVisible(false);

            CanvasVisibilityUtility.SetPanelActive(confirmPanelRoot, true);

            if (confirmPanelRoot != null)

            {

                confirmPanelRoot.transform.SetAsLastSibling();

            }



            confirmView?.ShowPreview(

                pendingModelName,

                pendingThumbnailPng,

                selectedSlotIndex,

                pendingStatus,

                pendingAttacks);

        }



        private void RefreshSlotList()

        {

            if (slotScrollList == null || saveService == null)

            {

                return;

            }



            ClearRuntimeThumbnails();

            slotScrollList.RefreshSlots(

                ModelSavePool.TrainedPlayer,

                saveService,

                "空き",

                runtimeThumbnailObjects,

                allowEmptySlotSelection: true);

            slotScrollList.RefreshHostLayout();

        }



        private void ShowSelection()

        {

            EnsureVisibleRoot();

            confirmView?.Clear();

            CanvasVisibilityUtility.SetPanelActive(confirmPanelRoot, false);

            SetSelectionContentVisible(true);

        }



        private void HideAll()

        {

            confirmView?.Clear();

            CanvasVisibilityUtility.SetPanelActive(confirmPanelRoot, false);

            SetSelectionContentVisible(false);

            CanvasVisibilityUtility.SetCanvasEnabled(rootCanvas, false);

            gameObject.SetActive(false);

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



                child.gameObject.SetActive(visible);

            }



            HideHeaderText();

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

            EnsureConfirmPanel();

            WireBackToTitleButtonReference();

            HideHeaderText();

            slotScrollList = ModelSaveSlotScrollListRuntimeUtility.EnsureHostUnderTransform(
                transform,
                slotScrollList);

            slotScrollList?.Initialize(OnSlotSelected);

            saveButton?.SubscribeOnClick(OnSaveClicked);

            backButton?.SubscribeOnClick(OnBackClicked);

            backToTitleButton?.SubscribeOnClick(OnBackToTitleClicked);

            ValidateSceneReferences();

            isInitialized = true;

        }



        private void EnsureRootReferences()

        {

            if (rootCanvas == null)

            {

                rootCanvas = GetComponent<Canvas>();

            }

        }



        private void EnsureConfirmPanel()

        {

            if (confirmPanelRoot != null)

            {

                return;

            }



            Transform confirmRoot = transform.Find("ConfirmPanel")

                ?? transform.Find("TrainingTrainedSaveConfirmCanvas");

            if (confirmRoot == null)

            {

                return;

            }



            confirmPanelRoot = confirmRoot.gameObject;

        }



        private void WireBackToTitleButtonReference()

        {

            if (backToTitleButton != null)

            {

                return;

            }



            backToTitleButton = TrainingUiReferenceUtility.FindButton(transform, "BackToTitleButton");

        }



        private void ValidateSceneReferences()

        {

            if (backToTitleButton == null)

            {

                Debug.LogError(

                    "[TrainingTrainedSaveView] backToTitleButtonが未設定です。Hierarchyで参照を配線してください",

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

