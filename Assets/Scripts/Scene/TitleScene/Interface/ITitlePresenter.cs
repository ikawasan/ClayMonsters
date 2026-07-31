namespace Scene.TitleScene.Interface
{
    public interface ITitlePresenter
    {
        /// <summary>
        /// タイトル初期設定
        /// </summary>
        void Setup();

        /// <summary>
        /// 入場時にポイント表示などを更新する
        /// </summary>
        void OnEnter();

        /// <summary>
        /// 退場時にメッセージとオプションUIを閉じる
        /// </summary>
        void OnLeave();
    }
}
