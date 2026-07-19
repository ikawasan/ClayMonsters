using Extensions;
using R3;
using System;
using UnityEngine;

namespace Battle.Presenter
{
    /// <summary>
    /// 戦闘中のオフセット結果をフィールド上のモデル配置へ反映する
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
            layout.ApplyModelTransforms();
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

            system.OnUpdated
                .Subscribe(_ =>
                {
                    layout.ApplyModelTransforms();
                    system.Player.SyncMotionLayoutPosition();
                    system.Enemy.SyncMotionLayoutPosition();
                })
                .AddTo(disposables);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            positionEnforcer?.Unbind();
            disposables.Dispose();
        }
    }
}
