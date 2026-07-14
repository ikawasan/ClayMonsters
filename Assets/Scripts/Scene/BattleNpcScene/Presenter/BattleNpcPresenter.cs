using Scene.BattleNpcScene.Interface;
using VContainer;

namespace Scene.BattleNpcScene.Presenter
{
    /// <summary>
    /// BattleNpcシーンのプレゼンター
    /// 勝利後の戻る遷移はBattleFlowRunnerが担当する
    /// </summary>
    public class BattleNpcPresenter : IBattleNpcPresenter
    {
        /// <inheritdoc/>
        void IBattleNpcPresenter.Setup()
        {
        }
    }
}
