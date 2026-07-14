using R3;

namespace Scene.ClayEditScene.Interface
{
    /// <summary>
    /// ClayEdit入場時の新規作成・作り直し選択UI
    /// </summary>
    public interface IClayEditEntryView
    {
        /// <summary>
        /// 新規作成ボタンが押された通知
        /// </summary>
        Observable<Unit> OnNewCreateClicked { get; }

        /// <summary>
        /// モンスターを作り直すボタンが押された通知
        /// </summary>
        Observable<Unit> OnRemakeClicked { get; }

        /// <summary>
        /// 入場選択UIを表示する
        /// </summary>
        void Show();

        /// <summary>
        /// 入場選択UIを隠す
        /// </summary>
        void Hide();
    }
}
