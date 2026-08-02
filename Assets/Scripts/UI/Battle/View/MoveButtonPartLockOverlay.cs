using Extensions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Battle.View
{
    /// <summary>
    /// 部位欠損で封じられた攻撃ボタンへ鎖と錠の画像オーバーレイを表示する
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MoveButtonPartLockOverlay : MonoBehaviour
    {
        private const float AppearDuration = 0.28f;
        private const float UnlockFadeDuration = 0.16f;
        private const float PadlockAnchoredSize = 0.4f;
        private const float TemporaryMessageDuration = 1.4f;
        private const string LockedMessage = "部位が破壊されています";

        [SerializeField] private Image veilImage;
        [SerializeField] private Image chainImage;
        [SerializeField] private Image padlockImage;
        [SerializeField] private Image maskImage;
        [SerializeField] private TMP_Text lockedMessageText;
        [SerializeField] private Color veilColor = new Color(0.22f, 0.24f, 0.28f, 0.38f);
        [SerializeField] private Color chainColor = new Color(1f, 1f, 1f, 0.88f);
        [SerializeField] private Color padlockColor = new Color(1f, 1f, 1f, 0.95f);
        [SerializeField] private Color messageColor = new Color(1f, 0.94f, 0.78f, 1f);

        private bool isLocked;
        private bool isMessageHoverVisible;
        private float temporaryMessageUntilUnscaled = -1f;
        private float reveal01;
        private float targetReveal01;
        private bool resourcesBound;
        private bool clipMaskReady;
        private Mask clipMask;
        private RectMask2D rectMask;

        private void Awake()
        {
            EnsureStructure();
            BindSprites();
            reveal01 = 0f;
            targetReveal01 = 0f;
            ApplyVisuals(0f);
        }

        /// <summary>
        /// 攻撃ボタンの枠形状でオーバーレイをクリップする
        /// </summary>
        public void ApplyClipMask(Image buttonFrameImage)
        {
            EnsureStructure();
            ConfigureClipMask(buttonFrameImage);
            clipMaskReady = true;
        }

        /// <summary>
        /// 部位ロック表示のON/OFFを切り替える
        /// </summary>
        public void SetLocked(bool locked)
        {
            EnsureStructure();
            BindSprites();
            if (!clipMaskReady)
            {
                ConfigureClipMask(null);
                clipMaskReady = true;
            }

            if (isLocked == locked)
            {
                RefreshMessageVisibility();
                return;
            }

            isLocked = locked;
            if (!locked)
            {
                isMessageHoverVisible = false;
                temporaryMessageUntilUnscaled = -1f;
            }

            targetReveal01 = locked ? 1f : 0f;
            if (locked && reveal01 <= 0.001f)
            {
                reveal01 = 0f;
            }

            ApplyVisuals(reveal01);
        }

        /// <summary>
        /// カーソルホバー中のみ破壊メッセージを表示する
        /// </summary>
        public void SetMessageHoverVisible(bool visible)
        {
            if (isMessageHoverVisible == visible)
            {
                return;
            }

            isMessageHoverVisible = visible;
            RefreshMessageVisibility();
        }

        /// <summary>
        /// 技使用試行時などに破壊メッセージを一時表示する
        /// </summary>
        public void ShowMessageTemporary(float durationSeconds = TemporaryMessageDuration)
        {
            if (!isLocked)
            {
                return;
            }

            EnsureStructure();
            float duration = Mathf.Max(0.1f, durationSeconds);
            temporaryMessageUntilUnscaled = Time.unscaledTime + duration;
            RefreshMessageVisibility();
        }

        private bool IsTemporaryMessageActive()
        {
            return temporaryMessageUntilUnscaled > 0f
                && Time.unscaledTime < temporaryMessageUntilUnscaled;
        }

        private void RefreshMessageVisibility()
        {
            bool showMessage = isLocked
                && reveal01 > 0.4f
                && (isMessageHoverVisible || IsTemporaryMessageActive());
            SetTextAlpha(lockedMessageText, messageColor, showMessage ? 1f : 0f, showMessage);
        }

        private void Update()
        {
            if (temporaryMessageUntilUnscaled > 0f
                && Time.unscaledTime >= temporaryMessageUntilUnscaled)
            {
                temporaryMessageUntilUnscaled = -1f;
                RefreshMessageVisibility();
            }

            float duration = targetReveal01 > reveal01 ? AppearDuration : UnlockFadeDuration;
            if (duration <= 0.001f)
            {
                return;
            }

            float next = Mathf.MoveTowards(reveal01, targetReveal01, Time.unscaledDeltaTime / duration);
            if (Mathf.Approximately(next, reveal01))
            {
                return;
            }

            reveal01 = next;
            ApplyVisuals(reveal01);
        }

        private void EnsureStructure()
        {
            EnsureClipComponents();

            if (veilImage == null)
            {
                veilImage = ResolveOrCreateImage("Veil", stretch: true, preserveAspect: false);
            }

            if (chainImage == null)
            {
                chainImage = ResolveOrCreateImage("Chain", stretch: true, preserveAspect: false);
            }

            if (padlockImage == null)
            {
                padlockImage = ResolveOrCreateImage("Padlock", stretch: false, preserveAspect: true);
            }

            if (lockedMessageText == null)
            {
                lockedMessageText = ResolveOrCreateMessageText("LockedMessage");
            }

            ConfigureImage(veilImage, preserveAspect: false);
            ConfigureImage(chainImage, preserveAspect: false);
            ConfigureImage(padlockImage, preserveAspect: true);
            ConfigureMessageText(lockedMessageText);

            if (veilImage != null)
            {
                StretchRect(veilImage.rectTransform);
            }

            if (chainImage != null)
            {
                StretchRect(chainImage.rectTransform);
            }

            ConfigurePadlockRect(padlockImage);
            ConfigureMessageRect(lockedMessageText);
            SetChildrenMaskable(true);
        }

        private void EnsureClipComponents()
        {
            if (maskImage == null)
            {
                maskImage = GetComponent<Image>();
            }

            if (maskImage == null)
            {
                maskImage = gameObject.AddComponent<Image>();
            }

            maskImage.raycastTarget = false;
            maskImage.maskable = false;
            maskImage.color = Color.white;

            if (clipMask == null)
            {
                clipMask = GetComponent<Mask>();
            }

            if (clipMask == null)
            {
                clipMask = gameObject.AddComponent<Mask>();
            }

            clipMask.showMaskGraphic = false;

            if (rectMask == null)
            {
                rectMask = GetComponent<RectMask2D>();
            }

            if (rectMask == null)
            {
                rectMask = gameObject.AddComponent<RectMask2D>();
            }

            // 矩形クリップを二重にかけ角のはみ出しを抑える
            rectMask.enabled = true;
        }

        private void ConfigureClipMask(Image buttonFrameImage)
        {
            EnsureClipComponents();

            if (buttonFrameImage != null && buttonFrameImage.sprite != null)
            {
                maskImage.sprite = buttonFrameImage.sprite;
                maskImage.type = buttonFrameImage.type;
                maskImage.pixelsPerUnitMultiplier = buttonFrameImage.pixelsPerUnitMultiplier;
                maskImage.preserveAspect = false;
                maskImage.fillCenter = buttonFrameImage.fillCenter;
                clipMask.enabled = true;
            }
            else
            {
                maskImage.sprite = GetWhiteSprite();
                maskImage.type = Image.Type.Simple;
                maskImage.preserveAspect = false;
                clipMask.enabled = true;
            }

            maskImage.color = new Color(1f, 1f, 1f, 1f);
            maskImage.raycastTarget = false;
            SetChildrenMaskable(true);
        }

        private void SetChildrenMaskable(bool maskable)
        {
            if (veilImage != null)
            {
                veilImage.maskable = maskable;
            }

            if (chainImage != null)
            {
                chainImage.maskable = maskable;
            }

            if (padlockImage != null)
            {
                padlockImage.maskable = maskable;
            }

            if (lockedMessageText != null)
            {
                lockedMessageText.maskable = maskable;
            }
        }

        private Image ResolveOrCreateImage(string childName, bool stretch, bool preserveAspect)
        {
            Transform child = transform.Find(childName);
            if (child != null && child.TryGetComponent(out Image existing))
            {
                ConfigureImage(existing, preserveAspect);
                if (stretch)
                {
                    StretchRect(existing.rectTransform);
                }

                return existing;
            }

            var go = new GameObject(childName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);
            var image = go.GetComponent<Image>();
            ConfigureImage(image, preserveAspect);
            if (stretch)
            {
                StretchRect(image.rectTransform);
            }

            return image;
        }

        private TMP_Text ResolveOrCreateMessageText(string childName)
        {
            Transform child = transform.Find(childName);
            if (child != null && child.TryGetComponent(out TextMeshProUGUI existing))
            {
                return existing;
            }

            var go = new GameObject(childName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(transform, false);
            return go.GetComponent<TextMeshProUGUI>();
        }

        private static void ConfigureImage(Image image, bool preserveAspect)
        {
            if (image == null)
            {
                return;
            }

            image.raycastTarget = false;
            image.preserveAspect = preserveAspect;
            image.type = Image.Type.Simple;
            image.maskable = true;
        }

        private static void ConfigureMessageText(TMP_Text text)
        {
            if (text == null)
            {
                return;
            }

            text.raycastTarget = false;
            text.maskable = true;
            text.text = LockedMessage;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.fontSize = 13f;
            text.fontSizeMin = 9f;
            text.fontSizeMax = 14f;
            text.enableAutoSizing = true;
            text.characterSpacing = -1f;
            text.lineSpacing = -8f;
            AppTmpFontUtility.ApplyDefaultFont(text);
            BattleHudVisualUtility.ApplyLabelOutline(text, 0.28f);
        }

        private static void StretchRect(RectTransform rectTransform)
        {
            if (rectTransform == null)
            {
                return;
            }

            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.localScale = Vector3.one;
            rectTransform.localRotation = Quaternion.identity;
        }

        private static void ConfigurePadlockRect(Image padlock)
        {
            if (padlock == null)
            {
                return;
            }

            RectTransform rect = padlock.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localRotation = Quaternion.identity;

            RectTransform parentRect = rect.parent as RectTransform;
            float parentW = 120f;
            float parentH = 120f;
            if (parentRect != null)
            {
                parentW = parentRect.rect.width;
                parentH = parentRect.rect.height;
                if (parentW < 1f || parentH < 1f)
                {
                    parentW = Mathf.Max(parentRect.sizeDelta.x, 120f);
                    parentH = Mathf.Max(parentRect.sizeDelta.y, 120f);
                }
            }

            float parentMin = Mathf.Min(parentW, parentH);
            if (parentMin < 40f)
            {
                parentMin = 120f;
            }

            float size = parentMin * PadlockAnchoredSize;
            rect.sizeDelta = new Vector2(size, size);
            // 文言を下に出すため錠をやや上へずらす
            rect.anchoredPosition = new Vector2(0f, parentH * 0.08f);
            rect.localScale = Vector3.one;
        }

        private static void ConfigureMessageRect(TMP_Text text)
        {
            if (text == null)
            {
                return;
            }

            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0.06f, 0.04f);
            rect.anchorMax = new Vector2(0.94f, 0.32f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.SetAsLastSibling();
        }

        private void BindSprites()
        {
            if (resourcesBound)
            {
                return;
            }

            Sprite chainSprite = MoveCommandSpriteCatalog.LoadPartLockChain();
            Sprite padlockSprite = MoveCommandSpriteCatalog.LoadPartLockPadlock();
            if (chainSprite == null || padlockSprite == null)
            {
                Debug.LogError(
                    "[MoveButtonPartLockOverlay] 鎖または錠スプライトの読み込みに失敗しました" +
                    " Resources/Image/Battle/MovePartLockChain と MovePartLockPadlock を確認してください");
                return;
            }

            if (chainImage != null)
            {
                chainImage.sprite = chainSprite;
            }

            if (padlockImage != null)
            {
                padlockImage.sprite = padlockSprite;
            }

            if (veilImage != null)
            {
                veilImage.sprite = GetWhiteSprite();
                veilImage.color = veilColor;
            }

            if (lockedMessageText != null)
            {
                lockedMessageText.text = LockedMessage;
            }

            resourcesBound = true;
        }

        private static Sprite whiteSprite;

        private static Sprite GetWhiteSprite()
        {
            if (whiteSprite != null)
            {
                return whiteSprite;
            }

            whiteSprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                100f);
            return whiteSprite;
        }

        private void ApplyVisuals(float progress)
        {
            float p = Mathf.Clamp01(progress);
            bool visible = p > 0.001f;

            if (visible)
            {
                if (veilImage != null)
                {
                    StretchRect(veilImage.rectTransform);
                }

                if (chainImage != null)
                {
                    StretchRect(chainImage.rectTransform);
                    chainImage.preserveAspect = false;
                }

                if (padlockImage != null)
                {
                    ConfigurePadlockRect(padlockImage);
                }

                if (lockedMessageText != null)
                {
                    ConfigureMessageRect(lockedMessageText);
                }
            }

            float veilAlpha = Mathf.Clamp01(p * 1.15f);
            SetImageAlpha(veilImage, veilColor, veilAlpha, visible);

            float chainAlpha = Mathf.Clamp01((p - 0.05f) / 0.65f);
            SetImageAlpha(chainImage, chainColor, chainAlpha, visible && chainAlpha > 0.001f);

            float lockT = Mathf.Clamp01((p - 0.4f) / 0.6f);
            SetImageAlpha(padlockImage, padlockColor, lockT, visible && lockT > 0.001f);

            bool showMessage = isLocked
                && lockT > 0.001f
                && (isMessageHoverVisible || IsTemporaryMessageActive());
            SetTextAlpha(lockedMessageText, messageColor, showMessage ? 1f : 0f, showMessage);

            if (padlockImage != null)
            {
                float pop = Mathf.Sin(lockT * Mathf.PI * 0.5f);
                float scale = Mathf.Lerp(0.55f, 1f, pop);
                if (lockT > 0.85f)
                {
                    float bounceT = (lockT - 0.85f) / 0.15f;
                    scale *= 1f + (0.06f * (1f - bounceT));
                }

                if (lockT <= 0.001f)
                {
                    scale = 0.55f;
                }

                padlockImage.rectTransform.localScale = new Vector3(scale, scale, 1f);
            }
        }

        private static void SetImageAlpha(Image image, Color baseColor, float alpha01, bool enabled)
        {
            if (image == null)
            {
                return;
            }

            image.enabled = enabled;
            Color c = baseColor;
            c.a = Mathf.Clamp01(alpha01) * baseColor.a;
            image.color = c;
        }

        private static void SetTextAlpha(TMP_Text text, Color baseColor, float alpha01, bool enabled)
        {
            if (text == null)
            {
                return;
            }

            text.enabled = enabled;
            Color c = baseColor;
            c.a = Mathf.Clamp01(alpha01) * baseColor.a;
            text.color = c;
        }
    }
}
