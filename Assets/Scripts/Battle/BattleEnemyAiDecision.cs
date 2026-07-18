namespace Battle
{
    /// <summary>
    /// 敵AIが返す移動意図と攻撃技番号と部位修復意図
    /// </summary>
    public readonly struct BattleEnemyAiDecision
    {
        /// <summary>
        /// 敵AIの行動決定を生成する
        /// </summary>
        /// <param name="movementIntent">-1=接近・0=停止・+1=後退</param>
        /// <param name="attackMoveIndex">攻撃する技番号・攻撃しないとき-1</param>
        /// <param name="wantsRepair">部位修復を行うか</param>
        /// <param name="stepIntent">ステップ移動意図・通常移動でないとき0以外</param>
        /// <param name="attackSequence">ネットワーク攻撃開始同期番号・未使用時0</param>
        public BattleEnemyAiDecision(
            int movementIntent,
            int attackMoveIndex,
            bool wantsRepair = false,
            int stepIntent = 0,
            int attackSequence = 0)
        {
            MovementIntent = movementIntent;
            AttackMoveIndex = attackMoveIndex;
            WantsRepair = wantsRepair;
            StepIntent = stepIntent;
            AttackSequence = attackSequence;
        }

        /// <summary>
        /// 移動意図(-1=接近・0=停止・+1=後退)
        /// </summary>
        public int MovementIntent { get; }

        /// <summary>
        /// 攻撃する技番号・攻撃しないとき-1
        /// </summary>
        public int AttackMoveIndex { get; }

        /// <summary>
        /// 部位修復を行うか
        /// </summary>
        public bool WantsRepair { get; }

        /// <summary>
        /// ステップ移動意図(-1=接近・0=なし・+1=後退)
        /// </summary>
        public int StepIntent { get; }

        /// <summary>
        /// ネットワーク攻撃開始同期番号・未使用時0
        /// </summary>
        public int AttackSequence { get; }

        /// <summary>
        /// 停止のみの決定
        /// </summary>
        public static BattleEnemyAiDecision Hold => new BattleEnemyAiDecision(0, -1);

        /// <summary>
        /// 部位修復に専念する決定
        /// </summary>
        public static BattleEnemyAiDecision Repair => new BattleEnemyAiDecision(0, -1, true);

    }
}
