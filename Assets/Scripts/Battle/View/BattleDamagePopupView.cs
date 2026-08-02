using Battle.Interface;
using Extensions;
using TMPro;
using UnityEngine;

namespace Battle.View
{
    /// <summary>
    /// 攻撃命中時にワールド空間へダメージ数値を浮かべて表示する
    /// 外れたときはミスラベルを表示する
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleDamagePopupView : MonoBehaviour, IBattleDamagePopup
    {
        [SerializeField] private float spawnHeightOffset = 0.45f;
        [SerializeField] private float floatDistance = 1.15f;
        [SerializeField] private float duration = 0.9f;
        [SerializeField] private float normalFontSize = 4.8f;
        [SerializeField] private float heavyFontSize = 6.4f;
        [SerializeField] private Color normalColor = new Color(1f, 0.95f, 0.45f, 1f);
        [SerializeField] private Color partBreakColor = new Color(1f, 0.42f, 0.12f, 1f);
        [SerializeField] private Color knockoutColor = new Color(1f, 0.2f, 0.18f, 1f);
        [SerializeField] private float missFontSize = 4.2f;
        [SerializeField] private Color missColor = new Color(0.78f, 0.82f, 0.92f, 1f);

        private UnityEngine.Camera worldCamera;

        /// <summary>
        /// ラベル向き合わせに使うカメラを設定する
        /// </summary>
        public void SetWorldCamera(UnityEngine.Camera camera)
        {
            worldCamera = camera;
        }

        /// <inheritdoc/>
        public void PlayMiss(Vector3 worldPosition)
        {
            SpawnFloatingLabel(
                worldPosition,
                Localization.LocalizedText.Get(Localization.GameTextKeys.BattleMiss),
                missColor,
                missFontSize,
                "MissPopup");
        }

        /// <inheritdoc/>
        public void PlayDamage(Vector3 worldPosition, int damage, bool isPartBreak, bool isKnockout)
        {
            if (damage <= 0)
            {
                return;
            }

            bool heavy = isPartBreak || isKnockout;
            Color color = isKnockout ? knockoutColor : isPartBreak ? partBreakColor : normalColor;
            float fontSize = heavy ? heavyFontSize : normalFontSize;
            string label = heavy ? $"-{damage}!" : $"-{damage}";
            SpawnFloatingLabel(
                worldPosition,
                label,
                color,
                fontSize,
                heavy ? "DamagePopupHeavy" : "DamagePopup");
        }

        private void SpawnFloatingLabel(
            Vector3 worldPosition,
            string label,
            Color color,
            float fontSize,
            string objectName)
        {
            Vector3 position = worldPosition + Vector3.up * spawnHeightOffset;

            GameObject host = new GameObject(objectName);
            host.transform.position = position;

            TextMeshPro labelText = host.AddComponent<TextMeshPro>();
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.fontSize = fontSize;
            labelText.fontStyle = FontStyles.Bold;
            labelText.color = color;
            labelText.text = label;
            labelText.sortingOrder = 500;
            AppTmpFontUtility.ApplyDefaultFont(labelText);
            AppTmpFontUtility.ApplyOutline(
                labelText,
                0.22f,
                new Color(0.12f, 0.02f, 0.02f, 0.95f));

            MeshRenderer meshRenderer = labelText.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.sortingOrder = 500;
            }

            var floater = host.AddComponent<DamagePopupFloater>();
            floater.Configure(duration, floatDistance, color, worldCamera);
        }

        private sealed class DamagePopupFloater : MonoBehaviour
        {
            private float lifetime;
            private float riseDistance;
            private Color baseColor;
            private TextMeshPro labelText;
            private Vector3 startPosition;
            private float elapsed;
            private UnityEngine.Camera targetCamera;

            public void Configure(float duration, float rise, Color color, UnityEngine.Camera camera)
            {
                lifetime = Mathf.Max(0.1f, duration);
                riseDistance = rise;
                baseColor = color;
                labelText = GetComponent<TextMeshPro>();
                startPosition = transform.position;
                targetCamera = camera != null ? camera : UnityEngine.Camera.main;
            }

            private void LateUpdate()
            {
                elapsed += GameplayTime.PresentationDeltaTime;
                float normalized = Mathf.Clamp01(elapsed / lifetime);
                float eased = 1f - (1f - normalized) * (1f - normalized);

                transform.position = startPosition + Vector3.up * (riseDistance * eased);

                if (targetCamera != null)
                {
                    Vector3 forward = targetCamera.transform.rotation * Vector3.forward;
                    Vector3 up = targetCamera.transform.rotation * Vector3.up;
                    transform.rotation = Quaternion.LookRotation(forward, up);
                }

                if (labelText != null)
                {
                    float alpha = normalized < 0.55f ? 1f : Mathf.Lerp(1f, 0f, (normalized - 0.55f) / 0.45f);
                    Color color = baseColor;
                    color.a = alpha;
                    labelText.color = color;

                    Color outline = labelText.outlineColor;
                    outline.a = alpha;
                    labelText.outlineColor = outline;
                }

                if (normalized >= 1f)
                {
                    Destroy(gameObject);
                }
            }
        }
    }
}
