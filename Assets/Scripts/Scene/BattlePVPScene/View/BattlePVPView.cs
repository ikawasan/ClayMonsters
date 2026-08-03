using Extensions;
using LighthouseExtends.UIComponent.Button;
using Localization;
using Scene.BattlePVPScene.Interface;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace Scene.BattlePVPScene.View
{
    /// <summary>
    /// BattlePVPシーンのマッチングUIと戻る操作を表示する
    /// </summary>
    public sealed class BattlePVPView : MonoBehaviour, IBattlePVPView, ILanguageAwareUi
    {
        [Header("共通")]
        [SerializeField] private LHButton returnButton;

        [Header("パネル")]
        [SerializeField] private GameObject modeSelectPanel;
        [SerializeField] private GameObject directMatchPanel;
        [SerializeField] private GameObject randomMatchPanel;
        [SerializeField] private GameObject matchingPanel;

        [Header("モード選択")]
        [SerializeField] private LHButton directMatchButton;
        [SerializeField] private LHButton randomMatchButton;

        [Header("特定対戦")]
        [SerializeField] private LHButton createRoomButton;
        [SerializeField] private LHButton joinRoomButton;
        [SerializeField] private LHButton directBackButton;
        [SerializeField] private TMP_InputField joinCodeInput;
        [SerializeField] private TMP_Text directJoinCodeText;

        [Header("不特定対戦")]
        [SerializeField] private LHButton startRandomMatchButton;
        [SerializeField] private LHButton randomBackButton;

        [Header("マッチング中")]
        [SerializeField] private LHButton cancelMatchButton;
        [SerializeField] private LHButton copyJoinCodeButton;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text lobbyTitleText;

        private string cachedJoinCode = string.Empty;
        private string cachedStatusMessage = string.Empty;
        private bool hasCachedStatus;
        private bool statusWasConnecting;
        private bool statusWasRoomCodeCopied;

        private void Awake()
        {
            ApplyLocalizedLabels();
        }

        private void OnEnable()
        {
            ApplyLocalizedLabels();
        }

        /// <summary>
        /// ボタンと固定ラベルを現在言語で更新する
        /// </summary>

        /// <inheritdoc/>
        public void RefreshLocalizedUi()
        {
            ApplyLocalizedLabels();
            if (!string.IsNullOrEmpty(cachedJoinCode))
            {
                SetJoinCodeText(cachedJoinCode);
            }

            if (statusWasConnecting)
            {
                SetStatusText(
                    LocalizedText.GetOrFallback(GameTextKeys.BattlePvpConnecting, "接続中…"));
            }
            else if (statusWasRoomCodeCopied)
            {
                SetStatusText(
                    LocalizedText.GetOrFallback(
                        GameTextKeys.BattlePvpRoomCodeCopied,
                        "参加コードをコピーしました"));
            }
            else if (hasCachedStatus)
            {
                SetStatusText(cachedStatusMessage);
            }
        }

        public void ApplyLocalizedLabels()
        {
            if (lobbyTitleText != null)
            {
                LocalizedFont.SetText(
                    lobbyTitleText,
                    LocalizedText.GetOrFallback(GameTextKeys.BattlePvpLobbyTitle, "通信対戦"));
            }

            LhButtonLabelUtility.SetLabel(
                returnButton,
                LocalizedText.GetOrFallback(GameTextKeys.TrainingHudBackToTitle, "タイトルへ戻る"));
            LhButtonLabelUtility.SetLabel(
                directMatchButton,
                LocalizedText.GetOrFallback(GameTextKeys.BattlePvpDirectMatch, "特定の相手と対戦"));
            LhButtonLabelUtility.SetLabel(
                randomMatchButton,
                LocalizedText.GetOrFallback(GameTextKeys.BattlePvpRandomMatch, "不特定の相手と対戦"));
            LhButtonLabelUtility.SetLabel(
                createRoomButton,
                LocalizedText.GetOrFallback(GameTextKeys.BattlePvpCreateRoom, "ルームを作成"));
            LhButtonLabelUtility.SetLabel(
                joinRoomButton,
                LocalizedText.GetOrFallback(GameTextKeys.BattlePvpJoinRoom, "ルームに参加"));
            LhButtonLabelUtility.SetLabel(
                startRandomMatchButton,
                LocalizedText.GetOrFallback(GameTextKeys.BattlePvpStartMatch, "マッチング開始"));
            LhButtonLabelUtility.SetLabel(
                cancelMatchButton,
                LocalizedText.GetOrFallback(GameTextKeys.CommonCancel, "キャンセル"));
            LhButtonLabelUtility.SetLabel(
                copyJoinCodeButton,
                LocalizedText.GetOrFallback(GameTextKeys.BattlePvpCopyCode, "コードをコピー"));
            LhButtonLabelUtility.SetLabel(
                directBackButton,
                LocalizedText.GetOrFallback(GameTextKeys.CommonReturn, "戻る"));
            LhButtonLabelUtility.SetLabel(
                randomBackButton,
                LocalizedText.GetOrFallback(GameTextKeys.CommonReturn, "戻る"));

            if (joinCodeInput != null && joinCodeInput.placeholder is TMP_Text placeholder)
            {
                LocalizedFont.SetText(
                    placeholder,
                    LocalizedText.GetOrFallback(
                        GameTextKeys.BattlePvpJoinCodePlaceholder,
                        "参加コードを入力"));
            }
        }

        /// <inheritdoc/>
        IDisposable IBattlePVPView.SubscribeReturnButtonClick(UnityAction action) =>
            returnButton.SubscribeOnClick(action);

        /// <inheritdoc/>
        IDisposable IBattlePVPView.SubscribeDirectMatchButtonClick(UnityAction action) =>
            directMatchButton.SubscribeOnClick(action);

        /// <inheritdoc/>
        IDisposable IBattlePVPView.SubscribeRandomMatchButtonClick(UnityAction action) =>
            randomMatchButton.SubscribeOnClick(action);

        /// <inheritdoc/>
        IDisposable IBattlePVPView.SubscribeCreateRoomButtonClick(UnityAction action) =>
            createRoomButton.SubscribeOnClick(action);

        /// <inheritdoc/>
        IDisposable IBattlePVPView.SubscribeJoinRoomButtonClick(UnityAction action) =>
            joinRoomButton.SubscribeOnClick(action);

        /// <inheritdoc/>
        IDisposable IBattlePVPView.SubscribeStartRandomMatchButtonClick(UnityAction action) =>
            startRandomMatchButton.SubscribeOnClick(action);

        /// <inheritdoc/>
        IDisposable IBattlePVPView.SubscribeCancelMatchButtonClick(UnityAction action) =>
            cancelMatchButton.SubscribeOnClick(action);

        /// <inheritdoc/>
        IDisposable IBattlePVPView.SubscribeMatchBackButtonClick(UnityAction action)
        {
            IDisposable direct = directBackButton != null
                ? directBackButton.SubscribeOnClick(action)
                : null;
            IDisposable random = randomBackButton != null
                ? randomBackButton.SubscribeOnClick(action)
                : null;
            return new CompositeDisposable(direct, random);
        }

        /// <inheritdoc/>
        IDisposable IBattlePVPView.SubscribeCopyJoinCodeButtonClick(UnityAction action) =>
            copyJoinCodeButton != null ? copyJoinCodeButton.SubscribeOnClick(action) : null;

        /// <inheritdoc/>
        public void SetCopyJoinCodeButtonVisible(bool isVisible)
        {
            if (copyJoinCodeButton != null)
            {
                copyJoinCodeButton.gameObject.SetActive(isVisible);
            }
        }

        /// <inheritdoc/>
        public string GetJoinCodeInput() =>
            joinCodeInput != null ? joinCodeInput.text : string.Empty;

        /// <inheritdoc/>
        public void ShowPanel(BattlePvpUiPanel panel)
        {
            SetActive(modeSelectPanel, panel == BattlePvpUiPanel.ModeSelect);
            SetActive(directMatchPanel, panel == BattlePvpUiPanel.DirectMatch);
            SetActive(randomMatchPanel, panel == BattlePvpUiPanel.RandomMatch);
            SetActive(matchingPanel, panel == BattlePvpUiPanel.Matching);
        }

        /// <inheritdoc/>
        public void SetStatusText(string message)
        {
            hasCachedStatus = true;
            cachedStatusMessage = message ?? string.Empty;
            statusWasConnecting = string.Equals(
                cachedStatusMessage,
                LocalizedText.GetOrFallback(GameTextKeys.BattlePvpConnecting, "接続中…"),
                StringComparison.Ordinal)
                || string.Equals(cachedStatusMessage, "接続中…", StringComparison.Ordinal);
            statusWasRoomCodeCopied = string.Equals(
                cachedStatusMessage,
                LocalizedText.GetOrFallback(
                    GameTextKeys.BattlePvpRoomCodeCopied,
                    "参加コードをコピーしました"),
                StringComparison.Ordinal)
                || string.Equals(
                    cachedStatusMessage,
                    "参加コードをコピーしました",
                    StringComparison.Ordinal);

            if (statusText != null)
            {
                statusText.text = cachedStatusMessage;
            }
        }

        /// <inheritdoc/>
        public void SetJoinCodeText(string joinCode)
        {
            cachedJoinCode = joinCode ?? string.Empty;
            if (directJoinCodeText != null)
            {
                directJoinCodeText.text = string.IsNullOrEmpty(joinCode)
                    ? string.Empty
                    : LocalizedText.GetOrFallback(
                        GameTextKeys.BattlePvpJoinCodeLabel,
                        "参加コード: {code}",
                        "code",
                        joinCode);
            }
        }

        private static void SetActive(GameObject target, bool isActive)
        {
            if (target != null)
            {
                target.SetActive(isActive);
            }
        }

        private sealed class CompositeDisposable : IDisposable
        {
            private readonly IDisposable first;
            private readonly IDisposable second;

            public CompositeDisposable(IDisposable first, IDisposable second)
            {
                this.first = first;
                this.second = second;
            }

            public void Dispose()
            {
                first?.Dispose();
                second?.Dispose();
            }
        }
    }
}
