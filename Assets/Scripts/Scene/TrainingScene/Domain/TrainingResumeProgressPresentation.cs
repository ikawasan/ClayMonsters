using ClayEditor.Rigging;
using System.Collections.Generic;

namespace Scene.TrainingScene.Domain
{
    /// <summary>
    /// 育成再開確認ウィンドウ向けの表示データ
    /// </summary>
    public readonly struct TrainingResumeProgressPresentation
    {
        /// <summary>
        /// 表示データを生成する
        /// </summary>
        /// <param name="modelName">モデル名</param>
        /// <param name="scheduleMoneyLabel">曜日・時間割・所持金</param>
        /// <param name="motivationLabel">やる気ラベル</param>
        /// <param name="staminaLabel">体力ラベル</param>
        /// <param name="statsText">ステータス表示</param>
        /// <param name="attacks">技構成</param>
        /// <param name="thumbnailPng">モデルサムネイルPNG</param>
        /// <param name="motivation">やる気</param>
        public TrainingResumeProgressPresentation(
            string modelName,
            string scheduleMoneyLabel,
            string motivationLabel,
            string staminaLabel,
            string statsText,
            IReadOnlyList<MotionType> attacks,
            byte[] thumbnailPng,
            TrainingMotivation motivation = TrainingMotivation.Normal)
        {
            ModelName = modelName ?? string.Empty;
            ScheduleMoneyLabel = scheduleMoneyLabel ?? string.Empty;
            MotivationLabel = motivationLabel ?? string.Empty;
            StaminaLabel = staminaLabel ?? string.Empty;
            StatsText = statsText ?? string.Empty;
            Attacks = attacks ?? System.Array.Empty<MotionType>();
            ThumbnailPng = thumbnailPng;
            Motivation = TrainingMotivationCatalog.Clamp((int)motivation);
        }

        /// <summary>
        /// モデル名
        /// </summary>
        public string ModelName { get; }

        /// <summary>
        /// 曜日・時間割・所持金
        /// </summary>
        public string ScheduleMoneyLabel { get; }

        /// <summary>
        /// やる気ラベル
        /// </summary>
        public string MotivationLabel { get; }

        /// <summary>
        /// 体力ラベル
        /// </summary>
        public string StaminaLabel { get; }

        /// <summary>
        /// ステータス表示
        /// </summary>
        public string StatsText { get; }

        /// <summary>
        /// 技構成
        /// </summary>
        public IReadOnlyList<MotionType> Attacks { get; }

        /// <summary>
        /// モデルサムネイルPNG
        /// </summary>
        public byte[] ThumbnailPng { get; }

        /// <summary>
        /// やる気
        /// </summary>
        public TrainingMotivation Motivation { get; }
    }
}
