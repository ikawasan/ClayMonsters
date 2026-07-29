using Cysharp.Threading.Tasks;
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

        [Header("依存")]
        [SerializeField] private TrainingMonsterRoamController roamController;
        [Tooltip("未設定時はMainCamera")]
        [SerializeField] private UnityEngine.Camera throwCamera;
        [Tooltip("ボール表示ルート未設定時は実行時に生成する")]
        [SerializeField] private Transform ballRoot;

        [Header("構え")]
        [SerializeField] private float holdDistance = 1.35f;
        [SerializeField] private float holdHeightOffset = -0.12f;
        [SerializeField] private float ballScale = 50f;
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
        [SerializeField] private float playDurationSeconds = 8f;

        private GameObject ballVisual;
        private bool ownsBallRoot;
        private Vector3 velocity;
        private bool isInFlight;

        private void Awake()
        {
            if (roamController == null)
            {
                roamController = GetComponent<TrainingMonsterRoamController>();
            }

            if (roamController == null)
            {
                Debug.LogError(
                    "[TrainingMotivationBallPlayController] roamControllerが未配線です",
                    this);
            }

            EnsureBallRoot();
            SetBallVisible(false);
        }

        private void OnDestroy()
        {
            if (ownsBallRoot && ballRoot != null)
            {
                Destroy(ballRoot.gameObject);
            }
        }

        /// <summary>
        /// カメラ前で構え投げ跳ね回り追いかけを再生する
        /// </summary>
        /// <param name="cancellationToken">キャンセルトークン</param>
        public async UniTask PlayAsync(CancellationToken cancellationToken)
        {
            if (roamController == null)
            {
                Debug.LogError(
                    "[TrainingMotivationBallPlayController] roamControllerが未配線のため再生できません",
                    this);
                return;
            }

            EnsureBallRoot();
            EnsureBallVisual();
            if (ballRoot == null || ballVisual == null)
            {
                return;
            }

            isInFlight = false;
            velocity = Vector3.zero;
            ApplyBallScale();
            SetBallVisible(true);

            try
            {
                await WaitThrowAsync(cancellationToken);
                roamController.StartChase(ballRoot);
                await SimulateFlightAsync(cancellationToken);
            }
            finally
            {
                isInFlight = false;
                roamController.StopChase();
                SetBallVisible(false);
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

        private async UniTask SimulateFlightAsync(CancellationToken cancellationToken)
        {
            float elapsed = 0f;
            float duration = Mathf.Max(1f, playDurationSeconds);
            while (elapsed < duration && !cancellationToken.IsCancellationRequested)
            {
                if (isInFlight)
                {
                    StepPhysics(Time.deltaTime);
                }

                elapsed += Time.deltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
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

            ApplyBallScale();
            Transform cameraTransform = camera.transform;
            ballRoot.position = cameraTransform.position
                + cameraTransform.forward * holdDistance
                + cameraTransform.up * holdHeightOffset;
            ballRoot.rotation = Quaternion.LookRotation(cameraTransform.forward, Vector3.up);
        }

        private bool TryGetPointerWorldOnHoldPlane(out Vector3 worldPosition)
        {
            worldPosition = default;
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
                + cameraTransform.up * holdHeightOffset;
            Plane holdPlane = new Plane(-cameraTransform.forward, planePoint);
            Ray ray = camera.ScreenPointToRay(screenPosition);
            if (!holdPlane.Raycast(ray, out float enter))
            {
                return false;
            }

            worldPosition = ray.GetPoint(enter);
            return true;
        }

        private void EnsureBallRoot()
        {
            if (ballRoot != null)
            {
                return;
            }

            GameObject rootObject = new GameObject("MotivationBall");
            rootObject.transform.SetParent(transform, false);
            ballRoot = rootObject.transform;
            ownsBallRoot = true;
        }

        private void EnsureBallVisual()
        {
            if (ballRoot == null)
            {
                return;
            }

            if (ballVisual != null)
            {
                ApplyBallScale();
                return;
            }

            if (ballRoot.childCount > 0)
            {
                ballVisual = ballRoot.GetChild(0).gameObject;
                ApplyBallScale();
                StripImportedCameras(ballVisual);
                return;
            }

            GameObject prefab = Resources.Load<GameObject>(SoccerBallResourcePath);
            if (prefab == null)
            {
                Debug.LogError(
                    $"[TrainingMotivationBallPlayController] Resources/{SoccerBallResourcePath}が見つかりません",
                    this);
                return;
            }

            ballVisual = Instantiate(prefab, ballRoot);
            ballVisual.name = "SoccerBallVisual";
            ballVisual.transform.localPosition = Vector3.zero;
            ballVisual.transform.localRotation = Quaternion.identity;
            ApplyBallScale();
            StripImportedCameras(ballVisual);
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
                    return Mathf.Max(0.05f, bounds.extents.y);
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

            // FBX付属カメラがMainCameraを乗っ取り投げ時に視点が動くのを防ぐ
            UnityEngine.Camera[] cameras = root.GetComponentsInChildren<UnityEngine.Camera>(true);
            for (int i = 0; i < cameras.Length; i++)
            {
                UnityEngine.Camera importedCamera = cameras[i];
                if (importedCamera == null)
                {
                    continue;
                }

                importedCamera.enabled = false;
                importedCamera.gameObject.SetActive(false);
            }

            AudioListener[] listeners = root.GetComponentsInChildren<AudioListener>(true);
            for (int i = 0; i < listeners.Length; i++)
            {
                AudioListener listener = listeners[i];
                if (listener == null)
                {
                    continue;
                }

                listener.enabled = false;
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
