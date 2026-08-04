using System.Collections.Generic;
using System.Text;
using Battle.Interface;
using Extensions;
using TMPro;
using UnityEngine;

namespace Battle.View
{
    /// <summary>
    /// 攻撃命中時にワールド空間へダメージ数値を浮かべて表示する
    /// 外れたときはミスラベルを表示する
    /// インスタンスをプールして生成と破棄のGCを抑える
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
        private readonly Stack<DamagePopupFloater> freeFloaters = new Stack<DamagePopupFloater>(8);
        private readonly StringBuilder labelBuilder = new StringBuilder(12);
        private Transform poolRoot;

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
                missFontSize);
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
            labelBuilder.Clear();
            labelBuilder.Append('-');
            labelBuilder.Append(damage);
            if (heavy)
            {
                labelBuilder.Append('!');
            }

            SpawnFloatingLabel(worldPosition, labelBuilder.ToString(), color, fontSize);
        }

        private void SpawnFloatingLabel(
            Vector3 worldPosition,
            string label,
            Color color,
            float fontSize)
        {
            Vector3 position = worldPosition + Vector3.up * spawnHeightOffset;
            DamagePopupFloater floater = RentFloater();
            floater.Play(
                position,
                label,
                color,
                fontSize,
                duration,
                floatDistance,
                worldCamera,
                ReleaseFloater);
        }

        private DamagePopupFloater RentFloater()
        {
            if (freeFloaters.Count > 0)
            {
                DamagePopupFloater recycled = freeFloaters.Pop();
                recycled.gameObject.SetActive(true);
                return recycled;
            }

            EnsurePoolRoot();
            var host = new GameObject("DamagePopup");
            host.transform.SetParent(null, false);
            TextMeshPro labelText = host.AddComponent<TextMeshPro>();
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.fontStyle = FontStyles.Bold;
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

            return host.AddComponent<DamagePopupFloater>();
        }

        private void ReleaseFloater(DamagePopupFloater floater)
        {
            if (floater == null)
            {
                return;
            }

            EnsurePoolRoot();
            floater.gameObject.SetActive(false);
            floater.transform.SetParent(poolRoot, false);
            freeFloaters.Push(floater);
        }

        private void EnsurePoolRoot()
        {
            if (poolRoot != null)
            {
                return;
            }

            var root = new GameObject("BattleDamagePopupPool")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            Object.DontDestroyOnLoad(root);
            poolRoot = root.transform;
            poolRoot.position = new Vector3(0f, -10000f, 0f);
        }

        private void OnDestroy()
        {
            while (freeFloaters.Count > 0)
            {
                DamagePopupFloater floater = freeFloaters.Pop();
                if (floater != null)
                {
                    Destroy(floater.gameObject);
                }
            }

            if (poolRoot != null)
            {
                Destroy(poolRoot.gameObject);
                poolRoot = null;
            }
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
            private System.Action<DamagePopupFloater> onComplete;

            public void Play(
                Vector3 worldPosition,
                string label,
                Color color,
                float fontSize,
                float durationSeconds,
                float rise,
                UnityEngine.Camera camera,
                System.Action<DamagePopupFloater> complete)
            {
                if (labelText == null)
                {
                    labelText = GetComponent<TextMeshPro>();
                }

                transform.SetParent(null, false);
                transform.position = worldPosition;
                startPosition = worldPosition;
                lifetime = Mathf.Max(0.1f, durationSeconds);
                riseDistance = rise;
                baseColor = color;
                elapsed = 0f;
                targetCamera = camera != null ? camera : UnityEngine.Camera.main;
                onComplete = complete;

                if (labelText != null)
                {
                    labelText.fontSize = fontSize;
                    labelText.color = color;
                    labelText.text = label;
                    Color outline = labelText.outlineColor;
                    outline.a = 1f;
                    labelText.outlineColor = outline;
                }

                enabled = true;
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
                    enabled = false;
                    System.Action<DamagePopupFloater> complete = onComplete;
                    onComplete = null;
                    complete?.Invoke(this);
                }
            }
        }
    }
}
