using Cysharp.Threading.Tasks;
using Lighthouse.Scene;
using Scene.BattleNpcScene.Interface;
using Scene.BattleNpcScene.Tournament;
using Scene.Core;
using Scene.TitleScene;
using System.Threading;
using UnityEngine;
using VContainer;

namespace Scene.BattleNpcScene
{
    public class BattleNpcScene : FadeInSceneBase<BattleNpcScene.BattleNpcTransitionData>
    {
        IBattleNpcSceneCoordinator sceneCoordinator;
        INpcTournamentEntryState tournamentEntryState;

        public override MainSceneId MainSceneId => ClayMonstersMainSceneId.BattleNpc;

        public class BattleNpcTransitionData : TransitionDataBase
        {
            public override MainSceneId MainSceneId => ClayMonstersMainSceneId.BattleNpc;

            /// <summary>
            /// トーナメント入場か
            /// </summary>
            public bool IsTournament;

            /// <summary>
            /// トーナメント難易度
            /// </summary>
            public NpcTournamentDifficulty TournamentDifficulty;

            /// <summary>
            /// 中断トーナメントの再開か
            /// </summary>
            public bool ResumeTournament;
        }

        protected override bool PerformEntryFadeIn => false;

        [Inject]
        public void Construct(
            IBattleNpcSceneCoordinator sceneCoordinator,
            INpcTournamentEntryState tournamentEntryState)
        {
            this.sceneCoordinator = sceneCoordinator;
            this.tournamentEntryState = tournamentEntryState;
        }

        protected override UniTask OnSetup()
        {
            return UniTask.CompletedTask;
        }

        protected override UniTask OnEnterCore(ISceneTransitionContext context, CancellationToken cancelToken)
        {
            tournamentEntryState?.Clear();
            if (context?.TransitionData is BattleNpcTransitionData data && data.IsTournament)
            {
                tournamentEntryState?.SetTournament(data.TournamentDifficulty, data.ResumeTournament);
                Debug.Log(
                    "[BattleNpcScene] トーナメント入場"
                    + $" difficulty={data.TournamentDifficulty}"
                    + $" resume={data.ResumeTournament}");
            }

            sceneCoordinator.PrepareEnter();
            return UniTask.CompletedTask;
        }

        protected override UniTask OnEnterAfterFadeInCore(ISceneTransitionContext context, CancellationToken cancelToken)
        {
            return sceneCoordinator.RunBattleFlowAsync(cancelToken);
        }

        protected override UniTask OnLeave(ISceneTransitionContext context, CancellationToken cancelToken)
        {
            tournamentEntryState?.Clear();
            sceneCoordinator.Leave();
            return base.OnLeave(context, cancelToken);
        }
    }
}
