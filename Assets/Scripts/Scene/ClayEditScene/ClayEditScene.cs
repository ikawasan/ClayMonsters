using Cysharp.Threading.Tasks;
using Lighthouse.Scene;
using Lighthouse.Scene.SceneBase;
using Scene.ClayEditScene.Interface;
using Scene.Core;
using VContainer;

namespace Scene.ClayEditScene
{
    public class ClayEditScene : CanvasMainSceneBase<ClayEditScene.ClayEditTransitionData>
    {
        IClayEditPresenter clayEditPresenter;

        public override MainSceneId MainSceneId => ClayMonstersMainSceneId.ClayEdit;

        public class ClayEditTransitionData : TransitionDataBase
        {
            public override MainSceneId MainSceneId => ClayMonstersMainSceneId.ClayEdit;
        }

        [Inject]
        public void Construct(IClayEditPresenter clayEditPresenter)
        {
            this.clayEditPresenter = clayEditPresenter;
        }

        protected override UniTask OnSetup()
        {
            clayEditPresenter.Setup();
            return UniTask.CompletedTask;
        }
    }
}