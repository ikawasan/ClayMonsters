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
using Scene.BattleNpcScene.Interface;
using Scene.Core;
using Scene.Core.Interface;
using System.Threading;
using TMPro;
using UI.Battle.View;
using UnityEngine;
using VContainer;
using static Scene.TitleScene.TitleScene;

namespace Scene.BattleNpcScene
{
    /// <summary>
    /// BattleNpcのSerializeField参照をBattleFlowへ渡して実行する
    /// </summary>
    public sealed class BattleFlowRunner : MonoBehaviour, IBattleFlowRunner
    {
        [Header("選択・UI")]
        [SerializeField] private LHButton titleReturnButton;
        [SerializeField] private BattleView battleView;
        [SerializeField] private Canvas battleUiCanvas;
        [SerializeField] private BattleTipsView tipsView;

        [Header("配置")]
        [SerializeField] private Transform playerSpawn;
        [SerializeField] private Transform enemySpawn;

        [Header("モデル構築・演出")]
        [SerializeField] private LoadedModelConfigurator configurator;
        [SerializeField] private BattleNpcStaging staging;

        [Header("カメラ")]
        [SerializeField] private ClayEditCameraView battleCamera;
        [SerializeField] private BattleFieldCameraProfile cameraProfile = new BattleFieldCameraProfile();

        [Header("敵設定")]
        [SerializeField] private int enemySlotIndex;

        [Header("レベルデザイン")]
        [SerializeField] private BattleLevelDesignSettings levelDesignSettings;

        private IClayModelImporter importer;
        private IClayModelSaveService saveService;
        private INpcBattleProgressService npcBattleProgress;
        private IPointsService pointsService;
        private ISkillTreeService skillTreeService;
        private IBattleCanvasTransition presentationTransition;
        private IBgmService bgmService;
        private ISeService seService;
        private IBattleDualVictoryReturnView victoryDualReturnView;
        private IClayMonsterSceneManager sceneManager;
        private IMonsterSelectionSession selectionSession;

        private CancellationTokenSource flowCts;
        private bool isRunning;
        private System.IDisposable titleReturnSubscription;
        private GameObject trackedPlayerModel;
        private GameObject trackedEnemyModel;

        /// <summary>プレイヤー配置先</summary>
        public Transform PlayerSpawn => playerSpawn;

        /// <summary>敵配置先</summary>
        public Transform EnemySpawn => enemySpawn;

        /// <summary>戦闘UIキャンバス</summary>
        public Canvas BattleUiCanvas => battleUiCanvas;

        /// <summary>戦闘チュートリアルTips</summary>
        public IBattleTipsView TipsView => ResolveTipsView();

        /// <summary>戦闘カメラ</summary>
        public ClayEditCameraView BattleCamera => battleCamera;

        /// <summary>カメラプロファイル</summary>
        public BattleFieldCameraProfile CameraProfile => cameraProfile;

        /// <summary>演出</summary>
        public BattleNpcStaging Staging => staging;

        /// <summary>レベルデザイン</summary>
        public BattleLevelDesignSettings LevelDesignSettings => levelDesignSettings;

        /// <summary>勝利後の単一戻りUI(Dual優先のため通常は未使用)</summary>
        public IBattleVictoryReturnView VictoryReturnView => null;

        /// <summary>勝利後のタイトル戻りと再戦UI</summary>
        public IBattleDualVictoryReturnView VictoryDualReturnView => victoryDualReturnView;

        /// <summary>ヒット演出</summary>
        public BattleHitEffectView HitEffect => GetComponent<BattleHitEffectView>();

        /// <summary>ダメージポップ</summary>
        public BattleDamagePopupView DamagePopup => GetComponent<BattleDamagePopupView>();

        [Inject]
        public void Construct(
            IClayModelImporter importer,
            IClayModelSaveService saveService,
            INpcBattleProgressService npcBattleProgress,
            IPointsService pointsService,
            ISkillTreeService skillTreeService,
            IBattleCanvasTransition presentationTransition,
            IBgmService bgmService,
            ISeService seService,
            IBattleDualVictoryReturnView victoryDualReturnView,
            IClayMonsterSceneManager sceneManager,
            IMonsterSelectionSession selectionSession)
        {
            this.importer = importer;
            this.saveService = saveService;
            this.npcBattleProgress = npcBattleProgress;
            this.pointsService = pointsService;
            this.skillTreeService = skillTreeService;
            this.presentationTransition = presentationTransition;
            this.bgmService = bgmService;
            this.seService = seService;
            this.victoryDualReturnView = victoryDualReturnView;
            this.sceneManager = sceneManager;
            this.selectionSession = selectionSession;

            titleReturnSubscription?.Dispose();
            if (titleReturnButton != null)
            {
                TMP_Text label = titleReturnButton.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    label.text = "戻る";
                }

                titleReturnSubscription = titleReturnButton.SubscribeOnClick(OnClickTitleReturn);
            }
            else
            {
                titleReturnSubscription = null;
            }
        }

        private void OnDestroy()
        {
            titleReturnSubscription?.Dispose();
            flowCts?.Cancel();
            flowCts?.Dispose();
        }

        /// <inheritdoc />
        public void StartFlow()
        {
            if (isRunning)
            {
                return;
            }

            EnsureBattleComponents();
            flowCts?.Cancel();
            flowCts?.Dispose();
            flowCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());

            RunInternalAsync(flowCts.Token).Forget();
        }

        private async UniTask RunInternalAsync(CancellationToken cancellationToken)
        {
            isRunning = true;

            try
            {
                var loader = new BattleParticipantLoader(importer, saveService, configurator);
                BattleFlow.Context context = BattleFlowContextBuilder.Build(
                    this,
                    saveService,
                    presentationTransition,
                    enemySlotIndex);
                context.RegisterSpawnedParticipants = RegisterSpawnedParticipants;
                context.OnNpcBattleSettled = OnNpcBattleSettled;

                var flow = new BattleFlow(
                    selectionSession,
                    battleView,
                    staging,
                    loader,
                    context,
                    bgmService,
                    seService);

                while (!cancellationToken.IsCancellationRequested)
                {
                    BattleVictoryReturnChoice choice = await flow.RunAsync(cancellationToken);
                    if (choice == BattleVictoryReturnChoice.Rematch)
                    {
                        await PrepareRematchAsync(cancellationToken);
                        continue;
                    }

                    await ReturnToTitleAsync(cancellationToken);
                    break;
                }
            }
            catch (System.OperationCanceledException)
            {
                Debug.LogWarning("[BattleFlowRunner] 戦闘フローがキャンセルされました");
                presentationTransition?.ReleasePresentationInput();
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"[BattleFlowRunner] 戦闘フローで例外が発生しました\n{exception}");
                presentationTransition?.ReleasePresentationInput();
            }
            finally
            {
                isRunning = false;
            }
        }

        /// <inheritdoc />
        public void Stop()
        {
            flowCts?.Cancel();
            BattleHitStopClock.Clear();
        }

        /// <inheritdoc />
        public void CleanupForLeave()
        {
            Stop();
            isRunning = false;
            DestroyTrackedParticipants();
            ClearSpawnedModels(playerSpawn);
            ClearSpawnedModels(enemySpawn);
            CanvasVisibilityUtility.SetCanvasEnabled(battleUiCanvas, false);
            VictoryDualReturnView?.SetDualButtonsVisible(false);
            ResolveTipsView()?.HideAll();
            staging?.PrepareSelectionEntry();
            presentationTransition?.ReleasePresentationInput();
        }

        private async UniTask PrepareRematchAsync(CancellationToken cancellationToken)
        {
            DestroyTrackedParticipants();
            ClearSpawnedModels(playerSpawn);
            ClearSpawnedModels(enemySpawn);
            CanvasVisibilityUtility.SetCanvasEnabled(battleUiCanvas, false);
            VictoryDualReturnView?.SetDualButtonsVisible(false);
            ResolveTipsView()?.HideAll();
            staging?.PrepareSelectionEntry();

            // 再戦は自分のキャラ選択からやり直す
            selectionSession?.PrepareEntry();

            if (presentationTransition != null)
            {
                await presentationTransition.FadeOutAsync(cancellationToken);
            }
        }

        private void OnNpcBattleSettled(
            bool playerWon,
            int settledEnemySlotIndex,
            EnemyStrengthTier settledStrengthTier)
        {
            if (!playerWon)
            {
                return;
            }

            if (npcBattleProgress == null)
            {
                Debug.LogError("[BattleFlowRunner] npcBattleProgressが未注入です");
            }
            else
            {
                npcBattleProgress.RegisterVictory(settledEnemySlotIndex, settledStrengthTier);
            }

            if (pointsService == null)
            {
                Debug.LogError("[BattleFlowRunner] pointsServiceが未注入です");
                return;
            }

            int reward = BattlePointsRules.ResolveNpcVictoryPoints(settledStrengthTier);
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

        private void OnClickTitleReturn()
        {
            if (sceneManager == null || sceneManager.IsTransition)
            {
                return;
            }

            // 敵選択中の戻るはタイトルではなくプレイヤー選択へ戻す
            if (selectionSession != null
                && selectionSession.SavePool == ModelSavePool.Enemy)
            {
                selectionSession.CancelWaitingSelection();
                return;
            }

            Stop();
            ReturnToTitleAsync(CancellationToken.None).Forget();
        }

        private void EnsureBattleComponents()
        {
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
        }

        private IBattleTipsView ResolveTipsView()
        {
            if (tipsView != null)
            {
                return tipsView;
            }

            if (battleView != null)
            {
                Transform hudRoot = battleView.transform.parent;
                if (hudRoot != null)
                {
                    tipsView = hudRoot.GetComponentInChildren<BattleTipsView>(true);
                }
            }

            return tipsView;
        }

        private async UniTask ReturnToTitleAsync(CancellationToken cancellationToken)
        {
            if (sceneManager == null || sceneManager.IsTransition)
            {
                return;
            }

            await sceneManager.TransitionScene(
                new TitleTransitionData(),
                TransitionType.Exclusive,
                ClayMonstersMainSceneId.Title);
        }
    }
}
