using Cysharp.Threading.Tasks;
using Extensions;
using LighthouseExtends.UIComponent.Button;
using Scene.BattlePVPScene.Interface;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Scene.BattlePVPScene.View
{
    /// <summary>
    /// 通信切断時のメッセージウィンドウと閉じるボタン
    /// </summary>
    public sealed class BattlePvpDisconnectView : MonoBehaviour, IBattlePvpDisconnectView
    {
        private const string DisconnectMessage = "通信が切れました";
        private const string CloseButtonLabel = "閉じる";
        private const int VisibleSortingOrder = 1200;

        [Tooltip("ONのときフォールバックUIを実行時生成しない")]
        [SerializeField] private bool useSceneCanvasLayout = true;

        [SerializeField] private Canvas rootCanvas;
        [SerializeField] private Image panelBackground;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private LHButton titleReturnButton;

        private int cachedSortingOrder = 350;
        private bool hasCachedSortingOrder;

        private void Awake()
        {
            ValidateSceneLayout();
            titleReturnButton?.EnsureUiSoundFeedback();
            CacheSortingOrderIfNeeded();
            ApplyDisconnectCopy();
            CanvasVisibilityUtility.SetCanvasEnabled(rootCanvas, false);
        }

        /// <inheritdoc/>
        public void SetVisible(bool visible)
        {
            if (visible)
            {
                ApplyDisconnectCopy();
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

            if (titleReturnButton != null)
            {
                if (!titleReturnButton.gameObject.activeSelf)
                {
                    titleReturnButton.gameObject.SetActive(true);
                }

                titleReturnButton.interactable = visible;
            }
        }

        /// <inheritdoc/>
        public async UniTask WaitReturnToTitleAsync(CancellationToken cancellationToken)
        {
            if (titleReturnButton == null)
            {
                Debug.LogError("[BattlePvpDisconnectView] 閉じるボタン参照がありません", this);
                await UniTask.WaitUntilCanceled(cancellationToken);
                return;
            }

            bool decided = false;

            void OnClick()
            {
                decided = true;
            }

            titleReturnButton.onClick.AddListener(OnClick);
            try
            {
                await UniTask.WaitUntil(() => decided, cancellationToken: cancellationToken);
            }
            finally
            {
                titleReturnButton.onClick.RemoveListener(OnClick);
            }
        }

        private void ApplyDisconnectCopy()
        {
            if (messageText != null)
            {
                messageText.text = DisconnectMessage;
            }

            if (titleReturnButton == null)
            {
                return;
            }

            TMP_Text label = titleReturnButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = CloseButtonLabel;
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

        private void ValidateSceneLayout()
        {
            if (!useSceneCanvasLayout)
            {
                return;
            }

            if (rootCanvas == null
                || panelBackground == null
                || messageText == null
                || titleReturnButton == null)
            {
                Debug.LogError(
                    "[BattlePvpDisconnectView] シーン上のUI参照が未設定です。HierarchyでUI参照を確認してください",
                    this);
            }
        }
    }
}
