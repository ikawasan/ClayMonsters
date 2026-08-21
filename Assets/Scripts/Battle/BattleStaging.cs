using Battle.Interface;
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 演出フックの最小実装。待ち時間だけを持つ既定動作で、カメラ等はサブクラスで上書きする。
    /// </summary>
    public class BattleStaging : MonoBehaviour, IBattleStaging
    {
        [Header("各演出の所要時間(秒)")]
        [SerializeField] private float introSeconds = 2f;
        [SerializeField] private float battleStartSeconds = 1f;
        [SerializeField] private float victorySeconds = 2f;

        [Header("勝利演出")]
        [SerializeField] private float victorySpinDegreesPerSecond = 0f;

        /// <summary>
        /// 両モンスターを見せる導入演出既定では一定時間待つだけ
        /// </summary>
        public virtual async UniTask PlayIntroAsync(BattleStagingContext context, CancellationToken cancellationToken)
        {
            await DelayUnscaledAsync(introSeconds, cancellationToken);
        }

        /// <summary>
        /// バトル開始演出既定では一定時間待つだけ
        /// </summary>
        public virtual async UniTask PlayBattleStartAsync(BattleStagingContext context, CancellationToken cancellationToken)
        {
            await DelayUnscaledAsync(battleStartSeconds, cancellationToken);
        }

        /// <summary>
        /// 勝利演出。歩きモーションと軽い跳ねを再生して一定時間待つ。
        /// </summary>
        public virtual async UniTask PlayVictoryAsync(
            BattleStagingContext context,
            BattleUnit winner,
            CancellationToken cancellationToken)
        {
            if (winner == null)
            {
                await DelayUnscaledAsync(victorySeconds, cancellationToken);
                return;
            }

            BattleVictoryWalkSpin.BeginWalk(winner);
            Transform winnerModel = ResolveWinnerModel(context, winner);
            using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            UniTask spinTask = BattleVictoryWalkSpin.SpinWhileAsync(
                winnerModel,
                degreesPerSecond: 0f,
                linkedCts.Token);

            try
            {
                await DelayUnscaledAsync(victorySeconds, cancellationToken);
            }
            finally
            {
                linkedCts.Cancel();
                try
                {
                    await spinTask;
                }
                catch (System.OperationCanceledException)
                {
                }
            }
        }

        /// <summary>
        /// 勝利演出のその場回転速度(度/秒)
        /// </summary>
        protected float VictorySpinDegreesPerSecond => victorySpinDegreesPerSecond;

        protected static Transform ResolveWinnerModel(BattleStagingContext context, BattleUnit winner)
        {
            if (context == null || winner == null)
            {
                return null;
            }

            if (winner == context.Player)
            {
                return context.PlayerModel;
            }

            if (winner == context.Enemy)
            {
                return context.EnemyModel;
            }

            return null;
        }

        protected static async UniTask DelayUnscaledAsync(float seconds, CancellationToken cancellationToken)
        {
            if (seconds <= 0f)
            {
                return;
            }

            float elapsed = 0f;
            while (elapsed < seconds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
        }
    }
}
