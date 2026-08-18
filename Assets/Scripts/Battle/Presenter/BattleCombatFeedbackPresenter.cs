using Audio;
using Audio.Interface;
using Battle.Interface;
using Battle.View;
using ClayEditor.Rigging;
using R3;
using System;
using UnityEngine;

namespace Battle.Presenter
{
    /// <summary>
    /// 命中イベントをヒット演出とダメージ表示とSEへまとめて渡す
    /// </summary>
    public sealed class BattleCombatFeedbackPresenter : IDisposable
    {
        private readonly IBattleDamagePopup damagePopup;
        private readonly IBattleHitEffect hitEffect;
        private readonly BattleHitEffectView hitEffectView;
        private readonly ISeService seService;
        private readonly BattleSystem system;
        private readonly Transform playerRoot;
        private readonly Transform enemyRoot;
        private readonly CompositeDisposable disposables = new CompositeDisposable();

        /// <summary>
        /// 命中イベントを購読して戦闘演出を再生する
        /// </summary>
        public BattleCombatFeedbackPresenter(
            IBattleHitEffect hitEffect,
            IBattleDamagePopup damagePopup,
            BattleSystem system,
            Transform playerRoot,
            Transform enemyRoot,
            UnityEngine.Camera worldCamera = null,
            ISeService seService = null)
        {
            this.hitEffect = hitEffect;
            this.damagePopup = damagePopup;
            hitEffectView = hitEffect as BattleHitEffectView;
            this.seService = seService;
            this.system = system;
            this.playerRoot = playerRoot;
            this.enemyRoot = enemyRoot;

            if (damagePopup is BattleDamagePopupView popupView)
            {
                popupView.SetWorldCamera(worldCamera);
            }

            system.OnMoveUsed
                .Subscribe(OnMoveUsed)
                .AddTo(disposables);
        }

        private void OnMoveUsed(MoveUsedResult result)
        {
            PlayHitFeedbackSe(result);

            Transform targetRoot = ResolveTargetRoot(result);
            if (targetRoot == null)
            {
                return;
            }

            Vector3 position = ResolveHitPosition(targetRoot);

            if (damagePopup != null && result.ShowDamagePopup)
            {
                if (!result.Hit)
                {
                    damagePopup.PlayMiss(position);
                }
                else if (result.Damage > 0)
                {
                    damagePopup.PlayDamage(
                        position,
                        result.Damage,
                        result.PartLost,
                        result.IsKnockout);
                }
            }

            if (result.Hit && hitEffect != null)
            {
                hitEffect.PlayHit(position, result.PartLost || result.IsKnockout);
            }
        }

        private void PlayHitFeedbackSe(MoveUsedResult result)
        {
            if (seService == null)
            {
                return;
            }

            if (!result.Hit)
            {
                if (result.ShowDamagePopup)
                {
                    seService.PlayAttackMiss();
                }

                return;
            }

            if (result.PartLost || result.IsKnockout)
            {
                seService.PlayPartsBreak();
                return;
            }

            // ファイアーボール着弾は専用SE
            if (result.Move != null && result.Move.Motion == MotionType.Fireball)
            {
                seService.Play(SeTrackId.MagicFireballHit);
                return;
            }

            // その他魔法は技SE側で鳴らすので通常ヒットSEは重ねない
            if (result.Move != null && ProceduralMotionCharacter.IsMagicAttack(result.Move.Motion))
            {
                return;
            }

            seService.PlayAttackHit();
        }

        private Transform ResolveTargetRoot(MoveUsedResult result)
        {
            if (ReferenceEquals(result.Target, system.Player))
            {
                return playerRoot;
            }

            if (ReferenceEquals(result.Target, system.Enemy))
            {
                return enemyRoot;
            }

            return null;
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
