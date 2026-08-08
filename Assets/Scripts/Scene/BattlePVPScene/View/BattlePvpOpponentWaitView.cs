using Extensions;
using Localization;
using Scene.BattlePVPScene.Interface;
using TMPro;
using UI.ClayEditor.View;
using UnityEngine;
using UnityEngine.UI;

namespace Scene.BattlePVPScene.View
{
    /// <summary>
    /// 相手入力待ち時に画面下部へメッセージを表示する
    /// </summary>
    public sealed class BattlePvpOpponentWaitView : MonoBehaviour, IBattlePvpOpponentWaitView, ILanguageAwareUi
    {
        // SceneFade(32000)より前面に出し暗転下に埋もれないようにする
        private const int VisibleSortingOrder = 32500;

        [Tooltip("ONのときフォールバックUIを実行時生成しない")]
        [SerializeField] private bool useSceneCanvasLayout = true;

        [SerializeField] private Canvas rootCanvas;
        [SerializeField] private Image bannerBackground;
        [SerializeField] private TMP_Text messageText;

        private void Awake()
        {
            ValidateSceneLayout();
            ApplyLocalizedLabels();
            if (rootCanvas != null)
            {
                rootCanvas.enabled = false;
            }
        }

        /// <inheritdoc/>
        public void SetVisible(bool visible)
        {
            if (visible)
            {
                ApplyLocalizedLabels();
                EnsureVisibleHierarchy();
            }

            CanvasVisibilityUtility.SetCanvasEnabled(rootCanvas, visible);

            if (visible && rootCanvas != null)
            {
                EnsureVisibleHierarchy();
                CanvasVisibilityUtility.SetCanvasEnabled(rootCanvas, true);
            }
        }

        /// <inheritdoc/>
        public void RefreshLocalizedUi()
        {
            ApplyLocalizedLabels();
        }

        private void EnsureVisibleHierarchy()
        {
            if (rootCanvas == null)
            {
                rootCanvas = GetComponent<Canvas>();
            }

            if (rootCanvas == null)
            {
                return;
            }

            Transform current = rootCanvas.transform;
            while (current != null)
            {
                if (!current.gameObject.activeSelf)
                {
                    current.gameObject.SetActive(true);
                }

                current = current.parent;
            }

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

        private void ApplyLocalizedLabels()
        {
            if (messageText == null)
            {
                return;
            }

            LocalizedFont.SetText(
                messageText,
                LocalizedText.GetOrFallback(
                    GameTextKeys.BattlePvpOpponentWaiting,
                    "相手の応答を待っています"));
        }

        private void ValidateSceneLayout()
        {
            if (!useSceneCanvasLayout)
            {
                return;
            }

            if (rootCanvas == null || bannerBackground == null || messageText == null)
            {
                Debug.LogError(
                    "[BattlePvpOpponentWaitView] シーン上のUI参照が未設定です。HierarchyでUI参照を確認してください",
                    this);
            }
        }
    }
}
