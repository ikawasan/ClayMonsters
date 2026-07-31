namespace SaveData
{
    /// <summary>
    /// スキルツリーの前提接続(From解放でToへ進める)
    /// </summary>
    public readonly struct SkillTreeEdge
    {
        /// <summary>
        /// 前提ノード
        /// </summary>
        public SkillTreeNodeId From { get; }

        /// <summary>
        /// 接続先ノード
        /// </summary>
        public SkillTreeNodeId To { get; }

        /// <summary>
        /// 接続を生成する
        /// </summary>
        /// <param name="from">前提</param>
        /// <param name="to">接続先</param>
        public SkillTreeEdge(SkillTreeNodeId from, SkillTreeNodeId to)
        {
            From = from;
            To = to;
        }
    }
}
