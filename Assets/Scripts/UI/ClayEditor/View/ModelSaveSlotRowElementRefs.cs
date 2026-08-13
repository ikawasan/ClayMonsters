using ClayEditor.Rigging;
using GameData;
using LighthouseExtends.UIComponent.Button;
using SaveData;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace UI.ClayEditor.View
{
    /// <summary>
    /// シーン配置済みスロット行のUI参照を保持する
    /// ModelSaveSlotRowUiBuilderのRowElementsへ変換する
    /// </summary>
    public sealed class ModelSaveSlotRowElementRefs :
        MonoBehaviour,
        IDeselectHandler,
        IPointerEnterHandler,
        IPointerExitHandler,
        ISelectHandler
    {
        private const float HighlightedBrightness = 0.72f;
        private const string SelectionCheckMaskRootName = "SelectionCheckMask";
        private const string SelectionCheckMarkRootName = "SelectionCheckMark";
        private const string SelectionCheckIconName = "SelectionCheckIcon";
        private const int SelectionCheckIconSize = 128;

        [SerializeField] private Image thumbnailImage;
        [SerializeField] private RectTransform thumbnailFrame;
        [SerializeField] private RectTransform thumbnailColumn;
        [SerializeField] private TMP_Text indexText;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text paramsText;
        [SerializeField] private TMP_Text subText;
        [SerializeField] private RectTransform leftInfoColumn;
        [SerializeField] private RectTransform attacksContainer;
        [SerializeField] private float thumbnailColumnWidth = ModelSaveSlotRowUiBuilder.ScrollListThumbnailColumnWidth;
        [SerializeField] private bool useConfirmLayout = true;
        [SerializeField] private bool preservePrefabLayout;
        [SerializeField] private ModelSaveSlotRowUiBuilder.ConfirmLayoutSize confirmLayoutSize =
            ModelSaveSlotRowUiBuilder.ConfirmLayoutSize.Standard;

        private Color[] baseTextColors;
        private Color[] baseImageColors;
        private bool hasCapturedHighlightTargets;
        private Image[] highlightImages;
        private bool isPointerInside;
        private bool isSelected;
        private bool isSelectionMarked;
        private bool suppressHoverHighlight;
        private TMP_Text[] highlightTexts;
        private GameObject selectionCheckMaskRoot;
        private GameObject selectionCheckRoot;
        private Image selectionCheckOverlay;
        private Image selectionCheckIcon;
        private static Sprite sharedCheckIconSprite;
        private static int sharedCheckIconVersion;

        private void Awake()
        {
            if (GetComponentInParent<ModelSaveConfirmView>(true) != null)
            {
                EnsureConfirmPrefabLayout();
            }

            EnsureThumbnailVisuals();
        }

        /// <summary>
        /// サムネイル枠と内側背景を未設定時のみ適用する
        /// </summary>
        public void EnsureThumbnailVisuals()
        {
        }

        /// <summary>
        /// セーブスロット一覧向けの横長レイアウトを適用する
        /// </summary>
        public void EnsureScrollListLayout()
        {
            thumbnailColumnWidth = ModelSaveSlotRowUiBuilder.ScrollListThumbnailColumnWidth;
            useConfirmLayout = false;
            preservePrefabLayout = false;
            suppressHoverHighlight = false;
            confirmLayoutSize = ModelSaveSlotRowUiBuilder.ConfirmLayoutSize.Standard;
        }

        /// <summary>
        /// ModelSaveConfirmView向けにプレハブ手動配置を維持する
        /// </summary>
        public void EnsureConfirmPrefabLayout()
        {
            useConfirmLayout = true;
            preservePrefabLayout = true;
            suppressHoverHighlight = true;
        }

        /// <summary>
        /// プレハブ配線済みの参照が揃っているか返す
        /// </summary>
        public bool HasWiredReferences()
        {
            return thumbnailImage != null && nameText != null && paramsText != null;
        }

        /// <summary>
        /// ClayEdit保存確認画面向けの拡大レイアウトを適用する
        /// </summary>
        public void EnsureClayEditPreviewLayout()
        {
            thumbnailColumnWidth = ModelSaveSlotRowUiBuilder.ConfirmPreviewThumbnailColumnWidth;
            useConfirmLayout = true;
            preservePrefabLayout = false;
            suppressHoverHighlight = true;
            confirmLayoutSize = ModelSaveSlotRowUiBuilder.ConfirmLayoutSize.Preview;
        }

        /// <inheritdoc/>
        public void OnDeselect(BaseEventData eventData)
        {
            if (suppressHoverHighlight)
            {
                return;
            }

            isSelected = false;
            ApplyHighlightState();
        }

        /// <inheritdoc/>
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (suppressHoverHighlight)
            {
                return;
            }

            isPointerInside = true;
            ApplyHighlightState();
        }

        /// <inheritdoc/>
        public void OnPointerExit(PointerEventData eventData)
        {
            if (suppressHoverHighlight)
            {
                return;
            }

            isPointerInside = false;
            ApplyHighlightState();
        }

        /// <inheritdoc/>
        public void OnSelect(BaseEventData eventData)
        {
            if (suppressHoverHighlight)
            {
                return;
            }

            // 複数選択マーク中はEventSystem選択の暗転を抑止する
            if (isSelectionMarked)
            {
                isSelected = false;
                ApplyHighlightState();
                return;
            }

            isSelected = true;
            ApplyHighlightState();
        }

        /// <summary>
        /// 複数選択の選択マークを付けるまたは外す
        /// </summary>
        /// <param name="marked">選択中ならtrue</param>
        public void SetSelectionMarked(bool marked)
        {
            isSelectionMarked = marked;
            isSelected = false;
            if (!marked
                && EventSystem.current != null
                && EventSystem.current.currentSelectedGameObject != null
                && EventSystem.current.currentSelectedGameObject.transform.IsChildOf(transform))
            {
                EventSystem.current.SetSelectedGameObject(null);
            }

            ApplySelectionMarkVisual();
            ApplyHighlightState();
        }

        /// <summary>
        /// 保存確認画面向けにセーブ済みスロット内容だけ反映する
        /// レイアウトはプレハブ配置を維持する
        /// </summary>
        public void BindConfirmFromSlot(ModelSaveSlot slot)
        {
            EnsureConfirmPrefabLayout();

            if (slot == null || string.IsNullOrEmpty(slot.modelName))
            {
                BindConfirmEmpty();
                return;
            }

            if (nameText != null)
            {
                nameText.text = slot.modelName;
            }

            if (paramsText != null)
            {
                paramsText.text = ModelSaveSummaryFormatter.FormatConfirmStatusParameters(slot.status);
            }

            ResolveAttacksPanel()?.ShowForConfirmPrefab(CollectAttackMotions(slot.attackMotions));
        }

        /// <summary>
        /// 保存確認画面向けに保存前プレビュー内容だけ反映する
        /// レイアウトはプレハブ配置を維持する
        /// </summary>
        public void BindConfirmPreview(
            string modelName,
            ModelStatus status,
            IReadOnlyList<MotionType> registeredAttackMotions)
        {
            EnsureConfirmPrefabLayout();

            if (nameText != null)
            {
                nameText.text = modelName ?? string.Empty;
            }

            if (paramsText != null)
            {
                paramsText.text = ModelSaveSummaryFormatter.FormatConfirmStatusParameters(status);
            }

            ResolveAttacksPanel()?.ShowForConfirmPrefab(CollectAttackMotions(registeredAttackMotions));
        }

        /// <summary>
        /// 保存確認画面向けに表示内容をクリアする
        /// レイアウトはプレハブ配置を維持する
        /// </summary>
        public void BindConfirmEmpty()
        {
            EnsureConfirmPrefabLayout();

            if (nameText != null)
            {
                nameText.text = string.Empty;
            }

            if (paramsText != null)
            {
                paramsText.text = string.Empty;
            }

            ResolveAttacksPanel()?.ClearForConfirmPrefab();
        }

        /// <summary>
        /// 保存確認画面向けにサムネイル画像を反映する
        /// レイアウトはプレハブ配置を維持する
        /// </summary>
        public void ApplyConfirmThumbnail(Sprite sprite)
        {
            if (thumbnailImage == null)
            {
                return;
            }

            if (sprite == null)
            {
                thumbnailImage.sprite = null;
                return;
            }

            thumbnailImage.sprite = sprite;
        }

        /// <summary>
        /// 保持している参照からRowElementsを生成する
        /// </summary>
        public ModelSaveSlotRowUiBuilder.RowElements ToRowElements()
        {
            if (thumbnailImage == null || nameText == null || paramsText == null)
            {
                CaptureFromHierarchy(transform);
            }

            return new ModelSaveSlotRowUiBuilder.RowElements(
                thumbnailImage,
                thumbnailFrame,
                indexText,
                nameText,
                paramsText,
                subText,
                leftInfoColumn,
                attacksContainer,
                thumbnailColumnWidth,
                useSquareThumbnail: !useConfirmLayout,
                useConfirmLayout: useConfirmLayout,
                thumbnailColumn: thumbnailColumn,
                confirmLayoutSize: confirmLayoutSize,
                preservePrefabLayout: preservePrefabLayout);
        }

        /// <summary>
        /// BuildConfirmContentの結果をそのまま保持する
        /// </summary>
        public void ApplyBuiltRowElements(ModelSaveSlotRowUiBuilder.RowElements rowElements)
        {
            thumbnailImage = rowElements.ThumbnailImage;
            thumbnailFrame = rowElements.ThumbnailFrame;
            thumbnailColumn = rowElements.ThumbnailColumn;
            indexText = rowElements.IndexText;
            nameText = rowElements.NameText;
            paramsText = rowElements.ParamsText;
            subText = rowElements.SubText;
            leftInfoColumn = rowElements.LeftInfoColumn;
            attacksContainer = rowElements.AttacksContainer;
            thumbnailColumnWidth = rowElements.ThumbnailColumnWidth;
            useConfirmLayout = rowElements.UseConfirmLayout;
            confirmLayoutSize = rowElements.ConfirmLayoutSize;
            preservePrefabLayout = rowElements.PreservePrefabLayout;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>
        /// 子階層から参照を収集する
        /// </summary>
        public void CaptureFromHierarchy(Transform rowContentRoot)
        {
            if (preservePrefabLayout && HasWiredReferences())
            {
                return;
            }

            if (!ModelSaveSlotRowUiBuilder.TryBindRowElements(
                    rowContentRoot,
                    thumbnailColumnWidth,
                    useConfirmLayout,
                    confirmLayoutSize,
                    preservePrefabLayout,
                    out ModelSaveSlotRowUiBuilder.RowElements rowElements))
            {
                Debug.LogError($"[ModelSaveSlotRowElementRefs] bind failed: {name}", this);
                return;
            }

            ApplyBuiltRowElements(rowElements);
        }

        private void ApplyHighlightState()
        {
            if (suppressHoverHighlight)
            {
                return;
            }

            CaptureHighlightTargets();
            bool highlighted = isPointerInside || isSelected || isSelectionMarked;

            for (int i = 0; i < highlightTexts.Length; i++)
            {
                TMP_Text text = highlightTexts[i];
                if (text == null)
                {
                    continue;
                }

                text.color = highlighted
                    ? DarkenColor(baseTextColors[i])
                    : baseTextColors[i];
            }

            for (int i = 0; i < highlightImages.Length; i++)
            {
                Image image = highlightImages[i];
                if (image == null)
                {
                    continue;
                }

                image.color = highlighted
                    ? DarkenColor(baseImageColors[i])
                    : baseImageColors[i];
            }
        }

        private void CaptureHighlightTargets()
        {
            if (hasCapturedHighlightTargets)
            {
                return;
            }

            highlightTexts = CollectHighlightTexts();
            baseTextColors = new Color[highlightTexts.Length];
            for (int i = 0; i < highlightTexts.Length; i++)
            {
                baseTextColors[i] = highlightTexts[i].color;
            }

            highlightImages = CollectHighlightImages();
            baseImageColors = new Color[highlightImages.Length];
            for (int i = 0; i < highlightImages.Length; i++)
            {
                baseImageColors[i] = highlightImages[i].color;
            }

            hasCapturedHighlightTargets = true;
        }

        private TMP_Text[] CollectHighlightTexts()
        {
            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
            var filtered = new List<TMP_Text>(texts.Length);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (text == null || IsSelectionCheckMarkObject(text.gameObject))
                {
                    continue;
                }

                filtered.Add(text);
            }

            return filtered.ToArray();
        }

        private Image[] CollectHighlightImages()
        {
            Image[] rowImages = GetComponentsInChildren<Image>(true);
            var contentImages = new List<Image>(rowImages.Length);
            for (int i = 0; i < rowImages.Length; i++)
            {
                Image image = rowImages[i];
                if (image == null || IsSelectionCheckMarkObject(image.gameObject))
                {
                    continue;
                }

                contentImages.Add(image);
            }

            return contentImages.ToArray();
        }

        private static bool IsSelectionCheckMarkObject(GameObject target)
        {
            if (target == null)
            {
                return false;
            }

            return target.name == SelectionCheckMaskRootName
                || target.name == SelectionCheckMarkRootName
                || target.name == SelectionCheckIconName;
        }

        private static Color DarkenColor(Color color)
        {
            return new Color(
                color.r * HighlightedBrightness,
                color.g * HighlightedBrightness,
                color.b * HighlightedBrightness,
                color.a);
        }

        private void OnDisable()
        {
            if (!hasCapturedHighlightTargets)
            {
                return;
            }

            isPointerInside = false;
            isSelected = false;
            if (isSelectionMarked)
            {
                isSelectionMarked = false;
                ApplySelectionMarkVisual();
            }

            ApplyHighlightState();
        }

        private void EnsureSelectionCheckMark()
        {
            if (selectionCheckMaskRoot != null
                && selectionCheckRoot != null
                && selectionCheckOverlay != null
                && selectionCheckIcon != null)
            {
                ApplySelectionCheckMaskLayout();
                return;
            }

            RectTransform hostRect = ResolveSelectionCheckHost();
            if (hostRect == null)
            {
                return;
            }

            DestroyLegacySelectionCheckObjects(hostRect);

            Transform existingMask = hostRect.Find(SelectionCheckMaskRootName);
            if (existingMask != null)
            {
                selectionCheckMaskRoot = existingMask.gameObject;
                Transform existingMark = existingMask.Find(SelectionCheckMarkRootName);
                if (existingMark != null)
                {
                    selectionCheckRoot = existingMark.gameObject;
                    selectionCheckOverlay = existingMark.GetComponent<Image>();
                    Transform iconTransform = existingMark.Find(SelectionCheckIconName);
                    selectionCheckIcon = iconTransform != null
                        ? iconTransform.GetComponent<Image>()
                        : null;
                    if (selectionCheckOverlay != null && selectionCheckIcon != null)
                    {
                        EnsureMaskComponent(selectionCheckMaskRoot, hostRect);
                        ApplySelectionCheckMaskLayout();
                        return;
                    }
                }

                Object.Destroy(existingMask.gameObject);
                selectionCheckMaskRoot = null;
                selectionCheckRoot = null;
                selectionCheckOverlay = null;
                selectionCheckIcon = null;
            }

            GameObject maskObject = new GameObject(
                SelectionCheckMaskRootName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Mask),
                typeof(LayoutElement));
            maskObject.layer = hostRect.gameObject.layer;
            IgnoreParentLayout(maskObject);
            RectTransform maskRect = maskObject.GetComponent<RectTransform>();
            maskRect.SetParent(hostRect, false);
            selectionCheckMaskRoot = maskObject;
            EnsureMaskComponent(maskObject, hostRect);

            GameObject rootObject = new GameObject(
                SelectionCheckMarkRootName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(LayoutElement));
            rootObject.layer = hostRect.gameObject.layer;
            IgnoreParentLayout(rootObject);
            RectTransform rootRect = rootObject.GetComponent<RectTransform>();
            rootRect.SetParent(maskRect, false);

            selectionCheckRoot = rootObject;
            selectionCheckOverlay = rootObject.GetComponent<Image>();
            selectionCheckOverlay.sprite = CreateWhiteSprite();
            selectionCheckOverlay.type = Image.Type.Simple;
            selectionCheckOverlay.color = new Color(0.05f, 0.55f, 0.22f, 0.42f);
            selectionCheckOverlay.raycastTarget = false;
            selectionCheckOverlay.maskable = true;

            CanvasGroup canvasGroup = rootObject.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = rootObject.AddComponent<CanvasGroup>();
            }

            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            GameObject iconObject = new GameObject(
                SelectionCheckIconName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(LayoutElement));
            iconObject.layer = hostRect.gameObject.layer;
            IgnoreParentLayout(iconObject);
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.SetParent(rootRect, false);
            ApplyCenteredCheckIconLayout(iconRect);

            selectionCheckIcon = iconObject.GetComponent<Image>();
            selectionCheckIcon.sprite = GetOrCreateCheckIconSprite();
            selectionCheckIcon.type = Image.Type.Simple;
            selectionCheckIcon.preserveAspect = true;
            selectionCheckIcon.color = Color.white;
            selectionCheckIcon.raycastTarget = false;
            selectionCheckIcon.maskable = true;

            ApplySelectionCheckMaskLayout();
        }

        private void DestroyLegacySelectionCheckObjects(RectTransform hostRect)
        {
            if (hostRect == null)
            {
                return;
            }

            // マスクなしで直下に付いた旧オーバーレイを除去する
            Transform legacyMark = hostRect.Find(SelectionCheckMarkRootName);
            if (legacyMark != null)
            {
                Object.Destroy(legacyMark.gameObject);
            }

            if (hostRect != transform)
            {
                Transform legacyOnSelf = transform.Find(SelectionCheckMarkRootName);
                if (legacyOnSelf != null)
                {
                    Object.Destroy(legacyOnSelf.gameObject);
                }

                Transform legacyMaskOnSelf = transform.Find(SelectionCheckMaskRootName);
                if (legacyMaskOnSelf != null && legacyMaskOnSelf != hostRect.Find(SelectionCheckMaskRootName))
                {
                    Object.Destroy(legacyMaskOnSelf.gameObject);
                }
            }

            LHButton parentButton = GetComponentInParent<LHButton>();
            if (parentButton != null && parentButton.transform != hostRect)
            {
                Transform legacyOnButton = parentButton.transform.Find(SelectionCheckMarkRootName);
                if (legacyOnButton != null)
                {
                    Object.Destroy(legacyOnButton.gameObject);
                }

                Transform legacyMaskOnButton = parentButton.transform.Find(SelectionCheckMaskRootName);
                if (legacyMaskOnButton != null)
                {
                    Object.Destroy(legacyMaskOnButton.gameObject);
                }
            }
        }

        private static void EnsureMaskComponent(GameObject maskObject, RectTransform hostRect)
        {
            if (maskObject == null || hostRect == null)
            {
                return;
            }

            Image maskImage = maskObject.GetComponent<Image>();
            if (maskImage == null)
            {
                maskImage = maskObject.AddComponent<Image>();
            }

            Image hostImage = hostRect.GetComponent<Image>();
            if (hostImage != null && hostImage.sprite != null)
            {
                maskImage.sprite = hostImage.sprite;
                maskImage.type = hostImage.type;
                maskImage.pixelsPerUnitMultiplier = hostImage.pixelsPerUnitMultiplier;
            }
            else
            {
                maskImage.sprite = CreateWhiteSprite();
                maskImage.type = Image.Type.Simple;
            }

            maskImage.color = Color.white;
            maskImage.raycastTarget = false;

            Mask mask = maskObject.GetComponent<Mask>();
            if (mask == null)
            {
                mask = maskObject.AddComponent<Mask>();
            }

            mask.showMaskGraphic = false;
        }

        private void ApplySelectionCheckMaskLayout()
        {
            if (selectionCheckMaskRoot != null)
            {
                IgnoreParentLayout(selectionCheckMaskRoot);
                ApplyStretchOverlayLayout(selectionCheckMaskRoot.transform as RectTransform);
                selectionCheckMaskRoot.transform.SetAsLastSibling();
                RectTransform hostRect = ResolveSelectionCheckHost();
                if (hostRect != null)
                {
                    EnsureMaskComponent(selectionCheckMaskRoot, hostRect);
                }
            }

            if (selectionCheckRoot != null)
            {
                IgnoreParentLayout(selectionCheckRoot);
                ApplyStretchOverlayLayout(selectionCheckRoot.transform as RectTransform);
            }

            if (selectionCheckOverlay != null)
            {
                selectionCheckOverlay.maskable = true;
                selectionCheckOverlay.raycastTarget = false;
            }

            if (selectionCheckIcon != null)
            {
                IgnoreParentLayout(selectionCheckIcon.gameObject);
                selectionCheckIcon.maskable = true;
                selectionCheckIcon.raycastTarget = false;
                ApplyCenteredCheckIconLayout(selectionCheckIcon.rectTransform);
            }
        }

        private static void IgnoreParentLayout(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            LayoutElement layoutElement = target.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = target.AddComponent<LayoutElement>();
            }

            layoutElement.ignoreLayout = true;
            layoutElement.minWidth = -1f;
            layoutElement.minHeight = -1f;
            layoutElement.preferredWidth = -1f;
            layoutElement.preferredHeight = -1f;
            layoutElement.flexibleWidth = -1f;
            layoutElement.flexibleHeight = -1f;
        }

        private static void ApplyStretchOverlayLayout(RectTransform overlayRect)
        {
            if (overlayRect == null)
            {
                return;
            }

            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.pivot = new Vector2(0.5f, 0.5f);
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            overlayRect.anchoredPosition = Vector2.zero;
            overlayRect.sizeDelta = Vector2.zero;
            overlayRect.localScale = Vector3.one;
            overlayRect.localRotation = Quaternion.identity;
        }

        private static void ApplyCenteredCheckIconLayout(RectTransform iconRect)
        {
            if (iconRect == null)
            {
                return;
            }

            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = new Vector2(SelectionCheckIconSize, SelectionCheckIconSize);
            iconRect.localScale = Vector3.one;
            iconRect.localRotation = Quaternion.identity;
        }

        private RectTransform ResolveSelectionCheckHost()
        {
            // 見た目のスロット本体(行コンテンツ)の中央に合わせる
            if (transform is RectTransform selfRect)
            {
                return selfRect;
            }

            LHButton button = GetComponentInParent<LHButton>();
            return button != null ? button.transform as RectTransform : null;
        }

        private void ApplySelectionMarkVisual()
        {
            if (nameText != null)
            {
                const string LegacyMarkPrefix = "★ ";
                string current = nameText.text ?? string.Empty;
                if (current.StartsWith(LegacyMarkPrefix, System.StringComparison.Ordinal))
                {
                    nameText.text = current.Substring(LegacyMarkPrefix.Length);
                }
            }

            if (!isSelectionMarked)
            {
                if (selectionCheckMaskRoot != null)
                {
                    selectionCheckMaskRoot.SetActive(false);
                }
                else if (selectionCheckRoot != null)
                {
                    selectionCheckRoot.SetActive(false);
                }

                return;
            }

            EnsureSelectionCheckMark();
            if (selectionCheckMaskRoot != null)
            {
                selectionCheckMaskRoot.SetActive(true);
            }

            if (selectionCheckRoot != null)
            {
                selectionCheckRoot.SetActive(true);
            }

            ApplySelectionCheckMaskLayout();

            if (selectionCheckOverlay != null)
            {
                selectionCheckOverlay.enabled = true;
            }

            if (selectionCheckIcon != null)
            {
                selectionCheckIcon.enabled = true;
            }
        }

        private static Sprite CreateWhiteSprite()
        {
            return Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                100f);
        }

        private static Sprite GetOrCreateCheckIconSprite()
        {
            const int spriteVersion = 2;
            if (sharedCheckIconSprite != null && sharedCheckIconVersion == spriteVersion)
            {
                return sharedCheckIconSprite;
            }

            if (sharedCheckIconSprite != null)
            {
                if (sharedCheckIconSprite.texture != null)
                {
                    Object.Destroy(sharedCheckIconSprite.texture);
                }

                Object.Destroy(sharedCheckIconSprite);
                sharedCheckIconSprite = null;
            }

            sharedCheckIconVersion = spriteVersion;
            const int size = SelectionCheckIconSize;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            Color32 clear = new Color32(0, 0, 0, 0);
            Color32[] pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = clear;
            }

            // 緑の丸背景
            Color32 fill = new Color32(36, 180, 72, 255);
            Color32 stroke = new Color32(255, 255, 255, 255);
            float center = (size - 1) * 0.5f;
            float radius = size * 0.46f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    if ((dx * dx) + (dy * dy) <= radius * radius)
                    {
                        pixels[(y * size) + x] = fill;
                    }
                }
            }

            // 白い太めのチェック
            DrawCheckStroke(pixels, size, stroke, thickness: 10);
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            sharedCheckIconSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f);
            sharedCheckIconSprite.name = "DesktopPetSelectionCheckIcon";
            return sharedCheckIconSprite;
        }

        private static void DrawCheckStroke(Color32[] pixels, int size, Color32 color, int thickness)
        {
            // チェック図形をテクスチャ中央付近へ寄せる
            Vector2 start = new Vector2(size * 0.24f, size * 0.48f);
            Vector2 mid = new Vector2(size * 0.42f, size * 0.34f);
            Vector2 end = new Vector2(size * 0.76f, size * 0.66f);
            DrawThickLine(pixels, size, start, mid, color, thickness);
            DrawThickLine(pixels, size, mid, end, color, thickness);
        }

        private static void DrawThickLine(
            Color32[] pixels,
            int size,
            Vector2 from,
            Vector2 to,
            Color32 color,
            int thickness)
        {
            float length = Vector2.Distance(from, to);
            int steps = Mathf.Max(1, Mathf.CeilToInt(length));
            float half = thickness * 0.5f;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                Vector2 point = Vector2.Lerp(from, to, t);
                int minX = Mathf.Max(0, Mathf.FloorToInt(point.x - half));
                int maxX = Mathf.Min(size - 1, Mathf.CeilToInt(point.x + half));
                int minY = Mathf.Max(0, Mathf.FloorToInt(point.y - half));
                int maxY = Mathf.Min(size - 1, Mathf.CeilToInt(point.y + half));
                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        float dx = x - point.x;
                        float dy = y - point.y;
                        if ((dx * dx) + (dy * dy) <= half * half)
                        {
                            pixels[(y * size) + x] = color;
                        }
                    }
                }
            }
        }

        private TrainingResumeAttacksContentView ResolveAttacksPanel()
        {
            return attacksContainer != null
                ? attacksContainer.GetComponent<TrainingResumeAttacksContentView>()
                : null;
        }

        private static List<MotionType> CollectAttackMotions(IReadOnlyList<MotionType> motions)
        {
            var attacks = new List<MotionType>(ModelSaveSlotRowUiBuilder.ConfirmAttackSlotCount);
            if (motions == null)
            {
                return attacks;
            }

            int count = Mathf.Min(motions.Count, ModelSaveSlotRowUiBuilder.ConfirmAttackSlotCount);
            for (int i = 0; i < count; i++)
            {
                attacks.Add(motions[i]);
            }

            return attacks;
        }
    }
}
