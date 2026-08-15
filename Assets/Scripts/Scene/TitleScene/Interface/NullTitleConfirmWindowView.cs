using System;
using UnityEngine.Events;

namespace Scene.TitleScene.Interface
{
    /// <summary>
    /// 未配線時のはいいいえ確認ウィンドウダミー
    /// </summary>
    public sealed class NullTitleConfirmWindowView : ITitleConfirmWindowView
    {
        /// <inheritdoc/>
        public void ShowLocalized(string key, string fallback)
        {
        }

        /// <inheritdoc/>
        public void ShowLocalized(string key, string fallback, string paramName, object paramValue)
        {
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
        }

        /// <inheritdoc/>
        public void Hide()
        {
        }

        /// <inheritdoc/>
        public IDisposable SubscribeYesButtonClick(UnityAction action) => EmptyDisposable.Instance;

        /// <inheritdoc/>
        public IDisposable SubscribeNoButtonClick(UnityAction action) => EmptyDisposable.Instance;

        private sealed class EmptyDisposable : IDisposable
        {
            public static readonly EmptyDisposable Instance = new EmptyDisposable();

            public void Dispose()
            {
            }
        }
    }
}
