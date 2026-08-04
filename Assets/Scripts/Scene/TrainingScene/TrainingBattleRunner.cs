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

using Localization;

using R3;

using SaveData;

using SaveData.Interface;

using Scene.BattleNpcScene;

using Scene.BattleNpcScene.View;

using Scene.Core.Interface;

using Scene.TrainingScene.Domain;

using Scene.TrainingScene.Interface;

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

        [SerializeField] private BattleTipsView tipsView;



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

        private ITrainingHudView hudView;

        private ITrainingBackgroundView trainingBackgroundView;

        private ITrainingLocationCameraView locationCameraView;

        private BattleClassroomLighting classroomLighting;

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
            overlayView?.HideForLeave();
            SetBattleSceneActive(false);
            DestroyEnemyModel();
            HideBattleFieldForTraining();
        }

        [Inject]

        public void Construct(

            IClayModelImporter importer,

            IClayModelSaveService saveService,

            ISceneFade sceneFade,

            IBattleCanvasTransition canvasTransition,

            IBgmService bgmService,

            ISeService seService,

            TrainingDisplay trainingDisplay,

            ITrainingHudView hudView,

            ITrainingBackgroundView trainingBackgroundView,

            ITrainingLocationCameraView locationCameraView,

            BattleClassroomLighting classroomLighting)

        {

            this.importer = importer;

            this.saveService = saveService;

            this.sceneFade = sceneFade;

            this.canvasTransition = canvasTransition;

            this.bgmService = bgmService;

            this.seService = seService;

            this.trainingDisplay = trainingDisplay;

            this.hudView = hudView;

            this.trainingBackgroundView = trainingBackgroundView;

            this.locationCameraView = locationCameraView;

            this.classroomLighting = classroomLighting;

        }



        /// <summary>

        /// 放課後戦闘を実行する

        /// </summary>

        /// <param name="session">育成セッション</param>

        /// <param name="playerModel">プレイヤーモデル</param>

        /// <param name="modelName">プレイヤー名</param>

        /// <param name="enemySlotIndex">敵スロット</param>

        /// <param name="enemyStrengthTier">敵強さ段階</param>

        /// <param name="cancellationToken">キャンセルトークン</param>

        public async UniTask<TrainingBattleResult> RunAfterSchoolBattleAsync(

            TrainingSession session,

            GameObject playerModel,

            string modelName,

            int enemySlotIndex,

            EnemyStrengthTier enemyStrengthTier,

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



            string fallbackName = saveService.GetSlot(ModelSavePool.Enemy, enemySlotIndex)?.modelName
                ?? LocalizedText.GetOrFallback(GameTextKeys.TrainingLogEnemyDefault, "敵");
            string enemyName = EnemyDisplayName.Resolve(enemySlotIndex, fallbackName);



            BattleHitStopClock.Clear();

            GameplayTime.Reset();

            BattleSystem system = null;

            BattleKeyboardMovementInput movementInput = null;

            BattlePresenter presenter = null;

            BattleFieldPresenter fieldPresenter = null;

            BattleFieldCameraPresenter cameraPresenter = null;

            BattleCombatFeedbackPresenter combatFeedbackPresenter = null;

            BattleChargeEffectPresenter chargeEffectPresenter = null;

            BattleMagicAttackEffectPresenter magicAttackEffectPresenter = null;

            BattleKnockbackEffectPresenter knockbackEffectPresenter = null;

            BattleFinishPresenter finishPresenter = null;

            BattlePartBreakPresenter partBreakPresenter = null;



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



                BattleParticipant enemy = await loader.LoadEnemyAsync(enemySlotIndex, enemySpawn, enemyStrengthTier, cancellationToken);

                if (!enemy.IsValid)

                {

                    Debug.LogError($"[TrainingBattleRunner] 敵スロット{enemySlotIndex}の読み込みに失敗しました");

                    return new TrainingBattleResult(false, false, enemyName);

                }



                lastEnemyModel = enemy.Model;

                PrepareBattleEnvironment();

                if (player.Model != null)
                {
                    player.Model.SetActive(false);
                }

                if (enemy.Model != null)
                {
                    enemy.Model.SetActive(false);
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

                EnsureBattleFieldVisible();
                SetBattleSceneActive(true);
                battleView?.PrepareForBattleInput();

                if (battleCamera != null)
                {
                    battleCamera.SetCameraEnable(true);
                    battleCamera.SetCameraOperatable(false);
                }

                // カメラ切替後にもFieldを再確認する
                EnsureBattleFieldVisible();

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
                    new StandardBattleEnemyAi(
                        BattleEnemyAiProfile.FromStrengthTier(enemyStrengthTier)),
                    null);



                fieldPresenter = new BattleFieldPresenter(fieldLayout, system, player.Model);



                if (battleCamera != null)

                {

                    BattleModelVisualExtents.TryCapture(

                        player.Model.transform,

                        out BattleModelVisualExtents playerExtents);

                    BattleModelVisualExtents.TryCapture(

                        enemy.Model.transform,

                        out BattleModelVisualExtents enemyExtents);

                    cameraPresenter = new BattleFieldCameraPresenter(

                        battleCamera,

                        system,

                        player.Model.transform,

                        enemy.Model.transform,

                        playerSpawn,

                        enemySpawn,

                        cameraProfile,

                        playerExtents,

                        enemyExtents);

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



                BattleChargeEffectView chargeEffect = GetOrAddComponent<BattleChargeEffectView>();

                chargeEffectPresenter = new BattleChargeEffectPresenter(

                    chargeEffect,

                    system,

                    player.Model.transform,

                    enemy.Model.transform,

                    seService);



                BattleMagicAttackEffectView magicEffectView = GetOrAddComponent<BattleMagicAttackEffectView>();

                float battleGroundY = ResolveBattleGroundY(playerSpawn, enemySpawn);
                magicAttackEffectPresenter = new BattleMagicAttackEffectPresenter(

                    magicEffectView,

                    system,

                    player.Model.transform,

                    enemy.Model.transform,

                    battleGroundY,

                    seService);



                BattleKnockbackEffectView knockbackEffectView = GetOrAddComponent<BattleKnockbackEffectView>();

                knockbackEffectPresenter = new BattleKnockbackEffectPresenter(

                    knockbackEffectView,

                    system,

                    player.Model.transform,

                    enemy.Model.transform,

                    seService);



                if (overlayView != null)

                {

                    overlayView.enabled = true;

                    finishPresenter = new BattleFinishPresenter(overlayView, system, cancellationToken);

                    partBreakPresenter = new BattlePartBreakPresenter(overlayView, system, cancellationToken);

                }



                BattleUnit winner = null;

                BattleUiInputScope inputScope = BattleUiInputScope.Suppress();

                System.IDisposable tipsSession = null;

                if (tipsView == null && battleView != null)

                {

                    Transform hudRoot = battleView.transform.parent;

                    if (hudRoot != null)

                    {

                        tipsView = hudRoot.GetComponentInChildren<BattleTipsView>(true);

                    }

                }

                if (tipsView != null)

                {

                    tipsSession = tipsView.BeginCombatSession();

                }

                try

                {

                    using (system.OnBattleEnd.Subscribe(w => winner = w))

                    {

                        await system.RunAsync(cancellationToken);

                    }

                }

                finally

                {

                    tipsSession?.Dispose();

                    inputScope.Dispose();

                }



                battleView.PrepareForResultDisplay();

                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);



                bool playerWon = winner != null && winner == player.Unit;

                if (playerWon && staging != null)
                {
                    DisposeBattlePresenters(
                        ref presenter,
                        ref combatFeedbackPresenter,
                        ref chargeEffectPresenter,
                        ref magicAttackEffectPresenter,
                        ref knockbackEffectPresenter,
                        ref finishPresenter,
                        ref partBreakPresenter,
                        ref fieldPresenter,
                        ref cameraPresenter);

                    stagingContext.VictoryReturnView = new TrainingBattleVictoryAdapter(hudView);
                    await staging.PlayVictoryAsync(stagingContext, winner, cancellationToken);
                }

                return new TrainingBattleResult(true, playerWon, enemyName);

            }

            finally

            {

                presenter?.Dispose();

                combatFeedbackPresenter?.Dispose();

                chargeEffectPresenter?.Dispose();

                magicAttackEffectPresenter?.Dispose();

                knockbackEffectPresenter?.Dispose();

                finishPresenter?.Dispose();

                partBreakPresenter?.Dispose();

                fieldPresenter?.Dispose();

                cameraPresenter?.Dispose();

                system?.Dispose();

                movementInput?.Dispose();

                BattleHitStopClock.Clear();

                GameplayTime.Reset();



                if (cancellationToken.IsCancellationRequested)
                {
                    DestroyEnemyModel();
                    HideForLeave();
                }
                else
                {
                    await FadeOutForTrainingRestoreAsync(cancellationToken);
                    DestroyEnemyModel();
                    // 結果テキスト準備まで黒画面を維持するためここではフェード明けしない
                    await RestoreTrainingViewAsync(
                        playerModel,
                        cancellationToken,
                        skipInitialFade: true,
                        skipFadeIn: true);
                    SetBattleSceneActive(false);
                }
            }

        }

        private async UniTask FadeOutForTrainingRestoreAsync(CancellationToken cancellationToken)
        {
            if (canvasTransition != null)
            {
                await canvasTransition.FadeOutAsync(cancellationToken);
                return;
            }

            if (sceneFade != null)
            {
                await sceneFade.FadeOutAsync(cancellationToken);
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
            EnsureBattleUiReferences();

            if (battleCanvasRoot != null && !battleCanvasRoot.activeSelf)
            {
                // Canvas.enabled切替前提のため非アクティブなら有効化する
                battleCanvasRoot.SetActive(true);
            }

            if (battleUiCanvas != null)
            {
                CanvasVisibilityUtility.SetCanvasEnabled(battleUiCanvas, active);
                return;
            }

            if (battleCanvasRoot != null)
            {
                CanvasVisibilityUtility.SetUiVisible(battleCanvasRoot, active);
            }
        }

        private void PrepareBattleEnvironment()
        {
            trainingBackgroundView?.HideForLeave();
            classroomLighting?.Apply();

            if (staging != null)
            {
                staging.enabled = true;
            }

            EnsureBattleFieldVisible();
        }

        /// <summary>
        /// 戦闘用Fieldを表示し育成背景を隠した状態へ揃える
        /// </summary>
        private void EnsureBattleFieldVisible()
        {
            trainingBackgroundView?.HideForLeave();
            classroomLighting?.Apply();

            if (staging == null)
            {
                Debug.LogError("[TrainingBattleRunner] stagingが未配線です", this);
                return;
            }

            staging.EnsureClassroomVisible();
        }

        /// <summary>
        /// 育成復帰用に戦闘Fieldと対戦炎演出を隠す
        /// DefaultBackgroundは育成背景のためここでは触らない
        /// </summary>
        private void HideBattleFieldForTraining()
        {
            staging?.EndMatchupPresentation();
            staging?.HideClassroom();
        }

        /// <summary>
        /// 戦闘UI参照が未配線でもBattleViewから復元する
        /// </summary>
        private void EnsureBattleUiReferences()
        {
            if (battleView == null)
            {
                return;
            }

            if (battleCanvasRoot == null)
            {
                battleCanvasRoot = battleView.gameObject;
            }

            if (battleUiCanvas == null && battleCanvasRoot != null)
            {
                battleUiCanvas = battleCanvasRoot.GetComponent<Canvas>();
            }
        }



        private async UniTask RestoreTrainingViewAsync(
            GameObject playerModel,
            CancellationToken cancellationToken,
            bool skipInitialFade = false,
            bool skipFadeIn = false)
        {
            if (!skipInitialFade)
            {
                await FadeOutForTrainingRestoreAsync(cancellationToken);
            }

            overlayView?.HideImmediate();
            overlayView?.HideVsUi();

            HideBattleFieldForTraining();

            if (staging != null)
            {
                staging.enabled = false;
            }

            // Staging無効化後にもう一度隠しOnDisable副作用を潰す
            HideBattleFieldForTraining();
            trainingBackgroundView?.ShowRoamBackground();

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

            ApplyTrainingCameraView();
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);

            if (battleCamera != null)
            {
                battleCamera.SetCameraEnable(true);
                battleCamera.SetCameraOperatable(false);
            }

            hudView?.Show();

            if (!skipFadeIn)
            {
                if (canvasTransition != null)
                {
                    await canvasTransition.FadeInAsync(cancellationToken);
                }
                else if (sceneFade != null)
                {
                    await sceneFade.FadeInAsync(cancellationToken);
                }
            }

            if (bgmService != null)
            {
                await bgmService.PlayAsync(BgmTrackId.Training, forceSwitch: true, cancellationToken);
            }

            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);
        }

        /// <summary>
        /// 勝利演出後のカメラを育成構図へ戻す
        /// </summary>
        private void ApplyTrainingCameraView()
        {
            locationCameraView?.ApplyRoamView();
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

        private static float ResolveBattleGroundY(Transform playerSpawnPoint, Transform enemySpawnPoint)
        {
            float playerY = playerSpawnPoint != null ? playerSpawnPoint.position.y : 0f;
            float enemyY = enemySpawnPoint != null ? enemySpawnPoint.position.y : playerY;
            return (playerY + enemyY) * 0.5f;
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

        private static void DisposeBattlePresenters(
            ref BattlePresenter presenter,
            ref BattleCombatFeedbackPresenter combatFeedbackPresenter,
            ref BattleChargeEffectPresenter chargeEffectPresenter,
            ref BattleMagicAttackEffectPresenter magicAttackEffectPresenter,
            ref BattleKnockbackEffectPresenter knockbackEffectPresenter,
            ref BattleFinishPresenter finishPresenter,
            ref BattlePartBreakPresenter partBreakPresenter,
            ref BattleFieldPresenter fieldPresenter,
            ref BattleFieldCameraPresenter cameraPresenter)
        {
            presenter?.Dispose();
            presenter = null;
            combatFeedbackPresenter?.Dispose();
            combatFeedbackPresenter = null;
            chargeEffectPresenter?.Dispose();
            chargeEffectPresenter = null;
            magicAttackEffectPresenter?.Dispose();
            magicAttackEffectPresenter = null;
            knockbackEffectPresenter?.Dispose();
            knockbackEffectPresenter = null;
            finishPresenter?.Dispose();
            finishPresenter = null;
            partBreakPresenter?.Dispose();
            partBreakPresenter = null;
            fieldPresenter?.Dispose();
            fieldPresenter = null;
            cameraPresenter?.Dispose();
            cameraPresenter = null;
        }

    }

}


