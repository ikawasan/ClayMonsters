using Cysharp.Threading.Tasks;
using Extensions;
using Lighthouse.Scene;
using Scene.BattleNpcScene.Interface;
using Scene.BattlePVPScene.Interface;
using Scene.Core;
using System.Diagnostics;
using System.Threading;
using UnityEngine;
using VContainer;
using Debug = UnityEngine.Debug;

namespace Scene.BattlePVPScene
{
    /// <summary>
    /// 通信対戦シーンの入退場とマッチングUIを管理する
    /// </summary>
    public sealed class BattlePVPScene : FadeInSceneBase<BattlePVPScene.BattlePVPTransitionData>
    {
        IBattlePVPPresenter battlePvpPresenter;
        BattlePvpFlowRunner battlePvpFlowRunner;
        IBattleNpcPostProcess battleNpcPostProcess;

        public override MainSceneId MainSceneId => ClayMonstersMainSceneId.BattlePVP;

        /// <summary>
        /// BattlePVPシーン遷移データ
        /// </summary>
        public class BattlePVPTransitionData : TransitionDataBase
        {
            public override MainSceneId MainSceneId => ClayMonstersMainSceneId.BattlePVP;
        }

        // 入場時にマッチングUIを表示済みのため基底の明転を行いフェードオーバーレイの入力ブロックを解除する
        protected override bool PerformEntryFadeIn => true;

        [Inject]
        public void Construct(
            IBattlePVPPresenter battlePvpPresenter,
            BattlePvpFlowRunner battlePvpFlowRunner,
            IBattleNpcPostProcess battleNpcPostProcess)
        {
            this.battlePvpPresenter = battlePvpPresenter;
            this.battlePvpFlowRunner = battlePvpFlowRunner;
            this.battleNpcPostProcess = battleNpcPostProcess;
        }

        protected override UniTask OnSetup()
        {
            BattlePvpSceneBootstrap.EnsureSceneReady(gameObject.scene, "OnSetup");
            BattlePvpLifetimeScopeSetup.EnsureDedicatedScope(transform);
            battlePvpPresenter.Setup();
            return UniTask.CompletedTask;
        }

        protected override UniTask OnEnterCore(ISceneTransitionContext context, CancellationToken cancelToken)
        {
            BattlePvpSceneBootstrap.EnsureSceneReady(gameObject.scene, "OnEnterCore");
            BattlePvpSceneDiagnostics.LogState("OnEnterCore開始");
            BattlePvpLifetimeScopeSetup.EnsureDedicatedScope(transform);
            battleNpcPostProcess?.Enable();
            battlePvpFlowRunner.PrepareEntryLayout();
            BattlePvpSceneDiagnostics.LogState("OnEnterCore完了");
            return UniTask.CompletedTask;
        }

        protected override UniTask OnEnterAfterFadeInCore(ISceneTransitionContext context, CancellationToken cancelToken)
        {
            BattlePvpSceneBootstrap.EnsureSceneReady(gameObject.scene, "OnEnterAfterFadeInCore");
            BattlePvpLifetimeScopeSetup.EnsureDedicatedScope(transform);
            BattlePvpContentPersistence.EnsurePersisted(gameObject);
            BattlePvpSceneDiagnostics.LogState("OnEnterAfterFadeInCore");
            return UniTask.CompletedTask;
        }

        protected override UniTask OnLeave(ISceneTransitionContext context, CancellationToken cancelToken)
        {
            Debug.LogWarning("[BattlePvpScene] OnLeave BattlePVPシーン退場");
            battlePvpFlowRunner.CleanupForLeave();
            battleNpcPostProcess?.Disable();
            return base.OnLeave(context, cancelToken);
        }

        private void OnDisable()
        {
            if (!ApplicationQuitGuard.IsQuitting)
            {
                Debug.LogWarning(
                    "[BattlePvpScene] BattlePVPSceneルートが非アクティブになりました"
                    + $" ownerScene={gameObject.scene.name}");
            }
        }

        private void OnDestroy()
        {
            if (!ApplicationQuitGuard.IsQuitting)
            {
                Debug.LogError(
                    "[BattlePvpScene] BattlePVPSceneルートがDestroyされました"
                    + $" ownerScene={gameObject.scene.name}"
                    + $" stack={new StackTrace(1, true)}");
            }
        }
    }
}
