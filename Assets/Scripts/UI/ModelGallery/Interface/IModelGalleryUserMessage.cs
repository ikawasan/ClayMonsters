namespace UI.ModelGallery.Interface
{
    /// <summary>
    /// 展示室のプレーヤー向け通知表示契約
    /// </summary>
    public interface IModelGalleryUserMessage
    {
        /// <summary>
        /// ローカライズキーでメッセージを表示する
        /// </summary>
        /// <param name="key">文言キー</param>
        /// <param name="fallback">フォールバック文言</param>
        void ShowLocalized(string key, string fallback);
    }
}
