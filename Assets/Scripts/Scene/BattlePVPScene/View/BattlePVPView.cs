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
        private bool sceneLabelOriginalsCaptured;
        private string returnOriginal = "タイトルへ戻る";
        private string directMatchOriginal = "特定の相手と対戦";
        private string randomMatchOriginal = "不特定の相手と対戦";
        private string createRoomOriginal = "ルームを作成";
        private string joinRoomOriginal = "ルームに参加";
        private string startMatchOriginal = "マッチング開始";
        private string cancelMatchOriginal = "キャンセル";
        private string copyCodeOriginal = "コードをコピー";
        private string directBackOriginal = "戻る";
        private string randomBackOriginal = "戻る";
        private string lobbyTitleOriginal = "通信対戦";
        private string joinPlaceholderOriginal = string.Empty;

        private void Awake()
        {
            ValidateRequiredLabels();
            CaptureSceneLabelOriginals();
            ApplyLocalizedLabels();
        }

        private void OnEnable()
        {
            CaptureSceneLabelOriginals();
            ApplyLocalizedLabels();
        }

        private void ValidateRequiredLabels()
        {
            if (lobbyTitleText == null)
            {
                Debug.LogError(
                    "[BattlePVPView] lobbyTitleTextが未配線ですPvpLobbyHostで接続してください",
                    this);
            }
        }

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

        private void CaptureSceneLabelOriginals()
        {
            if (sceneLabelOriginalsCaptured)
            {
                return;
            }

            returnOriginal = SceneLocalizedLabel.Capture(returnButton, returnOriginal);
            directMatchOriginal = SceneLocalizedLabel.Capture(directMatchButton, directMatchOriginal);
            randomMatchOriginal = SceneLocalizedLabel.Capture(randomMatchButton, randomMatchOriginal);
            createRoomOriginal = SceneLocalizedLabel.Capture(createRoomButton, createRoomOriginal);
            joinRoomOriginal = SceneLocalizedLabel.Capture(joinRoomButton, joinRoomOriginal);
            startMatchOriginal = SceneLocalizedLabel.Capture(startRandomMatchButton, startMatchOriginal);
            cancelMatchOriginal = SceneLocalizedLabel.Capture(cancelMatchButton, cancelMatchOriginal);
            copyCodeOriginal = SceneLocalizedLabel.Capture(copyJoinCodeButton, copyCodeOriginal);
            directBackOriginal = SceneLocalizedLabel.Capture(directBackButton, directBackOriginal);
            randomBackOriginal = SceneLocalizedLabel.Capture(randomBackButton, randomBackOriginal);
            lobbyTitleOriginal = SceneLocalizedLabel.Capture(lobbyTitleText, lobbyTitleOriginal);
            if (joinCodeInput != null && joinCodeInput.placeholder is TMP_Text placeholder)
            {
                joinPlaceholderOriginal = SceneLocalizedLabel.Capture(placeholder, joinPlaceholderOriginal);
            }

            sceneLabelOriginalsCaptured = true;
        }

        public void ApplyLocalizedLabels()
        {
            CaptureSceneLabelOriginals();
            if (lobbyTitleText != null)
            {
                LocalizedFont.SetText(
                    lobbyTitleText,
                    SceneLocalizedLabel.Resolve(GameTextKeys.BattlePvpLobbyTitle, lobbyTitleOriginal));
            }

            LhButtonLabelUtility.SetLabel(
                returnButton,
                SceneLocalizedLabel.Resolve(GameTextKeys.TrainingHudBackToTitle, returnOriginal));
            LhButtonLabelUtility.SetLabel(
                directMatchButton,
                SceneLocalizedLabel.Resolve(GameTextKeys.BattlePvpDirectMatch, directMatchOriginal));
            LhButtonLabelUtility.SetLabel(
                randomMatchButton,
                SceneLocalizedLabel.Resolve(GameTextKeys.BattlePvpRandomMatch, randomMatchOriginal));
            LhButtonLabelUtility.SetLabel(
                createRoomButton,
                SceneLocalizedLabel.Resolve(GameTextKeys.BattlePvpCreateRoom, createRoomOriginal));
            LhButtonLabelUtility.SetLabel(
                joinRoomButton,
                SceneLocalizedLabel.Resolve(GameTextKeys.BattlePvpJoinRoom, joinRoomOriginal));
            LhButtonLabelUtility.SetLabel(
                startRandomMatchButton,
                SceneLocalizedLabel.Resolve(GameTextKeys.BattlePvpStartMatch, startMatchOriginal));
            LhButtonLabelUtility.SetLabel(
                cancelMatchButton,
                SceneLocalizedLabel.Resolve(GameTextKeys.CommonCancel, cancelMatchOriginal));
            LhButtonLabelUtility.SetLabel(
                copyJoinCodeButton,
                SceneLocalizedLabel.Resolve(GameTextKeys.BattlePvpCopyCode, copyCodeOriginal));
            LhButtonLabelUtility.SetLabel(
                directBackButton,
                SceneLocalizedLabel.Resolve(GameTextKeys.CommonReturn, directBackOriginal));
            LhButtonLabelUtility.SetLabel(
                randomBackButton,
                SceneLocalizedLabel.Resolve(GameTextKeys.CommonReturn, randomBackOriginal));

            if (joinCodeInput != null && joinCodeInput.placeholder is TMP_Text placeholder)
            {
                LocalizedFont.SetText(
                    placeholder,
                    SceneLocalizedLabel.Resolve(
                        GameTextKeys.BattlePvpJoinCodePlaceholder,
                        string.IsNullOrEmpty(joinPlaceholderOriginal)
                            ? "参加コードを入力"
                            : joinPlaceholderOriginal));
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
