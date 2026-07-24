using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Scene.TrainingScene.View
{
    /// <summary>
    /// 売店・所持アイテム1枠の表示
    /// </summary>
    public sealed class TrainingItemSlotView : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Image thumbnailImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text priceText;
        [SerializeField] private TMP_Text effectText;
        [SerializeField] private Button selectButton;

        /// <summary>
        /// 選択ボタン
        /// </summary>
        public Button SelectButton => selectButton;

        /// <summary>
        /// 売店用に表示する
        /// </summary>
        public void ShowShop(string itemName, Sprite thumbnail, int price, string effect)
        {
            SetVisible(true);
            SetName(itemName);
            SetThumbnail(thumbnail);
            SetPrice($"{price}G");
            SetEffect(effect);
        }

        /// <summary>
        /// 所持用に表示する
        /// </summary>
        public void ShowInventory(string itemName, Sprite thumbnail, string effect)
        {
            SetVisible(true);
            SetName(itemName);
            SetThumbnail(thumbnail);
            SetPrice(string.Empty);
            SetEffect(effect);
        }

        /// <summary>
        /// 枠を隠す
        /// </summary>
        public void Hide()
        {
            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            GameObject target = root != null ? root : gameObject;
            if (target.activeSelf != visible)
            {
                target.SetActive(visible);
            }
        }

        private void SetName(string value)
        {
            if (nameText == null)
            {
                return;
            }

            nameText.text = value ?? string.Empty;
        }

        private void SetPrice(string value)
        {
            if (priceText == null)
            {
                return;
            }

            bool hasPrice = !string.IsNullOrEmpty(value);
            priceText.enabled = hasPrice;
            priceText.text = hasPrice ? value : string.Empty;
        }

        private void SetEffect(string value)
        {
            if (effectText == null)
            {
                return;
            }

            effectText.text = value ?? string.Empty;
        }

        private void SetThumbnail(Sprite thumbnail)
        {
            if (thumbnailImage == null)
            {
                return;
            }

            thumbnailImage.sprite = thumbnail;
            thumbnailImage.color = Color.white;
            thumbnailImage.preserveAspect = true;
            thumbnailImage.enabled = thumbnail != null;
            if (thumbnail == null)
            {
                Debug.LogError(
                    "[TrainingItemSlotView] thumbnailがnullです",
                    this);
            }
        }
    }
}
