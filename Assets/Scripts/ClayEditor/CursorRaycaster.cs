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
            float anchorDepth = ComputeDepth(camera, anchor.position);

            if (!lockDepth)
            {
                Ray ray = camera.ScreenPointToRay(screenPos);
                if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, mask))
                {
                    // メッシュに当たった場合深度を更新する
                    currentDepth = ComputeDepth(camera, hit.point);
                    return hit.point;
                }

                if (resetDepthOnMiss)
                {
                    currentDepth = anchorDepth;
                }
            }

            // 深度が未初期化ならanchorの深度を使う
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
            currentDepth = ComputeDepth(camera, worldPoint);
        }

        /// <summary>
        // カメラ前方ベクトルに対する深度（前方への射影距離）を計算する
        /// </summary>
        private static float ComputeDepth(UnityEngine.Camera camera, Vector3 worldPoint)
        {
            Vector3 toPoint = worldPoint - camera.transform.position;
            return Vector3.Dot(toPoint, camera.transform.forward);
        }
    }
}
