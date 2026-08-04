using Cysharp.Threading.Tasks;
using Extensions;
using LighthouseExtends.UIComponent.Button;
using Localization;
using Scene.TrainingScene.Domain;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UI.ClayEditor.View;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Scene.TrainingScene.View
{
    /// <summary>
    /// 売店と所持アイテムの一覧ウィンドウ
    /// </summary>
    public sealed class TrainingItemListWindowView : MonoBehaviour, ILanguageAwareUi
    {
        public const int SlotCount = 3;

        [Header("Root")]
        [FormerlySerializedAs("windowGroup")]
        [SerializeField] private GameObject windowRoot;
        [SerializeField] private Image blocker;

        [Header("Texts")]
        [SerializeField] private TMP_Text titleText;
        [FormerlySerializedAs("modelNameText")]
        [SerializeField] private TMP_Text moneyText;

        [Header("Item Slots")]
        [SerializeField] private TrainingItemSlotView[] itemSlots = new TrainingItemSlotView[SlotCount];

        [Header("Buttons")]
        [FormerlySerializedAs("backToTitleButton")]
        [SerializeField] private LHButton closeButton;
        [FormerlySerializedAs("backToTitleButtonLabel")]
        [SerializeField] private TMP_Text closeButtonLabel;
        [SerializeField] private LHButton nextPageButton;
        [SerializeField] private TMP_Text nextPageButtonLabel;
        [SerializeField] private LHButton openInventoryButton;
        [SerializeField] private TMP_Text openInventoryButtonLabel;

        private bool hasChoice;
        private int pendingChoice;
        private bool uiBound;
        private enum ListMode
        {
            None,
            Shop,
            Inventory
        }

        private ListMode listMode = ListMode.None;
        private IReadOnlyList<TrainingShopItem> cachedShopItems;
        private IReadOnlyList<TrainingInventoryEntryView> cachedInventoryEntries;
        private int cachedMoney;
        private bool cachedHasNextPage;
        private bool cachedShowOpenInventory;
        private bool listChromeOriginalsCaptured;
        private string shopTitleOriginal = "売店";
        private string inventoryTitleOriginal = "所持アイテム";
        private string closeOriginal = "戻る";
        private string nextPageOriginal = "次のページ";
        private string openInventoryOriginal = "所持";

        private void CaptureListChromeOriginalsIfNeeded()
        {
            if (listChromeOriginalsCaptured)
            {
                return;
            }

            closeOriginal = SceneLocalizedLabel.Capture(closeButtonLabel, closeOriginal);
            nextPageOriginal = SceneLocalizedLabel.Capture(nextPageButtonLabel, nextPageOriginal);
            openInventoryOriginal = SceneLocalizedLabel.Capture(
                openInventoryButtonLabel,
                openInventoryOriginal);
            listChromeOriginalsCaptured = true;
        }

        /// <summary>
        /// 閉じるボタンがあるか
        /// </summary>
        public bool HasCloseButton => closeButton != null;

        /// <summary>
        /// 次ページボタンがあるか
        /// </summary>
        public bool HasNextPageButton => nextPageButton != null;

        /// <summary>
        /// 所持ボタンがあるか
        /// </summary>
        public bool HasOpenInventoryButton => openInventoryButton != null;

        /// <summary>
        /// アイテム枠で選択できるか
        /// </summary>
        public bool HasItemSlots => itemSlots != null && itemSlots.Length > 0;

        /// <summary>
        /// 売店一覧を表示する
        /// </summary>
        public void ShowShop(
            IReadOnlyList<TrainingShopItem> items,
            int currentMoney,
            bool hasNextPage,
            bool showOpenInventory)
        {
            EnsureUiBound();
            CaptureListChromeOriginalsIfNeeded();
            listMode = ListMode.Shop;
            cachedShopItems = items;
            cachedMoney = currentMoney;
            cachedHasNextPage = hasNextPage;
            cachedShowOpenInventory = showOpenInventory;
            BindActionButtons();
            SetTitle(SceneLocalizedLabel.Resolve(GameTextKeys.TrainingShopTitle, shopTitleOriginal));
            SetMoney(currentMoney);
            BindShopSlots(items);
            ConfigureCloseButton(
                SceneLocalizedLabel.Resolve(GameTextKeys.CommonReturn, closeOriginal));
            ConfigureNextPageButton(hasNextPage);
            ConfigureOpenInventoryButton(showOpenInventory);
            hasChoice = false;
            SetWindowVisible(true);
        }

        /// <summary>
        /// 所持アイテム一覧を表示する
        /// </summary>
        public void ShowInventory(
            IReadOnlyList<TrainingInventoryEntryView> entries,
            bool hasNextPage)
        {
            EnsureUiBound();
            CaptureListChromeOriginalsIfNeeded();
            listMode = ListMode.Inventory;
            cachedInventoryEntries = entries;
            cachedHasNextPage = hasNextPage;
            BindActionButtons();
            SetTitle(
                SceneLocalizedLabel.Resolve(
                    GameTextKeys.TrainingInventoryTitle,
                    inventoryTitleOriginal));
            if (moneyText != null)
            {
                moneyText.text = string.Empty;
                moneyText.enabled = false;
            }

            BindInventorySlots(entries);
            ConfigureCloseButton(
                SceneLocalizedLabel.Resolve(GameTextKeys.CommonReturn, closeOriginal));
            ConfigureNextPageButton(hasNextPage);
            ConfigureOpenInventoryButton(false);
            hasChoice = false;
            SetWindowVisible(true);
        }

        /// <inheritdoc/>
        public void RefreshLocalizedUi()
        {
            if (listMode == ListMode.Shop)
            {
                ShowShop(
                    cachedShopItems,
                    cachedMoney,
                    cachedHasNextPage,
                    cachedShowOpenInventory);
            }
            else if (listMode == ListMode.Inventory)
            {
                ShowInventory(cachedInventoryEntries, cachedHasNextPage);
            }
        }

        /// <summary>
        /// ウィンドウを隠す
        /// </summary>
        public void Hide()
        {
            hasChoice = false;
            listMode = ListMode.None;
            ClearSlots();
            SetWindowVisible(false);
        }

        /// <summary>
        /// ウィンドウ内の選択を待つ
        /// </summary>
        public async UniTask<int> WaitWindowActionAsync(CancellationToken cancellationToken)
        {
            hasChoice = false;
            await UniTask.WaitUntil(() => hasChoice, cancellationToken: cancellationToken);
            return pendingChoice;
        }

        /// <summary>
        /// 外部から選択を確定する
        /// </summary>
        public void CompleteChoice(int choice)
        {
            pendingChoice = choice;
            hasChoice = true;
        }

        private void EnsureUiBound()
        {
            if (uiBound)
            {
                return;
            }

            uiBound = true;
            if (windowRoot == null)
            {
                windowRoot = gameObject;
            }

            if (closeButton == null)
            {
                Debug.LogError(
                    "[TrainingItemListWindowView] closeButtonが未配線です",
                    this);
            }

            if (itemSlots == null || itemSlots.Length == 0)
            {
                Debug.LogError(
                    "[TrainingItemListWindowView] itemSlotsが未配線です",
                    this);
            }
        }

        private void BindActionButtons()
        {
            BindButton(closeButton, TrainingShopChoiceCodes.Back);
            BindButton(nextPageButton, TrainingShopChoiceCodes.NextPage);
            BindButton(openInventoryButton, TrainingShopChoiceCodes.OpenInventory);
        }

        private void BindButton(LHButton button, int choice)
        {
            if (button == null)
            {
                return;
            }

            button.EnsureUiSoundFeedback();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => CompleteChoice(choice));
        }

        private void BindShopSlots(IReadOnlyList<TrainingShopItem> items)
        {
            int count = items != null ? items.Count : 0;
            for (int i = 0; i < SlotCount; i++)
            {
                TrainingItemSlotView slot = GetSlot(i);
                if (slot == null)
                {
                    continue;
                }

                if (i >= count || string.IsNullOrEmpty(items[i].Id))
                {
                    // 売り切れ枠は隠すがスロット位置は維持する
                    slot.Hide();
                    continue;
                }

                TrainingShopItem item = items[i];
                slot.ShowShop(
                    TrainingShopCatalog.GetLocalizedName(item),
                    TrainingShopThumbnailCatalog.Resolve(item.Id),
                    item.Price,
                    TrainingShopCatalog.GetLocalizedDescription(item));
                BindSlotSelect(slot, i);
            }
        }

        private void BindInventorySlots(IReadOnlyList<TrainingInventoryEntryView> entries)
        {
            int count = entries != null ? entries.Count : 0;
            for (int i = 0; i < SlotCount; i++)
            {
                TrainingItemSlotView slot = GetSlot(i);
                if (slot == null)
                {
                    continue;
                }

                if (i >= count)
                {
                    slot.Hide();
                    continue;
                }

                TrainingInventoryEntryView entry = entries[i];
                ResolveInventoryDisplay(entry, out string displayName, out string description);
                string name = entry.Count > 1
                    ? $"{displayName} x{entry.Count}"
                    : displayName;
                slot.ShowInventory(
                    name,
                    TrainingShopThumbnailCatalog.Resolve(entry.ItemId),
                    description);
                BindSlotSelect(slot, i);
            }
        }

        private static void ResolveInventoryDisplay(
            TrainingInventoryEntryView entry,
            out string displayName,
            out string description)
        {
            if (TrainingShopCatalog.TryGetById(entry.ItemId, out TrainingShopItem item))
            {
                displayName = TrainingShopCatalog.GetLocalizedName(item);
                description = TrainingShopCatalog.GetLocalizedDescription(item);
                return;
            }

            displayName = entry.DisplayName;
            description = entry.Description;
        }

        private void BindSlotSelect(TrainingItemSlotView slot, int index)
        {
            if (slot.SelectButton == null)
            {
                return;
            }

            int captured = index;
            slot.SelectButton.onClick.RemoveAllListeners();
            slot.SelectButton.onClick.AddListener(() => CompleteChoice(captured));
        }

        private void ClearSlots()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                GetSlot(i)?.Hide();
            }
        }

        private TrainingItemSlotView GetSlot(int index)
        {
            if (itemSlots == null || index < 0 || index >= itemSlots.Length)
            {
                return null;
            }

            return itemSlots[index];
        }

        private void ConfigureCloseButton(string label)
        {
            if (closeButton == null)
            {
                return;
            }

            closeButton.gameObject.SetActive(true);
            closeButton.interactable = true;
            LhButtonLabelUtility.SetLabel(closeButtonLabel, label);
        }

        private void ConfigureNextPageButton(bool visible)
        {
            if (nextPageButton == null)
            {
                return;
            }

            nextPageButton.gameObject.SetActive(visible);
            nextPageButton.interactable = visible;
            if (visible)
            {
                CaptureListChromeOriginalsIfNeeded();
                LhButtonLabelUtility.SetLabel(
                    nextPageButtonLabel,
                    SceneLocalizedLabel.Resolve(GameTextKeys.TrainingShopNextPage, nextPageOriginal));
            }
        }

        private void ConfigureOpenInventoryButton(bool visible)
        {
            if (openInventoryButton == null)
            {
                return;
            }

            openInventoryButton.gameObject.SetActive(visible);
            openInventoryButton.interactable = visible;
            if (visible)
            {
                CaptureListChromeOriginalsIfNeeded();
                LhButtonLabelUtility.SetLabel(
                    openInventoryButtonLabel,
                    SceneLocalizedLabel.Resolve(
                        GameTextKeys.TrainingShopOpenInventory,
                        openInventoryOriginal));
            }
        }

        private void SetTitle(string title)
        {
            if (titleText == null)
            {
                return;
            }

            titleText.text = title;
        }

        private void SetMoney(int currentMoney)
        {
            if (moneyText == null)
            {
                return;
            }

            moneyText.enabled = true;
            moneyText.text = $"{currentMoney}G";
        }

        private void SetWindowVisible(bool visible)
        {
            GameObject root = windowRoot != null ? windowRoot : gameObject;
            Canvas canvas = root.GetComponent<Canvas>();
            if (canvas != null)
            {
                CanvasVisibilityUtility.SetCanvasEnabled(canvas, visible);
            }
            else
            {
                CanvasVisibilityUtility.SetPanelActive(root, visible);
            }

            if (blocker != null)
            {
                TitleClayUiVisualUtility.ConfigureInputBlocker(blocker, visible);
            }
        }
    }
}
