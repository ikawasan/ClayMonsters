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
        private static readonly Color UnlockFlashColor = new(1f, 0.95f, 0.55f, 1f);
        private static readonly Vector2 SelectedOutlineDistance = new(5f, 5f);
        private const float UnlockScalePeak = 1.22f;
        private const float UnlockDurationSeconds = 0.38f;
        private const float RevealDurationSeconds = 0.28f;
        private const float CheckmarkPopDelaySeconds = 0.08f;

        [SerializeField] private SkillTreeNodeId nodeId;
        [SerializeField] private LHButton button;
        [SerializeField] private Image frameImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private Image checkmarkImage;

        private Outline selectionOutline;
        private bool isSelected;
        private CancellationTokenSource animationCts;

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
            animationCts = CancellationTokenSource.CreateLinkedTokenSource(
                this.GetCancellationTokenOnDestroy());
            PlayUnlockAnimationAsync(animationCts.Token).Forget();
        }

        /// <summary>
        /// 前提達成で新たに出現したノードの演出を再生する
        /// </summary>
        public void PlayRevealAnimation()
        {
            CancelAnimation(resetVisuals: true);
            animationCts = CancellationTokenSource.CreateLinkedTokenSource(
                this.GetCancellationTokenOnDestroy());
            PlayRevealAnimationAsync(animationCts.Token).Forget();
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

        private void ApplyVisibility(bool visible, bool unlocked)
        {
            if (frameImage != null)
            {
                frameImage.raycastTarget = visible;
            }

            if (checkmarkImage != null)
            {
                checkmarkImage.enabled = visible && unlocked;
                checkmarkImage.rectTransform.localScale = Vector3.one;
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
        }

        private void ApplySelectionOutline()
        {
            Graphic outlineTarget = iconImage != null ? iconImage : (Graphic)frameImage;
            if (outlineTarget == null)
            {
                return;
            }

            if (selectionOutline == null || selectionOutline.gameObject != outlineTarget.gameObject)
            {
                selectionOutline = outlineTarget.GetComponent<Outline>();
                if (selectionOutline == null)
                {
                    selectionOutline = outlineTarget.gameObject.AddComponent<Outline>();
                }
            }

            selectionOutline.effectColor = SelectedOutlineColor;
            selectionOutline.effectDistance = SelectedOutlineDistance;
            selectionOutline.useGraphicAlpha = true;
            selectionOutline.enabled = isSelected && outlineTarget.enabled;
        }

        private async UniTaskVoid PlayUnlockAnimationAsync(CancellationToken cancellationToken)
        {
            Transform root = transform;
            root.localScale = Vector3.one * 0.72f;

            if (checkmarkImage != null && checkmarkImage.enabled)
            {
                checkmarkImage.rectTransform.localScale = Vector3.zero;
            }

            if (iconImage != null && iconImage.enabled)
            {
                iconImage.color = UnlockFlashColor;
            }

            try
            {
                await UniTask.WhenAll(
                    AnimateScaleAsync(
                        root,
                        0.72f,
                        UnlockScalePeak,
                        1f,
                        UnlockDurationSeconds,
                        cancellationToken),
                    AnimateCheckmarkUnlockAsync(cancellationToken));

                if (iconImage != null && iconImage.enabled)
                {
                    iconImage.color = Color.white;
                }
            }
            catch (OperationCanceledException)
            {
                // 中断時はBindまたはOnDisableで正規化する
            }
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

            await AnimateScaleAsync(
                checkmarkImage.rectTransform,
                0f,
                1.35f,
                1f,
                UnlockDurationSeconds * 0.75f,
                cancellationToken);
        }

        private async UniTaskVoid PlayRevealAnimationAsync(CancellationToken cancellationToken)
        {
            Transform root = transform;
            root.localScale = Vector3.one * 0.55f;

            if (iconImage != null && iconImage.enabled)
            {
                Color c = iconImage.color;
                c.a = 0.35f;
                iconImage.color = c;
            }

            try
            {
                await AnimateScaleAsync(
                    root,
                    0.55f,
                    1.12f,
                    1f,
                    RevealDurationSeconds,
                    cancellationToken);

                if (iconImage != null && iconImage.enabled)
                {
                    iconImage.color = Color.white;
                }
            }
            catch (OperationCanceledException)
            {
                // 中断時はBindまたはOnDisableで正規化する
            }
        }

        private static async UniTask AnimateScaleAsync(
            Transform target,
            float from,
            float peak,
            float to,
            float durationSeconds,
            CancellationToken cancellationToken)
        {
            if (target == null)
            {
                return;
            }

            float duration = Mathf.Max(0.01f, durationSeconds);
            float elapsed = 0f;
            // 前半peak後半to
            float upRatio = 0.55f;

            while (elapsed < duration)
            {
                cancellationToken.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float scale;
                if (t <= upRatio)
                {
                    float localT = t / upRatio;
                    float eased = EaseOutCubic(localT);
                    scale = Mathf.LerpUnclamped(from, peak, eased);
                }
                else
                {
                    float localT = (t - upRatio) / (1f - upRatio);
                    float eased = EaseOutCubic(localT);
                    scale = Mathf.LerpUnclamped(peak, to, eased);
                }

                target.localScale = Vector3.one * scale;
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            target.localScale = Vector3.one * to;
        }

        private static float EaseOutCubic(float t)
        {
            float inv = 1f - t;
            return 1f - (inv * inv * inv);
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

            transform.localScale = Vector3.one;
            if (checkmarkImage != null)
            {
                checkmarkImage.rectTransform.localScale = Vector3.one;
            }

            if (iconImage != null && iconImage.enabled)
            {
                iconImage.color = Color.white;
            }
        }

        private sealed class EmptyDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
