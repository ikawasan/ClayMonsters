using Audio;
using Audio.Interface;
using Battle.Input;

using Battle.Interface;
using Battle.Presenter;
using Battle.View;

using Camera.Interface;

using ClayEditor.Rigging;

using Cysharp.Threading.Tasks;

using Extensions;

using R3;

using System;

using System.Threading;

using UI.Battle.Interface;

using UnityEngine;



namespace Battle

{

    /// <summary>

    /// 戦闘フロー(選択→演出→開始→本戦→勝利/リザルト)を進行させるドメインのコーディネータ。

    /// </summary>

    public sealed class BattleFlow

    {

        /// <summary>

        /// シーン固有の設定(配置先・UIルート・敵スロット)。

        /// </summary>

        public sealed class Context

        {

            public Transform PlayerSpawn;

            public Transform EnemySpawn;

            public Canvas BattleUiCanvas;

            public int EnemySlotIndex;

            public IClayEditCameraView BattleCamera;

            public BattleFieldCameraProfile CameraProfile;

            /// <summary>
            /// 演出フェードとキャンバス切替
            /// </summary>
            public IBattleCanvasTransition PresentationTransition;

            public Interface.IBattleHitEffect HitEffect;

            public Interface.IBattleDamagePopup DamagePopup;

            public Interface.IBattleFinishPresentation FinishPresentation;

            public IBattleEnemyAi EnemyAi;

            public Func<CancellationToken, UniTask<BattleParticipant>> EnemyLoader;

            public Action<BattleKeyboardMovementInput> OnBattleInputCreated;

            public Action OnBattleInputDisposed;

            public IBattleVictoryReturnView VictoryReturnView;

            public IBattleDualVictoryReturnView VictoryDualReturnView;

            public GameObject PreloadedPlayerModel;

            public Func<GameObject, BattleParticipant> PreloadedPlayerBuilder;

            public Action<BattleUnit> OnBattleEnd;

            public Func<CancellationToken, UniTask> WaitForMatchupStartAsync;

            public IBattlePvpCombatSync CombatSync;

            /// <summary>
            /// 戦闘のレベルデザイン設定
            /// </summary>
            public BattleLevelDesignSettings LevelDesignSettings;

            /// <summary>
            /// VS開始ボタンを待たずに紹介演出だけ再生する
            /// </summary>
            public bool AutoStartMatchup;
        }



        private readonly IMonsterSelectionSession selectionSession;

        private readonly IBattleView battleView;

        private readonly IBattleStaging staging;

        private readonly BattleParticipantLoader loader;

        private readonly Context context;

        private readonly IBgmService bgmService;

        private readonly ISeService seService;



        public BattleFlow(

            IMonsterSelectionSession selectionSession,

            IBattleView battleView,

            IBattleStaging staging,

            BattleParticipantLoader loader,

            Context context,

            IBgmService bgmService,

            ISeService seService = null)

        {

            this.selectionSession = selectionSession;

            this.battleView = battleView;

            this.staging = staging;

            this.loader = loader;

            this.context = context;

            this.bgmService = bgmService;

            this.seService = seService;

        }



        /// <summary>

        /// 戦闘フローを最後まで進行させる。

        /// </summary>

        public async UniTask<BattleVictoryReturnChoice> RunAsync(CancellationToken cancellationToken)

        {

            BattleVictoryReturnChoice returnChoice = BattleVictoryReturnChoice.Title;

            BattlePresenter presenter = null;
            BattleFieldPresenter fieldPresenter = null;
            BattleFieldCameraPresenter cameraPresenter = null;
            BattleCombatFeedbackPresenter combatFeedbackPresenter = null;
            BattleFinishPresenter finishPresenter = null;

            BattleSystem system = null;
            BattleKeyboardMovementInput movementInput = null;



            try

            {

                // 1. プレイヤーのモンスター選択

                GameObject playerModel;
                BattleParticipant player;
                BattleParticipant enemy;

                if (context.PreloadedPlayerModel != null)
                {
                    bgmService?.Play(BgmTrackId.Battle);
                    SetCanvasEnabled(context.BattleUiCanvas, false);
                    selectionSession?.Hide();
                    playerModel = context.PreloadedPlayerModel;

                    if (context.PreloadedPlayerBuilder != null)
                    {
                        player = context.PreloadedPlayerBuilder(playerModel);
                    }
                    else if (selectionSession != null)
                    {
                        player = loader.BuildFromLoadedModel(
                            playerModel,
                            selectionSession.SavePool,
                            selectionSession.SelectedSlotIndex,
                            context.PlayerSpawn);
                    }
                    else
                    {
                        Debug.LogError("[BattleFlow] モンスター選択UIが未設定です");
                        return BattleVictoryReturnChoice.Title;
                    }

                    enemy = context.EnemyLoader != null
                        ? await context.EnemyLoader(cancellationToken)
                        : await loader.LoadEnemyAsync(context.EnemySlotIndex, context.EnemySpawn, cancellationToken);
                }
                else
                {
                    playerModel = null;
                    player = default;
                    enemy = default;

                    while (true)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        bgmService?.Play(BgmTrackId.BattleSelection);
                        SetCanvasEnabled(context.BattleUiCanvas, false);

                        if (selectionSession == null)
                        {
                            Debug.LogError("[BattleFlow] モンスター選択セッションが未設定です");
                            return BattleVictoryReturnChoice.Title;
                        }

                        playerModel = await selectionSession.WaitForModelAsync(cancellationToken);
                        if (playerModel == null)
                        {
                            Debug.LogError("[BattleFlow] モデル選択が完了しませんでした");
                            return BattleVictoryReturnChoice.Title;
                        }

                        player = loader.BuildFromLoadedModel(
                            playerModel,
                            selectionSession.SavePool,
                            selectionSession.SelectedSlotIndex,
                            context.PlayerSpawn);

                        enemy = context.EnemyLoader != null
                            ? await context.EnemyLoader(cancellationToken)
                            : await loader.LoadEnemyAsync(context.EnemySlotIndex, context.EnemySpawn, cancellationToken);

                        if (player.IsValid && enemy.IsValid)
                        {
                            bgmService?.Play(BgmTrackId.Battle);
                            break;
                        }

                        Debug.LogError(
                            "[BattleFlow] 参加者の構築に失敗しました"
                                + $" player={player.IsValid}"
                                + $" enemy={enemy.IsValid}"
                                + $" enemySlot={context.EnemySlotIndex}");

                        if (player.IsValid && !enemy.IsValid)
                        {
                            DestroyParticipantModels(null, enemy.Model);
                            ReleasePresentationInput();
                            return BattleVictoryReturnChoice.Title;
                        }

                        if (playerModel != null)
                        {
                            UnityEngine.Object.Destroy(playerModel);
                            playerModel = null;
                        }

                        if (enemy.Model != null)
                        {
                            UnityEngine.Object.Destroy(enemy.Model);
                            enemy = default;
                        }

                        await selectionSession.RestoreAfterParticipantFailureAsync(cancellationToken);
                    }
                }



                if (!player.IsValid || !enemy.IsValid)

                {

                    Debug.LogError("[BattleFlow] 参加者の構築に失敗しました");

                    if (context.PresentationTransition != null)
                    {
                        await context.PresentationTransition.FadeInAsync(cancellationToken);
                    }

                    return BattleVictoryReturnChoice.Title;

                }



                BattleSettings battleSettings = BattleLevelDesignSettings.Resolve(context.LevelDesignSettings);

                var stagingContext = new BattleStagingContext
                {
                    Player = player.Unit,
                    Enemy = enemy.Unit,
                    PlayerSpawn = context.PlayerSpawn,
                    EnemySpawn = context.EnemySpawn,
                    PlayerModel = player.Model.transform,
                    EnemyModel = enemy.Model.transform,
                    ScreenFade = context.PresentationTransition,
                    VictoryReturnView = context.VictoryReturnView,
                    VictoryDualReturnView = context.VictoryDualReturnView,
                    InitialBattleDistance = battleSettings.MaxDistance,
                    MaxBattleDistance = battleSettings.MaxDistance,
                    WaitForMatchupStartAsync = context.WaitForMatchupStartAsync,
                    AutoStartMatchup = context.AutoStartMatchup
                };

                // 3. 対戦紹介(VS表示と開始待ち)
                if (staging == null)
                {
                    Debug.LogError("[BattleFlow] BattleStagingが未設定です");
                    if (selectionSession != null)
                    {
                        await selectionSession.RestoreAfterParticipantFailureAsync(cancellationToken);
                    }

                    ReleasePresentationInput();
                    return BattleVictoryReturnChoice.Title;
                }

                try
                {
                    await staging.PlayIntroAsync(stagingContext, cancellationToken);

                    bgmService?.Play(BgmTrackId.Battle);

                    await staging.PlayBattleStartAsync(stagingContext, cancellationToken);
                }
                catch (System.OperationCanceledException)
                {
                    throw;
                }
                catch (System.Exception exception)
                {
                    Debug.LogError($"[BattleFlow] 対戦紹介演出に失敗しました\n{exception}");
                    DestroyParticipantModels(player.Model, enemy.Model);
                    if (selectionSession != null)
                    {
                        await selectionSession.RestoreAfterParticipantFailureAsync(cancellationToken);
                    }

                    ReleasePresentationInput();
                    return BattleVictoryReturnChoice.Title;
                }



                // 5. 本戦

                movementInput = new BattleKeyboardMovementInput();
                context.OnBattleInputCreated?.Invoke(movementInput);
                system = new BattleSystem(
                    player.Unit,
                    enemy.Unit,
                    battleSettings,
                    movementInput,
                    context.EnemyAi ?? new StandardBattleEnemyAi(),
                    context.CombatSync);

                BattleFieldLayout fieldLayout = new BattleFieldLayout(
                    player.Model.transform,
                    enemy.Model.transform,
                    context.PlayerSpawn,
                    context.EnemySpawn,
                    context.CameraProfile != null
                        ? context.CameraProfile.HorizontalAngle
                        : 0f,
                    battleSettings.MinCloseSeparation);
                var playerMotion = player.Model.GetComponent<ProceduralMotionCharacter>();
                var enemyMotion = enemy.Model.GetComponent<ProceduralMotionCharacter>();
                playerMotion?.SetRootTranslationEnabled(false);
                enemyMotion?.SetRootTranslationEnabled(false);
                fieldLayout.ConfigurePositionConstraint(playerMotion);
                fieldPresenter = new BattleFieldPresenter(fieldLayout, system, player.Model);

                if (context.BattleCamera != null)
                {
                    cameraPresenter = new BattleFieldCameraPresenter(
                        context.BattleCamera,
                        system,
                        player.Model.transform,
                        enemy.Model.transform,
                        context.PlayerSpawn,
                        context.EnemySpawn,
                        context.CameraProfile);
                }

                presenter = new BattlePresenter(battleView);

                presenter.Bind(system);

                if (context.HitEffect == null)
                {
                    context.HitEffect = EnsureHitEffectView();
                }

                if (context.DamagePopup == null)
                {
                    context.DamagePopup = EnsureDamagePopupView();
                }

                if (context.HitEffect != null || context.DamagePopup != null || seService != null)
                {
                    combatFeedbackPresenter = new BattleCombatFeedbackPresenter(
                        context.HitEffect,
                        context.DamagePopup,
                        system,
                        player.Model.transform,
                        enemy.Model.transform,
                        ResolveWorldCamera(context.BattleCamera),
                        seService);
                }

                if (context.FinishPresentation != null)
                {
                    finishPresenter = new BattleFinishPresenter(
                        context.FinishPresentation,
                        system,
                        cancellationToken);
                }

                context.CombatSync?.BeginListening();
                GameplayTime.Reset();
                BattleHitStopClock.Clear();

                BattleUnit winner = null;
                BattleUiInputScope inputScope = BattleUiInputScope.Suppress();

                try
                {
                    if (context.PresentationTransition != null)
                    {
                        await context.PresentationTransition.SetCanvasEnabledAsync(
                            context.BattleUiCanvas,
                            true,
                            cancellationToken,
                            fadeOutBeforeChange: false,
                            fadeInAfterChange: false);
                    }
                    else
                    {
                        SetCanvasEnabled(context.BattleUiCanvas, true);
                    }

                    battleView.PrepareForBattleInput();

                    using (system.OnBattleEnd.Subscribe(w => winner = w))
                    {
                        await system.RunAsync(cancellationToken);
                    }

                    presenter.Dispose();
                    presenter = null;
                    combatFeedbackPresenter?.Dispose();
                    combatFeedbackPresenter = null;
                    finishPresenter?.Dispose();
                    finishPresenter = null;
                    fieldPresenter.Dispose();
                    fieldPresenter = null;
                    cameraPresenter?.Dispose();
                    cameraPresenter = null;

                    battleView.PrepareForResultDisplay();
                    await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);

                    GameplayTime.Reset();
                    BattleHitStopClock.Clear();

                    await staging.PlayVictoryAsync(stagingContext, winner, cancellationToken);

                    returnChoice = stagingContext.VictoryReturnChoice;
                    context.OnBattleEnd?.Invoke(winner);
                }
                finally
                {
                    inputScope.Dispose();
                }

            }

            finally

            {

                presenter?.Dispose();
                combatFeedbackPresenter?.Dispose();
                finishPresenter?.Dispose();
                fieldPresenter?.Dispose();
                cameraPresenter?.Dispose();

                system?.Dispose();
                context.CombatSync?.EndListening();
                context.OnBattleInputDisposed?.Invoke();
                movementInput?.Dispose();
                BattleHitStopClock.Clear();
                GameplayTime.Reset();

            }

            return returnChoice;

        }



        private void ReleasePresentationInput()
        {
            context.PresentationTransition?.ReleasePresentationInput();
        }

        private static void DestroyParticipantModels(GameObject playerModel, GameObject enemyModel)
        {
            if (playerModel != null)
            {
                UnityEngine.Object.Destroy(playerModel);
            }

            if (enemyModel != null)
            {
                UnityEngine.Object.Destroy(enemyModel);
            }
        }



        private static void SetCanvasEnabled(Canvas canvas, bool isEnabled)

        {

            CanvasVisibilityUtility.SetCanvasEnabled(canvas, isEnabled);

        }

        private static UnityEngine.Camera ResolveWorldCamera(IClayEditCameraView battleCamera)
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

        private static BattleHitEffectView EnsureHitEffectView()
        {
            BattleHitEffectView existing = UnityEngine.Object.FindFirstObjectByType<BattleHitEffectView>(
                UnityEngine.FindObjectsInactive.Include);
            if (existing != null)
            {
                return existing;
            }

            var host = new GameObject(nameof(BattleHitEffectView));
            return host.AddComponent<BattleHitEffectView>();
        }

        private static BattleDamagePopupView EnsureDamagePopupView()
        {
            BattleDamagePopupView existing = UnityEngine.Object.FindFirstObjectByType<BattleDamagePopupView>(
                UnityEngine.FindObjectsInactive.Include);
            if (existing != null)
            {
                return existing;
            }

            var host = new GameObject(nameof(BattleDamagePopupView));
            return host.AddComponent<BattleDamagePopupView>();
        }

    }

}


