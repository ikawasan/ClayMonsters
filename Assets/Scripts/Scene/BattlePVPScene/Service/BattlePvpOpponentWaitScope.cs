using Cysharp.Threading.Tasks;
using Scene.BattlePVPScene.Interface;
using System;
using System.Threading;

namespace Scene.BattlePVPScene.Service
{
    /// <summary>
    /// 相手入力待ち中のみメッセージUIを表示する
    /// </summary>
    public static class BattlePvpOpponentWaitScope
    {
        /// <summary>
        /// 待機処理の前後でメッセージ表示を切り替える
        /// </summary>
        /// <param name="view">待機メッセージView</param>
        /// <param name="waitAsync">相手応答待ち処理</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        public static async UniTask RunAsync(
            IBattlePvpOpponentWaitView view,
            Func<CancellationToken, UniTask> waitAsync,
            CancellationToken cancellationToken)
        {
            if (waitAsync == null)
            {
                return;
            }

            if (view == null)
            {
                await waitAsync(cancellationToken);
                return;
            }

            view.SetVisible(true);
            try
            {
                await waitAsync(cancellationToken);
            }
            finally
            {
                view.SetVisible(false);
            }
        }
    }
}
