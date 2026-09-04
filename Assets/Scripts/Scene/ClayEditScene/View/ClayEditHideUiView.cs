using Extensions;
using GameData;
using Localization;
using R3;
using Scene.ClayEditScene.Interface;
using TMPro;
using UI.ClayEditor.ViewModel;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Scene.ClayEditScene.View
{
    /// <summary>
    /// ClayEditの成形ペイント中に編集UIを隠す
    /// 専用ToggleとCanvasだけを残して操作を継続する
    /// </summary>
    public sealed class ClayEditHideUiView : MonoBehaviour, ILanguageAwareUi
    {
        [Inject] private readonly ClayEditModeViewModel viewModel;
        [Inject] private readonly IClayEditEditorUiGate editorUiGate;

        [SerializeField] private Canvas canvas;
        [SerializeField] private Toggle hideUiToggle;
        [SerializeField] private TMP_Text hideUiToggleLabel;

        private bool hideUiOriginalCaptured;
        private string hideUiOriginal = "UIを非表示";

        private void Start()
        {
            if (canvas == null || hideUiToggle == null || hideUiToggleLabel == null)
            {
                Debug.LogError(
                    "[ClayEditHideUiView]必須SerializeFieldが未配線ですHierarchyで接続してください",
                    this);
                return;
            }

            ApplyLocalizedLabels();

            hideUiToggle.SetIsOnWithoutNotify(viewModel.IsEditorUiHidden.CurrentValue);
            hideUiToggle.OnValueChangedAsObservable()
                .Subscribe(isOn => viewModel.SetEditorUiHidden(isOn))
                .AddTo(this);

            // 再入場リセット時もトグル見た目をViewModelに追従させる
            viewModel.IsEditorUiHidden
                .Subscribe(hidden => hideUiToggle.SetIsOnWithoutNotify(hidden))
                .AddTo(this);

            Observable.CombineLatest(
                    viewModel.CurrentMode,
                    viewModel.HasModel,
                    viewModel.IsEditorUiHidden,
                    (mode, hasModel, hidden) => (mode, hasModel, hidden))
                .Subscribe(state => UpdateVisibility(state.mode, state.hasModel, state.hidden))
                .AddTo(this);
        }

        private void OnDisable()
        {
            if (editorUiGate != null && canvas != null)
            {
                editorUiGate.SetEditorCanvasesVisible(true, canvas);
            }
        }

        /// <inheritdoc/>
        public void RefreshLocalizedUi()
        {
            ApplyLocalizedLabels();
        }

        private void ApplyLocalizedLabels()
        {
            if (hideUiToggleLabel == null)
            {
                return;
            }

            if (hideUiOriginalCaptured == false)
            {
                hideUiOriginal = SceneLocalizedLabel.Capture(hideUiToggleLabel, hideUiOriginal);
                hideUiOriginalCaptured = true;
            }

            LhButtonLabelUtility.SetLabel(
                hideUiToggleLabel,
                SceneLocalizedLabel.Resolve(GameTextKeys.ClayEditHideUi, hideUiOriginal));
        }

        private void UpdateVisibility(EditModeType mode, bool hasModel, bool hidden)
        {
            bool inTargetMode = hasModel && (mode is EditModeType.Clay or EditModeType.Paint);
            canvas.enabled = inTargetMode;

            bool showOtherUi = inTargetMode == false || hidden == false;
            editorUiGate.SetEditorCanvasesVisible(showOtherUi, canvas);
        }
    }
}
