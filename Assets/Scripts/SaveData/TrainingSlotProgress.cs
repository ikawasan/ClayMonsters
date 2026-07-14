using ClayEditor.Rigging;
using System.Collections.Generic;

namespace SaveData
{
    /// <summary>
    /// 育成途中の進行データ
    /// スロットごとに1件だけ保持する
    /// </summary>
    [System.Serializable]
    public class TrainingSlotProgress
    {
        /// <summary>
        /// 育成途中か
        /// </summary>
        public bool inProgress;

        /// <summary>
        /// 現在の曜日
        /// </summary>
        public int day = 1;

        /// <summary>
        /// 当日の完了ターン数
        /// </summary>
        public int turnIndexInDay;

        /// <summary>
        /// 行動体力
        /// </summary>
        public int stamina;

        /// <summary>
        /// 育成中のステータス
        /// </summary>
        public ModelStatus status = new ModelStatus();

        /// <summary>
        /// 育成中の攻撃構成
        /// </summary>
        public List<MotionType> attackMotions = new List<MotionType>();
    }
}
