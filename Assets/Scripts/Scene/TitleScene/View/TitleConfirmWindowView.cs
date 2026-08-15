using Extensions;
using LighthouseExtends.UIComponent.Button;
using Localization;
using Scene.TitleScene.Interface;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace Scene.TitleScene.View
{
    /// <summary>
    /// タイトル画面のはいいいえ確認ウィンドウ
    /// UIはTitleシーンのCanvas上に配置する
    /// </summary>
    public sealed class TitleConfirmWindowView : MonoBehaviour, ITitleConfirmWindowView, ILanguageAwareUi
    {
        [SerializeField] private Canvas canvas;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private LHButton yesButton;
        [SerializeField] private LHButton noButton;

        private bool isShowing;
        private string messageKey = string.Empty;
        private string messageFallback = string.Empty;
        private string messageParamName = string.Empty;
        private object messageParamValue;
        private bool hasMessageParam;
        private string yesOriginal = "はい";
        private string noOriginal = "いいえ";
        private bool labelOriginalsCaptured;
        private string yesLabelKey = GameTextKeys.CommonYes;
        private string yesLabelFallback = "はい";
        private string noLabelKey = GameTextKeys.CommonNo;
        private string noLabelFallback = "いいえ";

        private void Awake()
        {
            ValidateSceneUi();
            CaptureLabelOriginalsIfNeeded();
            ApplyChromeLabels();
            Hide();
        }

        /// <inheritdoc/>
        public void ShowLocalized(string key, string fallback)
        {
            hasMessageParam = false;
            messageParamName = string.Empty;
            messageParamValue = null;
            ResetChoiceLabels();
            ShowInternal(key, fallback);
        }

        /// <inheritdoc/>
        public void ShowLocalized(string key, string fallback, string paramName, object paramValue)
        {
            hasMessageParam = !string.IsNullOrEmpty(paramName);
            messageParamName = paramName ?? string.Empty;
            messageParamValue = paramValue;
            ResetChoiceLabels();
            ShowInternal(key, fallback);
        }

        /// <inheritdoc/>
        public void ShowLocalizedChoice(
            string messageKey,
            string messageFallback,
            string yesKey,
            string yesFallback,
            string noKey,
            string noFallback)
        {
            hasMessageParam = false;
            messageParamName = string.Empty;
            messageParamValue = null;
            yesLabelKey = yesKey ?? GameTextKeys.CommonYes;
            yesLabelFallback = yesFallback ?? "はい";
            noLabelKey = noKey ?? GameTextKeys.CommonNo;
            noLabelFallback = noFallback ?? "いいえ";
            ShowInternal(messageKey, messageFallback);
        }

        private void ShowInternal(string key, string fallback)
        {
            isShowing = true;
            messageKey = key ?? string.Empty;
            messageFallback = fallback ?? string.Empty;
            ApplyMessage();
            ApplyChromeLabels();

            if (canvas != null)
            {
                canvas.enabled = true;
            }
        }

        /// <inheritdoc/>
        public void Hide()
        {
            isShowing = false;
            ResetChoiceLabels();
            if (canvas != null)
            {
                canvas.enabled = false;
            }
        }

        private void ResetChoiceLabels()
        {
            yesLabelKey = GameTextKeys.CommonYes;
            yesLabelFallback = yesOriginal;
            noLabelKey = GameTextKeys.CommonNo;
            noLabelFallback = noOriginal;
        }

        /// <inheritdoc/>
        public IDisposable SubscribeYesButtonClick(UnityAction action)
        {
            if (yesButton == null)
            {
                return EmptyDisposable.Instance;
            }

            return yesButton.SubscribeOnClick(action);
        }

        /// <inheritdoc/>
        public IDisposable SubscribeNoButtonClick(UnityAction action)
        {
            if (noButton == null)
            {
                return EmptyDisposable.Instance;
            }

            return noButton.SubscribeOnClick(action);
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

        private void ApplyChromeLabels()
        {
            CaptureLabelOriginalsIfNeeded();
            LhButtonLabelUtility.SetLabel(
                yesButton,
                SceneLocalizedLabel.Resolve(yesLabelKey, yesLabelFallback));
            LhButtonLabelUtility.SetLabel(
                noButton,
                SceneLocalizedLabel.Resolve(noLabelKey, noLabelFallback));
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

        private void ValidateSceneUi()
        {
            if (canvas == null || messageText == null || yesButton == null || noButton == null)
            {
                Debug.LogError(
                    "[TitleConfirmWindowView] シーン上のUI参照が未設定ですHierarchyでUI参照を確認してください",
                    this);
            }
        }

        private sealed class EmptyDisposable : IDisposable
        {
            public static readonly EmptyDisposable Instance = new EmptyDisposable();

            public void Dispose()
            {
            }
        }
    }
}
