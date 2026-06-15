using System.Collections.Generic;
using UI.ClayEditor.ViewModel;
using UnityEngine;
using VContainer;
using R3;
using LighthouseExtends.UIComponent.Button;

namespace UI.ClayEditor.View
{
    /// <summary>
    /// アニメーション再生のためのView
    /// </summary>
    public class ClayModelAnimationView : MonoBehaviour
    {
        [Inject] private readonly ClayModelAnimationViewModel viewModel;

        [Header("ボタン設定")]
        [Tooltip("インスペクターで割り当てたボタンと、送出するアニメーショントリガー名の対応")]
        [SerializeField] private List<AnimationTriggerButton> triggerButtons;

        private void Start()
        {
            RegisterButtons();
        }

        private void RegisterButtons()
        {
            if (triggerButtons == null)
            {
                return;
            }

            foreach (var triggerButton in triggerButtons)
            {
                if (triggerButton == null || triggerButton.Button == null)
                {
                    continue;
                }

                // ループ変数をローカルに退避し、クロージャで安全に参照する
                AnimationTriggerButton target = triggerButton;
                target.Button.onClick.AsObservable()
                    .Subscribe(_ => viewModel.PlayTrigger(target.TriggerName))
                    .AddTo(this);
            }
        }
    }

    [System.Serializable]
    public class AnimationTriggerButton
    {
        [SerializeField] private LHButton button;
        [SerializeField] private string triggerName;

        /// <summary>
        /// インスペクターで割り当てたボタン
        /// </summary>
        public LHButton Button => button;

        /// <summary>
        /// クリック時に送出するアニメーショントリガー名
        /// </summary>
        public string TriggerName => triggerName;
    }
}