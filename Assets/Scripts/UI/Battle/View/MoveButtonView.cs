using LighthouseExtends.UIComponent.Button;
using Localization;
using TMPro;
using UI.Battle.Interface;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UI.Battle.View
{
    /// <summary>
    /// 1つの攻撃ボタンの表示部品
    /// 攻撃名・必要部位・破壊部位・威力・コスト・射程を表示する
    /// </summary>
    public class MoveButtonView : MonoBehaviour, ILanguageAwareUi, IPointerEnterHandler, IPointerExitHandler
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
        [SerializeField] private Color rangeActiveColor = MoveRangeSegmentBarView.RangeInColor;
        [SerializeField] private Color rangeInactiveColor = MoveRangeSegmentBarView.RangeOutColor;
        [SerializeField] private Color rangeOutOfBandColor = MoveRangeSegmentBarView.RangeOutColor;

        [Tooltip("部位欠損ロック演出(未設定なら子から解決)")]
        [SerializeField] private MoveButtonPartLockOverlay partLockOverlay;

        [Tooltip("リキャスト蓄積ゲージ(下から上へfill)未設定なら子RecastFillを解決")]
        [SerializeField] private Image recastFillImage;

        private static readonly Color RecastFillColor = new Color(1f, 0.12f, 0.1f, 0.55f);
        private const string FrameFxRootName = "FrameFxRoot";
        private const string RecastFillName = "RecastFill";
        private const string HoverOverlayName = "HoverOverlay";

        private bool isUsable;
        private bool isPointerOver;
        private Sprite rangeActiveSprite;
        private Sprite rangeInactiveSprite;
        private MoveRangeSegmentBarView rangeSegmentBarView;
        private LocalizedBakedTextApplier bakedChromeLabelApplier;
        private Transform frameFxRoot;
        private Image frameFxMaskImage;
        private Mask frameFxMask;
        private RectMask2D frameFxRectMask;

        private void Awake()
        {
            if (buttonBackground == null && button != null)
            {
                buttonBackground = button.GetComponent<Image>();
            }

            DisableRaycastOnDecorations();
            DisableBuiltInColorTint();
            EnsureRangeSegmentBarView();
            DisableHoverDarkenOverlay();
            EnsureRecastFillImage();
            EnsurePartLockOverlay();
            EnsureChromeLabelsResolved();
            BindHoverPointerEvents();
            if (!HasConfiguredRangeSegments())
            {
                MoveRangeSegmentBarView.ApplyDefaultSprites(rangeSegments, ref rangeActiveSprite, ref rangeInactiveSprite);
            }

            ApplyChromeLabels();
            RefreshBackgroundColor();
        }

        /// <summary>
        /// SelectableのColorTintが背景色を上書きしないよう無効化する
        /// </summary>
        private void DisableBuiltInColorTint()
        {
            if (button == null)
            {
                return;
            }

            button.transition = Selectable.Transition.None;
        }

        /// <summary>
        /// 部位ロック案内などホバー検知用のポインターイベントを登録する
        /// </summary>
        private void BindHoverPointerEvents()
        {
            if (button == null)
            {
                return;
            }

            EventTrigger trigger = button.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = button.gameObject.AddComponent<EventTrigger>();
            }

            AddHoverTrigger(trigger, EventTriggerType.PointerEnter, true);
            AddHoverTrigger(trigger, EventTriggerType.PointerExit, false);
        }

        private void AddHoverTrigger(EventTrigger trigger, EventTriggerType type, bool highlighted)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => SetHighlighted(highlighted));
            trigger.triggers.Add(entry);
        }

        /// <inheritdoc/>
        public void OnPointerEnter(PointerEventData eventData)
        {
            SetHighlighted(true);
        }

        /// <inheritdoc/>
        public void OnPointerExit(PointerEventData eventData)
        {
            SetHighlighted(false);
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
        /// 部位ロック案内のホバー表示用
        /// </summary>
        public void SetHighlighted(bool highlighted)
        {
            isPointerOver = highlighted;
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
                LocalizedFont.SetText(
                    moveNameText,
                    string.IsNullOrEmpty(move.Name) ? string.Empty : move.Name);
            }

            if (powerText != null)
            {
                LocalizedFont.SetText(
                    powerText,
                    Mathf.RoundToInt(move.Power * 100f).ToString());
            }

            if (gutsText != null)
            {
                LocalizedFont.SetText(
                    gutsText,
                    Mathf.RoundToInt(move.GutsCost).ToString());
            }

            if (rangeLabelText != null)
            {
                LocalizedFont.SetText(rangeLabelText, RangeLabel);
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
            ApplyRecastFill(move.RecastReady01);
            ApplyRangeSegments(move.RangeMin, move.RangeMax, maxDistance, move.Usable);
            RefreshBackgroundColor();
        }

        /// <summary>
        /// リキャストゲージだけを更新する
        /// </summary>
        /// <param name="recastReady01">準備完了度0=開始直後1=使用可</param>
        public void SetRecastReadyRatio(float recastReady01)
        {
            ApplyRecastFill(recastReady01);
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
                LocalizedFont.SetText(rangeLabelText, RangeLabel);
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

        /// <summary>
        /// リキャストとホバー共通の枠クリップルートを解決する
        /// </summary>
        private Transform EnsureFrameFxRoot()
        {
            if (frameFxRoot != null)
            {
                ConfigureFrameFxRoot(frameFxRoot);
                return frameFxRoot;
            }

            Transform existing = FindFrameFxRootTransform();
            if (existing != null)
            {
                frameFxRoot = existing;
                ConfigureFrameFxRoot(frameFxRoot);
                return frameFxRoot;
            }

            Transform maskParent = ResolveFrameMaskParent();
            var rootObject = new GameObject(
                FrameFxRootName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(LayoutElement),
                typeof(Mask),
                typeof(RectMask2D));
            frameFxRoot = rootObject.transform;
            frameFxRoot.SetParent(maskParent, false);
            ConfigureFrameFxRoot(frameFxRoot);
            return frameFxRoot;
        }

        private void ConfigureFrameFxRoot(Transform root)
        {
            if (root == null)
            {
                return;
            }

            // SlotFrameの子にMaskを置くと親枠のマテリアルが半透明化するため
            // 攻撃カード直下に置き部位ロックと同じく枠スプライトでクリップする
            if (root.parent != transform)
            {
                root.SetParent(transform, false);
            }

            StretchIgnoreLayoutRect((RectTransform)root);
            PlaceFrameFxRootInHierarchy(root);

            frameFxMaskImage = root.GetComponent<Image>();
            if (frameFxMaskImage == null)
            {
                frameFxMaskImage = root.gameObject.AddComponent<Image>();
            }

            frameFxMask = root.GetComponent<Mask>();
            if (frameFxMask == null)
            {
                frameFxMask = root.gameObject.AddComponent<Mask>();
            }

            frameFxRectMask = root.GetComponent<RectMask2D>();
            if (frameFxRectMask == null)
            {
                frameFxRectMask = root.gameObject.AddComponent<RectMask2D>();
            }

            BindFrameFxMask();
            ProtectButtonVisualsFromMask();
        }

        private void PlaceFrameFxRootInHierarchy(Transform root)
        {
            if (root == null)
            {
                return;
            }

            root.SetAsLastSibling();
            if (partLockOverlay != null)
            {
                partLockOverlay.transform.SetAsLastSibling();
            }
        }

        private void BindFrameFxMask()
        {
            if (frameFxMaskImage == null || frameFxMask == null || frameFxRectMask == null)
            {
                return;
            }

            Image frame = ResolveButtonFrameImage();
            if (frame != null && frame.sprite != null)
            {
                frameFxMaskImage.sprite = frame.sprite;
                frameFxMaskImage.type = frame.type;
                frameFxMaskImage.pixelsPerUnitMultiplier = frame.pixelsPerUnitMultiplier;
                frameFxMaskImage.preserveAspect = false;
                frameFxMaskImage.fillCenter = frame.fillCenter;
            }
            else
            {
                frameFxMaskImage.sprite = ResolveUiWhiteSprite();
                frameFxMaskImage.type = Image.Type.Simple;
                frameFxMaskImage.preserveAspect = false;
            }

            // 枠クリップ専用マスクは常に不透明の白(表示は出さない)
            frameFxMaskImage.color = Color.white;
            frameFxMaskImage.raycastTarget = false;
            frameFxMaskImage.maskable = false;
            frameFxMask.showMaskGraphic = false;
            frameFxMask.enabled = true;
            frameFxRectMask.enabled = true;
        }

        /// <summary>
        /// 攻撃カード本体がマスク対象に巻き込まれて薄くなるのを防ぐ
        /// </summary>
        private void ProtectButtonVisualsFromMask()
        {
            MaskableGraphic[] graphics = GetComponentsInChildren<MaskableGraphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                MaskableGraphic graphic = graphics[i];
                if (graphic == null)
                {
                    continue;
                }

                // FrameFxの子のみクリップ対象
                if (frameFxRoot != null && graphic.transform.IsChildOf(frameFxRoot))
                {
                    if (graphic != frameFxMaskImage)
                    {
                        graphic.maskable = true;
                    }

                    continue;
                }

                graphic.maskable = false;
            }
        }

        private Transform FindFrameFxRootTransform()
        {
            Transform direct = transform.Find(FrameFxRootName);
            if (direct != null)
            {
                return direct;
            }

            Image frame = ResolveButtonFrameImage();
            if (frame != null)
            {
                Transform underFrame = frame.transform.Find(FrameFxRootName);
                if (underFrame != null)
                {
                    return underFrame;
                }

                // 旧ルート名から移行
                Transform legacyRecast = frame.transform.Find("RecastFillRoot");
                if (legacyRecast != null)
                {
                    legacyRecast.name = FrameFxRootName;
                    return legacyRecast;
                }

                Transform legacyHover = frame.transform.Find("HoverOverlayRoot");
                if (legacyHover != null)
                {
                    legacyHover.name = FrameFxRootName;
                    return legacyHover;
                }
            }

            Transform legacyRoot = transform.Find("RecastFillRoot");
            if (legacyRoot != null)
            {
                legacyRoot.name = FrameFxRootName;
                return legacyRoot;
            }

            Transform legacyHoverOnRoot = transform.Find("HoverOverlayRoot");
            if (legacyHoverOnRoot != null)
            {
                legacyHoverOnRoot.name = FrameFxRootName;
                return legacyHoverOnRoot;
            }

            return null;
        }

        private Transform ResolveFrameMaskParent()
        {
            return transform;
        }

        private static void StretchIgnoreLayoutRect(RectTransform rectTransform)
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

            if (!rectTransform.TryGetComponent(out LayoutElement layoutElement))
            {
                layoutElement = rectTransform.gameObject.AddComponent<LayoutElement>();
            }

            layoutElement.ignoreLayout = true;
        }

        private Image ResolveButtonFrameImage()
        {
            if (buttonBackground != null)
            {
                return buttonBackground;
            }

            if (button != null)
            {
                return button.targetGraphic as Image;
            }

            return null;
        }

        private Image ResolveOrCreateChildImage(Transform parent, string childName)
        {
            if (parent == null)
            {
                return null;
            }

            Transform child = parent.Find(childName);
            if (child != null && child.TryGetComponent(out Image existing))
            {
                StretchIgnoreLayoutRect((RectTransform)child);
                return existing;
            }

            // 旧階層からの引き上げ
            Image relocated = FindDescendantImageNamed(childName);
            if (relocated != null)
            {
                relocated.transform.SetParent(parent, false);
                StretchIgnoreLayoutRect(relocated.rectTransform);
                return relocated;
            }

            var childObject = new GameObject(
                childName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            Transform childTransform = childObject.transform;
            childTransform.SetParent(parent, false);
            StretchIgnoreLayoutRect((RectTransform)childTransform);
            return childObject.GetComponent<Image>();
        }

        private Image FindDescendantImageNamed(string objectName)
        {
            Transform[] children = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i];
                if (child != null
                    && child.name == objectName
                    && child.TryGetComponent(out Image image))
                {
                    return image;
                }
            }

            return null;
        }

        /// <summary>
        /// リキャストゲージImageを共有枠マスク内に解決する
        /// </summary>
        private void EnsureRecastFillImage()
        {
            Transform root = EnsureFrameFxRoot();
            if (recastFillImage == null)
            {
                recastFillImage = ResolveOrCreateChildImage(root, RecastFillName);
            }
            else if (recastFillImage.transform.parent != root)
            {
                recastFillImage.transform.SetParent(root, false);
                StretchIgnoreLayoutRect(recastFillImage.rectTransform);
            }

            ConfigureRecastFillImage(recastFillImage);
        }

        private void ConfigureRecastFillImage(Image fillImage)
        {
            if (fillImage == null)
            {
                return;
            }

            fillImage.raycastTarget = false;
            fillImage.maskable = true;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Vertical;
            fillImage.fillOrigin = (int)Image.OriginVertical.Bottom;
            fillImage.fillClockwise = true;
            fillImage.material = null;
            fillImage.color = RecastFillColor;
            if (fillImage.sprite == null)
            {
                fillImage.sprite = ResolveUiWhiteSprite();
            }
        }

        private void ApplyRecastFill(float recastReady01)
        {
            if (recastFillImage == null)
            {
                EnsureRecastFillImage();
            }

            if (recastFillImage == null)
            {
                return;
            }

            float ready = Mathf.Clamp01(recastReady01);
            bool showFill = ready < 0.999f;
            recastFillImage.enabled = showFill;
            if (!showFill)
            {
                recastFillImage.fillAmount = 1f;
                return;
            }

            recastFillImage.fillAmount = ready;
        }

        private static Sprite cachedUiWhiteSprite;

        private static Sprite ResolveUiWhiteSprite()
        {
            if (cachedUiWhiteSprite != null)
            {
                return cachedUiWhiteSprite;
            }

            Texture2D texture = Texture2D.whiteTexture;
            cachedUiWhiteSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            return cachedUiWhiteSprite;
        }

        private void BindPartLockOverlayMask()
        {
            if (partLockOverlay == null)
            {
                return;
            }

            partLockOverlay.ApplyClipMask(ResolveButtonFrameImage());
        }

        private void ApplyPartLockOverlay(bool lockedByMissingPart)
        {
            EnsurePartLockOverlay();
            if (partLockOverlay == null)
            {
                return;
            }

            partLockOverlay.SetLocked(lockedByMissingPart);
            partLockOverlay.SetMessageHoverVisible(isPointerOver);
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
                LocalizedFont.SetText(
                    requiredPartLabelText,
                    MoveTargetPartLabelUtility.FormatRequiredRowLabel(requiredPartId));
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
                LocalizedFont.SetText(
                    targetPartLabelText,
                    MoveTargetPartLabelUtility.FormatTargetRowLabel(targetPartId));
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

        /// <summary>
        /// ホバー暗転用オーバーレイを無効化する
        /// </summary>
        private void DisableHoverDarkenOverlay()
        {
            Image overlay = FindDescendantImageNamed(HoverOverlayName);
            if (overlay == null)
            {
                return;
            }

            overlay.enabled = false;
            overlay.raycastTarget = false;
        }

        private void RefreshBackgroundColor()
        {
            if (buttonBackground == null)
            {
                return;
            }

            // 枠画像ありは常に不透明の乗算
            if (UsesFrameSpriteBackground())
            {
                buttonBackground.color = isUsable
                    ? Color.white
                    : new Color(0.75f, 0.75f, 0.76f, 1f);
                return;
            }

            Color target = unusableBackgroundColor;
            if (isUsable)
            {
                target = usableBackgroundColor;
            }

            target.a = 1f;
            buttonBackground.color = target;
        }

        private bool UsesFrameSpriteBackground()
        {
            return buttonBackground != null && buttonBackground.sprite != null;
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
