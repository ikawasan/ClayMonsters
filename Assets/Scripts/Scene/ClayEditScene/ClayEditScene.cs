using Camera.Interface;
using Cysharp.Threading.Tasks;
using Lighthouse.Scene;
using Scene.BattleNpcScene.Interface;
using Scene.ClayEditScene.Interface;
using Scene.Core;
using Scene.BattleNpcScene;
using System.Threading;
using VContainer;

namespace Scene.ClayEditScene
{
    public class ClayEditScene : FadeInSceneBase<ClayEditScene.ClayEditTransitionData>
    {
        IClayEditPresenter clayEditPresenter;
        IClayEditCameraPresenter cameraPresenter;
        IBattleNpcPostProcess battleNpcPostProcess;
        BattleClassroomLighting classroomLighting;
        ClayEditSceneResetter sceneResetter;

        public override MainSceneId MainSceneId => ClayMonstersMainSceneId.ClayEdit;

        public class ClayEditTransitionData : TransitionDataBase
        {
            public override MainSceneId MainSceneId => ClayMonstersMainSceneId.ClayEdit;
        }

        [Inject]
        public void Construct(
            IClayEditPresenter clayEditPresenter,
            IClayEditCameraPresenter cameraPresenter,
            IBattleNpcPostProcess battleNpcPostProcess,
            BattleClassroomLighting classroomLighting,
            ClayEditSceneResetter sceneResetter)
        {
            this.clayEditPresenter = clayEditPresenter;
            this.cameraPresenter = cameraPresenter;
            this.battleNpcPostProcess = battleNpcPostProcess;
            this.classroomLighting = classroomLighting;
            this.sceneResetter = sceneResetter;
        }

        protected override UniTask OnSetup()
        {
            clayEditPresenter.Setup();
            cameraPresenter.Setup();
            return UniTask.CompletedTask;
        }

        protected override UniTask OnEnterCore(ISceneTransitionContext context, CancellationToken cancelToken)
        {
            classroomLighting.Apply();
            clayEditPresenter.OnEnter();
            cameraPresenter.OnEnter();
            battleNpcPostProcess.Enable();
            return UniTask.CompletedTask;
        }

        protected override UniTask OnEnterAfterFadeInCore(ISceneTransitionContext context, CancellationToken cancelToken)
        {
            cameraPresenter.OnEnterAfterFadeIn();
            return UniTask.CompletedTask;
        }

        protected override UniTask OnLeave(ISceneTransitionContext context, CancellationToken cancelToken)
        {
            battleNpcPostProcess.Disable();
            classroomLighting.DisableLighting();
            cameraPresenter.OnExit();
            sceneResetter.Reset();

            return base.OnLeave(context, cancelToken);
        }
    }
}
