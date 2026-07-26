using System.Threading;
using Battle;
using Battle.Interface;
using ClayEditor.Rigging;
using Cysharp.Threading.Tasks;
using Camera.View;
using Extensions;
using Scene.BattleNpcScene.View;
using UnityEngine;

namespace Scene.BattleNpcScene
{
    /// <summary>
    /// BattleNpc用の対戦紹介から本番開始までの演出
    /// </summary>
    public sealed class BattleNpcStaging : BattleStaging
    {
        [Header("参照")]
        [SerializeField] private ClayEditCameraView cameraView;
        [SerializeField] private BattleStartOverlayView overlayView;
        [SerializeField] private BattleMatchupBackgroundView matchupBackground;
        [SerializeField] private Transform matchupPlayerPoint;
        [SerializeField] private Transform matchupEnemyPoint;

        /// <summary>
        /// とどめ命中時のFinish演出
        /// </summary>
        public IBattleFinishPresentation FinishPresentation => overlayView;

        /// <summary>
        /// 部位破壊時のBreak演出
        /// </summary>
        public IBattlePartBreakPresentation PartBreakPresentation => overlayView;

        [Header("対戦紹介")]
        [SerializeField] private float matchupSideGapPadding = 0.55f;
        [SerializeField] private float matchupMinSideSeparation = 1.2f;
        [SerializeField] private float matchupMaxSideSeparation = 24f;
        [SerializeField] private float matchupSideSeparation = 1.5f;
        [SerializeField] private float matchupHorizontalAngle = 0f;
        [SerializeField] private float matchupVerticalAngle = 4f;
        [SerializeField] private float matchupDistance = 5.5f;
        [SerializeField] private float matchupFocusHeightOffset = 0.85f;
        [SerializeField] private float matchupModelTowardCameraDegrees = 34f;
        [SerializeField] private bool matchupAutoFrame = true;
        [SerializeField] private float matchupFramePadding = 0.8f;
        [SerializeField] private float matchupMinDistance = 3.8f;
        [SerializeField] private float matchupMaxDistance = 11f;

        [Header("本番カメラ")]
        [SerializeField] private float battleHorizontalAngle = 0f;
        [SerializeField] private float battleVerticalAngle = 10f;
        [SerializeField] private float battleDistance = 7.5f;
        [SerializeField] private float battleFocusHeightOffset = 0.75f;

        [Header("勝利演出")]
        [SerializeField] private float victoryVerticalAngle = 8f;
        [SerializeField] private float victoryDistance = 5.5f;
        [SerializeField] private float victoryFocusHeightOffset = 0.9f;
        [SerializeField] private bool victoryAutoFrame = true;
        [SerializeField] private float victoryFramePadding = 0.85f;
        [SerializeField] private float victoryMinDistance = 3.6f;
        [SerializeField] private float victoryMaxDistance = 7f;

        private void Awake()
        {
            if (cameraView == null)
            {
                Debug.LogError("[BattleNpcStaging] cameraViewが未配線です", this);
            }

            if (overlayView == null)
            {
                Debug.LogError("[BattleNpcStaging] overlayViewが未配線です", this);
            }

            if (matchupBackground == null)
            {
                Debug.LogError("[BattleNpcStaging] matchupBackgroundが未配線です", this);
            }
        }

        /// <summary>
        /// セーブスロット選択表示前の準備
        /// 教室は対戦紹介開始時にのみ隠す
        /// 退場途中でも対戦紹介オーバーレイを閉じる
        /// </summary>
        public void PrepareSelectionEntry()
        {
            matchupBackground?.ShowClassroom();
            overlayView?.HideForLeave();
        }

        /// <summary>
        /// 戦闘用Field教室を表示する
        /// </summary>
        public void EnsureClassroomVisible()
        {
            if (matchupBackground == null)
            {
                Debug.LogError("[BattleNpcStaging] matchupBackgroundが未配線です", this);
                return;
            }

            matchupBackground.enabled = true;
            matchupBackground.ShowClassroom();
        }

        /// <summary>
        /// 戦闘用Field教室を隠す
        /// </summary>
        public void HideClassroom()
        {
            matchupBackground?.HideClassroom();
        }

        /// <summary>
        /// 対戦炎演出のみ終了する
        /// </summary>
        public void EndMatchupPresentation()
        {
            matchupBackground?.EndFlamePresentation();
        }

        /// <inheritdoc />
        public override async UniTask PlayIntroAsync(BattleStagingContext context, CancellationToken cancellationToken)
        {
            GameplayTime.Reset();
            BattleHitStopClock.Clear();

            if (context.ScreenFade != null)
            {
                await context.ScreenFade.FadeOutAsync(cancellationToken);
            }

            // 採寸前に表示しないとRenderer境界が取れず既定値で寄ってしまう
            EnsureMatchupModelsVisible(context);
            PrepareCamera();
            context.Player?.PlayMotion(MotionType.Idle);
            context.Enemy?.PlayMotion(MotionType.Idle);

            ApplyMatchupLayout(context);
            Vector3 focus = ApplyMatchupCamera(context);

            if (matchupBackground != null)
            {
                matchupBackground.SetTargetCamera(UnityEngine.Camera.main);
                if (context.PlayerModel != null && context.EnemyModel != null)
                {
                    matchupBackground.SetCharacterWorldPositions(
                        context.PlayerModel.position,
                        context.EnemyModel.position);
                }
            }

            matchupBackground?.ShowFlame(focus);

            if (context.ScreenFade != null)
            {
                await context.ScreenFade.FadeInAsync(cancellationToken);
            }

            context.ScreenFade?.ReleasePresentationInput();

            if (overlayView != null)
            {
                overlayView.EnsureMatchupUiReady();
                string playerName = context.Player?.Name ?? string.Empty;
                string enemyName = context.Enemy?.Name ?? string.Empty;
                if (context.AutoStartMatchup || !overlayView.IsVsUiConfigured())
                {
                    await overlayView.ShowVsAndAutoStartAsync(playerName, enemyName, cancellationToken);
                }
                else
                {
                    await overlayView.ShowVsAndWaitStartAsync(
                        playerName,
                        enemyName,
                        context.Player,
                        context.Enemy,
                        cancellationToken);
                }
            }
            else
            {
                await DelayUnscaledAsync(1f, cancellationToken);
            }

            if (context.WaitForMatchupStartAsync != null)
            {
                await context.WaitForMatchupStartAsync(cancellationToken);
            }
        }

        /// <inheritdoc />
        public override async UniTask PlayBattleStartAsync(BattleStagingContext context, CancellationToken cancellationToken)
        {
            GameplayTime.Reset();
            BattleHitStopClock.Clear();
            if (context.ScreenFade != null)
            {
                await context.ScreenFade.FadeOutAsync(cancellationToken);
            }

            matchupBackground?.ShowClassroom();
            overlayView?.HideVsUi();
            ApplyBattleFieldLayout(context);

            if (cameraView != null)
            {
                cameraView.SetCameraOperatable(false);
                cameraView.SetFocusPosition(ResolveBattleFocus(context));
                cameraView.SetOrbitView(battleHorizontalAngle, battleVerticalAngle, battleDistance);
            }

            if (context.ScreenFade != null)
            {
                await context.ScreenFade.FadeInAsync(cancellationToken);
            }

            if (overlayView != null)
            {
                await overlayView.PlayReadyFightAsync(cancellationToken);
            }
            else
            {
                await DelayUnscaledAsync(1f, cancellationToken);
            }

            context.ScreenFade?.ReleasePresentationInput();
        }

        /// <inheritdoc />
        public override async UniTask PlayVictoryAsync(
            BattleStagingContext context,
            BattleUnit winner,
            CancellationToken cancellationToken)
        {
            GameplayTime.Reset();
            BattleHitStopClock.Clear();

            if (context?.ScreenFade != null)
            {
                await context.ScreenFade.FadeOutAsync(cancellationToken);
            }

            overlayView?.HideImmediate();
            overlayView?.HideVsUi();

            Transform winnerModel = null;
            if (winner != null)
            {
                BattleVictoryLayout.Apply(
                    context,
                    winner,
                    battleHorizontalAngle,
                    matchupModelTowardCameraDegrees);
                winnerModel = ResolveWinnerModel(context, winner);
            }
            else
            {
                if (context.PlayerModel != null)
                {
                    context.PlayerModel.gameObject.SetActive(false);
                }

                if (context.EnemyModel != null)
                {
                    context.EnemyModel.gameObject.SetActive(false);
                }
            }

            matchupBackground?.ShowClassroom();
            ApplyVictoryCamera(context, winner);

            if (context?.ScreenFade != null)
            {
                await context.ScreenFade.FadeInAsync(cancellationToken);
            }

            using CancellationTokenSource spinCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            UniTask spinTask = winnerModel != null
                ? BattleVictoryWalkSpin.SpinWhileAsync(
                    winnerModel,
                    VictorySpinDegreesPerSecond,
                    spinCts.Token)
                : UniTask.CompletedTask;

            try
            {
                if (overlayView != null)
                {
                    string label = winner != null ? winner.Name : "引き分け";
                    await overlayView.PlayVictoryPresentationAsync(label, cancellationToken);
                }

                IBattleDualVictoryReturnView dualReturnView = context?.VictoryDualReturnView;
                if (dualReturnView != null)
                {
                    dualReturnView.SetDualButtonsVisible(true);
                    context.VictoryReturnChoice = await dualReturnView.WaitVictoryReturnChoiceAsync(cancellationToken);
                    dualReturnView.SetDualButtonsVisible(false);
                }
                else
                {
                    IBattleVictoryReturnView returnView = context?.VictoryReturnView;
                    if (returnView != null)
                    {
                        context.VictoryReturnChoice = BattleVictoryReturnChoice.Title;
                        returnView.SetReturnButtonVisible(true);
                        await returnView.WaitReturnButtonClickAsync(cancellationToken);
                        returnView.SetReturnButtonVisible(false);
                    }
                    else
                    {
                        await DelayUnscaledAsync(2f, cancellationToken);
                    }
                }
            }
            finally
            {
                spinCts.Cancel();
                try
                {
                    await spinTask;
                }
                catch (System.OperationCanceledException)
                {
                }

                overlayView?.EndVictoryPresentation();
            }
        }

        private void ApplyVictoryCamera(BattleStagingContext context, BattleUnit winner)
        {
            if (cameraView == null || context == null || winner == null)
            {
                return;
            }

            Transform winnerModel = winner == context.Player ? context.PlayerModel : context.EnemyModel;
            if (winnerModel == null)
            {
                return;
            }

            bool hasBounds = BattleFieldFocusResolver.TryGetPosedModelBounds(
                winnerModel,
                out Bounds bounds);

            Vector3 focus = hasBounds
                ? bounds.center
                : winnerModel.position + Vector3.up * victoryFocusHeightOffset;
            cameraView.SetCameraOperatable(false);
            cameraView.SetFocusPosition(focus);

            float distance = victoryDistance;
            if (victoryAutoFrame && hasBounds)
            {
                UnityEngine.Camera camera = UnityEngine.Camera.main;
                float verticalFov = camera != null ? camera.fieldOfView : 45f;
                float aspect = camera != null ? camera.aspect : 16f / 9f;
                distance = BattleFieldFocusResolver.ResolveOrbitDistanceForBounds(
                    bounds,
                    battleHorizontalAngle,
                    victoryVerticalAngle,
                    verticalFov,
                    aspect,
                    victoryFramePadding,
                    victoryMinDistance,
                    victoryMaxDistance);
            }

            cameraView.SetOrbitView(battleHorizontalAngle, victoryVerticalAngle, distance);
        }

        private void OnDisable()
        {
            if (matchupBackground == null)
            {
                return;
            }

            Transform sceneRoot = transform.root;
            if (sceneRoot != null && !sceneRoot.gameObject.activeSelf)
            {
                return;
            }

            // 炎のみ終了するField再表示は育成戦闘終了を壊すため行わない
            matchupBackground.EndFlamePresentation();
        }

        private static float ResolveMatchupGroundY(BattleStagingContext context)
        {
            float playerY = context.PlayerSpawn != null ? context.PlayerSpawn.position.y : 0f;
            float enemyY = context.EnemySpawn != null ? context.EnemySpawn.position.y : 0f;
            return Mathf.Max(playerY, enemyY);
        }

        private static void EnsureMatchupModelsVisible(BattleStagingContext context)
        {
            if (context.PlayerModel != null)
            {
                context.PlayerModel.gameObject.SetActive(true);
            }

            if (context.EnemyModel != null)
            {
                context.EnemyModel.gameObject.SetActive(true);
            }
        }

        private void ApplyMatchupLayout(BattleStagingContext context)
        {
            if (context.PlayerModel == null || context.EnemyModel == null)
            {
                return;
            }

            if (matchupPlayerPoint != null && matchupEnemyPoint != null)
            {
                BattleMatchupLayout.Apply(
                    context.PlayerModel,
                    context.EnemyModel,
                    matchupPlayerPoint,
                    matchupEnemyPoint,
                    matchupHorizontalAngle,
                    matchupModelTowardCameraDegrees,
                    matchupSideGapPadding);
                SyncMotionLayout(context);
                return;
            }

            // 短いキャラ向けの控えめな初期間隔で並べ隙間補正に任せる
            // 前後に長いモデルはEnforceMatchupSideClearanceが外側へ広げる
            float sideSeparation = Mathf.Clamp(
                matchupSideSeparation,
                matchupMinSideSeparation,
                matchupMaxSideSeparation);
            BattleMatchupLayout.ApplyFromSpawns(
                context.PlayerModel,
                context.EnemyModel,
                context.PlayerSpawn,
                context.EnemySpawn,
                sideSeparation,
                matchupHorizontalAngle,
                matchupModelTowardCameraDegrees,
                matchupSideGapPadding);
            SyncMotionLayout(context);
        }

        private static void SyncMotionLayout(BattleStagingContext context)
        {
            context.Player?.SyncMotionLayoutPosition();
            context.Enemy?.SyncMotionLayoutPosition();
        }

        private void ApplyBattleFieldLayout(BattleStagingContext context)
        {
            if (context.PlayerModel == null || context.EnemyModel == null)
            {
                return;
            }

            var layout = new BattleFieldLayout(
                context.PlayerModel,
                context.EnemyModel,
                context.PlayerSpawn,
                context.EnemySpawn,
                battleHorizontalAngle);

            layout.ApplyInitialBattlePositions(context.InitialBattleDistance, context.MaxBattleDistance);
            SyncMotionLayout(context);
        }

        private void PrepareCamera()
        {
            if (cameraView == null)
            {
                return;
            }

            // 注視点は配置確定後にApplyMatchupCameraで採寸して決める
            cameraView.SetCameraEnable(true);
            cameraView.SetCameraOperatable(false);
        }

        private Vector3 ResolveBattleFocus(BattleStagingContext context)
        {
            float groundY = ResolveBattleGroundY(context);
            if (context.PlayerModel != null && context.EnemyModel != null)
            {
                return BattleFieldFocusResolver.ResolveMidpoint(
                    context.PlayerModel,
                    context.EnemyModel,
                    groundY,
                    battleFocusHeightOffset);
            }

            Vector3 playerHome = context.PlayerSpawn != null
                ? context.PlayerSpawn.position
                : Vector3.zero;
            Vector3 enemyHome = context.EnemySpawn != null
                ? context.EnemySpawn.position
                : Vector3.zero;
            Vector3 fallback = (playerHome + enemyHome) * 0.5f;
            fallback.y = groundY + battleFocusHeightOffset;
            return fallback;
        }

        private static float ResolveBattleGroundY(BattleStagingContext context)
        {
            float playerY = context.PlayerSpawn != null ? context.PlayerSpawn.position.y : 0f;
            float enemyY = context.EnemySpawn != null ? context.EnemySpawn.position.y : 0f;
            return (playerY + enemyY) * 0.5f;
        }

        private Vector3 ResolveMatchupFallbackFocus(BattleStagingContext context)
        {
            if (matchupPlayerPoint != null && matchupEnemyPoint != null)
            {
                Vector3 focus = (matchupPlayerPoint.position + matchupEnemyPoint.position) * 0.5f;
                focus.y += matchupFocusHeightOffset;
                return focus;
            }

            // スポーン中央を使い長い側へ押した後もVSと注視点を一致させる
            if (context.PlayerSpawn != null && context.EnemySpawn != null)
            {
                Vector3 spawnFocus = (context.PlayerSpawn.position + context.EnemySpawn.position) * 0.5f;
                spawnFocus.y = ResolveBattleGroundY(context) + matchupFocusHeightOffset;
                return spawnFocus;
            }

            if (context.PlayerModel != null && context.EnemyModel != null)
            {
                return BattleFieldFocusResolver.ResolveMidpoint(
                    context.PlayerModel,
                    context.EnemyModel,
                    ResolveBattleGroundY(context),
                    matchupFocusHeightOffset);
            }

            Vector3 center = context.ResolveCenterPosition();
            center.y += matchupFocusHeightOffset;
            return center;
        }

        // 注視点はレイアウト中央に固定し距離だけ姿勢実測で決める
        // 結合AABB中心だと前後に長い側へ寄り相手側はみ出しに見える
        private Vector3 ApplyMatchupCamera(BattleStagingContext context)
        {
            Bounds bounds = default;
            bool hasBounds = context.PlayerModel != null
                && context.EnemyModel != null
                && BattleFieldFocusResolver.TryGetPosedCombinedBounds(
                    context.PlayerModel,
                    context.EnemyModel,
                    out bounds);

            Vector3 focus = ResolveMatchupFallbackFocus(context);

            if (cameraView == null)
            {
                return focus;
            }

            float distance = matchupDistance;
            if (matchupAutoFrame && hasBounds)
            {
                UnityEngine.Camera camera = UnityEngine.Camera.main;
                float verticalFov = camera != null ? camera.fieldOfView : 45f;
                float aspect = camera != null ? camera.aspect : 16f / 9f;
                distance = BattleFieldFocusResolver.ResolveOrbitDistanceForBoundsAroundFocus(
                    bounds,
                    focus,
                    matchupHorizontalAngle,
                    matchupVerticalAngle,
                    verticalFov,
                    aspect,
                    matchupFramePadding,
                    matchupMinDistance,
                    matchupMaxDistance);
            }

            cameraView.SetCameraOperatable(false);
            cameraView.SetFocusPosition(focus);
            cameraView.SetOrbitView(matchupHorizontalAngle, matchupVerticalAngle, distance);
            return focus;
        }
    }
}
