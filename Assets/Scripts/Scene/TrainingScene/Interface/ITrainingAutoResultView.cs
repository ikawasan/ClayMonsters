using Cysharp.Threading.Tasks;
using Scene.TrainingScene.Domain;
using System;
using System.Threading;
using UnityEngine.Events;

namespace Scene.TrainingScene.Interface
{
    /// <summary>
    /// 育成完了リザルト画面の表示と入力
    /// </summary>
    public interface ITrainingAutoResultView
    {
        /// <summary>
        /// 育成完了結果を表示する
        /// </summary>
        /// <param name="presentation">表示データ</param>
        void Show(TrainingAutoResultPresentation presentation);

        /// <summary>
        /// 画面を隠す
        /// </summary>
        void Hide();

        /// <summary>
        /// 続行ボタンが押されるまで待機する
        /// </summary>
        /// <param name="cancellationToken">キャンセルトークン</param>
        UniTask WaitContinueAsync(CancellationToken cancellationToken);

        /// <summary>
        /// タイトルへ戻るが押されるまで待機する
        /// </summary>
        /// <param name="cancellationToken">キャンセルトークン</param>
        UniTask WaitBackToTitleAsync(CancellationToken cancellationToken);

        /// <summary>
        /// タイトルへ戻るクリックを購読する
        /// </summary>
        /// <param name="action">クリック時処理</param>
        /// <returns>購読解除用</returns>
        IDisposable SubscribeBackToTitleClick(UnityAction action);
    }
}
