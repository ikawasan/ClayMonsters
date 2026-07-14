using UnityEngine;

namespace ClayEditor
{
    /// <summary>
    /// スクリーン座標からブラシのワールド座標を求める共通ヘルパー
    /// レイがメッシュに当たればその点を、外れた場合は保持している深度に投影した点を返す
    /// 深度を内部に保持するため、カーソルごとにインスタンスを生成して使う
    /// </summary>
    public sealed class CursorRaycaster
    {
        private const int RaycastHitBufferSize = 32;
        private static readonly RaycastHit[] RaycastHitBuffer = new RaycastHit[RaycastHitBufferSize];

        private float currentDepth;

        /// <summary>
        /// スクリーン座標に対応するワールド座標を解決する
        /// </summary>
        /// <param name="camera">対象カメラ</param>
        /// <param name="anchor">深度の基準となる対象</param>
        /// <param name="screenPos">ポインタのスクリーン座標</param>
        /// <param name="mask">レイキャスト対象のレイヤー</param>
        /// <param name="lockDepth">true の場合はレイキャストせず保持深度に投影する</param>
        /// <param name="resetDepthOnMiss">レイが外れたときに深度をanchorの深度へ戻すか</param>
        /// <returns>解決されたワールド座標。</returns>
        public Vector3 Resolve(UnityEngine.Camera camera, Transform anchor, Vector2 screenPos, LayerMask mask, bool lockDepth, bool resetDepthOnMiss)
        {
            return Resolve(camera, anchor, screenPos, mask, lockDepth, resetDepthOnMiss, out _, out _);
        }

        /// <summary>
        /// スクリーン座標に対応するワールド座標を解決する
        /// </summary>
        /// <param name="camera">対象カメラ</param>
        /// <param name="anchor">深度の基準となる対象</param>
        /// <param name="screenPos">ポインタのスクリーン座標</param>
        /// <param name="mask">レイキャスト対象のレイヤー</param>
        /// <param name="lockDepth">true の場合はレイキャストせず保持深度に投影する</param>
        /// <param name="resetDepthOnMiss">レイが外れたときに深度をanchorの深度へ戻すか</param>
        /// <param name="surfaceHit">カメラ側の表面ヒット情報</param>
        /// <param name="hasSurfaceHit">表面ヒットがあったか</param>
        /// <returns>解決されたワールド座標。</returns>
        public Vector3 Resolve(
            UnityEngine.Camera camera,
            Transform anchor,
            Vector2 screenPos,
            LayerMask mask,
            bool lockDepth,
            bool resetDepthOnMiss,
            out RaycastHit surfaceHit,
            out bool hasSurfaceHit)
        {
            surfaceHit = default;
            hasSurfaceHit = false;
            Ray ray = camera.ScreenPointToRay(screenPos);
            float anchorDepth = ComputeDepthAlongRay(ray, anchor.position);

            if (!lockDepth)
            {
                if (TryRaycastFrontSurface(ray, mask, anchor, out surfaceHit))
                {
                    hasSurfaceHit = true;
                    currentDepth = ComputeDepthAlongRay(ray, surfaceHit.point);
                    return surfaceHit.point;
                }

                if (resetDepthOnMiss)
                {
                    currentDepth = anchorDepth;
                }
            }

            if (currentDepth <= 0f)
            {
                currentDepth = anchorDepth;
            }

            Vector3 screenWithDepth = new Vector3(screenPos.x, screenPos.y, currentDepth);
            return camera.ScreenToWorldPoint(screenWithDepth);
        }

        /// <summary>
        /// 初期深度を指定ワールド座標を基準に設定する
        /// </summary>
        /// <param name="camera">対象カメラ</param>
        /// <param name="worldPoint">基準とするワールド座標</param>
        public void InitializeDepth(UnityEngine.Camera camera, Vector3 worldPoint)
        {
            Ray ray = camera.ScreenPointToRay(camera.WorldToScreenPoint(worldPoint));
            currentDepth = ComputeDepthAlongRay(ray, worldPoint);
        }

        /// <summary>
        /// ヒット点から深度を更新する
        /// </summary>
        /// <param name="camera">対象カメラ</param>
        /// <param name="screenPos">ポインタのスクリーン座標</param>
        /// <param name="worldPoint">ヒットしたワールド座標</param>
        public void RecordDepth(UnityEngine.Camera camera, Vector2 screenPos, Vector3 worldPoint)
        {
            Ray ray = camera.ScreenPointToRay(screenPos);
            currentDepth = ComputeDepthAlongRay(ray, worldPoint);
        }

        /// <summary>
        /// カメラに最も近い表面をヒットさせる
        /// </summary>
        /// <param name="ray">スクリーンから飛ばすレイ</param>
        /// <param name="mask">レイキャスト対象のレイヤー</param>
        /// <param name="surfaceRoot">指定時はこのTransform配下のコライダーのみ対象</param>
        /// <param name="hit">補正済みヒット情報</param>
        /// <returns>ヒットした場合true</returns>
        public static bool TryRaycastFrontSurface(Ray ray, LayerMask mask, Transform surfaceRoot, out RaycastHit hit)
        {
            hit = default;

            bool previousBackfaceQuery = Physics.queriesHitBackfaces;
            Physics.queriesHitBackfaces = true;

            try
            {
                int hitCount = Physics.RaycastNonAlloc(
                    ray,
                    RaycastHitBuffer,
                    Mathf.Infinity,
                    mask,
                    QueryTriggerInteraction.Ignore);
                if (hitCount <= 0)
                {
                    return false;
                }

                int closestIndex = -1;
                float closestDistance = float.MaxValue;
                for (int i = 0; i < hitCount; i++)
                {
                    if (surfaceRoot != null && !IsUnderTransform(RaycastHitBuffer[i].collider.transform, surfaceRoot))
                    {
                        continue;
                    }

                    if (RaycastHitBuffer[i].distance < closestDistance)
                    {
                        closestDistance = RaycastHitBuffer[i].distance;
                        closestIndex = i;
                    }
                }

                if (closestIndex < 0)
                {
                    return false;
                }

                hit = RaycastHitBuffer[closestIndex];
            }
            finally
            {
                Physics.queriesHitBackfaces = previousBackfaceQuery;
            }

            OrientNormalTowardCamera(ref hit, ray.origin);
            return true;
        }

        private static bool IsUnderTransform(Transform target, Transform root)
        {
            Transform current = target;
            while (current != null)
            {
                if (current == root)
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static void OrientNormalTowardCamera(ref RaycastHit hit, Vector3 cameraPosition)
        {
            Vector3 toCamera = cameraPosition - hit.point;
            if (toCamera.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            if (Vector3.Dot(hit.normal, toCamera) < 0f)
            {
                hit.normal = -hit.normal;
            }
        }

        private static float ComputeDepthAlongRay(Ray ray, Vector3 worldPoint)
        {
            Vector3 toPoint = worldPoint - ray.origin;
            return Vector3.Dot(toPoint, ray.direction);
        }
    }
}
