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
        [SerializeField] private TrainingMonsterRoamController monsterRoamController;
        [SerializeField] private TrainingInheritancePresentationView inheritancePresentationView;

        private IClayModelSaveService saveService;
        private ITrainingHudView hudView;
        private ITrainingTrainedSaveView trainedSaveView;
        private ITrainingInheritanceSelectView inheritanceSelectView;
        private ITrainingModeSelectView modeSelectView;
        private ITrainingAutoResultView autoResultView;
        private ITrainingAmbushView ambushView;
        private IClayMonsterSceneManager sceneManager;
        private IBattleCanvasTransition canvasTransition;
        private TrainingBattleRunner battleRunner;
        private CancellationTokenSource flowCts;
        private bool isRunning;
        private System.Random random = new System.Random();
        private TrainingSession activeSession;

        private ITrainingInheritancePresentation InheritancePresentation =>
            inheritancePresentationView != null
                ? inheritancePresentationView
                : null;

        [Inject]
        public void Construct(
            IClayModelSaveService saveService,
            ITrainingHudView hudView,
            ITrainingTrainedSaveView trainedSaveView,
            ITrainingInheritanceSelectView inheritanceSelectView,
            ITrainingModeSelectView modeSelectView,
            ITrainingAutoResultView autoResultView,
            ITrainingAmbushView ambushView,
            IClayMonsterSceneManager sceneManager,
            IBattleCanvasTransition canvasTransition,
            TrainingBattleRunner battleRunner)
        {
            this.saveService = saveService;
            this.hudView = hudView;
            this.trainedSaveView = trainedSaveView;
            this.inheritanceSelectView = inheritanceSelectView;
            this.modeSelectView = modeSelectView;
            this.autoResultView = autoResultView;
            this.ambushView = ambushView;
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
            StopMonsterRoam();
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
            inheritanceSelectView?.HideForLeave();
            InheritancePresentation?.HideForLeave();
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
            inheritanceSelectView?.HideForLeave();
            InheritancePresentation?.HideForLeave();
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

                while (true)
                {
                    (int slotIndex, GameObject selectedModel) =
                        await WaitForMonsterSelectionAsync(cancellationToken);
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
                    bool loaded = await trainingDisplay.AdoptLoadedModelAsync(
                        selectedModel,
                        slotIndex,
                        cancellationToken);
                    if (!loaded)
                    {
                        await PresentLoadFailureAsync(cancellationToken);
                        return;
                    }

                    ApplyDefaultDestinationPresentation();
                    TrainingInheritanceResult inheritance =
                        await ResolveInheritanceAsync(slot, cancellationToken);
                    if (inheritance == null)
                    {
                        await ReturnToMonsterSelectionAsync(cancellationToken);
                        continue;
                    }

                    TrainingSession session = CreateFreshSession(slotIndex, slot, inheritance);
                    TrainingPlayMode playMode = await WaitForPlayModeAsync(slot.modelName, cancellationToken);
                    if (playMode == TrainingPlayMode.Auto)
                    {
                        await RunAutoTrainingAsync(session, slot.modelName, cancellationToken);
                        return;
                    }

                    await RunActiveTrainingAsync(session, slot.modelName, cancellationToken);
                    return;
                }
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
            modeSelectView.Show();
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

            session.EnsureShopOffer(random);
            CheckpointSave(session);
            activeSession = session;
            hudView.Show();
            hudView.SetInterruptButtonVisible(true);
            hudView.BindSession(session);

            if (canvasTransition != null)
            {
                await canvasTransition.FadeInAsync(cancellationToken);
            }

            using (hudView.SubscribeInterruptClick(OnInterruptRequested))
            {
                await RunTrainingDaysAsync(session, modelName, cancellationToken);
            }
        }

        private TrainingSession CreateFreshSession(
            int slotIndex,
            ModelSaveSlot slot,
            TrainingInheritanceResult inheritance)
        {
            TrainingSlotProgress savedProgress = saveService.GetTrainingProgress(ModelSavePool.Player, slotIndex);
            if (savedProgress != null)
            {
                saveService.ClearTrainingProgress(ModelSavePool.Player, slotIndex);
            }

            if (inheritance != null && inheritance.Applied)
            {
                return new TrainingSession(slotIndex, inheritance.Status, inheritance.AttackMotions);
            }

            return new TrainingSession(slotIndex, slot.status, slot.attackMotions);
        }

        private async UniTask<TrainingInheritanceResult> ResolveInheritanceAsync(
            ModelSaveSlot traineeSlot,
            CancellationToken cancellationToken)
        {
            if (traineeSlot == null)
            {
                return TrainingInheritanceResult.None(null, null);
            }

            int trainedCount = TrainingInheritanceResolver.CountUsedTrainedSlots(saveService);
            if (trainedCount < TrainingSettings.InheritanceParentCount
                || inheritanceSelectView == null)
            {
                if (canvasTransition != null)
                {
                    await canvasTransition.FadeInAsync(cancellationToken);
                }

                return TrainingInheritanceResult.None(traineeSlot.status, traineeSlot.attackMotions);
            }

            // モンスター決定後の暗転を維持し背景とモデルを隠してから継承UIを出す
            if (canvasTransition != null)
            {
                await canvasTransition.FadeOutAsync(cancellationToken);
            }

            SetWorldVisibleForInheritanceSelect(false);

            UniTask<(int parentSlotA, int parentSlotB)> waitParentsTask =
                inheritanceSelectView.WaitForParentsAsync(cancellationToken);

            if (canvasTransition != null)
            {
                await canvasTransition.FadeInAsync(cancellationToken);
            }

            (int parentSlotA, int parentSlotB) = await waitParentsTask;

            if (canvasTransition != null)
            {
                await canvasTransition.FadeOutAsync(cancellationToken);
            }

            inheritanceSelectView.HideForLeave();

            // 継承元選択の戻る操作はモンスター選択へ戻す
            if (parentSlotA < 0 || parentSlotB < 0)
            {
                return null;
            }

            if (parentSlotA == parentSlotB)
            {
                SetWorldVisibleForInheritanceSelect(true);
                if (canvasTransition != null)
                {
                    await canvasTransition.FadeInAsync(cancellationToken);
                }

                return TrainingInheritanceResult.None(traineeSlot.status, traineeSlot.attackMotions);
            }

            ModelSaveSlot parentA = saveService.GetSlot(ModelSavePool.TrainedPlayer, parentSlotA);
            ModelSaveSlot parentB = saveService.GetSlot(ModelSavePool.TrainedPlayer, parentSlotB);
            if (parentA == null || !parentA.isUsed || parentB == null || !parentB.isUsed)
            {
                SetWorldVisibleForInheritanceSelect(true);
                if (canvasTransition != null)
                {
                    await canvasTransition.FadeInAsync(cancellationToken);
                }

                return TrainingInheritanceResult.None(traineeSlot.status, traineeSlot.attackMotions);
            }

            if (InheritancePresentation != null && trainingDisplay != null)
            {
                if (trainingDisplay.LoadedModel != null)
                {
                    trainingDisplay.LoadedModel.SetActive(true);
                }

                TrainingInheritanceResult resolved = TrainingInheritanceResolver.Resolve(
                    traineeSlot.status,
                    traineeSlot.attackMotions,
                    parentA,
                    parentB,
                    trainingDisplay.UsableAttacks,
                    random);

                await InheritancePresentation.PrepareAsync(
                    trainingDisplay.LoadedModel,
                    parentSlotA,
                    parentSlotB,
                    cancellationToken);

                if (canvasTransition != null)
                {
                    await canvasTransition.FadeInAsync(cancellationToken);
                }

                // 明転後に1フレーム待ってから構図と表示を確定する
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);

                try
                {
                    await InheritancePresentation.PlayAsync(cancellationToken);
                    resolved = await PresentInheritanceResultAsync(resolved, cancellationToken);
                }
                finally
                {
                    await InheritancePresentation.FinishAsync(
                        trainingDisplay.LoadedModel,
                        cancellationToken);
                }

                if (canvasTransition != null)
                {
                    await canvasTransition.FadeOutAsync(cancellationToken);
                }

                return resolved;
            }

            SetWorldVisibleForInheritanceSelect(true);
            if (canvasTransition != null)
            {
                await canvasTransition.FadeInAsync(cancellationToken);
            }

            TrainingInheritanceResult fallbackResolved = TrainingInheritanceResolver.Resolve(
                traineeSlot.status,
                traineeSlot.attackMotions,
                parentA,
                parentB,
                trainingDisplay != null ? trainingDisplay.UsableAttacks : null,
                random);
            fallbackResolved = await PresentInheritanceResultAsync(fallbackResolved, cancellationToken);

            if (canvasTransition != null)
            {
                await canvasTransition.FadeOutAsync(cancellationToken);
            }

            return fallbackResolved;
        }

        private async UniTask<TrainingInheritanceResult> PresentInheritanceResultAsync(
            TrainingInheritanceResult result,
            CancellationToken cancellationToken)
        {
            if (result == null || !result.Applied || hudView == null)
            {
                return result;
            }

            string summary = TrainingInheritanceResolver.FormatSummary(result);
            hudView.ShowOverlayMessage(summary);
            await hudView.WaitContinueAsync(cancellationToken);
            hudView.ClearOverlayMessage();

            List<MotionType> attacks = ModelAttackMotionUtility.Normalize(
                result.AttackMotions,
                TrainingSettings.AttackSlotCount);
            MotionType? acceptedA = await OfferInheritedAttackSwapAsync(
                attacks,
                result.InheritedAttackFromParentA,
                cancellationToken);
            MotionType? acceptedB = await OfferInheritedAttackSwapAsync(
                attacks,
                result.InheritedAttackFromParentB,
                cancellationToken);

            hudView.Hide();
            return new TrainingInheritanceResult(
                result.Status,
                attacks,
                result.StatGain,
                acceptedA,
                acceptedB,
                true);
        }

        private async UniTask<MotionType?> OfferInheritedAttackSwapAsync(
            List<MotionType> attacks,
            MotionType? candidate,
            CancellationToken cancellationToken)
        {
            if (!candidate.HasValue || hudView == null || attacks == null)
            {
                return null;
            }

            MotionType learned = candidate.Value;
            if (ContainsAttackMotion(attacks, learned))
            {
                return learned;
            }

            hudView.ShowOverlayMessage(
                $"技「{TrainingAttackTeacher.FormatAttackLabel(learned)}」を覚えるスロットを選んでください");
            await hudView.WaitContinueAsync(cancellationToken);
            hudView.ClearOverlayMessage();

            hudView.ShowAttackSwapChoices(learned, attacks, showSessionPanels: false);
            int replaceIndex = await hudView.WaitAttackSwapChoiceAsync(cancellationToken);
            if (replaceIndex < 0)
            {
                hudView.ShowOverlayMessage(
                    $"「{TrainingAttackTeacher.FormatAttackLabel(learned)}」は覚えませんでした");
                await hudView.WaitContinueAsync(cancellationToken);
                hudView.ClearOverlayMessage();
                return null;
            }

            if (replaceIndex >= attacks.Count)
            {
                hudView.ShowOverlayMessage("入れ替えスロットが無効です");
                await hudView.WaitContinueAsync(cancellationToken);
                hudView.ClearOverlayMessage();
                return null;
            }

            MotionType oldAttack = attacks[replaceIndex];
            attacks[replaceIndex] = learned;
            hudView.ShowOverlayMessage(
                $"技を入れ替えました\nスロット{replaceIndex + 1}: {TrainingAttackTeacher.FormatAttackLabel(oldAttack)}"
                + $" → {TrainingAttackTeacher.FormatAttackLabel(learned)}");
            await hudView.WaitContinueAsync(cancellationToken);
            hudView.ClearOverlayMessage();
            return learned;
        }

        private static bool ContainsAttackMotion(
            IReadOnlyList<MotionType> attacks,
            MotionType motion)
        {
            if (attacks == null)
            {
                return false;
            }

            for (int i = 0; i < attacks.Count; i++)
            {
                if (attacks[i] == motion)
                {
                    return true;
                }
            }

            return false;
        }

        private void SetWorldVisibleForInheritanceSelect(bool visible)
        {
            GameObject loadedModel = trainingDisplay != null ? trainingDisplay.LoadedModel : null;
            if (loadedModel != null)
            {
                loadedModel.SetActive(visible);
            }

            if (visible)
            {
                backgroundView?.ShowDefaultBackground();
                return;
            }

            backgroundView?.HideForLeave();
        }

        private async UniTask ReturnToMonsterSelectionAsync(CancellationToken cancellationToken)
        {
            InheritancePresentation?.HideForLeave();
            inheritanceSelectView?.HideForLeave();
            trainingDisplay?.Clear();
            loadSlotView?.ClearLoadedModelForNewSelection();

            if (canvasTransition != null)
            {
                await canvasTransition.FadeOutAsync(cancellationToken);
            }

            await RevealSelectionAsync(cancellationToken);
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
            return ModelSavePoolSettings.IsValidSlotIndex(ModelSavePool.TrainedPlayer, slotIndex);
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

            for (int i = startPeriodIndex;
                i < periods.Length && !cancellationToken.IsCancellationRequested;
                i++)
            {
                TrainingPeriod period = periods[i];
                int turnNumber = i + 1;

                if (i > startPeriodIndex)
                {
                    // 訓練後のRoam背景は維持するDefaultへ戻すのは休憩や戦闘後のみ
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
                    await RunAfterSchoolPeriodAsync(
                        session,
                        modelName,
                        period,
                        turnNumber,
                        cancellationToken);
                    CheckpointSave(session);
                    continue;
                }

                if (TrainingPeriodCatalog.IsShopPeriod(period))
                {
                    await RunLunchBreakAsync(session, period, turnNumber, cancellationToken);
                    CheckpointSave(session);
                    continue;
                }

                await RunCommandPeriodAsync(session, modelName, period, turnNumber, cancellationToken);
                CheckpointSave(session);
            }
        }

        private async UniTask RunLunchBreakAsync(
            TrainingSession session,
            TrainingPeriod period,
            int turnNumber,
            CancellationToken cancellationToken)
        {
            hudView.BindSession(session, period, turnNumber);
            hudView.HideLocationChoices();
            hudView.SetLogMessage("昼休みになった\n売店で買い物ができる");
            await hudView.WaitContinueAsync(cancellationToken);
            await RunShopVisitAsync(session, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            session.CompletePeriod();
            hudView.BindSession(session, period, turnNumber);
            hudView.SetLogMessage("昼休みが終わった");
            await hudView.WaitContinueAsync(cancellationToken);
        }

        private async UniTask RunCommandPeriodAsync(
            TrainingSession session,
            string modelName,
            TrainingPeriod period,
            int turnNumber,
            CancellationToken cancellationToken)
        {
            while (session.TurnIndexInDay < turnNumber
                && !cancellationToken.IsCancellationRequested)
            {
                hudView.BindSession(session, period, turnNumber);
                System.Collections.Generic.List<TrainingCommandType> commands =
                    TrainingSchedule.BuildHudCommands(session);
                hudView.ShowCommandChoices(commands, session.Stamina);
                TrainingCommandType command =
                    await hudView.WaitCommandChoiceAsync(cancellationToken);

                if (command == TrainingCommandType.Shop)
                {
                    hudView.SetLogMessage("売店は昼休みにだけ利用できる");
                    await hudView.WaitContinueAsync(cancellationToken);
                    continue;
                }

                if (command == TrainingCommandType.UseItem)
                {
                    await RunInventoryVisitAsync(session, cancellationToken);
                    continue;
                }

                TrainingFocus focus = default;
                if (TrainingCommandCatalog.RequiresFocus(command))
                {
                    hudView.ShowFocusChoices(command);
                    TrainingFocus? focusChoice =
                        await hudView.WaitFocusChoiceAsync(cancellationToken);
                    if (focusChoice == null)
                    {
                        continue;
                    }

                    focus = focusChoice.Value;
                }

                TrainingWeekChoice weekChoice = TrainingCommandCatalog.RequiresFocus(command)
                    ? TrainingWeekChoice.FromFocus(command, focus)
                    : TrainingWeekChoice.FromCommand(command);

                switch (weekChoice.Command)
                {
                    case TrainingCommandType.Rest:
                        await RunRestWeekAsync(session, cancellationToken);
                        break;
                    case TrainingCommandType.SpecialTrain:
                        await RunFocusCommandWeekAsync(
                            session,
                            modelName,
                            weekChoice,
                            isSpecial: true,
                            cancellationToken);
                        break;
                    default:
                        await RunFocusCommandWeekAsync(
                            session,
                            modelName,
                            weekChoice,
                            isSpecial: false,
                            cancellationToken);
                        break;
                }
            }
        }

        private async UniTask RunShopVisitAsync(
            TrainingSession session,
            CancellationToken cancellationToken)
        {
            session.EnsureShopOffer(random);

            while (!cancellationToken.IsCancellationRequested)
            {
                System.Collections.Generic.List<TrainingShopItem> offerItems =
                    session.GetShopOfferItems();
                hudView.BindSession(session);
                hudView.ShowShopChoices(
                    offerItems,
                    hasNextPage: false,
                    session.Money,
                    showOpenInventory: true);
                int choice = await hudView.WaitShopChoiceAsync(cancellationToken);
                if (choice == TrainingShopChoiceCodes.Back)
                {
                    return;
                }

                if (choice == TrainingShopChoiceCodes.OpenInventory)
                {
                    await RunInventoryVisitAsync(session, cancellationToken);
                    continue;
                }

                if (choice < 0 || choice >= offerItems.Count)
                {
                    continue;
                }

                TrainingShopPurchaseResult purchase =
                    TrainingShopResolver.TryPurchase(session, offerItems[choice]);
                CheckpointSave(session);
                hudView.BindSession(session);
                hudView.SetLogMessage(purchase.Message);
                await hudView.WaitContinueAsync(cancellationToken);
            }
        }

        private async UniTask RunInventoryVisitAsync(
            TrainingSession session,
            CancellationToken cancellationToken)
        {
            int page = 0;
            int pageSize = TrainingSettings.ShopPageSize;

            while (!cancellationToken.IsCancellationRequested)
            {
                System.Collections.Generic.List<TrainingInventoryEntryView> allEntries =
                    session.BuildInventoryViews();
                int pageCount = pageSize <= 0
                    ? 0
                    : (allEntries.Count + pageSize - 1) / pageSize;
                if (pageCount <= 0)
                {
                    pageCount = 1;
                }

                if (page >= pageCount)
                {
                    page = 0;
                }

                var pageEntries = new System.Collections.Generic.List<TrainingInventoryEntryView>(
                    pageSize);
                int start = page * pageSize;
                for (int i = start; i < allEntries.Count && pageEntries.Count < pageSize; i++)
                {
                    pageEntries.Add(allEntries[i]);
                }

                bool hasNext = allEntries.Count > pageSize;
                hudView.BindSession(session);
                hudView.ShowInventoryChoices(pageEntries, hasNext);
                int choice = await hudView.WaitInventoryChoiceAsync(cancellationToken);
                if (choice == TrainingInventoryChoiceCodes.Back)
                {
                    return;
                }

                if (choice == TrainingInventoryChoiceCodes.NextPage)
                {
                    page = (page + 1) % pageCount;
                    continue;
                }

                if (choice < 0 || choice >= pageEntries.Count)
                {
                    continue;
                }

                TrainingItemUseResult useResult =
                    TrainingShopResolver.TryUseItem(session, pageEntries[choice].ItemId);
                CheckpointSave(session);
                hudView.BindSession(session);
                hudView.SetLogMessage(useResult.Message);
                await hudView.WaitContinueAsync(cancellationToken);
            }
        }

        private async UniTask RunRestWeekAsync(
            TrainingSession session,
            CancellationToken cancellationToken)
        {
            TrainingActionResult result = TrainingActionResolver.ExecuteRest(session.Stamina);
            StopMonsterRoam();
            await TransitionTurnAsync(
                cancellationToken,
                () =>
                {
                    hudView.HideLocationChoices();
                    ApplyDefaultDestinationPresentation();
                });
            session.ApplyAction(result);
            CheckpointSave(session);
            hudView.BindSession(session);
            hudView.SetLogMessage(BuildActionLog(result));
            await hudView.WaitContinueAsync(cancellationToken);
        }

        private async UniTask RunFocusCommandWeekAsync(
            TrainingSession session,
            string modelName,
            TrainingWeekChoice weekChoice,
            bool isSpecial,
            CancellationToken cancellationToken)
        {
            TrainingActionResult result = isSpecial
                ? TrainingActionResolver.ExecuteSpecialTrain(
                    weekChoice.Focus,
                    session,
                    random)
                : TrainingActionResolver.ExecuteTrain(
                    weekChoice.Focus,
                    session,
                    random);

            await TransitionTurnAsync(
                cancellationToken,
                () =>
                {
                    hudView.HideLocationChoices();
                    ApplyRoamDestinationPresentation();
                    hudView.Hide();
                });

            StartMonsterRoam(cancellationToken);
            hudView.Show();
            session.ApplyAction(result);
            CheckpointSave(session);
            hudView.BindSession(session);
            hudView.SetLogMessage(BuildActionLog(result));
            await hudView.WaitContinueAsync(cancellationToken);

            if (result.Succeeded)
            {
                bool ambushRan = await TryRunAmbushEventAsync(
                    session,
                    modelName,
                    cancellationToken);
                if (!ambushRan)
                {
                    await TryRunRandomEventAsync(session, cancellationToken);
                }

                CheckpointSave(session);
            }
        }

        private async UniTask<bool> TryRunAmbushEventAsync(
            TrainingSession session,
            string modelName,
            CancellationToken cancellationToken)
        {
            if (!TrainingEventResolver.TryRollAmbushEvent(random))
            {
                return false;
            }

            if (ambushView == null)
            {
                Debug.LogError("[TrainingFlowRunner] ITrainingAmbushViewが未注入です");
                return false;
            }

            if (!TrainingEnemyResolver.TryPickAmbushEnemySlotIndex(
                    saveService,
                    out int enemySlotIndex))
            {
                return false;
            }

            string enemyName = saveService.GetSlot(ModelSavePool.Enemy, enemySlotIndex)?.modelName
                ?? "強敵";

            await ambushView.PlayAlertAsync(cancellationToken);
            ambushView.ShowChoice(enemyName);
            TrainingAmbushChoice choice = await ambushView.WaitChoiceAsync(cancellationToken);
            ambushView.Hide();

            if (choice == TrainingAmbushChoice.Flee)
            {
                hudView.SetLogMessage($"【強敵急襲】\n{enemyName}から逃げ出した");
                await hudView.WaitContinueAsync(cancellationToken);
                return true;
            }

            await RunAmbushBattleAsync(
                session,
                modelName,
                enemySlotIndex,
                enemyName,
                cancellationToken);
            return true;
        }

        private async UniTask RunAmbushBattleAsync(
            TrainingSession session,
            string modelName,
            int enemySlotIndex,
            string enemyName,
            CancellationToken cancellationToken)
        {
            GameObject playerModel = trainingDisplay != null ? trainingDisplay.LoadedModel : null;
            if (battleRunner == null || playerModel == null)
            {
                Debug.LogError("[TrainingFlowRunner] 強敵急襲の戦闘を開始できませんでした");
                hudView.SetLogMessage("強敵急襲の戦闘を開始できませんでした");
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

            StopMonsterRoam();
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

            hudView.BindSession(session);
            ApplyDefaultDestinationPresentation();

            if (!battleResult.Played)
            {
                hudView.SetLogMessage("強敵急襲の戦闘を開始できませんでした");
                await hudView.WaitContinueAsync(cancellationToken);
                return;
            }

            if (battleResult.PlayerWon)
            {
                TrainingStatGain victoryGain = TrainingEventResolver.CreateAmbushVictoryGain();
                session.ApplyEventStatGain(victoryGain);
                session.AddMoney(TrainingSettings.AmbushVictoryReward);
                hudView.BindSession(session);
                hudView.SetLogMessage(
                    $"【強敵急襲】\n{enemyName}に勝利した"
                    + $"\n賞金+{TrainingSettings.AmbushVictoryReward}G"
                    + $"\nHP+{victoryGain.Hp} 攻撃+{victoryGain.Attack} 防御+{victoryGain.Defense}"
                    + $" 速度+{victoryGain.Speed} 命中+{victoryGain.Hit}");
            }
            else
            {
                hudView.SetLogMessage($"【強敵急襲】\n{enemyName}に敗北した");
            }

            await hudView.WaitContinueAsync(cancellationToken);
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

            if (!TrainingEnemyResolver.TryPickEnemySlotIndex(
                    saveService,
                    session.CurrentDay,
                    out int enemySlotIndex))
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

            StopMonsterRoam();
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
                int reward = TrainingShopResolver.ResolveTournamentReward((int)session.CurrentDay);
                session.ApplyAfterSchoolVictoryRecovery();
                session.AddMoney(reward);
                hudView.BindSession(session, period, turnNumber);
                session.RefreshShopOffer(random);
                hudView.SetLogMessage(
                    $"放課後の戦闘に勝利した\n体力+{TrainingSettings.AfterSchoolVictoryStaminaRecovery}"
                    + $" 賞金+{reward}G\n売店の商品が入れ替わった");
            }
            else
            {
                session.RefreshShopOffer(random);
                hudView.SetLogMessage("放課後の戦闘に敗北した\n売店の商品が入れ替わった");
            }

            await hudView.WaitContinueAsync(cancellationToken);
            session.CompletePeriod();
        }

        private async UniTask TryRunRandomEventAsync(
            TrainingSession session,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<MotionType> usableAttacks = ResolveUsableAttacks(session);

            if (TrainingEventResolver.TryRollLearnAttackEvent(
                random,
                session.AttackMotions,
                usableAttacks,
                out TrainingEventOutcome learnOutcome))
            {
                await RunLearnAttackEventAsync(session, learnOutcome, cancellationToken);
                return;
            }

            if (!TrainingEventResolver.TryRollStatBoostEvent(random, out TrainingEventOutcome statOutcome))
            {
                return;
            }

            hudView.SetLogMessage($"【{statOutcome.Title}】\n{statOutcome.Message}");
            await hudView.WaitContinueAsync(cancellationToken);
            session.ApplyEventStatGain(statOutcome.StatGain);
            hudView.BindSession(session);
        }

        private async UniTask TryRunLearnAttackOnlyAsync(
            TrainingSession session,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<MotionType> usableAttacks = ResolveUsableAttacks(session);
            if (!TrainingEventResolver.TryRollLearnAttackEvent(
                    random,
                    session.AttackMotions,
                    usableAttacks,
                    out TrainingEventOutcome learnOutcome))
            {
                // 出会いマスは必ず技候補を出す
                if (!TrainingAttackTeacher.TryPickLearnableAttack(
                        session.AttackMotions,
                        usableAttacks,
                        random,
                        out MotionType learned))
                {
                    hudView.SetLogMessage("出会いがあったが新しい技はなかった");
                    await hudView.WaitContinueAsync(cancellationToken);
                    return;
                }

                learnOutcome = new TrainingEventOutcome(
                    TrainingEventType.LearnAttack,
                    "出会い",
                    $"新しい技「{TrainingAttackTeacher.FormatAttackLabel(learned)}」を覚えるチャンスです",
                    default,
                    learned);
            }

            await RunLearnAttackEventAsync(session, learnOutcome, cancellationToken);
        }

        private async UniTask RunLearnAttackEventAsync(
            TrainingSession session,
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
            hudView.BindSession(session);
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
            string commandName = TrainingCommandCatalog.GetDisplayName(result.Command);
            if (!result.Succeeded)
            {
                return result.FailedByLowStamina
                    ? $"{commandName}は体力不足で失敗した"
                    : $"{commandName}は失敗した";
            }

            if (result.IsRestAction)
            {
                return $"休憩で体力全回復 {result.StaminaBefore}→{result.StaminaAfter}";
            }

            TrainingStatGain gain = result.AppliedGain;
            string outcome = result.IsGreatSuccess ? "大成功" : "成功";
            if (result.Command == TrainingCommandType.Train
                || result.Command == TrainingCommandType.SpecialTrain)
            {
                return $"{commandName}{outcome} {TrainingFocusCatalog.GetDisplayName(result.Focus)}"
                    + $"\nHP+{gain.Hp} 攻撃+{gain.Attack} 防御+{gain.Defense} 速度+{gain.Speed} 命中+{gain.Hit}";
            }

            return $"{commandName}{outcome}"
                + $"\nHP+{gain.Hp} 攻撃+{gain.Attack} 防御+{gain.Defense} 速度+{gain.Speed} 命中+{gain.Hit}";
        }

        private void ApplyDefaultDestinationPresentation()
        {
            StopMonsterRoam();
            backgroundView?.ShowDefaultBackground();
            locationCameraView?.ApplyDefaultView();
        }

        private void ApplyRoamDestinationPresentation()
        {
            backgroundView?.ShowRoamBackground();
            locationCameraView?.ApplyRoamView();
        }

        private void StartMonsterRoam(CancellationToken cancellationToken)
        {
            EnsureMonsterRoamController();
            monsterRoamController?.StartRoam(cancellationToken);
        }

        private void StopMonsterRoam()
        {
            EnsureMonsterRoamController();
            monsterRoamController?.StopRoam();
        }

        private void EnsureMonsterRoamController()
        {
            if (monsterRoamController != null)
            {
                return;
            }

            if (trainingDisplay == null)
            {
                Debug.LogError(
                    "[TrainingFlowRunner] TrainingDisplayが未配線のため徘徊を開始できません",
                    this);
                return;
            }

            monsterRoamController = trainingDisplay.GetComponent<TrainingMonsterRoamController>();
            if (monsterRoamController == null)
            {
                Debug.LogError(
                    "[TrainingFlowRunner] TrainingMonsterRoamControllerが未配線です"
                    + " TrainingDisplayへ追加して接続してください",
                    this);
            }
        }

        private void ApplyDestinationBackground(TrainingLocation location, bool isRest)
        {
            if (isRest)
            {
                backgroundView?.ShowRestBackground();
                locationCameraView?.ApplyRestView();
                return;
            }

            backgroundView?.ShowLocationBackground(location);
            locationCameraView?.ApplyLocationView(location);
        }

        private void ApplyDestinationBackground(TrainingTurnChoice turnChoice)
        {
            ApplyDestinationBackground(turnChoice.Location, turnChoice.IsRest);
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
