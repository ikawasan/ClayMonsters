using Cysharp.Threading.Tasks;
using Lighthouse.Scene;
using Lighthouse.Scene.SceneBase;
using Scene.BattlePVPScene.Interfaces;
using Scene.Core;
using VContainer;

namespace Scene.BattlePVPScene
{
    public class BattlePVPScene : CanvasMainSceneBase<BattlePVPScene.BattlePVPTransitionData>
    {
        IBattlePVPPresenter battlePVPPresenter;

        public override MainSceneId MainSceneId => ClayMonstersMainSceneId.BattlePVP;

        public class BattlePVPTransitionData : TransitionDataBase
        {
            public override MainSceneId MainSceneId => ClayMonstersMainSceneId.BattlePVP;
        }

        [Inject]
        public void Construct(IBattlePVPPresenter battlePVPPresenter)
        {
            this.battlePVPPresenter = battlePVPPresenter;
        }

        protected override UniTask OnSetup()
        {
            battlePVPPresenter.Setup();
            return UniTask.CompletedTask;
        }
    }
}