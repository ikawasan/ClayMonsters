using System;
using UnityEngine.Events;

namespace Scene.TitleScene.Interface
{
    /// <summary>
    /// タイトル画面のメッセージウィンドウ表示を制御する
    /// </summary>
    public interface ITitleMessageWindowView
    {
        /// <summary>
        /// メッセージを表示する
        /// </summary>
        /// <param name="message">表示文言</param>
        void Show(string message);

        /// <summary>
        /// キーでメッセージを表示する
        /// </summary>
        /// <param name="key">文言キー</param>
        /// <param name="fallback">フォールバック</param>
        void ShowLocalized(string key, string fallback);

        /// <summary>
        /// メッセージウィンドウを閉じる
        /// </summary>
        void Hide();

        /// <summary>
        /// OKボタン押下を購読する
        /// </summary>
        /// <param name="action">押下時の処理</param>
        /// <returns>購読解除用のDisposable</returns>
        IDisposable SubscribeOkButtonClick(UnityAction action);
    }
}
