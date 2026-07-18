using Extensions;
using LighthouseExtends.UIComponent.Button;
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
    public sealed class TitleMessageWindowView : MonoBehaviour, ITitleMessageWindowView
    {
        [SerializeField] private Canvas canvas;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private LHButton okButton;

        private void Awake()
        {
            ValidateSceneUi();
            Hide();
        }

        /// <inheritdoc/>
        public void Show(string message)
        {
            if (messageText != null)
            {
                messageText.text = message ?? string.Empty;
            }

            if (canvas != null)
            {
                canvas.enabled = true;
            }
        }

        /// <inheritdoc/>
        public void Hide()
        {
            if (canvas != null)
            {
                canvas.enabled = false;
            }
        }

        /// <inheritdoc/>
        public IDisposable SubscribeOkButtonClick(UnityAction action) => okButton.SubscribeOnClick(action);

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
