using System.Threading;
using Cysharp.Threading.Tasks;

namespace Battle.Interface
{
    /// <summary>
    /// 部位破壊時のBreak文字演出を再生する
    /// </summary>
    public interface IBattlePartBreakPresentation
    {
        /// <summary>
        /// Break文字と演出を再生する
        /// </summary>
        /// <param name="cancellationToken">キャンセルトークン</param>
        UniTask PlayPartBreakAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Break演出を即座に隠す
        /// </summary>
        void HidePartBreakImmediate();
    }
}
