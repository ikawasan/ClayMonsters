using Battle.Interface;
using Cysharp.Threading.Tasks;
using R3;
using System;
using System.Threading;

namespace Battle.Presenter
{
    /// <summary>
    /// 部位破壊イベントをBreak演出Viewへ渡す
    /// とどめ時はFinish演出に任せる
    /// </summary>
    public sealed class BattlePartBreakPresenter : IDisposable
    {
        private readonly IBattlePartBreakPresentation partBreakPresentation;
        private readonly CancellationToken cancellationToken;
        private readonly CompositeDisposable disposables = new CompositeDisposable();

        /// <summary>
        /// 部位破壊イベントを購読してBreak演出を再生する
        /// </summary>
        public BattlePartBreakPresenter(
            IBattlePartBreakPresentation partBreakPresentation,
            BattleSystem system,
            CancellationToken cancellationToken)
        {
            this.partBreakPresentation = partBreakPresentation;
            this.cancellationToken = cancellationToken;

            if (partBreakPresentation == null || system == null)
            {
                return;
            }

            system.OnMoveUsed
                .Where(result => result.Hit && result.PartLost && !result.IsKnockout)
                .Subscribe(_ => PlayPartBreakAsync().Forget())
                .AddTo(disposables);
        }

        private async UniTaskVoid PlayPartBreakAsync()
        {
            await partBreakPresentation.PlayPartBreakAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            disposables.Dispose();
        }
    }
}
