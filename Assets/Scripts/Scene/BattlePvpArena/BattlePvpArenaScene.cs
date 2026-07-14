using Cysharp.Threading.Tasks;
using Lighthouse.Scene;
using Lighthouse.Scene.SceneCamera;
using Scene.BattleNpcScene.Interface;
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

        public override MainSceneId MainSceneId => ClayMonstersMainSceneId.BattlePvpArena;

        /// <summary>
        /// BattlePvpArenaシーン遷移データ
        /// </summary>
        public class BattlePvpArenaTransitionData : TransitionDataBase
        {
            public override MainSceneId MainSceneId => ClayMonstersMainSceneId.BattlePvpArena;
        }

        /// <summary>
        /// 遷移暗転オーバーレイを解除してから選択UIを表示する
        /// </summary>
        protected override bool PerformEntryFadeIn => true;

        [Inject]
        public void Construct(
            BattlePvpArenaFlowRunner arenaFlowRunner,
            IBattleNpcPostProcess battleNpcPostProcess)
        {
            this.arenaFlowRunner = arenaFlowRunner;
            this.battleNpcPostProcess = battleNpcPostProcess;
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
            battleNpcPostProcess.Enable();
            BattlePvpArenaFlowRunner runner = ResolveFlowRunner();
            runner?.EnsureSceneReferences(transform);
            runner?.PrepareSelectionLayout();
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
            ResolveFlowRunner()?.StopFlow();
            battleNpcPostProcess.Disable();
            ModelSaveSlotScrollListView.ExitFullscreenSelectionLayout();
            return base.OnLeave(context, cancelToken);
        }

        private async UniTaskVoid ScheduleFlowStart(string reason)
        {
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            ResolveFlowRunner()?.TryStartFlowAfterCanvasInit(reason);
        }
    }
}
