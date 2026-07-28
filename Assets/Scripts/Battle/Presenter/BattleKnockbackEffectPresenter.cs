using Audio;
using Audio.Interface;
using Battle.View;
using R3;
using System;
using UnityEngine;

namespace Battle.Presenter
{
    /// <summary>
    /// ふきとばし成立を衝撃波エフェクトへ同期する
    /// </summary>
    public sealed class BattleKnockbackEffectPresenter : IDisposable
    {
        private readonly BattleKnockbackEffectView effectView;
        private readonly BattleSystem system;
        private readonly Transform playerRoot;
        private readonly Transform enemyRoot;
        private readonly ISeService seService;
        private readonly CompositeDisposable disposables = new CompositeDisposable();

        /// <summary>
        /// ふきとばし演出を購読して再生する
        /// </summary>
        public BattleKnockbackEffectPresenter(
            BattleKnockbackEffectView effectView,
            BattleSystem system,
            Transform playerRoot,
            Transform enemyRoot,
            ISeService seService = null)
        {
            this.effectView = effectView;
            this.system = system;
            this.playerRoot = playerRoot;
            this.enemyRoot = enemyRoot;
            this.seService = seService;

            if (effectView == null || system == null)
            {
                return;
            }

            system.OnKnockbackPerformed
                .Subscribe(OnKnockbackPerformed)
                .AddTo(disposables);
        }

        private void OnKnockbackPerformed(KnockbackPerformed performed)
        {
            Transform sourceRoot = ResolveRoot(performed.Source);
            if (sourceRoot == null)
            {
                return;
            }

            seService?.Play(SeTrackId.PressAction);
            effectView.Play(sourceRoot, ResolveRoot(performed.Target));
        }

        private Transform ResolveRoot(BattleUnit unit)
        {
            if (unit == null || system == null)
            {
                return null;
            }

            if (ReferenceEquals(unit, system.Player))
            {
                return playerRoot;
            }

            if (ReferenceEquals(unit, system.Enemy))
            {
                return enemyRoot;
            }

            return null;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            disposables.Dispose();
        }
    }
}
