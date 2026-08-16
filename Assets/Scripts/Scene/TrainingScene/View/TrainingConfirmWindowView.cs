using Cysharp.Threading.Tasks;
using Extensions;
using LighthouseExtends.UIComponent.Button;
using Localization;
using System;
using System.Threading;
using TMPro;
using UnityEngine;

namespace Scene.TrainingScene.View
{
    /// <summary>
    /// 育成のはいいいえ確認テキストウィンドウ
    /// </summary>
    public sealed class TrainingConfirmWindowView : MonoBehaviour, ILanguageAwareUi
    {
        [SerializeField] private Canvas canvas;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private LHButton yesButton;
        [SerializeField] private LHButton noButton;

        private bool isShowing;
        private bool hasChoice;
        private bool pendingYes;
        private bool uiBound;
        private string messageKey = string.Empty;
        private string messageFallback = string.Empty;
        private string messageParamName = string.Empty;
        private object messageParamValue;
        private bool hasMessageParam;
        private string yesOriginal = "はい";
        private string noOriginal = "いいえ";
        private bool labelOriginalsCaptured;

        private void Awake()
        {
            EnsureUiBound();
            CaptureLabelOriginalsIfNeeded();
            ApplyChromeLabels();
            Hide();
        }

        /// <summary>
        /// パラメータ付き確認を表示してはいいいえを待つ
        /// </summary>
        /// <param name="key">文言キー</param>
        /// <param name="fallback">フォールバックテンプレート</param>
        /// <param name="paramName">置換パラメータ名</param>
        /// <param name="paramValue">置換パラメータ値</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>はいならtrue</returns>
        public async UniTask<bool> WaitLocalizedYesNoAsync(
            string key,
            string fallback,
            string paramName,
            object paramValue,
            CancellationToken cancellationToken)
        {
            EnsureUiBound();
            hasMessageParam = !string.IsNullOrEmpty(paramName);
            messageParamName = paramName ?? string.Empty;
            messageParamValue = paramValue;
            ShowInternal(key, fallback);
            hasChoice = false;
            pendingYes = false;
            try
            {
                await UniTask.WaitUntil(() => hasChoice, cancellationToken: cancellationToken);
                return pendingYes;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            finally
            {
                Hide();
            }
        }

        /// <summary>
        /// 確認ウィンドウを隠す
        /// </summary>
        public void Hide()
        {
            isShowing = false;
            hasChoice = false;
            if (canvas != null)
            {
                CanvasVisibilityUtility.SetCanvasEnabled(canvas, false);
            }
        }

        /// <inheritdoc/>
        public void RefreshLocalizedUi()
        {
            ApplyChromeLabels();
            if (!isShowing)
            {
                return;
            }

            ApplyMessage();
        }

        private void ShowInternal(string key, string fallback)
        {
            isShowing = true;
            messageKey = key ?? string.Empty;
            messageFallback = fallback ?? string.Empty;
            ApplyChromeLabels();
            ApplyMessage();
            BindButtons();
            if (canvas != null)
            {
                CanvasVisibilityUtility.SetCanvasEnabled(canvas, true);
            }
        }

        private void EnsureUiBound()
        {
            if (uiBound)
            {
                return;
            }

            uiBound = true;
            if (canvas == null)
            {
                canvas = GetComponent<Canvas>();
            }

            if (canvas == null || messageText == null || yesButton == null || noButton == null)
            {
                Debug.LogError(
                    "[TrainingConfirmWindowView] 確認ウィンドウの参照が未配線ですHierarchyで接続してください",
                    this);
            }
        }

        private void BindButtons()
        {
            BindChoiceButton(yesButton, accepted: true);
            BindChoiceButton(noButton, accepted: false);
        }

        private void BindChoiceButton(LHButton button, bool accepted)
        {
            if (button == null)
            {
                return;
            }

            button.EnsureUiSoundFeedback();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => CompleteChoice(accepted));
        }

        private void CompleteChoice(bool accepted)
        {
            pendingYes = accepted;
            hasChoice = true;
        }

        private void ApplyChromeLabels()
        {
            CaptureLabelOriginalsIfNeeded();
            LhButtonLabelUtility.SetLabel(
                yesButton,
                SceneLocalizedLabel.Resolve(GameTextKeys.CommonYes, yesOriginal));
            LhButtonLabelUtility.SetLabel(
                noButton,
                SceneLocalizedLabel.Resolve(GameTextKeys.CommonNo, noOriginal));
        }

        private void CaptureLabelOriginalsIfNeeded()
        {
            if (labelOriginalsCaptured)
            {
                return;
            }

            yesOriginal = SceneLocalizedLabel.Capture(yesButton, yesOriginal);
            noOriginal = SceneLocalizedLabel.Capture(noButton, noOriginal);
            labelOriginalsCaptured = true;
        }

        private void ApplyMessage()
        {
            if (messageText == null)
            {
                return;
            }

            if (hasMessageParam)
            {
                messageText.text = LocalizedText.GetOrFallback(
                    messageKey,
                    messageFallback,
                    messageParamName,
                    messageParamValue);
                return;
            }

            messageText.text = LocalizedText.GetOrFallback(messageKey, messageFallback);
        }
    }
}
