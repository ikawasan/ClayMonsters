using Battle.Interface;
using ClayEditor.Rigging;
using Cysharp.Threading.Tasks;
using Extensions;
using LighthouseExtends.UIComponent.Button;
using R3;
using SaveData;
using SaveData.Interface;
using Scene.Core.Interface;
using Scene.TrainingScene.Domain;
using Scene.TrainingScene.Interface;
using Scene.TrainingScene.View;
using System;
using System.Collections.Generic;
using System.Threading;
using UI.ClayEditor.View;
using UnityEngine;
using UnityEngine.Serialization;
using VContainer;

namespace Scene.TrainingScene
{
    /// <summary>
    /// 育成シーンの5日間ループと行動ターン進行を実行する
    /// </summary>
    public sealed class TrainingFlowRunner : MonoBehaviour
    {
        private static TrainingFlowRunner activeRunner;

        [SerializeField] private LoadSlotView loadSlotView;
        [FormerlySerializedAs("selectionUiGroup")]
        [FormerlySerializedAs("selectionCanvas")]
        [SerializeField] private Canvas selectionCanvas;
        [Tooltip("モンスター選択画面のタイトルへ戻るボタン")]
        [SerializeField] private LHButton selectionBackToTitleButton;
        [SerializeField] private TrainingDisplay trainingDisplay;
        [SerializeField] private TrainingBackgroundView backgroundView;
        [SerializeField] private TrainingLocationCameraView locationCameraView;

        private IClayModelSaveService saveService;
        private ITrainingHudView hudView;
        private ITrainingTrainedSaveView trainedSaveView;
        private ITrainingModeSelectView modeSelectView;
        private ITrainingAutoResultView autoResultView;
        private IClayMonsterSceneManager sceneManager;
        private IBattleCanvasTransition canvasTransition;
        private TrainingBattleRunner battleRunner;

        private CancellationTokenSource flowCts;
        private bool isRunning;
        private System.Random random = new System.Random();
        private TrainingSession activeSession;

        [Inject]
        public void Construct(
            IClayModelSaveService saveService,
            ITrainingHudView hudView,
            ITrainingTrainedSaveView trainedSaveView,
            ITrainingModeSelectView modeSelectView,
            ITrainingAutoResultView autoResultView,
            IClayMonsterSceneManager sceneManager,
            IBattleCanvasTransition canvasTransition,
            TrainingBattleRunner battleRunner)
        {
            this.saveService = saveService;
            this.hudView = hudView;
            this.trainedSaveView = trainedSaveView;
            this.modeSelectView = modeSelectView;
            this.autoResultView = autoResultView;
            this.sceneManager = sceneManager;
            this.canvasTransition = canvasTransition;
            this.battleRunner = battleRunner;
        }

        /// <summary>
        /// 育成フローを開始する
        /// </summary>
        public void StartFlow()
        {
            if (activeRunner != null && activeRunner != this && activeRunner.isRunning)
            {
                Debug.LogWarning("[TrainingFlowRunner] 別インスタンスが既に育成フローを実行中のため開始をスキップします");
                return;
            }

            if (isRunning)
            {
                return;
            }

            activeRunner = this;
            flowCts?.Cancel();
            flowCts?.Dispose();
            flowCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            RunAsync(flowCts.Token).Forget();
        }

        /// <summary>
        /// 育成フローを停止する
        /// </summary>
        public void StopFlow()
        {
            flowCts?.Cancel();
            if (activeRunner == this)
            {
                activeRunner = null;
            }
        }

        /// <summary>
        /// 進行中の育成データを保存する
        /// </summary>
        public void SaveActiveProgressIfNeeded()
        {
            if (activeSession == null || activeSession.IsCompleted)
            {
                return;
            }

            SaveTrainingProgress(activeSession);
        }

        /// <summary>
        /// 明転前にスロット選択UIのレイアウトだけ整える
        /// </summary>
        public void PrepareSelectionLayout()
        {
            loadSlotView?.HideForLeave();
            loadSlotView?.PrepareLayout();
            SetSelectionUiVisible(false);
            hudView?.Hide();
            modeSelectView?.Hide();
            autoResultView?.Hide();
            trainedSaveView?.HideForLeave();
        }

        /// <summary>
        /// シーン退場時に選択UIと育成UIを整理する
        /// </summary>
        public void CleanupForLeave()
        {
            StopFlow();
            battleRunner?.HideForLeave();
            loadSlotView?.HideForLeave();
            SetSelectionUiVisible(false);
            SetSelectionBackToTitleButtonVisible(false);
            hudView?.Hide();
            modeSelectView?.Hide();
            autoResultView?.Hide();
            trainedSaveView?.HideForLeave();
            ModelSaveSlotScrollListView.ExitFullscreenSelectionLayout();
        }

        /// <summary>
        /// 暗転後にスロット選択キャンバスを表示して明転する
        /// </summary>
        public async UniTask RevealSelectionAsync(CancellationToken cancellationToken)
        {
            if (selectionCanvas != null)
            {
                selectionCanvas.gameObject.SetActive(true);
            }

            loadSlotView?.DetachSelectionUiToSceneRoot(transform);
            SetSelectionUiVisible(true);
            loadSlotView?.PrepareLayout();
            loadSlotView?.EnsureSelectionReady();
            await FinalizeSelectionLayoutAsync(cancellationToken);

            if (canvasTransition != null)
            {
                await canvasTransition.FadeInAsync(cancellationToken);
            }
        }

        private async UniTask FinalizeSelectionLayoutAsync(CancellationToken cancellationToken)
        {
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);
            loadSlotView?.DetachSelectionUiToSceneRoot(transform);
            loadSlotView?.PrepareLayout();
            loadSlotView?.EnsureSelectionReady();
            loadSlotView?.Refresh();

            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);
            Canvas.ForceUpdateCanvases();
            LogSelectionUiState("RevealSelectionAsync完了");
        }

        private void LogSelectionUiState(string phase)
        {
            if (selectionCanvas == null)
            {
                Debug.LogWarning($"[TrainingFlowRunner] {phase} selectionCanvas未設定");
                return;
            }

            Debug.Log(
                $"[TrainingFlowRunner] {phase}"
                + $" canvasEnabled={selectionCanvas.enabled}"
                + $" active={selectionCanvas.gameObject.activeInHierarchy}"
                + $" scale={selectionCanvas.transform.lossyScale}");
        }

        /// <summary>
        /// シーン入場後の初回表示を行う
        /// 途中データがある場合は暗転のままフロー側で明転する
        /// </summary>
        public async UniTask RevealEntryAsync(CancellationToken cancellationToken)
        {
            loadSlotView?.PrepareLayout();
            SetSelectionUiVisible(false);

            bool hasResume = saveService != null
                && TrainingSlotProgressMapper.TryFindResumablePlayerSlot(
                    saveService,
                    out _,
                    out _,
                    out _);

            if (hasResume)
            {
                return;
            }

            await RevealSelectionAsync(cancellationToken);
        }

        private async UniTask RunAsync(CancellationToken cancellationToken)
        {
            isRunning = true;
            activeSession = null;
            try
            {
                autoResultView?.Hide();
                hudView.Hide();

                if (await TryRunResumedTrainingAsync(cancellationToken))
                {
                    return;
                }

                (int slotIndex, GameObject selectedModel) = await WaitForMonsterSelectionAsync(cancellationToken);
                if (slotIndex < 0 || selectedModel == null)
                {
                    await EnsureSceneVisibleAfterSelectionFailureAsync(cancellationToken);
                    return;
                }

                ModelSaveSlot slot = saveService.GetSlot(ModelSavePool.Player, slotIndex);
                if (slot == null)
                {
                    await EnsureSceneVisibleAfterSelectionFailureAsync(cancellationToken);
                    return;
                }

                PreparePostSelectionPresentation();
                bool loaded = await trainingDisplay.AdoptLoadedModelAsync(selectedModel, slotIndex, cancellationToken);
                if (!loaded)
                {
                    await PresentLoadFailureAsync(cancellationToken);
                    return;
                }

                ApplyDefaultDestinationPresentation();
                TrainingSession session = CreateFreshSession(slotIndex, slot);
                TrainingPlayMode playMode = await WaitForPlayModeAsync(slot.modelName, cancellationToken);
                if (playMode == TrainingPlayMode.Auto)
                {
                    await RunAutoTrainingAsync(session, slot.modelName, cancellationToken);
                    return;
                }

                await RunActiveTrainingAsync(session, slot.modelName, cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                activeSession = null;
                hudView.SetInterruptButtonVisible(false);
                hudView.Hide();
                modeSelectView?.Hide();
                autoResultView?.Hide();
                isRunning = false;
                if (activeRunner == this)
                {
                    activeRunner = null;
                }
            }
        }

        private async UniTask<bool> TryRunResumedTrainingAsync(CancellationToken cancellationToken)
        {
            if (!TrainingSlotProgressMapper.TryFindResumablePlayerSlot(
                saveService,
                out int slotIndex,
                out ModelSaveSlot slot,
                out TrainingSlotProgress progress))
            {
                return false;
            }

            EnsureTrainingDisplayActive();
            if (!await trainingDisplay.LoadSlotAsync(slotIndex, cancellationToken))
            {
                saveService.ClearTrainingProgress(ModelSavePool.Player, slotIndex);
                trainingDisplay.Clear();
                await RevealSelectionAsync(cancellationToken);
                return false;
            }

            ApplyDefaultDestinationPresentation();
            hudView.ShowOverlayHost();
            hudView.ShowResumeChoices(TrainingSlotProgressMapper.BuildResumePresentation(slot, progress));

            if (canvasTransition != null)
            {
                await canvasTransition.FadeInAsync(cancellationToken);
            }

            bool resume = await hudView.WaitResumeChoiceAsync(cancellationToken);
            if (!resume)
            {
                trainingDisplay.Clear();
                saveService.ClearTrainingProgress(ModelSavePool.Player, slotIndex);
                hudView.Hide();
                await RevealSelectionAsync(cancellationToken);
                return false;
            }

            if (canvasTransition != null)
            {
                await canvasTransition.FadeOutAsync(cancellationToken);
            }

            if (!trainingDisplay.IsDisplayingSlot(slotIndex)
                && !await trainingDisplay.LoadSlotAsync(slotIndex, cancellationToken))
            {
                saveService.ClearTrainingProgress(ModelSavePool.Player, slotIndex);
                trainingDisplay.Clear();
                hudView.Hide();
                await RevealSelectionAsync(cancellationToken);
                return false;
            }

            if (canvasTransition != null)
            {
                await canvasTransition.FadeInAsync(cancellationToken);
            }

            TrainingSession session = TrainingSlotProgressMapper.FromSaveData(slotIndex, progress);
            await RunActiveTrainingAsync(session, slot.modelName, cancellationToken);
            return true;
        }

        private void EnsureTrainingDisplayActive()
        {
            if (trainingDisplay != null)
            {
                trainingDisplay.gameObject.SetActive(true);
            }
        }

        private async UniTask<TrainingPlayMode> WaitForPlayModeAsync(
            string modelName,
            CancellationToken cancellationToken)
        {
            if (modeSelectView == null)
            {
                return TrainingPlayMode.Manual;
            }

            ApplyDefaultDestinationPresentation();
            autoResultView?.Hide();
            hudView.ShowOverlayHost();
            modeSelectView.Show(modelName);
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);
            Canvas.ForceUpdateCanvases();

            if (canvasTransition != null)
            {
                await canvasTransition.FadeInAsync(cancellationToken);
            }

            TrainingPlayMode playMode = await modeSelectView.WaitChoiceAsync(cancellationToken);
            if (canvasTransition != null)
            {
                await canvasTransition.FadeOutAsync(cancellationToken);
            }

            modeSelectView?.Hide();
            return playMode;
        }

        private void PreparePostSelectionPresentation()
        {
            EnsureTrainingDisplayActive();
            ApplyDefaultDestinationPresentation();
        }

        private async UniTask RunAutoTrainingAsync(
            TrainingSession session,
            string modelName,
            CancellationToken cancellationToken)
        {
            if (session == null)
            {
                return;
            }

            activeSession = session;
            ApplyDefaultDestinationPresentation();

            float progressStartedAt = Time.unscaledTime;
            hudView.ShowOverlayMessage("自動育成を実行中...");
            if (canvasTransition != null)
            {
                await canvasTransition.FadeInAsync(cancellationToken);
            }
            await UniTask.Yield(cancellationToken);

            TrainingAutoSimulator.Run(
                session,
                saveService,
                ResolveUsableAttacks(session),
                random);

            float progressElapsed = Time.unscaledTime - progressStartedAt;
            if (progressElapsed < 1f)
            {
                await UniTask.Delay(
                    System.TimeSpan.FromSeconds(1f - progressElapsed),
                    ignoreTimeScale: true,
                    cancellationToken: cancellationToken);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            hudView.ClearOverlayMessage();
            await RunTrainingCompletionFlowAsync(session, modelName, cancellationToken);
        }

        private async UniTask RunTrainingCompletionFlowAsync(
            TrainingSession session,
            string modelName,
            CancellationToken cancellationToken)
        {
            if (session == null)
            {
                return;
            }

            TrainingActionResolver.ClampStatus(session.CurrentStatus);
            hudView.SetInterruptButtonVisible(false);
            hudView.Hide();

            byte[] thumbnailPng = ModelSaveStorage.ReadThumbnailPng(
                saveService.GetSlot(ModelSavePool.Player, session.PlayerSlotIndex));


            if (autoResultView == null)
            {
                await RunLegacyTrainingCompletionFlowAsync(
                    session,
                    modelName,
                    thumbnailPng,
                    cancellationToken);
                return;
            }

            hudView.ShowModalOverlayHost();
            autoResultView.Show(TrainingSlotProgressMapper.BuildAutoResultPresentation(
                modelName,
                session.CurrentStatus,
                session.AttackMotions,
                thumbnailPng: thumbnailPng));
            await autoResultView.WaitContinueAsync(cancellationToken);
            autoResultView.Hide();

            int trainedSlotIndex = trainedSaveView != null
                ? await trainedSaveView.WaitForConfirmedSlotAsync(
                    modelName,
                    session.CurrentStatus,
                    session.AttackMotions,
                    thumbnailPng,
                    cancellationToken)
                : 0;

            if (trainedSlotIndex < 0)
            {
                await ReturnToTitleWithoutTrainedSaveAsync(session, cancellationToken);
                return;
            }

            (bool saved, string saveResultMessage) = await SaveTrainedResultAsync(
                session,
                trainedSlotIndex,
                modelName,
                cancellationToken);
            if (saved)
            {
                saveService.ClearTrainingProgress(ModelSavePool.Player, session.PlayerSlotIndex);
            }

            session.MarkCompleted();
            activeSession = null;

            hudView.ShowModalOverlayHost();
            autoResultView.Show(TrainingSlotProgressMapper.BuildAutoResultPresentation(
                modelName,
                session.CurrentStatus,
                session.AttackMotions,
                saveResultMessage,
                "タイトルへ戻る",
                thumbnailPng));
            await autoResultView.WaitBackToTitleAsync(cancellationToken);

            if (sceneManager != null && !sceneManager.IsTransition)
            {
                await sceneManager.BackScene();
            }
        }

        private async UniTask RunLegacyTrainingCompletionFlowAsync(
            TrainingSession session,
            string modelName,
            byte[] thumbnailPng,
            CancellationToken cancellationToken)
        {
            int trainedSlotIndex = trainedSaveView != null
                ? await trainedSaveView.WaitForConfirmedSlotAsync(
                    modelName,
                    session.CurrentStatus,
                    session.AttackMotions,
                    thumbnailPng,
                    cancellationToken)
                : 0;

            if (trainedSlotIndex < 0)
            {
                await ReturnToTitleWithoutTrainedSaveAsync(session, cancellationToken);
                return;
            }

            (bool saved, _) = await SaveTrainedResultAsync(
                session,
                trainedSlotIndex,
                modelName,
                cancellationToken);
            if (saved)
            {
                saveService.ClearTrainingProgress(ModelSavePool.Player, session.PlayerSlotIndex);
            }

            session.MarkCompleted();
            activeSession = null;
            await WaitBackToTitleAsync(cancellationToken);
        }

        private async UniTask RunActiveTrainingAsync(
            TrainingSession session,
            string modelName,
            CancellationToken cancellationToken)
        {
            if (session == null)
            {
                return;
            }

            activeSession = session;
            hudView.Show();
            hudView.SetInterruptButtonVisible(true);
            if (canvasTransition != null)
            {
                await canvasTransition.FadeInAsync(cancellationToken);
            }

            using (hudView.SubscribeInterruptClick(OnInterruptRequested))
            {
                await RunTrainingDaysAsync(session, modelName, cancellationToken);
            }
        }

        private TrainingSession CreateFreshSession(int slotIndex, ModelSaveSlot slot)
        {
            TrainingSlotProgress savedProgress = saveService.GetTrainingProgress(ModelSavePool.Player, slotIndex);
            if (savedProgress != null)
            {
                saveService.ClearTrainingProgress(ModelSavePool.Player, slotIndex);
            }

            return new TrainingSession(slotIndex, slot.status, slot.attackMotions);
        }

        private void OnInterruptRequested()
        {
            SaveActiveProgressIfNeeded();
            StopFlow();
            if (sceneManager != null && !sceneManager.IsTransition)
            {
                sceneManager.BackScene().Forget();
            }
        }

        private async UniTask<(int slotIndex, GameObject model)> WaitForMonsterSelectionAsync(
            CancellationToken cancellationToken)
        {
            if (loadSlotView == null)
            {
                return (-1, null);
            }

            loadSlotView.Refresh();
            GameObject model = null;
            bool backToTitlePressed = false;
            using (loadSlotView.OnModelLoaded.Subscribe(loaded => model = loaded))
            using (SubscribeSelectionBackToTitleClick(() => backToTitlePressed = true))
            {
                await UniTask.WaitUntil(
                    () => model != null || backToTitlePressed,
                    cancellationToken: cancellationToken);
            }

            if (backToTitlePressed)
            {
                SetSelectionUiVisible(false);
                if (sceneManager != null && !sceneManager.IsTransition)
                {
                    await sceneManager.BackScene();
                }

                return (-1, null);
            }

            int slotIndex = loadSlotView.SelectedSlotIndex;
            GameObject selectedModel = loadSlotView.DetachLoadedModel() ?? model;

            if (canvasTransition != null)
            {
                await canvasTransition.FadeOutAsync(cancellationToken);
            }

            SetSelectionUiVisible(false);

            return (slotIndex, selectedModel);
        }

        private async UniTask EnsureSceneVisibleAfterSelectionFailureAsync(CancellationToken cancellationToken)
        {
            if (canvasTransition != null)
            {
                await canvasTransition.FadeInAsync(cancellationToken);
            }
        }

        private async UniTask PresentLoadFailureAsync(CancellationToken cancellationToken)
        {
            hudView.Show();
            hudView.SetLogMessage("モデルの読み込みに失敗しました");
            if (canvasTransition != null)
            {
                await canvasTransition.FadeInAsync(cancellationToken);
            }

            await WaitBackToTitleAsync(cancellationToken);
        }

        private IDisposable SubscribeSelectionBackToTitleClick(Action action)
        {
            if (selectionBackToTitleButton == null || action == null)
            {
                return new EmptyDisposable();
            }

            selectionBackToTitleButton.EnsureUiSoundFeedback();
            return selectionBackToTitleButton.SubscribeOnClick(() => action());
        }

        private async UniTask ReturnToTitleWithoutTrainedSaveAsync(
            TrainingSession session,
            CancellationToken cancellationToken)
        {
            trainedSaveView?.HideForLeave();
            session?.MarkCompleted();
            activeSession = null;

            if (sceneManager != null && !sceneManager.IsTransition)
            {
                await sceneManager.BackScene();
            }
        }

        private void SetSelectionBackToTitleButtonVisible(bool visible)
        {
            if (selectionBackToTitleButton != null)
            {
                selectionBackToTitleButton.gameObject.SetActive(visible);
            }
        }

        private void SetSelectionUiVisible(bool visible)
        {
            if (selectionCanvas == null && loadSlotView != null)
            {
                selectionCanvas = loadSlotView.SelectionCanvas;
            }

            if (!visible)
            {
                loadSlotView?.HideSelectionUi();
                SetSelectionBackToTitleButtonVisible(false);
                CanvasVisibilityUtility.SetCanvasEnabled(selectionCanvas, false);
                return;
            }

            if (selectionBackToTitleButton == null)
            {
                Debug.LogError(
                    "[TrainingFlowRunner] selectionBackToTitleButtonが未設定です。Editor Wireツールで参照を配線してください");
            }

            SetSelectionBackToTitleButtonVisible(true);

            if (loadSlotView != null)
            {
                loadSlotView.ShowSelectionUi();
            }

            CanvasVisibilityUtility.SetCanvasEnabled(selectionCanvas, visible);
        }

        private sealed class EmptyDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }

        private async UniTask RunTrainingDaysAsync(
            TrainingSession session,
            string modelName,
            CancellationToken cancellationToken)
        {
            while (!session.IsCompleted && !cancellationToken.IsCancellationRequested)
            {
                await RunSingleDayAsync(session, modelName, cancellationToken);
                if (session.IsCompleted || cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                // 金曜終了後は次の日開始メッセージを出さず完了フローへ進む
                if ((int)session.CurrentDay >= TrainingSettings.TotalDays)
                {
                    session.AdvanceDay();
                    CheckpointSave(session);
                    break;
                }

                await TransitionTurnAsync(
                    cancellationToken,
                    () =>
                    {
                        session.AdvanceDay();
                        CheckpointSave(session);
                        hudView.BindSession(
                            session,
                            TrainingDailySchedule.AllPeriods[0],
                            1);
                        hudView.SetLogMessage(
                            $"{TrainingDayCatalog.GetDisplayName(session.CurrentDay)}が始まりました");
                    });
                await hudView.WaitContinueAsync(cancellationToken);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await RunTrainingCompletionFlowAsync(session, modelName, cancellationToken);
        }

        private async UniTask<(bool saved, string message)> SaveTrainedResultAsync(
            TrainingSession session,
            int trainedSlotIndex,
            string modelName,
            CancellationToken cancellationToken)
        {
            if (!IsValidTrainedSlotIndex(trainedSlotIndex))
            {
                return (false, "育成済みデータの保存先が選ばれませんでした");
            }

            SkinnedMeshRenderer renderer = null;
            Transform boneRoot = null;
            if (trainingDisplay != null)
            {
                trainingDisplay.TryGetSaveComponents(out renderer, out boneRoot);
            }

            bool saved = await saveService.SaveTrainedResultAsync(
                trainedSlotIndex,
                session.PlayerSlotIndex,
                modelName,
                session.CurrentStatus,
                session.AttackMotions,
                renderer,
                boneRoot,
                cancellationToken);

            if (saved)
            {
                return (true, string.Empty);
            }

            Debug.LogError(
                $"[TrainingFlowRunner] 育成済みデータの保存に失敗しました trainedSlot={trainedSlotIndex} playerSlot={session.PlayerSlotIndex}");
            return (false, "育成済みデータの保存に失敗しました\n未育成データは保持されています");
        }

        private static bool IsValidTrainedSlotIndex(int slotIndex)
        {
            return slotIndex >= 0 && slotIndex < ModelSavePoolSettings.SlotCount;
        }

        private async UniTask RunSingleDayAsync(
            TrainingSession session,
            string modelName,
            CancellationToken cancellationToken)
        {
            TrainingPeriod[] periods = TrainingDailySchedule.AllPeriods;
            int startPeriodIndex = Mathf.Clamp(session.TurnIndexInDay, 0, periods.Length);
            if (startPeriodIndex >= periods.Length)
            {
                return;
            }

            for (int i = startPeriodIndex; i < periods.Length && !cancellationToken.IsCancellationRequested; i++)
            {
                TrainingPeriod period = periods[i];
                int turnNumber = i + 1;

                if (i > startPeriodIndex)
                {
                    await TransitionTurnAsync(
                        cancellationToken,
                        () =>
                        {
                            hudView.HideLocationChoices();
                            hudView.HideAttackSwapChoices();
                        });
                }

                if (TrainingPeriodCatalog.IsBattlePeriod(period))
                {
                    await RunAfterSchoolPeriodAsync(session, modelName, period, turnNumber, cancellationToken);
                    CheckpointSave(session);
                    continue;
                }

                hudView.BindSession(session, period, turnNumber);
                TrainingLocation[] choices = TrainingActionResolver.PickLocationChoices(
                    TrainingSettings.LocationChoiceCount,
                    random);
                hudView.ShowLocationChoices(choices, session.Stamina);
                TrainingTurnChoice turnChoice = await hudView.WaitTurnChoiceAsync(cancellationToken);
                TrainingActionResult result = turnChoice.IsRest
                    ? TrainingActionResolver.ExecuteRest(session.Stamina, random)
                    : TrainingActionResolver.ExecuteAction(turnChoice.Location, session.Stamina, random);
                session.ApplyAction(result);
                CheckpointSave(session);

                await TransitionTurnAsync(
                    cancellationToken,
                    () =>
                    {
                        hudView.HideLocationChoices();
                        hudView.HideAttackSwapChoices();
                        ApplyDestinationBackground(turnChoice);
                        hudView.BindSession(session, period, turnNumber);
                        hudView.SetLogMessage(BuildActionLog(result));
                    });
                await hudView.WaitContinueAsync(cancellationToken);

                if (result.Succeeded)
                {
                    await TryRunRandomEventAsync(session, period, turnNumber, cancellationToken);
                    CheckpointSave(session);
                }
            }
        }

        private async UniTask RunAfterSchoolPeriodAsync(
            TrainingSession session,
            string modelName,
            TrainingPeriod period,
            int turnNumber,
            CancellationToken cancellationToken)
        {
            hudView.BindSession(session, period, turnNumber);
            hudView.HideLocationChoices();

            if (!TrainingEnemyResolver.TryPickEnemySlotIndex(saveService, session.CurrentDay, out int enemySlotIndex))
            {
                hudView.SetLogMessage("放課後の対戦相手が見つかりませんでした");
                session.CompletePeriod();
                await hudView.WaitContinueAsync(cancellationToken);
                return;
            }

            string enemyName = saveService.GetSlot(ModelSavePool.Enemy, enemySlotIndex)?.modelName ?? "敵";
            hudView.SetLogMessage($"放課後の戦闘\n{enemyName}との対戦が始まります");
            await hudView.WaitContinueAsync(cancellationToken);

            GameObject playerModel = trainingDisplay != null ? trainingDisplay.LoadedModel : null;
            if (battleRunner == null)
            {
                Debug.LogError("[TrainingFlowRunner] TrainingBattleRunnerが未注入です");
                hudView.SetLogMessage("放課後の戦闘を開始できませんでした");
                session.CompletePeriod();
                await hudView.WaitContinueAsync(cancellationToken);
                return;
            }

            if (playerModel == null)
            {
                Debug.LogError("[TrainingFlowRunner] 表示中のプレイヤーモデルがありません");
                hudView.SetLogMessage("放課後の戦闘を開始できませんでした");
                session.CompletePeriod();
                await hudView.WaitContinueAsync(cancellationToken);
                return;
            }

            if (canvasTransition != null)
            {
                await canvasTransition.FadeOutAsync(cancellationToken);
            }

            SetSelectionUiVisible(false);
            if (loadSlotView != null)
            {
                CanvasVisibilityUtility.SetCanvasEnabled(loadSlotView.SelectionCanvas, false);
            }

            trainingDisplay?.PrepareModelForBattle(battleRunner.BattlePlayerSpawn);
            hudView.Hide();

            TrainingBattleResult battleResult;
            try
            {
                battleResult = await battleRunner.RunAfterSchoolBattleAsync(
                    session,
                    playerModel,
                    modelName,
                    enemySlotIndex,
                    cancellationToken);
            }
            finally
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    hudView.Show();
                }
            }
            hudView.BindSession(session, period, turnNumber);

            if (!battleResult.Played)
            {
                hudView.SetLogMessage("放課後の戦闘を開始できませんでした");
                session.CompletePeriod();
                await hudView.WaitContinueAsync(cancellationToken);
                return;
            }

            ApplyDefaultDestinationPresentation();

            if (battleResult.PlayerWon)
            {
                session.ApplyAfterSchoolVictoryRecovery();
                hudView.BindSession(session, period, turnNumber);
                hudView.SetLogMessage(
                    $"放課後の戦闘に勝利しました\n体力+{TrainingSettings.AfterSchoolVictoryStaminaRecovery}");
            }
            else
            {
                hudView.SetLogMessage("放課後の戦闘に敗北しました");
            }

            await hudView.WaitContinueAsync(cancellationToken);
            session.CompletePeriod();
        }

        private async UniTask TryRunRandomEventAsync(
            TrainingSession session,
            TrainingPeriod period,
            int turnNumber,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<MotionType> usableAttacks = ResolveUsableAttacks(session);

            if (TrainingEventResolver.TryRollLearnAttackEvent(
                random,
                session.AttackMotions,
                usableAttacks,
                out TrainingEventOutcome learnOutcome))
            {
                await RunLearnAttackEventAsync(session, period, turnNumber, learnOutcome, cancellationToken);
                return;
            }

            if (!TrainingEventResolver.TryRollStatBoostEvent(random, out TrainingEventOutcome statOutcome))
            {
                return;
            }

            hudView.SetLogMessage($"【{statOutcome.Title}】\n{statOutcome.Message}");
            await hudView.WaitContinueAsync(cancellationToken);
            session.ApplyEventStatGain(statOutcome.StatGain);
            hudView.BindSession(session, period, turnNumber);
        }

        private async UniTask RunLearnAttackEventAsync(
            TrainingSession session,
            TrainingPeriod period,
            int turnNumber,
            TrainingEventOutcome outcome,
            CancellationToken cancellationToken)
        {
            hudView.SetLogMessage($"【{outcome.Title}】\n{outcome.Message}");
            await hudView.WaitContinueAsync(cancellationToken);

            hudView.ShowAttackSwapChoices(outcome.LearnedAttack, session.AttackMotions);
            int replaceIndex = await hudView.WaitAttackSwapChoiceAsync(cancellationToken);
            if (replaceIndex < 0)
            {
                hudView.SetLogMessage($"「{TrainingAttackTeacher.FormatAttackLabel(outcome.LearnedAttack)}」は覚えませんでした");
                await hudView.WaitContinueAsync(cancellationToken);
                return;
            }

            MotionType oldAttack = session.AttackMotions[replaceIndex];
            if (session.TryReplaceAttack(replaceIndex, outcome.LearnedAttack))
            {
                hudView.SetLogMessage(
                    $"技を入れ替えました\nスロット{replaceIndex + 1}: {TrainingAttackTeacher.FormatAttackLabel(oldAttack)}"
                    + $" → {TrainingAttackTeacher.FormatAttackLabel(outcome.LearnedAttack)}");
            }

            await hudView.WaitContinueAsync(cancellationToken);
        }

        private IReadOnlyList<MotionType> ResolveUsableAttacks(TrainingSession session)
        {
            if (trainingDisplay != null && trainingDisplay.UsableAttacks.Count > 0)
            {
                return trainingDisplay.UsableAttacks;
            }

            return session.AttackMotions;
        }

        private void CheckpointSave(TrainingSession session)
        {
            if (session == null || session.IsCompleted)
            {
                return;
            }

            SaveTrainingProgress(session);
        }

        private void SaveTrainingProgress(TrainingSession session)
        {
            TrainingSlotProgress progress = TrainingSlotProgressMapper.ToSaveData(session);
            if (progress == null)
            {
                return;
            }

            saveService.SaveTrainingProgress(ModelSavePool.Player, session.PlayerSlotIndex, progress);
        }

        private async UniTask WaitBackToTitleAsync(CancellationToken cancellationToken)
        {
            bool pressed = false;
            using (hudView.SubscribeBackToTitleClick(() => pressed = true))
            {
                await UniTask.WaitUntil(() => pressed, cancellationToken: cancellationToken);
            }

            if (sceneManager != null && !sceneManager.IsTransition)
            {
                await sceneManager.BackScene();
            }
        }

        private static string BuildActionLog(TrainingActionResult result)
        {
            string locationName = TrainingLocationCatalog.GetDisplayName(result.Location);
            if (!result.Succeeded)
            {
                return result.FailedByLowStamina
                    ? $"{locationName}の行動は体力不足で失敗しました"
                    : $"{locationName}の行動は失敗しました";
            }

            if (result.IsRestAction)
            {
                string outcome = result.IsGreatSuccess ? "大成功" : "成功";
                return $"休憩{outcome} 体力 {result.StaminaBefore}→{result.StaminaAfter}";
            }

            TrainingStatGain gain = result.AppliedGain;
            return $"{locationName}で育成成功 HP+{gain.Hp} 攻+{gain.Attack} 防+{gain.Defense} 速+{gain.Speed}";
        }

        private void ApplyDefaultDestinationPresentation()
        {
            backgroundView?.ShowDefaultBackground();
            locationCameraView?.ApplyDefaultView();
        }

        private void ApplyDestinationBackground(TrainingTurnChoice turnChoice)
        {
            if (turnChoice.IsRest)
            {
                backgroundView?.ShowRestBackground();
                locationCameraView?.ApplyRestView();
                return;
            }

            backgroundView?.ShowLocationBackground(turnChoice.Location);
            locationCameraView?.ApplyLocationView(turnChoice.Location);
        }

        private async UniTask TransitionTurnAsync(CancellationToken cancellationToken, Action applyWhileBlack = null)
        {
            if (canvasTransition == null)
            {
                applyWhileBlack?.Invoke();
                return;
            }

            await canvasTransition.FadeOutAsync(cancellationToken);
            applyWhileBlack?.Invoke();
            await canvasTransition.FadeInAsync(cancellationToken);
        }
    }
}
