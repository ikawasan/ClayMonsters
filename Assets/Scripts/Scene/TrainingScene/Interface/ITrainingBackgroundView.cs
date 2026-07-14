using Scene.TrainingScene.Domain;

namespace Scene.TrainingScene.Interface
{
    /// <summary>
    /// 育成シーンの背景オブジェクト切り替えを担当する
    /// </summary>
    public interface ITrainingBackgroundView
    {
        /// <summary>
        /// 行き先の背景オブジェクトへ切り替える
        /// </summary>
        /// <param name="location">切り替え先の行き先</param>
        void ShowLocationBackground(TrainingLocation location);

        /// <summary>
        /// 休憩用の背景オブジェクトへ切り替える
        /// </summary>
        void ShowRestBackground();

        /// <summary>
        /// 選択待ちなどで使う既定の背景オブジェクトへ切り替える
        /// </summary>
        void ShowDefaultBackground();

        /// <summary>
        /// シーン退場時に背景表示を止める
        /// </summary>
        void HideForLeave();
    }
}
