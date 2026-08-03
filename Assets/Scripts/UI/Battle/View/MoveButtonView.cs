using LighthouseExtends.UIComponent.Button;
using Localization;
using TMPro;
using UI.Battle.Interface;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Battle.View
{
    /// <summary>
    /// 1つの攻撃ボタンの表示部品
    /// 攻撃名・必要部位・破壊部位・威力・コスト・射程を表示する
    /// </summary>
    public class MoveButtonView : MonoBehaviour, ILanguageAwareUi
    {
        private static string RangeLabel =>
            LocalizedText.GetOrFallback(GameTextKeys.BattleRangeLabel, "射程");

        [Tooltip("クリック購読に使うボタン本体")]
        [SerializeField] private LHButton button;

        [Tooltip("攻撃名表示")]
        [SerializeField] private TMP_Text moveNameText;

        [Tooltip("威力の数値表示")]
        [SerializeField] private TMP_Text powerText;

        [Tooltip("必要部位の画像")]
        [SerializeField] private Image requiredPartImage;

        [Tooltip("必要部位のラベル表示")]
        [SerializeField] private TMP_Text requiredPartLabelText;

        [Tooltip("破壊対象部位の画像")]
        [SerializeField] private Image attributeImage;

        [Tooltip("破壊対象部位のラベル表示")]
        [SerializeField] private TMP_Text targetPartLabelText;

        [Tooltip("必要ガッツの数値表示")]
        [SerializeField] private TMP_Text gutsText;

        [Tooltip("射程ラベル表示")]
        [SerializeField] private TMP_Text rangeLabelText;

        [Tooltip("有効距離を分割した距離セグメント画像(近・中・遠など)。射程に重なるものだけ有効化する)")]
        [SerializeField] private Image[] rangeSegments;

        [Header("見た目")]
        [SerializeField] private Image buttonBackground;
        [SerializeField] private Color usableBackgroundColor = new Color(1f, 0.98f, 0.94f, 0.96f);
        [SerializeField] private Color unusableBackgroundColor = new Color(0.82f, 0.8f, 0.78f, 0.72f);
        [SerializeField] private Color highlightBackgroundColor = new Color(1f, 0.96f, 0.9f, 1f);
        [SerializeField] private Color rangeActiveColor = MoveRangeSegmentBarView.RangeInColor;
        [SerializeField] private Color rangeInactiveColor = MoveRangeSegmentBarView.RangeOutColor;
        [SerializeField] private Color rangeOutOfBandColor = MoveRangeSegmentBarView.RangeOutColor;

        [Tooltip("部位欠損ロック演出(未設定なら子から解決)")]
        [SerializeField] private MoveButtonPartLockOverlay partLockOverlay;

        private bool isUsable;
        private bool isHighlighted;
        private Sprite rangeActiveSprite;
        private Sprite rangeInactiveSprite;
        private MoveRangeSegmentBarView rangeSegmentBarView;
        private LocalizedBakedTextApplier bakedChromeLabelApplier;

        private void Awake()
        {
            if (buttonBackground == null && button != null)
            {
                buttonBackground = button.GetComponent<Image>();
            }

            DisableRaycastOnDecorations();
            EnsureRangeSegmentBarView();
            EnsurePartLockOverlay();
            EnsureChromeLabelsResolved();
            if (!HasConfiguredRangeSegments())
            {
                MoveRangeSegmentBarView.ApplyDefaultSprites(rangeSegments, ref rangeActiveSprite, ref rangeInactiveSprite);
            }

            ApplyChromeLabels();
        }

        /// <summary>
        /// 装飾用GraphicのRaycastを無効化しクリックをボタン本体へ通す
        /// </summary>
        private void DisableRaycastOnDecorations()
        {
            if (button == null)
            {
                return;
            }

            Graphic buttonGraphic = button.targetGraphic;
            Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                Graphic graphic = graphics[i];
                if (graphic != null && graphic != buttonGraphic)
                {
                    graphic.raycastTarget = false;
                }
            }
        }

        private void EnsureRangeSegmentBarView()
        {
            if (rangeSegmentBarView != null)
            {
                return;
            }

            Transform rangeRoot = transform.Find("RangeSegments");
            if (rangeRoot == null && rangeSegments != null && rangeSegments.Length > 0 && rangeSegments[0] != null)
            {
                rangeRoot = rangeSegments[0].transform.parent;
            }

            if (rangeRoot == null)
            {
                return;
            }

            rangeSegmentBarView = rangeRoot.GetComponent<MoveRangeSegmentBarView>();
            if (rangeSegmentBarView == null)
            {
                rangeSegmentBarView = rangeRoot.gameObject.AddComponent<MoveRangeSegmentBarView>();
            }

            rangeSegmentBarView.Bind(rangeSegments);
        }

        /// <summary>
        /// 未設定の射程セグメントに既定スプライトを割り当てる
        /// </summary>
        public void ApplyDefaultRangeSprites()
        {
            MoveRangeSegmentBarView.ApplyDefaultSprites(rangeSegments, ref rangeActiveSprite, ref rangeInactiveSprite);
        }

        /// <summary>
        /// クリック購読やホバー対象に使うボタン本体
        /// </summary>
        public LHButton Button => button;

        /// <summary>
        /// ホバー強調のオンオフ
        /// </summary>
        public void SetHighlighted(bool highlighted)
        {
            isHighlighted = highlighted;
            RefreshBackgroundColor();
            if (partLockOverlay != null)
            {
                partLockOverlay.SetMessageHoverVisible(highlighted);
            }
        }

        /// <summary>
        /// 表示内容を更新する有効距離は0からmaxDistanceの軸で射程に重なるセグメントを有効化する
        /// </summary>
        public void Apply(
            in MoveDisplay move,
            Sprite targetPartIcon,
            Color targetPartColor,
            float maxDistance)
        {
            ApplyChromeLabels();

            if (moveNameText != null)
            {
                moveNameText.text = string.IsNullOrEmpty(move.Name) ? string.Empty : move.Name;
            }

            if (powerText != null)
            {
                powerText.text = Mathf.RoundToInt(move.Power * 100f).ToString();
            }

            if (gutsText != null)
            {
                gutsText.text = Mathf.RoundToInt(move.GutsCost).ToString();
            }

            if (rangeLabelText != null)
            {
                rangeLabelText.text = RangeLabel;
            }

            ApplyRequiredPartIcon(move.RequiredPartId);
            ApplyTargetPartIcon(targetPartIcon, targetPartColor, move.TargetPartId);

            if (button != null)
            {
                // 部位ロック中はクリックで案内を出せるよう操作受付は残す
                button.interactable = move.Usable || move.LockedByMissingPart;
            }

            isUsable = move.Usable;
            SetVisualUsable(move.Usable);
            ApplyPartLockOverlay(move.LockedByMissingPart);
            ApplyRangeSegments(move.RangeMin, move.RangeMax, maxDistance, move.Usable);
            RefreshBackgroundColor();
        }

        /// <summary>
        /// 部位破壊ロックの理由メッセージを一時表示する
        /// </summary>
        public void ShowPartLockMessage()
        {
            EnsurePartLockOverlay();
            if (partLockOverlay == null)
            {
                return;
            }

            partLockOverlay.ShowMessageTemporary();
        }

        private void EnsureChromeLabelsResolved()
        {
            if (bakedChromeLabelApplier != null)
            {
                return;
            }

            bakedChromeLabelApplier = new LocalizedBakedTextApplier();
            bakedChromeLabelApplier.Register(GameTextKeys.TrainingAttackPower, "威力");
            bakedChromeLabelApplier.Register(GameTextKeys.TrainingAttackCost, "コスト");
            bakedChromeLabelApplier.Capture(transform);
        }


        /// <inheritdoc/>
        public void RefreshLocalizedUi()
        {
            ApplyChromeLabels();
            if (rangeLabelText != null)
            {
                rangeLabelText.text = RangeLabel;
            }
        }

        private void ApplyChromeLabels()
        {
            EnsureChromeLabelsResolved();
            bakedChromeLabelApplier?.Apply();
        }

        private void EnsurePartLockOverlay()
        {
            if (partLockOverlay != null)
            {
                BindPartLockOverlayMask();
                return;
            }

            partLockOverlay = GetComponentInChildren<MoveButtonPartLockOverlay>(true);
            if (partLockOverlay != null)
            {
                BindPartLockOverlayMask();
                return;
            }

            // プレハブ未配線時のみ最前面に描画用オーバーレイを足す(既存Rectは変更しない)
            var overlayObject = new GameObject(
                "PartLockOverlay",
                typeof(RectTransform),
                typeof(LayoutElement),
                typeof(MoveButtonPartLockOverlay));
            Transform overlayTransform = overlayObject.transform;
            overlayTransform.SetParent(transform, false);
            overlayTransform.SetAsLastSibling();

            var rectTransform = (RectTransform)overlayTransform;
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.localScale = Vector3.one;
            rectTransform.localRotation = Quaternion.identity;

            var layoutElement = overlayObject.GetComponent<LayoutElement>();
            layoutElement.ignoreLayout = true;

            partLockOverlay = overlayObject.GetComponent<MoveButtonPartLockOverlay>();
            BindPartLockOverlayMask();
        }

        private void BindPartLockOverlayMask()
        {
            if (partLockOverlay == null)
            {
                return;
            }

            Image frame = buttonBackground;
            if (frame == null && button != null)
            {
                frame = button.targetGraphic as Image;
            }

            partLockOverlay.ApplyClipMask(frame);
        }

        private void ApplyPartLockOverlay(bool lockedByMissingPart)
        {
            EnsurePartLockOverlay();
            if (partLockOverlay == null)
            {
                return;
            }

            partLockOverlay.SetLocked(lockedByMissingPart);
            partLockOverlay.SetMessageHoverVisible(isHighlighted);
        }

        private void ApplyRequiredPartIcon(MoveTargetPartId requiredPartId)
        {
            Image icon = ResolvePartIconImage(requiredPartImage);
            if (icon == null)
            {
                return;
            }

            Sprite sprite = MoveCommandSpriteCatalog.LoadTargetPartIcon(requiredPartId);
            if (sprite == null)
            {
                SetPartIconVisible(requiredPartImage, false);
                SetPartLabelVisible(requiredPartLabelText, false);
                return;
            }

            SetPartIconVisible(requiredPartImage, true);
            SetPartLabelVisible(requiredPartLabelText, true);
            if (requiredPartLabelText != null)
            {
                requiredPartLabelText.text = MoveTargetPartLabelUtility.FormatRequiredRowLabel(requiredPartId);
            }
            icon.sprite = sprite;
            icon.color = Color.white;
            icon.preserveAspect = true;
            icon.type = Image.Type.Simple;
            DisablePartFrame(requiredPartImage);
        }

        private void ApplyTargetPartIcon(Sprite sprite, Color color, MoveTargetPartId targetPartId)
        {
            Image icon = ResolvePartIconImage(attributeImage);
            if (icon == null)
            {
                return;
            }

            Sprite resolvedSprite = sprite ?? MoveCommandSpriteCatalog.LoadTargetPartIcon(targetPartId);
            if (resolvedSprite == null)
            {
                SetPartIconVisible(attributeImage, false);
                SetPartLabelVisible(targetPartLabelText, false);
                return;
            }

            SetPartIconVisible(attributeImage, true);
            SetPartLabelVisible(targetPartLabelText, true);
            if (targetPartLabelText != null)
            {
                targetPartLabelText.text = MoveTargetPartLabelUtility.FormatTargetRowLabel(targetPartId);
            }
            icon.sprite = resolvedSprite;
            icon.color = color;
            icon.preserveAspect = true;
            icon.type = Image.Type.Simple;
            DisablePartFrame(attributeImage);
        }

        private static Image ResolvePartIconImage(Image rootImage)
        {
            if (rootImage == null)
            {
                return null;
            }

            if (rootImage.gameObject.name == "Icon")
            {
                return rootImage;
            }

            Transform iconTransform = rootImage.transform.Find("Icon");
            if (iconTransform != null && iconTransform.TryGetComponent(out Image childIcon))
            {
                return childIcon;
            }

            return rootImage;
        }

        private static void DisablePartFrame(Image rootImage)
        {
            if (rootImage == null)
            {
                return;
            }

            Image icon = ResolvePartIconImage(rootImage);
            if (icon == null || icon == rootImage)
            {
                return;
            }

            // 親枠Imageは常に無効化して塗りが透けないようにする
            rootImage.enabled = false;
            rootImage.sprite = null;
            rootImage.color = new Color(1f, 1f, 1f, 0f);
        }

        private static void SetPartIconVisible(Image rootImage, bool visible)
        {
            if (rootImage == null)
            {
                return;
            }

            Image icon = ResolvePartIconImage(rootImage);
            if (icon != null)
            {
                icon.enabled = visible;
                if (!visible)
                {
                    icon.sprite = null;
                }
            }

            DisablePartFrame(rootImage);
            rootImage.gameObject.SetActive(visible);
        }

        private static void SetPartLabelVisible(TMP_Text labelText, bool visible)
        {
            if (labelText == null)
            {
                return;
            }

            labelText.gameObject.SetActive(visible);
        }

        private void SetVisualUsable(bool usable)
        {
            float alpha = usable ? 1f : 0.42f;
            SetImageAlpha(requiredPartImage, alpha);
            SetImageAlpha(attributeImage, alpha);
            SetTextAlpha(moveNameText, alpha);
            SetTextAlpha(requiredPartLabelText, alpha);
            SetTextAlpha(targetPartLabelText, alpha);
            SetTextAlpha(powerText, alpha);
            SetTextAlpha(gutsText, alpha);
            SetTextAlpha(rangeLabelText, alpha);
        }

        private void RefreshBackgroundColor()
        {
            if (buttonBackground == null)
            {
                return;
            }

            if (UsesFrameSpriteBackground())
            {
                buttonBackground.color = isUsable && isHighlighted
                    ? highlightBackgroundColor
                    : Color.white;
                return;
            }

            Color target = unusableBackgroundColor;
            if (isUsable)
            {
                target = isHighlighted ? highlightBackgroundColor : usableBackgroundColor;
            }

            buttonBackground.color = target;
        }

        private bool UsesFrameSpriteBackground()
        {
            return buttonBackground != null
                && buttonBackground.sprite != null
                && button != null
                && button.transition == Selectable.Transition.ColorTint;
        }

        private bool HasConfiguredRangeSegments()
        {
            if (rangeSegments == null || rangeSegments.Length < MoveRangeSegmentBarView.SegmentCount)
            {
                return false;
            }

            for (int i = 0; i < MoveRangeSegmentBarView.SegmentCount; i++)
            {
                Image segment = rangeSegments[i];
                if (segment == null || segment.sprite == null)
                {
                    return false;
                }
            }

            return true;
        }

        private static void SetImageAlpha(Image image, float alpha)
        {
            if (image == null)
            {
                return;
            }

            Transform iconTransform = image.transform.Find("Icon");
            if (iconTransform != null && iconTransform.TryGetComponent(out Image childIcon))
            {
                // 親は枠なので透過のまま子アイコンだけアルファを変える
                image.enabled = false;
                Color frameColor = image.color;
                frameColor.a = 0f;
                image.color = frameColor;

                Color childColor = childIcon.color;
                childColor.a = alpha;
                childIcon.color = childColor;
                return;
            }

            Color color = image.color;
            color.a = alpha;
            image.color = color;
        }

        private static void SetTextAlpha(TMP_Text text, float alpha)
        {
            if (text == null)
            {
                return;
            }

            Color color = text.color;
            color.a = alpha;
            text.color = color;
        }

        private void ApplyRangeSegments(float rangeMin, float rangeMax, float maxDistance, bool usable)
        {
            EnsureRangeSegmentBarView();
            if (rangeSegmentBarView != null)
            {
                rangeSegmentBarView.ApplyStyled(
                    rangeMin,
                    rangeMax,
                    maxDistance,
                    usable,
                    rangeActiveColor,
                    rangeInactiveColor,
                    rangeOutOfBandColor);
                return;
            }

            MoveRangeSegmentBarView.ApplySegments(
                rangeSegments,
                rangeMin,
                rangeMax,
                maxDistance,
                usable,
                rangeActiveSprite,
                rangeInactiveSprite,
                rangeActiveColor,
                rangeInactiveColor,
                rangeOutOfBandColor);
        }
    }
}
