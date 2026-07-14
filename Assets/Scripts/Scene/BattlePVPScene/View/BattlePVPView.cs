using Extensions;
using LighthouseExtends.UIComponent.Button;
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
    public sealed class BattlePVPView : MonoBehaviour, IBattlePVPView
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
            if (statusText != null)
            {
                statusText.text = message ?? string.Empty;
            }
        }

        /// <inheritdoc/>
        public void SetJoinCodeText(string joinCode)
        {
            if (directJoinCodeText != null)
            {
                directJoinCodeText.text = string.IsNullOrEmpty(joinCode)
                    ? string.Empty
                    : $"参加コード: {joinCode}";
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
