using ClayEditor.Rigging;

namespace Battle
{
    /// <summary>
    /// ネットワーク同期された攻撃結果
    /// </summary>
    public readonly struct BattleRemoteStrikePayload
    {
        /// <summary>
        /// 同期攻撃結果を生成する
        /// </summary>
        public BattleRemoteStrikePayload(
            int moveIndex,
            bool hit,
            int damage,
            bool partLost,
            BonePart lostPart,
            int lostLimbIndex,
            bool isKnockout,
            int attackSequence = 0)
        {
            MoveIndex = moveIndex;
            Hit = hit;
            Damage = damage;
            PartLost = partLost;
            LostPart = lostPart;
            LostLimbIndex = lostLimbIndex;
            IsKnockout = isKnockout;
            AttackSequence = attackSequence;
        }

        /// <summary>
        /// 攻撃技番号
        /// </summary>
        public int MoveIndex { get; }

        /// <summary>
        /// 命中したか
        /// </summary>
        public bool Hit { get; }

        /// <summary>
        /// ダメージ
        /// </summary>
        public int Damage { get; }

        /// <summary>
        /// 部位欠損が発生したか
        /// </summary>
        public bool PartLost { get; }

        /// <summary>
        /// 欠損した部位
        /// </summary>
        public BonePart LostPart { get; }

        /// <summary>
        /// 欠損したリム番号
        /// </summary>
        public int LostLimbIndex { get; }

        /// <summary>
        /// とどめか
        /// </summary>
        public bool IsKnockout { get; }

        /// <summary>
        /// 攻撃開始同期番号・カウンター無効化判定用
        /// </summary>
        public int AttackSequence { get; }
    }
}
