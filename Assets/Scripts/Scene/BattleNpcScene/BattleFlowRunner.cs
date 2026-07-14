using Audio.Interface;
using Battle;
using Battle.Interface;
using Battle.View;
using Camera.View;
using ClayEditor.Rigging;
using Cysharp.Threading.Tasks;
using Extensions;
using LighthouseExtends.UIComponent.Button;
using Lighthouse.Scene;
using SaveData.Interface;
using Scene.BattleNpcScene.Interface;
using Scene.BattleNpcScene.View;
using Scene.Core;
using Scene.Core.Interface;
using System.Threading;
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
        private IBattleCanvasTransition presentationTransition;
        private IBgmService bgmService;
        private ISeService seService;
        private IBattleNpcView battleNpcView;
        private IClayMonsterSceneManager sceneManager;
        private IMonsterSelectionSession selectionSession;

        private CancellationTokenSource flowCts;
        private bool isRunning;
        private System.IDisposable titleReturnSubscription;

        /// <summary>プレイヤー配置先</summary>
        public Transform PlayerSpawn => playerSpawn;

        /// <summary>敵配置先</summary>
        public Transform EnemySpawn => enemySpawn;

        /// <summary>戦闘UIキャンバス</summary>
        public Canvas BattleUiCanvas => battleUiCanvas;

        /// <summary>戦闘カメラ</summary>
        public ClayEditCameraView BattleCamera => battleCamera;

        /// <summary>カメラプロファイル</summary>
        public BattleFieldCameraProfile CameraProfile => cameraProfile;

        /// <summary>演出</summary>
        public BattleNpcStaging Staging => staging;

        /// <summary>レベルデザイン</summary>
        public BattleLevelDesignSettings LevelDesignSettings => levelDesignSettings;

        /// <summary>勝利後戻るUI</summary>
        public IBattleNpcView VictoryReturnView => battleNpcView;

        /// <summary>ヒット演出</summary>
        public BattleHitEffectView HitEffect => GetComponent<BattleHitEffectView>();

        /// <summary>ダメージポップ</summary>
        public BattleDamagePopupView DamagePopup => GetComponent<BattleDamagePopupView>();

        [Inject]
        public void Construct(
            IClayModelImporter importer,
            IClayModelSaveService saveService,
            IBattleCanvasTransition presentationTransition,
            IBgmService bgmService,
            ISeService seService,
            IBattleNpcView battleNpcView,
            IClayMonsterSceneManager sceneManager,
            IMonsterSelectionSession selectionSession)
        {
            this.importer = importer;
            this.saveService = saveService;
            this.presentationTransition = presentationTransition;
            this.bgmService = bgmService;
            this.seService = seService;
            this.battleNpcView = battleNpcView;
            this.sceneManager = sceneManager;
            this.selectionSession = selectionSession;

            titleReturnSubscription?.Dispose();
            titleReturnSubscription = titleReturnButton != null
                ? titleReturnButton.SubscribeOnClick(OnClickTitleReturn)
                : null;
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

                var flow = new BattleFlow(
                    selectionSession,
                    battleView,
                    staging,
                    loader,
                    context,
                    bgmService,
                    seService);

                await flow.RunAsync(cancellationToken);
                await ReturnToTitleAsync(cancellationToken);
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

        private void OnClickTitleReturn()
        {
            if (sceneManager == null || sceneManager.IsTransition)
            {
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
