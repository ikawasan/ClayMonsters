using ClayEditor.Rigging;
using Cysharp.Threading.Tasks;
using Extensions;
using LighthouseExtends.UIComponent.Button;
using SaveData;
using Scene.TrainingScene.Domain;
using Scene.TrainingScene.Interface;
using System;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UI.ClayEditor.View;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Scene.TrainingScene.View
{
    /// <summary>
    /// 育成の日程・体力・ステータス・行き先選択UI
    /// </summary>
    public sealed class TrainingHudView : MonoBehaviour, ITrainingHudView
    {
        [Header("Root")]
        [Tooltip("HUD全体のCanvas")]
        [SerializeField] private Canvas rootCanvas;

        [Header("Status Texts")]
        [Tooltip("現在の曜日。月曜〜金曜または育成完了")]
        [SerializeField] private TMP_Text dayText;
        [Tooltip("現在の時間割。1時間目〜放課後。完了時はモデル名")]
        [SerializeField] private TMP_Text periodText;
        [Tooltip("現在ターン数。ターン n/9 または完了メッセージ")]
        [SerializeField] private TMP_Text turnText;
        [Tooltip("体力の数値表示。体力 現在/最大")]
        [SerializeField] private TMP_Text staminaText;
        [Tooltip("やる気のテキスト。やる気　のあとにアイコンまたは記号を出す")]
        [SerializeField] private TMP_Text motivationText;
        [Tooltip("やる気アイコン。未配線時はmotivationTextへ記号を出す")]
        [SerializeField] private Image motivationIcon;
        [Tooltip("所持金の数値表示。nG")]
        [SerializeField] private TMP_Text moneyText;
        [Tooltip("HP・攻撃・防御・速度・命中のステータス表示")]
        [SerializeField] private TMP_Text statsText;
        [Tooltip("行動結果・行き先プレビュー・セーブ結果などのログ")]
        [SerializeField] private TMP_Text logText;

        [Header("Action Buttons")]
        [Tooltip("ターン結果確認後に次へ進むボタン")]
        [SerializeField] private LHButton continueButton;
        [Tooltip("育成完了後にタイトルへ戻るボタン")]
        [SerializeField] private LHButton backToTitleButton;
        [Tooltip("育成中に中断してタイトルへ戻るボタン")]
        [SerializeField] private LHButton interruptButton;

        [Header("Resume Window")]
        [Tooltip("育成再開確認用の大型ウィンドウ")]
        [SerializeField] private TrainingResumeWindowView resumeWindowView;

        [Header("Auto Result")]
        [Tooltip("育成完了リザルトウィンドウ")]
        [SerializeField] private TrainingAutoResultView autoResultView;

        [Header("Item Windows")]
        [Tooltip("売店のアイテム購入ウィンドウ")]
        [FormerlySerializedAs("itemListWindowView")]
        [SerializeField] private TrainingItemListWindowView shopWindowView;
        [Tooltip("所持アイテムの使用ウィンドウ")]
        [SerializeField] private TrainingItemListWindowView inventoryWindowView;

        [Header("Location Choice")]
        [Tooltip("行き先3択ボタン。インデックス0〜2に選択肢を割り当てる")]
        [SerializeField] private LHButton[] locationButtons;
        [Tooltip("行き先ボタンのラベル")]
        [SerializeField] private TMP_Text[] locationButtonLabels;
        [Tooltip("休憩を選ぶボタン。体力回復用")]
        [SerializeField] private LHButton restButton;
        [Tooltip("休憩ボタンのラベル")]
        [SerializeField] private TMP_Text restButtonLabel;

        [Header("Button Labels")]
        [SerializeField] private TMP_Text continueButtonLabel;
        [SerializeField] private TMP_Text backToTitleButtonLabel;

        [Header("Attack Swap")]
        [Tooltip("攻撃入れ替えパネル")]
        [FormerlySerializedAs("attackSwapGroup")]
        [SerializeField] private GameObject attackSwapPanel;
        [Tooltip("攻撃入れ替えUI。AttackSwapPanel配下")]
        [SerializeField] private TrainingAttackSwapChoicesView attackSwapChoicesView;

        [Header("Panels")]
        [Tooltip("曜日・時間・ターン表示パネル")]
        [FormerlySerializedAs("hudHeaderGroup")]
        [SerializeField] private GameObject hudHeaderPanel;
        [Tooltip("行動体力表示パネル")]
        [FormerlySerializedAs("movePowerGroup")]
        [SerializeField] private GameObject movePowerPanel;
        [Tooltip("所持金表示パネル")]
        [SerializeField] private GameObject moneyPanel;
        [Tooltip("ステータス表示パネル")]
        [FormerlySerializedAs("statusGroup")]
        [SerializeField] private GameObject statusPanel;
        [Tooltip("メッセージログパネル")]
        [FormerlySerializedAs("logGroup")]
        [SerializeField] private GameObject logPanel;
        [Tooltip("行き先選択ボタン群の親。未設定時は各ボタンを個別制御")]
        [SerializeField] private GameObject locationChoicePanelRoot;
        [Tooltip("体力ゲージのFill画像。fillAmountで残量を表示")]
        [SerializeField] private Image staminaFill;

        private TrainingHudLayoutMode layoutMode = TrainingHudLayoutMode.Hidden;

        private int locationChoiceStamina;
        private TrainingTurnChoice pendingTurnChoice;
        private TrainingCommandType pendingCommandChoice;
        private TrainingFocus pendingFocusChoice;
        private bool focusChoiceCancelled;
        private TrainingCommandType pendingFocusCommand;
        private int pendingShopChoice;
        private int pendingInventoryChoice;
        private ChoiceMode choiceMode = ChoiceMode.None;
        private bool hasChoice;
        private bool continuePressed;
        private int pendingSwapChoice = -1;
        private bool hasSwapChoice;
        private bool attackSwapRestoreTrainingLayout = true;
        private Sprite[] motivationFrames;
        private int motivationFrameIndex;
        private float motivationFrameTimer;
        private TrainingMotivation playingMotivation;
        private const float MotivationFrameSeconds = 0.12f;
        private const string CommandChoicePrompt = "この時間の行動を選んでください";
        private const string FocusChoicePrompt = "伸ばすステータスを選んでください";
        private const string ShopChoicePrompt = "買いたい商品を選んでください";
        private const string InventoryChoicePrompt = "使うアイテムを選んでください";
        private const string LocationChoicePrompt = "行き先を選んでください";

        private enum ChoiceMode
        {
            None,
            Command,
            Focus,
            Shop,
            Inventory,
            Location
        }

        private enum TrainingHudLayoutMode
        {
            Hidden,
            Training,
            AttackSwap,
            Resume,
            ItemList
        }

        private void Awake()
        {
            EnsureSerializedReferences();
            BindUi();
            Hide();
        }

        private void Update()
        {
            TickMotivationIconAnimation();
        }

        private void PlayMotivationIcon(TrainingMotivation motivation)
        {
            if (motivationIcon == null)
            {
                return;
            }

            bool changed = motivationFrames == null
                || motivationFrames.Length == 0
                || playingMotivation != motivation;
            if (changed)
            {
                playingMotivation = motivation;
                motivationFrames = TrainingMotivationCatalog.ResolveIconFrames(motivation);
                motivationFrameIndex = 0;
                motivationFrameTimer = 0f;
                Sprite first = motivationFrames != null && motivationFrames.Length > 0
                    ? motivationFrames[0]
                    : null;
                motivationIcon.sprite = first;
            }

            bool hasFrames = motivationFrames != null && motivationFrames.Length > 0;
            motivationIcon.enabled = hasFrames && motivationIcon.sprite != null;
            motivationIcon.gameObject.SetActive(true);
        }

        private void TickMotivationIconAnimation()
        {
            if (motivationIcon == null
                || !motivationIcon.isActiveAndEnabled
                || motivationFrames == null
                || motivationFrames.Length <= 1)
            {
                return;
            }

            motivationFrameTimer += Time.unscaledDeltaTime;
            if (motivationFrameTimer < MotivationFrameSeconds)
            {
                return;
            }

            motivationFrameTimer = 0f;
            motivationFrameIndex = (motivationFrameIndex + 1) % motivationFrames.Length;
            Sprite frame = motivationFrames[motivationFrameIndex];
            if (frame != null)
            {
                motivationIcon.sprite = frame;
            }
        }

        /// <inheritdoc/>
        public void Show()
        {
            SetHudRootVisible(true);
            HideAutoResultWindow();
            ApplyTrainingLayout();
        }

        /// <inheritdoc/>
        public void ShowOverlayHost()
        {
            SetHudRootVisible(true);
            HideLocationChoices();
            HideAttackSwapChoices();
            HideResumeChoices();
            HideAutoResultWindow();
            SetInterruptButtonVisible(false);
            if (continueButton != null)
            {
                continueButton.gameObject.SetActive(false);
            }

            ApplyResumeLayout();
        }

        /// <summary>
        /// モーダルウィンドウ表示用にCanvasだけ有効化する
        /// 育成完了リザルト表示前に使う
        /// </summary>
        public void ShowModalOverlayHost()
        {
            SetHudRootVisible(true);
            HideLocationChoices();
            HideAttackSwapChoices();
            HideResumeChoices();
            ClearOverlayMessage();
            SetInterruptButtonVisible(false);
            if (continueButton != null)
            {
                continueButton.gameObject.SetActive(false);
            }

            ApplyResumeLayout();
        }

        /// <inheritdoc/>
        public void ShowOverlayMessage(string message)
        {
            ShowOverlayHost();

            if (logText != null)
            {
                logText.text = message ?? string.Empty;
            }

            SetPanelVisible(logPanel, !string.IsNullOrWhiteSpace(message));
        }

        /// <inheritdoc/>
        public void ClearOverlayMessage()
        {
            if (logText != null)
            {
                logText.text = string.Empty;
            }

            SetPanelVisible(logPanel, false);
        }

        /// <inheritdoc/>
        public void Hide()
        {
            ClearOverlayMessage();
            layoutMode = TrainingHudLayoutMode.Hidden;
            HideLocationChoices();
            HideAttackSwapChoices();
            HideResumeChoices();
            HideAutoResultWindow();
            if (continueButton != null)
            {
                continueButton.gameObject.SetActive(false);
            }

            SetPanelVisible(hudHeaderPanel, false);
            SetPanelVisible(movePowerPanel, false);
            SetPanelVisible(moneyPanel, false);
            SetPanelVisible(statusPanel, false);
            SetPanelVisible(logPanel, false);
            SetLocationChoicePanelVisible(false);
            SetPanelVisible(attackSwapPanel, false);
            attackSwapChoicesView?.Clear();
            SetHudRootVisible(false);
        }

        /// <inheritdoc/>
        public void BindSession(TrainingSession session)
        {
            if (session == null)
            {
                return;
            }

            if (dayText != null)
            {
                dayText.text = TrainingDayCatalog.GetDisplayName(session.CurrentDay);
            }

            // periodTextとturnTextはBindSession(session,period,turnNumber)側が正
            // ここであんぶん表示を書くと訓練成功ログ表示時に時間割が壊れる

            if (moneyText != null)
            {
                moneyText.text = $"{session.Money}G";
            }

            if (staminaText != null)
            {
                staminaText.text = $"体力 {session.Stamina} / {TrainingSettings.MaxStamina}";
            }

            if (motivationText != null)
            {
                motivationText.text = "やる気　";
            }

            if (motivationIcon != null)
            {
                PlayMotivationIcon(session.Motivation);
            }

            if (statsText != null)
            {
                statsText.text = FormatStatsText(session.CurrentStatus);
            }

            if (staminaFill != null)
            {
                staminaFill.fillAmount = session.Stamina / (float)TrainingSettings.MaxStamina;
            }
        }

        /// <inheritdoc/>
        public void BindSession(TrainingSession session, TrainingPeriod period, int turnNumber)
        {
            BindSession(session);
            if (session == null)
            {
                return;
            }

            if (periodText != null)
            {
                periodText.text = TrainingPeriodCatalog.GetDisplayName(period);
            }

            if (turnText != null)
            {
                turnText.text = $"ターン {turnNumber}";
            }
        }

        /// <inheritdoc/>
        public void ShowCommandChoices(
            IReadOnlyList<TrainingCommandType> commands,
            int currentStamina)
        {
            ApplyTrainingLayout();
            HideLocationChoices();
            HideAttackSwapChoices();
            HideResumeChoices();
            locationChoiceStamina = currentStamina;
            choiceMode = ChoiceMode.Command;
            hasChoice = false;
            SetLogMessage(CommandChoicePrompt);
            if (locationButtons == null || commands == null)
            {
                return;
            }

            for (int i = 0; i < locationButtons.Length; i++)
            {
                LHButton button = locationButtons[i];
                if (button == null)
                {
                    continue;
                }

                bool visible = i < commands.Count;
                button.gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                TrainingCommandType command = commands[i];
                if (locationButtonLabels != null
                    && i < locationButtonLabels.Length
                    && locationButtonLabels[i] != null)
                {
                    locationButtonLabels[i].text =
                        TrainingCommandCatalog.GetDisplayName(command);
                }

                int captured = i;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => OnCommandClicked(commands[captured]));
                BindCommandHover(button, commands[captured]);
            }

            ShowRestButtonForCommand();
            SetLocationChoicePanelVisible(true);
        }

        /// <inheritdoc/>
        public void ShowShopChoices(
            IReadOnlyList<TrainingShopItem> items,
            bool hasNextPage,
            int currentMoney,
            bool showOpenInventory)
        {
            ApplyItemListLayout();
            HideAttackSwapChoices();
            HideResumeChoices();
            choiceMode = ChoiceMode.Shop;
            hasChoice = false;
            SetLogMessage($"{ShopChoicePrompt}\n所持金 {currentMoney}G");

            bool windowHandlesNext = shopWindowView != null
                && shopWindowView.HasNextPageButton;
            bool windowHandlesInventory = shopWindowView != null
                && shopWindowView.HasOpenInventoryButton;
            bool windowHandlesClose = shopWindowView != null
                && shopWindowView.HasCloseButton;
            bool windowHandlesItems = shopWindowView != null
                && shopWindowView.HasItemSlots;

            inventoryWindowView?.Hide();
            shopWindowView?.ShowShop(
                items,
                currentMoney,
                hasNextPage && windowHandlesNext,
                showOpenInventory && windowHandlesInventory);

            if (windowHandlesItems)
            {
                HideLocationChoiceButtonsOnly();
                if (restButton != null)
                {
                    restButton.gameObject.SetActive(!windowHandlesClose);
                    if (!windowHandlesClose)
                    {
                        LhButtonLabelUtility.SetLabel(restButtonLabel, "戻る");
                        restButton.onClick.RemoveAllListeners();
                        restButton.onClick.AddListener(
                            () => OnShopClicked(TrainingShopChoiceCodes.Back));
                        BindShopHover(restButton, "売店を離れる");
                    }
                }

                SetLocationChoicePanelVisible(!windowHandlesClose);
                return;
            }

            if (locationButtons == null)
            {
                return;
            }

            int itemCount = items != null ? items.Count : 0;
            bool showNextOnButton = hasNextPage && !windowHandlesNext;
            bool showInventoryOnButton = showOpenInventory && !windowHandlesInventory;
            int reservedSlots = (showNextOnButton ? 1 : 0) + (showInventoryOnButton ? 1 : 0);
            int visibleItemCount = itemCount;
            if (reservedSlots > 0 && visibleItemCount > locationButtons.Length - reservedSlots)
            {
                visibleItemCount = Mathf.Max(0, locationButtons.Length - reservedSlots);
            }

            int nextSlot = -1;
            int inventorySlot = -1;
            if (showNextOnButton)
            {
                nextSlot = visibleItemCount;
            }

            if (showInventoryOnButton)
            {
                inventorySlot = showNextOnButton ? visibleItemCount + 1 : visibleItemCount;
            }

            for (int i = 0; i < locationButtons.Length; i++)
            {
                LHButton button = locationButtons[i];
                if (button == null)
                {
                    continue;
                }

                if (i == nextSlot)
                {
                    button.gameObject.SetActive(true);
                    SetLocationButtonLabel(i, "次のページ");
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(
                        () => OnShopClicked(TrainingShopChoiceCodes.NextPage));
                    BindShopHover(button, "次の商品ページを表示する");
                    continue;
                }

                if (i == inventorySlot)
                {
                    button.gameObject.SetActive(true);
                    SetLocationButtonLabel(i, "所持アイテム");
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(
                        () => OnShopClicked(TrainingShopChoiceCodes.OpenInventory));
                    BindShopHover(button, "所持アイテムを使う");
                    continue;
                }

                bool hasItem = i < visibleItemCount && !string.IsNullOrEmpty(items[i].Id);
                button.gameObject.SetActive(hasItem);
                if (!hasItem)
                {
                    continue;
                }

                TrainingShopItem item = items[i];
                SetLocationButtonLabel(i, $"{item.DisplayName}\n{item.Price}G");
                int captured = i;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => OnShopClicked(captured));
                BindShopHover(
                    button,
                    $"{item.DisplayName}\n{item.Description}\n価格 {item.Price}G");
            }

            if (restButton != null)
            {
                restButton.gameObject.SetActive(!windowHandlesClose);
                if (!windowHandlesClose)
                {
                    LhButtonLabelUtility.SetLabel(restButtonLabel, "戻る");
                    restButton.onClick.RemoveAllListeners();
                    restButton.onClick.AddListener(
                        () => OnShopClicked(TrainingShopChoiceCodes.Back));
                    BindShopHover(restButton, "売店を離れる");
                }
            }

            SetLocationChoicePanelVisible(true);
        }

        /// <inheritdoc/>
        public void ShowInventoryChoices(
            IReadOnlyList<TrainingInventoryEntryView> entries,
            bool hasNextPage)
        {
            ApplyItemListLayout();
            HideAttackSwapChoices();
            HideResumeChoices();
            choiceMode = ChoiceMode.Inventory;
            hasChoice = false;
            SetLogMessage(InventoryChoicePrompt);

            bool windowHandlesNext = inventoryWindowView != null
                && inventoryWindowView.HasNextPageButton;
            bool windowHandlesClose = inventoryWindowView != null
                && inventoryWindowView.HasCloseButton;
            bool windowHandlesItems = inventoryWindowView != null
                && inventoryWindowView.HasItemSlots;

            shopWindowView?.Hide();
            inventoryWindowView?.ShowInventory(
                entries,
                hasNextPage && windowHandlesNext);

            if (windowHandlesItems)
            {
                HideLocationChoiceButtonsOnly();
                if (restButton != null)
                {
                    restButton.gameObject.SetActive(!windowHandlesClose);
                    if (!windowHandlesClose)
                    {
                        LhButtonLabelUtility.SetLabel(restButtonLabel, "戻る");
                        restButton.onClick.RemoveAllListeners();
                        restButton.onClick.AddListener(
                            () => OnInventoryClicked(TrainingInventoryChoiceCodes.Back));
                        BindInventoryHover(restButton, "所持一覧を閉じる");
                    }
                }

                SetLocationChoicePanelVisible(!windowHandlesClose);
                return;
            }

            if (locationButtons == null)
            {
                return;
            }

            int entryCount = entries != null ? entries.Count : 0;
            bool showNextOnButton = hasNextPage && !windowHandlesNext;
            int visibleEntryCount = entryCount;
            if (showNextOnButton && visibleEntryCount >= locationButtons.Length)
            {
                visibleEntryCount = locationButtons.Length - 1;
            }

            for (int i = 0; i < locationButtons.Length; i++)
            {
                LHButton button = locationButtons[i];
                if (button == null)
                {
                    continue;
                }

                if (showNextOnButton && i == visibleEntryCount)
                {
                    button.gameObject.SetActive(true);
                    SetLocationButtonLabel(i, "次のページ");
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(
                        () => OnInventoryClicked(TrainingInventoryChoiceCodes.NextPage));
                    BindInventoryHover(button, "次の所持ページを表示する");
                    continue;
                }

                bool visible = i < visibleEntryCount;
                button.gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                TrainingInventoryEntryView entry = entries[i];
                SetLocationButtonLabel(i, $"{entry.DisplayName}\nx{entry.Count}");
                int captured = i;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => OnInventoryClicked(captured));
                BindInventoryHover(
                    button,
                    $"{entry.DisplayName} x{entry.Count}\n{entry.Description}");
            }

            if (restButton != null)
            {
                restButton.gameObject.SetActive(!windowHandlesClose);
                if (!windowHandlesClose)
                {
                    LhButtonLabelUtility.SetLabel(restButtonLabel, "戻る");
                    restButton.onClick.RemoveAllListeners();
                    restButton.onClick.AddListener(
                        () => OnInventoryClicked(TrainingInventoryChoiceCodes.Back));
                    BindInventoryHover(restButton, "所持一覧を閉じる");
                }
            }

            SetLocationChoicePanelVisible(true);
        }

        /// <inheritdoc/>
        public void ShowFocusChoices(
            TrainingCommandType command,
            IReadOnlyList<TrainingFocus> focuses)
        {
            ApplyTrainingLayout();
            HideLocationChoices();
            HideAttackSwapChoices();
            HideResumeChoices();
            choiceMode = ChoiceMode.Focus;
            hasChoice = false;
            focusChoiceCancelled = false;
            pendingFocusCommand = command;
            SetLogMessage(FocusChoicePrompt);
            BindFocusChoices(focuses);
        }

        private void BindFocusChoices(IReadOnlyList<TrainingFocus> focuses)
        {
            if (locationButtons == null)
            {
                return;
            }

            int focusCount = focuses != null ? focuses.Count : 0;
            if (focusCount <= 0)
            {
                Debug.LogError(
                    "[TrainingHudView] 訓練主ステ候補が空です",
                    this);
            }

            if (locationButtons.Length < focusCount)
            {
                Debug.LogError(
                    $"[TrainingHudView] 訓練ボタンが不足しています必要数{focusCount} 現在{locationButtons.Length}"
                    + " LocationButtonをPrefabで追加しlocationButtonsへ配線してください",
                    this);
            }

            for (int i = 0; i < locationButtons.Length; i++)
            {
                LHButton button = locationButtons[i];
                if (button == null)
                {
                    continue;
                }

                button.onClick.RemoveAllListeners();
                if (i < focusCount)
                {
                    BindFocusButton(button, i, focuses[i], pendingFocusCommand);
                    continue;
                }

                button.gameObject.SetActive(false);
            }

            if (restButton != null)
            {
                restButton.gameObject.SetActive(true);
                LhButtonLabelUtility.SetLabel(restButtonLabel, "戻る");
                restButton.onClick.RemoveAllListeners();
                restButton.onClick.AddListener(OnFocusBackClicked);
                BindFocusNavHover(restButton, "行動選択へ戻る");
            }

            SetLocationChoicePanelVisible(true);
        }

        private void BindFocusButton(
            LHButton button,
            int labelIndex,
            TrainingFocus focus,
            TrainingCommandType command)
        {
            button.gameObject.SetActive(true);
            if (locationButtonLabels != null
                && labelIndex < locationButtonLabels.Length
                && locationButtonLabels[labelIndex] != null)
            {
                locationButtonLabels[labelIndex].text =
                    TrainingFocusCatalog.GetDisplayName(focus);
            }

            TrainingFocus captured = focus;
            button.onClick.AddListener(() => OnFocusClicked(captured));
            BindFocusHover(button, captured, command);
        }

        /// <inheritdoc/>
        public void ShowLocationChoices(TrainingLocation[] choices, int currentStamina)
        {
            ApplyTrainingLayout();
            HideLocationChoices();
            HideAttackSwapChoices();
            HideResumeChoices();
            locationChoiceStamina = currentStamina;
            choiceMode = ChoiceMode.Location;
            hasChoice = false;
            SetLogMessage(LocationChoicePrompt);
            if (locationButtons == null || choices == null)
            {
                return;
            }

            for (int i = 0; i < locationButtons.Length; i++)
            {
                LHButton button = locationButtons[i];
                if (button == null)
                {
                    continue;
                }

                bool visible = i < choices.Length;
                button.gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                TrainingLocation location = choices[i];
                if (locationButtonLabels != null
                    && i < locationButtonLabels.Length
                    && locationButtonLabels[i] != null)
                {
                    locationButtonLabels[i].text = TrainingLocationCatalog.GetDisplayName(location);
                }

                int captured = i;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => OnLocationClicked(choices[captured]));
                BindLocationHover(button, choices[captured]);
            }

            ShowRestButton();
            SetLocationChoicePanelVisible(true);
        }

        private void ShowRestButton()
        {
            if (restButton == null)
            {
                return;
            }

            restButton.gameObject.SetActive(true);
            LhButtonLabelUtility.SetLabel(restButtonLabel, "休憩");

            restButton.onClick.RemoveAllListeners();
            restButton.onClick.AddListener(OnRestClicked);
            BindRestHover(restButton);
        }

        private void ShowRestButtonForCommand()
        {
            if (restButton == null)
            {
                return;
            }

            restButton.gameObject.SetActive(true);
            LhButtonLabelUtility.SetLabel(restButtonLabel, "休憩");
            restButton.onClick.RemoveAllListeners();
            restButton.onClick.AddListener(() => OnCommandClicked(TrainingCommandType.Rest));
            BindCommandHover(restButton, TrainingCommandType.Rest);
        }

        /// <inheritdoc/>
        public void HideLocationChoices()
        {
            choiceMode = ChoiceMode.None;
            shopWindowView?.Hide();
            inventoryWindowView?.Hide();
            HideRestButton();
            HideLocationChoiceButtonsOnly();
            SetLocationChoicePanelVisible(false);
        }

        private void HideLocationChoiceButtonsOnly()
        {
            if (locationButtons == null)
            {
                return;
            }

            for (int i = 0; i < locationButtons.Length; i++)
            {
                if (locationButtons[i] != null)
                {
                    locationButtons[i].gameObject.SetActive(false);
                }
            }
        }

        private void SetLocationChoicePanelVisible(bool visible)
        {
            if (locationChoicePanelRoot == null)
            {
                return;
            }

            // SetUiVisibleは子を再アクティブ化するため親はSetActiveのみ使う
            locationChoicePanelRoot.SetActive(visible);
        }

        private void HideRestButton()
        {
            if (restButton != null)
            {
                restButton.gameObject.SetActive(false);
            }
        }

        /// <inheritdoc/>
        public void SetLogMessage(string message)
        {
            if (logText != null)
            {
                logText.text = message ?? string.Empty;
            }

            if (layoutMode != TrainingHudLayoutMode.Hidden
                && layoutMode != TrainingHudLayoutMode.Resume
                && layoutMode != TrainingHudLayoutMode.ItemList)
            {
                UpdateLogPanelVisibility();
            }
        }

        /// <inheritdoc/>
        public async UniTask<TrainingCommandType> WaitCommandChoiceAsync(
            CancellationToken cancellationToken)
        {
            hasChoice = false;
            await UniTask.WaitUntil(() => hasChoice, cancellationToken: cancellationToken);
            HideLocationChoices();
            return pendingCommandChoice;
        }

        /// <inheritdoc/>
        public async UniTask<int> WaitShopChoiceAsync(CancellationToken cancellationToken)
        {
            hasChoice = false;
            using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                if (shopWindowView != null)
                {
                    ForwardWindowChoiceAsync(
                        shopWindowView,
                        OnShopClicked,
                        linkedCts.Token).Forget();
                }

                await UniTask.WaitUntil(() => hasChoice, cancellationToken: cancellationToken);
                linkedCts.Cancel();
            }

            HideLocationChoices();
            return pendingShopChoice;
        }

        /// <inheritdoc/>
        public async UniTask<int> WaitInventoryChoiceAsync(CancellationToken cancellationToken)
        {
            hasChoice = false;
            using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                if (inventoryWindowView != null)
                {
                    ForwardWindowChoiceAsync(
                        inventoryWindowView,
                        OnInventoryClicked,
                        linkedCts.Token).Forget();
                }

                await UniTask.WaitUntil(() => hasChoice, cancellationToken: cancellationToken);
                linkedCts.Cancel();
            }

            HideLocationChoices();
            return pendingInventoryChoice;
        }

        private static async UniTaskVoid ForwardWindowChoiceAsync(
            TrainingItemListWindowView windowView,
            Action<int> onChoice,
            CancellationToken cancellationToken)
        {
            try
            {
                int choice = await windowView.WaitWindowActionAsync(cancellationToken);
                onChoice?.Invoke(choice);
            }
            catch (OperationCanceledException)
            {
            }
        }

        /// <inheritdoc/>
        public async UniTask<TrainingFocus?> WaitFocusChoiceAsync(
            CancellationToken cancellationToken)
        {
            hasChoice = false;
            focusChoiceCancelled = false;
            await UniTask.WaitUntil(() => hasChoice, cancellationToken: cancellationToken);
            HideLocationChoices();
            if (focusChoiceCancelled)
            {
                return null;
            }

            return pendingFocusChoice;
        }

        /// <inheritdoc/>
        public async UniTask<TrainingTurnChoice> WaitTurnChoiceAsync(CancellationToken cancellationToken)
        {
            hasChoice = false;
            await UniTask.WaitUntil(() => hasChoice, cancellationToken: cancellationToken);
            HideLocationChoices();
            HideAttackSwapChoices();
            HideResumeChoices();
            return pendingTurnChoice;
        }

        /// <inheritdoc/>
        public async UniTask WaitContinueAsync(CancellationToken cancellationToken)
        {
            if (continueButton == null)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(0.8f), cancellationToken: cancellationToken);
                return;
            }

            continuePressed = false;
            continueButton.gameObject.SetActive(true);
            continueButton.interactable = true;
            await UniTask.WaitUntil(() => continuePressed, cancellationToken: cancellationToken);
            continueButton.gameObject.SetActive(false);
        }

        /// <inheritdoc/>
        public void ShowAttackSwapChoices(
            MotionType newAttack,
            IReadOnlyList<MotionType> currentAttacks,
            bool showSessionPanels = true)
        {
            attackSwapRestoreTrainingLayout = showSessionPanels;
            ApplyAttackSwapLayout(showSessionPanels);
            HideLocationChoices();
            HideAttackSwapChoices();
            HideResumeChoices();
            if (attackSwapChoicesView == null || currentAttacks == null)
            {
                return;
            }

            attackSwapChoicesView.Show(newAttack, currentAttacks, OnAttackSwapClicked);
            if (attackSwapPanel != null)
            {
                attackSwapPanel.transform.SetAsLastSibling();
            }

            SetPanelVisible(attackSwapPanel, true);
        }

        /// <inheritdoc/>
        public void HideAttackSwapChoices()
        {
            attackSwapChoicesView?.Clear();
            SetPanelVisible(attackSwapPanel, false);
        }

        /// <inheritdoc/>
        public async UniTask<int> WaitAttackSwapChoiceAsync(CancellationToken cancellationToken)
        {
            hasSwapChoice = false;
            await UniTask.WaitUntil(() => hasSwapChoice, cancellationToken: cancellationToken);
            HideAttackSwapChoices();
            if (attackSwapRestoreTrainingLayout)
            {
                ApplyTrainingLayout();
            }
            else
            {
                ApplyResumeLayout();
            }

            return pendingSwapChoice;
        }

        /// <inheritdoc/>
        public void SetInterruptButtonVisible(bool visible)
        {
            if (interruptButton == null)
            {
                if (visible)
                {
                    Debug.LogError(
                        "[TrainingHudView] interruptButtonが未配線ですHierarchyで接続してください",
                        this);
                }

                return;
            }

            interruptButton.gameObject.SetActive(visible);
            interruptButton.interactable = visible;
        }

        /// <inheritdoc/>
        public IDisposable SubscribeInterruptClick(UnityAction action)
        {
            if (interruptButton == null)
            {
                Debug.LogError(
                    "[TrainingHudView] interruptButtonが未配線のため中断できません",
                    this);
                return new EmptyDisposable();
            }

            return interruptButton.SubscribeOnClick(action);
        }

        /// <inheritdoc/>
        public void ShowResumeChoices(TrainingResumeProgressPresentation presentation)
        {
            ApplyResumeLayout();
            HideLocationChoices();
            HideAttackSwapChoices();
            SetLegacyResumeButtonsVisible(false);
            TrainingResumeWindowView windowView = GetResumeWindowView();
            if (windowView == null)
            {
                Debug.LogError(
                    "[TrainingHudView] resumeWindowViewが未配線ですHierarchyで接続してください",
                    this);
                return;
            }

            windowView.Show(presentation);
        }

        /// <inheritdoc/>
        public void HideResumeChoices()
        {
            SetLegacyResumeButtonsVisible(false);
            GetResumeWindowView()?.Hide();
        }

        private void HideAutoResultWindow()
        {
            GetAutoResultView()?.Hide();
        }

        private TrainingAutoResultView GetAutoResultView()
        {
            return autoResultView;
        }

        /// <inheritdoc/>
        public async UniTask<TrainingResumeChoice> WaitResumeChoiceAsync(CancellationToken cancellationToken)
        {
            TrainingResumeWindowView windowView = GetResumeWindowView();
            if (windowView == null)
            {
                Debug.LogError(
                    "[TrainingHudView] resumeWindowViewが未配線のため再開選択できません",
                    this);
                return TrainingResumeChoice.Unavailable;
            }

            bool resume = await windowView.WaitChoiceAsync(cancellationToken);
            return resume ? TrainingResumeChoice.Continue : TrainingResumeChoice.Restart;
        }

        /// <inheritdoc/>
        public IDisposable SubscribeBackToTitleClick(UnityAction action)
        {
            if (backToTitleButton == null)
            {
                return new EmptyDisposable();
            }

            return backToTitleButton.SubscribeOnClick(action);
        }

        private sealed class EmptyDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }

        private void OnLocationClicked(TrainingLocation location)
        {
            pendingTurnChoice = TrainingTurnChoice.FromLocation(location);
            hasChoice = true;
        }

        private void OnCommandClicked(TrainingCommandType command)
        {
            pendingCommandChoice = command;
            hasChoice = true;
        }

        private void OnShopClicked(int choice)
        {
            pendingShopChoice = choice;
            hasChoice = true;
        }

        private void OnInventoryClicked(int choice)
        {
            pendingInventoryChoice = choice;
            hasChoice = true;
        }

        private void OnFocusClicked(TrainingFocus focus)
        {
            focusChoiceCancelled = false;
            pendingFocusChoice = focus;
            hasChoice = true;
        }

        private void OnFocusBackClicked()
        {
            focusChoiceCancelled = true;
            hasChoice = true;
        }

        private void OnRestClicked()
        {
            if (choiceMode == ChoiceMode.Command)
            {
                OnCommandClicked(TrainingCommandType.Rest);
                return;
            }

            if (choiceMode == ChoiceMode.Shop)
            {
                OnShopClicked(TrainingShopChoiceCodes.Back);
                return;
            }

            if (choiceMode == ChoiceMode.Inventory)
            {
                OnInventoryClicked(TrainingInventoryChoiceCodes.Back);
                return;
            }

            if (choiceMode == ChoiceMode.Focus)
            {
                OnFocusBackClicked();
                return;
            }

            pendingTurnChoice = TrainingTurnChoice.Rest();
            hasChoice = true;
        }

        private void OnContinueClicked()
        {
            continuePressed = true;
        }

        private void OnAttackSwapClicked(int slotIndex)
        {
            pendingSwapChoice = slotIndex;
            hasSwapChoice = true;
        }

        private void BindCommandHover(LHButton button, TrainingCommandType command)
        {
            if (button == null)
            {
                return;
            }

            EventTrigger trigger = button.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = button.gameObject.AddComponent<EventTrigger>();
            }

            trigger.triggers.Clear();
            string detail = command switch
            {
                TrainingCommandType.Train =>
                    FormatTrainCommandHoverDetail(
                        "訓練ごとに体力消費が異なる 成功/大成功でステ上昇"),
                TrainingCommandType.SpecialTrain =>
                    FormatTrainCommandHoverDetail(
                        "特訓ごとに体力消費が異なる 大幅にステ上昇"),
                TrainingCommandType.Rest =>
                    $"体力+{TrainingSettings.RestStaminaRecovery}"
                    + $"(大成功で+{TrainingSettings.RestGreatSuccessRecovery})",
                TrainingCommandType.Shop => "昼休みにだけ利用できる売店",
                TrainingCommandType.UseItem => "所持アイテムを使う(時間は消費しない)",
                TrainingCommandType.Tournament => "対戦に勝利すると賞金と体力回復",
                _ => TrainingCommandCatalog.GetDisplayName(command)
            };
            AddHoverEntry(trigger, EventTriggerType.PointerEnter, () => SetLogMessage(detail));
            AddHoverEntry(trigger, EventTriggerType.PointerExit, () => SetLogMessage(CommandChoicePrompt));
        }

        private string FormatTrainCommandHoverDetail(string baseDetail)
        {
            float failurePercent =
                TrainingActionResolver.ComputeFailurePercent(locationChoiceStamina);
            if (failurePercent <= 0f)
            {
                return baseDetail;
            }

            return $"{baseDetail}\n失敗率 {failurePercent:0.#}%";
        }

        private void BindFocusHover(
            LHButton button,
            TrainingFocus focus,
            TrainingCommandType command)
        {
            if (button == null)
            {
                return;
            }

            EventTrigger trigger = button.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = button.gameObject.AddComponent<EventTrigger>();
            }

            trigger.triggers.Clear();
            TrainingStatGain gain = TrainingFocusCatalog.GetBaseGain(focus);
            bool isSpecial = command == TrainingCommandType.SpecialTrain;
            float multiplier = isSpecial
                ? TrainingSettings.SpecialTrainSuccessMultiplier
                : 1f;
            int staminaCost = isSpecial
                ? TrainingFocusCatalog.GetSpecialTrainStaminaCost(focus)
                : TrainingFocusCatalog.GetTrainStaminaCost(focus);
            TrainingStatGain scaled = TrainingFocusCatalog.ScaleGain(gain, multiplier);
            float failurePercent =
                TrainingActionResolver.ComputeFailurePercent(locationChoiceStamina);
            string detail =
                $"{TrainingFocusCatalog.GetDisplayName(focus)}\n"
                + $"体力-{staminaCost}\n"
                + FormatFocusGainPreview(scaled);
            if (failurePercent > 0f)
            {
                detail += $"\n失敗率 {failurePercent:0.#}%";
            }

            AddHoverEntry(trigger, EventTriggerType.PointerEnter, () => SetLogMessage(detail));
            AddHoverEntry(trigger, EventTriggerType.PointerExit, () => SetLogMessage(FocusChoicePrompt));
        }

        private static string FormatFocusGainPreview(TrainingStatGain gain)
        {
            var parts = new List<string>(3);
            if (gain.Hp != 0)
            {
                parts.Add($"HP+{gain.Hp}");
            }

            if (gain.Attack != 0)
            {
                parts.Add($"攻撃+{gain.Attack}");
            }

            if (gain.Defense != 0)
            {
                parts.Add($"防御+{gain.Defense}");
            }

            if (gain.Speed != 0)
            {
                parts.Add($"速度+{gain.Speed}");
            }

            if (gain.Hit != 0)
            {
                parts.Add($"命中+{gain.Hit}");
            }

            return parts.Count > 0 ? string.Join(" ", parts) : "ステ上昇なし";
        }

        private void BindFocusNavHover(LHButton button, string detail)
        {
            if (button == null)
            {
                return;
            }

            EventTrigger trigger = button.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = button.gameObject.AddComponent<EventTrigger>();
            }

            trigger.triggers.Clear();
            AddHoverEntry(trigger, EventTriggerType.PointerEnter, () => SetLogMessage(detail));
            AddHoverEntry(trigger, EventTriggerType.PointerExit, () => SetLogMessage(FocusChoicePrompt));
        }

        private void BindShopHover(LHButton button, string detail)
        {
            if (button == null)
            {
                return;
            }

            EventTrigger trigger = button.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = button.gameObject.AddComponent<EventTrigger>();
            }

            trigger.triggers.Clear();
            AddHoverEntry(trigger, EventTriggerType.PointerEnter, () => SetLogMessage(detail));
            AddHoverEntry(trigger, EventTriggerType.PointerExit, () => SetLogMessage(ShopChoicePrompt));
        }

        private void BindInventoryHover(LHButton button, string detail)
        {
            if (button == null)
            {
                return;
            }

            EventTrigger trigger = button.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = button.gameObject.AddComponent<EventTrigger>();
            }

            trigger.triggers.Clear();
            AddHoverEntry(trigger, EventTriggerType.PointerEnter, () => SetLogMessage(detail));
            AddHoverEntry(
                trigger,
                EventTriggerType.PointerExit,
                () => SetLogMessage(InventoryChoicePrompt));
        }

        private void SetLocationButtonLabel(int index, string label)
        {
            if (locationButtonLabels == null
                || index < 0
                || index >= locationButtonLabels.Length
                || locationButtonLabels[index] == null)
            {
                return;
            }

            locationButtonLabels[index].text = label;
        }

        private void BindLocationHover(LHButton button, TrainingLocation location)
        {
            if (button == null)
            {
                return;
            }

            EventTrigger trigger = button.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = button.gameObject.AddComponent<EventTrigger>();
            }

            trigger.triggers.Clear();
            AddHoverEntry(trigger, EventTriggerType.PointerEnter, () =>
            {
                SetLogMessage(TrainingLocationPreviewResolver.FormatDisplayText(location, locationChoiceStamina));
            });
            AddHoverEntry(trigger, EventTriggerType.PointerExit, () => SetLogMessage(LocationChoicePrompt));
        }

        private void BindRestHover(LHButton button)
        {
            if (button == null)
            {
                return;
            }

            EventTrigger trigger = button.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = button.gameObject.AddComponent<EventTrigger>();
            }

            trigger.triggers.Clear();
            AddHoverEntry(trigger, EventTriggerType.PointerEnter, () =>
            {
                SetLogMessage(TrainingLocationPreviewResolver.FormatRestDisplayText());
            });
            AddHoverEntry(trigger, EventTriggerType.PointerExit, () => SetLogMessage(LocationChoicePrompt));
        }

        private static void AddHoverEntry(EventTrigger trigger, EventTriggerType type, UnityAction onHover)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => onHover.Invoke());
            trigger.triggers.Add(entry);
        }

        private void BindUi()
        {
            if (rootCanvas == null)
            {
                rootCanvas = GetComponent<Canvas>();
            }

            EnsureSerializedReferences();

            if (continueButton != null)
            {
                continueButton.onClick.RemoveListener(OnContinueClicked);
                continueButton.onClick.AddListener(OnContinueClicked);
                LhButtonLabelUtility.SetLabel(continueButtonLabel, "続ける");
            }

            if (backToTitleButton != null)
            {
                backToTitleButton.EnsureUiSoundFeedback();
                LhButtonLabelUtility.SetLabel(backToTitleButtonLabel, "タイトルへ戻る");

                backToTitleButton.gameObject.SetActive(false);
            }

            if (interruptButton != null)
            {
                interruptButton.EnsureUiSoundFeedback();
                interruptButton.gameObject.SetActive(false);
            }

            if (locationButtons != null)
            {
                for (int i = 0; i < locationButtons.Length; i++)
                {
                    LHButton button = locationButtons[i];
                    if (button == null)
                    {
                        continue;
                    }

                    button.EnsureUiSoundFeedback();
                }
            }

            if (restButton != null)
            {
                restButton.EnsureUiSoundFeedback();
                restButton.gameObject.SetActive(false);
            }

            attackSwapChoicesView?.Clear();
            SetPanelVisible(attackSwapPanel, false);
        }

        private void ApplyTrainingLayout()
        {
            layoutMode = TrainingHudLayoutMode.Training;
            SetPanelVisible(hudHeaderPanel, true);
            SetPanelVisible(movePowerPanel, true);
            SetPanelVisible(moneyPanel, true);
            SetPanelVisible(statusPanel, true);
            attackSwapChoicesView?.Clear();
            SetPanelVisible(attackSwapPanel, false);
            SetInterruptButtonVisible(true);
            UpdateLogPanelVisibility();
        }

        /// <summary>
        /// 売店・所持アイテム選択用レイアウト
        /// ステータスパネルは出さない
        /// </summary>
        private void ApplyItemListLayout()
        {
            layoutMode = TrainingHudLayoutMode.ItemList;
            SetPanelVisible(hudHeaderPanel, true);
            SetPanelVisible(movePowerPanel, true);
            SetPanelVisible(moneyPanel, true);
            SetPanelVisible(statusPanel, false);
            attackSwapChoicesView?.Clear();
            SetPanelVisible(attackSwapPanel, false);
            SetPanelVisible(logPanel, true);
            SetInterruptButtonVisible(true);
        }

        private void ApplyAttackSwapLayout(bool showSessionPanels)
        {
            layoutMode = TrainingHudLayoutMode.AttackSwap;
            SetPanelVisible(hudHeaderPanel, showSessionPanels);
            SetPanelVisible(movePowerPanel, showSessionPanels);
            SetPanelVisible(moneyPanel, showSessionPanels);
            SetPanelVisible(statusPanel, showSessionPanels);
            SetLocationChoicePanelVisible(false);
            SetPanelVisible(logPanel, false);
            SetInterruptButtonVisible(false);
        }

        private void ApplyResumeLayout()
        {
            layoutMode = TrainingHudLayoutMode.Resume;
            SetPanelVisible(hudHeaderPanel, false);
            SetPanelVisible(movePowerPanel, false);
            SetPanelVisible(moneyPanel, false);
            SetPanelVisible(statusPanel, false);
            SetLocationChoicePanelVisible(false);
            attackSwapChoicesView?.Clear();
            SetPanelVisible(attackSwapPanel, false);
            SetPanelVisible(logPanel, false);
            SetInterruptButtonVisible(false);
        }

        private TrainingResumeWindowView GetResumeWindowView()
        {
            return resumeWindowView;
        }

        private void SetLegacyResumeButtonsVisible(bool visible)
        {
            TrainingResumeWindowView windowView = GetResumeWindowView();
            Transform windowRoot = windowView != null ? windowView.transform : null;
            foreach (Transform child in transform)
            {
                if (child.name is not ("ResumeContinueButton" or "ResumeRestartButton"))
                {
                    continue;
                }

                if (windowRoot != null && child.IsChildOf(windowRoot))
                {
                    continue;
                }

                child.gameObject.SetActive(visible);
            }
        }

        private void UpdateLogPanelVisibility()
        {
            bool hasLog = logText != null && !string.IsNullOrWhiteSpace(logText.text);
            SetPanelVisible(logPanel, hasLog);
        }

        private void EnsureSerializedReferences()
        {
            EnsureRootCanvasReference();

            if (dayText == null
                || periodText == null
                || turnText == null
                || staminaText == null
                || motivationText == null
                || motivationIcon == null
                || moneyText == null
                || moneyPanel == null
                || statsText == null
                || continueButton == null
                || locationButtons == null
                || locationButtons.Length < TrainingSettings.OfferedTrainFocusCount
                || locationButtonLabels == null
                || locationButtonLabels.Length < TrainingSettings.OfferedTrainFocusCount
                || restButton == null)
            {
                Debug.LogError(
                    "[TrainingHudView] SerializeFieldが未配線ですHierarchy/Inspectorで手動接続してください"
                    + $" (訓練ボタンは{TrainingSettings.OfferedTrainFocusCount}個必要)",
                    this);
            }

            if (resumeWindowView == null)
            {
                Debug.LogError(
                    "[TrainingHudView] resumeWindowViewが未配線です",
                    this);
            }

            if (autoResultView == null)
            {
                Debug.LogError(
                    "[TrainingHudView] autoResultViewが未配線です",
                    this);
            }

            if (shopWindowView == null)
            {
                Debug.LogError(
                    "[TrainingHudView] shopWindowViewが未配線です",
                    this);
            }

            if (inventoryWindowView == null)
            {
                Debug.LogError(
                    "[TrainingHudView] inventoryWindowViewが未配線です",
                    this);
            }
        }

        private static void SetPanelVisible(GameObject panel, bool visible)
        {
            CanvasVisibilityUtility.SetPanelActive(panel, visible);
        }

        private void EnsureRootCanvasReference()
        {
            if (rootCanvas == null)
            {
                rootCanvas = GetComponent<Canvas>();
            }
        }

        private void SetHudRootVisible(bool visible)
        {
            EnsureRootCanvasReference();
            CanvasVisibilityUtility.SetCanvasEnabled(rootCanvas, visible);
        }

        private static string FormatStatsText(ModelStatus status)
        {
            if (status == null)
            {
                return string.Empty;
            }

            return ModelSaveSummaryFormatter.FormatTrainingStatusParameters(status);
        }
    }
}
