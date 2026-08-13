using Extensions;
using LighthouseExtends.UIComponent.Button;
using Localization;
using R3;
using System;
using TMPro;
using UnityEngine;

namespace UI.ClayEditor.View
{
    /// <summary>
    /// セーブ完了専用ウィンドウ
    /// セーブ中と完了メッセージを同じウィンドウで表示する
    /// </summary>
    public sealed class ModelSaveCompleteView : MonoBehaviour, ILanguageAwareUi
    {
        private const int CanvasSortingOrder = 110;

        [SerializeField] private Canvas canvas;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private LHButton closeButton;

        private readonly Subject<Unit> closedSubject = new Subject<Unit>();
        private bool isBound;
        private bool isShowing;
        private bool closeButtonEnabled = true;
        private string messageKey = GameTextKeys.SaveComplete;
        private string messageFallback = "セーブが完了しました";

        /// <summary>
        /// 閉じるボタンが押されたときに発火する
        /// </summary>
        public Observable<Unit> OnClosed => closedSubject;

        private void Awake()
        {
            BindCloseButton();
            Hide();
        }

        /// <summary>
        /// セーブ中ウィンドウを表示する
        /// </summary>
        public void ShowSaving()
        {
            ShowLocalized(GameTextKeys.SaveSaving, "セーブ中…", closeEnabled: false);
        }

        /// <summary>
        /// 完了ウィンドウを表示する
        /// </summary>
        /// <param name="message">表示メッセージ</param>
        public void Show(string message)
        {
            ShowLocalized(GameTextKeys.SaveComplete, message, closeEnabled: true);
        }

        /// <summary>
        /// 完了ウィンドウをキーで表示する
        /// </summary>
        /// <param name="key">文言キー</param>
        /// <param name="fallback">フォールバック</param>
        public void ShowLocalized(string key, string fallback)
        {
            ShowLocalized(key, fallback, closeEnabled: true);
        }

        /// <summary>
        /// メッセージウィンドウをキーで表示する
        /// </summary>
        /// <param name="key">文言キー</param>
        /// <param name="fallback">フォールバック</param>
        /// <param name="closeEnabled">閉じるボタンを出すか</param>
        public void ShowLocalized(string key, string fallback, bool closeEnabled)
        {
            BindCloseButton();
            isShowing = true;
            messageKey = key ?? GameTextKeys.SaveComplete;
            messageFallback = fallback ?? "セーブが完了しました";
            ApplyMessage();
            SetCloseButtonEnabled(closeEnabled);
            CanvasVisibilityUtility.SetCanvasEnabled(canvas, true, CanvasSortingOrder);
        }

        /// <summary>
        /// 完了ウィンドウを閉じる
        /// </summary>
        public void Hide()
        {
            isShowing = false;
            SetCloseButtonEnabled(true);
            CanvasVisibilityUtility.SetCanvasEnabled(canvas, false, CanvasSortingOrder);
        }

        /// <inheritdoc/>
        public void RefreshLocalizedUi()
        {
            ApplyCloseLabel();
            if (!isShowing)
            {
                return;
            }

            ApplyMessage();
        }

        private void ApplyMessage()
        {
            if (messageText == null)
            {
                return;
            }

            messageText.text = LocalizedText.GetOrFallback(messageKey, messageFallback);
        }

        private bool closeOriginalCaptured;
        private string closeOriginal = "閉じる";

        private void ApplyCloseLabel()
        {
            if (!closeOriginalCaptured)
            {
                closeOriginal = SceneLocalizedLabel.Capture(closeButton, closeOriginal);
                closeOriginalCaptured = true;
            }

            LhButtonLabelUtility.SetLabel(
                closeButton,
                SceneLocalizedLabel.Resolve(GameTextKeys.CommonClose, closeOriginal));
        }

        private void SetCloseButtonEnabled(bool enabled)
        {
            closeButtonEnabled = enabled;
            if (closeButton == null)
            {
                return;
            }

            closeButton.gameObject.SetActive(enabled);
        }

        private void BindCloseButton()
        {
            ApplyCloseLabel();
            if (isBound || closeButton == null)
            {
                return;
            }

            closeButton.SubscribeOnClick(OnCloseClicked);
            isBound = true;
        }

        private void OnCloseClicked()
        {
            if (!closeButtonEnabled)
            {
                return;
            }

            Hide();
            closedSubject.OnNext(Unit.Default);
        }

        private void OnDestroy()
        {
            closedSubject.Dispose();
        }
    }
}
