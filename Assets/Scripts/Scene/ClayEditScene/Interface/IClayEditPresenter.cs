namespace Scene.ClayEditScene.Interface
{
    /// <summary>
    /// ClayEditシーンの進行を制御するPresenter
    /// </summary>
    public interface IClayEditPresenter
    {
        /// <summary>
        /// イベント購読など初期化を行う
        /// </summary>
        void Setup();

        /// <summary>
        /// フェード前の入場準備を行う
        /// </summary>
        void OnEnter();

        /// <summary>
        /// フェード明け後に入場UIを表示する
        /// </summary>
        void OnEnterAfterFadeIn();

        /// <summary>
        /// 退場時に保存UIと作り直しUIを整理する
        /// </summary>
        void OnLeave();
    }
}
