using Battle;
using Camera.Interface;
using UnityEngine;

namespace Scene.TitleScene
{
    /// <summary>
    /// Titleシーンのカメラ構図を決める
    /// 水平角・垂直角はInspectorで設定し距離はモデルサイズに応じて自動調整する
    /// </summary>
    public sealed class TitleSceneCamera : MonoBehaviour
    {
        [Header("注視点")]
        [SerializeField] private float focusHeightOffset = 0.85f;
        [Tooltip("モデル中心からカメラ画面上の左右へずらす距離。正で右寄りの空間を注視する")]
        [SerializeField] private float focusSideOffset;

        [Header("オービット")]
        [SerializeField] private float horizontalAngle = 122f;
        [SerializeField] private float verticalAngle = 18f;
        [SerializeField] private float distance = 8f;

        [Header("モデルサイズ連動")]
        [Tooltip("ONのとき表示モデルの境界に合わせてオービット距離を調整する")]
        [SerializeField] private bool autoFrameFromModelSize = true;
        [Tooltip("境界計算時の余白係数。大きいほどカメラが遠ざかる")]
        [SerializeField] private float framePadding = 1.12f;
        [SerializeField] private float minDistance = 5f;
        [SerializeField] private float maxDistance = 14f;
        [Tooltip("距離計算に使う垂直視野角。MainCamera未設定時に使用する")]
        [SerializeField] private float verticalFovDegrees = 46f;

        [Header("モデルの向き")]
        [Tooltip("ONのとき窓と反対方向へモデルを向ける")]
        [SerializeField] private bool rotateModelAwayFromWindows;
        [SerializeField] private Transform windowsReference;

        /// <summary>
        /// 注視点の高さ補正
        /// </summary>
        public float FocusHeightOffset => focusHeightOffset;

        /// <summary>
        /// モデル向き用の窓参照
        /// </summary>
        public Transform WindowsReference => windowsReference;

        /// <summary>
        /// モデルを窓と反対方向へ向けるか
        /// </summary>
        public bool RotateModelAwayFromWindows => rotateModelAwayFromWindows;

        /// <summary>
        /// モデル中心を基準にタイトル用カメラ構図を適用する
        /// </summary>
        public void ApplyView(IClayEditCameraView cameraView, Vector3 modelCenter)
        {
            ApplyView(cameraView, modelCenter, null);
        }

        /// <summary>
        /// モデル中心と境界を基準にタイトル用カメラ構図を適用する
        /// </summary>
        public void ApplyView(IClayEditCameraView cameraView, Vector3 modelCenter, Bounds? modelBounds)
        {
            Vector3 focus = modelCenter + Vector3.up * focusHeightOffset;
            if (!Mathf.Approximately(focusSideOffset, 0f))
            {
                Vector3 screenRight = BattleFieldScreenAxis.ResolveScreenRight(horizontalAngle);
                focus += screenRight * focusSideOffset;
            }

            float orbitDistance = ResolveOrbitDistance(modelBounds);
            cameraView.SetFocusPosition(focus);
            cameraView.SetOrbitView(horizontalAngle, verticalAngle, orbitDistance);
            cameraView.SetCameraEnable(true);
            cameraView.SetCameraOperatable(false);
        }

        private float ResolveOrbitDistance(Bounds? modelBounds)
        {
            if (!autoFrameFromModelSize || !modelBounds.HasValue)
            {
                return distance;
            }

            Bounds bounds = ExpandBoundsForObliqueView(modelBounds.Value);
            UnityEngine.Camera mainCamera = UnityEngine.Camera.main;
            float verticalFov = mainCamera != null ? mainCamera.fieldOfView : verticalFovDegrees;
            float aspect = mainCamera != null ? mainCamera.aspect : (float)Screen.width / Mathf.Max(1, Screen.height);
            float framedDistance = BattleFieldFocusResolver.ResolveOrbitDistanceForBounds(
                bounds,
                verticalFov,
                aspect,
                framePadding,
                minDistance,
                maxDistance);

            return Mathf.Clamp(Mathf.Max(distance, framedDistance), minDistance, maxDistance);
        }

        private static Bounds ExpandBoundsForObliqueView(Bounds bounds)
        {
            Vector3 size = bounds.size;
            float horizontalSpan = Mathf.Max(size.x, size.z);
            size.x = horizontalSpan;
            size.z = horizontalSpan;
            return new Bounds(bounds.center, size);
        }
    }
}
