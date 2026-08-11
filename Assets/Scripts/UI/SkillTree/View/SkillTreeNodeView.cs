using Cysharp.Threading.Tasks;
using Extensions;
using LighthouseExtends.UIComponent.Button;
using SaveData;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace UI.SkillTree.View
{
    /// <summary>
    /// スキルツリー1ノードの表示
    /// </summary>
    public sealed class SkillTreeNodeView : MonoBehaviour
    {
        private static readonly Color SelectedOutlineColor = new(1f, 0.92f, 0.28f, 1f);
        private static readonly Color UnlockFlashHot = new(1.4f, 1.25f, 0.35f, 1f);
        private static readonly Color UnlockFlashWhite = new(1.6f, 1.55f, 1.3f, 1f);
        private static readonly Color UnlockOutlineColor = new(1f, 0.85f, 0.15f, 1f);
        private static readonly Color CheckmarkFlashColor = new(1.3f, 1.15f, 0.4f, 1f);
        private static readonly Vector2 SelectedOutlineDistance = new(5f, 5f);
        private static readonly Vector2 UnlockOutlineDistancePeak = new(18f, 18f);

        private const float UnlockAnticipationScale = 0.45f;
        private const float UnlockBurstScale = 1.85f;
        private const float UnlockDurationSeconds = 0.85f;
        private const float RevealDurationSeconds = 0.55f;
        private const float CheckmarkPopDelaySeconds = 0.18f;
        private const float UnlockSpinDegrees = 28f;

        [SerializeField] private SkillTreeNodeId nodeId;
        [SerializeField] private LHButton button;
        [SerializeField] private Image frameImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private Image checkmarkImage;

        private Outline selectionOutline;
        private bool isSelected;
        private CancellationTokenSource animationCts;
        private Color checkmarkBaseColor = Color.white;
        private bool checkmarkBaseColorCached;

        /// <summary>
        /// ノードID
        /// </summary>
        public SkillTreeNodeId NodeId => nodeId;

        /// <summary>
        /// 配置用RectTransform
        /// </summary>
        public RectTransform RectTransform => (RectTransform)transform;

        private void Awake()
        {
            HideFrameBackground();
            CacheCheckmarkBaseColor();
        }

        private void OnDisable()
        {
            CancelAnimation(resetVisuals: true);
        }

        private void OnDestroy()
        {
            CancelAnimation(resetVisuals: false);
        }

        /// <summary>
        /// 表示状態を更新する
        /// </summary>
        /// <param name="level">現在レベル</param>
        /// <param name="prerequisitesMet">前提達成か</param>
        /// <param name="selected">選択中か</param>
        public void Bind(int level, bool prerequisitesMet, bool selected)
        {
            // 再Bind時に途中スケールを正規化する
            CancelAnimation(resetVisuals: true);
            HideFrameBackground();

            bool unlocked = level > 0;
            bool visible = unlocked || prerequisitesMet;
            ApplyVisibility(visible, unlocked);
            SetSelected(visible && selected);

            if (button != null)
            {
                button.interactable = visible;
            }
        }

        /// <summary>
        /// 選択表示を切り替える
        /// </summary>
        /// <param name="selected">選択中か</param>
        public void SetSelected(bool selected)
        {
            isSelected = selected;
            ApplySelectionOutline();
        }

        /// <summary>
        /// 解放成功時の演出を再生する
        /// </summary>
        public void PlayUnlockAnimation()
        {
            CancelAnimation(resetVisuals: true);
            // 解放演出中は前面に出して見えやすくする
            transform.SetAsLastSibling();
            animationCts = CancellationTokenSource.CreateLinkedTokenSource(
                this.GetCancellationTokenOnDestroy());
            PlayUnlockAnimationAsync(animationCts.Token).Forget();
        }

        /// <summary>
        /// 前提達成で新たに出現したノードの演出を再生する
        /// </summary>
        /// <param name="startDelaySeconds">開始遅延秒</param>
        public void PlayRevealAnimation(float startDelaySeconds = 0f)
        {
            CancelAnimation(resetVisuals: true);
            animationCts = CancellationTokenSource.CreateLinkedTokenSource(
                this.GetCancellationTokenOnDestroy());
            PlayRevealAnimationAsync(startDelaySeconds, animationCts.Token).Forget();
        }

        /// <summary>
        /// ノード選択を購読する
        /// </summary>
        /// <param name="action">選択時</param>
        public IDisposable SubscribeClick(UnityAction action)
        {
            if (button == null)
            {
                Debug.LogError("[SkillTreeNodeView] buttonが未配線です", this);
                return new EmptyDisposable();
            }

            return button.SubscribeOnClick(action);
        }

        private void HideFrameBackground()
        {
            if (frameImage == null)
            {
                return;
            }

            // 後ろのボタン枠画像だけ非表示クリック判定は残す
            frameImage.color = new Color(1f, 1f, 1f, 0f);
            frameImage.raycastTarget = true;
        }

        private void CacheCheckmarkBaseColor()
        {
            if (checkmarkImage == null || checkmarkBaseColorCached)
            {
                return;
            }

            checkmarkBaseColor = checkmarkImage.color;
            checkmarkBaseColorCached = true;
        }

        private void ApplyVisibility(bool visible, bool unlocked)
        {
            if (frameImage != null)
            {
                frameImage.raycastTarget = visible;
            }

            if (checkmarkImage != null)
            {
                CacheCheckmarkBaseColor();
                checkmarkImage.enabled = visible && unlocked;
                checkmarkImage.rectTransform.localScale = Vector3.one;
                checkmarkImage.rectTransform.localRotation = Quaternion.identity;
                checkmarkImage.color = checkmarkBaseColor;
            }

            if (iconImage == null)
            {
                Debug.LogError("[SkillTreeNodeView] iconImageが未配線です", this);
                return;
            }

            if (!visible)
            {
                iconImage.enabled = false;
                return;
            }

            Sprite sprite = SkillTreeIconCatalog.Resolve(nodeId);
            iconImage.sprite = sprite;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            iconImage.enabled = sprite != null;
            iconImage.color = Color.white;
            iconImage.rectTransform.localRotation = Quaternion.identity;
        }

        private void ApplySelectionOutline()
        {
            Graphic outlineTarget = iconImage != null ? iconImage : (Graphic)frameImage;
            if (outlineTarget == null)
            {
                return;
            }

            EnsureOutline(outlineTarget);
            selectionOutline.effectColor = SelectedOutlineColor;
            selectionOutline.effectDistance = SelectedOutlineDistance;
            selectionOutline.useGraphicAlpha = true;
            selectionOutline.enabled = isSelected && outlineTarget.enabled;
        }

        private void EnsureOutline(Graphic outlineTarget)
        {
            if (selectionOutline == null || selectionOutline.gameObject != outlineTarget.gameObject)
            {
                selectionOutline = outlineTarget.GetComponent<Outline>();
                if (selectionOutline == null)
                {
                    selectionOutline = outlineTarget.gameObject.AddComponent<Outline>();
                }
            }
        }

        private async UniTaskVoid PlayUnlockAnimationAsync(CancellationToken cancellationToken)
        {
            Transform root = transform;
            root.localScale = Vector3.one * UnlockAnticipationScale;
            root.localRotation = Quaternion.identity;

            if (checkmarkImage != null && checkmarkImage.enabled)
            {
                checkmarkImage.rectTransform.localScale = Vector3.zero;
                checkmarkImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -48f);
                CacheCheckmarkBaseColor();
                checkmarkImage.color = CheckmarkFlashColor;
            }

            if (iconImage != null && iconImage.enabled)
            {
                iconImage.color = UnlockFlashHot;
            }

            try
            {
                await UniTask.WhenAll(
                    AnimateUnlockRootAsync(root, cancellationToken),
                    AnimateUnlockIconFlashAsync(cancellationToken),
                    AnimateUnlockOutlineAsync(cancellationToken),
                    AnimateCheckmarkUnlockAsync(cancellationToken));

                ResetAnimatedVisualsKeepingSelection();
            }
            catch (OperationCanceledException)
            {
                // 中断時はBindまたはOnDisableで正規化する
            }
        }

        private async UniTask AnimateUnlockRootAsync(
            Transform root,
            CancellationToken cancellationToken)
        {
            float duration = UnlockDurationSeconds;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                cancellationToken.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // 予縮->爆発->強弾性で収束
                float scale;
                if (t < 0.12f)
                {
                    float localT = t / 0.12f;
                    scale = Mathf.LerpUnclamped(
                        UnlockAnticipationScale,
                        UnlockAnticipationScale * 0.82f,
                        EaseInCubic(localT));
                }
                else if (t < 0.38f)
                {
                    float localT = (t - 0.12f) / 0.26f;
                    scale = Mathf.LerpUnclamped(
                        UnlockAnticipationScale * 0.82f,
                        UnlockBurstScale,
                        EaseOutBack(localT));
                }
                else
                {
                    float localT = (t - 0.38f) / 0.62f;
                    float bounce = Mathf.Exp(-5.2f * localT)
                        * Mathf.Cos(localT * Mathf.PI * 5.5f);
                    scale = 1f + ((UnlockBurstScale - 1f) * bounce);
                    scale = Mathf.Max(0.82f, scale);
                }

                // 左右に振って着地
                float spinWave = Mathf.Sin(t * Mathf.PI * 3.2f)
                    * (1f - EaseOutCubic(t))
                    * UnlockSpinDegrees;
                root.localScale = Vector3.one * scale;
                root.localRotation = Quaternion.Euler(0f, 0f, spinWave);
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            root.localScale = Vector3.one;
            root.localRotation = Quaternion.identity;
        }

        private async UniTask AnimateUnlockIconFlashAsync(CancellationToken cancellationToken)
        {
            if (iconImage == null || !iconImage.enabled)
            {
                return;
            }

            float duration = UnlockDurationSeconds;
            float elapsed = 0f;
            RectTransform iconRect = iconImage.rectTransform;

            while (elapsed < duration)
            {
                cancellationToken.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // 金白交互フラッシュ
                float flash = Mathf.Abs(Mathf.Sin(t * Mathf.PI * 7.5f));
                Color flashColor = Color.Lerp(UnlockFlashHot, UnlockFlashWhite, flash);
                // 終盤は白へ
                float settle = EaseInCubic(Mathf.Clamp01((t - 0.65f) / 0.35f));
                iconImage.color = Color.Lerp(flashColor, Color.white, settle);

                float iconSpin = Mathf.Sin(t * Mathf.PI * 4f)
                    * (1f - t)
                    * 12f;
                iconRect.localRotation = Quaternion.Euler(0f, 0f, iconSpin);
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            iconImage.color = Color.white;
            iconRect.localRotation = Quaternion.identity;
        }

        private async UniTask AnimateUnlockOutlineAsync(CancellationToken cancellationToken)
        {
            Graphic outlineTarget = iconImage != null ? iconImage : (Graphic)frameImage;
            if (outlineTarget == null)
            {
                return;
            }

            EnsureOutline(outlineTarget);
            selectionOutline.enabled = true;
            selectionOutline.useGraphicAlpha = true;

            float duration = UnlockDurationSeconds;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                cancellationToken.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                float pulse = 0.5f + (0.5f * Mathf.Sin(t * Mathf.PI * 8f));
                float envelope = 1f - EaseInCubic(Mathf.Clamp01((t - 0.55f) / 0.45f));
                float distance = Mathf.Lerp(6f, UnlockOutlineDistancePeak.x, pulse) * envelope;
                // 終盤は選択枠へ収束
                if (isSelected)
                {
                    distance = Mathf.Lerp(distance, SelectedOutlineDistance.x, Settle01(t));
                }

                selectionOutline.effectDistance = new Vector2(distance, distance);
                selectionOutline.effectColor = Color.Lerp(
                    UnlockOutlineColor,
                    isSelected ? SelectedOutlineColor : new Color(1f, 0.85f, 0.15f, 0f),
                    Settle01(t));
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            ApplySelectionOutline();
        }

        private static float Settle01(float t)
        {
            return EaseInCubic(Mathf.Clamp01((t - 0.7f) / 0.3f));
        }

        private async UniTask AnimateCheckmarkUnlockAsync(CancellationToken cancellationToken)
        {
            if (checkmarkImage == null || !checkmarkImage.enabled)
            {
                return;
            }

            await UniTask.Delay(
                TimeSpan.FromSeconds(CheckmarkPopDelaySeconds),
                DelayType.UnscaledDeltaTime,
                cancellationToken: cancellationToken);

            RectTransform checkRect = checkmarkImage.rectTransform;
            float duration = UnlockDurationSeconds * 0.72f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                cancellationToken.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                float scaleEnvelope;
                if (t < 0.45f)
                {
                    float localT = t / 0.45f;
                    scaleEnvelope = Mathf.LerpUnclamped(0f, 1.7f, EaseOutBack(localT));
                }
                else
                {
                    float localT = (t - 0.45f) / 0.55f;
                    float bounce = Mathf.Exp(-4.5f * localT)
                        * Mathf.Cos(localT * Mathf.PI * 4.2f);
                    scaleEnvelope = 1f + (0.7f * bounce);
                    scaleEnvelope = Mathf.Max(0.85f, scaleEnvelope);
                }

                float zRot = Mathf.LerpUnclamped(-48f, 0f, EaseOutBack(Mathf.Clamp01(t * 1.2f)));
                // 終盤微振動
                zRot += Mathf.Sin(t * Mathf.PI * 6f) * (1f - t) * 10f;

                checkRect.localScale = Vector3.one * scaleEnvelope;
                checkRect.localRotation = Quaternion.Euler(0f, 0f, zRot);

                float flash = Mathf.Abs(Mathf.Sin(t * Mathf.PI * 6f));
                checkmarkImage.color = Color.Lerp(
                    CheckmarkFlashColor,
                    checkmarkBaseColor,
                    EaseInCubic(t) * (0.35f + (0.65f * (1f - flash))));
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            checkRect.localScale = Vector3.one;
            checkRect.localRotation = Quaternion.identity;
            checkmarkImage.color = checkmarkBaseColor;
        }

        private async UniTaskVoid PlayRevealAnimationAsync(
            float startDelaySeconds,
            CancellationToken cancellationToken)
        {
            Transform root = transform;
            root.localScale = Vector3.one * 0.15f;
            root.localRotation = Quaternion.identity;

            if (iconImage != null && iconImage.enabled)
            {
                Color c = UnlockFlashHot;
                c.a = 0.2f;
                iconImage.color = c;
            }

            try
            {
                if (startDelaySeconds > 0f)
                {
                    await UniTask.Delay(
                        TimeSpan.FromSeconds(startDelaySeconds),
                        DelayType.UnscaledDeltaTime,
                        cancellationToken: cancellationToken);
                }

                await UniTask.WhenAll(
                    AnimateRevealRootAsync(root, cancellationToken),
                    AnimateRevealIconAsync(cancellationToken));

                ResetAnimatedVisualsKeepingSelection();
            }
            catch (OperationCanceledException)
            {
                // 中断時はBindまたはOnDisableで正規化する
            }
        }

        private async UniTask AnimateRevealRootAsync(
            Transform root,
            CancellationToken cancellationToken)
        {
            float duration = RevealDurationSeconds;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                cancellationToken.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                float scale;
                if (t < 0.55f)
                {
                    float localT = t / 0.55f;
                    scale = Mathf.LerpUnclamped(0.15f, 1.35f, EaseOutBack(localT));
                }
                else
                {
                    float localT = (t - 0.55f) / 0.45f;
                    float bounce = Mathf.Exp(-5f * localT)
                        * Mathf.Cos(localT * Mathf.PI * 3.5f);
                    scale = 1f + (0.35f * bounce);
                }

                float spin = Mathf.Sin(t * Mathf.PI * 2.4f) * (1f - t) * 16f;
                root.localScale = Vector3.one * scale;
                root.localRotation = Quaternion.Euler(0f, 0f, spin);
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            root.localScale = Vector3.one;
            root.localRotation = Quaternion.identity;
        }

        private async UniTask AnimateRevealIconAsync(CancellationToken cancellationToken)
        {
            if (iconImage == null || !iconImage.enabled)
            {
                return;
            }

            float duration = RevealDurationSeconds;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                cancellationToken.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                Color hot = UnlockFlashHot;
                hot.a = Mathf.Lerp(0.2f, 1f, EaseOutCubic(t));
                iconImage.color = Color.Lerp(hot, Color.white, EaseInCubic(t));
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            iconImage.color = Color.white;
        }

        private void ResetAnimatedVisualsKeepingSelection()
        {
            transform.localScale = Vector3.one;
            transform.localRotation = Quaternion.identity;

            if (iconImage != null && iconImage.enabled)
            {
                iconImage.color = Color.white;
                iconImage.rectTransform.localRotation = Quaternion.identity;
            }

            if (checkmarkImage != null)
            {
                CacheCheckmarkBaseColor();
                checkmarkImage.rectTransform.localScale = Vector3.one;
                checkmarkImage.rectTransform.localRotation = Quaternion.identity;
                checkmarkImage.color = checkmarkBaseColor;
            }

            ApplySelectionOutline();
        }

        private static float EaseOutCubic(float t)
        {
            float inv = 1f - t;
            return 1f - (inv * inv * inv);
        }

        private static float EaseInCubic(float t)
        {
            return t * t * t;
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float inv = t - 1f;
            return 1f + (c3 * inv * inv * inv) + (c1 * inv * inv);
        }

        private void CancelAnimation(bool resetVisuals)
        {
            if (animationCts != null)
            {
                animationCts.Cancel();
                animationCts.Dispose();
                animationCts = null;
            }

            if (!resetVisuals)
            {
                return;
            }

            ResetAnimatedVisualsKeepingSelection();
        }

        private sealed class EmptyDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
