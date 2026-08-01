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
    /// 展示室閲覧タブの育成済みセーブスロット風セル
    /// 右上にお気に入りボタンを持つ
    /// </summary>
    public sealed class ModelGalleryItemCellView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup cellCanvasGroup;
        [SerializeField] private LHButton selectButton;
        [SerializeField] private LHButton favoriteButton;
        [SerializeField] private Image favoriteButtonImage;
        [SerializeField] private Image favoriteMarkImage;
        [SerializeField] private Sprite favoriteOffSprite;
        [SerializeField] private Sprite favoriteOnSprite;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text favoriteCountText;
        [SerializeField] private Image thumbnailImage;
        [SerializeField] private Image selectedHighlightImage;

        private Texture2D ownedThumbnail;
        private Sprite ownedSprite;
        private bool favoriteButtonInteractionConfigured;

        private void Awake()
        {
            ConfigureFavoriteButtonInteraction();
        }

        /// <summary>
        /// セル選択を購読する
        /// </summary>
        public IDisposable SubscribeClick(UnityAction action)
        {
            if (selectButton == null)
            {
                Debug.LogError("[ModelGalleryItemCellView] selectButtonが未配線です", this);
                return EmptyDisposable.Instance;
            }

            return selectButton.SubscribeOnClick(action);
        }

        /// <summary>
        /// お気に入り押下を購読する
        /// </summary>
        public IDisposable SubscribeFavoriteClick(UnityAction action)
        {
            if (favoriteButton == null)
            {
                Debug.LogError("[ModelGalleryItemCellView] favoriteButtonが未配線です", this);
                return EmptyDisposable.Instance;
            }

            ConfigureFavoriteButtonInteraction();
            return favoriteButton.SubscribeOnClick(action);
        }

        /// <summary>
        /// セルの表示内容を更新する
        /// </summary>
        public void SetContent(
            string title,
            int favoriteCount,
            bool isFavorited,
            Texture2D thumbnail,
            bool isSelected)
        {
            if (titleText != null)
            {
                titleText.text = title ?? string.Empty;
            }

            ApplyFavoriteVisual(isFavorited);

            if (favoriteCountText != null)
            {
                favoriteCountText.text = Mathf.Max(0, favoriteCount).ToString();
            }

            ReplaceThumbnail(thumbnail);
            if (thumbnailImage != null)
            {
                thumbnailImage.enabled = thumbnail != null;
                thumbnailImage.sprite = ownedSprite;
                thumbnailImage.maskable = true;
            }

            if (selectedHighlightImage != null)
            {
                selectedHighlightImage.enabled = isSelected;
                selectedHighlightImage.maskable = true;
            }
        }

        /// <summary>
        /// セルの表示を切り替える
        /// ScrollRect配下はネストCanvasではなくCanvasGroupで制御する
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (cellCanvasGroup == null)
            {
                Debug.LogError("[ModelGalleryItemCellView] cellCanvasGroupが未配線です", this);
                return;
            }

            cellCanvasGroup.alpha = visible ? 1f : 0f;
            cellCanvasGroup.interactable = visible;
            cellCanvasGroup.blocksRaycasts = visible;
        }

        private void ApplyFavoriteVisual(bool isFavorited)
        {
            ConfigureFavoriteButtonInteraction();

            if (favoriteMarkImage == null)
            {
                Debug.LogError("[ModelGalleryItemCellView] favoriteMarkImageが未配線です", this);
                return;
            }

            Sprite heart = isFavorited ? favoriteOnSprite : favoriteOffSprite;
            favoriteMarkImage.enabled = heart != null;
            favoriteMarkImage.sprite = heart;
            favoriteMarkImage.preserveAspect = true;
            favoriteMarkImage.type = Image.Type.Simple;
            favoriteMarkImage.maskable = true;
            favoriteMarkImage.color = Color.white;
            favoriteMarkImage.raycastTarget = false;
        }

        /// <summary>
        /// お気に入りボタンでSelected/SpriteSwapを使わない
        /// ヒット領域のImageは透明にしてハート本体はMark側で表示する
        /// </summary>
        private void ConfigureFavoriteButtonInteraction()
        {
            if (favoriteButtonInteractionConfigured || favoriteButton == null)
            {
                return;
            }

            favoriteButton.navigation = new Navigation { mode = Navigation.Mode.None };
            favoriteButton.transition = Selectable.Transition.ColorTint;
            var colors = favoriteButton.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.55f);
            favoriteButton.colors = colors;
            favoriteButton.spriteState = default;

            Image hitImage = favoriteButtonImage != null
                ? favoriteButtonImage
                : favoriteButton.targetGraphic as Image;
            if (hitImage != null)
            {
                // タイトルメニューボタン枠をハート代わりに使わない
                hitImage.color = new Color(1f, 1f, 1f, 0f);
                hitImage.raycastTarget = true;
            }

            favoriteButtonInteractionConfigured = true;
        }

        private void ReplaceThumbnail(Texture2D thumbnail)
        {
            if (ownedThumbnail == thumbnail)
            {
                return;
            }

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
