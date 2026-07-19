using Camera.Interface;
using Cysharp.Threading.Tasks;
using Lighthouse.Scene;
using Scene.BattleNpcScene;
using Scene.BattleNpcScene.Interface;
using Scene.Core;
using Scene.TrainingScene.Interface;
using System.Threading;
using VContainer;

namespace Scene.TrainingScene
{
    /// <summary>
    /// ClayEditモデルを使った5日間育成シーン
    /// </summary>
    public sealed class TrainingScene : FadeInSceneBase<TrainingScene.TrainingTransitionData>
    {
        private ITrainingPresenter presenter;
        private IBattleNpcPostProcess postProcess;
        private BattleClassroomLighting classroomLighting;
        private IClayEditCameraPresenter cameraPresenter;
        private TrainingFlowRunner flowRunner;

        public override MainSceneId MainSceneId => ClayMonstersMainSceneId.Training;

        /// <summary>
        /// Trainingシーンへの遷移データ
        /// </summary>
        public sealed class TrainingTransitionData : TransitionDataBase
        {
            public override MainSceneId MainSceneId => ClayMonstersMainSceneId.Training;
        }

        protected override bool PerformEntryFadeIn => false;

        [Inject]
        public void Construct(
            ITrainingPresenter presenter,
            IBattleNpcPostProcess postProcess,
            BattleClassroomLighting classroomLighting,
            IClayEditCameraPresenter cameraPresenter,
            TrainingFlowRunner flowRunner)
        {
            this.presenter = presenter;
            this.postProcess = postProcess;
            this.classroomLighting = classroomLighting;
            this.cameraPresenter = cameraPresenter;
            this.flowRunner = flowRunner;
        }

        protected override UniTask OnSetup()
        {
            cameraPresenter.Setup();
            presenter.Setup();
            return UniTask.CompletedTask;
        }

        protected override UniTask OnEnterCore(ISceneTransitionContext context, CancellationToken cancelToken)
        {
            classroomLighting?.Apply();
            postProcess.Enable();
            cameraPresenter.OnEnter();
            flowRunner.PrepareSelectionLayout();
            return presenter.OnEnterAsync(cancelToken);
        }

        protected override async UniTask OnEnterAfterFadeInCore(ISceneTransitionContext context, CancellationToken cancelToken)
        {
            await flowRunner.RevealEntryAsync(cancelToken);
            flowRunner.StartFlow();
        }

        protected override UniTask OnLeave(ISceneTransitionContext context, CancellationToken cancelToken)
        {
            flowRunner.SaveActiveProgressIfNeeded();
            flowRunner.CleanupForLeave();
            cameraPresenter.OnExit();
            postProcess.Disable();
            classroomLighting?.DisableLighting();
            presenter.OnLeave();
            return base.OnLeave(context, cancelToken);
        }
    }
}
