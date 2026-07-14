namespace ClayEditor.Rigging
{
    /// <summary>
    /// モデルの形状(ボーン構造)から判定した、実行可能なモーションの情報
    /// UI側でモーションボタンの有効、無効の切り替えに使う
    /// </summary>
    public struct MotionAvailability
    {
        /// <summary>
        /// 歩行(Run)が可能か、下方向に伸びる脚が必要数以上あるか
        /// </summary>
        public bool CanWalk;

        /// <summary>
        /// 攻撃(パンチ)が可能か、左右方向に伸びる腕が1本以上あるか
        /// </summary>
        public bool CanPunch;

        /// <summary>
        /// 脚と判定された枝の数
        /// </summary>
        public int LegCount;

        /// <summary>
        /// 腕と判定された枝の数
        /// </summary>
        public int ArmCount;

        /// <summary>
        /// 前(頭など前方に伸びる)と判定された枝の数
        /// </summary>
        public int FrontCount;

        /// <summary>
        /// 後ろ(尻尾など後方に伸びる)と判定された枝の数
        /// </summary>
        public int BackCount;
    }
}