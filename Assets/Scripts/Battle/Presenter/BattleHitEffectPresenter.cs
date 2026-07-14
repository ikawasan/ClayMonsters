using System;
using Battle.Interface;
using Battle.View;
using R3;
using UnityEngine;

namespace Battle.Presenter
{
    /// <summary>
    /// 戦闘エンジンの命中イベントをヒット演出Viewへ渡す
    /// </summary>
    public sealed class BattleHitEffectPresenter : IDisposable
    {
        private readonly IBattleHitEffect hitEffect;
        private readonly BattleHitEffectView hitEffectView;
        private readonly BattleSystem system;
        private readonly Transform playerRoot;
        private readonly Transform enemyRoot;
        private readonly CompositeDisposable disposables = new CompositeDisposable();

        /// <summary>
        /// 命中イベントを購読してヒット演出を再生する
        /// </summary>
        public BattleHitEffectPresenter(
            IBattleHitEffect hitEffect,
            BattleSystem system,
            Transform playerRoot,
            Transform enemyRoot)
        {
            this.hitEffect = hitEffect;
            hitEffectView = hitEffect as BattleHitEffectView;
            this.system = system;
            this.playerRoot = playerRoot;
            this.enemyRoot = enemyRoot;

            system.OnMoveUsed
                .Subscribe(OnMoveUsed)
                .AddTo(disposables);
        }

        private void OnMoveUsed(MoveUsedResult result)
        {
            if (!result.Hit || hitEffect == null)
            {
                return;
            }

            Transform targetRoot = result.Target == system.Player ? playerRoot : enemyRoot;
            if (targetRoot == null)
            {
                return;
            }

            Vector3 position = hitEffectView != null
                ? hitEffectView.ResolveWorldHitPoint(targetRoot)
                : BattleHitEffectView.ResolveWorldHitPoint(targetRoot, 1.2f);
            hitEffect.PlayHit(position, result.PartLost || result.IsKnockout);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            disposables.Dispose();
        }
    }
}
