using Cysharp.Threading.Tasks;
using System.Threading;

namespace Scene.BattlePVPScene.Interface
{
    /// <summary>
    /// 通信切断時にメッセージと閉じるボタンを表示する
    /// </summary>
    public interface IBattlePvpDisconnectView
    {
        /// <summary>
        /// ウィンドウの表示を切り替える
        /// </summary>
        /// <param name="visible">表示するか</param>
        void SetVisible(bool visible);

        /// <summary>
        /// 閉じるボタン押下を待つ
        /// </summary>
        /// <param name="cancellationToken">キャンセルトークン</param>
        UniTask WaitReturnToTitleAsync(CancellationToken cancellationToken);
    }
}
