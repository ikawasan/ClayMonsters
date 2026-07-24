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
        /// 現在週(1始まり)旧データでは曜日
        /// </summary>
        public int day = 1;

        /// <summary>
        /// 互換用の旧ターン位置
        /// </summary>
        public int turnIndexInDay;

        /// <summary>
        /// 互換用の旧遠征フラグ
        /// </summary>
        public bool usedFirstExpedition;

        /// <summary>
        /// 互換用の旧遠征フラグ
        /// </summary>
        public bool usedSecondExpedition;

        /// <summary>
        /// 行動体力
        /// </summary>
        public int stamina;

        /// <summary>
        /// 所持金
        /// </summary>
        public int money;

        /// <summary>
        /// 訓練大成功率の加算(百分率)
        /// </summary>
        public float trainGreatSuccessBonusPercent;

        /// <summary>
        /// 訓練大成功ボーナスの残り週数
        /// </summary>
        public int trainGreatSuccessBonusWeeks;

        /// <summary>
        /// 所持アイテム一覧
        /// </summary>
        public List<TrainingInventoryEntry> inventory = new List<TrainingInventoryEntry>();

        /// <summary>
        /// 売店に並んでいる商品ID(常に最大3件)
        /// </summary>
        public List<string> shopOfferItemIds = new List<string>();

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
