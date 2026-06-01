using Cysharp.Threading.Tasks;
using Lighthouse.Scene;
using Lighthouse.Scene.SceneBase;
using Scene.BattleNpcScene.Interface;
using Scene.Core;
using VContainer;

namespace Scene.BattleNpcScene
{
    public class BattleNpcScene : CanvasMainSceneBase<BattleNpcScene.BattleNpcTransitionData>
    {
        IBattleNpcPresenter battleNpcPresenter;

        public override MainSceneId MainSceneId => ClayMonstersMainSceneId.BattleNpc;

        public class BattleNpcTransitionData : TransitionDataBase
        {
            public override MainSceneId MainSceneId => ClayMonstersMainSceneId.BattleNpc;
        }

        [Inject]
        public void Construct(IBattleNpcPresenter battleNpcPresenter)
        {
            this.battleNpcPresenter = battleNpcPresenter;
        }

        protected override UniTask OnSetup()
        {
            battleNpcPresenter.Setup();
            return UniTask.CompletedTask;
        }
    }
}