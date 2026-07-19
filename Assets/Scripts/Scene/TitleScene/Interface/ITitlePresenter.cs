namespace Scene.TitleScene.Interface
{
    public interface ITitlePresenter
    {
        /// <summary>
        /// タイトル初期設定
        /// </summary>
        void Setup();

        /// <summary>
        /// 退場時にメッセージとオプションUIを閉じる
        /// </summary>
        void OnLeave();
    }
}
