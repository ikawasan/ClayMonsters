namespace Battle
{
    /// <summary>
    /// ネットワーク同期されたステップ移動
    /// </summary>
    public readonly struct BattleRemoteStepPayload
    {
        /// <summary>
        /// 同期ステップ移動を生成する
        /// </summary>
        public BattleRemoteStepPayload(int stepIntent, float targetDistance, int sequence, int matchGeneration = 0)
        {
            StepIntent = stepIntent;
            TargetDistance = targetDistance;
            Sequence = sequence;
            MatchGeneration = matchGeneration;
        }

        /// <summary>
        /// ステップ移動意図
        /// </summary>
        public int StepIntent { get; }

        /// <summary>
        /// 間合いの目標値
        /// </summary>
        public float TargetDistance { get; }

        /// <summary>
        /// 同期番号
        /// </summary>
        public int Sequence { get; }

        /// <summary>
        /// 対戦世代番号・再戦後の遅延パケット破棄用
        /// </summary>
        public int MatchGeneration { get; }
    }
}
