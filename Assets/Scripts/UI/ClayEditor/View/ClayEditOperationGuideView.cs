using Extensions;
using GameData;
using R3;
using TMPro;
using UI.ClayEditor.ViewModel;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace UI.ClayEditor.View
{
    /// <summary>
    /// ClayEditシーンの成形・ペイント操作説明を表示する
    /// シーン上のCanvasとTMP_Textを参照しToggleで表示・非表示を切り替える
    /// </summary>
    public sealed class ClayEditOperationGuideView : MonoBehaviour
    {
        [Inject] private readonly ClayEditModeViewModel viewModel;

        [SerializeField] private Canvas canvas;
        [SerializeField] private Toggle visibilityToggle;
        [SerializeField] private RectTransform chevronIcon;
        [SerializeField] private GameObject clayGuidePanel;
        [SerializeField] private GameObject paintGuidePanel;
        [SerializeField] private TMP_Text clayGuideText;
        [SerializeField] private TMP_Text paintGuideText;

        private void Start()
        {
            if (visibilityToggle != null)
            {
                Transform legacyCheckmark = visibilityToggle.transform.Find("Background/Checkmark");
                if (legacyCheckmark != null)
                {
                    legacyCheckmark.gameObject.SetActive(false);
                }

                visibilityToggle.graphic = null;
                visibilityToggle.toggleTransition = Toggle.ToggleTransition.None;
            }

            ApplyGuideTexts();

            bool isVisible = viewModel.IsGuideVisible.CurrentValue;
            visibilityToggle.SetIsOnWithoutNotify(isVisible);
            UpdateChevron(isVisible);

            visibilityToggle.OnValueChangedAsObservable()
                .Subscribe(isOn =>
                {
                    viewModel.SetGuideVisible(isOn);
                    UpdateChevron(isOn);
                })
                .AddTo(this);

            Observable.CombineLatest(
                    viewModel.CurrentMode,
                    viewModel.HasModel,
                    viewModel.IsGuideVisible,
                    (mode, hasModel, visible) => (mode, hasModel, visible))
                .Subscribe(state => UpdateVisibility(state.mode, state.hasModel, state.visible))
                .AddTo(this);
        }

        private void ApplyGuideTexts()
        {
            ResolveGuideTextsIfNeeded();
            if (clayGuideText != null)
            {
                InputIconTmpUtility.ApplySpriteAsset(clayGuideText);
                clayGuideText.text = InputGuideTexts.ClayEditClayGuide;
            }
            else
            {
                Debug.LogError(
                    "[ClayEditOperationGuideView] clayGuideTextが未配線です",
                    this);
            }

            if (paintGuideText != null)
            {
                InputIconTmpUtility.ApplySpriteAsset(paintGuideText);
                paintGuideText.text = InputGuideTexts.ClayEditPaintGuide;
            }
            else
            {
                Debug.LogError(
                    "[ClayEditOperationGuideView] paintGuideTextが未配線です",
                    this);
            }
        }

        private void ResolveGuideTextsIfNeeded()
        {
            if (clayGuideText == null && clayGuidePanel != null)
            {
                clayGuideText = clayGuidePanel.GetComponentInChildren<TMP_Text>(true);
            }

            if (paintGuideText == null && paintGuidePanel != null)
            {
                paintGuideText = paintGuidePanel.GetComponentInChildren<TMP_Text>(true);
            }
        }

        private void UpdateVisibility(EditModeType mode, bool hasModel, bool visible)
        {
            bool inTargetMode = hasModel && (mode is EditModeType.Clay or EditModeType.Paint);
            canvas.enabled = inTargetMode;

            bool showText = inTargetMode && visible;
            clayGuidePanel.SetActive(showText && mode == EditModeType.Clay);
            paintGuidePanel.SetActive(showText && mode == EditModeType.Paint);
        }

        private void UpdateChevron(bool isOpen)
        {
            if (chevronIcon == null)
            {
                return;
            }

            chevronIcon.localEulerAngles = isOpen ? new Vector3(0f, 0f, -90f) : Vector3.zero;
        }
    }
}
