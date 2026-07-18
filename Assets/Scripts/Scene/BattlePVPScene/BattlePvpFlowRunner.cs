using Audio.Interface;
using Battle;
using Battle.Interface;
using Battle.View;
using Camera.View;
using ClayEditor.Rigging;
using Cysharp.Threading.Tasks;
using Extensions;
using Lighthouse.Scene;
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
using UI.Battle.Interface;
using UI.Battle.View;
using UI.ClayEditor.View;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;

namespace Scene.BattlePVPScene
{
    /// <summary>
    /// BattlePVPシーンのマッチング完了後に戦闘フローを組み立て実行する
    /// </summary>
    public sealed class BattlePvpFlowRunner : MonoBehaviour, IBattlePvpFlowStarter
    {
        [Header("選択・UI")]
        [SerializeField] private LoadSlotView loadSlotView;
        [SerializeField] private Canvas selectionCanvas;
        [SerializeField] private BattleView battleView;
        [SerializeField] private Canvas battleUiCanvas;
        [SerializeField] private Canvas matchmakingCanvas;

        [Header("配置")]
        [SerializeField] private Transform playerSpawn;
        [SerializeField] private Transform enemySpawn;

        [Header("モデル構築・演出")]
        [SerializeField] private LoadedModelConfigurator configurator;
        [SerializeField] private BattleNpcStaging staging;

        [Header("カメラ")]
        [SerializeField] private ClayEditCameraView battleCamera;
        [SerializeField] private BattleFieldCameraProfile cameraProfile = new BattleFieldCameraProfile();

        [Header("ネットワーク")]
        [SerializeField] private BattlePvpSessionSpawner sessionSpawner;

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
        private IMonsterSelectionSession selectionSession;

        private CancellationTokenSource flowCts;
        private bool isRunning;
        private bool diagnosticsSubscribed;

        private void OnDestroy()
        {
            Debug.LogWarning(
                "[BattlePvpFlow] FlowRunnerが破棄されました"
                + $" sceneLoaded={gameObject.scene.isLoaded}"
                + $" quitting={applicationQuitting}");
            UnsubscribeDiagnostics();
            disconnectHandler?.Dispose();
            flowCts?.Cancel();
            flowCts?.Dispose();
        }

        private static bool applicationQuitting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetQuitFlag()
        {
            applicationQuitting = false;
            Application.quitting -= OnApplicationQuitting;
            Application.quitting += OnApplicationQuitting;
        }

        private static void OnApplicationQuitting()
        {
            applicationQuitting = true;
        }

        private void SubscribeDiagnostics()
        {
            if (diagnosticsSubscribed)
            {
                return;
            }

            diagnosticsSubscribed = true;
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded += OnSceneUnloaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;

            Unity.Netcode.NetworkManager manager = Unity.Netcode.NetworkManager.Singleton;
            if (manager != null)
            {
                manager.OnClientDisconnectCallback += OnClientDisconnectDiagnostic;
                manager.OnServerStopped += OnServerStoppedDiagnostic;
                manager.OnClientStopped += OnClientStoppedDiagnostic;
            }
        }

        private void UnsubscribeDiagnostics()
        {
            if (!diagnosticsSubscribed)
            {
                return;
            }

            diagnosticsSubscribed = false;
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded -= OnSceneUnloaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;

            Unity.Netcode.NetworkManager manager = Unity.Netcode.NetworkManager.Singleton;
            if (manager != null)
            {
                manager.OnClientDisconnectCallback -= OnClientDisconnectDiagnostic;
                manager.OnServerStopped -= OnServerStoppedDiagnostic;
                manager.OnClientStopped -= OnClientStoppedDiagnostic;
            }
        }

        private void OnSceneUnloaded(UnityEngine.SceneManagement.Scene scene)
        {
            Debug.LogWarning(
                $"[BattlePvpFlow] シーンアンロード検知 scene={scene.name}"
                + $" isLoaded={scene.isLoaded}");
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "BattlePVP")
            {
                return;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            Debug.LogWarning(
                $"[BattlePvpFlow] BattlePVPシーンロード検知 mode={mode}"
                + $" roots={roots.Length}"
                + $" isLoaded={scene.isLoaded}");
        }

        private void OnClientDisconnectDiagnostic(ulong clientId)
        {
            Debug.LogWarning($"[BattlePvpFlow] クライアント切断検知 clientId={clientId}");
        }

        private void OnServerStoppedDiagnostic(bool isHost)
        {
            Debug.LogWarning($"[BattlePvpFlow] サーバー停止検知 isHost={isHost}");
        }

        private void OnClientStoppedDiagnostic(bool isHost)
        {
            Debug.LogWarning($"[BattlePvpFlow] クライアント停止検知 isHost={isHost}");
        }

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
        }

        /// <summary>
        /// マッチングUI表示前に教室フィールドと選択UIを整える
        /// </summary>
        public void PrepareEntryLayout()
        {
            staging?.PrepareSelectionEntry();
            loadSlotView?.PrepareLayout();
            SetCanvasEnabled(selectionCanvas, false);
            SetCanvasEnabled(battleUiCanvas, false);
            SetMatchmakingCanvasEnabled(true);
        }

        /// <inheritdoc/>
        public void BeginAfterMatchmaking()
        {
            if (!this)
            {
                Debug.LogError("[BattlePvpFlow] BattlePvpFlowRunnerが破棄されています");
                return;
            }

            Debug.Log(
                "[BattlePvpFlow] マッチング完了 戦闘フローを開始します"
                + $" runner={name}"
                + $" instance={GetInstanceID()}"
                + $" isRunning={isRunning}");
            BattlePvpSceneDiagnostics.LogState("BeginAfterMatchmaking");
            EnsureBattlePvpSceneRootActive();
            SubscribeDiagnostics();
            SetMatchmakingCanvasEnabled(false);
            SchedulePostConnectionDiagnostics().Forget();
            StartFlow();
        }

        private void EnsureBattlePvpSceneRootActive()
        {
            GameObject battlePvpSceneRoot = BattlePvpSceneDiagnostics.FindBattlePvpSceneRoot();
            if (battlePvpSceneRoot == null)
            {
                Debug.LogError("[BattlePvpFlow] BattlePVPSceneルートが見つかりません");
                return;
            }

            if (!battlePvpSceneRoot.activeSelf)
            {
                Debug.LogWarning("[BattlePvpFlow] BattlePVPSceneルートを再有効化します");
                battlePvpSceneRoot.SetActive(true);
            }
        }

        private async UniTaskVoid SchedulePostConnectionDiagnostics()
        {
            await UniTask.Delay(System.TimeSpan.FromSeconds(0.5f));
            BattlePvpSceneDiagnostics.LogState("接続後+0.5s");
            await UniTask.Delay(System.TimeSpan.FromSeconds(1.5f));
            BattlePvpSceneDiagnostics.LogState("接続後+2.0s");
        }

        /// <summary>
        /// 戦闘フローを開始する
        /// </summary>
        public void StartFlow()
        {
            if (!this)
            {
                Debug.LogWarning("[BattlePvpFlow] StartFlow runner破棄済み");
                return;
            }

            if (isRunning)
            {
                Debug.LogWarning("[BattlePvpFlow] StartFlow skipped 既にフロー実行中");
                return;
            }

            if (GetComponent<BattleHitStopClock>() == null)
            {
                gameObject.AddComponent<BattleHitStopClock>();
            }

            if (GetComponent<BattleHitEffectView>() == null)
            {
                gameObject.AddComponent<BattleHitEffectView>();
            }

            if (GetComponent<BattleDamagePopupView>() == null)
            {
                gameObject.AddComponent<BattleDamagePopupView>();
            }

            BattleHitStopClock.Clear();

            flowCts?.Cancel();
            flowCts?.Dispose();
            flowCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            isRunning = true;
            disconnectHandler?.BeginMonitoring();
            RunAsync(flowCts.Token).Forget();
        }

        /// <summary>
        /// 戦闘フローを停止する
        /// </summary>
        public void StopFlow()
        {
            flowCts?.Cancel();
            BattleHitStopClock.Clear();
        }

        /// <summary>
        /// マッチングUIの表示を切り替える
        /// </summary>
        public void SetMatchmakingCanvasEnabled(bool isEnabled)
        {
            if (matchmakingCanvas == null)
            {
                return;
            }

            matchmakingCanvas.gameObject.SetActive(isEnabled);
            matchmakingCanvas.enabled = isEnabled;
        }

        /// <summary>
        /// 暗転後にセーブスロット選択を表示する
        /// </summary>
        public async UniTask RevealSelectionAsync(CancellationToken cancellationToken)
        {
            BattlePvpSceneDiagnostics.LogState("RevealSelectionAsync開始");
            EnsureBattlePvpSceneRootActive();
            staging?.PrepareSelectionEntry();
            SetMatchmakingCanvasEnabled(false);
            loadSlotView?.PrepareLayout();

            if (selectionCanvas != null)
            {
                selectionCanvas.gameObject.SetActive(true);
            }

            if (loadSlotView != null)
            {
                loadSlotView.gameObject.SetActive(true);
            }

            Debug.Log("[BattlePvpFlow] 選択Canvasを有効化します");
            if (canvasTransition != null)
            {
                await canvasTransition.SetCanvasEnabledAsync(
                    selectionCanvas,
                    true,
                    cancellationToken,
                    fadeOutBeforeChange: false,
                    fadeInAfterChange: false);
            }
            else
            {
                loadSlotView?.PrepareForDisplay();
                SetCanvasEnabled(selectionCanvas, true);
            }

            if (loadSlotView != null)
            {
                loadSlotView.EnsureSelectionReady();
            }
            await FinalizeSelectionLayoutAsync(cancellationToken);
            BattlePvpSceneDiagnostics.LogState("RevealSelectionAsync完了");
            DumpSelectionCanvasState();
            DumpSelectionContentState();
        }

        private async UniTask FinalizeSelectionLayoutAsync(CancellationToken cancellationToken)
        {
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);
            Canvas.ForceUpdateCanvases();
            loadSlotView?.PrepareLayout();
            loadSlotView?.EnsureSelectionReady();

            ModelSaveSlotScrollListView scrollList = loadSlotView != null
                ? loadSlotView.GetComponentInChildren<ModelSaveSlotScrollListView>(true)
                : null;
            scrollList?.ForceSelectionLayout();
            RectTransform selectionRect = selectionCanvas.GetComponent<RectTransform>();
            if (selectionRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(selectionRect);
            }

            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);
            Canvas.ForceUpdateCanvases();
        }

        private void DumpSelectionContentState()
        {
            if (selectionCanvas == null)
            {
                return;
            }

            Transform background = selectionCanvas.transform.Find("BackGround");
            ModelSaveSlotScrollListView scrollList =
                selectionCanvas.GetComponentInChildren<ModelSaveSlotScrollListView>(true);
            Transform scrollRoot = scrollList != null
                ? scrollList.transform.Find("SlotScrollList")
                : selectionCanvas.transform.Find("SlotScrollList");
            Transform scrollContent = scrollRoot != null
                ? scrollRoot.Find("Viewport/Content")
                : null;
            Transform viewport = scrollRoot != null
                ? scrollRoot.Find("Viewport")
                : null;
            int rowCount = scrollContent != null ? scrollContent.childCount : -1;
            int activeChildCount = 0;
            for (int i = 0; i < selectionCanvas.transform.childCount; i++)
            {
                if (selectionCanvas.transform.GetChild(i).gameObject.activeSelf)
                {
                    activeChildCount++;
                }
            }

            RectTransform scrollListRect = scrollList != null
                ? scrollList.transform as RectTransform
                : null;
            RectTransform viewportRect = viewport as RectTransform;
            RectTransform contentRect = scrollContent as RectTransform;
            RectTransform hostRect = selectionCanvas.GetComponent<RectTransform>();
            Transform firstRow = scrollContent != null && scrollContent.childCount > 0
                ? scrollContent.GetChild(0)
                : null;
            CanvasGroup firstRowGroup = firstRow != null
                ? firstRow.GetComponent<CanvasGroup>()
                : null;
            CanvasGroup sceneGroup = GetComponent<CanvasGroup>();
            Debug.Log(
                "[BattlePvpFlow] 選択UI内容"
                + $" childCount={selectionCanvas.transform.childCount}"
                + $" activeChildCount={activeChildCount}"
                + $" background={(background != null)}"
                + $" scrollList={(scrollList != null)}"
                + $" scrollRoot={(scrollRoot != null)}"
                + $" scrollActive={(scrollRoot != null && scrollRoot.gameObject.activeSelf)}"
                + $" rowCount={rowCount}"
                + $" hostRect={(hostRect != null ? hostRect.rect.size.ToString() : "null")}"
                + $" scrollListRect={(scrollListRect != null ? scrollListRect.rect.size.ToString() : "null")}"
                + $" viewportRect={(viewportRect != null ? viewportRect.rect.size.ToString() : "null")}"
                + $" contentSize={(contentRect != null ? contentRect.sizeDelta.ToString() : "null")}"
                + $" firstRowActive={(firstRow != null && firstRow.gameObject.activeSelf)}"
                + $" firstRowAlpha={(firstRowGroup != null ? firstRowGroup.alpha : 1f)}"
                + $" sceneGroupAlpha={(sceneGroup != null ? sceneGroup.alpha : -1f)}"
                + $" loadSlotView={(loadSlotView != null)}");
        }

        // 選択UIが表示されない不具合切り分け用に描画状態を出力する
        private void DumpSelectionCanvasState()
        {
            if (selectionCanvas == null)
            {
                Debug.LogWarning("[BattlePvpFlow] selectionCanvas未設定");
                return;
            }

            Debug.Log(
                "[BattlePvpFlow] 選択Canvas状態"
                + $" enabled={selectionCanvas.enabled}"
                + $" active={selectionCanvas.gameObject.activeInHierarchy}"
                + $" mode={selectionCanvas.renderMode}"
                + $" worldCamera={(selectionCanvas.worldCamera != null ? selectionCanvas.worldCamera.name : "null")}"
                + $" localScale={selectionCanvas.transform.localScale}"
                + $" lossyScale={selectionCanvas.transform.lossyScale}"
                + $" parent={(selectionCanvas.transform.parent != null ? selectionCanvas.transform.parent.name : "null")}"
                + $" sortOrder={selectionCanvas.sortingOrder}"
                + $" parent={(selectionCanvas.transform.parent != null ? selectionCanvas.transform.parent.name : "null")}"
                + $" matchmakingActive={(matchmakingCanvas != null && matchmakingCanvas.gameObject.activeSelf)}");
        }

        private async UniTask RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                Debug.Log("[BattlePvpFlow] RunAsync開始 マッチングUIを閉じます");
                SetCanvasEnabled(matchmakingCanvas, false);
                await RevealSelectionAsync(cancellationToken);
                Debug.Log($"[BattlePvpFlow] 選択UI表示完了 sessionSpawner={(sessionSpawner != null)}");

                sessionSpawner?.TrySpawnPlayersIfNeeded();

                var loader = new BattleParticipantLoader(importer, saveService, configurator);
                BattlePvpInputRelay inputRelay = null;
                var context = new BattleFlow.Context
                {
                    PlayerSpawn = playerSpawn,
                    EnemySpawn = enemySpawn,
                    BattleUiCanvas = battleUiCanvas,
                    EnemySlotIndex = 0,
                    BattleCamera = battleCamera,
                    CameraProfile = cameraProfile,
                    PresentationTransition = canvasTransition,
                    HitEffect = GetComponent<BattleHitEffectView>(),
                    DamagePopup = GetComponent<BattleDamagePopupView>(),
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
                    Debug.Log(
                        $"[BattlePvpFlow] ローカルリレー取得 relay={(inputRelay != null)}"
                        + $" IsOwner={(inputRelay != null && inputRelay.IsOwner)}");
                    if (inputRelay == null)
                    {
                        Debug.LogError("[BattlePvpFlow] ローカルリレーが取得できませんでした");
                        return default;
                    }

                    context.EnemyAi = new NetworkBattleRemoteEnemyAi(inputRelay);
                    context.CombatSync = new BattlePvpCombatSync(inputRelay);
                    inputRelay.ResetSessionState();
                    context.WaitForMatchupStartAsync = CreateMatchupStartWaiter(inputRelay);
                    return await LoadOpponentAsync(loader, inputRelay, token);
                };
                context.OnBattleInputCreated = input =>
                {
                    inputRelay?.BeginBattleInput(input);
                };
                context.OnBattleInputDisposed = () =>
                {
                    inputRelay?.EndBattleInput();
                };

                var flow = new BattleFlow(selectionSession, battleView, staging, loader, context, bgmService, seService);
                while (!cancellationToken.IsCancellationRequested)
                {
                    Debug.Log("[BattlePvpFlow] BattleFlow開始 モンスター選択待機");
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
                Debug.LogWarning("[BattlePvpFlow] RunAsyncキャンセル");
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

            pvpVictoryReturnView = GetComponentInChildren<BattlePvpVictoryReturnView>(true);
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
            ClearSpawnedModels(playerSpawn);
            ClearSpawnedModels(enemySpawn);
            SetCanvasEnabled(battleUiCanvas, false);
            pvpVictoryReturnView?.SetDualButtonsVisible(false);

            if (sceneFade != null)
            {
                await sceneFade.FadeOutAsync(cancellationToken);
            }

            await RevealSelectionAsync(cancellationToken);
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
            int localSlot = loadSlotView != null ? loadSlotView.SelectedSlotIndex : 0;
            Debug.Log($"[BattlePvpFlow] スロット送信 localSlot={localSlot} IsOwner={inputRelay.IsOwner}");
            inputRelay.SubmitSlotSelection(localSlot);
            await BattlePvpOpponentWaitScope.RunAsync(
                opponentWaitView,
                inputRelay.WaitForBothSlotsAsync,
                cancellationToken);
            Debug.Log($"[BattlePvpFlow] 両者スロット確定 opponentSlot={inputRelay.OpponentSlotIndex}");
            return await loader.LoadPlayerSlotAsync(inputRelay.OpponentSlotIndex, enemySpawn, cancellationToken);
        }

        private static void SetCanvasEnabled(Canvas canvas, bool isEnabled)
        {
            if (canvas == null)
            {
                return;
            }

            if (isEnabled)
            {
                Canvas root = canvas.rootCanvas;
                if (root != null && !root.enabled)
                {
                    root.enabled = true;
                }
            }

            canvas.enabled = isEnabled;
        }
    }
}
