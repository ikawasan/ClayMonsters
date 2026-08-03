using ClayEditor.Rigging;
using Localization;
using SaveData;
using System.Collections.Generic;
using UI.ClayEditor.View;

namespace Scene.TrainingScene.Domain
{
    /// <summary>
    /// 育成完了リザルト画面向けの表示データ
    /// 表示文字列はプロパティ参照時に現在言語で組み立てる
    /// </summary>
    public readonly struct TrainingAutoResultPresentation
    {
        private readonly string titleKey;
        private readonly string titleFallback;
        private readonly string continueKey;
        private readonly string continueFallback;
        private readonly string saveResultKey;
        private readonly string saveResultFallback;

        /// <summary>
        /// 表示データを生成する
        /// </summary>
        /// <param name="modelName">モデル名</param>
        /// <param name="status">最終ステータス</param>
        /// <param name="attacks">技構成</param>
        /// <param name="saveResultKey">保存結果キー</param>
        /// <param name="saveResultFallback">保存結果フォールバック</param>
        /// <param name="continueKey">続行ボタンキー</param>
        /// <param name="continueFallback">続行ボタンフォールバック</param>
        /// <param name="thumbnailPng">モデルサムネイルPNG</param>
        /// <param name="titleKey">タイトルキー</param>
        /// <param name="titleFallback">タイトルフォールバック</param>
        public TrainingAutoResultPresentation(
            string modelName,
            ModelStatus status,
            IReadOnlyList<MotionType> attacks,
            string saveResultKey,
            string saveResultFallback,
            string continueKey,
            string continueFallback,
            byte[] thumbnailPng,
            string titleKey,
            string titleFallback)
        {
            ModelName = modelName ?? string.Empty;
            Status = status;
            Attacks = attacks ?? System.Array.Empty<MotionType>();
            ThumbnailPng = thumbnailPng;
            this.saveResultKey = saveResultKey ?? string.Empty;
            this.saveResultFallback = saveResultFallback ?? string.Empty;
            this.continueKey = continueKey ?? string.Empty;
            this.continueFallback = continueFallback ?? string.Empty;
            this.titleKey = titleKey ?? string.Empty;
            this.titleFallback = titleFallback ?? string.Empty;
        }

        /// <summary>
        /// 表示データを生成する
        /// </summary>
        /// <param name="modelName">モデル名</param>
        /// <param name="status">最終ステータス</param>
        /// <param name="attacks">技構成</param>
        /// <param name="saveResultKey">保存結果キー</param>
        /// <param name="saveResultFallback">保存結果フォールバック</param>
        public TrainingAutoResultPresentation(
            string modelName,
            ModelStatus status,
            IReadOnlyList<MotionType> attacks,
            string saveResultKey = null,
            string saveResultFallback = null)
            : this(
                modelName,
                status,
                attacks,
                saveResultKey,
                saveResultFallback,
                GameTextKeys.TrainingChooseSave, "保存先",
                null,
                GameTextKeys.TrainingComplete,
                "育成完了")
        {
        }

        /// <summary>
        /// ウィンドウタイトル
        /// </summary>
        public string TitleText =>
            string.IsNullOrEmpty(titleKey)
                ? titleFallback
                : LocalizedText.GetOrFallback(titleKey, titleFallback);

        /// <summary>
        /// モデル名
        /// </summary>
        public string ModelName { get; }

        /// <summary>
        /// 最終ステータス
        /// </summary>
        public ModelStatus Status { get; }

        /// <summary>
        /// 最終ステータス表示
        /// </summary>
        public string StatsText =>
            ModelSaveSummaryFormatter.FormatTrainingFinalStatusParameters(Status);

        /// <summary>
        /// 技構成
        /// </summary>
        public IReadOnlyList<MotionType> Attacks { get; }

        /// <summary>
        /// 保存結果
        /// </summary>
        public string SaveResultMessage =>
            string.IsNullOrEmpty(saveResultKey)
                ? saveResultFallback
                : LocalizedText.GetOrFallback(saveResultKey, saveResultFallback);

        /// <summary>
        /// 続行ボタンラベル
        /// </summary>
        public string ContinueButtonLabel =>
            string.IsNullOrEmpty(continueKey)
                ? continueFallback
                : LocalizedText.GetOrFallback(continueKey, continueFallback);

        /// <summary>
        /// モデルサムネイルPNG
        /// </summary>
        public byte[] ThumbnailPng { get; }
    }
}
