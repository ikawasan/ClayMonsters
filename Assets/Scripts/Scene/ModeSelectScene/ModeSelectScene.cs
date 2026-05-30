using Cysharp.Threading.Tasks;
using Lighthouse.Scene;
using Lighthouse.Scene.SceneBase;
using Scene.Core;
using Scene.ModeSelectScene.Interfaces;
using VContainer;

namespace Scene.ModeSelectScene
{
    public class ModeSelectScene : CanvasMainSceneBase<ModeSelectScene.ModeSelectTransitionData>
    {
        IModeSelectPresenter modeSelectPresenter;

        public override MainSceneId MainSceneId => ClayMonstersMainSceneId.ModeSelect;

        // クラス内にネストしてTransitionDataを定義
        public class ModeSelectTransitionData : TransitionDataBase
        {
            public override MainSceneId MainSceneId => ClayMonstersMainSceneId.ModeSelect;
        }

        [Inject]
        public void Construct(IModeSelectPresenter modeSelectPresenter)
        {
            this.modeSelectPresenter = modeSelectPresenter;
        }

        protected override UniTask OnSetup()
        {
            modeSelectPresenter.Setup();
            return UniTask.CompletedTask;
        }
    }
}