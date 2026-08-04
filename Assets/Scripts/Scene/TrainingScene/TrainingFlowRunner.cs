using Localization;
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
    public sealed class TrainingFlowRunner : MonoBehaviour, ILanguageAwareUi
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
        [SerializeField] private TrainingMotivationBallPlayController motivationBallPlay;
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
        private ISkillTreeService skillTreeService;
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
            ISkillTreeService skillTreeService,
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
            this.skillTreeService = skillTreeService;
            this.battleRunner = battleRunner;
            ApplySelectionBackToTitleLabel();
        }

        /// <inheritdoc/>
        public void RefreshLocalizedUi()
        {
            ApplySelectionBackToTitleLabel();
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
        /// PlayMode中の進行中Runnerを探す
        /// </summary>
        public static TrainingFlowRunner FindActiveRunner()
        {
            if (activeRunner != null && activeRunner.isRunning && activeRunner.activeSession != null)
            {
                return activeRunner;
            }

            TrainingFlowRunner[] runners =
                UnityEngine.Object.FindObjectsByType<TrainingFlowRunner>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
            for (int i = 0; i < runners.Length; i++)
            {
                TrainingFlowRunner runner = runners[i];
                if (runner != null && runner.isRunning && runner.activeSession != null)
                {
                    return runner;
                }
            }

            return null;
        }

        /// <summary>
        /// 進行中セッションへデバッグ用に所持アイテムを追加する
        /// </summary>
        /// <param name="itemId">商品ID</param>
        /// <param name="count">追加個数</param>
        /// <param name="message">結果メッセージ</param>
        /// <returns>追加できたか</returns>
        public bool TryDebugAddInventoryItem(string itemId, int count, out string message)
        {
            if (!isRunning || activeSession == null || activeSession.IsCompleted)
            {
                message = LocalizedText.GetOrFallback(
                    GameTextKeys.TrainingDebugNoSession,
                    "進行中の育成セッションがありません");
                return false;
            }

            if (!TrainingShopCatalog.TryGetById(itemId, out TrainingShopItem item))
            {
                message = LocalizedText.GetOrFallback(
                    GameTextKeys.TrainingDebugUnknownItem,
                    "未知のアイテムIDです: {id}",
                    "id",
                    itemId);
                return false;
            }

            int addCount = Mathf.Max(1, count);
            activeSession.AddInventoryItem(item.Id, addCount);
            CheckpointSave(activeSession);
            hudView?.BindSession(activeSession);
            message =
                LocalizedText.GetOrFallback(
                    GameTextKeys.TrainingDebugItemAdded,
                    "{name}を{count}個追加しました",
                    new System.Collections.Generic.Dictionary<string, object>
                    {
                        { "name", item.DisplayName },
                        { "count", addCount },
                    })
                + $" slot={activeSession.PlayerSlotIndex}";
            return true;
        }

        /// <summary>
        /// デバッグ表示用の所持一覧を返す
        /// </summary>
        public IReadOnlyList<TrainingInventoryEntryView> DebugGetInventoryViews()
        {
            if (activeSession == null || activeSession.IsCompleted)
            {
                return System.Array.Empty<TrainingInventoryEntryView>();
            }

            return activeSession.BuildInventoryViews();
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
                // 読込失敗で途中データを消すと再開不能になるため保持する
                Debug.LogError(
                    $"[TrainingFlowRunner] 再開用モデルの読込に失敗したため途中データを保持します slot={slotIndex}");
                trainingDisplay.Clear();
                await RevealSelectionAsync(cancellationToken);
                return false;
            }

            if (IsTrainingStartProgress(progress))
            {
                ApplyDefaultDestinationPresentation();
            }
            else
            {
                ApplyRoamDestinationPresentation();
            }

            hudView.ShowOverlayHost();
            hudView.ShowResumeChoices(TrainingSlotProgressMapper.BuildResumePresentation(slot, progress));

            if (canvasTransition != null)
            {
                await canvasTransition.FadeInAsync(cancellationToken);
            }

            TrainingResumeChoice resumeChoice = await hudView.WaitResumeChoiceAsync(cancellationToken);
            if (resumeChoice == TrainingResumeChoice.Unavailable)
            {
                Debug.LogError(
                    "[TrainingFlowRunner] 再開UIが使えないため途中データを保持したまま選択画面へ戻ります");
                trainingDisplay.Clear();
                hudView.Hide();
                await RevealSelectionAsync(cancellationToken);
                return false;
            }

            if (resumeChoice == TrainingResumeChoice.Restart)
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
                Debug.LogError(
                    $"[TrainingFlowRunner] 再開確定後のモデル再読込に失敗したため途中データを保持します slot={slotIndex}");
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
            if (skillTreeService != null)
            {
                session.ApplySkillTreeRuntimeBonuses(skillTreeService.Bonuses);
            }

            if (!IsTrainingStartProgress(progress))
            {
                ApplyRoamDestinationPresentation();
                StartMonsterRoam(cancellationToken);
            }

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
            hudView.ShowOverlayMessage(LocalizedText.GetOrFallback(GameTextKeys.TrainingLogAutoTraining, "自動育成を実行中..."));
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

            (bool saved, string saveResultKey, string saveResultFallback) =
                await SaveTrainedResultAsync(
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
                saveResultKey,
                saveResultFallback,
                GameTextKeys.TrainingLogReturnToTitle, "タイトル",
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

            (bool saved, _, _) = await SaveTrainedResultAsync(
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
            // 途中データの削除は再開UIで最初からを選んだときだけ行う
            // ここで消すと再開失敗時に進行データが消える

            TrainingSession session;
            if (inheritance != null && inheritance.Applied)
            {
                session = new TrainingSession(
                    slotIndex,
                    inheritance.Status,
                    SanitizeAttacks(inheritance.AttackMotions));
            }
            else
            {
                session = new TrainingSession(
                    slotIndex,
                    slot.status,
                    SanitizeAttacks(slot.attackMotions));
            }

            if (skillTreeService != null)
            {
                session.ApplySkillTreeBonuses(skillTreeService.Bonuses);
            }

            return session;
        }

        private IReadOnlyList<MotionType> SanitizeAttacks(IReadOnlyList<MotionType> attacks)
        {
            IReadOnlyList<MotionType> usable = trainingDisplay != null
                ? trainingDisplay.UsableAttacks
                : null;
            return ModelAttackMotionUtility.SanitizeForUsableAttacks(
                attacks,
                usable,
                TrainingSettings.AttackSlotCount);
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
                // 明転前に継承背景を先に出し読み込み中の露出を防ぐ
                backgroundView?.ShowInheritanceBackground();

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
                    random,
                    skillTreeService != null
                        ? skillTreeService.Bonuses.InheritancePercentBonus
                        : 0);

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
                random,
                skillTreeService != null
                    ? skillTreeService.Bonuses.InheritancePercentBonus
                    : 0);
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

            // 空きスロットがあれば入れ替えずにそのまま覚える
            if (attacks.Count < TrainingSettings.AttackSlotCount)
            {
                attacks.Add(learned);
                hudView.ShowOverlayMessage(
                    LocalizedText.Get(
                        GameTextKeys.TrainingLearnedMove,
                        new System.Collections.Generic.Dictionary<string, object>
                        {
                            { "slot", attacks.Count },
                            { "name", TrainingAttackTeacher.FormatAttackName(learned) },
                        }));
                await hudView.WaitContinueAsync(cancellationToken);
                hudView.ClearOverlayMessage();
                return learned;
            }

            hudView.ShowOverlayMessage(
                LocalizedText.Get(
                    GameTextKeys.TrainingChooseLearnSlot,
                    "name",
                    TrainingAttackTeacher.FormatAttackName(learned)));
            await hudView.WaitContinueAsync(cancellationToken);
            hudView.ClearOverlayMessage();

            hudView.ShowAttackSwapChoices(learned, attacks, showSessionPanels: false);
            int replaceIndex = await hudView.WaitAttackSwapChoiceAsync(cancellationToken);
            if (replaceIndex < 0)
            {
                hudView.ShowOverlayMessage(
                    LocalizedText.Get(
                        GameTextKeys.TrainingChooseLearnSlot,
                        "name",
                        TrainingAttackTeacher.FormatAttackName(learned)));
                await hudView.WaitContinueAsync(cancellationToken);
                hudView.ClearOverlayMessage();
                return null;
            }

            if (replaceIndex >= attacks.Count)
            {
                hudView.ShowOverlayMessage(LocalizedText.Get(GameTextKeys.TrainingInvalidSwapSlot));
                await hudView.WaitContinueAsync(cancellationToken);
                hudView.ClearOverlayMessage();
                return null;
            }

            MotionType oldAttack = attacks[replaceIndex];
            attacks[replaceIndex] = learned;
            hudView.ShowOverlayMessage(
                LocalizedText.Get(
                    GameTextKeys.TrainingSwappedMove,
                    new System.Collections.Generic.Dictionary<string, object>
                    {
                        { "slot", replaceIndex + 1 },
                        { "name", TrainingAttackTeacher.FormatAttackName(oldAttack) },
                    }));
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
            hudView.SetLogMessage(LocalizedText.GetOrFallback(GameTextKeys.TrainingLogModelLoadFailed, "モデルの読み込みに失敗しました"));
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
            if (selectionBackToTitleButton == null)
            {
                return;
            }

            if (visible)
            {
                ApplySelectionBackToTitleLabel();
            }

            selectionBackToTitleButton.gameObject.SetActive(visible);
        }

        private void ApplySelectionBackToTitleLabel()
        {
            if (selectionBackToTitleButton == null)
            {
                return;
            }

            LhButtonLabelUtility.SetLabel(
                selectionBackToTitleButton,
                LocalizedText.GetOrFallback(GameTextKeys.TrainingHudBackToTitle, "タイトル"));
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
                            LocalizedText.GetOrFallback(
                                GameTextKeys.TrainingHudDayStarted,
                                "{day}が始まりました",
                                "day",
                                TrainingDayCatalog.GetDisplayName(session.CurrentDay)));
                    });
                await hudView.WaitContinueAsync(cancellationToken);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await RunTrainingCompletionFlowAsync(session, modelName, cancellationToken);
        }

        private async UniTask<(bool saved, string messageKey, string messageFallback)>
            SaveTrainedResultAsync(
                TrainingSession session,
                int trainedSlotIndex,
                string modelName,
                CancellationToken cancellationToken)
        {
            if (!IsValidTrainedSlotIndex(trainedSlotIndex))
            {
                return (
                    false,
                    GameTextKeys.TrainingSaveDestNotSelected,
                    "育成済みデータの保存先が選ばれませんでした");
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
                return (true, null, string.Empty);
            }

            Debug.LogError(
                $"[TrainingFlowRunner] 育成済みデータの保存に失敗しました trainedSlot={trainedSlotIndex} playerSlot={session.PlayerSlotIndex}");
            return (
                false,
                GameTextKeys.TrainingSaveFailed,
                "育成済みデータの保存に失敗しました\n未育成データは保持されています");
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
            hudView.SetLogMessage(LocalizedText.GetOrFallback(GameTextKeys.TrainingLogLunchStart, "昼休みになった\n売店で買い物ができる"));
            await hudView.WaitContinueAsync(cancellationToken);
            await RunShopVisitAsync(session, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            session.CompletePeriod();
            hudView.BindSession(session, period, turnNumber);
            hudView.SetLogMessage(LocalizedText.GetOrFallback(GameTextKeys.TrainingLogLunchEnd, "昼休みが終わった"));
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
                    hudView.SetLogMessage(LocalizedText.GetOrFallback(GameTextKeys.TrainingLogShopOnlyLunch, "売店は昼休みにだけ利用できる"));
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
                    IReadOnlyList<TrainingFocus> offeredFocuses =
                        session.GetOrRollOfferedFocuses(random);
                    hudView.ShowFocusChoices(command, offeredFocuses);
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

                // 次ページは陳列入れ替えに使わない
                if (choice == TrainingShopChoiceCodes.NextPage)
                {
                    continue;
                }

                if (choice < 0 || choice >= offerItems.Count)
                {
                    continue;
                }

                TrainingShopItem selectedItem = offerItems[choice];
                if (string.IsNullOrEmpty(selectedItem.Id))
                {
                    continue;
                }

                TrainingShopPurchaseResult purchase =
                    TrainingShopResolver.TryPurchase(session, selectedItem);
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

                if (useResult.Succeeded
                    && useResult.Item.ItemType == TrainingShopItemType.MotivationBoost)
                {
                    await PlayMotivationBallAsync(
                        useResult.Item,
                        useResult.Message,
                        cancellationToken);
                    continue;
                }

                hudView.SetLogMessage(useResult.Message);
                await hudView.WaitContinueAsync(cancellationToken);
            }
        }

        private async UniTask PlayMotivationBallAsync(
            TrainingShopItem item,
            string resultMessage,
            CancellationToken cancellationToken)
        {
            EnsureMotivationBallPlay();
            ApplyRoamDestinationPresentation();
            StartMonsterRoam(cancellationToken);
            hudView.HideLocationChoices();
            hudView.SetLogMessage(LocalizedText.GetOrFallback(GameTextKeys.TrainingLogBallThrowHint, "ボールを長押しして離すと投げる"));
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);

            if (motivationBallPlay != null)
            {
                TrainingMotivationBallPlayController.ResolveBallVisual(
                    item.Id,
                    out string resourcePath,
                    out float visualScale);
                await motivationBallPlay.PlayAsync(
                    resourcePath,
                    visualScale,
                    cancellationToken);
            }
            else
            {
                Debug.LogError(
                    "[TrainingFlowRunner] motivationBallPlayが未配線です",
                    this);
            }

            hudView.BindSession(activeSession);
            hudView.SetLogMessage(resultMessage);
            await hudView.WaitContinueAsync(cancellationToken);
        }

        private void EnsureMotivationBallPlay()
        {
            if (motivationBallPlay != null)
            {
                return;
            }

            if (trainingDisplay != null)
            {
                motivationBallPlay =
                    trainingDisplay.GetComponent<TrainingMotivationBallPlayController>();
            }

            if (motivationBallPlay == null)
            {
                Debug.LogError(
                    "[TrainingFlowRunner] TrainingMotivationBallPlayControllerが未配線です"
                    + " TrainingDisplayへ追加して接続してください",
                    this);
            }
        }

        private async UniTask RunRestWeekAsync(
            TrainingSession session,
            CancellationToken cancellationToken)
        {
            TrainingActionResult result = TrainingActionResolver.ExecuteRest(session.Stamina, random);
            await TransitionTurnAsync(
                cancellationToken,
                () =>
                {
                    hudView.HideLocationChoices();

                    // 徘徊位置の切り替えを見せないため暗転中に行う
                    ApplyRoamDestinationPresentation();
                    StartMonsterRoam(cancellationToken);
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

                    // スポーン位置の切り替えを見せないため暗転中に開始する
                    StartMonsterRoam(cancellationToken);
                });

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
                    session.CurrentDay,
                    out int enemySlotIndex))
            {
                return false;
            }

            string fallbackModelName = saveService.GetSlot(ModelSavePool.Enemy, enemySlotIndex)?.modelName;
            if (string.IsNullOrEmpty(fallbackModelName))
            {
                fallbackModelName = LocalizedText.Get(GameTextKeys.TrainingAmbushEnemyDefault);
            }

            string enemyName = EnemyDisplayName.Resolve(enemySlotIndex, fallbackModelName);

            await ambushView.PlayAlertAsync(cancellationToken);
            ambushView.ShowChoice(enemySlotIndex, fallbackModelName);
            TrainingAmbushChoice choice = await ambushView.WaitChoiceAsync(cancellationToken);
            ambushView.Hide();

            if (choice == TrainingAmbushChoice.Flee)
            {
                hudView.SetLogMessage(LocalizedText.Get(GameTextKeys.TrainingFleeAmbush, "name", enemyName));
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
                hudView.SetLogMessage(LocalizedText.Get(GameTextKeys.TrainingAmbushStartFailed));
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
                    TrainingEnemyResolver.ResolveAmbushTier(session.CurrentDay),
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
            ApplyRoamDestinationPresentation();
            StartMonsterRoam(cancellationToken);

            if (!battleResult.Played)
            {
                hudView.SetLogMessage(LocalizedText.Get(GameTextKeys.TrainingAmbushStartFailed));
                await FadeInAfterBattleResultReadyAsync(cancellationToken);
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
                    LocalizedText.Get(GameTextKeys.TrainingWinAmbush, "name", enemyName)
                    + LocalizedText.GetOrFallback(
                        GameTextKeys.TrainingLogAmbushWinTail,
                        "\n賞金+{reward}G\nHP+{hp} 攻撃+{atk} 防御+{def} 速度+{spd} 命中+{hit}",
                        new Dictionary<string, object>
                        {
                            { "reward", TrainingSettings.AmbushVictoryReward },
                            { "hp", victoryGain.Hp },
                            { "atk", victoryGain.Attack },
                            { "def", victoryGain.Defense },
                            { "spd", victoryGain.Speed },
                            { "hit", victoryGain.Hit },
                        }));
            }
            else
            {
                session.LowerMotivation(1);
                hudView.BindSession(session);
                hudView.SetLogMessage(LocalizedText.Get(GameTextKeys.TrainingLoseAmbush, "name", enemyName));
            }

            await FadeInAfterBattleResultReadyAsync(cancellationToken);
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
                hudView.SetLogMessage(LocalizedText.GetOrFallback(GameTextKeys.TrainingLogAfterSchoolNoOpponent, "放課後の対戦相手が見つかりませんでした"));
                session.CompletePeriod();
                await hudView.WaitContinueAsync(cancellationToken);
                return;
            }

            string enemyName = ResolveEnemyDisplayName(enemySlotIndex);
            hudView.SetLogMessage(LocalizedText.GetOrFallback(GameTextKeys.TrainingLogAfterSchoolStart, "放課後の戦闘\n{name}との対戦が始まります", "name", enemyName));
            await hudView.WaitContinueAsync(cancellationToken);

            GameObject playerModel = trainingDisplay != null ? trainingDisplay.LoadedModel : null;
            if (battleRunner == null)
            {
                Debug.LogError("[TrainingFlowRunner] TrainingBattleRunnerが未注入です");
                hudView.SetLogMessage(LocalizedText.GetOrFallback(GameTextKeys.TrainingLogAfterSchoolStartFailed, "放課後の戦闘を開始できませんでした"));
                session.CompletePeriod();
                await hudView.WaitContinueAsync(cancellationToken);
                return;
            }

            if (playerModel == null)
            {
                Debug.LogError("[TrainingFlowRunner] 表示中のプレイヤーモデルがありません");
                hudView.SetLogMessage(LocalizedText.GetOrFallback(GameTextKeys.TrainingLogAfterSchoolStartFailed, "放課後の戦闘を開始できませんでした"));
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
                    TrainingEnemyResolver.ResolveAfterSchoolTier(session.CurrentDay),
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
                ApplyPostBattleDestinationPresentation(session, cancellationToken);
                hudView.SetLogMessage(LocalizedText.GetOrFallback(GameTextKeys.TrainingLogAfterSchoolStartFailed, "放課後の戦闘を開始できませんでした"));
                session.CompletePeriod();
                await FadeInAfterBattleResultReadyAsync(cancellationToken);
                await hudView.WaitContinueAsync(cancellationToken);
                return;
            }

            ApplyPostBattleDestinationPresentation(session, cancellationToken);

            if (battleResult.PlayerWon)
            {
                int reward = TrainingShopResolver.ResolveTournamentReward((int)session.CurrentDay);
                session.ApplyAfterSchoolVictoryRecovery();
                if (reward > 0)
                {
                    session.AddMoney(reward);
                }

                hudView.BindSession(session, period, turnNumber);
                session.RefreshShopOffer(random);
                string rewardText = reward > 0
                    ? LocalizedText.GetOrFallback(
                        GameTextKeys.TrainingLogRewardMoney,
                        " 賞金+{reward}G",
                        "reward",
                        reward)
                    : string.Empty;
                hudView.SetLogMessage(
                    LocalizedText.GetOrFallback(
                        GameTextKeys.TrainingLogAfterSchoolWin,
                        "放課後の戦闘に勝利した\n体力+{stamina}",
                        "stamina",
                        TrainingSettings.AfterSchoolVictoryStaminaRecovery)
                    + rewardText);
            }
            else
            {
                session.LowerMotivation(1);
                session.RefreshShopOffer(random);
                hudView.BindSession(session, period, turnNumber);
                hudView.SetLogMessage(
                    LocalizedText.GetOrFallback(
                        GameTextKeys.TrainingLoseAfterSchool,
                        "放課後の戦闘に敗北した\nやる気が下がった"));
            }

            await FadeInAfterBattleResultReadyAsync(cancellationToken);
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
                    hudView.SetLogMessage(LocalizedText.GetOrFallback(GameTextKeys.TrainingLogNoNewMove, "出会いがあったが新しい技はなかった"));
                    await hudView.WaitContinueAsync(cancellationToken);
                    return;
                }

                learnOutcome = new TrainingEventOutcome(
                    TrainingEventType.LearnAttack,
                    LocalizedText.GetOrFallback(GameTextKeys.TrainingLogMeetTitle, "出会い"),
                    LocalizedText.GetOrFallback(
                        GameTextKeys.TrainingLogMeetLearnChance,
                        "新しい技「{name}」を覚えるチャンスです",
                        "name",
                        TrainingAttackTeacher.FormatAttackName(learned)),
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

            // 空きスロットがあれば入れ替えずにそのまま覚える
            if (session.AttackMotions.Count < TrainingSettings.AttackSlotCount
                && session.TryAddAttack(outcome.LearnedAttack))
            {
                hudView.SetLogMessage(
                    LocalizedText.Get(
                        GameTextKeys.TrainingLearnedMove,
                        new System.Collections.Generic.Dictionary<string, object>
                        {
                            { "slot", session.AttackMotions.Count },
                            { "name", TrainingAttackTeacher.FormatAttackName(outcome.LearnedAttack) },
                        }));
                await hudView.WaitContinueAsync(cancellationToken);
                hudView.BindSession(session);
                return;
            }

            hudView.ShowAttackSwapChoices(outcome.LearnedAttack, session.AttackMotions);
            int replaceIndex = await hudView.WaitAttackSwapChoiceAsync(cancellationToken);
            if (replaceIndex < 0)
            {
                hudView.SetLogMessage(
                    LocalizedText.Get(
                        GameTextKeys.TrainingChooseLearnSlot,
                        "name",
                        TrainingAttackTeacher.FormatAttackName(outcome.LearnedAttack)));
                await hudView.WaitContinueAsync(cancellationToken);
                return;
            }

            MotionType oldAttack = session.AttackMotions[replaceIndex];
            if (session.TryReplaceAttack(replaceIndex, outcome.LearnedAttack))
            {
                hudView.SetLogMessage(
                    LocalizedText.Get(
                        GameTextKeys.TrainingSwappedMove,
                        new System.Collections.Generic.Dictionary<string, object>
                        {
                            { "slot", replaceIndex + 1 },
                            { "name", TrainingAttackTeacher.FormatAttackName(oldAttack) },
                        }));
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
                Debug.LogError("[TrainingFlowRunner] 育成途中データへ変換できませんでした");
                return;
            }

            if (saveService == null)
            {
                Debug.LogError("[TrainingFlowRunner] saveServiceが未注入のため育成途中データを保存できません");
                return;
            }

            if (!saveService.SaveTrainingProgress(ModelSavePool.Player, session.PlayerSlotIndex, progress))
            {
                Debug.LogError(
                    $"[TrainingFlowRunner] 育成途中データの保存に失敗しました slot={session.PlayerSlotIndex}");
            }
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
                string failReason = result.FailedByLowStamina
                    ? LocalizedText.GetOrFallback(
                        GameTextKeys.TrainingLogActionFailLowStamina,
                        "{name}は体力不足で失敗した",
                        "name",
                        commandName)
                    : LocalizedText.GetOrFallback(
                        GameTextKeys.TrainingLogActionFail,
                        "{name}は失敗した",
                        "name",
                        commandName);
                return failReason
                    + "\n"
                    + LocalizedText.GetOrFallback(
                        GameTextKeys.TrainingLogMotivationDown,
                        "やる気が下がった");
            }

            if (result.IsRestAction)
            {
                string restOutcome = result.IsGreatSuccess
                    ? LocalizedText.GetOrFallback(GameTextKeys.TrainingLogRestGreat, "休憩大成功")
                    : LocalizedText.GetOrFallback(GameTextKeys.TrainingLogRestOk, "休憩");
                string restLog = LocalizedText.GetOrFallback(
                    GameTextKeys.TrainingLogRestRecover,
                    "{title}で体力回復 {before}→{after}",
                    new Dictionary<string, object>
                    {
                        { "title", restOutcome },
                        { "before", result.StaminaBefore },
                        { "after", result.StaminaAfter },
                    });
                if (result.MotivationGain > 0)
                {
                    restLog += "\n"
                        + LocalizedText.GetOrFallback(
                            GameTextKeys.TrainingLogMotivationUp,
                            "やる気が上がった");
                }

                return restLog;
            }

            TrainingStatGain gain = result.AppliedGain;
            string outcome = result.IsGreatSuccess
                ? LocalizedText.GetOrFallback(GameTextKeys.TrainingLogGreatSuccess, "大成功")
                : LocalizedText.GetOrFallback(GameTextKeys.TrainingLogSuccess, "成功");
            string gainLine = FormatGainLine(gain);
            string log;
            if (result.Command == TrainingCommandType.Train
                || result.Command == TrainingCommandType.SpecialTrain)
            {
                log = $"{commandName}{outcome} {TrainingFocusCatalog.GetDisplayName(result.Focus)}"
                    + $"\n{gainLine}";
            }
            else
            {
                log = $"{commandName}{outcome}\n{gainLine}";
            }

            if (!string.IsNullOrEmpty(result.FoundItemId)
                && TrainingShopCatalog.TryGetById(result.FoundItemId, out TrainingShopItem item))
            {
                log += "\n"
                    + LocalizedText.GetOrFallback(
                        GameTextKeys.TrainingLogFoundItem,
                        "運よく{name}を拾った",
                        "name",
                        TrainingShopCatalog.GetLocalizedName(item));
            }

            return log;
        }

        private static string FormatGainLine(TrainingStatGain gain)
        {
            var parts = new List<string>(3);
            if (gain.Hp != 0)
            {
                parts.Add(
                    LocalizedText.GetOrFallback(
                        GameTextKeys.TrainingLogStatHp,
                        "HP+{value}",
                        "value",
                        gain.Hp));
            }

            if (gain.Attack != 0)
            {
                parts.Add(
                    LocalizedText.GetOrFallback(
                        GameTextKeys.TrainingLogStatAtk,
                        "攻撃+{value}",
                        "value",
                        gain.Attack));
            }

            if (gain.Defense != 0)
            {
                parts.Add(
                    LocalizedText.GetOrFallback(
                        GameTextKeys.TrainingLogStatDef,
                        "防御+{value}",
                        "value",
                        gain.Defense));
            }

            if (gain.Speed != 0)
            {
                parts.Add(
                    LocalizedText.GetOrFallback(
                        GameTextKeys.TrainingLogStatSpd,
                        "速度+{value}",
                        "value",
                        gain.Speed));
            }

            if (gain.Hit != 0)
            {
                parts.Add(
                    LocalizedText.GetOrFallback(
                        GameTextKeys.TrainingLogStatHit,
                        "命中+{value}",
                        "value",
                        gain.Hit));
            }

            return parts.Count > 0
                ? string.Join(" ", parts)
                : LocalizedText.GetOrFallback(GameTextKeys.TrainingLogStatNone, "ステ上昇なし");
        }

        private void ApplyDefaultDestinationPresentation()
        {
            StopMonsterRoam();
            backgroundView?.ShowDefaultBackground();
            locationCameraView?.ApplyDefaultView();
        }

        /// <summary>
        /// 放課後戦闘復帰後の背景を適用する
        /// 最終日のみDefaultそれ以外はRoam
        /// </summary>
        /// <param name="session">育成セッション</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        private void ApplyPostBattleDestinationPresentation(
            TrainingSession session,
            CancellationToken cancellationToken)
        {
            if (IsLastTrainingDay(session))
            {
                ApplyDefaultDestinationPresentation();
                return;
            }

            ApplyRoamDestinationPresentation();
            StartMonsterRoam(cancellationToken);
        }

        private static bool IsLastTrainingDay(TrainingSession session)
        {
            return session != null
                && (int)session.CurrentDay >= TrainingSettings.TotalDays;
        }

        private static bool IsTrainingStartProgress(TrainingSlotProgress progress)
        {
            return progress != null
                && progress.day <= (int)TrainingDayOfWeek.Monday
                && progress.turnIndexInDay <= 0;
        }

        /// <summary>
        /// 戦闘復帰後に結果テキストとカメラを揃えてからフェード明けする
        /// </summary>
        private async UniTask FadeInAfterBattleResultReadyAsync(CancellationToken cancellationToken)
        {
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);
            if (canvasTransition != null)
            {
                await canvasTransition.FadeInAsync(cancellationToken);
            }
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
            ClearMotivationBall();
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
            ClearMotivationBall();
            if (canvasTransition == null)
            {
                applyWhileBlack?.Invoke();
                return;
            }

            await canvasTransition.FadeOutAsync(cancellationToken);
            applyWhileBlack?.Invoke();
            await canvasTransition.FadeInAsync(cancellationToken);
        }

        private void ClearMotivationBall()
        {
            if (motivationBallPlay == null && trainingDisplay != null)
            {
                motivationBallPlay =
                    trainingDisplay.GetComponent<TrainingMotivationBallPlayController>();
            }

            motivationBallPlay?.ClearBall();
        }

        /// <summary>
        /// 敵スロットの表示名を言語別で解決する
        /// </summary>
        /// <param name="enemySlotIndex">敵スロット</param>
        /// <returns>表示名</returns>
        private string ResolveEnemyDisplayName(int enemySlotIndex)
        {
            string fallback = saveService.GetSlot(ModelSavePool.Enemy, enemySlotIndex)?.modelName;
            if (string.IsNullOrEmpty(fallback))
            {
                fallback = LocalizedText.GetOrFallback(GameTextKeys.TrainingAmbushEnemyDefault, "強敵");
            }

            return EnemyDisplayName.Resolve(enemySlotIndex, fallback);
        }
    }
}
