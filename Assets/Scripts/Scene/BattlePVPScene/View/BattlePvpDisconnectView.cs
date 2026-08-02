using Cysharp.Threading.Tasks;
using Extensions;
using LighthouseExtends.UIComponent.Button;
using Localization;
using Scene.BattlePVPScene.Interface;
using System;
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
        private static string DisconnectMessage =>
            LocalizedText.Get(GameTextKeys.BattlePvpDisconnectMessage);

        private static string CloseButtonLabel =>
            LocalizedText.Get(GameTextKeys.BattlePvpDisconnectClose);
        // SceneFade(32000)より前面に出し暗転下に埋もれないようにする
        private const int VisibleSortingOrder = 33000;
        private const float MissingButtonFallbackSeconds = 1.5f;

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
                await UniTask.Delay(
                    TimeSpan.FromSeconds(MissingButtonFallbackSeconds),
                    cancellationToken: cancellationToken);
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
