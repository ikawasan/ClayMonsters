using Scene.TitleScene.Interface;
using UI.ModelGallery.Interface;
using VContainer;

namespace Scene.TitleScene.Service
{
    /// <summary>
    /// タイトルのメッセージウィンドウへ展示室通知を橋渡しする
    /// </summary>
    public sealed class ModelGalleryUserMessageAdapter : IModelGalleryUserMessage
    {
        private readonly ITitleMessageWindowView messageWindow;

        /// <summary>
        /// 依存を注入する
        /// </summary>
        /// <param name="messageWindow">タイトルメッセージウィンドウ</param>
        [Inject]
        public ModelGalleryUserMessageAdapter(ITitleMessageWindowView messageWindow)
        {
            this.messageWindow = messageWindow;
        }

        /// <inheritdoc />
        public void ShowLocalized(string key, string fallback)
        {
            if (messageWindow == null)
            {
                UnityEngine.Debug.LogError(
                    "[ModelGalleryUserMessageAdapter] messageWindowが未配線です");
                return;
            }

            messageWindow.ShowLocalized(key, fallback);
        }
    }
}
