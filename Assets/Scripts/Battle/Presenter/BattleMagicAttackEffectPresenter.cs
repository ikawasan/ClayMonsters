using Audio;
using Audio.Interface;
using Battle.View;
using ClayEditor.Rigging;
using R3;
using System;
using UnityEngine;

namespace Battle.Presenter
{
    /// <summary>
    /// 魔法攻撃の詠唱円と技別エフェクトを戦闘イベントへ同期する
    /// </summary>
    public sealed class BattleMagicAttackEffectPresenter : IDisposable
    {
        private readonly BattleMagicAttackEffectView effectView;
        private readonly BattleSystem system;
        private readonly Transform playerRoot;
        private readonly Transform enemyRoot;
        private readonly ISeService seService;
        private readonly CompositeDisposable disposables = new CompositeDisposable();

        private BattleUnit activeAttacker;
        private bool isMagicCircleSePlaying;

        /// <summary>
        /// 魔法演出を購読して再生する
        /// </summary>
        public BattleMagicAttackEffectPresenter(
            BattleMagicAttackEffectView effectView,
            BattleSystem system,
            Transform playerRoot,
            Transform enemyRoot,
            float battleGroundY,
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

            effectView.ConfigureGroundY(battleGroundY);

            system.OnAttackWindUpStarted
                .Subscribe(OnAttackWindUpStarted)
                .AddTo(disposables);

            system.OnAttackProjectileStarted
                .Subscribe(OnAttackProjectileStarted)
                .AddTo(disposables);

            system.OnMoveUsed
                .Subscribe(OnMoveUsed)
                .AddTo(disposables);

            system.OnBattleEnd
                .Subscribe(_ => StopActive())
                .AddTo(disposables);
        }

        private void OnAttackWindUpStarted(AttackWindUpStarted started)
        {
            if (started.Move == null || !ProceduralMotionCharacter.IsMagicAttack(started.Move.Motion))
            {
                return;
            }

            Transform attackerRoot = ResolveRoot(started.Attacker);
            if (attackerRoot == null)
            {
                return;
            }

            activeAttacker = started.Attacker;
            effectView.PlayCastCircle(attackerRoot, started.WindUpDuration);
            PlayMagicCircleSe(started.WindUpDuration);
        }

        private void OnAttackProjectileStarted(AttackProjectileStarted started)
        {
            if (started.Move == null || started.Move.Motion != MotionType.Fireball)
            {
                return;
            }

            Transform attackerRoot = ResolveRoot(started.Attacker);
            Transform targetRoot = ResolveRoot(started.Target);
            StopMagicCircleSe();
            effectView.StopCastCircle();
            effectView.PlayFireballTravel(attackerRoot, targetRoot, started.TravelDuration);
            seService?.Play(SeTrackId.MagicFireball);
            activeAttacker = null;
        }

        private void OnMoveUsed(MoveUsedResult result)
        {
            if (result.Move == null || !ProceduralMotionCharacter.IsMagicAttack(result.Move.Motion))
            {
                if (activeAttacker != null && ReferenceEquals(result.Attacker, activeAttacker))
                {
                    StopActive();
                }

                return;
            }

            Transform attackerRoot = ResolveRoot(result.Attacker);
            Transform targetRoot = ResolveRoot(result.Target);
            StopMagicCircleSe();
            effectView.StopCastCircle();
            effectView.PlayAttackEffect(result.Move.Motion, attackerRoot, targetRoot);
            PlayMagicAttackSe(result.Move.Motion);
            activeAttacker = null;
        }

        private void PlayMagicCircleSe(float windUpDuration)
        {
            if (seService == null)
            {
                return;
            }

            float duration = Mathf.Max(0.05f, windUpDuration);
            seService.PlayTimed(SeTrackId.MagicCircle, duration);
            isMagicCircleSePlaying = true;
        }

        private void StopMagicCircleSe()
        {
            if (!isMagicCircleSePlaying || seService == null)
            {
                return;
            }

            seService.Stop(SeTrackId.MagicCircle);
            isMagicCircleSePlaying = false;
        }

        private void PlayMagicAttackSe(MotionType motion)
        {
            if (seService == null)
            {
                return;
            }

            // ファイアーボールは飛翔開始で再生済み着弾はCombatFeedback側
            switch (motion)
            {
                case MotionType.WindSlasher:
                    seService.Play(SeTrackId.MagicWindSlasher);
                    break;
                case MotionType.DiamondDust:
                    seService.Play(SeTrackId.MagicDiamondDust);
                    break;
                case MotionType.ThunderShock:
                    seService.Play(SeTrackId.MagicThunderShock);
                    break;
            }
        }

        private void StopActive()
        {
            activeAttacker = null;
            StopMagicCircleSe();
            effectView?.StopCastCircle();
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
            StopActive();
            disposables.Dispose();
        }
    }
}
