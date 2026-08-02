using Localization;
using ClayEditor.Rigging;
using System.Collections.Generic;

namespace Scene.TrainingScene.Domain
{
    /// <summary>
    /// 育成完了リザルト画面向けの表示データ
    /// じっくり育成と自動育成で共通利用する
    /// </summary>
    public readonly struct TrainingAutoResultPresentation
    {
        /// <summary>
        /// 表示データを生成する
        /// </summary>
        /// <param name="modelName">モデル名</param>
        /// <param name="statsText">最終ステータス</param>
        /// <param name="attacks">技構成</param>
        /// <param name="saveResultMessage">保存結果</param>
        /// <param name="continueButtonLabel">続行ボタンラベル</param>
        /// <param name="thumbnailPng">モデルサムネイルPNG</param>
        /// <param name="titleText">ウィンドウタイトル</param>
        public TrainingAutoResultPresentation(
            string modelName,
            string statsText,
            IReadOnlyList<MotionType> attacks,
            string saveResultMessage,
            string continueButtonLabel,
            byte[] thumbnailPng,
            string titleText)
        {
            ModelName = modelName ?? string.Empty;
            StatsText = statsText ?? string.Empty;
            Attacks = attacks ?? System.Array.Empty<MotionType>();
            SaveResultMessage = saveResultMessage ?? string.Empty;
            ContinueButtonLabel = continueButtonLabel ?? string.Empty;
            ThumbnailPng = thumbnailPng;
            TitleText = string.IsNullOrEmpty(titleText) ? LocalizedText.Get(GameTextKeys.TrainingComplete) : titleText;
        }

        /// <summary>
        /// 表示データを生成する
        /// </summary>
        /// <param name="modelName">モデル名</param>
        /// <param name="statsText">最終ステータス</param>
        /// <param name="attacks">技構成</param>
        /// <param name="saveResultMessage">保存結果</param>
        public TrainingAutoResultPresentation(
            string modelName,
            string statsText,
            IReadOnlyList<MotionType> attacks,
            string saveResultMessage)
            : this(modelName, statsText, attacks, saveResultMessage, LocalizedText.Get(GameTextKeys.TrainingChooseSave), null, LocalizedText.Get(GameTextKeys.TrainingComplete))
        {
        }

        /// <summary>
        /// ウィンドウタイトル
        /// </summary>
        public string TitleText { get; }

        /// <summary>
        /// モデル名
        /// </summary>
        public string ModelName { get; }

        /// <summary>
        /// 最終ステータス
        /// </summary>
        public string StatsText { get; }

        /// <summary>
        /// 技構成
        /// </summary>
        public IReadOnlyList<MotionType> Attacks { get; }

        /// <summary>
        /// 保存結果
        /// </summary>
        public string SaveResultMessage { get; }

        /// <summary>
        /// 続行ボタンラベル
        /// </summary>
        public string ContinueButtonLabel { get; }

        /// <summary>
        /// モデルサムネイルPNG
        /// </summary>
        public byte[] ThumbnailPng { get; }
    }
}
