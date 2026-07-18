using System.Threading;
using Cysharp.Threading.Tasks;

namespace Scene.Core.Interface
{
    /// <summary>
    /// シーン遷移時の画面フェード(暗転・明転)を行うインターフェース
    /// </summary>
    public interface ISceneFade
    {
        /// <summary>
        /// 画面が暗転で覆われているか
        /// </summary>
        bool IsOpaque { get; }

        /// <summary>
        /// 画面を指定色で覆う(暗転)。完了まで待機する
        /// </summary>
        UniTask FadeOutAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 画面の覆いを消す(明転)。完了まで待機する
        /// </summary>
        UniTask FadeInAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 暗転状態を即時確定する
        /// </summary>
        void EnsureOpaque();
    }
}
