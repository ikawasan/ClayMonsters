using ClayEditor.Rigging;

namespace Scene.TrainingScene.Domain
{
    /// <summary>
    /// 発生した育成イベントの内容
    /// </summary>
    public readonly struct TrainingEventOutcome
    {
        public TrainingEventOutcome(
            TrainingEventType eventType,
            string title,
            string message,
            TrainingStatGain statGain,
            MotionType learnedAttack)
        {
            EventType = eventType;
            Title = title;
            Message = message;
            StatGain = statGain;
            LearnedAttack = learnedAttack;
        }

        /// <summary>
        /// イベント種別
        /// </summary>
        public TrainingEventType EventType { get; }

        /// <summary>
        /// イベント見出し
        /// </summary>
        public string Title { get; }

        /// <summary>
        /// イベント説明
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// 追加ステータス上昇
        /// </summary>
        public TrainingStatGain StatGain { get; }

        /// <summary>
        /// 習得した攻撃
        /// </summary>
        public MotionType LearnedAttack { get; }
    }
}
