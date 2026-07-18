using Battle.Interface;
using Cysharp.Threading.Tasks;
using R3;
using System;
using System.Threading;

namespace Battle.Presenter
{
    /// <summary>
    /// とどめ命中イベントをFinish演出Viewへ渡す
    /// </summary>
    public sealed class BattleFinishPresenter : IDisposable
    {
        private readonly IBattleFinishPresentation finishPresentation;
        private readonly CancellationToken cancellationToken;
        private readonly CompositeDisposable disposables = new CompositeDisposable();

        /// <summary>
        /// とどめ命中イベントを購読してFinish演出を再生する
        /// </summary>
        public BattleFinishPresenter(
            IBattleFinishPresentation finishPresentation,
            BattleSystem system,
            CancellationToken cancellationToken)
        {
            this.finishPresentation = finishPresentation;
            this.cancellationToken = cancellationToken;

            if (finishPresentation == null || system == null)
            {
                return;
            }

            system.OnMoveUsed
                .Where(result => result.IsKnockout)
                .Subscribe(_ =>
                {
                    if (finishPresentation is IBattlePartBreakPresentation partBreakPresentation)
                    {
                        partBreakPresentation.HidePartBreakImmediate();
                    }

                    PlayFinishAsync().Forget();
                })
                .AddTo(disposables);
        }

        private async UniTaskVoid PlayFinishAsync()
        {
            await finishPresentation.PlayFinishAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            disposables.Dispose();
        }
    }
}
