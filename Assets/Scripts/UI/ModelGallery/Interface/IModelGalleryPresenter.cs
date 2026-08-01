namespace UI.ModelGallery.Interface
{
    /// <summary>
    /// 展示室画面のPresenter契約
    /// </summary>
    public interface IModelGalleryPresenter
    {
        /// <summary>
        /// 初期購読を設定する
        /// </summary>
        void Setup();

        /// <summary>
        /// 画面を表示する
        /// </summary>
        void Show();

        /// <summary>
        /// 画面を非表示にする
        /// </summary>
        void Hide();
    }
}
