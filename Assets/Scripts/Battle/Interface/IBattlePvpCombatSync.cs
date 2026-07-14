namespace Battle.Interface
{
    /// <summary>
    /// PvP戦闘の攻撃結果とステップ移動をネットワーク同期する
    /// </summary>
    public interface IBattlePvpCombatSync
    {
        /// <summary>
        /// 相手プレイヤー由来の敵攻撃をローカル乱数で解決しないか
        /// </summary>
        bool ShouldDeferRemoteEnemyStrike { get; }

        /// <summary>
        /// ローカルプレイヤーの攻撃結果を相手へ送信する
        /// </summary>
        void ReportLocalPlayerStrike(global::Battle.MoveUsedResult result, int moveIndex);

        /// <summary>
        /// ローカルプレイヤーのステップ移動を相手へ送信する
        /// </summary>
        void ReportLocalPlayerStep(int stepIntent, float targetDistance);

        /// <summary>
        /// 相手プレイヤーからの同期イベントを監視開始する
        /// </summary>
        void BeginListening();

        /// <summary>
        /// 相手プレイヤーからの同期イベント監視を停止する
        /// </summary>
        void EndListening();

        /// <summary>
        /// 監視未開始なら相手イベント購読を試みる
        /// </summary>
        void EnsureListening();

        /// <summary>
        /// 相手側NetworkVariableをポーリングしてイベント取りこぼしを補う
        /// </summary>
        void PollRemoteSync();

        /// <summary>
        /// 相手プレイヤーの攻撃結果を1回分消費する
        /// </summary>
        bool TryConsumeRemoteStrike(out global::Battle.BattleRemoteStrikePayload payload);

        /// <summary>
        /// 相手プレイヤーのステップ移動を1回分消費する
        /// </summary>
        bool TryConsumeRemoteStep(out global::Battle.BattleRemoteStepPayload payload);

        /// <summary>
        /// 相手ステップ同期を消費済みにする
        /// </summary>
        void ConfirmRemoteStepConsumed();

        /// <summary>
        /// ローカルプレイヤーの部位修復完了を相手へ送信する
        /// </summary>
        void ReportLocalPlayerPartRestored(int limbIndex);

        /// <summary>
        /// 相手プレイヤーの部位修復完了を1回分消費する
        /// </summary>
        bool TryConsumeRemotePartRestore(out global::Battle.BattleRemotePartRestorePayload payload);
    }
}
