using ClayEditor.Rigging;
using GameData;
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

        [SerializeField] private Image thumbnailImage;
        [SerializeField] private RectTransform thumbnailFrame;
        [SerializeField] private RectTransform thumbnailColumn;
        [SerializeField] private TMP_Text indexText;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text paramsText;
        [SerializeField] private TMP_Text subText;
        [SerializeField] private Image modelAttributeImage;
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
        private TMP_Text[] highlightTexts;

        private void Awake()
        {
            if (GetComponentInParent<ModelSaveConfirmView>(true) != null)
            {
                EnsureConfirmPrefabLayout();
            }

            HideAttributeRowDisplay();
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
            confirmLayoutSize = ModelSaveSlotRowUiBuilder.ConfirmLayoutSize.Standard;
        }

        /// <summary>
        /// ModelSaveConfirmView向けにプレハブ手動配置を維持する
        /// </summary>
        public void EnsureConfirmPrefabLayout()
        {
            useConfirmLayout = true;
            preservePrefabLayout = true;
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
            confirmLayoutSize = ModelSaveSlotRowUiBuilder.ConfirmLayoutSize.Preview;
        }

        /// <inheritdoc/>
        public void OnDeselect(BaseEventData eventData)
        {
            isSelected = false;
            ApplyHighlightState();
        }

        /// <inheritdoc/>
        public void OnPointerEnter(PointerEventData eventData)
        {
            isPointerInside = true;
            ApplyHighlightState();
        }

        /// <inheritdoc/>
        public void OnPointerExit(PointerEventData eventData)
        {
            isPointerInside = false;
            ApplyHighlightState();
        }

        /// <inheritdoc/>
        public void OnSelect(BaseEventData eventData)
        {
            isSelected = true;
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

            HideAttributeRowDisplay();
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

            HideAttributeRowDisplay();
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

            HideAttributeRowDisplay();
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
                modelAttributeImage,
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
            modelAttributeImage = rowElements.ModelAttributeImage;
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
            CaptureHighlightTargets();
            bool highlighted = isPointerInside || isSelected;

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

            highlightTexts = GetComponentsInChildren<TMP_Text>(true);
            baseTextColors = new Color[highlightTexts.Length];
            for (int i = 0; i < highlightTexts.Length; i++)
            {
                baseTextColors[i] = highlightTexts[i].color;
            }

            Image[] rowImages = GetComponentsInChildren<Image>(true);
            var contentImages = new List<Image>(rowImages.Length);
            for (int i = 0; i < rowImages.Length; i++)
            {
                Image image = rowImages[i];
                if (image != null)
                {
                    contentImages.Add(image);
                }
            }

            highlightImages = contentImages.ToArray();
            baseImageColors = new Color[highlightImages.Length];
            for (int i = 0; i < highlightImages.Length; i++)
            {
                baseImageColors[i] = highlightImages[i].color;
            }

            hasCapturedHighlightTargets = true;
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
            ApplyHighlightState();
        }

        private void HideAttributeRowDisplay()
        {
            ModelSaveSlotRowUiBuilder.HideAttributeRowDisplay(ToRowElements());
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
