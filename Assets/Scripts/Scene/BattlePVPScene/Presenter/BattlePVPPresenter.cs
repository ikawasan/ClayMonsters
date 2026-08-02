using Cysharp.Threading.Tasks;
using Localization;
using Scene.BattlePVPScene.Interface;
using Scene.BattlePVPScene.Interface;
using Scene.Core.Interface;
using System;
using System.Threading;
using Unity.Netcode;
using UnityEngine;
using VContainer;

namespace Scene.BattlePVPScene.Presenter
{
    /// <summary>
    /// BattlePVPシーンのマッチングUIと戦闘開始を制御する
    /// </summary>
    public sealed class BattlePVPPresenter : IBattlePVPPresenter, IDisposable
    {
        private readonly IClayMonsterSceneManager sceneManager;
        private readonly IBattlePVPView view;
        private readonly IBattlePvpMatchmakingService matchmakingService;
        private readonly IBattlePvpFlowStarter flowStarter;

        private CancellationTokenSource matchCts;
        private bool isMatching;
        private bool isRoomCreation;

        [Inject]
        public BattlePVPPresenter(
            IClayMonsterSceneManager sceneManager,
            IBattlePVPView view,
            IBattlePvpMatchmakingService matchmakingService,
            IBattlePvpFlowStarter flowStarter)
        {
            this.sceneManager = sceneManager;
            this.view = view;
            this.matchmakingService = matchmakingService;
            this.flowStarter = flowStarter;
        }

        void IBattlePVPPresenter.Setup()
        {
            matchmakingService.StatusChanged += OnMatchmakingStatusChanged;
            view.ShowPanel(BattlePvpUiPanel.ModeSelect);
            view.SetStatusText(string.Empty);
            view.SetJoinCodeText(string.Empty);

            view.SubscribeReturnButtonClick(OnClickReturnButton);
            view.SubscribeDirectMatchButtonClick(OnClickDirectMatchButton);
            view.SubscribeRandomMatchButtonClick(OnClickRandomMatchButton);
            view.SubscribeCreateRoomButtonClick(OnClickCreateRoomButton);
            view.SubscribeJoinRoomButtonClick(OnClickJoinRoomButton);
            view.SubscribeStartRandomMatchButtonClick(OnClickStartRandomMatchButton);
            view.SubscribeCancelMatchButtonClick(OnClickCancelMatchButton);
            view.SubscribeMatchBackButtonClick(OnClickMatchBackButton);
            view.SubscribeCopyJoinCodeButtonClick(OnClickCopyJoinCodeButton);
            view.SetCopyJoinCodeButtonVisible(false);
        }

        void IDisposable.Dispose()
        {
            matchmakingService.StatusChanged -= OnMatchmakingStatusChanged;
            CancelMatching();
        }

        private void OnClickReturnButton()
        {
            if (sceneManager.IsTransition)
            {
                return;
            }

            CancelMatching();
            sceneManager.BackScene().Forget();
        }

        private void OnClickDirectMatchButton()
        {
            view.ShowPanel(BattlePvpUiPanel.DirectMatch);
            view.SetJoinCodeText(string.Empty);
        }

        private void OnClickRandomMatchButton()
        {
            view.ShowPanel(BattlePvpUiPanel.RandomMatch);
        }

        private void OnClickMatchBackButton()
        {
            isRoomCreation = false;
            CancelMatching();
            view.SetCopyJoinCodeButtonVisible(false);
            view.ShowPanel(BattlePvpUiPanel.ModeSelect);
            view.SetStatusText(string.Empty);
        }

        private void OnClickCreateRoomButton()
        {
            isRoomCreation = true;
            BeginMatching(token => matchmakingService.CreateDirectRoomAsync(token));
        }

        private void OnClickJoinRoomButton()
        {
            isRoomCreation = false;
            BeginMatching(token =>
            {
                string joinCode = view.GetJoinCodeInput();
                return matchmakingService.JoinDirectRoomAsync(joinCode, token);
            });
        }

        private void OnClickStartRandomMatchButton()
        {
            isRoomCreation = false;
            BeginMatching(token => matchmakingService.StartRandomMatchAsync(token));
        }

        private void OnClickCopyJoinCodeButton()
        {
            string roomCode = matchmakingService.CurrentRoomCode;
            if (string.IsNullOrEmpty(roomCode))
            {
                return;
            }

            GUIUtility.systemCopyBuffer = roomCode;
            view.SetStatusText(
                LocalizedText.GetOrFallback(
                    GameTextKeys.BattlePvpRoomCodeCopied,
                    "参加コード: {code}\nコピーしました",
                    "code",
                    roomCode));
        }

        private void OnClickCancelMatchButton()
        {
            isRoomCreation = false;
            CancelMatching();
            view.SetCopyJoinCodeButtonVisible(false);
            view.ShowPanel(BattlePvpUiPanel.ModeSelect);
            view.SetStatusText(string.Empty);
            view.SetJoinCodeText(string.Empty);
        }

        private void BeginMatching(Func<CancellationToken, UniTask<BattlePvpMatchmakingResult>> matchTaskFactory)
        {
            if (isMatching)
            {
                return;
            }

            CancelMatching();
            isMatching = true;
            matchCts = new CancellationTokenSource();
            view.ShowPanel(BattlePvpUiPanel.Matching);
            view.SetCopyJoinCodeButtonVisible(false);
            view.SetStatusText(LocalizedText.GetOrFallback(GameTextKeys.BattlePvpConnecting, "接続中…"));
            RunMatchingAsync(matchTaskFactory, matchCts.Token).Forget();
        }

        private async UniTaskVoid RunMatchingAsync(
            Func<CancellationToken, UniTask<BattlePvpMatchmakingResult>> matchTaskFactory,
            CancellationToken cancellationToken)
        {
            try
            {
                BattlePvpMatchmakingResult result = await matchTaskFactory(cancellationToken);
                if (cancellationToken.IsCancellationRequested)
                {
                    Debug.LogWarning("[BattlePVP] マッチングがキャンセルされました");
                    ResetMatchingUi();
                    return;
                }

                if (!result.IsSuccess)
                {
                    Debug.LogWarning($"[BattlePVP] マッチング失敗 message={result.ErrorMessage}");
                    view.SetStatusText(result.ErrorMessage);
                    await UniTask.Delay(TimeSpan.FromSeconds(1.2f), cancellationToken: cancellationToken);
                    ResetMatchingUi();
                    return;
                }

                view.SetCopyJoinCodeButtonVisible(false);
                view.SetJoinCodeText(result.RoomCode);

                if (flowStarter == null)
                {
                    Debug.LogError("[BattlePVP] IBattlePvpFlowStarterが見つかりません");
                    ResetMatchingUi();
                    return;
                }

                NetworkManager networkManager = NetworkManager.Singleton;
                Debug.Log(
                    "[BattlePVP] マッチング成功"
                    + $" isHost={result.IsHost}"
                    + $" isServer={(networkManager != null && networkManager.IsServer)}"
                    + $" isClient={(networkManager != null && networkManager.IsClient)}"
                    + $" isConnectedClient={(networkManager != null && networkManager.IsConnectedClient)}"
                    + $" 接続数={(networkManager != null ? networkManager.ConnectedClientsIds.Count : -1)}"
                    + $" flowStarter={(flowStarter != null ? flowStarter.GetType().Name : "null")}");
                flowStarter.BeginAfterMatchmaking();
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning("[BattlePVP] マッチングがキャンセルされました");
                ResetMatchingUi();
            }
            finally
            {
                isMatching = false;
            }
        }

        private void ResetMatchingUi()
        {
            view.SetCopyJoinCodeButtonVisible(false);
            view.ShowPanel(BattlePvpUiPanel.ModeSelect);
        }

        private void CancelMatching()
        {
            matchCts?.Cancel();
            matchCts?.Dispose();
            matchCts = null;
            matchmakingService.Cancel();
            isMatching = false;
        }

        private void OnMatchmakingStatusChanged(string message)
        {
            view.SetStatusText(message);
            if (isRoomCreation && !string.IsNullOrEmpty(matchmakingService.CurrentRoomCode))
            {
                view.SetJoinCodeText(matchmakingService.CurrentRoomCode);
            }

            bool canCopy = isRoomCreation && !string.IsNullOrEmpty(matchmakingService.CurrentRoomCode);
            view.SetCopyJoinCodeButtonVisible(canCopy);
        }
    }
}
