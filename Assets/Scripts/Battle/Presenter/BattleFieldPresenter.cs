using Extensions;
using R3;
using System;
using UnityEngine;

namespace Battle.Presenter
{
    /// <summary>
    /// 戦闘エンジンの間合い変化をフィールド上のモデル配置へ反映する
    /// </summary>
    public sealed class BattleFieldPresenter : IDisposable
    {
        private readonly BattleFieldLayout layout;
        private readonly BattleSystem system;
        private readonly BattleFieldPositionEnforcer positionEnforcer;
        private readonly CompositeDisposable disposables = new CompositeDisposable();

        /// <summary>
        /// 戦闘更新に追従してモデル配置を同期する
        /// </summary>
        public BattleFieldPresenter(
            BattleFieldLayout layout,
            BattleSystem system,
            GameObject playerModelRoot = null)
        {
            this.layout = layout;
            this.system = system;
            system.SetFieldBoundary(layout);
            layout.ApplyDistance(
                system.Distance,
                system.MaxDistance,
                0f,
                ResolvePlayerMovementIntent(system),
                ResolveEnemyMovementIntent(system),
                system.Player.MoveSpeed,
                system.Enemy.MoveSpeed,
                system.IsPlayerStepping,
                system.PlayerStepMovementIntent,
                system.IsEnemyStepping,
                system.EnemyStepMovementIntent);
            system.Player.SyncMotionLayoutPosition();
            system.Enemy.SyncMotionLayoutPosition();

            if (playerModelRoot != null)
            {
                positionEnforcer = playerModelRoot.GetComponent<BattleFieldPositionEnforcer>();
                if (positionEnforcer == null)
                {
                    positionEnforcer = playerModelRoot.AddComponent<BattleFieldPositionEnforcer>();
                }

                positionEnforcer.Bind(layout);
            }

            system.OnDistanceResyncRequested
                .Subscribe(_ => ResyncLayoutToBattleDistance())
                .AddTo(disposables);

            system.OnUpdated
                .Subscribe(_ =>
                {
                    layout.ApplyDistance(
                        system.Distance,
                        system.MaxDistance,
                        GameplayTime.DeltaTime,
                        ResolvePlayerMovementIntent(system),
                        ResolveEnemyMovementIntent(system),
                        system.Player.MoveSpeed,
                        system.Enemy.MoveSpeed,
                        system.IsPlayerStepping,
                        system.PlayerStepMovementIntent,
                        system.IsEnemyStepping,
                        system.EnemyStepMovementIntent);
                    system.Player.SyncMotionLayoutPosition();
                    system.Enemy.SyncMotionLayoutPosition();
                })
                .AddTo(disposables);
        }

        private void ResyncLayoutToBattleDistance()
        {
            layout.ApplyInitialBattlePositions(system.Distance, system.MaxDistance);
            system.Player.SyncMotionLayoutPosition();
            system.Enemy.SyncMotionLayoutPosition();
        }

        private static int ResolvePlayerMovementIntent(BattleSystem system)
        {
            if (system.IsPlayerStepping)
            {
                return system.PlayerStepMovementIntent;
            }

            return system.Player.MovementIntent;
        }

        private static int ResolveEnemyMovementIntent(BattleSystem system)
        {
            if (system.IsEnemyStepping)
            {
                return system.EnemyStepMovementIntent;
            }

            return system.Enemy.MovementIntent;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            positionEnforcer?.Unbind();
            disposables.Dispose();
        }
    }
}
