using Extensions;
using LighthouseExtends.UIComponent.Button;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace UI.ModelGallery.View
{
    /// <summary>
    /// 展示室投稿タブの育成前セーブスロット風セル
    /// </summary>
    public sealed class ModelGallerySlotCellView : MonoBehaviour
    {
        [SerializeField] private Canvas cellCanvas;
        [SerializeField] private LHButton selectButton;
        [SerializeField] private TMP_Text indexText;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text paramsText;
        [SerializeField] private Image thumbnailImage;
        [SerializeField] private Image selectedHighlightImage;

        private Texture2D ownedThumbnail;
        private Sprite ownedSprite;

        /// <summary>
        /// セル選択を購読する
        /// </summary>
        public IDisposable SubscribeClick(UnityAction action)
        {
            if (selectButton == null)
            {
                Debug.LogError("[ModelGallerySlotCellView] selectButtonが未配線です", this);
                return EmptyDisposable.Instance;
            }

            return selectButton.SubscribeOnClick(action);
        }

        /// <summary>
        /// セルの表示内容を更新する
        /// </summary>
        public void SetContent(
            int slotIndex,
            string modelName,
            string statusParams,
            Texture2D thumbnail,
            bool isUsed,
            bool isSelected)
        {
            if (indexText != null)
            {
                indexText.text = $"{slotIndex + 1}";
            }

            if (nameText != null)
            {
                nameText.text = isUsed
                    ? (modelName ?? string.Empty)
                    : Localization.LocalizedText.GetOrFallback(
                        Localization.GameTextKeys.ModelGalleryEmptySlot,
                        "空きスロット");
            }

            if (paramsText != null)
            {
                paramsText.text = isUsed ? (statusParams ?? string.Empty) : string.Empty;
            }

            ReplaceThumbnail(thumbnail);
            if (thumbnailImage != null)
            {
                thumbnailImage.enabled = thumbnail != null;
                thumbnailImage.sprite = ownedSprite;
                thumbnailImage.color = isUsed ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            }

            if (selectedHighlightImage != null)
            {
                selectedHighlightImage.enabled = isSelected;
            }
        }

        /// <summary>
        /// セルのCanvas表示を切り替える
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (cellCanvas == null)
            {
                Debug.LogError("[ModelGallerySlotCellView] cellCanvasが未配線です", this);
                return;
            }

            cellCanvas.enabled = visible;
        }

        private void ReplaceThumbnail(Texture2D thumbnail)
        {
            if (ownedSprite != null)
            {
                Destroy(ownedSprite);
                ownedSprite = null;
            }

            if (ownedThumbnail != null)
            {
                Destroy(ownedThumbnail);
                ownedThumbnail = null;
            }

            ownedThumbnail = thumbnail;
            if (thumbnail != null)
            {
                ownedSprite = Sprite.Create(
                    thumbnail,
                    new Rect(0f, 0f, thumbnail.width, thumbnail.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
            }
        }

        private void OnDestroy()
        {
            ReplaceThumbnail(null);
        }

        private sealed class EmptyDisposable : IDisposable
        {
            public static readonly EmptyDisposable Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
