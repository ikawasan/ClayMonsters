using Battle;
using Battle.Interface;
using Scene.BattlePVPScene.Network;
using UnityEngine;

namespace Scene.BattlePVPScene.Service
{
    /// <summary>
    /// 相手プレイヤーの入力を敵AI決定へ変換する
    /// </summary>
    public sealed class NetworkBattleRemoteEnemyAi : IBattleEnemyAi
    {
        private readonly BattlePvpInputRelay inputRelay;
        private int lastRemoteAttackSequence;

        /// <summary>
        /// 入力リレーを参照してリモート敵AIを生成する
        /// </summary>
        public NetworkBattleRemoteEnemyAi(BattlePvpInputRelay inputRelay)
        {
            this.inputRelay = inputRelay;
        }

        /// <inheritdoc/>
        public BattleEnemyAiDecision Decide(BattleEnemyAiContext context)
        {
            if (inputRelay == null)
            {
                return BattleEnemyAiDecision.Hold;
            }

            BattlePvpInputRelay opponentRelay = inputRelay.GetOpponentRelay();
            if (opponentRelay != null
                && opponentRelay.TryConsumeRemoteAttackStart(out int rpcMoveIndex, out int rpcSequence))
            {
                if (rpcSequence > lastRemoteAttackSequence && rpcMoveIndex >= 0)
                {
                    lastRemoteAttackSequence = rpcSequence;
                    return new BattleEnemyAiDecision(0, rpcMoveIndex, attackSequence: rpcSequence);
                }
            }

            int remoteAttackSequence = inputRelay.OpponentAttackSequence;
            if (remoteAttackSequence > lastRemoteAttackSequence)
            {
                lastRemoteAttackSequence = remoteAttackSequence;
                int moveIndex = inputRelay.OpponentAttackMoveIndex;
                if (moveIndex >= 0)
                {
                    return new BattleEnemyAiDecision(0, moveIndex, attackSequence: remoteAttackSequence);
                }
            }

            BattlePvpInputSnapshot snapshot = inputRelay.RemoteInput;
            if (snapshot.IsHoldingPartRepair && context.Self.LostPartCount > 0)
            {
                return BattleEnemyAiDecision.Repair;
            }

            return new BattleEnemyAiDecision(snapshot.MovementIntent, -1);
        }

        /// <inheritdoc/>
        public int ConsumeRemoteStepIntent() => 0;
    }
}
