using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Scene.TrainingScene.View
{
    /// <summary>
    /// やる気アップ使用時のボール投げと跳ね回り演出
    /// </summary>
    public sealed class TrainingMotivationBallPlayController : MonoBehaviour
    {
        private const string SoccerBallResourcePath = "Field/Item/SoccerBall";
        private const string TennisBallResourcePath = "Field/Item/TennisBall";
        private const string TennisBallItemId = "motivation_tennis";
        private const float DefaultSoccerBallScale = 50f;
        private const float DefaultTennisBallScale = 28f;

        [Header("依存")]
        [SerializeField] private TrainingMonsterRoamController roamController;
        [SerializeField] private TrainingDisplay trainingDisplay;
        [Tooltip("未設定時はMainCamera")]
        [SerializeField] private UnityEngine.Camera throwCamera;
        [Tooltip("ボール表示ルート未設定時は実行時に生成する")]
        [SerializeField] private Transform ballRoot;

        [Header("構え")]
        [SerializeField] private float holdDistance = 1.35f;
        [SerializeField] private float holdHeightOffset = -0.12f;
        [SerializeField] private float ballScale = DefaultSoccerBallScale;
        [SerializeField] private float ballRadius = 25f;

        [Header("投げ")]
        [SerializeField] private float minThrowSpeed = 4.5f;
        [SerializeField] private float maxThrowSpeed = 11f;
        [SerializeField] private float maxChargeSeconds = 1.1f;
        [SerializeField] private float upwardThrowBias = 0.35f;
        [SerializeField] private float minDragThrowSpeed = 1.5f;

        [Header("跳ね")]
        [SerializeField] private float gravity = 18f;
        [SerializeField] private float bounceRestitution = 0.68f;
        [SerializeField] private float wallRestitution = 0.82f;
        [SerializeField] private float groundFriction = 0.988f;
        [SerializeField] private float airDrag = 0.02f;
        [SerializeField] private float minBounceSpeed = 0.35f;
        [Tooltip("モンスター接触またはこの秒数でやる気テキストを出す")]
        [SerializeField] private float playDurationSeconds = 8f;

        [Header("追いかけ遊び")]
        [Tooltip("追いついたときに弾く速さ")]
        [SerializeField] private float playKickSpeed = 7.5f;
        [Tooltip("弾くときの上方向成分")]
        [SerializeField] private float playKickUpward = 0.55f;
        [Tooltip("弾き方向のばらつき")]
        [SerializeField] private float playKickScatter = 0.45f;
        [Tooltip("連続で弾かないための間隔秒")]
        [SerializeField] private float playKickCooldownSeconds = 0.55f;

        private GameObject ballVisual;
        private string loadedBallResourcePath = string.Empty;
        private bool ownsBallRoot;
        private Vector3 velocity;
        private bool isInFlight;
        private bool isBallPersisted;
        private float nextPlayKickAllowedTime;
        private CancellationTokenSource lingerCts;

        /// <summary>
        /// アイテムIDからボール見た目を解決する
        /// </summary>
        /// <param name="itemId">商品ID</param>
        /// <param name="resourcePath">Resourcesパス</param>
        /// <param name="visualScale">表示スケール</param>
        public static void ResolveBallVisual(
            string itemId,
            out string resourcePath,
            out float visualScale)
        {
            if (itemId == TennisBallItemId)
            {
                resourcePath = TennisBallResourcePath;
                visualScale = DefaultTennisBallScale;
                return;
            }

            resourcePath = SoccerBallResourcePath;
            visualScale = DefaultSoccerBallScale;
        }

        private void Awake()
        {
            if (roamController == null)
            {
                roamController = GetComponent<TrainingMonsterRoamController>();
            }

            if (trainingDisplay == null)
            {
                trainingDisplay = GetComponent<TrainingDisplay>();
            }

            if (roamController == null)
            {
                Debug.LogError(
                    "[TrainingMotivationBallPlayController] roamControllerが未配線です",
                    this);
            }

            if (trainingDisplay == null)
            {
                Debug.LogError(
                    "[TrainingMotivationBallPlayController] trainingDisplayが未配線です",
                    this);
            }

            EnsureBallRoot();
            SetBallVisible(false);
        }

        private void OnDestroy()
        {
            CancelLinger();
            if (ownsBallRoot && ballRoot != null)
            {
                Destroy(ballRoot.gameObject);
            }
        }

        /// <summary>
        /// ターン経過などで残したボールを消す
        /// </summary>
        public void ClearBall()
        {
            CancelLinger();
            isInFlight = false;
            velocity = Vector3.zero;
            isBallPersisted = false;
            nextPlayKickAllowedTime = 0f;
            if (roamController != null)
            {
                roamController.StopChase();
            }

            SetBallVisible(false);
        }

        /// <summary>
        /// カメラ前で構え投げ跳ね回り追いかけを再生する
        /// 接触または制限時間で完了しボールはターン経過まで残す
        /// </summary>
        /// <param name="ballResourcePath">Resources上のボールprefabパス</param>
        /// <param name="visualScale">表示スケール</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        public async UniTask PlayAsync(
            string ballResourcePath,
            float visualScale,
            CancellationToken cancellationToken)
        {
            if (roamController == null)
            {
                Debug.LogError(
                    "[TrainingMotivationBallPlayController] roamControllerが未配線のため再生できません",
                    this);
                return;
            }

            ClearBall();
            ballScale = Mathf.Max(0.01f, visualScale);
            EnsureBallRoot();
            EnsureBallVisual(ballResourcePath);
            if (ballRoot == null || ballVisual == null)
            {
                return;
            }

            isInFlight = false;
            velocity = Vector3.zero;
            nextPlayKickAllowedTime = 0f;
            ApplyBallScale();
            SetBallVisible(true);

            try
            {
                await WaitThrowAsync(cancellationToken);
                roamController.StartChase(ballRoot);
                await SimulateUntilContactOrTimeoutAsync(cancellationToken);
                isBallPersisted = true;
                StartLingerPhysics();
            }
            catch (OperationCanceledException)
            {
                ClearBall();
                throw;
            }
        }

        private async UniTask WaitThrowAsync(CancellationToken cancellationToken)
        {
            // アイテム選択クリックの押しっぱなしを捨てる
            while (IsPrimaryPressed() && !cancellationToken.IsCancellationRequested)
            {
                PlaceBallInFrontOfCamera();
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            bool wasPressed = false;
            float chargeSeconds = 0f;
            Vector3 dragStartWorld = ballRoot != null ? ballRoot.position : Vector3.zero;
            Vector3 lastDragWorld = dragStartWorld;
            Vector3 smoothedDragVelocity = Vector3.zero;

            while (!cancellationToken.IsCancellationRequested)
            {
                ApplyBallScale();
                bool pressed = IsPrimaryPressed();
                if (pressed)
                {
                    if (!wasPressed)
                    {
                        smoothedDragVelocity = Vector3.zero;
                        if (TryGetPointerWorldOnHoldPlane(out Vector3 startWorld))
                        {
                            dragStartWorld = startWorld;
                            lastDragWorld = startWorld;
                            ballRoot.position = startWorld;
                        }
                        else
                        {
                            dragStartWorld = ballRoot.position;
                            lastDragWorld = dragStartWorld;
                        }
                    }

                    wasPressed = true;
                    chargeSeconds = Mathf.Min(
                        maxChargeSeconds,
                        chargeSeconds + Time.deltaTime);

                    if (TryGetPointerWorldOnHoldPlane(out Vector3 worldPos))
                    {
                        float dt = Mathf.Max(0.0001f, Time.deltaTime);
                        Vector3 frameVelocity = (worldPos - lastDragWorld) / dt;
                        smoothedDragVelocity = Vector3.Lerp(
                            smoothedDragVelocity,
                            frameVelocity,
                            0.35f);
                        lastDragWorld = worldPos;
                        ballRoot.position = worldPos;
                    }
                }
                else if (wasPressed)
                {
                    Vector3 swipeDelta = lastDragWorld - dragStartWorld;
                    ThrowBall(chargeSeconds, swipeDelta, smoothedDragVelocity);
                    return;
                }
                else
                {
                    PlaceBallInFrontOfCamera();
                }

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
        }

        private void ThrowBall(
            float chargeSeconds,
            Vector3 swipeDelta,
            Vector3 smoothedDragVelocity)
        {
            UnityEngine.Camera camera = ResolveCamera();
            if (camera == null || ballRoot == null)
            {
                return;
            }

            float chargeRatio = maxChargeSeconds <= 0.01f
                ? 1f
                : Mathf.Clamp01(chargeSeconds / maxChargeSeconds);
            float speed = Mathf.Lerp(minThrowSpeed, maxThrowSpeed, chargeRatio);

            Vector3 throwDirection;
            if (swipeDelta.sqrMagnitude >= 0.0004f)
            {
                // 押し始めから離すまでのマウス移動方向へ飛ばす
                throwDirection = swipeDelta.normalized;
                float swipeSpeed = swipeDelta.magnitude / Mathf.Max(0.05f, chargeSeconds);
                speed = Mathf.Max(speed, swipeSpeed * 0.55f);
            }
            else if (smoothedDragVelocity.sqrMagnitude >= minDragThrowSpeed * minDragThrowSpeed)
            {
                throwDirection = smoothedDragVelocity.normalized;
                speed = Mathf.Max(speed, smoothedDragVelocity.magnitude * 0.45f);
            }
            else
            {
                throwDirection = camera.transform.forward;
                throwDirection.y += upwardThrowBias;
                if (throwDirection.sqrMagnitude < 0.0001f)
                {
                    throwDirection = Vector3.forward;
                }

                throwDirection.Normalize();
            }

            speed = Mathf.Clamp(speed, minThrowSpeed, maxThrowSpeed * 1.5f);
            velocity = throwDirection * speed;
            isInFlight = true;
        }

        private async UniTask SimulateUntilContactOrTimeoutAsync(CancellationToken cancellationToken)
        {
            float elapsed = 0f;
            float duration = Mathf.Max(0.1f, playDurationSeconds);
            while (elapsed < duration && !cancellationToken.IsCancellationRequested)
            {
                if (isInFlight)
                {
                    StepPhysics(Time.deltaTime);
                }

                if (IsMonsterTouchingBall())
                {
                    TryKickBallForPlay();
                    return;
                }

                elapsed += Time.deltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
        }

        private void StartLingerPhysics()
        {
            CancelLinger();
            if (!isBallPersisted || ballRoot == null)
            {
                return;
            }

            lingerCts = new CancellationTokenSource();
            RunLingerPhysicsAsync(lingerCts.Token).Forget();
        }

        private async UniTaskVoid RunLingerPhysicsAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (isBallPersisted
                    && ballRoot != null
                    && !cancellationToken.IsCancellationRequested)
                {
                    if (isInFlight)
                    {
                        StepPhysics(Time.deltaTime);
                    }

                    TryKickBallForPlay();
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void TryKickBallForPlay()
        {
            if (!IsMonsterTouchingBall() || ballRoot == null || trainingDisplay == null)
            {
                return;
            }

            if (Time.time < nextPlayKickAllowedTime)
            {
                return;
            }

            GameObject model = trainingDisplay.LoadedModel;
            if (model == null)
            {
                return;
            }

            Vector3 fromMonster = ballRoot.position - model.transform.position;
            fromMonster.y = 0f;
            if (fromMonster.sqrMagnitude < 0.0001f)
            {
                float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                fromMonster = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            }

            Vector3 kickDirection = fromMonster.normalized;
            Vector3 scatter = UnityEngine.Random.insideUnitSphere * Mathf.Max(0f, playKickScatter);
            scatter.y = Mathf.Abs(scatter.y);
            kickDirection = (kickDirection + scatter).normalized;
            if (kickDirection.sqrMagnitude < 0.0001f)
            {
                kickDirection = Vector3.forward;
            }

            kickDirection.y = Mathf.Max(kickDirection.y, Mathf.Max(0.15f, playKickUpward));
            kickDirection.Normalize();

            float speed = Mathf.Max(1f, playKickSpeed);
            velocity = kickDirection * speed;
            isInFlight = true;
            nextPlayKickAllowedTime = Time.time + Mathf.Max(0.1f, playKickCooldownSeconds);

            if (roamController != null)
            {
                roamController.StartChase(ballRoot);
            }
        }

        private void CancelLinger()
        {
            if (lingerCts == null)
            {
                return;
            }

            lingerCts.Cancel();
            lingerCts.Dispose();
            lingerCts = null;
        }

        private bool IsMonsterTouchingBall()
        {
            if (ballRoot == null || trainingDisplay == null)
            {
                return false;
            }

            GameObject model = trainingDisplay.LoadedModel;
            if (model == null)
            {
                return false;
            }

            float touchDistance = ResolveBallRadius();
            float sqr = (model.transform.position - ballRoot.position).sqrMagnitude;
            return sqr <= touchDistance * touchDistance;
        }

        private void StepPhysics(float deltaTime)
        {
            if (ballRoot == null || roamController == null)
            {
                return;
            }

            ApplyBallScale();
            float groundY = roamController.ResolveGroundY(ballRoot.position) + ResolveBallRadius();
            velocity.y -= gravity * deltaTime;
            velocity *= Mathf.Clamp01(1f - airDrag * deltaTime);

            Vector3 next = ballRoot.position + velocity * deltaTime;
            next = ReflectAgainstRoamBounds(next, ref velocity);
            if (next.y <= groundY)
            {
                next.y = groundY;
                if (velocity.y < 0f)
                {
                    velocity.y = -velocity.y * bounceRestitution;
                    if (Mathf.Abs(velocity.y) < minBounceSpeed)
                    {
                        velocity.y = 0f;
                    }
                }

                velocity.x *= groundFriction;
                velocity.z *= groundFriction;
            }

            ballRoot.position = next;
            if (velocity.sqrMagnitude > 0.0001f)
            {
                ballRoot.Rotate(
                    velocity.normalized,
                    velocity.magnitude * 120f * deltaTime,
                    Space.World);
            }
        }

        private Vector3 ReflectAgainstRoamBounds(Vector3 next, ref Vector3 currentVelocity)
        {
            if (!roamController.TryGetRoamPlanarBounds(out Vector3 center, out Vector2 halfExtents))
            {
                return next;
            }

            float minX = center.x - halfExtents.x;
            float maxX = center.x + halfExtents.x;
            float minZ = center.z - halfExtents.y;
            float maxZ = center.z + halfExtents.y;

            if (next.x < minX)
            {
                next.x = minX;
                currentVelocity.x = Mathf.Abs(currentVelocity.x) * wallRestitution;
            }
            else if (next.x > maxX)
            {
                next.x = maxX;
                currentVelocity.x = -Mathf.Abs(currentVelocity.x) * wallRestitution;
            }

            if (next.z < minZ)
            {
                next.z = minZ;
                currentVelocity.z = Mathf.Abs(currentVelocity.z) * wallRestitution;
            }
            else if (next.z > maxZ)
            {
                next.z = maxZ;
                currentVelocity.z = -Mathf.Abs(currentVelocity.z) * wallRestitution;
            }

            return next;
        }

        private void PlaceBallInFrontOfCamera()
        {
            UnityEngine.Camera camera = ResolveCamera();
            if (camera == null || ballRoot == null)
            {
                return;
            }

            Transform cameraTransform = camera.transform;
            ballRoot.position = cameraTransform.position
                + cameraTransform.forward * holdDistance
                + Vector3.up * holdHeightOffset;
            ballRoot.rotation = Quaternion.LookRotation(cameraTransform.forward, Vector3.up);
            ApplyBallScale();
        }

        private bool TryGetPointerWorldOnHoldPlane(out Vector3 worldPos)
        {
            worldPos = default;
            UnityEngine.Camera camera = ResolveCamera();
            if (camera == null || ballRoot == null)
            {
                return false;
            }

            if (!TryGetPointerScreenPosition(out Vector2 screenPosition))
            {
                return false;
            }

            Transform cameraTransform = camera.transform;
            Vector3 planePoint = cameraTransform.position
                + cameraTransform.forward * holdDistance
                + Vector3.up * holdHeightOffset;
            var plane = new Plane(-cameraTransform.forward, planePoint);
            Ray ray = camera.ScreenPointToRay(screenPosition);
            if (!plane.Raycast(ray, out float enter))
            {
                return false;
            }

            worldPos = ray.GetPoint(enter);
            return true;
        }

        private void EnsureBallRoot()
        {
            if (ballRoot != null)
            {
                return;
            }

            GameObject rootObject = new GameObject("MotivationBall");
            rootObject.transform.SetParent(null, false);
            ballRoot = rootObject.transform;
            ownsBallRoot = true;
        }

        private void EnsureBallVisual(string ballResourcePath)
        {
            if (ballRoot == null)
            {
                return;
            }

            string resourcePath = string.IsNullOrEmpty(ballResourcePath)
                ? SoccerBallResourcePath
                : ballResourcePath;
            if (ballVisual != null && loadedBallResourcePath == resourcePath)
            {
                ApplyBallScale();
                return;
            }

            ClearBallVisual();

            GameObject prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab == null)
            {
                Debug.LogError(
                    $"[TrainingMotivationBallPlayController] Resources/{resourcePath}が見つかりません",
                    this);
                return;
            }

            ballVisual = Instantiate(prefab, ballRoot);
            ballVisual.name = resourcePath == TennisBallResourcePath
                ? "TennisBallVisual"
                : "SoccerBallVisual";
            ballVisual.transform.localPosition = Vector3.zero;
            ballVisual.transform.localRotation = Quaternion.identity;
            loadedBallResourcePath = resourcePath;
            StripImportedCameras(ballVisual);
            ApplyBallScale();
        }

        private void ClearBallVisual()
        {
            if (ballVisual != null)
            {
                Destroy(ballVisual);
                ballVisual = null;
            }

            loadedBallResourcePath = string.Empty;
            if (ballRoot == null)
            {
                return;
            }

            for (int i = ballRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(ballRoot.GetChild(i).gameObject);
            }
        }

        private void ApplyBallScale()
        {
            if (ballVisual == null)
            {
                return;
            }

            float scale = Mathf.Max(0.01f, ballScale);
            Vector3 target = Vector3.one * scale;
            if ((ballVisual.transform.localScale - target).sqrMagnitude > 0.000001f)
            {
                ballVisual.transform.localScale = target;
            }
        }

        private float ResolveBallRadius()
        {
            if (ballVisual != null)
            {
                Renderer renderer = ballVisual.GetComponentInChildren<Renderer>();
                if (renderer != null)
                {
                    Bounds bounds = renderer.bounds;
                    return Mathf.Max(0.05f, bounds.extents.magnitude);
                }
            }

            return Mathf.Max(0.05f, ballRadius);
        }

        private static void StripImportedCameras(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            UnityEngine.Camera[] cameras = root.GetComponentsInChildren<UnityEngine.Camera>(true);
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] != null)
                {
                    cameras[i].enabled = false;
                    cameras[i].gameObject.SetActive(false);
                }
            }
        }

        private void SetBallVisible(bool visible)
        {
            if (ballRoot == null)
            {
                return;
            }

            if (ballRoot.gameObject.activeSelf != visible)
            {
                ballRoot.gameObject.SetActive(visible);
            }
        }

        private UnityEngine.Camera ResolveCamera()
        {
            if (throwCamera != null)
            {
                return throwCamera;
            }

            return UnityEngine.Camera.main;
        }

        private static bool IsPrimaryPressed()
        {
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed)
            {
                return true;
            }

            Touchscreen touch = Touchscreen.current;
            return touch != null
                && touch.primaryTouch.press.isPressed;
        }

        private static bool TryGetPointerScreenPosition(out Vector2 screenPosition)
        {
            screenPosition = default;
            Touchscreen touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.isPressed)
            {
                screenPosition = touch.primaryTouch.position.ReadValue();
                return true;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return false;
            }

            screenPosition = mouse.position.ReadValue();
            return true;
        }
    }
}
