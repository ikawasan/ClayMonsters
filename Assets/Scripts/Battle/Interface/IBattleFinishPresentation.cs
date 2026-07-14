using System.Threading;
using Cysharp.Threading.Tasks;

namespace Battle.Interface
{
    /// <summary>
    /// とどめ命中時のFinish文字演出を再生する
    /// </summary>
    public interface IBattleFinishPresentation
    {
        /// <summary>
        /// Finish文字と演出を再生する
        /// </summary>
        /// <param name="cancellationToken">キャンセルトークン</param>
        UniTask PlayFinishAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Finish演出を即座に隠す
        /// </summary>
        void HideFinishImmediate();
    }
}
