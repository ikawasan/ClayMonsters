using GameData;
using R3;
using System.Collections.Generic;
using TMPro;
using UI.ClayEditor.ViewModel;
using UnityEngine;
using VContainer;

namespace UI.ClayEditor.View
{
    public class ClayEditModeView : MonoBehaviour
    {
        [Inject] private readonly ClayEditModeViewModel viewModel;

        [SerializeField] private TMP_Dropdown editModeUI;
        [SerializeField] private List<EditModeTypeObject> editModeTypeObjects;

        [SerializeField]
        private TMP_Text clayModeDescriptorText;

        private void Start()
        {
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
                    }
                })
                .AddTo(this);

            // 表示更新
            Observable.CombineLatest(
                    viewModel.CurrentMode,
                    viewModel.HasModel,
                    (mode, hasModel) => (mode, hasModel))
                .Subscribe(state => UpdateModeObjects(state.mode, state.hasModel))
                .AddTo(this);

            // 粘土モード用の説明テキスト表示
            viewModel.ShowClayDescriptor
                .Subscribe(show =>
                {
                    if (clayModeDescriptorText != null)
                    {
                        clayModeDescriptorText.gameObject.SetActive(show);
                    }
                })
                .AddTo(this);
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
