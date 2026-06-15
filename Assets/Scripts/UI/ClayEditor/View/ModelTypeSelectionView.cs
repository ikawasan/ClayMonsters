using ClayEditor;
using GameData;
using LighthouseExtends.UIComponent.Button;
using R3;
using System.Collections.Generic;
using UI.ClayEditor.ViewModel;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace UI.ClayEditor.View
{
    /// <summary>
    /// モデル選択のためのView
    /// </summary>
    public class ModelTypeSelectionView : MonoBehaviour
    {
        [Inject] private readonly ModelTypeSelectionViewModel viewModel;

        [Header("UI・管理設定")]
        [SerializeField] private Canvas selectorCanvas;
        [SerializeField] private Canvas editModeCanvas;
        [SerializeField] private Canvas modelExporterCanvas;
        [SerializeField] private Transform boneSpawnParent;

        [Header("ボタン設定")]
        [Tooltip("インスペクターで割り当てたモデル選択ボタンと、生成するプレハブの対応")]
        [SerializeField] private List<ModelTypeButton> modelButtons;

        private void Start()
        {
            // 選択UIの表示状態を購読する
            viewModel.ShowSelector
                .Subscribe(UpdateCanvases)
                .AddTo(this);

            // インスペクターで割り当てたボタンにクリック処理を登録する
            RegisterButtons();
        }

        private void RegisterButtons()
        {
            if (modelButtons == null)
            {
                return;
            }

            foreach (var modelButton in modelButtons)
            {
                if (modelButton == null || modelButton.Button == null)
                {
                    continue;
                }

                // ループ変数をローカルに退避し、クロージャで安全に参照する
                ModelTypeButton target = modelButton;
                target.Button.onClick.AsObservable()
                    .Subscribe(_ =>
                    {
                        viewModel.SelectModel(target.Prefab, boneSpawnParent);
                    })
                    .AddTo(this);
            }
        }

        private void UpdateCanvases(bool showSelector)
        {
            if (selectorCanvas != null)
            {
                selectorCanvas.enabled = showSelector;
            }

            if (editModeCanvas != null)
            {
                editModeCanvas.enabled = !showSelector;
            }

            if (modelExporterCanvas != null)
            {
                modelExporterCanvas.enabled = !showSelector;
            }
        }
    }

    [System.Serializable]
    public class ModelTypeButton
    {
        [SerializeField] private ModelType modelType;
        [SerializeField] private LHButton button;
        [SerializeField] private GameObject prefab;

        /// <summary>
        /// このボタンが表すモデルタイプ（インスペクター上の識別用）
        /// </summary>
        public ModelType ModelType => modelType;

        /// <summary>
        /// インスペクターで割り当てたモデル選択ボタン
        /// </summary>
        public Button Button => button;

        /// <summary>
        /// 選択時に生成するモデルのプレハブ
        /// </summary>
        public GameObject Prefab => prefab;
    }
}