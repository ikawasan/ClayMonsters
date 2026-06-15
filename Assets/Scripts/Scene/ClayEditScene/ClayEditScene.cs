using Camera.Interface;
using Cysharp.Threading.Tasks;
using Lighthouse.Scene;
using Lighthouse.Scene.SceneBase;
using Scene.ClayEditScene.Interface;
using Scene.Core;
using System.Threading;
using VContainer;

namespace Scene.ClayEditScene
{
    public class ClayEditScene : CanvasMainSceneBase<ClayEditScene.ClayEditTransitionData>
    {
        IClayEditPresenter clayEditPresenter;
        IClayEditCameraPresenter cameraPresenter;

        public override MainSceneId MainSceneId => ClayMonstersMainSceneId.ClayEdit;

        public class ClayEditTransitionData : TransitionDataBase
        {
            public override MainSceneId MainSceneId => ClayMonstersMainSceneId.ClayEdit;
        }

        [Inject]
        public void Construct(IClayEditPresenter clayEditPresenter, IClayEditCameraPresenter cameraPresenter)
        {
            this.clayEditPresenter = clayEditPresenter;
            this.cameraPresenter = cameraPresenter;
        }

        protected override UniTask OnSetup()
        {
            clayEditPresenter.Setup();
            cameraPresenter.Setup();
            return UniTask.CompletedTask;
        }

        protected override UniTask OnEnter(ISceneTransitionContext context, CancellationToken cancelToken)
        {
            clayEditPresenter.OnEnter(); 
            cameraPresenter.OnEnter();
            return base.OnEnter(context, cancelToken);
        }
    }
}