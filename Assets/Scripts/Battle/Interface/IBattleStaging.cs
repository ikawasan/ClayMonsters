using System.Threading;
using Cysharp.Threading.Tasks;

namespace Battle.Interface
{
    /// <summary>
    /// バトルのカメラ・開始/勝利などの演出フック。シーンごとの見せ方を差し替えるための契約。
    /// </summary>
    public interface IBattleStaging
    {
        /// <summary>
        /// 両モンスターを見せる導入演出(カメラ)を再生する
        /// </summary>
        UniTask PlayIntroAsync(BattleStagingContext context, CancellationToken cancellationToken);

        /// <summary>
        /// バトル開始の演出を再生する
        /// </summary>
        UniTask PlayBattleStartAsync(BattleStagingContext context, CancellationToken cancellationToken);

        /// <summary>
        /// 勝利演出を再生する(winnerがnullなら引き分け)。
        /// </summary>
        UniTask PlayVictoryAsync(
            BattleStagingContext context,
            BattleUnit winner,
            CancellationToken cancellationToken);
    }
}