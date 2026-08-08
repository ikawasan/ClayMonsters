using Cysharp.Threading.Tasks;
using Extensions;
using LighthouseExtends.UIComponent.Button;
using Localization;
using Scene.BattlePVPScene.Interface;
using System.Threading;
using TMPro;
using UI.ClayEditor.View;
using UnityEngine;
using UnityEngine.UI;

namespace Scene.BattlePVPScene.View
{
    /// <summary>
    /// 通信切断時のメッセージウィンドウと閉じるボタン
    /// </summary>
    public sealed class BattlePvpDisconnectView : MonoBehaviour, IBattlePvpDisconnectView, ILanguageAwareUi
    {
        private static string DisconnectMessage =>
            LocalizedText.Get(GameTextKeys.BattlePvpDisconnectMessage);

        private static string CloseButtonLabel =>
            LocalizedText.Get(GameTextKeys.BattlePvpDisconnectClose);
        // SceneFade(32000)より前面に出し暗転下に埋もれないようにする
        private const int VisibleSortingOrder = 33000;

        [Tooltip("ONのときフォールバックUIを実行時生成しない")]
        [SerializeField] private bool useSceneCanvasLayout = true;

        [SerializeField] private Canvas rootCanvas;
        [SerializeField] private Image panelBackground;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private LHButton titleReturnButton;

        private int cachedSortingOrder = 350;
        private bool hasCachedSortingOrder;
        private bool isPrepared;

        private void Awake()
        {
            PrepareIfNeeded();
            // 初回Awakeでは非表示にするがscaleを潰さない
            if (rootCanvas != null)
            {
                rootCanvas.enabled = false;
                GraphicRaycaster raycaster = rootCanvas.GetComponent<GraphicRaycaster>();
                if (raycaster != null)
                {
                    raycaster.enabled = false;
                }
            }
        }

        /// <inheritdoc/>
        public void SetVisible(bool visible)
        {
            PrepareIfNeeded();
            if (visible)
            {
                ApplyDisconnectCopy();
                EnsureVisibleHierarchy();
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

            if (visible)
            {
                // SetActive直後のAwakeなどでenabledが下がることがあるため再保証する
                EnsureVisibleHierarchy();
                CanvasVisibilityUtility.SetCanvasEnabled(rootCanvas, true);
                Debug.Log(
                    "[BattlePvpDisconnectView] 切断UI表示"
                    + $" canvas={(rootCanvas != null)}"
                    + $" active={(rootCanvas != null && rootCanvas.gameObject.activeInHierarchy)}"
                    + $" enabled={(rootCanvas != null && rootCanvas.enabled)}"
                    + $" scale={(rootCanvas != null ? rootCanvas.transform.lossyScale.ToString() : "null")}"
                    + $" button={(titleReturnButton != null)}");
            }

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
                Debug.LogError(
                    "[BattlePvpDisconnectView] 閉じるボタン参照がありません 即時Title戻りします",
                    this);
                return;
            }

            // 非表示状態で待てないよう再表示する
            SetVisible(true);

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

        /// <inheritdoc/>
        public void RefreshLocalizedUi()
        {
            ApplyDisconnectCopy();
        }

        private void PrepareIfNeeded()
        {
            if (isPrepared)
            {
                return;
            }

            isPrepared = true;
            ValidateSceneLayout();
            titleReturnButton?.EnsureUiSoundFeedback();
            CacheSortingOrderIfNeeded();
            ApplyDisconnectCopy();
        }

        private void EnsureVisibleHierarchy()
        {
            if (rootCanvas == null)
            {
                rootCanvas = GetComponent<Canvas>();
            }

            if (rootCanvas == null)
            {
                Debug.LogError("[BattlePvpDisconnectView] rootCanvasがありません", this);
                return;
            }

            // 親が非アクティブだと表示できない
            Transform current = rootCanvas.transform;
            while (current != null)
            {
                if (!current.gameObject.activeSelf)
                {
                    current.gameObject.SetActive(true);
                }

                current = current.parent;
            }

            // Overlayキャンバスのscale=0崩れを補正する
            ModelSaveSlotScrollListView.FixCanvasScaleHierarchy(rootCanvas);
            if (rootCanvas.transform.localScale.sqrMagnitude < 0.001f)
            {
                rootCanvas.transform.localScale = Vector3.one;
            }

            rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            rootCanvas.worldCamera = null;
            rootCanvas.overrideSorting = true;
            rootCanvas.sortingOrder = VisibleSortingOrder;
            rootCanvas.transform.SetAsLastSibling();
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
