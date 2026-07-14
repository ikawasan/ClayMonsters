using Cysharp.Threading.Tasks;
using System.Threading;

namespace Scene.BattleNpcScene.Interface
{
    /// <summary>
    /// BattleNpcシーン入場から戦闘フロー完了までを統括する
    /// </summary>
    public interface IBattleNpcSceneCoordinator
    {
        /// <summary>
        /// シーン入場直後の準備
        /// </summary>
        void PrepareEnter();

        /// <summary>
        /// 戦闘フローを開始する
        /// シーン遷移完了をブロックしない
        /// </summary>
        /// <param name="cancellationToken">キャンセルトークン</param>
        UniTask RunBattleFlowAsync(CancellationToken cancellationToken);

        /// <summary>
        /// シーン退場時の後片付け
        /// </summary>
        void Leave();
    }
}
