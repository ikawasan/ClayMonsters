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
