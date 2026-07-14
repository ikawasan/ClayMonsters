using Audio;

using Audio.Interface;

using Battle;

using Battle.Interface;

using Battle.Input;

using Battle.Presenter;

using Battle.View;

using Camera.View;

using ClayEditor.Rigging;

using Cysharp.Threading.Tasks;

using Extensions;

using R3;

using SaveData;

using SaveData.Interface;

using Scene.BattleNpcScene;

using Scene.BattleNpcScene.View;

using Scene.Core.Interface;

using Scene.TrainingScene.Domain;

using System.Threading;

using UI.Battle.View;

using UnityEngine;

using VContainer;



namespace Scene.TrainingScene

{

    /// <summary>

    /// 育成シーンの放課後戦闘を実行する

    /// </summary>

    public sealed class TrainingBattleRunner : MonoBehaviour

    {

        [Header("UI")]

        [SerializeField] private BattleView battleView;

        [SerializeField] private Canvas battleUiCanvas;

        [SerializeField] private GameObject battleCanvasRoot;



        [Header("配置")]

        [SerializeField] private Transform playerSpawn;

        [SerializeField] private Transform enemySpawn;



        [Header("モデル構築")]

        [SerializeField] private LoadedModelConfigurator configurator;

        [SerializeField] private BattleStartOverlayView overlayView;

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

        private TrainingDisplay trainingDisplay;



        private GameObject lastEnemyModel;

        /// <summary>
        /// 戦闘用プレイヤースポーン
        /// </summary>
        public Transform BattlePlayerSpawn => playerSpawn;

        /// <summary>
        /// シーン退場時に戦闘UIを隠す
        /// </summary>
        public void HideForLeave()
        {
            overlayView?.HideImmediate();
            overlayView?.HideVsUi();
            SetBattleSceneActive(false);
            DestroyEnemyModel();
        }

        [Inject]

        public void Construct(

            IClayModelImporter importer,

            IClayModelSaveService saveService,

            ISceneFade sceneFade,

            IBattleCanvasTransition canvasTransition,

            IBgmService bgmService,

            ISeService seService,

            TrainingDisplay trainingDisplay)

        {

            this.importer = importer;

            this.saveService = saveService;

            this.sceneFade = sceneFade;

            this.canvasTransition = canvasTransition;

            this.bgmService = bgmService;

            this.seService = seService;

            this.trainingDisplay = trainingDisplay;

        }



        /// <summary>

        /// 放課後戦闘を実行する

        /// </summary>

        /// <param name="session">育成セッション</param>

        /// <param name="playerModel">プレイヤーモデル</param>

        /// <param name="modelName">プレイヤー名</param>

        /// <param name="enemySlotIndex">敵スロット</param>

        /// <param name="cancellationToken">キャンセルトークン</param>

        public async UniTask<TrainingBattleResult> RunAfterSchoolBattleAsync(

            TrainingSession session,

            GameObject playerModel,

            string modelName,

            int enemySlotIndex,

            CancellationToken cancellationToken)

        {

            if (session == null || playerModel == null)

            {

                Debug.LogError("[TrainingBattleRunner] セッションまたはプレイヤーモデルがありません");

                return new TrainingBattleResult(false, false, string.Empty);

            }



            if (battleView == null || configurator == null || playerSpawn == null || enemySpawn == null)

            {

                Debug.LogError("[TrainingBattleRunner] 戦闘UIまたは配置参照が未設定です");

                return new TrainingBattleResult(false, false, string.Empty);

            }



            if (importer == null || saveService == null)

            {

                Debug.LogError("[TrainingBattleRunner] セーブサービスが未注入です");

                return new TrainingBattleResult(false, false, string.Empty);

            }



            string enemyName = saveService.GetSlot(ModelSavePool.Enemy, enemySlotIndex)?.modelName ?? "敵";



            BattleHitStopClock.Clear();

            GameplayTime.Reset();



            if (sceneFade != null)

            {

                await sceneFade.FadeInAsync(cancellationToken);

            }



            BattleSystem system = null;

            BattleKeyboardMovementInput movementInput = null;

            BattlePresenter presenter = null;

            BattleFieldPresenter fieldPresenter = null;

            BattleFieldCameraPresenter cameraPresenter = null;

            BattleCombatFeedbackPresenter combatFeedbackPresenter = null;

            BattleFinishPresenter finishPresenter = null;



            try

            {

                var loader = new BattleParticipantLoader(importer, saveService, configurator);

                BattleParticipant player = loader.BuildFromTrainingModel(

                    playerModel,

                    modelName,

                    session.CurrentStatus,

                    session.AttackMotions,

                    playerSpawn);

                if (!player.IsValid)

                {

                    Debug.LogError("[TrainingBattleRunner] プレイヤー参加者の構築に失敗しました");

                    return new TrainingBattleResult(false, false, enemyName);

                }



                BattleParticipant enemy = await loader.LoadEnemyAsync(enemySlotIndex, enemySpawn, cancellationToken);

                if (!enemy.IsValid)

                {

                    Debug.LogError($"[TrainingBattleRunner] 敵スロット{enemySlotIndex}の読み込みに失敗しました");

                    return new TrainingBattleResult(false, false, enemyName);

                }



                lastEnemyModel = enemy.Model;

                if (player.Model != null)
                {
                    player.Model.SetActive(true);
                }

                if (overlayView != null)
                {
                    overlayView.enabled = true;
                    overlayView.EnsureReadyForTrainingBattle();
                }

                SetBattleSceneActive(false);

                BattleSettings battleSettings = BattleLevelDesignSettings.Resolve(levelDesignSettings);

                var stagingContext = new BattleStagingContext
                {
                    Player = player.Unit,
                    Enemy = enemy.Unit,
                    PlayerSpawn = playerSpawn,
                    EnemySpawn = enemySpawn,
                    PlayerModel = player.Model.transform,
                    EnemyModel = enemy.Model.transform,
                    ScreenFade = canvasTransition,
                    InitialBattleDistance = battleSettings.MaxDistance,
                    MaxBattleDistance = battleSettings.MaxDistance
                };

                if (staging != null)
                {
                    await staging.PlayIntroAsync(stagingContext, cancellationToken);
                }
                else
                {
                    ApplyFallbackBattleFieldLayout(player, enemy, battleSettings);
                }

                bgmService?.Play(BgmTrackId.Battle);

                if (staging != null)
                {
                    await staging.PlayBattleStartAsync(stagingContext, cancellationToken);
                }
                else if (sceneFade != null)
                {
                    await sceneFade.FadeInAsync(cancellationToken);
                }

                SetBattleSceneActive(true);
                battleView?.PrepareForBattleInput();

                if (battleCamera != null)
                {
                    battleCamera.SetCameraEnable(true);
                    battleCamera.SetCameraOperatable(false);
                }

                BattleFieldLayout fieldLayout = new BattleFieldLayout(
                    player.Model.transform,
                    enemy.Model.transform,
                    playerSpawn,
                    enemySpawn,
                    cameraProfile != null ? cameraProfile.HorizontalAngle : 0f,
                    battleSettings.MinCloseSeparation);

                if (staging == null)
                {
                    fieldLayout.ApplyInitialBattlePositions(battleSettings.MaxDistance, battleSettings.MaxDistance);
                }

                ProceduralMotionCharacter playerMotion = player.Model.GetComponent<ProceduralMotionCharacter>();
                ProceduralMotionCharacter enemyMotion = enemy.Model.GetComponent<ProceduralMotionCharacter>();
                playerMotion?.SetRootTranslationEnabled(false);
                enemyMotion?.SetRootTranslationEnabled(false);
                fieldLayout.ConfigurePositionConstraint(playerMotion);

                movementInput = new BattleKeyboardMovementInput();

                system = new BattleSystem(

                    player.Unit,

                    enemy.Unit,

                    battleSettings,

                    movementInput,

                    new StandardBattleEnemyAi(),

                    null);



                fieldPresenter = new BattleFieldPresenter(fieldLayout, system, player.Model);



                if (battleCamera != null)

                {

                    cameraPresenter = new BattleFieldCameraPresenter(

                        battleCamera,

                        system,

                        player.Model.transform,

                        enemy.Model.transform,

                        playerSpawn,

                        enemySpawn,

                        cameraProfile);

                }



                presenter = new BattlePresenter(battleView);

                presenter.Bind(system);



                BattleHitEffectView hitEffect = GetOrAddComponent<BattleHitEffectView>();

                BattleDamagePopupView damagePopup = GetOrAddComponent<BattleDamagePopupView>();

                combatFeedbackPresenter = new BattleCombatFeedbackPresenter(

                    hitEffect,

                    damagePopup,

                    system,

                    player.Model.transform,

                    enemy.Model.transform,

                    ResolveWorldCamera(),

                    seService);



                if (overlayView != null)

                {

                    overlayView.enabled = true;

                    finishPresenter = new BattleFinishPresenter(overlayView, system, cancellationToken);

                }



                BattleUnit winner = null;

                BattleUiInputScope inputScope = BattleUiInputScope.Suppress();

                try

                {

                    using (system.OnBattleEnd.Subscribe(w => winner = w))

                    {

                        await system.RunAsync(cancellationToken);

                    }

                }

                finally

                {

                    inputScope.Dispose();

                }



                battleView.PrepareForResultDisplay();

                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);



                bool playerWon = winner != null && winner == player.Unit;

                return new TrainingBattleResult(true, playerWon, enemyName);

            }

            finally

            {

                presenter?.Dispose();

                combatFeedbackPresenter?.Dispose();

                finishPresenter?.Dispose();

                fieldPresenter?.Dispose();

                cameraPresenter?.Dispose();

                system?.Dispose();

                movementInput?.Dispose();

                BattleHitStopClock.Clear();

                GameplayTime.Reset();



                DestroyEnemyModel();

                if (cancellationToken.IsCancellationRequested)
                {
                    HideForLeave();
                }
                else
                {
                    await RestoreTrainingViewAsync(playerModel, cancellationToken);
                    SetBattleSceneActive(false);
                }
            }

        }



        private void ApplyFallbackBattleFieldLayout(
            BattleParticipant player,
            BattleParticipant enemy,
            BattleSettings battleSettings)
        {
            if (battleCamera != null)
            {
                battleCamera.SetCameraEnable(true);
                battleCamera.SetCameraOperatable(false);
            }

            var fieldLayout = new BattleFieldLayout(
                player.Model.transform,
                enemy.Model.transform,
                playerSpawn,
                enemySpawn,
                cameraProfile != null ? cameraProfile.HorizontalAngle : 0f,
                battleSettings.MinCloseSeparation);
            fieldLayout.ApplyInitialBattlePositions(battleSettings.MaxDistance, battleSettings.MaxDistance);
        }

        private void SetBattleSceneActive(bool active)

        {

            if (battleCanvasRoot != null)

            {

                battleCanvasRoot.SetActive(active);

            }

            else if (battleUiCanvas != null)

            {

                battleUiCanvas.gameObject.SetActive(active);

            }



            if (battleUiCanvas != null)

            {

                battleUiCanvas.enabled = active;

            }

        }



        private async UniTask RestoreTrainingViewAsync(GameObject playerModel, CancellationToken cancellationToken)
        {
            overlayView?.HideImmediate();
            overlayView?.HideVsUi();

            BattleMatchupBackgroundView matchupBackground =
                FindFirstObjectByType<BattleMatchupBackgroundView>(FindObjectsInactive.Include);
            matchupBackground?.ShowClassroom();

            if (playerModel != null)
            {
                playerModel.SetActive(true);
                ProceduralMotionCharacter motion = playerModel.GetComponent<ProceduralMotionCharacter>();
                motion?.ResetForTrainingDisplay();
            }

            if (trainingDisplay != null)
            {
                await trainingDisplay.RestoreAfterBattleAsync(cancellationToken);
            }

            if (battleCamera != null)
            {
                battleCamera.SetCameraEnable(true);
                battleCamera.SetCameraOperatable(false);
            }

            if (sceneFade != null)
            {
                await sceneFade.FadeInAsync(cancellationToken);
            }

            if (bgmService != null)
            {
                await bgmService.PlayAsync(BgmTrackId.Training, forceSwitch: true, cancellationToken);
            }

            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);
        }



        private void DestroyEnemyModel()

        {

            if (lastEnemyModel != null)

            {

                Destroy(lastEnemyModel);

                lastEnemyModel = null;

            }

        }



        private UnityEngine.Camera ResolveWorldCamera()

        {

            if (battleCamera is Component component)

            {

                UnityEngine.Camera camera = component.GetComponentInChildren<UnityEngine.Camera>(true);

                if (camera != null)

                {

                    return camera;

                }

            }



            return UnityEngine.Camera.main;

        }



        private T GetOrAddComponent<T>() where T : Component

        {

            T component = GetComponent<T>();

            if (component == null)

            {

                component = gameObject.AddComponent<T>();

            }



            return component;

        }

    }

}


