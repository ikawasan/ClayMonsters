using Scene.TrainingScene.Domain;

namespace Scene.TrainingScene.Interface
{
    /// <summary>
    /// 育成シーンの行き先ごとにカメラ構図を切り替える
    /// </summary>
    public interface ITrainingLocationCameraView
    {
        /// <summary>
        /// 選択待ちなどで使う既定のカメラ構図を適用する
        /// </summary>
        void ApplyDefaultView();

        /// <summary>
        /// 休憩行動用のカメラ構図を適用する
        /// </summary>
        void ApplyRestView();

        /// <summary>
        /// 行き先に対応するカメラ構図を適用する
        /// </summary>
        /// <param name="location">切り替え先の行き先</param>
        void ApplyLocationView(TrainingLocation location);
    }
}
