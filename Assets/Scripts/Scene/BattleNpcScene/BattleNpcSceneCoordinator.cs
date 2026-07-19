using Battle.Interface;
using Cysharp.Threading.Tasks;
using Scene.BattleNpcScene.Interface;
using Scene.BattleNpcScene.View;
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
        private readonly BattleClassroomLighting classroomLighting;
        private readonly IMonsterSelectionSession selectionSession;
        private readonly IBattleFlowRunner flowRunner;
        private readonly BattleMatchupBackgroundView matchupBackground;

        /// <summary>
        /// DIで依存を受け取る
        /// </summary>
        [Inject]
        public BattleNpcSceneCoordinator(
            IBattleNpcPostProcess postProcess,
            BattleClassroomLighting classroomLighting,
            IMonsterSelectionSession selectionSession,
            IBattleFlowRunner flowRunner,
            BattleMatchupBackgroundView matchupBackground)
        {
            this.postProcess = postProcess;
            this.classroomLighting = classroomLighting;
            this.selectionSession = selectionSession;
            this.flowRunner = flowRunner;
            this.matchupBackground = matchupBackground;
        }

        /// <inheritdoc />
        public void PrepareEnter()
        {
            classroomLighting?.Apply();
            postProcess?.Enable();
            // シーン上Fieldは初期非アクティブのため選択前に表示する
            matchupBackground?.ShowClassroom();
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
            flowRunner?.CleanupForLeave();
            selectionSession?.HideForLeave();
            postProcess?.Disable();
            classroomLighting?.DisableLighting();
            ModelSaveSlotScrollListView.ExitFullscreenSelectionLayout();
        }
    }
}
