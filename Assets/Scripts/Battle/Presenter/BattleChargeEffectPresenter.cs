using Battle.Interface;
using Battle.View;
using R3;
using System;
using UnityEngine;

namespace Battle.Presenter
{
    /// <summary>
    /// 攻撃溜め開始に合わせて力溜め演出を同期する
    /// </summary>
    public sealed class BattleChargeEffectPresenter : IDisposable
    {
        private readonly IBattleChargeEffect chargeEffect;
        private readonly BattleSystem system;
        private readonly Transform playerRoot;
        private readonly Transform enemyRoot;
        private readonly CompositeDisposable disposables = new CompositeDisposable();

        private BattleUnit activeAttacker;

        /// <summary>
        /// 溜めイベントを購読して演出を再生する
        /// </summary>
        public BattleChargeEffectPresenter(
            IBattleChargeEffect chargeEffect,
            BattleSystem system,
            Transform playerRoot,
            Transform enemyRoot)
        {
            this.chargeEffect = chargeEffect;
            this.system = system;
            this.playerRoot = playerRoot;
            this.enemyRoot = enemyRoot;

            if (chargeEffect == null || system == null)
            {
                return;
            }

            system.OnAttackWindUpStarted
                .Subscribe(OnAttackWindUpStarted)
                .AddTo(disposables);

            system.OnMoveUsed
                .Subscribe(OnMoveUsed)
                .AddTo(disposables);

            system.OnUpdated
                .Subscribe(_ => OnUpdated())
                .AddTo(disposables);

            system.OnBattleEnd
                .Subscribe(_ => StopActive())
                .AddTo(disposables);
        }

        private void OnAttackWindUpStarted(AttackWindUpStarted started)
        {
            Transform modelRoot = ResolveAttackerRoot(started.Attacker);
            if (modelRoot == null)
            {
                return;
            }

            activeAttacker = started.Attacker;
            chargeEffect.Play(modelRoot, started.WindUpDuration);
            chargeEffect.SetIntensity(started.Attacker != null ? started.Attacker.ChargeIntensity : 0f);
        }

        private void OnMoveUsed(MoveUsedResult result)
        {
            if (activeAttacker == null)
            {
                return;
            }

            if (ReferenceEquals(result.Attacker, activeAttacker))
            {
                StopActive();
            }
        }

        private void OnUpdated()
        {
            if (activeAttacker == null || chargeEffect == null)
            {
                return;
            }

            if (!activeAttacker.IsCharging)
            {
                StopActive();
                return;
            }

            chargeEffect.SetIntensity(activeAttacker.ChargeIntensity);
        }

        private void StopActive()
        {
            activeAttacker = null;
            chargeEffect?.Stop();
        }

        private Transform ResolveAttackerRoot(BattleUnit attacker)
        {
            if (attacker == null)
            {
                return null;
            }

            if (ReferenceEquals(attacker, system.Player))
            {
                return playerRoot;
            }

            if (ReferenceEquals(attacker, system.Enemy))
            {
                return enemyRoot;
            }

            return null;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            StopActive();
            disposables.Dispose();
        }
    }
}
