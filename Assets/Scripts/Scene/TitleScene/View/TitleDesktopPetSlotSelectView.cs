using Extensions;
using LighthouseExtends.UIComponent.Button;
using Localization;
using R3;
using SaveData;
using SaveData.Interface;
using Scene.TitleScene.Interface;
using System.Collections.Generic;
using TMPro;
using UI.ClayEditor.View;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VContainer;

namespace Scene.TitleScene.View
{
    /// <summary>
    /// デスクトップペット用の未育成セーブスロット選択UI
    /// Canvas.enabledで表示切替する
    /// </summary>
    public sealed class TitleDesktopPetSlotSelectView : MonoBehaviour, ITitleDesktopPetSlotSelectView, ILanguageAwareUi
    {
        private const int SelectionCanvasSortingOrder = 500;
        private const int MaxSelectableCount = 5;

        [Inject] private readonly IClayModelSaveService saveService;

        [SerializeField] private Canvas canvas;
        [SerializeField] private ModelSaveSlotScrollListView slotScrollList;
        [SerializeField] private TMP_Text selectionInstructionText;
        [SerializeField] private LHButton listBackButton;
        [SerializeField] private LHButton listConfirmButton;
        [SerializeField] private Image inputBlocker;

        private readonly Subject<IReadOnlyList<int>> selectionConfirmedSubject = new Subject<IReadOnlyList<int>>();
        private readonly Subject<Unit> cancelledSubject = new Subject<Unit>();
        private readonly List<UnityEngine.Object> runtimeThumbnailObjects = new List<UnityEngine.Object>();
        private readonly List<int> selectedSlotIndices = new List<int>(MaxSelectableCount);

        private bool isInitialized;
        private bool isShowing;
        private string backOriginal = "戻る";
        private string confirmOriginal = "決定";
        private bool labelOriginalsCaptured;

        /// <inheritdoc/>
        public Observable<IReadOnlyList<int>> OnSelectionConfirmed => selectionConfirmedSubject;

        /// <inheritdoc/>
        public Observable<Unit> OnCancelled => cancelledSubject;

        private void Awake()
        {
            ValidateSceneUi();
            Hide();
        }

        private void Start()
        {
            if (saveService == null)
            {
                Debug.LogError(
                    "[TitleDesktopPetSlotSelectView] saveServiceが未注入です",
                    this);
                enabled = false;
                return;
            }

            EnsureInitialized();
            ApplyLocalizedLabels();
        }

        private void Update()
        {
            if (!isShowing)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
            {
                TryConfirmSelection();
            }
        }

        /// <inheritdoc/>
        public void Show()
        {
            if (saveService == null || slotScrollList == null || canvas == null)
            {
                Debug.LogError(
                    "[TitleDesktopPetSlotSelectView] 必須参照が未配線のため表示できません",
                    this);
                return;
            }

            EnsureInitialized();
            selectedSlotIndices.Clear();
            isShowing = true;
            ApplyLocalizedLabels();
            ApplySelectionCanvasSorting();
            ModelSaveSlotScrollListView.ApplySelectionCanvasLayout(canvas);
            ModelSaveSlotScrollListView.EnterFullscreenSelectionLayout(canvas);
            EnsureInputBlocker();
            RefreshSlots();
            ApplySelectionMarks();
            UpdateConfirmButtonInteractable();
            CanvasVisibilityUtility.SetCanvasEnabled(canvas, true);
        }

        /// <inheritdoc/>
        public void Hide()
        {
            isShowing = false;
            selectedSlotIndices.Clear();
            ClearRuntimeThumbnails();
            CanvasVisibilityUtility.SetCanvasEnabled(canvas, false);
            ModelSaveSlotScrollListView.ExitFullscreenSelectionLayout();
        }

        /// <inheritdoc/>
        public void RefreshLocalizedUi()
        {
            ApplyLocalizedLabels();
            if (isShowing)
            {
                RefreshSlots();
                ApplySelectionMarks();
            }
        }

        private void EnsureInitialized()
        {
            if (isInitialized)
            {
                return;
            }

            if (slotScrollList != null)
            {
                slotScrollList.Initialize(HandleSlotClicked);
            }

            if (listBackButton != null)
            {
                listBackButton.SubscribeOnClick(HandleCancelled);
            }

            if (listConfirmButton != null)
            {
                listConfirmButton.SubscribeOnClick(TryConfirmSelection);
            }
            else
            {
                Debug.LogWarning(
                    "[TitleDesktopPetSlotSelectView] listConfirmButtonが未配線ですEnterキーで決定できますHierarchyへ決定ボタンを配置し接続してください",
                    this);
            }

            if (inputBlocker != null)
            {
                inputBlocker.raycastTarget = true;
            }

            isInitialized = true;
        }

        private void EnsureInputBlocker()
        {
            if (inputBlocker == null)
            {
                Debug.LogError(
                    "[TitleDesktopPetSlotSelectView] inputBlockerが未配線です背面入力を防げません",
                    this);
                return;
            }

            inputBlocker.raycastTarget = true;
            RectTransform blockerRect = inputBlocker.rectTransform;
            if (blockerRect != null)
            {
                blockerRect.anchorMin = Vector2.zero;
                blockerRect.anchorMax = Vector2.one;
                blockerRect.offsetMin = Vector2.zero;
                blockerRect.offsetMax = Vector2.zero;
                blockerRect.SetAsFirstSibling();
            }

            if (inputBlocker.color.a < 0.01f)
            {
                Color color = inputBlocker.color;
                color.a = 1f;
                inputBlocker.color = color;
            }
        }

        private void HandleSlotClicked(int slotIndex)
        {
            if (saveService == null)
            {
                return;
            }

            ModelSaveSlot slot = saveService.GetSlot(ModelSavePool.Player, slotIndex);
            if (slot == null || string.IsNullOrEmpty(slot.glbFileName))
            {
                Debug.LogWarning(
                    $"[TitleDesktopPetSlotSelectView] スロット{slotIndex}は選択できません",
                    this);
                return;
            }

            int existingIndex = selectedSlotIndices.IndexOf(slotIndex);
            if (existingIndex >= 0)
            {
                selectedSlotIndices.RemoveAt(existingIndex);
            }
            else
            {
                if (selectedSlotIndices.Count >= MaxSelectableCount)
                {
                    Debug.LogWarning(
                        $"[TitleDesktopPetSlotSelectView] 選択上限{MaxSelectableCount}体です",
                        this);
                    ApplyLocalizedLabels();
                    return;
                }

                selectedSlotIndices.Add(slotIndex);
            }

            ApplySelectionMarks();
            ApplyLocalizedLabels();
            UpdateConfirmButtonInteractable();
        }

        private void TryConfirmSelection()
        {
            if (!isShowing || selectedSlotIndices.Count <= 0)
            {
                return;
            }

            selectionConfirmedSubject.OnNext(selectedSlotIndices.ToArray());
        }

        private void HandleCancelled()
        {
            cancelledSubject.OnNext(Unit.Default);
        }

        private void RefreshSlots()
        {
            if (slotScrollList == null || saveService == null)
            {
                return;
            }

            ClearRuntimeThumbnails();
            Canvas.ForceUpdateCanvases();
            slotScrollList.RefreshHostLayout();
            slotScrollList.RefreshSlots(
                ModelSavePool.Player,
                saveService,
                LocalizedText.GetOrFallback(GameTextKeys.CommonEmpty, "空き"),
                runtimeThumbnailObjects,
                allowEmptySlotSelection: false,
                ModelSaveSlotListContentMode.Full);
        }

        private void ApplySelectionMarks()
        {
            slotScrollList?.SetSelectionMarks(selectedSlotIndices);
            if (UnityEngine.EventSystems.EventSystem.current != null)
            {
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            }
        }

        private void UpdateConfirmButtonInteractable()
        {
            if (listConfirmButton == null)
            {
                return;
            }

            listConfirmButton.interactable = selectedSlotIndices.Count > 0;
        }

        private void ApplyLocalizedLabels()
        {
            CaptureLabelOriginalsIfNeeded();
            if (selectionInstructionText != null)
            {
                string template = LocalizedText.GetOrFallback(
                    GameTextKeys.TitleDesktopPetSelectInstruction,
                    "最大{0}体まで選択してください（{1}/{0}）");
                LocalizedFont.SetText(
                    selectionInstructionText,
                    string.Format(template, MaxSelectableCount, selectedSlotIndices.Count));
            }

            LhButtonLabelUtility.SetLabel(
                listBackButton,
                SceneLocalizedLabel.Resolve(GameTextKeys.CommonReturn, backOriginal));
            if (listConfirmButton != null)
            {
                LhButtonLabelUtility.SetLabel(
                    listConfirmButton,
                    SceneLocalizedLabel.Resolve(GameTextKeys.CommonDecide, confirmOriginal));
            }
        }

        private void CaptureLabelOriginalsIfNeeded()
        {
            if (labelOriginalsCaptured)
            {
                return;
            }

            backOriginal = SceneLocalizedLabel.Capture(listBackButton, backOriginal);
            if (listConfirmButton != null)
            {
                confirmOriginal = SceneLocalizedLabel.Capture(listConfirmButton, confirmOriginal);
            }

            labelOriginalsCaptured = true;
        }

        private void ApplySelectionCanvasSorting()
        {
            if (canvas == null)
            {
                return;
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = SelectionCanvasSortingOrder;
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

        private void OnDestroy()
        {
            ClearRuntimeThumbnails();
            selectionConfirmedSubject.Dispose();
            cancelledSubject.Dispose();
        }

        private void ValidateSceneUi()
        {
            if (canvas == null || slotScrollList == null || listBackButton == null)
            {
                Debug.LogError(
                    "[TitleDesktopPetSlotSelectView] シーン上のUI参照が未設定ですHierarchyでUI参照を確認してください",
                    this);
            }
        }
    }
}
