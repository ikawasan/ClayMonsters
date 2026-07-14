using Battle;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace Battle.Interface
{
    /// <summary>
    /// 勝利演出後にタイトル戻りと再戦の2択を表示するUI
    /// </summary>
    public interface IBattleDualVictoryReturnView
    {
        /// <summary>
        /// 2択ボタンの表示を切り替える
        /// </summary>
        /// <param name="visible">表示するか</param>
        void SetDualButtonsVisible(bool visible);

        /// <summary>
        /// どちらかのボタンが押されるまで待機する
        /// </summary>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>選択された遷移先</returns>
        UniTask<BattleVictoryReturnChoice> WaitVictoryReturnChoiceAsync(CancellationToken cancellationToken);
    }
}
