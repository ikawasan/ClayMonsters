using Battle.Interface;
using Cysharp.Threading.Tasks;
using Scene.BattleNpcScene.Interface;
using System.Threading;
using UI.ClayEditor.View;
using VContainer;

namespace Scene.BattleNpcScene
{
    /// <summary>
    /// BattleNpcシーンの入場準備と戦闘フロー開始を統括する
    /// </summary>
    public sealed class BattleNpcSceneCoordinator : IBattleNpcSceneCoordinator
    {
        private readonly IBattleNpcPostProcess postProcess;
        private readonly IMonsterSelectionSession selectionSession;
        private readonly IBattleFlowRunner flowRunner;

        /// <summary>
        /// DIで依存を受け取る
        /// </summary>
        [Inject]
        public BattleNpcSceneCoordinator(
            IBattleNpcPostProcess postProcess,
            IMonsterSelectionSession selectionSession,
            IBattleFlowRunner flowRunner)
        {
            this.postProcess = postProcess;
            this.selectionSession = selectionSession;
            this.flowRunner = flowRunner;
        }

        /// <inheritdoc />
        public void PrepareEnter()
        {
            postProcess?.Enable();
            selectionSession?.PrepareEntry();
        }

        /// <inheritdoc />
        public UniTask RunBattleFlowAsync(CancellationToken cancellationToken)
        {
            flowRunner?.StartFlow();
            return UniTask.CompletedTask;
        }

        /// <inheritdoc />
        public void Leave()
        {
            flowRunner?.Stop();
            postProcess?.Disable();
            ModelSaveSlotScrollListView.ExitFullscreenSelectionLayout();
        }
    }
}
