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
    /// メッセージと閉じるボタンのみを表示する
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
        /// 完了ウィンドウを表示する
        /// </summary>
        /// <param name="message">表示メッセージ</param>
        public void Show(string message)
        {
            ShowLocalized(GameTextKeys.SaveComplete, message);
        }

        /// <summary>
        /// 完了ウィンドウをキーで表示する
        /// </summary>
        /// <param name="key">文言キー</param>
        /// <param name="fallback">フォールバック</param>
        public void ShowLocalized(string key, string fallback)
        {
            BindCloseButton();
            isShowing = true;
            messageKey = key ?? GameTextKeys.SaveComplete;
            messageFallback = fallback ?? "セーブが完了しました";
            ApplyMessage();
            CanvasVisibilityUtility.SetCanvasEnabled(canvas, true, CanvasSortingOrder);
        }

        /// <summary>
        /// 完了ウィンドウを閉じる
        /// </summary>
        public void Hide()
        {
            isShowing = false;
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
            Hide();
            closedSubject.OnNext(Unit.Default);
        }

        private void OnDestroy()
        {
            closedSubject.Dispose();
        }
    }
}
