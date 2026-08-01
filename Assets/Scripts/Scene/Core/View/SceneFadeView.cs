using System.Threading;
using Cysharp.Threading.Tasks;
using Scene.Core.Interface;
using UnityEngine;
using UnityEngine.UI;

namespace Scene.Core.View
{
    /// <summary>
    /// 全画面を覆うフェード用オーバーレイ。
    /// シーンをまたいで常駐し(DontDestroyOnLoad)、最前面で暗転・明転を行う。
    /// フェードの色と時間はインスペクターで設定する。
    /// </summary>
    public class SceneFadeView : MonoBehaviour, ISceneFade
    {
        private const float ClearAlphaThreshold = 0.001f;

        [Header("参照")]
        [Tooltip("オーバーレイのCanvas(Screen Space - Overlay)")]
        [SerializeField] private Canvas canvas;

        [Tooltip("透明度を制御するCanvasGroup")]
        [SerializeField] private CanvasGroup canvasGroup;

        [Tooltip("全画面に広げた塗りつぶし用Image")]
        [SerializeField] private Image fadeImage;

        [Header("フェード設定")]
        [Tooltip("フェードの色(暗転時の色)。アルファはフェードで制御するためここでは無視される")]
        [SerializeField] private Color fadeColor = Color.black;

        [Tooltip("フェードにかける秒数")]
        [SerializeField] private float fadeDuration = 0.3f;

        [Tooltip("最前面に出すためのソート順。他のCanvasより大きくする")]
        [SerializeField] private int sortingOrder = 32000;

        [Tooltip("起動時に暗転状態(覆った状態)から始める。trueなら最初のシーンも明転で表示される")]
        [SerializeField] private bool startOpaque = true;

        private readonly SemaphoreSlim fadeGate = new SemaphoreSlim(1, 1);
        private int fadeGeneration;

        /// <inheritdoc />
        public bool IsOpaque =>
            canvasGroup != null && canvasGroup.alpha > ClearAlphaThreshold;

        private void Awake()
        {
            EnsureFadeCanvasLayout();

            float initialAlpha = startOpaque ? 1f : 0f;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = initialAlpha;
            }

            ApplyPresentationState(initialAlpha > ClearAlphaThreshold);
        }

        /// <inheritdoc />
        public async UniTask FadeOutAsync(CancellationToken cancellationToken = default)
        {
            await fadeGate.WaitAsync(cancellationToken);
            try
            {
                EnsureFadeCanvasLayout();
                if (canvas != null)
                {
                    canvas.enabled = true;
                }

                ApplyInputBlocking(true);

                if (canvasGroup == null)
                {
                    return;
                }

                // 明転状態から暗転を始める前にCanvasを1フレーム描画可能にする
                if (canvasGroup.alpha <= ClearAlphaThreshold)
                {
                    canvasGroup.alpha = 0f;
                    await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);
                }

                await FadeToAsync(1f, cancellationToken);
                EnsureOpaque();
            }
            finally
            {
                fadeGate.Release();
            }
        }

        /// <inheritdoc />
        public async UniTask FadeInAsync(CancellationToken cancellationToken = default)
        {
            await fadeGate.WaitAsync(cancellationToken);
            try
            {
                await FadeToAsync(0f, cancellationToken);
            }
            finally
            {
                fadeGate.Release();
            }
        }

        /// <inheritdoc />
        public void EnsureOpaque()
        {
            EnsureFadeCanvasLayout();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }

            ApplyPresentationState(true);
        }

        /// <summary>
        /// 明転済み時だけ入力ブロックを解除する
        /// 進行中のフェードは中断しない
        /// </summary>
        public void ReleaseInputBlock()
        {
            if (canvasGroup != null && canvasGroup.alpha > ClearAlphaThreshold)
            {
                return;
            }

            ApplyPresentationState(false);
        }

        /// <inheritdoc />
        public void ForceRelease()
        {
            fadeGeneration++;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }

            ApplyPresentationState(false);
        }

        private async UniTask FadeToAsync(float targetAlpha, CancellationToken cancellationToken)
        {
            if (canvasGroup == null)
            {
                return;
            }

            int generation = ++fadeGeneration;
            bool wantOpaque = targetAlpha > ClearAlphaThreshold;

            // フェード開始時は必ず描画可能状態へ戻す
            EnsureFadeCanvasLayout();
            if (canvas != null)
            {
                canvas.enabled = true;
            }

            // 暗転中は入力を塞ぐ明転アニメ中も完了までは塞ぐ
            ApplyInputBlocking(true);

            try
            {
                float startAlpha = canvasGroup.alpha;
                float duration = Mathf.Max(0f, fadeDuration);

                if (Mathf.Approximately(startAlpha, targetAlpha))
                {
                    canvasGroup.alpha = targetAlpha;
                    return;
                }

                if (duration <= 0f)
                {
                    canvasGroup.alpha = targetAlpha;
                    return;
                }

                float elapsed = 0f;
                while (elapsed < duration)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (generation != fadeGeneration)
                    {
                        return;
                    }

                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }

                if (generation != fadeGeneration)
                {
                    return;
                }

                canvasGroup.alpha = targetAlpha;
            }
            finally
            {
                if (generation == fadeGeneration)
                {
                    ApplyPresentationState(wantOpaque);
                }
                else if (canvasGroup.alpha <= ClearAlphaThreshold)
                {
                    ApplyPresentationState(false);
                }
            }
        }

        private void ApplyPresentationState(bool isOpaque)
        {
            EnsureFadeCanvasLayout();

            if (canvasGroup != null)
            {
                // 完了時は端点へ完全に揃える
                canvasGroup.alpha = isOpaque ? 1f : 0f;
            }

            ApplyInputBlocking(isOpaque);

            if (canvas != null)
            {
                // 明転完了後は最前面Canvasを無効化し下位UIの入力を奪わない
                canvas.enabled = isOpaque;
            }
        }

        /// <summary>
        /// フェードCanvasの縮退スケールとソート崩れを直し不可視化を防ぐ
        /// </summary>
        private void EnsureFadeCanvasLayout()
        {
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.overrideSorting = true;
                canvas.sortingOrder = sortingOrder;
                RestoreNonZeroScale(canvas.transform as RectTransform);
            }

            RestoreNonZeroScale(transform as RectTransform);

            if (fadeImage != null)
            {
                fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 1f);
                RestoreNonZeroScale(fadeImage.rectTransform);
            }
        }

        private static void RestoreNonZeroScale(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            if (rect.localScale.sqrMagnitude < 0.001f)
            {
                rect.localScale = Vector3.one;
            }
        }

        private void ApplyInputBlocking(bool isBlocked)
        {
            if (canvasGroup != null)
            {
                canvasGroup.blocksRaycasts = isBlocked;
                canvasGroup.interactable = isBlocked;
            }

            if (fadeImage != null)
            {
                fadeImage.raycastTarget = isBlocked;
            }

            if (canvas != null)
            {
                GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
                if (raycaster != null)
                {
                    raycaster.enabled = isBlocked;
                }
            }
        }
    }
}
