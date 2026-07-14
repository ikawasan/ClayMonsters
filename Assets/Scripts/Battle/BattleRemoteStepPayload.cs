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

        public BattleRemoteStepPayload(int stepIntent, float targetDistance, int sequence)

        {

            StepIntent = stepIntent;

            TargetDistance = targetDistance;

            Sequence = sequence;

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

    }

}


