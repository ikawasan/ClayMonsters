using System.Threading;
using Cysharp.Threading.Tasks;

namespace Battle.Interface
{
    /// <summary>
    /// 勝利演出後に表示する戻るボタンUI
    /// </summary>
    public interface IBattleVictoryReturnView
    {
        /// <summary>
        /// 戻るボタンの表示を切り替える
        /// </summary>
        /// <param name="visible">表示するか</param>
        void SetReturnButtonVisible(bool visible);

        /// <summary>
        /// 戻るボタンが押されるまで待機する
        /// </summary>
        /// <param name="cancellationToken">キャンセルトークン</param>
        UniTask WaitReturnButtonClickAsync(CancellationToken cancellationToken);
    }
}
