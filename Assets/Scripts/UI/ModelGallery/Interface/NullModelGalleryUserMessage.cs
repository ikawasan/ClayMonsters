using UnityEngine;

namespace UI.ModelGallery.Interface
{
    /// <summary>
    /// メッセージウィンドウ未配線時の展示室通知フォールバック
    /// </summary>
    public sealed class NullModelGalleryUserMessage : IModelGalleryUserMessage
    {
        /// <inheritdoc />
        public void ShowLocalized(string key, string fallback)
        {
            Debug.LogError($"[NullModelGalleryUserMessage] key={key} message={fallback}");
        }
    }
}
