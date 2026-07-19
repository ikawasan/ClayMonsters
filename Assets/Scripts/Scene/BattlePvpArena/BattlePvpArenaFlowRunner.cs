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
using SaveData.Interface;
using Scene.BattleNpcScene;
using Scene.BattlePVPScene.Interface;
using Scene.BattlePVPScene.Network;
using Scene.BattlePVPScene.Service;
using Scene.BattlePVPScene.View;
using Scene.Core;
using Scene.Core.Interface;
using Scene.PvpLobby.Interface;
using System.Threading;
using static Scene.TitleScene.TitleScene;
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

        private CancellationTokenSource flowCts;
        private bool isRunning;
        private System.IDisposable titleReturnSubscription;
        private GameObject trackedPlayerModel;
        private GameObject trackedEnemyModel;

        private IMonsterSelectionSession selectionSession;

        [Inject]
        public void Construct(
            IClayModelImporter importer,
            IClayModelSaveService saveService,
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
                () => this.GetCancellationTokenOnDestroy());

            titleReturnSubscription?.Dispose();
            titleReturnSubscription = titleReturnButton != null
                ? titleReturnButton.SubscribeOnClick(OnClickTitleReturn)
                : null;
        }

        private void OnDestroy()
        {
            titleReturnSubscription?.Dispose();
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

            if (titleReturnButton == null)
            {
                Transform titleReturnTransform = FindTransformByName(sceneRoot, "TitleReturnButton");
                if (titleReturnTransform != null)
                {
                    titleReturnButton = titleReturnTransform.GetComponentInChildren<LHButton>(true);
                }
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
            selectionSession?.PrepareEntry();
            staging?.PrepareSelectionEntry();
            battleCamera?.SetCameraEnable(false);
            loadSlotView?.PrepareLayout();
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
            DestroyTrackedParticipants();
            selectionSession?.HideForLeave();
            loadSlotView?.HideForLeave();
            ClearSpawnedModels(playerSpawn);
            ClearSpawnedModels(enemySpawn);
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

            loadSlotView?.EnsureSelectionReady();
            await FinalizeSelectionLayoutAsync(cancellationToken);

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
            Debug.Log("[BattlePvpArena] StartFlow");
            disconnectHandler?.BeginMonitoring();
            RunAsync(flowCts.Token).Forget();
        }

        /// <summary>
        /// 戦闘フローを停止する
        /// </summary>
        public void StopFlow()
        {
            flowCts?.Cancel();
            isRunning = false;
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

        private async UniTask RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                Debug.Log("[BattlePvpArena] 戦闘フロー開始");
                EnsureBattleComponents();
                await WaitForSceneCanvasReadyAsync(cancellationToken);
                await RevealSelectionAsync(cancellationToken);

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
                    sessionSpawner?.TrySpawnPlayersIfNeeded();
                    inputRelay = sessionSpawner != null
                        ? await sessionSpawner.WaitForLocalRelayAsync(token)
                        : null;
                    if (inputRelay == null)
                    {
                        Debug.LogError("[BattlePvpArena] ローカルリレーが取得できませんでした");
                        return default;
                    }

                    context.EnemyAi = new NetworkBattleRemoteEnemyAi(inputRelay);
                    context.CombatSync = new BattlePvpCombatSync(inputRelay);
                    inputRelay.ResetSessionState();
                    context.WaitForMatchupStartAsync = CreateMatchupStartWaiter(inputRelay);
                    return await LoadOpponentAsync(loader, inputRelay, token);
                };
                context.OnBattleInputCreated = input => inputRelay?.BeginBattleInput(input);
                context.OnBattleInputDisposed = () => inputRelay?.EndBattleInput();
                context.RegisterSpawnedParticipants = RegisterSpawnedParticipants;

                var flow = new BattleFlow(selectionSession, battleView, staging, loader, context, bgmService, seService);
                while (!cancellationToken.IsCancellationRequested)
                {
                    BattleVictoryReturnChoice choice = await flow.RunAsync(cancellationToken);
                    if (choice == BattleVictoryReturnChoice.Rematch)
                    {
                        await PrepareRematchAsync(inputRelay, cancellationToken);
                        continue;
                    }

                    await ReturnToTitleAsync(cancellationToken);
                    break;
                }
            }
            catch (System.OperationCanceledException)
            {
                Debug.LogWarning("[BattlePvpArena] RunAsyncキャンセル");
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                isRunning = false;
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
            inputRelay?.ResetForRematch();
            DestroyTrackedParticipants();
            ClearSpawnedModels(playerSpawn);
            ClearSpawnedModels(enemySpawn);
            CanvasVisibilityUtility.SetCanvasEnabled(battleUiCanvas, false);
            pvpVictoryReturnView?.SetDualButtonsVisible(false);

            if (sceneFade != null)
            {
                await sceneFade.FadeOutAsync(cancellationToken);
            }

            await RevealSelectionAsync(cancellationToken);
        }

        private void RegisterSpawnedParticipants(GameObject playerModel, GameObject enemyModel)
        {
            trackedPlayerModel = playerModel;
            trackedEnemyModel = enemyModel;
        }

        private void DestroyTrackedParticipants()
        {
            if (trackedPlayerModel != null)
            {
                Object.Destroy(trackedPlayerModel);
                trackedPlayerModel = null;
            }

            if (trackedEnemyModel != null)
            {
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
                Object.Destroy(spawn.GetChild(i).gameObject);
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

        private async UniTask FinalizeSelectionLayoutAsync(CancellationToken cancellationToken)
        {
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);
            DetachSelectionCanvasToSceneRoot();
            loadSlotView?.PrepareLayout(reparentToUiRoot: false);
            loadSlotView?.EnsureSelectionReady();

            ModelSaveSlotScrollListView scrollList = loadSlotView != null
                ? loadSlotView.GetComponentInChildren<ModelSaveSlotScrollListView>(true)
                : null;
            scrollList?.ForceSelectionLayout();

            if (selectionCanvas != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(selectionCanvas.GetComponent<RectTransform>());
            }

            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);
            Canvas.ForceUpdateCanvases();
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

            Debug.Log(
                "[BattlePvpArena] スロット送信"
                + $" localSlot={localSlot}"
                + $" IsOwner={inputRelay.IsOwner}");
            inputRelay.SubmitSlotSelection(localSlot);
            await BattlePvpOpponentWaitScope.RunAsync(
                opponentWaitView,
                inputRelay.WaitForBothSlotsAsync,
                cancellationToken);
            Debug.Log(
                "[BattlePvpArena] 両者スロット確定"
                + $" localSlot={localSlot}"
                + $" opponentSlot={inputRelay.OpponentSlotIndex}");
            return await loader.LoadPlayerSlotAsync(inputRelay.OpponentSlotIndex, enemySpawn, cancellationToken);
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

        private async UniTask ReturnToTitleAsync(CancellationToken cancellationToken)
        {
            if (sceneManager == null || sceneManager.IsTransition)
            {
                return;
            }

            disconnectHandler?.SuppressNotifications();
            pvpSessionController?.EndSession();
            await sceneManager.TransitionScene(
                new TitleTransitionData(),
                TransitionType.Exclusive,
                ClayMonstersMainSceneId.Title);
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
