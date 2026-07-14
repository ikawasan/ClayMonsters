using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.ClayEditor.View
{
    /// <summary>
    /// シーン配置済みスロット行のUI参照を保持する
    /// ModelSaveSlotRowUiBuilderのRowElementsへ変換する
    /// </summary>
    public sealed class ModelSaveSlotRowElementRefs : MonoBehaviour
    {
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
        [SerializeField] private ModelSaveSlotRowUiBuilder.ConfirmLayoutSize confirmLayoutSize =
            ModelSaveSlotRowUiBuilder.ConfirmLayoutSize.Standard;

        private void Awake()
        {
            EnsureThumbnailVisuals();
        }

        /// <summary>
        /// サムネイル枠と内側背景を未設定時のみ適用する
        /// </summary>
        public void EnsureThumbnailVisuals()
        {
            if (thumbnailFrame != null)
            {
                Image frameImage = thumbnailFrame.GetComponent<Image>();
                if (frameImage != null && frameImage.sprite == null)
                {
                    TitleClayUiVisualUtility.ApplySlotThumbnailFrame(frameImage);
                }
            }

            if (thumbnailImage != null && thumbnailImage.sprite == null)
            {
                TitleClayUiVisualUtility.ApplySlotEmptyImage(thumbnailImage);
            }
        }

        /// <summary>
        /// セーブスロット一覧向けの横長レイアウトを適用する
        /// </summary>
        public void EnsureScrollListLayout()
        {
            thumbnailColumnWidth = ModelSaveSlotRowUiBuilder.ScrollListThumbnailColumnWidth;
            useConfirmLayout = false;
            confirmLayoutSize = ModelSaveSlotRowUiBuilder.ConfirmLayoutSize.Standard;
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
            confirmLayoutSize = ModelSaveSlotRowUiBuilder.ConfirmLayoutSize.Preview;
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
                confirmLayoutSize: confirmLayoutSize);
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
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>
        /// 子階層から参照を収集する
        /// </summary>
        public void CaptureFromHierarchy(Transform rowContentRoot)
        {
            if (!ModelSaveSlotRowUiBuilder.TryBindRowElements(
                    rowContentRoot,
                    thumbnailColumnWidth,
                    useConfirmLayout,
                    confirmLayoutSize,
                    out ModelSaveSlotRowUiBuilder.RowElements rowElements))
            {
                Debug.LogError($"[ModelSaveSlotRowElementRefs] bind failed: {name}", this);
                return;
            }

            ApplyBuiltRowElements(rowElements);
        }
    }
}
