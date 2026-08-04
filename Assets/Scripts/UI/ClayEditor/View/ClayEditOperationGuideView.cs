using Extensions;
using GameData;
using LighthouseExtends.TextTable;
using Localization;
using R3;
using System;
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
    public sealed class ClayEditOperationGuideView : MonoBehaviour, ILanguageAwareUi
    {
        [Inject] private readonly ClayEditModeViewModel viewModel;

        [SerializeField] private Canvas canvas;
        [SerializeField] private Toggle visibilityToggle;
        [Tooltip("操作説明の文言TMP(ToggleLabel)")]
        [SerializeField] private TMP_Text visibilityToggleLabel;
        [SerializeField] private RectTransform chevronIcon;
        [SerializeField] private GameObject clayGuidePanel;
        [SerializeField] private GameObject paintGuidePanel;
        [SerializeField] private TMP_Text clayGuideText;
        [SerializeField] private TMP_Text paintGuideText;

        private IDisposable languageSubscription;

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

            ApplyLocalizedLabels();
            SubscribeLanguageChange();

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

        private void OnDestroy()
        {
            languageSubscription?.Dispose();
            languageSubscription = null;
        }


        /// <inheritdoc/>
        public void RefreshLocalizedUi()
        {
            ApplyLocalizedLabels();
        }

        private void ApplyLocalizedLabels()
        {
            ApplyGuideTexts();
            TMP_Text label = ResolveVisibilityToggleLabel();
            if (label == null)
            {
                Debug.LogError(
                    "[ClayEditOperationGuideView] visibilityToggleLabel(ToggleLabel)が未配線です",
                    this);
                return;
            }

            CaptureGuideLabelOriginalIfNeeded();
            LhButtonLabelUtility.SetLabel(
                label,
                SceneLocalizedLabel.Resolve(GameTextKeys.ClayEditShowGuide, showGuideOriginal));
        }

        private bool guideLabelOriginalCaptured;
        private string showGuideOriginal = "操作説明を表示";

        private void CaptureGuideLabelOriginalIfNeeded()
        {
            if (guideLabelOriginalCaptured)
            {
                return;
            }

            TMP_Text label = ResolveVisibilityToggleLabel();
            showGuideOriginal = SceneLocalizedLabel.Capture(label, showGuideOriginal);
            guideLabelOriginalCaptured = true;
        }

        private TMP_Text ResolveVisibilityToggleLabel()
        {
            if (visibilityToggleLabel != null)
            {
                return visibilityToggleLabel;
            }

            if (visibilityToggle == null)
            {
                return null;
            }

            // GuidePanel全体がToggleのため最初の子TMPはChevronになる
            // ToggleLabelを名前一致で解決する
            TMP_Text[] texts = visibilityToggle.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (text != null && text.gameObject.name == "ToggleLabel")
                {
                    visibilityToggleLabel = text;
                    return visibilityToggleLabel;
                }
            }

            // 旧ベイク文言も含めて一致させる
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (text == null)
                {
                    continue;
                }

                string current = text.text.Trim();
                if (string.Equals(current, "操作", StringComparison.Ordinal)
                    || string.Equals(current, "操作説明", StringComparison.Ordinal)
                    || string.Equals(current, "操作説明を表示", StringComparison.Ordinal))
                {
                    visibilityToggleLabel = text;
                    return visibilityToggleLabel;
                }
            }

            return null;
        }

        private void SubscribeLanguageChange()
        {
            ITextTableService service = TextTableService.Instance;
            if (service == null)
            {
                return;
            }

            languageSubscription?.Dispose();
            languageSubscription = service.CurrentLanguage.Subscribe(_ => ApplyLocalizedLabels());
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
