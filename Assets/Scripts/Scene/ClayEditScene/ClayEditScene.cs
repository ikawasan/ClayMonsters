using Camera.Interface;
using Cysharp.Threading.Tasks;
using Lighthouse.Scene;
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
            ClayEditSceneResetter sceneResetter)
        {
            this.clayEditPresenter = clayEditPresenter;
            this.cameraPresenter = cameraPresenter;
            this.clayEditPostProcess = clayEditPostProcess;
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
            clayEditPresenter.OnEnter();
            cameraPresenter.OnEnter();
            clayEditPostProcess.Enable();
            return UniTask.CompletedTask;
        }

        protected override UniTask OnEnterAfterFadeInCore(ISceneTransitionContext context, CancellationToken cancelToken)
        {
            clayEditPresenter.OnEnterAfterFadeIn();
            cameraPresenter.OnEnterAfterFadeIn();
            return UniTask.CompletedTask;
        }

        protected override UniTask OnLeave(ISceneTransitionContext context, CancellationToken cancelToken)
        {
            clayEditPostProcess.Disable();
            cameraPresenter.OnExit();
            sceneResetter.Reset();

            return base.OnLeave(context, cancelToken);
        }
    }
}
