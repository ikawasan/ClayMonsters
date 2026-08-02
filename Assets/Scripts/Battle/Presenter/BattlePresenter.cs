using System;
using System.Collections.Generic;
using Battle;
using ClayEditor.Rigging;
using Extensions;
using R3;
using UI.Battle.Interface;
using UnityEngine;

namespace Battle.Presenter
{
    /// <summary>
    /// リアルタイム戦闘エンジンとUIを繋ぐ状態をUIへ反映し・UI操作をエンジンへ渡す
    /// </summary>
    public sealed class BattlePresenter : IDisposable
    {
        private readonly IBattleView view;
        private readonly CompositeDisposable disposables = new CompositeDisposable();
        private readonly List<MoveDisplay> playerMoveCache = new List<MoveDisplay>();
        private readonly List<MoveDisplay> enemyMoveCache = new List<MoveDisplay>();

        private BattleSystem system;
        private float lastMoveRefreshDistance = float.NaN;
        private float lastMoveRefreshPlayerGuts = float.NaN;
        private float lastMoveRefreshEnemyGuts = float.NaN;
        private int lastMoveRefreshPlayerHp = int.MinValue;
        private int lastMoveRefreshEnemyHp = int.MinValue;
        private int lastMoveRefreshPlayerLostParts = int.MinValue;
        private int lastMoveRefreshEnemyLostParts = int.MinValue;
        private int lastMoveRefreshPlayerChain = int.MinValue;
        private bool lastMoveRefreshPlayerCanAct;
        private bool lastMoveRefreshEnemyCanAct;
        private bool lastMoveRefreshKnockbackAvailable;
        private bool lastMoveRefreshCounterWindow;
        private int lastMoveRefreshPendingEnemyMove = int.MinValue;
        private bool lastMoveRefreshPlayerPerformingAttack;
        private bool lastMoveRefreshEnemyPerformingAttack;
        private bool lastMoveRefreshAttackLockout;
        private bool isBattleEnded;

        public BattlePresenter(IBattleView view)
        {
            this.view = view;
        }

        /// <summary>
        /// 戦闘システムに接続して購読を開始するRunAsyncを呼ぶ前に行う
        /// </summary>
        public void Bind(BattleSystem battleSystem)
        {
            system = battleSystem;

            view.InitializeUnits(system.Player.Name, system.Enemy.Name);
            view.PrepareForBattleInput();
            RefreshContinuous();

            // 毎フレームの連続値(HP/ガッツ/間合い/時間/技の使用可否)を更新
            system.OnUpdated
                .Subscribe(_ => RefreshContinuous())
                .AddTo(disposables);

            system.OnMoveUsed
                .Subscribe(_ => RefreshContinuous())
                .AddTo(disposables);

            system.OnAttackWindUpStarted
                .Subscribe(OnAttackWindUpStarted)
                .AddTo(disposables);

            system.OnBattleEnd
                .Subscribe(_ => isBattleEnded = true)
                .AddTo(disposables);

            // UIの技選択→エンジンへ
            view.OnMoveSelected
                .Subscribe(index =>
                {
                    if (index < 0 || index >= system.Player.Moves.Count)
                    {
                        return;
                    }

                    system.TryUsePlayerMove(index);
                })
                .AddTo(disposables);

            system.OnPartBreakMoveRejected
                .Subscribe(index => view.ShowPartBreakLockMessage(index))
                .AddTo(disposables);
        }

        private void OnAttackWindUpStarted(AttackWindUpStarted started)
        {
            if (isBattleEnded || started.Move == null || system == null)
            {
                return;
            }

            bool isPlayer = ReferenceEquals(started.Attacker, system.Player);
            view.SetAttackName(isPlayer, started.Move.DisplayName);
        }

        // 連続値をまとめてUIへ反映する
        private void RefreshContinuous()
        {
            if (isBattleEnded)
            {
                return;
            }

            BattleUnit p = system.Player;
            BattleUnit e = system.Enemy;

            view.SetHp(true, p.CurrentHp, p.MaxHp);
            view.SetHp(false, e.CurrentHp, e.MaxHp);
            view.SetGuts(true, p.Guts, p.MaxGuts);
            view.SetGuts(false, e.Guts, e.MaxGuts);
            view.SetDistance(
                system.Distance,
                system.MaxDistance,
                BattleDistanceBandResolver.ToDisplayName(system.CurrentDistanceBand));
            view.SetCombatHint(BuildCombatHint());
            view.SetTimeRemaining(system.TimeRemaining);

            if (ShouldRefreshMoves(p, e))
            {
                lastMoveRefreshDistance = system.Distance;
                lastMoveRefreshPlayerGuts = p.Guts;
                lastMoveRefreshEnemyGuts = e.Guts;
                lastMoveRefreshPlayerHp = p.CurrentHp;
                lastMoveRefreshEnemyHp = e.CurrentHp;
                lastMoveRefreshPlayerLostParts = p.LostPartCount;
                lastMoveRefreshEnemyLostParts = e.LostPartCount;
                lastMoveRefreshPlayerCanAct = p.CanAct;
                lastMoveRefreshEnemyCanAct = e.CanAct;
                lastMoveRefreshPlayerChain = system.PlayerChainCount;
                lastMoveRefreshKnockbackAvailable = system.IsKnockbackAvailable;
                lastMoveRefreshCounterWindow = system.IsCounterWindowOpen;
                lastMoveRefreshPendingEnemyMove = system.PendingEnemyMoveIndex;
                lastMoveRefreshPlayerPerformingAttack = system.IsPlayerPerformingAttack;
                lastMoveRefreshEnemyPerformingAttack = system.IsEnemyPerformingAttack;
                lastMoveRefreshAttackLockout = system.IsAttackLockoutActive;

                BuildMoveDisplays(p, playerMoveCache, true);
                BuildMoveDisplays(e, enemyMoveCache, false);
                view.SetPlayerMoves(playerMoveCache);
                view.SetEnemyMoves(enemyMoveCache);
            }
        }

        // 技ボタンの使用可否表示は間合い・ガッツ・HP・部位欠損が変わったときだけ更新する
        private bool ShouldRefreshMoves(BattleUnit player, BattleUnit enemy)
        {
            return !Mathf.Approximately(lastMoveRefreshDistance, system.Distance)
                || Mathf.FloorToInt(lastMoveRefreshPlayerGuts) != Mathf.FloorToInt(player.Guts)
                || Mathf.FloorToInt(lastMoveRefreshEnemyGuts) != Mathf.FloorToInt(enemy.Guts)
                || lastMoveRefreshPlayerHp != player.CurrentHp
                || lastMoveRefreshEnemyHp != enemy.CurrentHp
                || lastMoveRefreshPlayerLostParts != player.LostPartCount
                || lastMoveRefreshEnemyLostParts != enemy.LostPartCount
                || lastMoveRefreshPlayerCanAct != player.CanAct
                || lastMoveRefreshEnemyCanAct != enemy.CanAct
                || lastMoveRefreshPlayerChain != system.PlayerChainCount
                || lastMoveRefreshKnockbackAvailable != system.IsKnockbackAvailable
                || lastMoveRefreshCounterWindow != system.IsCounterWindowOpen
                || lastMoveRefreshPendingEnemyMove != system.PendingEnemyMoveIndex
                || lastMoveRefreshPlayerPerformingAttack != system.IsPlayerPerformingAttack
                || lastMoveRefreshEnemyPerformingAttack != system.IsEnemyPerformingAttack
                || lastMoveRefreshAttackLockout != system.IsAttackLockoutActive;
        }

        // 現在の間合いでの各技の表示情報(名前・使用可否・間合い・ガッツ)を作る
        private void BuildMoveDisplays(BattleUnit unit, List<MoveDisplay> destination, bool isPlayer)
        {
            destination.Clear();
            for (int i = 0; i < unit.Moves.Count; i++)
            {
                AttackMove m = unit.Moves[i];
                bool usable = system.IsMoveUsableForUnit(isPlayer, i);
                bool lockedByMissingPart = !unit.IsMoveUsableByPart(i);
                destination.Add(new MoveDisplay(
                    m.DisplayName,
                    usable,
                    lockedByMissingPart,
                    m.RangeMin,
                    m.RangeMax,
                    m.GutsCost,
                    m.Power,
                    ToTargetPartId(m.TargetDestroyPart),
                    ToRequiredPartId(m.RequiredPart)));
            }
        }

        private string BuildCombatHint()
        {
            if (system.IsCounterWindowOpen)
            {
                BonePart destroyPart = system.PendingEnemyTargetDestroyPart;
                string partLabel = MotionPartRequirement.FormatTargetDestroyPartLabel(destroyPart);
                return Localization.LocalizedText.GetOrFallback(
                    Localization.GameTextKeys.BattleHintCounter,
                    $"カウンター！{partLabel}技で迎撃",
                    new System.Collections.Generic.Dictionary<string, object>
                    {
                        { "part", partLabel },
                    });
            }

            if (system.IsAttackLockoutActive)
            {
                return Localization.LocalizedText.GetOrFallback(
                    Localization.GameTextKeys.BattleHintAttackLockout,
                    "攻撃クールダウン中");
            }

            if (system.Player.LostPartCount > 0)
            {
                return Extensions.InputGuideTexts.BattleHintPartRepair;
            }

            if (system.IsKnockbackAvailable)
            {
                return Extensions.InputGuideTexts.BattleHintKnockbackReady;
            }

            if (system.PlayerChainCount > 1)
            {
                return Localization.LocalizedText.GetOrFallback(
                    Localization.GameTextKeys.BattleHintChain,
                    $"チェーン x{system.PlayerChainCount}",
                    new System.Collections.Generic.Dictionary<string, object>
                    {
                        { "count", system.PlayerChainCount },
                    });
            }

            return string.Empty;
        }

        // ドメインの破壊対象部位をUIの識別子へ変換する
        private static MoveTargetPartId ToTargetPartId(BonePart part)
        {
            switch (part)
            {
                case BonePart.Arm: return MoveTargetPartId.Arm;
                case BonePart.Leg: return MoveTargetPartId.Leg;
                case BonePart.Front: return MoveTargetPartId.Front;
                case BonePart.Back: return MoveTargetPartId.Back;
                case BonePart.Body: return MoveTargetPartId.None;
                default: return MoveTargetPartId.None;
            }
        }

        // ドメインの必要部位をUIの識別子へ変換する
        private static MoveTargetPartId ToRequiredPartId(BonePart part)
        {
            switch (part)
            {
                case BonePart.Arm: return MoveTargetPartId.Arm;
                case BonePart.Leg: return MoveTargetPartId.Leg;
                case BonePart.Front: return MoveTargetPartId.Front;
                case BonePart.Back: return MoveTargetPartId.Back;
                case BonePart.Body: return MoveTargetPartId.None;
                default: return MoveTargetPartId.None;
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            disposables.Dispose();
        }
    }
}