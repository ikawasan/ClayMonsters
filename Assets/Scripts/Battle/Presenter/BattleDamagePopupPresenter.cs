using System;
using Battle.Interface;
using Battle.View;
using R3;
using UnityEngine;

namespace Battle.Presenter
{
    /// <summary>
    /// 戦闘エンジンの命中イベントをダメージ表示Viewへ渡す
    /// 外れた攻撃はミスラベルを表示する
    /// </summary>
    public sealed class BattleDamagePopupPresenter : IDisposable
    {
        private readonly IBattleDamagePopup damagePopup;
        private readonly BattleHitEffectView hitEffectView;
        private readonly BattleSystem system;
        private readonly Transform playerRoot;
        private readonly Transform enemyRoot;
        private readonly CompositeDisposable disposables = new CompositeDisposable();

        /// <summary>
        /// 命中イベントを購読してダメージ数値を表示する
        /// </summary>
        public BattleDamagePopupPresenter(
            IBattleDamagePopup damagePopup,
            IBattleHitEffect hitEffect,
            BattleSystem system,
            Transform playerRoot,
            Transform enemyRoot)
        {
            this.damagePopup = damagePopup;
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
            if (damagePopup == null)
            {
                return;
            }

            Transform targetRoot = result.Target == system.Player ? playerRoot : enemyRoot;
            if (targetRoot == null)
            {
                return;
            }

            Vector3 position = ResolveHitPosition(targetRoot);

            if (!result.Hit)
            {
                damagePopup.PlayMiss(position);
                return;
            }

            if (result.Damage <= 0)
            {
                return;
            }

            damagePopup.PlayDamage(
                position,
                result.Damage,
                result.PartLost,
                result.IsKnockout);
        }

        private Vector3 ResolveHitPosition(Transform targetRoot)
        {
            if (hitEffectView != null)
            {
                return hitEffectView.ResolveWorldHitPoint(targetRoot);
            }

            return BattleHitEffectView.ResolveWorldHitPoint(targetRoot, 1.2f);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            disposables.Dispose();
        }
    }
}
