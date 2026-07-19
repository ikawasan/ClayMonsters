using Battle;

namespace Battle.Interface
{
    /// <summary>
    /// 敵ユニットの行動を毎フレーム決定する
    /// </summary>
    public interface IBattleEnemyAi
    {
        /// <summary>
        /// 現在の戦況から移動と攻撃を決める
        /// </summary>
        /// <param name="context">戦闘状況のスナップショット</param>
        /// <returns>移動意図と攻撃技番号</returns>
        BattleEnemyAiDecision Decide(BattleEnemyAiContext context);

        /// <summary>
        /// 相手プレイヤー由来のステップ入力を1回分消費する
        /// </summary>
        /// <returns>ステップ移動意図・未同期時0</returns>
        int ConsumeRemoteStepIntent();

        /// <summary>
        /// ネットワーク同期の攻撃開始を1回分消費する
        /// </summary>
        /// <param name="moveIndex">攻撃技番号</param>
        /// <param name="attackSequence">攻撃開始同期番号</param>
        /// <param name="isCounter">カウンター攻撃か</param>
        /// <returns>同期攻撃があればtrue</returns>
        bool TryConsumeNetworkAttackStart(out int moveIndex, out int attackSequence, out bool isCounter);
    }
}
