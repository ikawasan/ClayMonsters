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
using SaveData;
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

            public Interface.IBattleChargeEffect ChargeEffect;

            public Interface.IBattleFinishPresentation FinishPresentation;

            public Interface.IBattlePartBreakPresentation PartBreakPresentation;

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
            /// NPC戦闘向けチュートリアルTips
            /// </summary>
            public IBattleTipsView TipsView;

            /// <summary>
            /// 戦闘のレベルデザイン設定
            /// </summary>
            public BattleLevelDesignSettings LevelDesignSettings;

            /// <summary>
            /// VS開始ボタンを待たずに紹介演出だけ再生する
            /// </summary>
            public bool AutoStartMatchup;

            /// <summary>
            /// CPU戦のVS待ちで敵強さ選択UIを出すか
            /// </summary>
            public bool EnableEnemyStrengthSelect;

            /// <summary>
            /// 初期の敵強さ段階
            /// </summary>
            public EnemyStrengthTier EnemyStrengthTier = EnemyStrengthTier.Normal;

            /// <summary>
            /// 参加者モデルが確定したときに通知する
            /// 退場時破棄のため呼び出し側が参照を保持する
            /// </summary>
            public System.Action<GameObject, GameObject> RegisterSpawnedParticipants;
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
            BattleChargeEffectPresenter chargeEffectPresenter = null;
            BattleMagicAttackEffectPresenter magicAttackEffectPresenter = null;
            BattleFinishPresenter finishPresenter = null;
            BattlePartBreakPresenter partBreakPresenter = null;

            BattleSystem system = null;
            BattleKeyboardMovementInput movementInput = null;
            GameObject spawnedPlayerModel = null;
            GameObject spawnedEnemyModel = null;

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
                        : await loader.LoadEnemyAsync(
                            context.EnemySlotIndex,
                            context.EnemySpawn,
                            context.EnemyStrengthTier,
                            cancellationToken);
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
                        // 敵ロード待ち中の中断でも破棄できるよう先に登録する
                        spawnedPlayerModel = player.Model;
                        context.RegisterSpawnedParticipants?.Invoke(spawnedPlayerModel, null);

                        enemy = context.EnemyLoader != null
                            ? await context.EnemyLoader(cancellationToken)
                            : await loader.LoadEnemyAsync(
                                context.EnemySlotIndex,
                                context.EnemySpawn,
                                context.EnemyStrengthTier,
                                cancellationToken);

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

                spawnedPlayerModel = player.Model;
                spawnedEnemyModel = enemy.Model;
                context.RegisterSpawnedParticipants?.Invoke(spawnedPlayerModel, spawnedEnemyModel);
                // 見せ合い前に追跡外の重複モデルを破棄する
                DestroyUntrackedBattleModels(
                    spawnedPlayerModel,
                    spawnedEnemyModel,
                    context.PlayerSpawn,
                    context.EnemySpawn);

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
                    AutoStartMatchup = context.AutoStartMatchup,
                    EnableEnemyStrengthSelect = context.EnableEnemyStrengthSelect,
                    EnemyStrengthTier = context.EnemyStrengthTier
                };

                if (context.EnableEnemyStrengthSelect)
                {
                    stagingContext.RebuildEnemyWithStrengthTier = tier =>
                    {
                        BattleParticipant rebuilt = loader.RebuildEnemyWithStrengthTier(
                            enemy.Model,
                            context.EnemySlotIndex,
                            tier);
                        if (!rebuilt.IsValid)
                        {
                            Debug.LogError($"[BattleFlow] 敵強さ段階{tier}の再構築に失敗しました");
                            return stagingContext.Enemy;
                        }

                        enemy = rebuilt;
                        stagingContext.Enemy = rebuilt.Unit;
                        stagingContext.EnemyStrengthTier = tier;
                        return rebuilt.Unit;
                    };
                }

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
                    stagingContext.Enemy != null ? stagingContext.Enemy : enemy.Unit,
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
                    // 本番配置後に1回だけ採寸し攻撃カメラで再利用する
                    BattleModelVisualExtents.TryCapture(
                        player.Model.transform,
                        out BattleModelVisualExtents playerExtents);
                    BattleModelVisualExtents.TryCapture(
                        enemy.Model.transform,
                        out BattleModelVisualExtents enemyExtents);
                    cameraPresenter = new BattleFieldCameraPresenter(
                        context.BattleCamera,
                        system,
                        player.Model.transform,
                        enemy.Model.transform,
                        context.PlayerSpawn,
                        context.EnemySpawn,
                        context.CameraProfile,
                        playerExtents,
                        enemyExtents);
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

                if (context.ChargeEffect == null)
                {
                    context.ChargeEffect = EnsureChargeEffectView();
                }

                if (context.ChargeEffect != null)
                {
                    chargeEffectPresenter = new BattleChargeEffectPresenter(
                        context.ChargeEffect,
                        system,
                        player.Model.transform,
                        enemy.Model.transform,
                        seService);
                }

                BattleMagicAttackEffectView magicEffectView = EnsureMagicAttackEffectView();
                if (magicEffectView != null)
                {
                    float battleGroundY = ResolveBattleGroundY(context.PlayerSpawn, context.EnemySpawn);
                    magicAttackEffectPresenter = new BattleMagicAttackEffectPresenter(
                        magicEffectView,
                        system,
                        player.Model.transform,
                        enemy.Model.transform,
                        battleGroundY,
                        seService);
                }

                if (context.FinishPresentation != null)
                {
                    finishPresenter = new BattleFinishPresenter(
                        context.FinishPresentation,
                        system,
                        cancellationToken);
                }

                if (context.PartBreakPresentation != null)
                {
                    partBreakPresenter = new BattlePartBreakPresenter(
                        context.PartBreakPresentation,
                        system,
                        cancellationToken);
                }

                context.CombatSync?.BeginListening();
                GameplayTime.Reset();
                BattleHitStopClock.Clear();

                BattleUnit winner = null;
                BattleUiInputScope inputScope = BattleUiInputScope.Suppress();
                System.IDisposable tipsSession = null;
                if (context.CombatSync == null && context.TipsView != null)
                {
                    tipsSession = context.TipsView.BeginCombatSession();
                }

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

                    tipsSession?.Dispose();
                    tipsSession = null;

                    presenter.Dispose();
                    presenter = null;
                    combatFeedbackPresenter?.Dispose();
                    combatFeedbackPresenter = null;
                    chargeEffectPresenter?.Dispose();
                    chargeEffectPresenter = null;
                    magicAttackEffectPresenter?.Dispose();
                    magicAttackEffectPresenter = null;
                    finishPresenter?.Dispose();
                    finishPresenter = null;
                    partBreakPresenter?.Dispose();
                    partBreakPresenter = null;
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
                    tipsSession?.Dispose();
                    inputScope.Dispose();
                }

            }

            finally

            {

                presenter?.Dispose();
                combatFeedbackPresenter?.Dispose();
                chargeEffectPresenter?.Dispose();
                magicAttackEffectPresenter?.Dispose();
                finishPresenter?.Dispose();
                partBreakPresenter?.Dispose();
                fieldPresenter?.Dispose();
                cameraPresenter?.Dispose();

                system?.Dispose();
                context.CombatSync?.EndListening();
                context.OnBattleInputDisposed?.Invoke();
                movementInput?.Dispose();
                BattleHitStopClock.Clear();
                GameplayTime.Reset();

                // BattleSpawnPlacementは親を外すためスポーン子破棄では消えない
                // プレロードのプレイヤーは呼び出し側所有のため敵のみ破棄する
                if (context.PreloadedPlayerModel != null)
                {
                    DestroyParticipantModels(null, spawnedEnemyModel);
                }
                else
                {
                    DestroyParticipantModels(spawnedPlayerModel, spawnedEnemyModel);
                }

                context.RegisterSpawnedParticipants?.Invoke(null, null);
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
                playerModel.SetActive(false);
                UnityEngine.Object.Destroy(playerModel);
            }

            if (enemyModel != null)
            {
                enemyModel.SetActive(false);
                UnityEngine.Object.Destroy(enemyModel);
            }
        }

        /// <summary>
        /// スポーン地点と一時インポート親に残った追跡外モデルを破棄する
        /// </summary>
        private static void DestroyUntrackedBattleModels(
            GameObject keepPlayer,
            GameObject keepEnemy,
            Transform playerSpawn,
            Transform enemySpawn)
        {
            DestroyChildrenExcept(playerSpawn, keepPlayer, keepEnemy);
            DestroyChildrenExcept(enemySpawn, keepPlayer, keepEnemy);

            GameObject importRootObject = GameObject.Find("TrainingModelImportRoot");
            if (importRootObject != null)
            {
                DestroyChildrenExcept(importRootObject.transform, keepPlayer, keepEnemy);
            }

            DestroyOrphanImportedRoots(keepPlayer, keepEnemy);
        }

        private static void DestroyChildrenExcept(Transform parent, GameObject keepA, GameObject keepB)
        {
            if (parent == null)
            {
                return;
            }

            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                GameObject childObject = child.gameObject;
                if (childObject == keepA || childObject == keepB)
                {
                    continue;
                }

                Debug.LogWarning(
                    "[BattleFlow] 追跡外モデルを破棄します"
                    + $" name={childObject.name}"
                    + $" parent={parent.name}");
                childObject.SetActive(false);
                UnityEngine.Object.Destroy(childObject);
            }
        }

        private static void DestroyOrphanImportedRoots(GameObject keepPlayer, GameObject keepEnemy)
        {
            // 親なしで残ったglbインポートルートを破棄する
            ProceduralMotionCharacter[] motions = UnityEngine.Object.FindObjectsByType<ProceduralMotionCharacter>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < motions.Length; i++)
            {
                ProceduralMotionCharacter motion = motions[i];
                if (motion == null)
                {
                    continue;
                }

                GameObject root = motion.gameObject;
                if (root == keepPlayer || root == keepEnemy)
                {
                    continue;
                }

                // Configurator上のテンプレートはシーン配置の正なので残す
                if (root.GetComponent<LoadedModelConfigurator>() != null)
                {
                    continue;
                }

                if (root.transform.parent != null)
                {
                    continue;
                }

                Debug.LogWarning($"[BattleFlow] 孤児インポートモデルを破棄します name={root.name}");
                root.SetActive(false);
                UnityEngine.Object.Destroy(root);
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

        private static BattleChargeEffectView EnsureChargeEffectView()
        {
            BattleChargeEffectView existing = UnityEngine.Object.FindFirstObjectByType<BattleChargeEffectView>(
                UnityEngine.FindObjectsInactive.Include);
            if (existing != null)
            {
                return existing;
            }

            var host = new GameObject(nameof(BattleChargeEffectView));
            return host.AddComponent<BattleChargeEffectView>();
        }

        private static BattleMagicAttackEffectView EnsureMagicAttackEffectView()
        {
            BattleMagicAttackEffectView existing = UnityEngine.Object.FindFirstObjectByType<BattleMagicAttackEffectView>(
                UnityEngine.FindObjectsInactive.Include);
            if (existing != null)
            {
                return existing;
            }

            var host = new GameObject(nameof(BattleMagicAttackEffectView));
            return host.AddComponent<BattleMagicAttackEffectView>();
        }

        private static float ResolveBattleGroundY(Transform playerSpawn, Transform enemySpawn)
        {
            float playerY = playerSpawn != null ? playerSpawn.position.y : 0f;
            float enemyY = enemySpawn != null ? enemySpawn.position.y : playerY;
            return (playerY + enemyY) * 0.5f;
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


