using Cysharp.Threading.Tasks;
using Lighthouse.Scene;
using Lighthouse.Scene.SceneBase;
using Scene.Core;
using Scene.TitleScene.Interfaces;
using VContainer;

namespace Scene.TitleScene
{
    public class TitleScene : CanvasMainSceneBase<TitleScene.TitleTransitionData>
    {
        ITitlePresenter titlePresenter;

        public override MainSceneId MainSceneId => ClayMonstersMainSceneId.Title;

        // クラス内にネストして遷移データを定義
        public class TitleTransitionData : TransitionDataBase
        {
            public override MainSceneId MainSceneId => ClayMonstersMainSceneId.Title;
        }

        [Inject]
        public void Construct(ITitlePresenter titlePresenter)
        {
            this.titlePresenter = titlePresenter;
        }

        protected override UniTask OnSetup()
        {
            titlePresenter.Setup();
            return UniTask.CompletedTask;
        }
    }
}