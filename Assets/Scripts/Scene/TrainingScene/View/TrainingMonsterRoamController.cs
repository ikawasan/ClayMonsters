using Audio;
using Audio.Interface;
using ClayEditor.Rigging;
using Cysharp.Threading.Tasks;
using Scene.TrainingScene.Interface;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using VContainer;

namespace Scene.TrainingScene.View
{
    /// <summary>
    /// 訓練後の専用背景でモンスターを範囲内に徘徊させる
    /// クリック時はカメラを向いて感嘆符を出し近くへ寄る
    /// </summary>
    public sealed class TrainingMonsterRoamController : MonoBehaviour, ITrainingMonsterRoamController
    {
        private static readonly Quaternion DisplayFacingRotation = Quaternion.Euler(0f, 180f, 0f);

        [Header("依存")]
        [SerializeField] private TrainingDisplay trainingDisplay;
        [Tooltip("クリック判定と向きに使うカメラ未設定時はMainCamera")]
        [SerializeField] private UnityEngine.Camera roamCamera;
        [Tooltip("頭上の感嘆符表示ルート初期は非表示")]
        [SerializeField] private GameObject noticeMarkRoot;

        [Header("徘徊範囲")]
        [Tooltip("未設定時はdisplayAnchor中心の半範囲を使う")]
        [SerializeField] private BoxCollider roamBounds;
        [SerializeField] private Vector2 roamHalfExtents = new Vector2(2.5f, 2.5f);

        [Header("移動")]
        [SerializeField] private float walkSpeed = 1.1f;
        [SerializeField] private float arriveDistance = 0.08f;
        [SerializeField] private float minIdleSeconds = 1.2f;
        [SerializeField] private float maxIdleSeconds = 3.5f;
        [SerializeField] private float minWalkSeconds = 1.5f;
        [SerializeField] private float maxWalkSeconds = 4f;

        [Header("クリック反応")]
        [SerializeField] private float noticeSeconds = 1.2f;
        [SerializeField] private float noticeHeightOffset = 0.25f;
        [SerializeField] private float noticeAppearSeconds = 0.28f;
        [SerializeField] private float noticeHoldSeconds = 0.55f;
        [SerializeField] private float noticeHideSeconds = 0.18f;
        [SerializeField] private float noticePopScale = 1.35f;
        [SerializeField] private float noticeHopHeight = 0.18f;

        [Header("寄ってくる")]
        [Tooltip("カメラからこの距離まで近寄る")]
        [SerializeField] private float approachDistanceFromCamera = 2.8f;
        [Tooltip("クリック時の寄る速度とボール追跡で共用する")]
        [SerializeField] private float approachSpeed = 1.35f;
        [Tooltip("近寄ったあとその場に留まる秒数")]
        [SerializeField] private float approachHoldSeconds = 5f;

        [Inject] private readonly ISeService seService;

        private CancellationTokenSource roamCts;
        private int roamSessionId;
        private int clickReactionId;
        private bool isRoaming;
        private bool isReacting;
        private bool isChasing;
        private bool suppressClickReaction;
        private Transform chaseTarget;
        private bool hasRoamSpawnPlacement;
        private float noticeAnimHeightBoost;
        private Vector3 noticeBaseScale = Vector3.one;

        private void Awake()
        {
            if (trainingDisplay == null)
            {
                trainingDisplay = GetComponent<TrainingDisplay>();
            }

            if (trainingDisplay == null)
            {
                Debug.LogError(
                    "[TrainingMonsterRoamController] trainingDisplayが未配線です",
                    this);
            }

            EnsureNoticeMark();
            if (noticeMarkRoot != null)
            {
                noticeBaseScale = noticeMarkRoot.transform.localScale;
                if (noticeBaseScale.sqrMagnitude < 0.0001f)
                {
                    noticeBaseScale = Vector3.one;
                }
            }

            SetNoticeVisible(false);
        }

        private void Update()
        {
            if (!isRoaming
                || isReacting
                || isChasing
                || suppressClickReaction
                || trainingDisplay == null)
            {
                return;
            }

            if (!WasPrimaryClickPressed())
            {
                return;
            }

            if (IsPointerOverUi())
            {
                return;
            }

            if (!TryRaycastMonster())
            {
                return;
            }

            HandleClickReactionAsync(roamSessionId, roamCts != null ? roamCts.Token : CancellationToken.None)
                .Forget();
        }

        private void LateUpdate()
        {
            if (!isRoaming || noticeMarkRoot == null || !noticeMarkRoot.activeSelf)
            {
                return;
            }

            UpdateNoticeMarkTransform();
            BillboardNoticeMark();
        }

        /// <inheritdoc/>
        public void StartRoam(CancellationToken cancellationToken)
        {
            StopRoamInternal(returnToAnchor: false);
            if (trainingDisplay == null || trainingDisplay.LoadedModel == null)
            {
                return;
            }

            EnsureNoticeMark();
            SetNoticeVisible(false);

            // 非Roamからの初回のみランダム配置するRoam→訓練→Roamでは位置を維持する
            if (!hasRoamSpawnPlacement)
            {
                PlaceAtRandomRoamSpawn();
                hasRoamSpawnPlacement = true;
            }

            isRoaming = true;
            int sessionId = ++roamSessionId;
            roamCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            RunRoamLoopAsync(sessionId, roamCts.Token).Forget();
        }

        /// <inheritdoc/>
        public void StopRoam()
        {
            StopRoamInternal(returnToAnchor: true);
        }

        /// <inheritdoc/>
        public bool TryGetRoamPlanarBounds(out Vector3 center, out Vector2 halfExtents)
        {
            if (roamBounds != null && roamBounds.enabled)
            {
                Bounds bounds = roamBounds.bounds;
                center = new Vector3(bounds.center.x, bounds.center.y, bounds.center.z);
                halfExtents = new Vector2(
                    Mathf.Max(0.1f, bounds.extents.x),
                    Mathf.Max(0.1f, bounds.extents.z));
                return true;
            }

            Transform anchor = trainingDisplay != null ? trainingDisplay.DisplayAnchor : null;
            if (anchor == null)
            {
                center = Vector3.zero;
                halfExtents = Vector2.zero;
                return false;
            }

            center = anchor.position;
            halfExtents = new Vector2(
                Mathf.Max(0.1f, roamHalfExtents.x),
                Mathf.Max(0.1f, roamHalfExtents.y));
            return true;
        }

        /// <inheritdoc/>
        public Vector3 ClampToRoamArea(Vector3 worldPoint, float keepY)
        {
            return ClampToRoamAreaInternal(worldPoint, keepY);
        }

        /// <inheritdoc/>
        public float ResolveGroundY(Vector3 nearPosition)
        {
            GameObject model = trainingDisplay != null ? trainingDisplay.LoadedModel : null;
            if (model != null)
            {
                return model.transform.position.y;
            }

            if (TryGetRoamPlanarBounds(out Vector3 center, out _))
            {
                return center.y;
            }

            return nearPosition.y;
        }

        /// <inheritdoc/>
        public void StartChase(Transform target)
        {
            chaseTarget = target;
            isChasing = target != null;
            InvalidateClickReaction();
        }

        /// <inheritdoc/>
        public void StopChase()
        {
            chaseTarget = null;
            isChasing = false;
        }

        /// <inheritdoc/>
        public void SetClickReactionSuppressed(bool suppressed)
        {
            suppressClickReaction = suppressed;
            if (suppressed)
            {
                InvalidateClickReaction();
            }
        }

        private void OnDisable()
        {
            StopRoamInternal(returnToAnchor: false);
        }

        private void OnDestroy()
        {
            StopRoamInternal(returnToAnchor: false);
        }

        private void StopRoamInternal(bool returnToAnchor)
        {
            roamSessionId++;
            clickReactionId++;
            isRoaming = false;
            isReacting = false;
            isChasing = false;
            suppressClickReaction = false;
            chaseTarget = null;
            ResetNoticeVisual();
            SetNoticeVisible(false);

            CancellationTokenSource pendingCts = roamCts;
            roamCts = null;
            if (pendingCts != null)
            {
                pendingCts.Cancel();
                pendingCts.Dispose();
            }

            ForceIdleAtAnchor(returnToAnchor);
        }

        private void ForceIdleAtAnchor(bool returnToAnchor)
        {
            if (trainingDisplay == null)
            {
                return;
            }

            GameObject model = trainingDisplay.LoadedModel;
            ProceduralMotionCharacter motion = trainingDisplay.MotionCharacter;
            if (model == null)
            {
                motion?.SetRootTranslationEnabled(false);
                motion?.Play(MotionType.Idle);
                return;
            }

            if (returnToAnchor)
            {
                Transform parent = model.transform.parent;
                model.transform.SetParent(parent, false);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = DisplayFacingRotation;
                model.transform.localScale = Vector3.one;
                trainingDisplay.SnapDisplayedModelToGround();
                hasRoamSpawnPlacement = false;
            }

            motion?.SetRootTranslationEnabled(false);
            motion?.Play(MotionType.Idle);
            motion?.OnLayoutPositionChanged();
        }

        private bool IsActiveRoamSession(int sessionId)
        {
            return isRoaming && sessionId == roamSessionId;
        }

        private async UniTaskVoid RunRoamLoopAsync(int sessionId, CancellationToken cancellationToken)
        {
            try
            {
                while (IsActiveRoamSession(sessionId) && !cancellationToken.IsCancellationRequested)
                {
                    GameObject model = trainingDisplay != null ? trainingDisplay.LoadedModel : null;
                    ProceduralMotionCharacter motion = trainingDisplay != null
                        ? trainingDisplay.MotionCharacter
                        : null;
                    if (model == null || motion == null)
                    {
                        break;
                    }

                    await WaitWhileReactingAsync(sessionId, cancellationToken);
                    if (!IsActiveRoamSession(sessionId) || cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    if (isChasing && chaseTarget != null)
                    {
                        await ChaseTargetAsync(sessionId, model.transform, motion, cancellationToken);
                        continue;
                    }

                    await IdleAsync(sessionId, motion, cancellationToken);
                    if (!IsActiveRoamSession(sessionId)
                        || cancellationToken.IsCancellationRequested
                        || model == null)
                    {
                        break;
                    }

                    await WaitWhileReactingAsync(sessionId, cancellationToken);
                    if (!IsActiveRoamSession(sessionId)
                        || cancellationToken.IsCancellationRequested
                        || model == null)
                    {
                        break;
                    }

                    if (isChasing && chaseTarget != null)
                    {
                        await ChaseTargetAsync(sessionId, model.transform, motion, cancellationToken);
                        continue;
                    }

                    Vector3 destination = PickDestination(model.transform.position);
                    await WalkToAsync(sessionId, model.transform, motion, destination, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                if (!IsActiveRoamSession(sessionId))
                {
                    ProceduralMotionCharacter motion = trainingDisplay != null
                        ? trainingDisplay.MotionCharacter
                        : null;
                    motion?.SetRootTranslationEnabled(false);
                    if (motion != null && !isRoaming)
                    {
                        motion.Play(MotionType.Idle);
                    }
                }
            }
        }

        private async UniTaskVoid HandleClickReactionAsync(int sessionId, CancellationToken cancellationToken)
        {
            if (isReacting
                || isChasing
                || suppressClickReaction
                || !IsActiveRoamSession(sessionId))
            {
                return;
            }

            isReacting = true;
            int reactionId = ++clickReactionId;
            GameObject model = trainingDisplay != null ? trainingDisplay.LoadedModel : null;
            ProceduralMotionCharacter motion = trainingDisplay != null
                ? trainingDisplay.MotionCharacter
                : null;
            try
            {
                if (model != null)
                {
                    FaceCamera(model.transform);
                    motion?.SetRootTranslationEnabled(false);
                    motion?.Play(MotionType.Idle);
                    motion?.OnLayoutPositionChanged();
                }

                await PlayNoticeAnimationAsync(reactionId, cancellationToken);
                if (!IsClickReactionActive(sessionId, reactionId)
                    || cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                ResetNoticeVisual();
                SetNoticeVisible(false);

                if (model != null && motion != null)
                {
                    await ApproachCameraAsync(
                        sessionId,
                        reactionId,
                        model.transform,
                        motion,
                        cancellationToken);
                    if (!IsClickReactionActive(sessionId, reactionId)
                        || cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    FaceCamera(model.transform);
                    motion.SetRootTranslationEnabled(false);
                    motion.Play(MotionType.Idle);
                    motion.OnLayoutPositionChanged();
                    await WaitApproachHoldAsync(sessionId, reactionId, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                ResetNoticeVisual();
                SetNoticeVisible(false);
                if (reactionId == clickReactionId)
                {
                    isReacting = false;
                }
            }
        }

        private void InvalidateClickReaction()
        {
            clickReactionId++;
            isReacting = false;
            ResetNoticeVisual();
            SetNoticeVisible(false);
        }

        private bool IsClickReactionActive(int sessionId, int reactionId)
        {
            return IsActiveRoamSession(sessionId)
                && reactionId == clickReactionId
                && !isChasing
                && !suppressClickReaction;
        }

        private async UniTask ChaseTargetAsync(
            int sessionId,
            Transform modelTransform,
            ProceduralMotionCharacter motion,
            CancellationToken cancellationToken)
        {
            MotionType walkMotion = ClayEditMotionPreview.ResolveRunMotion(motion);
            motion.SetRootTranslationEnabled(false);
            motion.Play(walkMotion);

            float speed = Mathf.Max(0.1f, approachSpeed);
            float arrive = Mathf.Max(0.02f, arriveDistance);
            bool wasMoving = true;
            while (IsActiveRoamSession(sessionId)
                && isChasing
                && chaseTarget != null
                && !cancellationToken.IsCancellationRequested)
            {
                Vector3 current = modelTransform.position;
                Vector3 target = chaseTarget.position;
                target.y = current.y;
                target = ClampToRoamAreaInternal(target, current.y);
                Vector3 toTarget = target - current;
                toTarget.y = 0f;
                float planarDistance = toTarget.magnitude;
                if (planarDistance > arrive)
                {
                    if (!wasMoving)
                    {
                        motion.Play(walkMotion);
                        wasMoving = true;
                    }

                    Vector3 step = toTarget.normalized * (speed * Time.deltaTime);
                    if (step.magnitude > planarDistance)
                    {
                        step = toTarget;
                    }

                    Vector3 next = current + step;
                    next.y = current.y;
                    FaceWalkDirection(modelTransform, toTarget);
                    modelTransform.position = next;
                    trainingDisplay.SnapDisplayedModelToGround();
                    motion.OnLayoutPositionChanged();
                }
                else if (wasMoving)
                {
                    motion.Play(MotionType.Idle);
                    motion.OnLayoutPositionChanged();
                    wasMoving = false;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            if (IsActiveRoamSession(sessionId))
            {
                motion.SetRootTranslationEnabled(false);
                motion.Play(MotionType.Idle);
                motion.OnLayoutPositionChanged();
            }
        }

        private async UniTask ApproachCameraAsync(
            int sessionId,
            int reactionId,
            Transform modelTransform,
            ProceduralMotionCharacter motion,
            CancellationToken cancellationToken)
        {
            if (!TryResolveApproachDestination(modelTransform.position, out Vector3 destination))
            {
                return;
            }

            MotionType walkMotion = ClayEditMotionPreview.ResolveRunMotion(motion);
            motion.SetRootTranslationEnabled(false);
            motion.Play(walkMotion);

            float speed = Mathf.Max(0.1f, approachSpeed);
            float arrive = Mathf.Max(0.02f, arriveDistance);
            while (IsClickReactionActive(sessionId, reactionId)
                && !cancellationToken.IsCancellationRequested)
            {
                Vector3 current = modelTransform.position;
                Vector3 toTarget = destination - current;
                toTarget.y = 0f;
                float planarDistance = toTarget.magnitude;
                if (planarDistance <= arrive)
                {
                    break;
                }

                Vector3 step = toTarget.normalized * (speed * Time.deltaTime);
                if (step.magnitude > planarDistance)
                {
                    step = toTarget;
                }

                Vector3 next = current + step;
                next.y = current.y;
                FaceWalkDirection(modelTransform, toTarget);
                modelTransform.position = next;
                trainingDisplay.SnapDisplayedModelToGround();
                motion.OnLayoutPositionChanged();
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            if (!IsActiveRoamSession(sessionId))
            {
                motion.SetRootTranslationEnabled(false);
                motion.Play(MotionType.Idle);
            }
        }

        private bool TryResolveApproachDestination(Vector3 currentPosition, out Vector3 destination)
        {
            destination = currentPosition;
            UnityEngine.Camera camera = ResolveCamera();
            if (camera == null)
            {
                return false;
            }

            Vector3 cameraPlanar = new Vector3(
                camera.transform.position.x,
                currentPosition.y,
                camera.transform.position.z);
            Vector3 toCamera = cameraPlanar - currentPosition;
            toCamera.y = 0f;
            float distance = toCamera.magnitude;
            float keepDistance = Mathf.Max(0.5f, approachDistanceFromCamera);
            if (distance <= keepDistance + arriveDistance)
            {
                return false;
            }

            destination = cameraPlanar - toCamera.normalized * keepDistance;
            destination.y = currentPosition.y;
            destination = ClampToRoamAreaInternal(destination, currentPosition.y);
            Vector3 remaining = destination - currentPosition;
            remaining.y = 0f;
            return remaining.sqrMagnitude > arriveDistance * arriveDistance;
        }

        private Vector3 ClampToRoamAreaInternal(Vector3 worldPoint, float keepY)
        {
            if (roamBounds != null && roamBounds.enabled)
            {
                Bounds bounds = roamBounds.bounds;
                worldPoint.x = Mathf.Clamp(worldPoint.x, bounds.min.x, bounds.max.x);
                worldPoint.z = Mathf.Clamp(worldPoint.z, bounds.min.z, bounds.max.z);
                worldPoint.y = keepY;
                return worldPoint;
            }

            Transform anchor = trainingDisplay != null ? trainingDisplay.DisplayAnchor : null;
            Vector3 center = anchor != null ? anchor.position : worldPoint;
            float halfX = Mathf.Max(0.1f, roamHalfExtents.x);
            float halfZ = Mathf.Max(0.1f, roamHalfExtents.y);
            worldPoint.x = Mathf.Clamp(worldPoint.x, center.x - halfX, center.x + halfX);
            worldPoint.z = Mathf.Clamp(worldPoint.z, center.z - halfZ, center.z + halfZ);
            worldPoint.y = keepY;
            return worldPoint;
        }

        private async UniTask WaitApproachHoldAsync(
            int sessionId,
            int reactionId,
            CancellationToken cancellationToken)
        {
            float hold = Mathf.Max(0f, approachHoldSeconds);
            float elapsed = 0f;
            while (IsClickReactionActive(sessionId, reactionId)
                && !cancellationToken.IsCancellationRequested
                && elapsed < hold)
            {
                elapsed += Time.deltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
        }

        private async UniTask PlayNoticeAnimationAsync(
            int reactionId,
            CancellationToken cancellationToken)
        {
            float appear = Mathf.Max(0.05f, noticeAppearSeconds);
            float hold = Mathf.Max(0.05f, noticeHoldSeconds);
            float hide = Mathf.Max(0.05f, noticeHideSeconds);
            float totalConfigured = Mathf.Max(0.1f, noticeSeconds);
            float phaseSum = appear + hold + hide;
            if (phaseSum > totalConfigured)
            {
                float scale = totalConfigured / phaseSum;
                appear *= scale;
                hold *= scale;
                hide *= scale;
            }
            else
            {
                hold += totalConfigured - phaseSum;
            }

            SetNoticeVisible(true);
            seService.Play(SeTrackId.Reaction);
            noticeAnimHeightBoost = 0f;
            SetNoticeScale(0f);
            UpdateNoticeMarkTransform();
            BillboardNoticeMark();

            await AnimateNoticeAppearAsync(reactionId, appear, cancellationToken);
            if (reactionId != clickReactionId || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await AnimateNoticeHoldAsync(reactionId, hold, cancellationToken);
            if (reactionId != clickReactionId || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await AnimateNoticeHideAsync(reactionId, hide, cancellationToken);
        }

        private async UniTask AnimateNoticeAppearAsync(
            int reactionId,
            float duration,
            CancellationToken cancellationToken)
        {
            float elapsed = 0f;
            float peak = Mathf.Max(1f, noticePopScale);
            while (elapsed < duration
                && reactionId == clickReactionId
                && !cancellationToken.IsCancellationRequested)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                float scale;
                if (t < 0.55f)
                {
                    float riseT = Mathf.SmoothStep(0f, 1f, t / 0.55f);
                    scale = Mathf.Lerp(0f, peak, riseT);
                }
                else
                {
                    float settleT = Mathf.SmoothStep(0f, 1f, (t - 0.55f) / 0.45f);
                    scale = Mathf.Lerp(peak, 1f, settleT);
                }

                float hop = Mathf.Sin(t * Mathf.PI) * noticeHopHeight;
                SetNoticeScale(scale);
                noticeAnimHeightBoost = hop;
                UpdateNoticeMarkTransform();
                BillboardNoticeMark();
                elapsed += Time.deltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            if (reactionId != clickReactionId)
            {
                return;
            }

            SetNoticeScale(1f);
            noticeAnimHeightBoost = 0f;
            UpdateNoticeMarkTransform();
        }

        private async UniTask AnimateNoticeHoldAsync(
            int reactionId,
            float duration,
            CancellationToken cancellationToken)
        {
            float elapsed = 0f;
            while (elapsed < duration
                && reactionId == clickReactionId
                && !cancellationToken.IsCancellationRequested)
            {
                float pulse = 1f + Mathf.Sin(elapsed * Mathf.PI * 4f) * 0.06f;
                SetNoticeScale(pulse);
                noticeAnimHeightBoost = 0f;
                UpdateNoticeMarkTransform();
                BillboardNoticeMark();
                elapsed += Time.deltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            if (reactionId != clickReactionId)
            {
                return;
            }

            SetNoticeScale(1f);
        }

        private async UniTask AnimateNoticeHideAsync(
            int reactionId,
            float duration,
            CancellationToken cancellationToken)
        {
            float elapsed = 0f;
            float startScale = noticeMarkRoot != null
                ? noticeMarkRoot.transform.localScale.x / Mathf.Max(0.0001f, noticeBaseScale.x)
                : 1f;
            while (elapsed < duration
                && reactionId == clickReactionId
                && !cancellationToken.IsCancellationRequested)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = t * t;
                SetNoticeScale(Mathf.Lerp(startScale, 0f, eased));
                noticeAnimHeightBoost = Mathf.Lerp(0f, noticeHopHeight * 0.35f, eased);
                UpdateNoticeMarkTransform();
                BillboardNoticeMark();
                elapsed += Time.deltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            if (reactionId != clickReactionId)
            {
                return;
            }

            SetNoticeScale(0f);
            noticeAnimHeightBoost = 0f;
        }

        private void SetNoticeScale(float normalizedScale)
        {
            if (noticeMarkRoot == null)
            {
                return;
            }

            noticeMarkRoot.transform.localScale =
                noticeBaseScale * Mathf.Max(0f, normalizedScale);
        }

        private void ResetNoticeVisual()
        {
            noticeAnimHeightBoost = 0f;
            if (noticeMarkRoot != null)
            {
                noticeMarkRoot.transform.localScale = noticeBaseScale;
            }
        }

        private async UniTask WaitWhileReactingAsync(int sessionId, CancellationToken cancellationToken)
        {
            while (isReacting
                && IsActiveRoamSession(sessionId)
                && !cancellationToken.IsCancellationRequested)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
        }

        private async UniTask IdleAsync(
            int sessionId,
            ProceduralMotionCharacter motion,
            CancellationToken cancellationToken)
        {
            motion.SetRootTranslationEnabled(false);
            motion.Play(MotionType.Idle);
            float idleSeconds = UnityEngine.Random.Range(minIdleSeconds, maxIdleSeconds);
            float elapsed = 0f;
            while (IsActiveRoamSession(sessionId)
                && !cancellationToken.IsCancellationRequested
                && elapsed < idleSeconds)
            {
                if (isReacting)
                {
                    await WaitWhileReactingAsync(sessionId, cancellationToken);
                    return;
                }

                if (isChasing)
                {
                    return;
                }

                elapsed += Time.deltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
        }

        private async UniTask WalkToAsync(
            int sessionId,
            Transform modelTransform,
            ProceduralMotionCharacter motion,
            Vector3 destination,
            CancellationToken cancellationToken)
        {
            MotionType walkMotion = ClayEditMotionPreview.ResolveRunMotion(motion);
            motion.SetRootTranslationEnabled(false);
            motion.Play(walkMotion);

            float maxSeconds = UnityEngine.Random.Range(minWalkSeconds, maxWalkSeconds);
            float elapsed = 0f;
            while (IsActiveRoamSession(sessionId)
                && !cancellationToken.IsCancellationRequested
                && elapsed < maxSeconds)
            {
                if (isReacting)
                {
                    await WaitWhileReactingAsync(sessionId, cancellationToken);
                    return;
                }

                if (isChasing)
                {
                    return;
                }

                Vector3 current = modelTransform.position;
                Vector3 toTarget = destination - current;
                toTarget.y = 0f;
                float planarDistance = toTarget.magnitude;
                if (planarDistance <= arriveDistance)
                {
                    break;
                }

                Vector3 step = toTarget.normalized * (walkSpeed * Time.deltaTime);
                if (step.magnitude > planarDistance)
                {
                    step = toTarget;
                }

                Vector3 next = current + step;
                next.y = current.y;
                FaceWalkDirection(modelTransform, toTarget);
                modelTransform.position = next;
                trainingDisplay.SnapDisplayedModelToGround();
                motion.OnLayoutPositionChanged();

                elapsed += Time.deltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            if (!IsActiveRoamSession(sessionId))
            {
                motion.SetRootTranslationEnabled(false);
                motion.Play(MotionType.Idle);
                return;
            }

            if (!isReacting)
            {
                motion.Play(MotionType.Idle);
            }
        }

        private void PlaceAtRandomRoamSpawn()
        {
            GameObject model = trainingDisplay != null ? trainingDisplay.LoadedModel : null;
            if (model == null)
            {
                return;
            }

            Transform modelTransform = model.transform;
            Vector3 spawnPosition = PickDestination(modelTransform.position);
            modelTransform.position = spawnPosition;
            modelTransform.rotation = DisplayFacingRotation;
            trainingDisplay.SnapDisplayedModelToGround();

            ProceduralMotionCharacter motion = trainingDisplay.MotionCharacter;
            motion?.SetRootTranslationEnabled(false);
            motion?.Play(MotionType.Idle);
            motion?.OnLayoutPositionChanged();
        }

        private Vector3 PickDestination(Vector3 currentPosition)
        {
            if (TrySampleBoundsPoint(out Vector3 sampled))
            {
                sampled.y = currentPosition.y;
                return sampled;
            }

            Transform anchor = trainingDisplay != null ? trainingDisplay.DisplayAnchor : null;
            Vector3 center = anchor != null ? anchor.position : currentPosition;
            float halfX = Mathf.Max(0.1f, roamHalfExtents.x);
            float halfZ = Mathf.Max(0.1f, roamHalfExtents.y);
            return new Vector3(
                center.x + UnityEngine.Random.Range(-halfX, halfX),
                currentPosition.y,
                center.z + UnityEngine.Random.Range(-halfZ, halfZ));
        }

        private bool TrySampleBoundsPoint(out Vector3 worldPoint)
        {
            worldPoint = default;
            if (roamBounds == null || !roamBounds.enabled)
            {
                return false;
            }

            Bounds bounds = roamBounds.bounds;
            worldPoint = new Vector3(
                UnityEngine.Random.Range(bounds.min.x, bounds.max.x),
                bounds.center.y,
                UnityEngine.Random.Range(bounds.min.z, bounds.max.z));
            return true;
        }

        private static void FaceWalkDirection(Transform modelTransform, Vector3 planarDirection)
        {
            if (planarDirection.sqrMagnitude < 0.0001f)
            {
                return;
            }

            // 表示はY180でTransform.forwardが正面と一致するため進行方向をそのまま向ける
            modelTransform.rotation = Quaternion.LookRotation(planarDirection.normalized, Vector3.up);
        }

        private void FaceCamera(Transform modelTransform)
        {
            UnityEngine.Camera camera = ResolveCamera();
            if (modelTransform == null || camera == null)
            {
                return;
            }

            Vector3 toCamera = camera.transform.position - modelTransform.position;
            toCamera.y = 0f;
            if (toCamera.sqrMagnitude < 0.0001f)
            {
                return;
            }

            modelTransform.rotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
        }

        private bool TryRaycastMonster()
        {
            UnityEngine.Camera camera = ResolveCamera();
            if (camera == null || trainingDisplay == null)
            {
                return false;
            }

            if (!trainingDisplay.TryGetVisualBounds(out Bounds bounds))
            {
                return false;
            }

            Vector2 screen = ResolvePointerScreenPosition();
            Ray ray = camera.ScreenPointToRay(screen);
            return bounds.IntersectRay(ray);
        }

        private void EnsureNoticeMark()
        {
            if (noticeMarkRoot != null)
            {
                return;
            }

            Debug.LogError(
                "[TrainingMonsterRoamController] noticeMarkRootが未配線ですHierarchyでRoamNoticeMarkを接続してください",
                this);
        }

        private void SetNoticeVisible(bool visible)
        {
            if (noticeMarkRoot == null)
            {
                return;
            }

            noticeMarkRoot.SetActive(visible);
        }

        private void UpdateNoticeMarkTransform()
        {
            if (noticeMarkRoot == null || trainingDisplay == null)
            {
                return;
            }

            if (!trainingDisplay.TryGetVisualBounds(out Bounds bounds))
            {
                GameObject model = trainingDisplay.LoadedModel;
                if (model == null)
                {
                    return;
                }

                noticeMarkRoot.transform.position =
                    model.transform.position
                    + Vector3.up * (1.8f + noticeHeightOffset + noticeAnimHeightBoost);
                return;
            }

            noticeMarkRoot.transform.position = new Vector3(
                bounds.center.x,
                bounds.max.y + noticeHeightOffset + noticeAnimHeightBoost,
                bounds.center.z);
        }

        private void BillboardNoticeMark()
        {
            UnityEngine.Camera camera = ResolveCamera();
            if (noticeMarkRoot == null || camera == null)
            {
                return;
            }

            noticeMarkRoot.transform.rotation = camera.transform.rotation;
        }

        private UnityEngine.Camera ResolveCamera()
        {
            if (roamCamera != null)
            {
                return roamCamera;
            }

            return UnityEngine.Camera.main;
        }

        private static bool WasPrimaryClickPressed()
        {
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                return true;
            }

            Touchscreen touch = Touchscreen.current;
            return touch != null
                && touch.primaryTouch.press.wasPressedThisFrame;
        }

        private static Vector2 ResolvePointerScreenPosition()
        {
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                return mouse.position.ReadValue();
            }

            Touchscreen touch = Touchscreen.current;
            if (touch != null)
            {
                return touch.primaryTouch.position.ReadValue();
            }

            return Vector2.zero;
        }

        private static bool IsPointerOverUi()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            if (eventSystem.IsPointerOverGameObject())
            {
                return true;
            }

            Touchscreen touch = Touchscreen.current;
            if (touch == null || !touch.primaryTouch.press.isPressed)
            {
                return false;
            }

            return eventSystem.IsPointerOverGameObject(touch.primaryTouch.touchId.ReadValue());
        }
    }
}
