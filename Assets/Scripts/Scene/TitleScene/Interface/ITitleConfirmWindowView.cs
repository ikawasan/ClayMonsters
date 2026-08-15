using System;
using UnityEngine.Events;

namespace Scene.TitleScene.Interface
{
    /// <summary>
    /// タイトルのはいいいえ確認ウィンドウを制御する
    /// </summary>
    public interface ITitleConfirmWindowView
    {
        /// <summary>
        /// キーで確認メッセージを表示する
        /// </summary>
        /// <param name="key">文言キー</param>
        /// <param name="fallback">フォールバック</param>
        void ShowLocalized(string key, string fallback);

        /// <summary>
        /// パラメータ付きキーで確認メッセージを表示する
        /// </summary>
        /// <param name="key">文言キー</param>
        /// <param name="fallback">フォールバックテンプレート</param>
        /// <param name="paramName">置換パラメータ名</param>
        /// <param name="paramValue">置換パラメータ値</param>
        void ShowLocalized(string key, string fallback, string paramName, object paramValue);

        /// <summary>
        /// はいいいえボタン文言を指定して確認メッセージを表示する
        /// </summary>
        /// <param name="messageKey">メッセージキー</param>
        /// <param name="messageFallback">メッセージフォールバック</param>
        /// <param name="yesKey">はいボタンキー</param>
        /// <param name="yesFallback">はいボタンフォールバック</param>
        /// <param name="noKey">いいえボタンキー</param>
        /// <param name="noFallback">いいえボタンフォールバック</param>
        void ShowLocalizedChoice(
            string messageKey,
            string messageFallback,
            string yesKey,
            string yesFallback,
            string noKey,
            string noFallback);

        /// <summary>
        /// 確認ウィンドウを閉じる
        /// </summary>
        void Hide();

        /// <summary>
        /// はいボタン押下を購読する
        /// </summary>
        /// <param name="action">押下時の処理</param>
        /// <returns>購読解除用のDisposable</returns>
        IDisposable SubscribeYesButtonClick(UnityAction action);

        /// <summary>
        /// いいえボタン押下を購読する
        /// </summary>
        /// <param name="action">押下時の処理</param>
        /// <returns>購読解除用のDisposable</returns>
        IDisposable SubscribeNoButtonClick(UnityAction action);
    }
}
