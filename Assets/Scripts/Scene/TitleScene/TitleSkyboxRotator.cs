using UnityEngine;

namespace Scene.TitleScene
{
    /// <summary>
    /// TitleのSkybox _Rotation を回して雲を流す
    /// マテリアル複製はしない
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class TitleSkyboxRotator : MonoBehaviour
    {
        private static readonly int RotationId = Shader.PropertyToID("_Rotation");

        [SerializeField]
        [Tooltip("1秒あたりの水平回転角度(度)")]
        private float degreesPerSecond = 6f;

        private Material skybox;
        private float baseRotation;
        private float angleOffset;
        private bool isActive;

        private void OnDisable()
        {
            // コンポーネント無効時は回転だけ止め値は戻さない
            // (戻すと空が飛び切り替わって見える)
            isActive = false;
        }

        private void LateUpdate()
        {
            if (!isActive)
            {
                return;
            }

            Material current = RenderSettings.skybox;
            if (current == null)
            {
                return;
            }

            // 参照差し替え時は現在値を基点にして飛びを抑える
            if (skybox != current)
            {
                CaptureBaseKeepVisual(current);
            }

            if (!skybox.HasProperty(RotationId))
            {
                return;
            }

            angleOffset = Mathf.Repeat(
                angleOffset + degreesPerSecond * Time.unscaledDeltaTime,
                360f);
            skybox.SetFloat(
                RotationId,
                Mathf.Repeat(baseRotation + angleOffset, 360f));
        }

        /// <summary>
        /// Skybox回転を開始する
        /// 既に動いている場合は何もしない
        /// </summary>
        public void Begin()
        {
            Material current = RenderSettings.skybox;
            if (current == null)
            {
                isActive = false;
                return;
            }

            if (isActive && skybox == current)
            {
                return;
            }

            CaptureBaseKeepVisual(current);
            isActive = true;
        }

        /// <summary>
        /// 回転を止める
        /// 見た目の飛躍を避けるため_Rotationは入場時へ戻さない
        /// </summary>
        public void End()
        {
            isActive = false;
            skybox = null;
            angleOffset = 0f;
        }

        private void CaptureBaseKeepVisual(Material material)
        {
            skybox = material;
            if (skybox.HasProperty(RotationId))
            {
                // いま見えている角度を基準にしゼロへ飛ばない
                baseRotation = skybox.GetFloat(RotationId);
            }
            else
            {
                baseRotation = 0f;
            }

            angleOffset = 0f;
        }
    }
}
