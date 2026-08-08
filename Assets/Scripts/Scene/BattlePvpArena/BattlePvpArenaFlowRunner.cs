using Localization;
using Audio.Interface;
using Battle;
using Battle.Interface;
using Battle.View;
using Camera.View;
using ClayEditor.Rigging;
using Cysharp.Threading.Tasks;
using Extensions;
using Lighthouse.Scene;
using LighthouseExtends.UIComponent.Button;
using SaveData;
using SaveData.Interface;
using Scene.BattleNpcScene;
using Scene.BattlePVPScene;
using Scene.BattlePVPScene.Interface;
using Scene.BattlePVPScene.Network;
using Scene.BattlePVPScene.Service;
using Scene.BattlePVPScene.View;
using Scene.Core;
using Scene.Core.Interface;
using Scene.PvpLobby.Interface;
using System.Threading;
using static Scene.TitleScene.TitleScene;
using TMPro;
using UI.Battle.View;
using UI.ClayEditor.View;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Scene.BattlePvpArena
{
    /// <summary>
    /// BattlePvpArenaシーンの戦闘フローを組み立てNetcode同期で実行する
    /// </summary>
    public sealed class BattlePvpArenaFlowRunner : MonoBehaviour
    {
        private const int SceneCanvasReadyMaxFrames = 180;

        [Header("選択・UI")]
        [SerializeField] private LoadSlotView loadSlotView;
        [SerializeField] private Canvas selectionCanvas;
        [SerializeField] private LHButton titleReturnButton;
        [SerializeField] private BattleView battleView;
        [SerializeField] private Canvas battleUiCanvas;

        [Header("配置")]
        [SerializeField] private Transform playerSpawn;
        [SerializeField] private Transform enemySpawn;

        [Header("モデル構築・演出")]
        [SerializeField] private LoadedModelConfigurator configurator;
        [SerializeField] private BattleNpcStaging staging;

        [Header("カメラ")]
        [SerializeField] private ClayEditCameraView battleCamera;
        [SerializeField] private BattleFieldCameraProfile cameraProfile = new BattleFieldCameraProfile();

        [Header("レベルデザイン")]
        [SerializeField] private BattleLevelDesignSettings levelDesignSettings;

        private IClayModelImporter importer;
        private IClayModelSaveService saveService;
        private IPointsService pointsService;
        private ISkillTreeService skillTreeService;
        private ISceneFade sceneFade;
        private IBattleCanvasTransition canvasTransition;
        private IBgmService bgmService;
        private ISeService seService;
        private IBattleDualVictoryReturnView pvpVictoryReturnView;
        private IClayMonsterSceneManager sceneManager;
        private IPvpSessionController pvpSessionController;
        private IBattlePvpDisconnectView disconnectView;
        private IBattlePvpOpponentWaitView opponentWaitView;
        private BattlePvpDisconnectFlowHandler disconnectHandler;
        private IBattlePvpCombatSync activeCombatSync;

        private CancellationTokenSource flowCts;
        private bool isRunning;
        private int flowVersion;
        private System.IDisposable titleReturnSubscription;
        private System.IDisposable languageSubscription;
        private GameObject trackedPlayerModel;
        private GameObject trackedEnemyModel;
        private BattlePvpMatchMode matchMode = BattlePvpMatchMode.Direct;
        private LHButton cachedSelectionLeaveButton;

        private IMonsterSelectionSession selectionSession;

        [Inject]
        public void Construct(
            IClayModelImporter importer,
            IClayModelSaveService saveService,
            IPointsService pointsService,
            ISkillTreeService skillTreeService,
            ISceneFade sceneFade,
            IBattleCanvasTransition canvasTransition,
            IBgmService bgmService,
            ISeService seService,
            IMonsterSelectionSession selectionSession,
            IBattleDualVictoryReturnView pvpVictoryReturnView,
            IClayMonsterSceneManager sceneManager,
            IPvpSessionController pvpSessionController,
            IBattlePvpDisconnectView disconnectView,
            IBattlePvpOpponentWaitView opponentWaitView)
        {
            this.importer = importer;
            this.saveService = saveService;
            this.pointsService = pointsService;
            this.skillTreeService = skillTreeService;
            this.sceneFade = sceneFade;
            this.canvasTransition = canvasTransition;
            this.bgmService = bgmService;
            this.seService = seService;
            this.selectionSession = selectionSession;
            this.pvpVictoryReturnView = pvpVictoryReturnView;
            this.sceneManager = sceneManager;
            this.pvpSessionController = pvpSessionController;
            this.disconnectView = EnsureDisconnectView(disconnectView);
            this.opponentWaitView = EnsureOpponentWaitView(opponentWaitView);
            disconnectHandler = new BattlePvpDisconnectFlowHandler(this.disconnectView);
            disconnectHandler.Bind(
                StopFlow,
                ReturnToTitleAsync,
                () => this.GetCancellationTokenOnDestroy(),
                () => sceneFade?.ForceRelease());

            BindSelectionLeaveButton();
            languageSubscription?.Dispose();
            languageSubscription = LanguageAwareUi.Register(ApplySelectionLeaveButtonLabel);
        }

        private void ApplySelectionLeaveButtonLabel()
        {
            LHButton leaveButton = cachedSelectionLeaveButton ?? ResolveSelectionLeaveButton();
            if (leaveButton == null)
            {
                return;
            }

            ApplyLeaveButtonLabel(leaveButton, LocalizedText.Get(GameTextKeys.BattleLeave));
        }

        /// <summary>
        /// マッチ方式を設定する
        /// </summary>
        /// <param name="mode">特定相手または不特定相手</param>
        public void SetMatchMode(BattlePvpMatchMode mode)
        {
            matchMode = mode;
        }

        private void OnDestroy()
        {
            titleReturnSubscription?.Dispose();
            languageSubscription?.Dispose();
            disconnectHandler?.Dispose();
            flowCts?.Cancel();
            flowCts?.Dispose();
        }

        /// <summary>
        /// Canvas初期化完了後に戦闘フローを開始する(Lighthouseコールバック用)
        /// </summary>
        public void TryStartFlowAfterCanvasInit(string reason)
        {
            if (!this)
            {
                Debug.LogWarning($"[BattlePvpArena] TryStartFlowAfterCanvasInit skipped runner破棄済み reason={reason}");
                return;
            }

            Debug.Log($"[BattlePvpArena] TryStartFlowAfterCanvasInit reason={reason}");
            StartFlow();
        }

        /// <summary>
        /// シーンHierarchyから未設定の参照を補完する
        /// </summary>
        public void EnsureSceneReferences(Transform sceneRoot)
        {
            if (sceneRoot == null)
            {
                return;
            }

            if (loadSlotView == null)
            {
                loadSlotView = sceneRoot.GetComponentInChildren<LoadSlotView>(true);
            }

            if (selectionCanvas == null)
            {
                selectionCanvas = FindCanvasInHierarchy(sceneRoot, "LoadSlotCanvas");
            }

            if (battleView == null)
            {
                battleView = sceneRoot.GetComponentInChildren<BattleView>(true);
            }

            if (battleUiCanvas == null)
            {
                battleUiCanvas = FindCanvasInHierarchy(sceneRoot, "BattleCanvas");
            }

            if (playerSpawn == null)
            {
                playerSpawn = FindTransformByName(sceneRoot, "PlayerSpawnPoint");
            }

            if (enemySpawn == null)
            {
                enemySpawn = FindTransformByName(sceneRoot, "EnemySpawnPoint");
            }

            if (configurator == null)
            {
                configurator = sceneRoot.GetComponentInChildren<LoadedModelConfigurator>(true);
            }

            if (staging == null)
            {
                staging = sceneRoot.GetComponentInChildren<BattleNpcStaging>(true);
            }

            if (pvpVictoryReturnView == null)
            {
                BattlePvpVictoryReturnView view = sceneRoot.GetComponentInChildren<BattlePvpVictoryReturnView>(true);
                if (view != null)
                {
                    pvpVictoryReturnView = view;
                }
            }

            if (battleCamera == null)
            {
                battleCamera = sceneRoot.GetComponentInChildren<ClayEditCameraView>(true);
            }
        }

        /// <summary>
        /// 明転前にセーブスロット選択UIのレイアウトだけ整える
        /// </summary>
        public void PrepareSelectionLayout()
        {
            // 選択UIが出るまで暗転を保ち背景だけが見える時間をなくす
            sceneFade?.EnsureOpaque();
            selectionSession?.PrepareEntry();
            staging?.PrepareSelectionEntry();
            battleCamera?.SetCameraEnable(false);
            loadSlotView?.PrepareLayout();
            BindSelectionLeaveButton();
            CanvasVisibilityUtility.SetCanvasEnabled(selectionCanvas, false);
            CanvasVisibilityUtility.SetCanvasEnabled(battleUiCanvas, false);
            pvpVictoryReturnView?.SetDualButtonsVisible(false);
            disconnectView?.SetVisible(false);
            opponentWaitView?.SetVisible(false);
        }

        /// <summary>
        /// シーン退場時に選択UI戦闘UI配置モデルを整理する
        /// </summary>
        public void CleanupForLeave()
        {
            disconnectHandler?.SuppressNotifications();
            pvpSessionController?.EndSession();
            StopFlow();
            ClearPreviousSpawnedModels();
            selectionSession?.HideForLeave();
            loadSlotView?.HideForLeave();
            CanvasVisibilityUtility.SetCanvasEnabled(selectionCanvas, false);
            CanvasVisibilityUtility.SetCanvasEnabled(battleUiCanvas, false);
            pvpVictoryReturnView?.SetDualButtonsVisible(false);
            disconnectView?.SetVisible(false);
            opponentWaitView?.SetVisible(false);
            staging?.PrepareSelectionEntry();
            canvasTransition?.ReleasePresentationInput();
            ModelSaveSlotScrollListView.ExitFullscreenSelectionLayout();
        }

        /// <summary>
        /// 暗転後にセーブスロット選択キャンバスを表示する
        /// </summary>
        public async UniTask RevealSelectionAsync(CancellationToken cancellationToken)
        {
            Debug.Log("[BattlePvpArena] RevealSelectionAsync開始");
            EnsureSceneReferences(transform);
            BindSelectionLeaveButton();
            staging?.PrepareSelectionEntry();
            battleCamera?.SetCameraEnable(false);
            loadSlotView?.PrepareLayout(reparentToUiRoot: false);

            if (selectionCanvas != null)
            {
                selectionCanvas.gameObject.SetActive(true);
            }

            if (loadSlotView != null)
            {
                loadSlotView.gameObject.SetActive(true);
            }

            DetachSelectionCanvasToSceneRoot();
            CanvasVisibilityUtility.SetCanvasEnabled(selectionCanvas, true);

            loadSlotView?.EnsureSelectionInputEnabled();
            if (loadSlotView != null)
            {
                await loadSlotView.PrepareSelectionContentsAsync(cancellationToken);
            }

            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);
            Canvas.ForceUpdateCanvases();

            if (sceneFade != null)
            {
                await sceneFade.FadeInAsync(cancellationToken);
            }

            LogSelectionCanvasState("RevealSelectionAsync完了");
        }

        /// <summary>
        /// 戦闘フローを開始する
        /// </summary>
        public void StartFlow()
        {
            if (!this)
            {
                Debug.LogWarning("[BattlePvpArena] StartFlow skipped runner破棄済み");
                return;
            }

            if (isRunning)
            {
                Debug.Log("[BattlePvpArena] StartFlow skipped 既に実行中");
                return;
            }

            EnsureBattleComponents();
            EnsureSceneReferences(transform);
            disconnectView = EnsureDisconnectView(disconnectView);
            disconnectHandler?.SetDisconnectView(disconnectView);
            BattleHitStopClock.Clear();
            flowCts?.Cancel();
            flowCts?.Dispose();
            flowCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            isRunning = true;
            // 選択中の相手切断も拾う(旧BattlePvpFlowと同じく入場直後から監視)
            disconnectHandler?.BeginMonitoring();
            int version = ++flowVersion;
            Debug.Log($"[BattlePvpArena] StartFlow version={version}");
            RunAsync(flowCts.Token, version).Forget();
        }

        /// <summary>
        /// 戦闘フローを停止する
        /// </summary>
        public void StopFlow()
        {
            flowCts?.Cancel();
            // isRunningはRunAsyncのfinallyで解除する
            // キャンセル処理中の再Startによる二重生成を防ぐ
            BattleHitStopClock.Clear();
        }

        private void EnsureBattleComponents()
        {
            if (!this)
            {
                return;
            }

            Transform effectsRoot = EnsureBattleEffectsRoot();

            if (effectsRoot.GetComponent<BattleHitStopClock>() == null)
            {
                effectsRoot.gameObject.AddComponent<BattleHitStopClock>();
            }

            if (effectsRoot.GetComponent<BattleHitEffectView>() == null)
            {
                effectsRoot.gameObject.AddComponent<BattleHitEffectView>();
            }

            if (effectsRoot.GetComponent<BattleDamagePopupView>() == null)
            {
                effectsRoot.gameObject.AddComponent<BattleDamagePopupView>();
            }
        }

        private Transform EnsureBattleEffectsRoot()
        {
            Transform existing = transform.Find("BattleEffects");
            if (existing != null)
            {
                return existing;
            }

            var effectsObject = new GameObject("BattleEffects");
            effectsObject.transform.SetParent(transform, false);
            return effectsObject.transform;
        }

        private async UniTask RunAsync(CancellationToken cancellationToken, int version)
        {
            try
            {
                Debug.Log($"[BattlePvpArena] 戦闘フロー開始 version={version}");
                EnsureBattleComponents();
                EnsureSceneReferences(transform);
                // 中断で残った前回フローのモデルを破棄し二重生成を防ぐ
                ClearPreviousSpawnedModels();
                await WaitForSceneCanvasReadyAsync(cancellationToken);
                ThrowIfFlowSuperseded(version);
                await RevealSelectionAsync(cancellationToken);
                ThrowIfFlowSuperseded(version);

                BattlePvpSessionSpawner sessionSpawner = FindSessionSpawner();
                sessionSpawner?.TrySpawnPlayersIfNeeded();

                var loader = new BattleParticipantLoader(importer, saveService, configurator);
                BattlePvpInputRelay inputRelay = null;
                Transform effectsRoot = EnsureBattleEffectsRoot();
                var context = new BattleFlow.Context
                {
                    PlayerSpawn = playerSpawn,
                    EnemySpawn = enemySpawn,
                    BattleUiCanvas = battleUiCanvas,
                    EnemySlotIndex = 0,
                    BattleCamera = battleCamera,
                    CameraProfile = cameraProfile,
                    PresentationTransition = canvasTransition,
                    HitEffect = effectsRoot.GetComponent<BattleHitEffectView>(),
                    DamagePopup = effectsRoot.GetComponent<BattleDamagePopupView>(),
                    FinishPresentation = staging != null ? staging.FinishPresentation : null,
                    PartBreakPresentation = staging != null ? staging.PartBreakPresentation : null,
                    VictoryDualReturnView = EnsureVictoryReturnView(),
                    LevelDesignSettings = levelDesignSettings
                };
                context.EnemyLoader = async token =>
                {
                    ThrowIfFlowSuperseded(version);
                    // 選択後は教室背景を見せず暗転を維持する待機UIはフェードより前面
                    sceneFade?.EnsureOpaque();

                    sessionSpawner?.TrySpawnPlayersIfNeeded();
                    if (sessionSpawner != null)
                    {
                        await BattlePvpOpponentWaitScope.RunAsync(
                            opponentWaitView,
                            async waitToken =>
                            {
                                inputRelay = await sessionSpawner.WaitForLocalRelayAsync(waitToken);
                            },
                            token);
                    }

                    if (inputRelay == null)
                    {
                        Debug.LogError("[BattlePvpArena] ローカルリレーが取得できませんでした");
                        return default;
                    }

                    ThrowIfFlowSuperseded(version);
                    // 入場直後に監視済みなら再購読のみ確定させる
                    disconnectHandler?.BeginMonitoring();
                    context.EnemyAi = new NetworkBattleRemoteEnemyAi(inputRelay);
                    activeCombatSync?.EndListening();
                    activeCombatSync = new BattlePvpCombatSync(inputRelay);
                    context.CombatSync = activeCombatSync;
                    inputRelay.ResetSessionState();
                    context.WaitForMatchupStartAsync = CreateMatchupStartWaiter(inputRelay);
                    return await LoadOpponentAsync(loader, inputRelay, token);
                };
                context.OnBattleInputCreated = input => inputRelay?.BeginBattleInput(input);
                context.OnBattleInputDisposed = () => inputRelay?.EndBattleInput();
                context.RegisterSpawnedParticipants = RegisterSpawnedParticipants;
                context.OnLocalBattleOutcomeSettled = OnLocalBattleOutcomeSettled;

                var flow = new BattleFlow(selectionSession, battleView, staging, loader, context, bgmService, seService);
                while (!cancellationToken.IsCancellationRequested)
                {
                    ThrowIfFlowSuperseded(version);
                    BattleVictoryReturnChoice choice = await flow.RunAsync(cancellationToken);
                    if (choice == BattleVictoryReturnChoice.Rematch)
                    {
                        try
                        {
                            await PrepareRematchAsync(inputRelay, cancellationToken);
                            continue;
                        }
                        catch (System.OperationCanceledException)
                        {
                            throw;
                        }
                        catch (System.Exception rematchException)
                        {
                            Debug.LogException(rematchException);
                            NotifyDisconnectIfNetworkDead();
                            await ReturnToTitleAsync(cancellationToken);
                            break;
                        }
                    }

                    await ReturnToTitleAsync(cancellationToken);
                    break;
                }
            }
            catch (System.OperationCanceledException)
            {
                Debug.LogWarning($"[BattlePvpArena] RunAsyncキャンセル version={version}");
                ClearPreviousSpawnedModels();
                // 暗転のまま固まるのを防ぐ
                sceneFade?.ForceRelease();
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                ClearPreviousSpawnedModels();
                // 明転前に失敗すると暗転のまま操作不能になるため明転させる
                if (sceneFade != null)
                {
                    await sceneFade.FadeInAsync(this.GetCancellationTokenOnDestroy());
                }
            }
            finally
            {
                if (version == flowVersion)
                {
                    isRunning = false;
                }
            }
        }

        private IBattleDualVictoryReturnView EnsureVictoryReturnView()
        {
            if (pvpVictoryReturnView != null)
            {
                return pvpVictoryReturnView;
            }

            EnsureSceneReferences(transform);
            if (pvpVictoryReturnView == null)
            {
                pvpVictoryReturnView = GetComponentInChildren<BattlePvpVictoryReturnView>(true);
            }

            if (pvpVictoryReturnView == null)
            {
                Debug.LogError("[BattlePvpArena] BattlePvpVictoryReturnViewが見つかりません");
            }

            return pvpVictoryReturnView;
        }

        private IBattlePvpDisconnectView EnsureDisconnectView(IBattlePvpDisconnectView injectedView)
        {
            if (injectedView != null)
            {
                return injectedView;
            }

            return GetComponentInChildren<BattlePvpDisconnectView>(true);
        }

        private IBattlePvpOpponentWaitView EnsureOpponentWaitView(IBattlePvpOpponentWaitView injectedView)
        {
            if (injectedView != null)
            {
                return injectedView;
            }

            return GetComponentInChildren<BattlePvpOpponentWaitView>(true);
        }

        private async UniTask PrepareRematchAsync(
            BattlePvpInputRelay inputRelay,
            CancellationToken cancellationToken)
        {
            if (inputRelay == null)
            {
                Debug.LogError("[BattlePvpArena] 再戦準備にリレーがありません");
                return;
            }

            // 片方が先に進むと相手の旧スロットを拾って破綻するため再戦合意を待つ
            sceneFade?.EnsureOpaque();
            pvpVictoryReturnView?.SetDualButtonsVisible(false);
            CanvasVisibilityUtility.SetCanvasEnabled(battleUiCanvas, false);
            inputRelay.SubmitRematchReady();
            await BattlePvpOpponentWaitScope.RunAsync(
                opponentWaitView,
                inputRelay.WaitForBothRematchReadyAsync,
                cancellationToken);

            // NV同期が一瞬遅れても片側だけが進まないよう数フレーム空ける
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);

            // 両者合意後に局所状態を同時に落とす
            inputRelay.ResetForRematch();
            inputRelay.EndBattleInput();
            activeCombatSync?.EndListening();
            activeCombatSync = null;
            ClearPreviousSpawnedModels();
            selectionSession?.HideForLeave();
            selectionSession?.PrepareEntry();
            loadSlotView?.InvalidateSelectionContents();

            if (sceneFade != null)
            {
                await sceneFade.FadeOutAsync(cancellationToken);
            }

            await RevealSelectionAsync(cancellationToken);
        }

        private void OnLocalBattleOutcomeSettled(BattleLocalOutcome outcome)
        {
            if (pointsService == null)
            {
                Debug.LogError("[BattlePvpArena] pointsServiceが未注入です");
                return;
            }

            bool isDraw = outcome == BattleLocalOutcome.Draw;
            bool playerWon = outcome == BattleLocalOutcome.Win;
            int reward = BattlePointsRules.ResolvePvpPoints(
                matchMode == BattlePvpMatchMode.Random,
                playerWon,
                isDraw);
            if (reward <= 0)
            {
                return;
            }

            if (skillTreeService != null)
            {
                reward = skillTreeService.ApplyPointsGainBonus(reward);
            }

            pointsService.AddPoints(reward);
        }

        private void RegisterSpawnedParticipants(GameObject playerModel, GameObject enemyModel)
        {
            trackedPlayerModel = playerModel;
            trackedEnemyModel = enemyModel;
        }

        /// <summary>
        /// 前回フローで生成し残ったモデルを破棄する
        /// 中断後の再開でモデルが二重生成されるのを防ぐ
        /// </summary>
        private void ClearPreviousSpawnedModels()
        {
            DestroyTrackedParticipants();
            ClearSpawnedModels(playerSpawn);
            ClearSpawnedModels(enemySpawn);
            // LoadSlotViewの一時親に残ったプレビューも破棄する
            ClearNamedRootChildren("TrainingModelImportRoot");
        }

        private void DestroyTrackedParticipants()
        {
            if (trackedPlayerModel != null)
            {
                trackedPlayerModel.SetActive(false);
                Object.Destroy(trackedPlayerModel);
                trackedPlayerModel = null;
            }

            if (trackedEnemyModel != null)
            {
                trackedEnemyModel.SetActive(false);
                Object.Destroy(trackedEnemyModel);
                trackedEnemyModel = null;
            }
        }

        private static void ClearSpawnedModels(Transform spawn)
        {
            if (spawn == null)
            {
                return;
            }

            for (int i = spawn.childCount - 1; i >= 0; i--)
            {
                Transform child = spawn.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                child.gameObject.SetActive(false);
                Object.Destroy(child.gameObject);
            }
        }

        private static void ClearNamedRootChildren(string rootName)
        {
            GameObject rootObject = GameObject.Find(rootName);
            if (rootObject == null)
            {
                return;
            }

            ClearSpawnedModels(rootObject.transform);
        }

        private void ThrowIfFlowSuperseded(int version)
        {
            if (version != flowVersion)
            {
                throw new System.OperationCanceledException("BattlePvpArena flow superseded");
            }
        }

        private async UniTask WaitForSceneCanvasReadyAsync(CancellationToken cancellationToken)
        {
            for (int frame = 0; frame < SceneCanvasReadyMaxFrames; frame++)
            {
                Canvas sceneCanvas = FindManagedSceneCanvas();
                if (sceneCanvas != null
                    && sceneCanvas.enabled
                    && sceneCanvas.renderMode == RenderMode.ScreenSpaceCamera
                    && sceneCanvas.worldCamera != null)
                {
                    Debug.Log(
                        "[BattlePvpArena] 親Canvas初期化検知"
                        + $" frame={frame}"
                        + $" mode={sceneCanvas.renderMode}"
                        + $" camera={sceneCanvas.worldCamera.name}");
                    return;
                }

                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);
            }

            Debug.LogWarning("[BattlePvpArena] 親Canvas初期化未検知 選択UI表示を続行します");
        }

        private Canvas FindManagedSceneCanvas()
        {
            Transform sceneRoot = transform;
            for (int i = 0; i < sceneRoot.childCount; i++)
            {
                Transform child = sceneRoot.GetChild(i);
                if (child.name != "Canvas")
                {
                    continue;
                }

                Canvas canvas = child.GetComponent<Canvas>();
                if (canvas != null)
                {
                    return canvas;
                }
            }

            return null;
        }

        private void DetachSelectionCanvasToSceneRoot()
        {
            Transform sceneRoot = transform;
            if (loadSlotView != null)
            {
                loadSlotView.DetachSelectionUiToSceneRoot(sceneRoot);
                if (selectionCanvas == null)
                {
                    selectionCanvas = loadSlotView.SelectionCanvas;
                }

                return;
            }

            if (selectionCanvas == null)
            {
                return;
            }

            if (selectionCanvas.transform.parent != sceneRoot)
            {
                selectionCanvas.transform.SetParent(sceneRoot, false);
            }

            selectionCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            selectionCanvas.worldCamera = null;
            selectionCanvas.overrideSorting = true;
            selectionCanvas.sortingOrder = 500;

            ModelSaveSlotScrollListView.FixCanvasScaleHierarchy(selectionCanvas);
        }

        private void LogSelectionCanvasState(string phase)
        {
            if (selectionCanvas == null)
            {
                Debug.LogWarning($"[BattlePvpArena] {phase} selectionCanvas未設定");
                return;
            }

            Debug.Log(
                $"[BattlePvpArena] {phase}"
                + $" enabled={selectionCanvas.enabled}"
                + $" active={selectionCanvas.gameObject.activeInHierarchy}"
                + $" mode={selectionCanvas.renderMode}"
                + $" scale={selectionCanvas.transform.lossyScale}"
                + $" parent={(selectionCanvas.transform.parent != null ? selectionCanvas.transform.parent.name : "null")}"
                + $" sortOrder={selectionCanvas.sortingOrder}");
        }

        private static BattlePvpSessionSpawner FindSessionSpawner()
        {
            return Object.FindFirstObjectByType<BattlePvpSessionSpawner>(FindObjectsInactive.Include);
        }

        private System.Func<CancellationToken, UniTask> CreateMatchupStartWaiter(BattlePvpInputRelay inputRelay)
        {
            return async token =>
            {
                if (inputRelay == null)
                {
                    return;
                }

                inputRelay.SubmitMatchupReady();
                await BattlePvpOpponentWaitScope.RunAsync(
                    opponentWaitView,
                    inputRelay.WaitForBothMatchupReadyAsync,
                    token);
            };
        }

        private async UniTask<BattleParticipant> LoadOpponentAsync(
            BattleParticipantLoader loader,
            BattlePvpInputRelay inputRelay,
            CancellationToken cancellationToken)
        {
            // DetachLoadedModel後はLoadSlotView.SelectedSlotIndexが-1になるためセッション側を使う
            int localSlot = selectionSession != null
                ? selectionSession.SelectedSlotIndex
                : (loadSlotView != null ? loadSlotView.SelectedSlotIndex : 0);
            if (localSlot < 0)
            {
                Debug.LogError(
                    "[BattlePvpArena] ローカルスロットが未確定です"
                    + $" sessionSlot={(selectionSession != null ? selectionSession.SelectedSlotIndex : -99)}"
                    + $" loadSlot={(loadSlotView != null ? loadSlotView.SelectedSlotIndex : -99)}");
                return default;
            }

            ModelSaveSlot localModelSlot = saveService != null
                ? saveService.GetSlot(ModelSavePool.TrainedPlayer, localSlot)
                : null;
            if (localModelSlot == null || string.IsNullOrEmpty(localModelSlot.glbFileName))
            {
                Debug.LogError($"[BattlePvpArena] ローカルスロット{localSlot}のモデルがありません");
                return default;
            }

            BattlePvpRemoteModelMeta meta = BattlePvpRemoteModelMeta.FromSlot(localModelSlot);
            BattlePvpModelTransfer.Payload transferPayload;
            bool didSubmitSlot = false;
            try
            {
                transferPayload = await BattlePvpModelTransfer.CreateFromSaveFileAsync(
                    localModelSlot.glbFileName,
                    cancellationToken);
                meta.ApplyTransferPayload(transferPayload);

                string metaJson = meta.ToJson();
                Debug.Log(
                    "[BattlePvpArena] スロット送信"
                    + $" localSlot={localSlot}"
                    + $" IsOwner={inputRelay.IsOwner}"
                    + $" wireBytes={transferPayload.WireBytes.Length}"
                    + $" rawBytes={transferPayload.UncompressedByteCount}"
                    + $" gzip={transferPayload.IsGzipCompressed}");
                // 相手待ち中は教室背景を出さず暗転維持する
                sceneFade?.EnsureOpaque();

                await BattlePvpOpponentWaitScope.RunAsync(
                    opponentWaitView,
                    async token =>
                    {
                        inputRelay.SubmitSlotSelection(localSlot);
                        didSubmitSlot = true;
                        await inputRelay.PublishLocalModelAsync(
                            metaJson,
                            transferPayload.WireBytes,
                            token);
                        await inputRelay.WaitForBothSlotsAsync(token);
                        await inputRelay.WaitForOpponentModelAsync(token);
                    },
                    cancellationToken);

                BattlePvpReceivedRemoteModel remoteModel = inputRelay.GetOpponentReceivedModel();
                if (remoteModel == null || !remoteModel.IsValid)
                {
                    Debug.LogError("[BattlePvpArena] 相手モデルの受信に失敗しました");
                    return default;
                }

                Debug.Log(
                    "[BattlePvpArena] 両者モデル確定"
                    + $" localSlot={localSlot}"
                    + $" opponentSlot={inputRelay.OpponentSlotIndex}"
                    + $" opponentName={remoteModel.Meta.modelName}"
                    + $" opponentBytes={remoteModel.GlbBytes.Length}");

                BattleParticipant enemy = default;
                await BattlePvpOpponentWaitScope.RunAsync(
                    opponentWaitView,
                    async token =>
                    {
                        enemy = await loader.LoadFromGlbBytesAsync(
                            remoteModel.Meta.ToTemporarySlot(),
                            remoteModel.GlbBytes,
                            enemySpawn,
                            token);
                    },
                    cancellationToken);

                if (!enemy.IsValid)
                {
                    return default;
                }

                // 片方だけ見せ合いへ進まないよう両者のロード完了を待つ
                inputRelay.SubmitStagingReady();
                await BattlePvpOpponentWaitScope.RunAsync(
                    opponentWaitView,
                    inputRelay.WaitForBothStagingReadyAsync,
                    cancellationToken);

                if (!inputRelay.AreBothStagingReady)
                {
                    Debug.LogError("[BattlePvpArena] 相手の見せ合い準備が揃いませんでした");
                    if (enemy.Model != null)
                    {
                        Object.Destroy(enemy.Model);
                    }

                    return default;
                }

                // 両者進入後に受信glbバッファを解放する
                inputRelay.ClearAllReceivedRemoteModels();
                didSubmitSlot = false;
                return enemy;
            }
            catch (System.OperationCanceledException)
            {
                throw;
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                return default;
            }
            finally
            {
                // 失敗時は相手が待ち続けないよう即時中断を通知する
                if (didSubmitSlot)
                {
                    inputRelay.ReportSessionFailure();
                }

                // 切断相当は選択UI復旧に埋もれず切断ウィンドウへ誘導する
                NotifyDisconnectIfNetworkDead();
            }
        }

        private void NotifyDisconnectIfNetworkDead()
        {
            Unity.Netcode.NetworkManager manager = Unity.Netcode.NetworkManager.Singleton;
            if (manager == null || manager.ShutdownInProgress)
            {
                disconnectHandler?.ForceNotifyDisconnect();
                return;
            }

            if (manager.IsServer || manager.IsHost)
            {
                if (manager.ConnectedClientsIds == null
                    || manager.ConnectedClientsIds.Count < 2)
                {
                    disconnectHandler?.ForceNotifyDisconnect();
                }

                return;
            }

            if (!manager.IsConnectedClient)
            {
                disconnectHandler?.ForceNotifyDisconnect();
            }
        }

        private void OnClickTitleReturn()
        {
            if (sceneManager == null || sceneManager.IsTransition)
            {
                return;
            }

            disconnectHandler?.SuppressNotifications();
            StopFlow();
            ReturnToTitleAsync(CancellationToken.None).Forget();
        }

        /// <summary>
        /// 選択画面の退出は「戻る」のみにする
        /// TitleReturnButtonは切断UI用のため選択Canvas上では非表示にする
        /// </summary>
        private void BindSelectionLeaveButton()
        {
            EnsureSceneReferences(transform);
            HideSelectionTitleReturnButton();

            titleReturnSubscription?.Dispose();
            titleReturnSubscription = null;

            LHButton leaveButton = ResolveSelectionLeaveButton();
            if (leaveButton == null)
            {
                Debug.LogError("[BattlePvpArena] 選択画面の戻るボタンが見つかりません");
                return;
            }

            ApplyLeaveButtonLabel(leaveButton, LocalizedText.Get(GameTextKeys.BattleLeave));
            titleReturnSubscription = leaveButton.SubscribeOnClick(OnClickTitleReturn);
            cachedSelectionLeaveButton = leaveButton;
        }

        private void HideSelectionTitleReturnButton()
        {
            if (selectionCanvas == null)
            {
                return;
            }

            Transform titleReturn = selectionCanvas.transform.Find("TitleReturnButton");
            if (titleReturn != null && titleReturn.gameObject.activeSelf)
            {
                titleReturn.gameObject.SetActive(false);
            }
        }

        private LHButton ResolveSelectionLeaveButton()
        {
            if (cachedSelectionLeaveButton != null)
            {
                return cachedSelectionLeaveButton;
            }

            if (selectionCanvas != null)
            {
                LHButton[] buttons = selectionCanvas.GetComponentsInChildren<LHButton>(true);
                for (int i = 0; i < buttons.Length; i++)
                {
                    LHButton button = buttons[i];
                    if (button == null || button.gameObject.name == "TitleReturnButton")
                    {
                        continue;
                    }

                    if (IsUnderConfirmPanel(button.transform))
                    {
                        continue;
                    }

                    TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
                    if (label != null && IsSelectionLeaveButtonLabel(label.text))
                    {
                        cachedSelectionLeaveButton = button;
                        return button;
                    }
                }
            }

            // 選択Canvas配下のTitleReturnだけをフォールバックにする(切断UIのボタンは使わない)
            if (titleReturnButton != null
                && selectionCanvas != null
                && titleReturnButton.transform.IsChildOf(selectionCanvas.transform))
            {
                cachedSelectionLeaveButton = titleReturnButton;
                return titleReturnButton;
            }

            return null;
        }

        /// <summary>
        /// 選択退出ボタンのラベルか判定する
        /// プレハブ原文戻ると翻訳後の戻る/退出表記の両方を認める
        /// </summary>
        /// <param name="labelText">ボタン上の文言</param>
        private static bool IsSelectionLeaveButtonLabel(string labelText)
        {
            if (string.IsNullOrEmpty(labelText))
            {
                return false;
            }

            // シーン配置時の原文は常に日本語の戻る
            if (labelText == "戻る")
            {
                return true;
            }

            string commonReturn = LocalizedText.GetOrFallback(GameTextKeys.CommonReturn, "戻る");
            if (labelText == commonReturn)
            {
                return true;
            }

            string battleLeave = LocalizedText.GetOrFallback(GameTextKeys.BattleLeave, "戻る");
            if (labelText == battleLeave)
            {
                return true;
            }

            string commonReturnTable = LocalizedText.Get(GameTextKeys.CommonReturn);
            if (!string.IsNullOrEmpty(commonReturnTable) && labelText == commonReturnTable)
            {
                return true;
            }

            string battleLeaveTable = LocalizedText.Get(GameTextKeys.BattleLeave);
            return !string.IsNullOrEmpty(battleLeaveTable) && labelText == battleLeaveTable;
        }

        private static bool IsUnderConfirmPanel(Transform target)
        {
            Transform current = target;
            while (current != null)
            {
                if (current.name == "ConfirmSaveSlotCanvas")
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static void ApplyLeaveButtonLabel(LHButton button, string label)
        {
            Extensions.LhButtonLabelUtility.SetLabel(button, label);
        }

        private async UniTask ReturnToTitleAsync(CancellationToken cancellationToken)
        {
            sceneFade?.ForceRelease();
            disconnectHandler?.SuppressNotifications();
            pvpSessionController?.EndSession();

            if (sceneManager == null)
            {
                Debug.LogError("[BattlePvpArena] sceneManager未設定のためTitle遷移できません");
                return;
            }

            // 遷移中でも必ず待ち切ってからTitleへ戻す
            await WaitForSceneTransitionIdleAsync(cancellationToken);
            await sceneManager.TransitionScene(
                new TitleTransitionData(),
                TransitionType.Exclusive,
                ClayMonstersMainSceneId.Title);
        }

        private async UniTask WaitForSceneTransitionIdleAsync(CancellationToken cancellationToken)
        {
            if (sceneManager == null || !sceneManager.IsTransition)
            {
                return;
            }

            const float maxWaitSeconds = 15f;
            float startedAt = Time.realtimeSinceStartup;
            await UniTask.WaitUntil(
                () => !sceneManager.IsTransition
                    || Time.realtimeSinceStartup - startedAt >= maxWaitSeconds,
                cancellationToken: cancellationToken);

            if (sceneManager.IsTransition)
            {
                Debug.LogError(
                    $"[BattlePvpArena] Title遷移待ちがタイムアウトしました ({maxWaitSeconds}秒)");
            }
        }

        private static Canvas FindCanvasInHierarchy(Transform sceneRoot, string canvasName)
        {
            Canvas[] canvases = sceneRoot.GetComponentsInChildren<Canvas>(true);
            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i].name == canvasName)
                {
                    return canvases[i];
                }
            }

            return null;
        }

        private static Transform FindTransformByName(Transform sceneRoot, string objectName)
        {
            Transform[] transforms = sceneRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].name == objectName)
                {
                    return transforms[i];
                }
            }

            return null;
        }
    }
}
