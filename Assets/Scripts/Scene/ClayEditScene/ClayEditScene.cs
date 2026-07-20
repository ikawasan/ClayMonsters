using Camera.Interface;
using Cysharp.Threading.Tasks;
using Lighthouse.Scene;
using Scene.BattleNpcScene;
using Scene.ClayEditScene.Interface;
using Scene.Core;
using System.Threading;
using VContainer;

namespace Scene.ClayEditScene
{
    public class ClayEditScene : FadeInSceneBase<ClayEditScene.ClayEditTransitionData>
    {
        IClayEditPresenter clayEditPresenter;
        IClayEditCameraPresenter cameraPresenter;
        IClayEditPostProcess clayEditPostProcess;
        ClayEditSceneResetter sceneResetter;
        BattleClassroomLighting classroomLighting;

        public override MainSceneId MainSceneId => ClayMonstersMainSceneId.ClayEdit;

        public class ClayEditTransitionData : TransitionDataBase
        {
            public override MainSceneId MainSceneId => ClayMonstersMainSceneId.ClayEdit;
        }

        [Inject]
        public void Construct(
            IClayEditPresenter clayEditPresenter,
            IClayEditCameraPresenter cameraPresenter,
            IClayEditPostProcess clayEditPostProcess,
            ClayEditSceneResetter sceneResetter,
            BattleClassroomLighting classroomLighting)
        {
            this.clayEditPresenter = clayEditPresenter;
            this.cameraPresenter = cameraPresenter;
            this.clayEditPostProcess = clayEditPostProcess;
            this.sceneResetter = sceneResetter;
            this.classroomLighting = classroomLighting;
        }

        protected override UniTask OnSetup()
        {
            clayEditPresenter.Setup();
            cameraPresenter.Setup();
            return UniTask.CompletedTask;
        }

        protected override UniTask OnEnterCore(ISceneTransitionContext context, CancellationToken cancelToken)
        {
            clayEditPresenter.OnEnter();
            cameraPresenter.OnEnter();
            clayEditPostProcess.Enable();
            classroomLighting?.Apply();
            return UniTask.CompletedTask;
        }

        protected override UniTask OnEnterAfterFadeInCore(ISceneTransitionContext context, CancellationToken cancelToken)
        {
            clayEditPresenter.OnEnterAfterFadeIn();
            cameraPresenter.OnEnterAfterFadeIn();
            classroomLighting?.Apply();
            return UniTask.CompletedTask;
        }

        protected override UniTask OnLeave(ISceneTransitionContext context, CancellationToken cancelToken)
        {
            clayEditPresenter.OnLeave();
            clayEditPostProcess.Disable();
            cameraPresenter.OnExit();
            sceneResetter.Reset();

            return base.OnLeave(context, cancelToken);
        }
    }
}
