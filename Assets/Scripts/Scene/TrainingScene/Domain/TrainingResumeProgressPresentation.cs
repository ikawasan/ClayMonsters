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
        /// <param name="progressLabel">曜日・時間割・所持金・体力</param>
        /// <param name="statsText">ステータス表示</param>
        /// <param name="attacks">技構成</param>
        /// <param name="thumbnailPng">モデルサムネイルPNG</param>
        public TrainingResumeProgressPresentation(
            string modelName,
            string progressLabel,
            string statsText,
            IReadOnlyList<MotionType> attacks,
            byte[] thumbnailPng)
        {
            ModelName = modelName ?? string.Empty;
            ProgressLabel = progressLabel ?? string.Empty;
            StatsText = statsText ?? string.Empty;
            Attacks = attacks ?? System.Array.Empty<MotionType>();
            ThumbnailPng = thumbnailPng;
        }

        /// <summary>
        /// モデル名
        /// </summary>
        public string ModelName { get; }

        /// <summary>
        /// 曜日・時間割・所持金・体力
        /// </summary>
        public string ProgressLabel { get; }

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
    }
}
