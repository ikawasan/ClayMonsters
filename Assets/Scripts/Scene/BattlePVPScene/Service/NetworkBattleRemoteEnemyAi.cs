using Battle;
using Battle.Interface;
using Scene.BattlePVPScene.Network;

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

            BattlePvpInputSnapshot snapshot = inputRelay.RemoteInput;
            if (snapshot.IsHoldingPartRepair && context.Self.LostPartCount > 0)
            {
                return BattleEnemyAiDecision.Repair;
            }

            return new BattleEnemyAiDecision(snapshot.MovementIntent, -1);
        }

        /// <inheritdoc/>
        public int ConsumeRemoteStepIntent() => 0;

        /// <inheritdoc/>
        public bool TryConsumeNetworkAttackStart(out int moveIndex, out int attackSequence, out bool isCounter)
        {
            moveIndex = -1;
            attackSequence = 0;
            isCounter = false;
            if (inputRelay == null)
            {
                return false;
            }

            BattlePvpInputRelay opponentRelay = inputRelay.GetOpponentRelay();
            if (opponentRelay != null
                && opponentRelay.TryConsumeRemoteAttackStart(out int rpcMoveIndex, out int rpcSequence, out bool rpcIsCounter))
            {
                if (rpcSequence > lastRemoteAttackSequence && rpcMoveIndex >= 0)
                {
                    lastRemoteAttackSequence = rpcSequence;
                    moveIndex = rpcMoveIndex;
                    attackSequence = rpcSequence;
                    isCounter = rpcIsCounter;
                    return true;
                }
            }

            int remoteAttackSequence = inputRelay.OpponentAttackSequence;
            if (remoteAttackSequence > lastRemoteAttackSequence)
            {
                int remoteMoveIndex = inputRelay.OpponentAttackMoveIndex;
                if (remoteMoveIndex >= 0)
                {
                    lastRemoteAttackSequence = remoteAttackSequence;
                    moveIndex = remoteMoveIndex;
                    attackSequence = remoteAttackSequence;
                    isCounter = inputRelay.OpponentAttackIsCounter;
                    return true;
                }
            }

            return false;
        }
    }
}
