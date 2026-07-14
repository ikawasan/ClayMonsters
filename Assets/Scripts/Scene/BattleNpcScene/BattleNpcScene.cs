using Cysharp.Threading.Tasks;

using Lighthouse.Scene;

using Scene.BattleNpcScene.Interface;

using Scene.Core;

using System.Threading;

using VContainer;



namespace Scene.BattleNpcScene

{

    public class BattleNpcScene : FadeInSceneBase<BattleNpcScene.BattleNpcTransitionData>

    {

        IBattleNpcSceneCoordinator sceneCoordinator;



        public override MainSceneId MainSceneId => ClayMonstersMainSceneId.BattleNpc;



        public class BattleNpcTransitionData : TransitionDataBase

        {

            public override MainSceneId MainSceneId => ClayMonstersMainSceneId.BattleNpc;

        }



        protected override bool PerformEntryFadeIn => false;



        [Inject]

        public void Construct(IBattleNpcSceneCoordinator sceneCoordinator)

        {

            this.sceneCoordinator = sceneCoordinator;

        }



        protected override UniTask OnSetup()

        {

            return UniTask.CompletedTask;

        }



        protected override UniTask OnEnterCore(ISceneTransitionContext context, CancellationToken cancelToken)

        {

            sceneCoordinator.PrepareEnter();

            return UniTask.CompletedTask;

        }



        protected override UniTask OnEnterAfterFadeInCore(ISceneTransitionContext context, CancellationToken cancelToken)

        {

            return sceneCoordinator.RunBattleFlowAsync(cancelToken);

        }



        protected override UniTask OnLeave(ISceneTransitionContext context, CancellationToken cancelToken)

        {

            sceneCoordinator.Leave();

            return base.OnLeave(context, cancelToken);

        }

    }

}

