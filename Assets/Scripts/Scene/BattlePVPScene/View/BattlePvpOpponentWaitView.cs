using Scene.BattlePVPScene.Interface;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Localization;

namespace Scene.BattlePVPScene.View
{
    /// <summary>
    /// 相手入力待ち時に画面下部へメッセージを表示する
    /// </summary>
    public sealed class BattlePvpOpponentWaitView : MonoBehaviour, IBattlePvpOpponentWaitView, ILanguageAwareUi
    {
        [Tooltip("ONのときフォールバックUIを実行時生成しない")]
        [SerializeField] private bool useSceneCanvasLayout = true;

        [SerializeField] private Canvas rootCanvas;
        [SerializeField] private Image bannerBackground;
        [SerializeField] private TMP_Text messageText;

        private void Awake()
        {
            ValidateSceneLayout();
            ApplyLocalizedLabels();
            SetVisible(false);
        }

        /// <inheritdoc/>
        public void SetVisible(bool visible)
        {
            if (visible)
            {
                ApplyLocalizedLabels();
            }

            if (rootCanvas != null)
            {
                if (visible)
                {
                    // SceneFade(32000)より前面に出し暗転下に埋もれないようにする
                    rootCanvas.overrideSorting = true;
                    rootCanvas.sortingOrder = 32500;
                }

                rootCanvas.enabled = visible;
            }
        }


        /// <inheritdoc/>
        public void RefreshLocalizedUi()
        {
            ApplyLocalizedLabels();
        }

        private void ApplyLocalizedLabels()
        {
            if (messageText == null)
            {
                return;
            }

            Localization.LocalizedFont.SetText(
                messageText,
                Localization.LocalizedText.GetOrFallback(
                    Localization.GameTextKeys.BattlePvpOpponentWaiting,
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
