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
        /// ローカル側が間合い同期の権威を持つか
        /// </summary>
        bool IsDistanceAuthority { get; }

        /// <summary>
        /// ローカルプレイヤーの最新攻撃開始同期番号
        /// </summary>
        int LocalAttackSequence { get; }

        /// <summary>
        /// ローカル攻撃開始を相手へ送信し同期番号を返す
        /// </summary>
        /// <param name="moveIndex">攻撃技番号</param>
        /// <returns>攻撃開始同期番号・未送信時0</returns>
        int ReportLocalAttackStart(int moveIndex);

        /// <summary>
        /// ローカルプレイヤーの攻撃結果を相手へ送信する
        /// </summary>
        void ReportLocalPlayerStrike(global::Battle.MoveUsedResult result, int moveIndex, int attackSequence);

        /// <summary>
        /// ローカルプレイヤーのステップ移動を相手へ送信する
        /// </summary>
        void ReportLocalPlayerStep(int stepIntent, float targetDistance);

        /// <summary>
        /// 権威側の現在間合いを相手へ送信する
        /// </summary>
        /// <param name="distance">現在間合い</param>
        void ReportAuthoritativeDistance(float distance);

        /// <summary>
        /// 相手攻撃をカウンターしたことを通知する
        /// </summary>
        /// <param name="counteredAttackSequence">無効化した相手攻撃開始同期番号</param>
        void ReportLocalCounter(int counteredAttackSequence);

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
        /// 権威側から届いた最新間合いを消費する
        /// </summary>
        /// <param name="distance">権威側の現在間合い</param>
        bool TryConsumeAuthoritativeDistance(out float distance);

        /// <summary>
        /// 相手ステップ同期を消費済みにする
        /// </summary>
        void ConfirmRemoteStepConsumed();

        /// <summary>
        /// 相手からのカウンター通知を1回分消費する
        /// </summary>
        /// <param name="counteredAttackSequence">無効化された自分の攻撃開始同期番号</param>
        bool TryConsumeRemoteCounter(out int counteredAttackSequence);

        /// <summary>
        /// ローカルプレイヤーの部位修復完了を相手へ送信する
        /// </summary>
        void ReportLocalPlayerPartRestored(int limbIndex);

        /// <summary>
        /// ローカルふっとばしを相手へ送信する
        /// </summary>
        /// <param name="resultingDistance">適用後の間合い</param>
        void ReportLocalKnockback(float resultingDistance);

        /// <summary>
        /// 相手のふっとばしを1回分消費する
        /// </summary>
        /// <param name="resultingDistance">同期後の間合い</param>
        bool TryConsumeRemoteKnockback(out float resultingDistance);

        /// <summary>
        /// 相手プレイヤーの部位修復完了を1回分消費する
        /// </summary>
        bool TryConsumeRemotePartRestore(out global::Battle.BattleRemotePartRestorePayload payload);
    }
}
