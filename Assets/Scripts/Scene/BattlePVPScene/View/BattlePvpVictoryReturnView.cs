using Battle;
using Battle.Interface;
using Cysharp.Threading.Tasks;
using Extensions;
using LighthouseExtends.UIComponent.Button;
using System.Threading;
using UnityEngine;

namespace Scene.BattlePVPScene.View
{
    /// <summary>
    /// 通信対戦勝利後のタイトル戻りと再戦ボタンUI
    /// </summary>
    public sealed class BattlePvpVictoryReturnView : MonoBehaviour, IBattleDualVictoryReturnView
    {
        private const int VisibleSortingOrder = 1100;

        [Tooltip("ONのときフォールバックUIを実行時生成しない")]
        [SerializeField] private bool useSceneCanvasLayout = true;

        [SerializeField] private Canvas rootCanvas;
        [SerializeField] private LHButton titleReturnButton;
        [SerializeField] private LHButton rematchButton;

        private int cachedSortingOrder = 320;
        private bool hasCachedSortingOrder;

        private void Awake()
        {
            ValidateSceneLayout();
            rematchButton?.EnsureUiSoundFeedback();
            titleReturnButton?.EnsureUiSoundFeedback();
            CacheSortingOrderIfNeeded();

            // GOを落とさずCanvasのみオフ(初回表示でAwake再入して消えるのを防ぐ)
            CanvasVisibilityUtility.SetCanvasEnabled(rootCanvas, false);
        }

        /// <inheritdoc/>
        public void SetDualButtonsVisible(bool visible)
        {
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
            EnsureButtonReady(titleReturnButton, visible);
            EnsureButtonReady(rematchButton, visible);
        }

        private void CacheSortingOrderIfNeeded()
        {
            if (hasCachedSortingOrder || rootCanvas == null)
            {
                return;
            }

            // 表示用に一時的に上げる前の値だけを覚える
            if (rootCanvas.sortingOrder != VisibleSortingOrder)
            {
                cachedSortingOrder = rootCanvas.sortingOrder;
            }

            hasCachedSortingOrder = true;
        }

        /// <inheritdoc/>
        public async UniTask<BattleVictoryReturnChoice> WaitVictoryReturnChoiceAsync(CancellationToken cancellationToken)
        {
            if (titleReturnButton == null && rematchButton == null)
            {
                Debug.LogError("[BattlePvpVictoryReturnView] 再戦/タイトル戻りボタン参照がありません", this);
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

            if (titleReturnButton != null)
            {
                titleReturnButton.onClick.AddListener(OnTitleClick);
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
                if (titleReturnButton != null)
                {
                    titleReturnButton.onClick.RemoveListener(OnTitleClick);
                }

                if (rematchButton != null)
                {
                    rematchButton.onClick.RemoveListener(OnRematchClick);
                }
            }
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

        private void ValidateSceneLayout()
        {
            if (!useSceneCanvasLayout)
            {
                return;
            }

            if (rootCanvas == null || titleReturnButton == null || rematchButton == null)
            {
                Debug.LogError(
                    "[BattlePvpVictoryReturnView] シーン上のUI参照が未設定です。HierarchyでUI参照を確認してください",
                    this);
            }
        }
    }
}
