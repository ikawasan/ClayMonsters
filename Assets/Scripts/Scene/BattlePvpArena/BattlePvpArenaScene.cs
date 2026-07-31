using Cysharp.Threading.Tasks;
using Lighthouse.Scene;
using Lighthouse.Scene.SceneCamera;
using Scene.BattleNpcScene;
using Scene.BattleNpcScene.Interface;
using Scene.BattleNpcScene.View;
using Scene.BattlePVPScene;
using Scene.Core;
using System.Threading;
using UI.ClayEditor.View;
using UnityEngine;
using VContainer;

namespace Scene.BattlePvpArena
{
    /// <summary>
    /// PvP戦闘Arenaシーンの入退場と戦闘フローを管理する
    /// </summary>
    public sealed class BattlePvpArenaScene : FadeInSceneBase<BattlePvpArenaScene.BattlePvpArenaTransitionData>
    {
        BattlePvpArenaFlowRunner arenaFlowRunner;
        IBattleNpcPostProcess battleNpcPostProcess;
        BattleClassroomLighting classroomLighting;
        BattleMatchupBackgroundView matchupBackground;

        public override MainSceneId MainSceneId => ClayMonstersMainSceneId.BattlePvpArena;

        /// <summary>
        /// BattlePvpArenaシーン遷移データ
        /// </summary>
        public class BattlePvpArenaTransitionData : TransitionDataBase
        {
            public override MainSceneId MainSceneId => ClayMonstersMainSceneId.BattlePvpArena;

            /// <summary>
            /// 特定相手か不特定相手か
            /// </summary>
            public BattlePvpMatchMode MatchMode { get; set; } = BattlePvpMatchMode.Direct;
        }

        /// <summary>
        /// 明転は選択UI表示後にRevealSelectionAsyncで行う
        /// 暗転のまま入場することで背景だけが見える時間をなくす
        /// </summary>
        protected override bool PerformEntryFadeIn => false;

        [Inject]
        public void Construct(
            BattlePvpArenaFlowRunner arenaFlowRunner,
            IBattleNpcPostProcess battleNpcPostProcess,
            BattleClassroomLighting classroomLighting,
            BattleMatchupBackgroundView matchupBackground)
        {
            this.arenaFlowRunner = arenaFlowRunner;
            this.battleNpcPostProcess = battleNpcPostProcess;
            this.classroomLighting = classroomLighting;
            this.matchupBackground = matchupBackground;
        }

        /// <summary>
        /// シーン再入場後も有効なFlowRunner参照を取得する
        /// </summary>
        private BattlePvpArenaFlowRunner ResolveFlowRunner()
        {
            if (arenaFlowRunner)
            {
                return arenaFlowRunner;
            }

            arenaFlowRunner = GetComponent<BattlePvpArenaFlowRunner>();
            if (arenaFlowRunner == null)
            {
                arenaFlowRunner = GetComponentInChildren<BattlePvpArenaFlowRunner>(true);
            }

            if (arenaFlowRunner == null)
            {
                Debug.LogError("[BattlePvpArena] FlowRunnerが見つかりません");
            }

            return arenaFlowRunner;
        }

        protected override UniTask OnSetup()
        {
            ResolveFlowRunner()?.EnsureSceneReferences(transform);
            return UniTask.CompletedTask;
        }

        protected override UniTask OnEnterCore(ISceneTransitionContext context, CancellationToken cancelToken)
        {
            classroomLighting?.Apply();
            battleNpcPostProcess.Enable();
            // シーン上Fieldは初期非アクティブのため入場時に表示する
            matchupBackground?.ShowClassroom();
            BattlePvpArenaFlowRunner runner = ResolveFlowRunner();
            runner?.EnsureSceneReferences(transform);
            runner?.PrepareSelectionLayout();

            BattlePvpMatchMode matchMode = BattlePvpMatchMode.Direct;
            if (context?.TransitionData is BattlePvpArenaTransitionData transitionData)
            {
                matchMode = transitionData.MatchMode;
            }

            runner?.SetMatchMode(matchMode);
            return UniTask.CompletedTask;
        }

        protected override UniTask OnEnterAfterFadeInCore(ISceneTransitionContext context, CancellationToken cancelToken)
        {
            ScheduleFlowStart("OnEnterAfterFadeInCore").Forget();
            return UniTask.CompletedTask;
        }

        /// <inheritdoc/>
        public override void InitializeCanvas(ISceneCamera canvasCamera)
        {
            base.InitializeCanvas(canvasCamera);
            Debug.Log("[BattlePvpArena] InitializeCanvas完了");
            ResolveFlowRunner()?.TryStartFlowAfterCanvasInit("InitializeCanvas");
        }

        public override void OnSceneTransitionFinished(SceneTransitionDiff sceneTransitionDiff)
        {
            ResolveFlowRunner()?.TryStartFlowAfterCanvasInit("OnSceneTransitionFinished");
        }

        protected override UniTask OnLeave(ISceneTransitionContext context, CancellationToken cancelToken)
        {
            ResolveFlowRunner()?.CleanupForLeave();
            battleNpcPostProcess.Disable();
            classroomLighting?.DisableLighting();
            return base.OnLeave(context, cancelToken);
        }

        private async UniTaskVoid ScheduleFlowStart(string reason)
        {
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            ResolveFlowRunner()?.TryStartFlowAfterCanvasInit(reason);
        }
    }
}
