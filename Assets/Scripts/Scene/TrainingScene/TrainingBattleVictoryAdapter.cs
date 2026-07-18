using Battle.Interface;
using Cysharp.Threading.Tasks;
using Scene.TrainingScene.Interface;
using System.Threading;

namespace Scene.TrainingScene
{
    /// <summary>
    /// 育成中の放課後戦闘で勝利演出後の続行入力を受け取る
    /// 続行時はオーバーレイホストのみ表示し曜日や状態パネルは出さない
    /// </summary>
    public sealed class TrainingBattleVictoryAdapter : IBattleVictoryReturnView
    {
        private readonly ITrainingHudView hudView;

        /// <summary>
        /// 育成HUDを使った勝利後入力アダプタを生成する
        /// </summary>
        /// <param name="hudView">育成HUD</param>
        public TrainingBattleVictoryAdapter(ITrainingHudView hudView)
        {
            this.hudView = hudView;
        }

        /// <inheritdoc/>
        public void SetReturnButtonVisible(bool visible)
        {
            if (hudView == null)
            {
                return;
            }

            if (visible)
            {
                hudView.ShowOverlayHost();
            }
            else
            {
                hudView.Hide();
            }
        }

        /// <inheritdoc/>
        public UniTask WaitReturnButtonClickAsync(CancellationToken cancellationToken)
        {
            return hudView != null
                ? hudView.WaitContinueAsync(cancellationToken)
                : UniTask.CompletedTask;
        }
    }
}