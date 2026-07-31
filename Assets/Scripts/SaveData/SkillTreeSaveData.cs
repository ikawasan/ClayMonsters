using System;

namespace SaveData
{
    /// <summary>
    /// スキルツリーの解放進捗
    /// </summary>
    [Serializable]
    public sealed class SkillTreeSaveData
    {
        /// <summary>
        /// ノードIDの配列
        /// </summary>
        public int[] nodeIds = Array.Empty<int>();

        /// <summary>
        /// 各ノードの解放レベル
        /// </summary>
        public int[] levels = Array.Empty<int>();
    }
}
