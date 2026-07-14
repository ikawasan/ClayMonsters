using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 戦闘カメラの水平角から画面上の左右方向を求める
    /// </summary>
    public static class BattleFieldScreenAxis
    {
        /// <summary>
        /// 水平オービット角から画面上の右方向を返す
        /// </summary>
        public static Vector3 ResolveScreenRight(float horizontalAngleDegrees)
        {
            Quaternion orbit = Quaternion.Euler(0f, horizontalAngleDegrees, 0f);
            Vector3 cameraForward = orbit * Vector3.forward;
            cameraForward.y = 0f;

            if (cameraForward.sqrMagnitude < 1e-6f)
            {
                return Vector3.right;
            }

            cameraForward.Normalize();
            return Vector3.Cross(Vector3.up, cameraForward).normalized;
        }

        /// <summary>
        /// 水平オービット角からカメラの正面方向を返す
        /// </summary>
        public static Vector3 ResolveCameraForward(float horizontalAngleDegrees)
        {
            Quaternion orbit = Quaternion.Euler(0f, horizontalAngleDegrees, 0f);
            Vector3 cameraForward = orbit * Vector3.forward;
            cameraForward.y = 0f;

            if (cameraForward.sqrMagnitude < 1e-6f)
            {
                return Vector3.forward;
            }

            cameraForward.Normalize();
            return cameraForward;
        }

        /// <summary>
        /// 水平オービット角からカメラ位置へ向く水平方向を返す
        /// </summary>
        public static Vector3 ResolveTowardCamera(float horizontalAngleDegrees)
        {
            return -ResolveCameraForward(horizontalAngleDegrees);
        }

        /// <summary>
        /// 水平オービット角から画面上の左方向を返す
        /// </summary>
        public static Vector3 ResolveScreenLeft(float horizontalAngleDegrees)
        {
            return -ResolveScreenRight(horizontalAngleDegrees);
        }
    }
}
