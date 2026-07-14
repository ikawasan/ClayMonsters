using Cysharp.Threading.Tasks;
using System.Threading;

namespace Scene.BattlePVPScene.Interface
{
    /// <summary>
    /// 通信切断時にメッセージとタイトル戻りボタンを表示する
    /// </summary>
    public interface IBattlePvpDisconnectView
    {
        /// <summary>
        /// ウィンドウの表示を切り替える
        /// </summary>
        /// <param name="visible">表示するか</param>
        void SetVisible(bool visible);

        /// <summary>
        /// タイトルへ戻るボタン押下を待つ
        /// </summary>
        /// <param name="cancellationToken">キャンセルトークン</param>
        UniTask WaitReturnToTitleAsync(CancellationToken cancellationToken);
    }
}
