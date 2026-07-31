using Cysharp.Threading.Tasks;
using Scene.BattlePVPScene;
using Scene.BattlePVPScene.Interface;
using Scene.BattlePvpArena;
using Scene.Core;
using Scene.Core.Interface;
using Scene.PvpLobby.Interface;
using System;
using System.Threading;
using UnityEngine;
using VContainer;

namespace Scene.PvpLobby.Presenter
{
    /// <summary>
    /// DDOLロビーのマッチングUIとArena遷移を制御する
    /// </summary>
    public sealed class PvpLobbyPresenter : IPvpLobbyPresenter, IDisposable
    {
        private readonly IClayMonsterSceneManager sceneManager;
        private readonly IPvpLobby pvpLobby;
        private readonly IBattlePVPView view;
        private readonly IBattlePvpMatchmakingService matchmakingService;

        private CancellationTokenSource matchCts;
        private bool isMatching;
        private bool isRoomCreation;
        private bool isSetup;
        private BattlePvpMatchMode pendingMatchMode = BattlePvpMatchMode.Direct;

        [Inject]
        public PvpLobbyPresenter(
            IClayMonsterSceneManager sceneManager,
            IPvpLobby pvpLobby,
            IBattlePVPView view,
            IBattlePvpMatchmakingService matchmakingService)
        {
            this.sceneManager = sceneManager;
            this.pvpLobby = pvpLobby;
            this.view = view;
            this.matchmakingService = matchmakingService;
        }

        /// <inheritdoc/>
        public void Setup()
        {
            if (isSetup)
            {
                return;
            }

            matchmakingService.StatusChanged += OnMatchmakingStatusChanged;
            view.SubscribeReturnButtonClick(OnClickReturnButton);
            view.SubscribeDirectMatchButtonClick(OnClickDirectMatchButton);
            view.SubscribeRandomMatchButtonClick(OnClickRandomMatchButton);
            view.SubscribeCreateRoomButtonClick(OnClickCreateRoomButton);
            view.SubscribeJoinRoomButtonClick(OnClickJoinRoomButton);
            view.SubscribeStartRandomMatchButtonClick(OnClickStartRandomMatchButton);
            view.SubscribeCancelMatchButtonClick(OnClickCancelMatchButton);
            view.SubscribeMatchBackButtonClick(OnClickMatchBackButton);
            view.SubscribeCopyJoinCodeButtonClick(OnClickCopyJoinCodeButton);
            isSetup = true;
        }

        /// <inheritdoc/>
        public void OnShow()
        {
            ResetMatchingUi();
            view.SetStatusText(string.Empty);
            view.SetJoinCodeText(string.Empty);
            view.SetCopyJoinCodeButtonVisible(false);
        }

        /// <inheritdoc/>
        public void OnHide()
        {
            // 成功遷移時のHideでNetworkを落とさない
            // 中断は戻る/キャンセルボタン側のCancelMatchingで行う
            ResetMatchingUi();
        }

        /// <inheritdoc/>
        public void Dispose()
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
            pvpLobby.Hide();
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
            BeginMatching(
                BattlePvpMatchMode.Direct,
                token => matchmakingService.CreateDirectRoomAsync(token));
        }

        private void OnClickJoinRoomButton()
        {
            isRoomCreation = false;
            BeginMatching(
                BattlePvpMatchMode.Direct,
                token =>
                {
                    string joinCode = view.GetJoinCodeInput();
                    return matchmakingService.JoinDirectRoomAsync(joinCode, token);
                });
        }

        private void OnClickStartRandomMatchButton()
        {
            isRoomCreation = false;
            BeginMatching(
                BattlePvpMatchMode.Random,
                token => matchmakingService.StartRandomMatchAsync(token));
        }

        private void OnClickCopyJoinCodeButton()
        {
            string roomCode = matchmakingService.CurrentRoomCode;
            if (string.IsNullOrEmpty(roomCode))
            {
                return;
            }

            GUIUtility.systemCopyBuffer = roomCode;
            view.SetStatusText($"参加コード: {roomCode}\nコピーしました");
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

        private void BeginMatching(
            BattlePvpMatchMode matchMode,
            Func<CancellationToken, UniTask<BattlePvpMatchmakingResult>> matchTaskFactory)
        {
            if (isMatching)
            {
                return;
            }

            CancelMatching();
            pendingMatchMode = matchMode;
            isMatching = true;
            matchCts = new CancellationTokenSource();
            view.ShowPanel(BattlePvpUiPanel.Matching);
            view.SetCopyJoinCodeButtonVisible(false);
            view.SetStatusText("接続中…");
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
                    ResetMatchingUi();
                    return;
                }

                if (!result.IsSuccess)
                {
                    view.SetStatusText(result.ErrorMessage);
                    await UniTask.Delay(TimeSpan.FromSeconds(1.2f), cancellationToken: cancellationToken);
                    ResetMatchingUi();
                    return;
                }

                view.SetCopyJoinCodeButtonVisible(false);
                view.SetJoinCodeText(result.RoomCode);
                Debug.Log(
                    "[PvpLobby] マッチング成功 Arenaへ遷移します"
                    + $" isHost={result.IsHost}"
                    + $" room={result.RoomCode}"
                    + " session=keep");

                if (sceneManager.IsTransition)
                {
                    return;
                }

                // Hide前に接続を保持したままマッチング状態だけ解放する
                matchmakingService.FinishMatchmakingKeepNetwork();
                matchCts?.Dispose();
                matchCts = null;
                isMatching = false;
                pvpLobby.Hide();
                await sceneManager.TransitionScene(
                    new BattlePvpArenaScene.BattlePvpArenaTransitionData
                    {
                        MatchMode = pendingMatchMode
                    });
            }
            catch (OperationCanceledException)
            {
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
            if (!string.IsNullOrEmpty(message) && message.Contains("参加コード"))
            {
                int index = message.IndexOf(':');
                if (index >= 0 && index + 1 < message.Length)
                {
                    view.SetJoinCodeText(message[(index + 1)..].Trim());
                }
            }

            bool canCopy = isRoomCreation && !string.IsNullOrEmpty(matchmakingService.CurrentRoomCode);
            view.SetCopyJoinCodeButtonVisible(canCopy);
        }
    }
}
