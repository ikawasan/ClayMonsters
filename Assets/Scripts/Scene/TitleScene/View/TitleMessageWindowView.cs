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
    /// タイトル画面で使うメッセージウィンドウのView
    /// 半透明背景・粘土風パネル・OKボタンで通知を表示する
    /// UIはTitleシーンのCanvas上に配置する
    /// </summary>
    public sealed class TitleMessageWindowView : MonoBehaviour, ITitleMessageWindowView, ILanguageAwareUi
    {
        [SerializeField] private Canvas canvas;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private LHButton okButton;

        private bool isShowing;
        private string messageKey = string.Empty;
        private string messageFallback = string.Empty;
        private string rawMessage;

        private void Awake()
        {
            ValidateSceneUi();
            ApplyChromeLabels();
            Hide();
        }

        /// <inheritdoc/>
        public void Show(string message)
        {
            isShowing = true;
            messageKey = string.Empty;
            messageFallback = string.Empty;
            rawMessage = message ?? string.Empty;
            ApplyMessage();
            ApplyChromeLabels();

            if (canvas != null)
            {
                canvas.enabled = true;
            }
        }

        /// <inheritdoc/>
        public void ShowLocalized(string key, string fallback)
        {
            isShowing = true;
            messageKey = key ?? string.Empty;
            messageFallback = fallback ?? string.Empty;
            rawMessage = null;
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
            if (canvas != null)
            {
                canvas.enabled = false;
            }
        }

        /// <inheritdoc/>
        public IDisposable SubscribeOkButtonClick(UnityAction action) => okButton.SubscribeOnClick(action);

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
            LhButtonLabelUtility.SetLabel(
                okButton,
                LocalizedText.GetOrFallback(GameTextKeys.CommonOk, "OK"));
        }

        private void ApplyMessage()
        {
            if (messageText == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(messageKey))
            {
                messageText.text = LocalizedText.GetOrFallback(messageKey, messageFallback);
                return;
            }

            messageText.text = rawMessage ?? string.Empty;
        }

        private void ValidateSceneUi()
        {
            if (canvas == null || messageText == null || okButton == null)
            {
                Debug.LogError(
                    "[TitleMessageWindowView] シーン上のUI参照が未設定です。HierarchyでUI参照を確認してください",
                    this);
            }
        }
    }
}
