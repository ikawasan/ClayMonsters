using Extensions;
using LighthouseExtends.UIComponent.Button;
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
    public sealed class ModelSaveCompleteView : MonoBehaviour
    {
        private const int CanvasSortingOrder = 110;

        [SerializeField] private Canvas canvas;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private LHButton closeButton;

        private readonly Subject<Unit> closedSubject = new Subject<Unit>();
        private bool isBound;

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
            BindCloseButton();
            if (messageText != null)
            {
                messageText.text = message ?? string.Empty;
            }

            CanvasVisibilityUtility.SetCanvasEnabled(canvas, true, CanvasSortingOrder);
        }

        /// <summary>
        /// 完了ウィンドウを閉じる
        /// </summary>
        public void Hide()
        {
            CanvasVisibilityUtility.SetCanvasEnabled(canvas, false, CanvasSortingOrder);
        }

        private void BindCloseButton()
        {
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
