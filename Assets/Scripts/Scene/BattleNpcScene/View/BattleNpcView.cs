using Battle.Interface;
using Cysharp.Threading.Tasks;
using Extensions;
using LighthouseExtends.UIComponent.Button;
using Scene.BattleNpcScene.Interface;
using System;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Scene.BattleNpcScene.View
{
    /// <summary>
    /// BattleNpcシーンの戻るボタンUI
    /// 勝利演出後に右下へ表示する
    /// </summary>
    public class BattleNpcView : MonoBehaviour, IBattleNpcView
    {
        private const float ReturnButtonWidth = 220f;
        private const float ReturnButtonHeight = 64f;
        private const float ReturnButtonRight = 40f;
        private const float ReturnButtonBottom = 36f;

        [SerializeField] private Canvas rootCanvas;
        [SerializeField] private LHButton returnButton;

        private void Awake()
        {
            if (rootCanvas == null)
            {
                rootCanvas = GetComponent<Canvas>();
            }

            EnsureReturnButtonCanvas();
            EnsureReturnButtonLayout();
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

            returnButton.gameObject.SetActive(visible);
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

        private void EnsureReturnButtonCanvas()
        {
            if (rootCanvas == null)
            {
                return;
            }

            rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            rootCanvas.overrideSorting = true;
            rootCanvas.sortingOrder = 320;

            if (rootCanvas.GetComponent<GraphicRaycaster>() == null)
            {
                rootCanvas.gameObject.AddComponent<GraphicRaycaster>();
            }
        }

        private void EnsureReturnButtonLayout()
        {
            if (returnButton == null)
            {
                return;
            }

            RectTransform rect = returnButton.transform as RectTransform;
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.sizeDelta = new Vector2(ReturnButtonWidth, ReturnButtonHeight);
            rect.anchoredPosition = new Vector2(-ReturnButtonRight, ReturnButtonBottom);

            returnButton.EnsureUiSoundFeedback();

            TMP_Text label = returnButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = "戻る";
                label.textWrappingMode = TextWrappingModes.NoWrap;
            }
        }
    }
}
