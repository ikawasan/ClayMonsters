using Extensions;
using GameData;
using Localization;
using R3;
using System.Collections.Generic;
using TMPro;
using UI.ClayEditor.ViewModel;
using UnityEngine;
using VContainer;

namespace UI.ClayEditor.View
{
    /// <summary>
    /// ClayEditの編集モード切替ドロップダウン表示
    /// </summary>
    public class ClayEditModeView : MonoBehaviour, ILanguageAwareUi
    {
        [Inject] private readonly ClayEditModeViewModel viewModel;

        [SerializeField] private TMP_Dropdown editModeUI;
        [SerializeField] private List<EditModeTypeObject> editModeTypeObjects;

        private void OnDisable()
        {
            if (editModeUI != null)
            {
                ClayEditModeDropdownLayout.Collapse(editModeUI);
            }
        }

        private void Start()
        {
            ApplyModeDropdownOptions();

            if (editModeUI != null)
            {
                editModeUI.onValueChanged.AsObservable()
                    .Subscribe(index => viewModel.ChangeMode((EditModeType)index))
                    .AddTo(this);
            }

            viewModel.CurrentMode
                .Subscribe(mode =>
                {
                    if (editModeUI != null)
                    {
                        editModeUI.SetValueWithoutNotify((int)mode);
                        editModeUI.RefreshShownValue();
                    }
                })
                .AddTo(this);

            Observable.CombineLatest(
                    viewModel.CurrentMode,
                    viewModel.HasModel,
                    (mode, hasModel) => (mode, hasModel))
                .Subscribe(state => UpdateModeObjects(state.mode, state.hasModel))
                .AddTo(this);
        }


        /// <inheritdoc/>
        public void RefreshLocalizedUi()
        {
            ApplyModeDropdownOptions();
        }

        private void ApplyModeDropdownOptions()
        {
            if (editModeUI == null)
            {
                Debug.LogError("[ClayEditModeView] editModeUIが未配線です", this);
                return;
            }

            // EditModeTypeの並びClayPaintAnimationと一致させる
            int selected = Mathf.Clamp(editModeUI.value, 0, 2);
            editModeUI.ClearOptions();
            editModeUI.AddOptions(
                new List<string>
                {
                    LocalizedText.GetOrFallback(GameTextKeys.ClayEditSculptMode, "成形"),
                    LocalizedText.GetOrFallback(GameTextKeys.ClayEditPaintMode, "塗る"),
                    LocalizedText.GetOrFallback(GameTextKeys.ClayEditAnimationMode, "動かす"),
                });
            editModeUI.SetValueWithoutNotify(selected);
            editModeUI.RefreshShownValue();

            if (editModeUI.captionText != null)
            {
                LocalizedFont.Apply(editModeUI.captionText);
            }

            if (editModeUI.itemText != null)
            {
                LocalizedFont.Apply(editModeUI.itemText);
            }
        }

        private void UpdateModeObjects(EditModeType mode, bool hasModel)
        {
            int modeIndex = (int)mode;

            foreach (var editModeTypeObject in editModeTypeObjects)
            {
                // モデル未選択(hasModel == false) なら、常に -1 を渡して全て Disable にする
                editModeTypeObject.SetActive(hasModel ? modeIndex : -1);
            }
        }
    }
}
