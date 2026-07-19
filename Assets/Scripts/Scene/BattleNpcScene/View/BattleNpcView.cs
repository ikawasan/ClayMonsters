using Battle.Interface;
using Cysharp.Threading.Tasks;
using Extensions;
using LighthouseExtends.UIComponent.Button;
using Scene.BattleNpcScene.Interface;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;

namespace Scene.BattleNpcScene.View
{
    /// <summary>
    /// BattleNpcシーンの戻るボタンUI
    /// 位置と文言はBattleVictoryReturnSingleプレハブ側で設定する
    /// </summary>
    public class BattleNpcView : MonoBehaviour, IBattleNpcView
    {
        [SerializeField] private Canvas rootCanvas;
        [SerializeField] private LHButton returnButton;

        private void Awake()
        {
            if (rootCanvas == null)
            {
                rootCanvas = GetComponent<Canvas>();
            }

            returnButton?.EnsureUiSoundFeedback();
            SetReturnButtonVisible(false);
        }

        /// <inheritdoc/>
        public IDisposable SubscribeReturnButtonClick(UnityAction action) => returnButton.SubscribeOnClick(action);

        /// <inheritdoc/>
        public void SetReturnButtonVisible(bool visible)
        {
            if (rootCanvas != null)
            {
                rootCanvas.enabled = visible;
            }

            if (returnButton == null)
            {
                return;
            }

            // 表示はCanvas.enabledのみボタンGOは常時有効
            if (!returnButton.gameObject.activeSelf)
            {
                returnButton.gameObject.SetActive(true);
            }

            returnButton.interactable = visible;
        }

        /// <inheritdoc/>
        public async UniTask WaitReturnButtonClickAsync(CancellationToken cancellationToken)
        {
            if (returnButton == null)
            {
                await UniTask.WaitUntilCanceled(cancellationToken);
                return;
            }

            bool pressed = false;
            void OnClick() => pressed = true;
            returnButton.onClick.AddListener(OnClick);

            try
            {
                await UniTask.WaitUntil(() => pressed, cancellationToken: cancellationToken);
            }
            finally
            {
                returnButton.onClick.RemoveListener(OnClick);
            }
        }
    }
}
