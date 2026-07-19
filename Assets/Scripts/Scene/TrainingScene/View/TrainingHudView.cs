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
        private const string LocationChoicePrompt = "行き先または休憩を選んでください";

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
        [Tooltip("HP・攻撃・防御・速度のステータス表示")]
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
        private bool hasChoice;
        private bool continuePressed;
        private int pendingSwapChoice = -1;
        private bool hasSwapChoice;

        private enum TrainingHudLayoutMode
        {
            Hidden,
            Training,
            AttackSwap,
            Resume
        }

        private void Awake()
        {
            EnsureSerializedReferences();
            BindUi();
            Hide();
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
            SetPanelVisible(statusPanel, false);
            SetPanelVisible(logPanel, false);
            SetPanelVisible(locationChoicePanelRoot, false);
            SetPanelVisible(attackSwapPanel, false);
            attackSwapChoicesView?.Clear();
            SetHudRootVisible(false);
        }

        /// <inheritdoc/>
        public void BindSession(TrainingSession session, TrainingPeriod period, int turnNumber)
        {
            if (session == null)
            {
                return;
            }

            if (dayText != null)
            {
                dayText.text = TrainingDayCatalog.GetDisplayName(session.CurrentDay);
            }

            if (periodText != null)
            {
                periodText.text = TrainingPeriodCatalog.GetDisplayName(period);
            }

            if (turnText != null)
            {
                turnText.text = $"ターン {turnNumber}/{TrainingDailySchedule.TurnsPerDay}";
            }

            if (staminaText != null)
            {
                staminaText.text = $"体力 {session.Stamina} / {TrainingSettings.MaxStamina}";
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
        public void ShowLocationChoices(TrainingLocation[] choices, int currentStamina)
        {
            ApplyTrainingLayout();
            HideLocationChoices();
            HideAttackSwapChoices();
            HideResumeChoices();
            locationChoiceStamina = currentStamina;
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
            if (UsesGroupedLocationChoicePanel())
            {
                SetPanelVisible(locationChoicePanelRoot, true);
            }
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

        /// <inheritdoc/>
        public void HideLocationChoices()
        {
            HideRestButton();
            if (UsesGroupedLocationChoicePanel())
            {
                SetPanelVisible(locationChoicePanelRoot, false);
                return;
            }

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
                && layoutMode != TrainingHudLayoutMode.Resume)
            {
                UpdateLogPanelVisibility();
            }
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
        public void ShowAttackSwapChoices(MotionType newAttack, IReadOnlyList<MotionType> currentAttacks)
        {
            ApplyAttackSwapLayout();
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
            ApplyTrainingLayout();
            return pendingSwapChoice;
        }

        /// <inheritdoc/>
        public void SetInterruptButtonVisible(bool visible)
        {
            if (interruptButton != null)
            {
                interruptButton.gameObject.SetActive(visible);
                interruptButton.interactable = visible;
            }
        }

        /// <inheritdoc/>
        public IDisposable SubscribeInterruptClick(UnityAction action)
        {
            if (interruptButton == null)
            {
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
            GetResumeWindowView()?.Show(presentation);
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
        public async UniTask<bool> WaitResumeChoiceAsync(CancellationToken cancellationToken)
        {
            TrainingResumeWindowView windowView = GetResumeWindowView();
            if (windowView == null)
            {
                return false;
            }

            return await windowView.WaitChoiceAsync(cancellationToken);
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

        private void OnRestClicked()
        {
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
            SetPanelVisible(statusPanel, true);
            attackSwapChoicesView?.Clear();
            SetPanelVisible(attackSwapPanel, false);
            SetInterruptButtonVisible(true);
            UpdateLogPanelVisibility();
        }

        private void ApplyAttackSwapLayout()
        {
            layoutMode = TrainingHudLayoutMode.AttackSwap;
            SetPanelVisible(hudHeaderPanel, true);
            SetPanelVisible(movePowerPanel, true);
            SetPanelVisible(statusPanel, true);
            SetPanelVisible(locationChoicePanelRoot, false);
            SetPanelVisible(logPanel, false);
            SetInterruptButtonVisible(false);
        }

        private void ApplyResumeLayout()
        {
            layoutMode = TrainingHudLayoutMode.Resume;
            SetPanelVisible(hudHeaderPanel, false);
            SetPanelVisible(movePowerPanel, false);
            SetPanelVisible(statusPanel, false);
            SetPanelVisible(locationChoicePanelRoot, false);
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
                || statsText == null
                || continueButton == null
                || locationButtons == null
                || locationButtons.Length < 3
                || restButton == null)
            {
                Debug.LogError(
                    "[TrainingHudView] SerializeFieldが未配線ですTools/ClayMonsters/Wire Training Scene Referencesを実行してください",
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
        }

        private bool UsesGroupedLocationChoicePanel()
        {
            return locationChoicePanelRoot != null;
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

            return ModelSaveSummaryFormatter.FormatStatusParameters(status);
        }
    }
}
