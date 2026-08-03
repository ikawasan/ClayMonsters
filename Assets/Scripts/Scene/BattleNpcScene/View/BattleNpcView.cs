using Battle;
using Battle.Interface;
using Cysharp.Threading.Tasks;
using Extensions;
using LighthouseExtends.UIComponent.Button;
using Localization;
using Scene.BattleNpcScene.Interface;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;

namespace Scene.BattleNpcScene.View
{
    /// <summary>
    /// BattleNpcシーンの勝利後戻り/再戦UI
    /// 位置と文言はBattleVictoryReturnDualプレハブ側で設定する
    /// </summary>
    public class BattleNpcView : MonoBehaviour, IBattleNpcView, IBattleDualVictoryReturnView, ILanguageAwareUi
    {
        private const int VisibleSortingOrder = 1100;

        [SerializeField] private Canvas rootCanvas;
        [SerializeField] private LHButton returnButton;
        [SerializeField] private LHButton rematchButton;

        private int cachedSortingOrder = 320;
        private bool hasCachedSortingOrder;

        private void Awake()
        {
            if (rootCanvas == null)
            {
                rootCanvas = GetComponent<Canvas>();
            }

            returnButton?.EnsureUiSoundFeedback();
            rematchButton?.EnsureUiSoundFeedback();
            CacheSortingOrderIfNeeded();
            ApplyLocalizedLabels();
            SetDualButtonsVisible(false);
        }

        /// <inheritdoc/>
        public IDisposable SubscribeReturnButtonClick(UnityAction action) => returnButton.SubscribeOnClick(action);

        /// <inheritdoc/>
        public void SetReturnButtonVisible(bool visible)
        {
            SetDualButtonsVisible(visible);
        }

        /// <inheritdoc/>
        public void SetDualButtonsVisible(bool visible)
        {
            if (visible)
            {
                ApplyLocalizedLabels();
            }

            if (rootCanvas != null)
            {
                CacheSortingOrderIfNeeded();
                if (visible)
                {
                    rootCanvas.overrideSorting = true;
                    rootCanvas.sortingOrder = VisibleSortingOrder;
                }
                else
                {
                    rootCanvas.sortingOrder = cachedSortingOrder;
                }
            }

            CanvasVisibilityUtility.SetCanvasEnabled(rootCanvas, visible);
            EnsureButtonReady(returnButton, visible);
            EnsureButtonReady(rematchButton, visible);
        }


        /// <inheritdoc/>
        public void RefreshLocalizedUi()
        {
            ApplyLocalizedLabels();
        }

        private void ApplyLocalizedLabels()
        {
            LhButtonLabelUtility.SetLabel(
                returnButton,
                LocalizedText.GetOrFallback(GameTextKeys.TrainingHudBackToTitle, "タイトルへ戻る"));
            LhButtonLabelUtility.SetLabel(
                rematchButton,
                LocalizedText.GetOrFallback(GameTextKeys.BattleRematch, "再戦"));
        }

        /// <inheritdoc/>
        public async UniTask WaitReturnButtonClickAsync(CancellationToken cancellationToken)
        {
            await WaitVictoryReturnChoiceAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public async UniTask<BattleVictoryReturnChoice> WaitVictoryReturnChoiceAsync(CancellationToken cancellationToken)
        {
            if (returnButton == null && rematchButton == null)
            {
                Debug.LogError("[BattleNpcView] 戻る/再戦ボタン参照がありません", this);
                await UniTask.WaitUntilCanceled(cancellationToken);
                return BattleVictoryReturnChoice.Title;
            }

            BattleVictoryReturnChoice choice = BattleVictoryReturnChoice.Title;
            bool decided = false;

            void OnTitleClick()
            {
                choice = BattleVictoryReturnChoice.Title;
                decided = true;
            }

            void OnRematchClick()
            {
                choice = BattleVictoryReturnChoice.Rematch;
                decided = true;
            }

            if (returnButton != null)
            {
                returnButton.onClick.AddListener(OnTitleClick);
            }

            if (rematchButton != null)
            {
                rematchButton.onClick.AddListener(OnRematchClick);
            }

            try
            {
                await UniTask.WaitUntil(() => decided, cancellationToken: cancellationToken);
                return choice;
            }
            finally
            {
                if (returnButton != null)
                {
                    returnButton.onClick.RemoveListener(OnTitleClick);
                }

                if (rematchButton != null)
                {
                    rematchButton.onClick.RemoveListener(OnRematchClick);
                }
            }
        }

        private void CacheSortingOrderIfNeeded()
        {
            if (hasCachedSortingOrder || rootCanvas == null)
            {
                return;
            }

            if (rootCanvas.sortingOrder != VisibleSortingOrder)
            {
                cachedSortingOrder = rootCanvas.sortingOrder;
            }

            hasCachedSortingOrder = true;
        }

        private static void EnsureButtonReady(LHButton button, bool visible)
        {
            if (button == null)
            {
                return;
            }

            // 親Canvas配下は常時有効にし表示はCanvas.enabledで制御する
            if (!button.gameObject.activeSelf)
            {
                button.gameObject.SetActive(true);
            }

            button.interactable = visible;
        }
    }
}
