using Extensions;
using LighthouseExtends.UIComponent.Button;
using SaveData;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UI.ClayEditor.View
{
    /// <summary>
    /// 育成済みセーブスロット1枠(名前とサムネイル)
    /// 見た目はTrainedSaveSlotGridプレハブ配置を正とする
    /// </summary>
    public sealed class TrainedSaveSlotCellView :
        MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        [SerializeField] private LHButton selectButton;
        [SerializeField] private Image thumbnailImage;
        [SerializeField] private TMP_Text nameText;

        private int slotIndex = -1;
        private Action<int> onSelected;
        private Action<int> onPointerEnter;
        private Action onPointerExit;

        /// <summary>
        /// 選択ボタン
        /// </summary>
        public LHButton SelectButton => selectButton;

        /// <summary>
        /// クリック購読を初期化する
        /// </summary>
        /// <param name="selectedHandler">選択時コールバック</param>
        public void Initialize(Action<int> selectedHandler)
        {
            onSelected = selectedHandler;
            if (selectButton == null)
            {
                selectButton = GetComponent<LHButton>();
            }

            if (selectButton == null)
            {
                Debug.LogError(
                    "[TrainedSaveSlotCellView] selectButtonが未設定ですHierarchyで接続してください",
                    this);
                return;
            }

            selectButton.EnsureUiSoundFeedback();
            selectButton.SubscribeOnClick(OnClicked);
        }

        /// <summary>
        /// ホバー購読を設定する
        /// </summary>
        /// <param name="enterHandler">進入時コールバック</param>
        /// <param name="exitHandler">退出時コールバック</param>
        public void SetHoverHandlers(Action<int> enterHandler, Action exitHandler)
        {
            onPointerEnter = enterHandler;
            onPointerExit = exitHandler;
        }

        /// <summary>
        /// 使用中スロットの表示を反映する
        /// </summary>
        /// <param name="index">スロット番号</param>
        /// <param name="slot">スロット</param>
        /// <param name="thumbnail">サムネイル</param>
        /// <param name="interactable">選択可能か</param>
        public void BindUsed(int index, ModelSaveSlot slot, Sprite thumbnail, bool interactable)
        {
            slotIndex = index;
            if (nameText != null)
            {
                nameText.text = slot != null ? slot.modelName : string.Empty;
                nameText.enabled = true;
            }

            ApplyThumbnail(thumbnail);
            SetInteractable(interactable);
        }

        /// <summary>
        /// 空きスロットの表示を反映する
        /// </summary>
        /// <param name="index">スロット番号</param>
        /// <param name="emptyLabel">空表示文言</param>
        /// <param name="interactable">選択可能か</param>
        public void BindEmpty(int index, string emptyLabel, bool interactable)
        {
            slotIndex = index;
            if (nameText != null)
            {
                nameText.text = string.IsNullOrEmpty(emptyLabel)
                    ? $"スロット{index + 1}"
                    : emptyLabel;
                nameText.enabled = true;
            }

            ApplyThumbnail(null);
            SetInteractable(interactable);
        }

        /// <inheritdoc/>
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (slotIndex < 0 || onPointerEnter == null)
            {
                return;
            }

            if (selectButton != null && !selectButton.interactable)
            {
                return;
            }

            onPointerEnter.Invoke(slotIndex);
        }

        /// <inheritdoc/>
        public void OnPointerExit(PointerEventData eventData)
        {
            onPointerExit?.Invoke();
        }

        private void ApplyThumbnail(Sprite thumbnail)
        {
            if (thumbnailImage == null)
            {
                return;
            }

            if (thumbnail == null)
            {
                thumbnailImage.sprite = null;
                thumbnailImage.enabled = false;
                return;
            }

            if (!thumbnailImage.gameObject.activeSelf)
            {
                thumbnailImage.gameObject.SetActive(true);
            }

            Transform root = thumbnailImage.transform.parent;
            if (root != null && !root.gameObject.activeSelf)
            {
                root.gameObject.SetActive(true);
            }

            thumbnailImage.sprite = thumbnail;
            thumbnailImage.enabled = true;
            thumbnailImage.color = Color.white;
            thumbnailImage.preserveAspect = false;
            thumbnailImage.type = Image.Type.Simple;
            thumbnailImage.maskable = true;
            thumbnailImage.raycastTarget = false;
        }

        private void SetInteractable(bool interactable)
        {
            if (selectButton != null)
            {
                selectButton.interactable = interactable;
            }
        }

        private void OnClicked()
        {
            if (slotIndex < 0 || onSelected == null)
            {
                return;
            }

            onSelected.Invoke(slotIndex);
        }
    }
}
