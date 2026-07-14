using Battle;
using ClayEditor.Rigging;
using Extensions;
using GameData;
using SaveData;
using System.Collections.Generic;
using TMPro;
using UI.Battle.View;
using UnityEngine;
using UnityEngine.UI;

namespace UI.ClayEditor.View
{
    /// <summary>
    /// セーブスロット行と同じ横長レイアウトを組み立てる
    /// 左にサムネイルと名前・ステータス、右に育成リザルト同型の技スロットを並べる
    /// </summary>
    public static class ModelSaveSlotRowUiBuilder
    {
        public const float RowMaxWidth = 1280f;
        public const float RowMinWidth = 960f;
        public const float RowMinHeight = 120f;
        public const float RowEmptyHeight = 96f;
        public const float DefaultThumbnailColumnWidth = 104f;
        public const float ConfirmThumbnailColumnWidth = 258f;
        public const float ScrollListThumbnailColumnWidth = 280f;
        public const int ConfirmAttackSlotCount = 4;
        public const float ConfirmPreviewRowWidth = 1320f;
        public const float ConfirmPreviewThumbnailColumnWidth = 520f;

        /// <summary>
        /// 保存確認画面のレイアウトサイズ
        /// </summary>
        public enum ConfirmLayoutSize
        {
            /// <summary>
            /// スロット一覧と同じ標準サイズ
            /// </summary>
            Standard,
            /// <summary>
            /// ClayEdit保存確認画面用の拡大サイズ
            /// </summary>
            Preview
        }

        internal readonly struct ConfirmLayoutMetrics
        {
            public ConfirmLayoutMetrics(
                float nameFontSize,
                float paramsFontSize,
                float attributeSquareSize,
                float attackChipHeight,
                float nameThumbnailSpacing,
                float attackChipNameFontSize,
                float attackChipDetailFontSize,
                float attackChipSpacing,
                float rangeBarWidth,
                float rangeBarHeight)
            {
                NameFontSize = nameFontSize;
                ParamsFontSize = paramsFontSize;
                AttributeSquareSize = attributeSquareSize;
                AttackChipHeight = attackChipHeight;
                NameThumbnailSpacing = nameThumbnailSpacing;
                AttackChipNameFontSize = attackChipNameFontSize;
                AttackChipDetailFontSize = attackChipDetailFontSize;
                AttackChipSpacing = attackChipSpacing;
                RangeBarWidth = rangeBarWidth;
                RangeBarHeight = rangeBarHeight;
            }

            public float NameFontSize { get; }

            public float ParamsFontSize { get; }

            public float AttributeSquareSize { get; }

            public float AttackChipHeight { get; }

            public float NameThumbnailSpacing { get; }

            public float AttackChipNameFontSize { get; }

            public float AttackChipDetailFontSize { get; }

            public float AttackChipSpacing { get; }

            public float RangeBarWidth { get; }

            public float RangeBarHeight { get; }

            public static ConfirmLayoutMetrics Standard { get; } = new ConfirmLayoutMetrics(
                36f,
                20f,
                44f,
                68f,
                6f,
                15f,
                13f,
                6f,
                MoveRangeSegmentBarView.CompactBarWidth,
                MoveRangeSegmentBarView.CompactBarHeight);

            public static ConfirmLayoutMetrics Preview { get; } = new ConfirmLayoutMetrics(
                48f,
                40f,
                72f,
                148f,
                14f,
                30f,
                26f,
                14f,
                104f,
                18f);

            public static ConfirmLayoutMetrics Resolve(ConfirmLayoutSize size)
            {
                return size == ConfirmLayoutSize.Preview ? Preview : Standard;
            }
        }

        private const float LeftInfoMinWidth = 140f;
        private const float LeftInfoMaxWidth = 200f;
        private const float AttacksColumnMinWidth = 520f;
        private const float ColumnSpacing = 12f;
        private const float IndexFontSize = 13f;
        private const float NameFontSize = 24f;
        private const float ParamsFontSize = 16f;
        private const int ParamsLineCount = 3;
        private const int ConfirmParamsLineCount = 4;
        private const float StatusTopSpacing = 10f;
        private const float AttributeSquareSize = 22f;
        private const float ResumeAttackSlotHeight = 108f;
        private const float ResumeAttackSlotSpacing = 8f;
        private const float AttacksStackBottomPadding = 8f;
        private const float RowContentPadding = 12f;
        private const float SlotRowInnerVerticalPadding = 24f;
        private const float NameRowVerticalSafetyPadding = 8f;
        private const string DataRowName = "DataRow";
        private const string NameHeaderRowName = "NameHeaderRow";

        private static float ResolveResumeAttacksStackHeight(int attackCount)
        {
            if (attackCount <= 0)
            {
                return 0f;
            }

            int slotCount = Mathf.Min(attackCount, TrainingResumeAttacksContentView.AttackSlotCount);
            return (slotCount * ResumeAttackSlotHeight)
                + (Mathf.Max(0, slotCount - 1) * ResumeAttackSlotSpacing)
                + AttacksStackBottomPadding;
        }

        private static float ResolveConfirmAttacksColumnHeight(int attackCount, ConfirmLayoutMetrics metrics)
        {
            if (attackCount <= 0)
            {
                return 0f;
            }

            return ResolveConfirmAttacksStackHeight(attackCount, metrics);
        }

        private static float IndexRowHeight => ResolveTextHeight(IndexFontSize);

        private static float NameRowHeight => ResolveTextHeight(NameFontSize);

        private static float NameRowContainerHeight => Mathf.Max(NameRowHeight, AttributeSquareSize);

        private static float StatusBlockHeight => ResolveTextHeight(ParamsFontSize, ParamsLineCount);

        private static float ResolveLeftSectionHeight()
        {
            return IndexRowHeight + NameRowContainerHeight + StatusTopSpacing + StatusBlockHeight + 6f;
        }

        private static float ConfirmStatusBlockHeight(ConfirmLayoutMetrics metrics) =>
            ResolveTextHeight(metrics.ParamsFontSize, ConfirmParamsLineCount);

        private static float ConfirmAttributeRowHeight(ConfirmLayoutMetrics metrics) =>
            Mathf.Max(ResolveTextHeight(metrics.ParamsFontSize), metrics.AttributeSquareSize);

        private static float ConfirmParamsSectionHeight(ConfirmLayoutMetrics metrics) =>
            ConfirmStatusBlockHeight(metrics);

        private static void ApplyDataRegionColumnLayout(RowElements row, float dataRegionHeight)
        {
            PrepareParamsColumnContent(row);

            if (row.LeftInfoColumn != null)
            {
                LayoutElement leftLayout = row.LeftInfoColumn.GetComponent<LayoutElement>();
                if (leftLayout != null)
                {
                    leftLayout.minHeight = dataRegionHeight;
                    leftLayout.preferredHeight = dataRegionHeight;
                    leftLayout.flexibleHeight = IsRestructuredLayout(row) ? 1f : 0f;
                }

                VerticalLayoutGroup leftLayoutGroup = row.LeftInfoColumn.GetComponent<VerticalLayoutGroup>();
                if (leftLayoutGroup != null)
                {
                    leftLayoutGroup.childAlignment = TextAnchor.UpperLeft;
                    leftLayoutGroup.spacing = 0f;
                    leftLayoutGroup.padding = new RectOffset(0, 0, 0, 0);
                }
            }

            if (row.AttacksContainer != null)
            {
                LayoutElement attacksLayout = row.AttacksContainer.GetComponent<LayoutElement>();
                if (attacksLayout != null)
                {
                    attacksLayout.minHeight = dataRegionHeight;
                    attacksLayout.preferredHeight = dataRegionHeight;
                    attacksLayout.flexibleHeight = IsRestructuredLayout(row) ? 1f : 0f;
                }

                VerticalLayoutGroup attacksLayoutGroup = row.AttacksContainer.GetComponent<VerticalLayoutGroup>();
                if (attacksLayoutGroup != null)
                {
                    attacksLayoutGroup.childAlignment = TextAnchor.UpperLeft;
                    attacksLayoutGroup.spacing = ResumeAttackSlotSpacing;
                    attacksLayoutGroup.padding = new RectOffset(0, 0, 0, 0);
                }
            }
        }

        private static void PrepareParamsColumnContent(RowElements row)
        {
            if (row.LeftInfoColumn == null || row.ParamsText == null)
            {
                return;
            }

            for (int i = 0; i < row.LeftInfoColumn.childCount; i++)
            {
                Transform child = row.LeftInfoColumn.GetChild(i);
                if (child == row.ParamsText.transform)
                {
                    continue;
                }

                child.gameObject.SetActive(false);
                LayoutElement hiddenLayout = child.GetComponent<LayoutElement>();
                if (hiddenLayout == null)
                {
                    hiddenLayout = child.gameObject.AddComponent<LayoutElement>();
                }

                hiddenLayout.ignoreLayout = true;
            }

            row.ParamsText.gameObject.SetActive(true);
            SetIndexRowVisible(row, false);
            SetAttributeRowVisible(row, false);
            if (row.SubText != null)
            {
                row.SubText.gameObject.SetActive(false);
            }
        }

        private static float ConfirmParamsTopOffset(ConfirmLayoutMetrics metrics) =>
            ResolveConfirmNameRowHeight(metrics) + metrics.NameThumbnailSpacing;

        private static float ResolveConfirmNameRowHeight(ConfirmLayoutMetrics metrics) =>
            ResolveTextHeight(metrics.NameFontSize) + NameRowVerticalSafetyPadding;

        private static float ResolveConfirmThumbnailColumnHeight(
            float thumbnailColumnWidth,
            ConfirmLayoutMetrics metrics,
            int attackCount = 0)
        {
            return ResolveConfirmRowContentHeight(attackCount, thumbnailColumnWidth, metrics);
        }

        private static float ResolveConfirmDataRegionHeight(int attackCount, ConfirmLayoutMetrics metrics)
        {
            float statsHeight = ConfirmStatusBlockHeight(metrics);
            float attacksStack = ResolveConfirmAttacksStackHeight(attackCount, metrics);
            return Mathf.Max(attacksStack, statsHeight, 72f);
        }

        private static float ResolveConfirmRowContentHeight(
            int attackCount,
            float thumbnailColumnWidth,
            ConfirmLayoutMetrics metrics)
        {
            return ResolveConfirmNameRowHeight(metrics)
                + ResolveConfirmDataRegionHeight(attackCount, metrics);
        }

        private static float ResolveConfirmLeftSectionHeight(
            float thumbnailColumnWidth,
            ConfirmLayoutMetrics metrics,
            int attackCount)
        {
            return ResolveConfirmThumbnailColumnHeight(thumbnailColumnWidth, metrics, attackCount);
        }

        private static float ResolveConfirmAttacksStackHeight(int attackCount, ConfirmLayoutMetrics metrics)
        {
            _ = metrics;
            return ResolveResumeAttacksStackHeight(attackCount);
        }

        private static float ResolveConfirmContentHeight(
            int attackCount,
            float thumbnailColumnWidth,
            ConfirmLayoutMetrics metrics)
        {
            return ResolveConfirmRowContentHeight(attackCount, thumbnailColumnWidth, metrics);
        }

        /// <summary>
        /// サムネイル列と枠をスロット行の内側高さいっぱいに広げる
        /// </summary>
        public static void ApplyThumbnailSlotHeight(RowElements row, int attackCount, float totalRowHeight = -1f)
        {
            if (row?.ThumbnailFrame == null)
            {
                return;
            }

            EnsureThreeColumnLayout(row);

            float thumbWidth = row.ThumbnailColumnWidth;
            float dataRegionHeight = ResolveThumbnailColumnHeight(row, attackCount, totalRowHeight);
            float frameHeight = ResolveThumbnailFrameHeight(row, dataRegionHeight);

            if (row.ThumbnailColumn != null)
            {
                LayoutElement thumbColumnLayout = row.ThumbnailColumn.GetComponent<LayoutElement>();
                if (thumbColumnLayout != null)
                {
                    thumbColumnLayout.flexibleWidth = 0f;
                    thumbColumnLayout.flexibleHeight = IsRestructuredLayout(row) ? 1f : 0f;
                    thumbColumnLayout.minWidth = thumbWidth;
                    thumbColumnLayout.preferredWidth = thumbWidth;
                    thumbColumnLayout.minHeight = dataRegionHeight;
                    thumbColumnLayout.preferredHeight = dataRegionHeight;
                }
            }

            LayoutElement frameLayout = row.ThumbnailFrame.GetComponent<LayoutElement>();
            if (frameLayout != null)
            {
                frameLayout.flexibleWidth = 0f;
                frameLayout.flexibleHeight = 0f;
                frameLayout.minWidth = thumbWidth;
                frameLayout.preferredWidth = thumbWidth;
                frameLayout.minHeight = frameHeight;
                frameLayout.preferredHeight = frameHeight;
            }

            RectTransform frameRect = row.ThumbnailFrame;
            frameRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, frameHeight);
            frameRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, thumbWidth);

            if (row.ThumbnailColumn != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(row.ThumbnailColumn);
                Transform slotRow = ResolveSlotRowTransform(row);
                if (slotRow is RectTransform slotRowRect)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(slotRowRect);
                }
            }

            if (row.LeftInfoColumn != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(row.LeftInfoColumn);
            }
        }

        private static float ResolveThumbnailColumnHeight(RowElements row, int attackCount, float totalRowHeight)
        {
            float dataRegionContent = ResolveConfirmDataRegionHeight(attackCount, row.ConfirmMetrics);

            if (totalRowHeight <= 0f)
            {
                return dataRegionContent;
            }

            float slotInnerHeight = Mathf.Max(0f, totalRowHeight - SlotRowInnerVerticalPadding);
            float nameHeight = ResolveConfirmNameRowHeight(row.ConfirmMetrics);
            float dataRegionFromSlot = Mathf.Max(72f, slotInnerHeight - nameHeight);
            return Mathf.Max(dataRegionContent, dataRegionFromSlot);
        }

        private static float ResolveThumbnailFrameHeight(RowElements row, float dataRegionHeight)
        {
            if (IsRestructuredLayout(row))
            {
                return dataRegionHeight;
            }

            float nameRowHeight = ResolveConfirmNameRowHeight(row.ConfirmMetrics);
            return Mathf.Max(72f, dataRegionHeight - nameRowHeight);
        }

        private static bool IsRestructuredLayout(RowElements row)
        {
            return row.ThumbnailColumn != null
                && row.ThumbnailColumn.parent != null
                && row.ThumbnailColumn.parent.name == DataRowName;
        }

        private static Transform ResolveSlotRowTransform(RowElements row)
        {
            if (row.ThumbnailColumn == null)
            {
                return null;
            }

            Transform parent = row.ThumbnailColumn.parent;
            if (parent != null && parent.name == DataRowName)
            {
                return parent.parent;
            }

            return parent;
        }

        private static void EnsureThreeColumnLayout(RowElements row)
        {
            if (!row.UseConfirmLayout || row.ThumbnailColumn == null)
            {
                return;
            }

            RestructureConfirmSlotRowLayout(row);
            RemoveNameRowFromParamsColumn(row);
            NormalizeThumbnailColumn(row);
            NormalizeParamsColumn(row);
            EnsureDataRowHorizontalStretch(row);
            ApplyNameHeaderLayout(row);
        }

        private static void EnsureDataRowHorizontalStretch(RowElements row)
        {
            if (!IsRestructuredLayout(row))
            {
                EnsureSlotRowHorizontalStretch(row);
                return;
            }

            Transform dataRow = row.ThumbnailColumn.parent;
            HorizontalLayoutGroup dataLayout = dataRow.GetComponent<HorizontalLayoutGroup>();
            if (dataLayout != null)
            {
                dataLayout.childForceExpandHeight = true;
                dataLayout.childControlHeight = true;
            }
        }

        private static void ApplyNameHeaderLayout(RowElements row)
        {
            Transform slotRow = ResolveSlotRowTransform(row);
            if (slotRow == null)
            {
                return;
            }

            Transform nameHeader = slotRow.Find(NameHeaderRowName);
            if (nameHeader == null)
            {
                return;
            }

            float nameRowHeight = ResolveConfirmNameRowHeight(row.ConfirmMetrics);
            LayoutElement headerLayout = nameHeader.GetComponent<LayoutElement>();
            if (headerLayout == null)
            {
                headerLayout = nameHeader.gameObject.AddComponent<LayoutElement>();
            }

            headerLayout.flexibleWidth = 1f;
            headerLayout.flexibleHeight = 0f;
            headerLayout.minHeight = nameRowHeight;
            headerLayout.preferredHeight = nameRowHeight;

            Transform nameRow = nameHeader.Find("NameRow");
            if (nameRow != null)
            {
                LayoutElement nameRowLayout = nameRow.GetComponent<LayoutElement>();
                if (nameRowLayout != null)
                {
                    nameRowLayout.flexibleWidth = 0f;
                    nameRowLayout.flexibleHeight = 0f;
                    nameRowLayout.minWidth = row.ThumbnailColumnWidth;
                    nameRowLayout.preferredWidth = row.ThumbnailColumnWidth;
                    nameRowLayout.minHeight = nameRowHeight;
                    nameRowLayout.preferredHeight = nameRowHeight;
                }
            }
        }

        private static void RestructureConfirmSlotRowLayout(RowElements row)
        {
            if (IsRestructuredLayout(row))
            {
                return;
            }

            Transform slotRow = ResolveSlotRowTransform(row);
            if (slotRow == null)
            {
                return;
            }

            HorizontalLayoutGroup slotHBox = slotRow.GetComponent<HorizontalLayoutGroup>();
            float columnSpacing = slotHBox != null ? slotHBox.spacing : ColumnSpacing;

            var dataRowObject = new GameObject(
                DataRowName,
                typeof(RectTransform),
                typeof(HorizontalLayoutGroup),
                typeof(LayoutElement));
            dataRowObject.transform.SetParent(slotRow, false);

            HorizontalLayoutGroup dataHBox = dataRowObject.GetComponent<HorizontalLayoutGroup>();
            dataHBox.padding = new RectOffset(0, 0, 0, 0);
            dataHBox.spacing = columnSpacing;
            dataHBox.childAlignment = TextAnchor.UpperLeft;
            dataHBox.childControlWidth = true;
            dataHBox.childControlHeight = true;
            dataHBox.childForceExpandWidth = false;
            dataHBox.childForceExpandHeight = true;

            LayoutElement dataRowLayout = dataRowObject.GetComponent<LayoutElement>();
            dataRowLayout.flexibleWidth = 1f;
            dataRowLayout.flexibleHeight = 1f;
            dataRowLayout.minHeight = 0f;

            row.ThumbnailColumn.SetParent(dataRowObject.transform, false);
            if (row.LeftInfoColumn != null)
            {
                row.LeftInfoColumn.SetParent(dataRowObject.transform, false);
            }

            if (row.AttacksContainer != null)
            {
                row.AttacksContainer.SetParent(dataRowObject.transform, false);
            }

            Transform nameRow = row.ThumbnailColumn.Find("NameRow");
            if (nameRow == null && row.NameText != null)
            {
                nameRow = row.NameText.transform.parent;
            }

            var nameHeaderObject = new GameObject(
                NameHeaderRowName,
                typeof(RectTransform),
                typeof(HorizontalLayoutGroup),
                typeof(LayoutElement));
            nameHeaderObject.transform.SetParent(slotRow, false);
            nameHeaderObject.transform.SetAsFirstSibling();

            HorizontalLayoutGroup nameHeaderGroup = nameHeaderObject.GetComponent<HorizontalLayoutGroup>();
            nameHeaderGroup.spacing = 0f;
            nameHeaderGroup.padding = new RectOffset(0, 0, 0, 0);
            nameHeaderGroup.childAlignment = TextAnchor.UpperLeft;
            nameHeaderGroup.childControlWidth = true;
            nameHeaderGroup.childControlHeight = true;
            nameHeaderGroup.childForceExpandWidth = false;
            nameHeaderGroup.childForceExpandHeight = false;

            if (nameRow != null)
            {
                nameRow.SetParent(nameHeaderObject.transform, false);
                nameRow.SetAsFirstSibling();
            }

            var spacerObject = new GameObject(
                "NameHeaderSpacer",
                typeof(RectTransform),
                typeof(LayoutElement));
            spacerObject.transform.SetParent(nameHeaderObject.transform, false);
            LayoutElement spacerLayout = spacerObject.GetComponent<LayoutElement>();
            spacerLayout.flexibleWidth = 1f;
            spacerLayout.flexibleHeight = 0f;
            spacerLayout.minWidth = 0f;
            spacerLayout.preferredWidth = 0f;

            ReplaceHorizontalLayoutWithVertical(slotRow.gameObject);

            dataRowObject.transform.SetSiblingIndex(1);
        }

        private static void EnsureSlotRowHorizontalStretch(RowElements row)
        {
            Transform slotRow = row.ThumbnailColumn.parent;
            if (slotRow == null)
            {
                return;
            }

            HorizontalLayoutGroup slotLayout = slotRow.GetComponent<HorizontalLayoutGroup>();
            if (slotLayout == null)
            {
                return;
            }

            slotLayout.childForceExpandHeight = true;
            slotLayout.childControlHeight = true;
        }

        private static void RemoveNameRowFromParamsColumn(RowElements row)
        {
            if (row.LeftInfoColumn == null)
            {
                return;
            }

            Transform nameInParams = row.LeftInfoColumn.Find("NameRow");
            if (nameInParams != null)
            {
                Object.Destroy(nameInParams.gameObject);
            }
        }

        private static void NormalizeThumbnailColumn(RowElements row)
        {
            VerticalLayoutGroup columnLayoutGroup = row.ThumbnailColumn.GetComponent<VerticalLayoutGroup>();
            if (columnLayoutGroup != null)
            {
                columnLayoutGroup.spacing = 0f;
                columnLayoutGroup.padding = new RectOffset(0, 0, 0, 0);
                columnLayoutGroup.childForceExpandWidth = true;
                bool thumbnailOnly = row.ThumbnailColumn.Find("NameRow") == null;
                columnLayoutGroup.childForceExpandHeight = thumbnailOnly;
            }

            LayoutElement thumbColumnLayout = row.ThumbnailColumn.GetComponent<LayoutElement>();
            if (thumbColumnLayout != null)
            {
                thumbColumnLayout.flexibleHeight = 1f;
            }

            if (row.ThumbnailFrame == null)
            {
                return;
            }

            LayoutElement frameLayout = row.ThumbnailFrame.GetComponent<LayoutElement>();
            if (frameLayout != null)
            {
                frameLayout.flexibleWidth = 0f;
                frameLayout.flexibleHeight = 1f;
            }
        }

        private static void NormalizeParamsColumn(RowElements row)
        {
            if (row.LeftInfoColumn == null)
            {
                return;
            }

            PrepareParamsColumnContent(row);
        }

        private static float ResolveTextHeight(float fontSize, int lineCount = 1)
        {
            return Mathf.Ceil(fontSize * 1.5f) * lineCount;
        }

        /// <summary>
        /// スロット行のUI要素参照
        /// </summary>
        public sealed class RowElements
        {
            internal RowElements(
                Image thumbnailImage,
                RectTransform thumbnailFrame,
                TMP_Text indexText,
                TMP_Text nameText,
                TMP_Text paramsText,
                TMP_Text subText,
                Image modelAttributeImage,
                RectTransform leftInfoColumn,
                RectTransform attacksContainer,
                float thumbnailColumnWidth,
                bool useSquareThumbnail,
                bool useConfirmLayout = false,
                RectTransform thumbnailColumn = null,
                ConfirmLayoutSize confirmLayoutSize = ConfirmLayoutSize.Standard)
            {
                ThumbnailImage = thumbnailImage;
                ThumbnailFrame = thumbnailFrame;
                IndexText = indexText;
                NameText = nameText;
                ParamsText = paramsText;
                SubText = subText;
                ModelAttributeImage = modelAttributeImage;
                LeftInfoColumn = leftInfoColumn;
                AttacksContainer = attacksContainer;
                ThumbnailColumnWidth = thumbnailColumnWidth;
                UseSquareThumbnail = useSquareThumbnail;
                UseConfirmLayout = useConfirmLayout;
                ThumbnailColumn = thumbnailColumn;
                ConfirmMetrics = useConfirmLayout
                    ? ConfirmLayoutMetrics.Resolve(confirmLayoutSize)
                    : ConfirmLayoutMetrics.Standard;
                ConfirmLayoutSize = confirmLayoutSize;
                AttacksPanel = attacksContainer != null
                    ? attacksContainer.GetComponent<TrainingResumeAttacksContentView>()
                    : null;
            }

            public int ActiveAttackCount { get; private set; }

            internal void SetActiveAttackCount(int attackCount)
            {
                ActiveAttackCount = Mathf.Max(0, attackCount);
            }

            public float ThumbnailColumnWidth { get; private set; } = DefaultThumbnailColumnWidth;

            public bool UseSquareThumbnail { get; private set; }

            public bool UseConfirmLayout { get; private set; }

            public Image ThumbnailImage { get; }

            public RectTransform ThumbnailFrame { get; }

            public RectTransform ThumbnailColumn { get; }

            public TMP_Text IndexText { get; }

            public TMP_Text NameText { get; }

            public TMP_Text ParamsText { get; }

            public TMP_Text SubText { get; }

            public Image ModelAttributeImage { get; }

            public RectTransform LeftInfoColumn { get; }

            public RectTransform AttacksContainer { get; }

            public TrainingResumeAttacksContentView AttacksPanel { get; }

            internal ConfirmLayoutMetrics ConfirmMetrics { get; }

            internal ConfirmLayoutSize ConfirmLayoutSize { get; }

            internal void SetThumbnailColumnWidth(float width)
            {
                ThumbnailColumnWidth = width;
            }
        }

        /// <summary>
        /// 利用可能幅から行幅を決める
        /// </summary>
        public static float ResolveRowWidth(float availableWidth, float listPadding = 0f)
        {
            float available = availableWidth - (listPadding * 2f);
            if (available <= 0f)
            {
                return RowMaxWidth;
            }

            return Mathf.Clamp(available, RowMinWidth, RowMaxWidth);
        }

        /// <summary>
        /// 攻撃数から行の高さを計算する
        /// </summary>
        public static float CalculateRowHeight(int attackCount)
        {
            float leftSectionHeight = ResolveLeftSectionHeight();
            if (attackCount <= 0)
            {
                return Mathf.Max(RowEmptyHeight, leftSectionHeight + (RowContentPadding * 2f));
            }

            float attacksHeight = ResolveResumeAttacksStackHeight(attackCount);
            float contentHeight = Mathf.Max(leftSectionHeight, attacksHeight);
            return Mathf.Max(RowMinHeight, contentHeight + (RowContentPadding * 2f));
        }

        /// <summary>
        /// 保存確認画面の攻撃数から行の高さを計算する
        /// </summary>
        public static float CalculateConfirmRowHeight(
            int attackCount,
            float thumbnailColumnWidth = ConfirmThumbnailColumnWidth,
            ConfirmLayoutSize layoutSize = ConfirmLayoutSize.Standard)
        {
            ConfirmLayoutMetrics metrics = ConfirmLayoutMetrics.Resolve(layoutSize);
            float contentHeight = ResolveConfirmContentHeight(attackCount, thumbnailColumnWidth, metrics);
            return Mathf.Max(RowMinHeight, contentHeight + (RowContentPadding * 2f));
        }

        /// <summary>
        /// 横並び親の子としてスロット行コンテンツを生成する
        /// </summary>
        public static RowElements BuildContent(
            Transform parent,
            float rowWidth,
            float thumbnailColumnWidth = DefaultThumbnailColumnWidth,
            bool useSquareThumbnail = false)
        {
            RectTransform thumbnailFrame = CreateThumbnailImage(
                parent,
                out Image thumbnailImage,
                thumbnailColumnWidth,
                useSquareThumbnail);
            return CreateRowColumns(
                parent,
                thumbnailImage,
                thumbnailFrame,
                rowWidth,
                thumbnailColumnWidth,
                useSquareThumbnail);
        }

        /// <summary>
        /// 保存確認画面向けに名前付きサムネイル列と縦並びステータスを生成する
        /// </summary>
        public static RowElements BuildConfirmContent(
            Transform parent,
            float rowWidth,
            float thumbnailColumnWidth = ConfirmThumbnailColumnWidth,
            ConfirmLayoutSize layoutSize = ConfirmLayoutSize.Standard)
        {
            ConfirmLayoutMetrics metrics = ConfirmLayoutMetrics.Resolve(layoutSize);
            EnsureSlotRowVerticalLayout(parent);
            RectTransform dataRow = CreateConfirmDataRow(parent);
            RectTransform nameHeader = CreateConfirmNameHeaderRow(
                parent,
                thumbnailColumnWidth,
                metrics,
                out TMP_Text nameText);

            RectTransform thumbnailColumn = CreateConfirmThumbnailOnlyColumn(
                dataRow,
                thumbnailColumnWidth,
                metrics,
                out Image thumbnailImage,
                out RectTransform thumbnailFrame);

            RectTransform leftInfoColumn = CreateConfirmParamsColumn(
                dataRow,
                rowWidth,
                thumbnailColumnWidth,
                metrics,
                out TMP_Text paramsText,
                out TMP_Text subText,
                out TMP_Text indexText,
                out Image modelAttributeImage);

            RectTransform attacksContainer = CreateConfirmAttacksContainer(
                dataRow,
                rowWidth,
                thumbnailColumnWidth,
                metrics);

            nameHeader.SetAsFirstSibling();
            dataRow.SetSiblingIndex(1);

            return new RowElements(
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
                useSquareThumbnail: false,
                useConfirmLayout: true,
                thumbnailColumn: thumbnailColumn,
                confirmLayoutSize: layoutSize);
        }

        /// <summary>
        /// シーン配置済み行からRowElementsを収集する
        /// </summary>
        public static bool TryBindRowElements(
            Transform rowContentRoot,
            float thumbnailColumnWidth,
            bool useConfirmLayout,
            ConfirmLayoutSize layoutSize,
            out RowElements rowElements)
        {
            rowElements = null;
            if (rowContentRoot == null)
            {
                return false;
            }

            RectTransform thumbnailColumnTransform = FindSlotDescendant(rowContentRoot, "ThumbnailColumn") as RectTransform;
            RectTransform thumbnailFrameTransform = thumbnailColumnTransform != null
                ? thumbnailColumnTransform.Find("ThumbnailFrame") as RectTransform
                : FindSlotDescendant(rowContentRoot, "ThumbnailFrame") as RectTransform;
            Image thumbnailImageComponent = thumbnailFrameTransform != null
                ? thumbnailFrameTransform.Find("Thumbnail")?.GetComponent<Image>()
                : FindSlotDescendant(rowContentRoot, "Thumbnail")?.GetComponent<Image>();
            RectTransform leftInfoColumnTransform = FindSlotDescendant(rowContentRoot, "LeftInfoColumn") as RectTransform;
            TMP_Text nameTextComponent = null;
            Transform nameHeaderTransform = rowContentRoot.Find(NameHeaderRowName);
            if (nameHeaderTransform != null)
            {
                nameTextComponent = nameHeaderTransform.Find("NameRow/NameText")?.GetComponent<TMP_Text>();
            }

            if (nameTextComponent == null && thumbnailColumnTransform != null)
            {
                nameTextComponent = thumbnailColumnTransform.Find("NameRow/NameText")?.GetComponent<TMP_Text>();
            }

            if (nameTextComponent == null && leftInfoColumnTransform != null)
            {
                nameTextComponent = leftInfoColumnTransform.Find("NameRow/NameText")?.GetComponent<TMP_Text>();
            }

            if (nameTextComponent == null)
            {
                Transform nameRow = FindSlotDescendant(rowContentRoot, "NameRow");
                nameTextComponent = nameRow != null
                    ? nameRow.Find("NameText")?.GetComponent<TMP_Text>()
                    : null;
            }

            TMP_Text indexTextComponent = leftInfoColumnTransform != null
                ? leftInfoColumnTransform.Find("IndexRow/IndexText")?.GetComponent<TMP_Text>()
                : null;
            TMP_Text paramsTextComponent = leftInfoColumnTransform != null
                ? leftInfoColumnTransform.Find("ParamsText")?.GetComponent<TMP_Text>()
                : null;
            TMP_Text subTextComponent = leftInfoColumnTransform != null
                ? leftInfoColumnTransform.Find("SubText")?.GetComponent<TMP_Text>()
                : null;
            Image modelAttributeImageComponent = leftInfoColumnTransform != null
                ? leftInfoColumnTransform.Find("AttributeRow/ModelAttribute")?.GetComponent<Image>()
                : null;
            RectTransform attacksContainerTransform = FindSlotDescendant(rowContentRoot, "AttacksContainer") as RectTransform;
            if (attacksContainerTransform == null)
            {
                attacksContainerTransform = FindSlotDescendant(rowContentRoot, "AttacksColumn") as RectTransform;
            }

            if (thumbnailImageComponent == null
                || nameTextComponent == null
                || leftInfoColumnTransform == null
                || paramsTextComponent == null
                || attacksContainerTransform == null)
            {
                return false;
            }

            rowElements = new RowElements(
                thumbnailImageComponent,
                thumbnailFrameTransform,
                indexTextComponent,
                nameTextComponent,
                paramsTextComponent,
                subTextComponent,
                modelAttributeImageComponent,
                leftInfoColumnTransform,
                attacksContainerTransform,
                thumbnailColumnWidth,
                useSquareThumbnail: !useConfirmLayout,
                useConfirmLayout: useConfirmLayout,
                thumbnailColumn: thumbnailColumnTransform,
                confirmLayoutSize: layoutSize);

            return true;
        }

        private static Transform FindSlotDescendant(Transform rowRoot, string childName)
        {
            if (rowRoot == null || string.IsNullOrEmpty(childName))
            {
                return null;
            }

            Transform dataRow = rowRoot.Find(DataRowName);
            if (dataRow != null)
            {
                Transform inDataRow = dataRow.Find(childName);
                if (inDataRow != null)
                {
                    return inDataRow;
                }
            }

            Transform direct = rowRoot.Find(childName);
            if (direct != null)
            {
                return direct;
            }

            for (int i = 0; i < rowRoot.childCount; i++)
            {
                Transform child = rowRoot.GetChild(i);
                if (child.name == childName)
                {
                    return child;
                }

                Transform nested = child.Find(childName);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        /// <summary>
        /// スクロール一覧用に行ヘッダーだけ反映する
        /// </summary>
        private static void BindScrollListHeader(RowElements row, string modelName)
        {
            if (row.SubText != null)
            {
                row.SubText.gameObject.SetActive(false);
            }

            SetIndexRowVisible(row, false);
            if (row.NameText != null)
            {
                row.NameText.text = modelName ?? string.Empty;
                row.NameText.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// スクロール一覧用に空スロット表示だけ反映する
        /// レイアウトはプレハブ配置を使う
        /// </summary>
        public static void BindScrollListEmpty(RowElements row, string emptySlotLabel, int slotIndex)
        {
            row.SetActiveAttackCount(ConfirmAttackSlotCount);
            row.AttacksPanel?.Clear();
            ApplySlotEmptyImage(row.ThumbnailImage);

            SetIndexRowVisible(row, false);
            row.NameText.text = emptySlotLabel + "  スロット" + (slotIndex + 1);
            row.NameText.gameObject.SetActive(true);
            row.ParamsText.text = string.Empty;
            row.ParamsText.gameObject.SetActive(false);
            SetAttributeRowVisible(row, false);
            if (row.SubText != null)
            {
                row.SubText.text = string.Empty;
                row.SubText.gameObject.SetActive(false);
            }

            if (row.AttacksContainer != null)
            {
                row.AttacksContainer.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// スクロール一覧用にセーブ済みスロット内容だけ反映する
        /// レイアウトはプレハブ配置を使う
        /// </summary>
        public static void BindScrollListFromSlot(RowElements row, ModelSaveSlot slot, int slotIndex)
        {
            if (slot == null || string.IsNullOrEmpty(slot.modelName))
            {
                BindScrollListEmpty(row, "空き", slotIndex);
                return;
            }

            BindScrollListHeader(row, slot.modelName);
            row.ParamsText.text = ModelSaveSummaryFormatter.FormatStatusParameters(slot.status);
            row.ParamsText.gameObject.SetActive(true);
            SetAttributeRowVisible(row, false);

            row.SetActiveAttackCount(ConfirmAttackSlotCount);
            List<MotionType> attacks = CollectAttackMotions(slot.attackMotions);
            ApplyAttacks(row, attacks);
            if (row.AttacksContainer != null)
            {
                row.AttacksContainer.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// 保存確認画面用に空スロット表示だけ反映する
        /// レイアウトはプレハブ配置を使う
        /// </summary>
        public static void BindConfirmEmpty(RowElements row, string emptySlotLabel, int slotIndex)
        {
            row.SetActiveAttackCount(0);
            row.AttacksPanel?.Clear();
            ApplySlotEmptyImage(row.ThumbnailImage);

            SetIndexRowVisible(row, false);
            row.NameText.text = emptySlotLabel + "  スロット" + (slotIndex + 1);
            row.NameText.gameObject.SetActive(true);
            row.ParamsText.text = string.Empty;
            row.ParamsText.gameObject.SetActive(false);
            SetAttributeRowVisible(row, false);
            if (row.SubText != null)
            {
                row.SubText.text = string.Empty;
                row.SubText.gameObject.SetActive(false);
            }

            if (row.AttacksContainer != null)
            {
                row.AttacksContainer.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 保存確認画面用にセーブ済みスロット内容だけ反映する
        /// レイアウトはプレハブ配置を使う
        /// </summary>
        public static void BindConfirmFromSlot(RowElements row, ModelSaveSlot slot, int slotIndex)
        {
            if (slot == null || string.IsNullOrEmpty(slot.modelName))
            {
                BindConfirmEmpty(row, "空き", slotIndex);
                return;
            }

            BindConfirmHeader(row, slot.modelName);
            row.ParamsText.text = ModelSaveSummaryFormatter.FormatConfirmStatusParameters(slot.status);
            row.ParamsText.gameObject.SetActive(true);
            SetAttributeRowVisible(row, false);

            row.SetActiveAttackCount(ConfirmAttackSlotCount);
            List<MotionType> attacks = CollectAttackMotions(slot.attackMotions);
            ApplyAttacks(row, attacks);
            if (row.AttacksContainer != null)
            {
                row.AttacksContainer.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// 保存確認画面用に保存前プレビュー内容だけ反映する
        /// レイアウトはプレハブ配置を使う
        /// </summary>
        public static void BindConfirmPreview(
            RowElements row,
            string modelName,
            int slotIndex,
            ModelStatus status,
            IReadOnlyList<MotionType> registeredAttackMotions)
        {
            BindConfirmHeader(row, modelName);
            row.ParamsText.text = ModelSaveSummaryFormatter.FormatConfirmStatusParameters(status);
            row.ParamsText.gameObject.SetActive(true);
            SetAttributeRowVisible(row, false);

            row.SetActiveAttackCount(ConfirmAttackSlotCount);
            List<MotionType> attacks = CollectAttackMotions(registeredAttackMotions);
            ApplyAttacks(row, attacks);
            if (row.AttacksContainer != null)
            {
                row.AttacksContainer.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// サムネイル画像を設定する
        /// </summary>
        public static void ApplyThumbnailSprite(RowElements row, Sprite sprite)
        {
            if (row.ThumbnailImage == null)
            {
                return;
            }

            if (sprite == null)
            {
                ApplySlotEmptyImage(row.ThumbnailImage);
                return;
            }

            ApplySlotThumbnailImage(row.ThumbnailImage, sprite);
        }

        /// <summary>
        /// 行レイアウトを更新する
        /// </summary>
        public static void UpdateLayout(RowElements row, float rowWidth, int attackCount)
        {
            UpdateLayout(row, rowWidth, attackCount, -1f);
        }

        private static void UpdateLayout(RowElements row, float rowWidth, int attackCount, float totalRowHeight)
        {
            UpdateRowColumnWidths(row, rowWidth);
            UpdateAttacksLayoutWidth(row, rowWidth);
            ApplyAttacksContainerHeight(row, attackCount);
            ApplyThumbnailFrameWidth(row, row.ThumbnailColumnWidth);

            float rowHeight = totalRowHeight > 0f
                ? totalRowHeight
                : row.UseConfirmLayout
                    ? CalculateConfirmRowHeight(attackCount, row.ThumbnailColumnWidth, row.ConfirmLayoutSize)
                    : CalculateRowHeight(attackCount);
            SyncThumbnailFrameHeight(row, rowHeight, attackCount);

            if (row.UseConfirmLayout)
            {
                ApplyConfirmTypography(row);
                ApplyConfirmAttackListLayout(row);
                ApplyConfirmColumnHeights(row, attackCount);
                ApplyThumbnailSlotHeight(row, attackCount, rowHeight);
            }
        }

        /// <summary>
        /// 確認画面の既存テキストへメトリクスを反映する
        /// 実行時はプレハブ設定を維持しテキスト属性は変更しない
        /// </summary>
        private static void ApplyConfirmTypography(RowElements row)
        {
            ConfirmLayoutMetrics metrics = row.ConfirmMetrics;
            float nameRowHeight = ResolveConfirmNameRowHeight(metrics);
            if (row.NameText != null)
            {
                LayoutElement nameLayout = row.NameText.GetComponent<LayoutElement>();
                if (nameLayout != null)
                {
                    ApplyTextLayoutHeight(nameLayout, metrics.NameFontSize, nameRowHeight);
                }

                Transform nameRowTransform = row.NameText.transform.parent;
                if (nameRowTransform != null)
                {
                    LayoutElement nameRowLayout = nameRowTransform.GetComponent<LayoutElement>();
                    if (nameRowLayout != null)
                    {
                        nameRowLayout.minHeight = nameRowHeight;
                        nameRowLayout.preferredHeight = nameRowHeight;
                    }
                }
            }

            float statusBlockHeight = ConfirmStatusBlockHeight(metrics);
            if (row.ParamsText != null)
            {
                LayoutElement paramsLayout = row.ParamsText.GetComponent<LayoutElement>();
                if (paramsLayout != null)
                {
                    ApplyTextLayoutHeight(paramsLayout, metrics.ParamsFontSize, statusBlockHeight, ConfirmParamsLineCount);
                }
            }

            if (row.ModelAttributeImage != null)
            {
                LayoutElement attributeLayout = row.ModelAttributeImage.GetComponent<LayoutElement>();
                if (attributeLayout != null)
                {
                    float attributeSize = metrics.AttributeSquareSize;
                    attributeLayout.minWidth = attributeSize;
                    attributeLayout.preferredWidth = attributeSize;
                    attributeLayout.minHeight = attributeSize;
                    attributeLayout.preferredHeight = attributeSize;
                }
            }

            EnsureThreeColumnLayout(row);
        }

        private static void ApplyConfirmAttackListLayout(RowElements row)
        {
            if (row.AttacksContainer == null)
            {
                return;
            }

            VerticalLayoutGroup attacksLayoutGroup = row.AttacksContainer.GetComponent<VerticalLayoutGroup>();
            if (attacksLayoutGroup != null)
            {
                attacksLayoutGroup.spacing = ResumeAttackSlotSpacing;
            }
        }

        /// <summary>
        /// 保存確認画面用に行ヘッダーだけ反映する
        /// </summary>
        private static void BindConfirmHeader(RowElements row, string modelName)
        {
            if (row.SubText != null)
            {
                row.SubText.gameObject.SetActive(false);
            }

            SetIndexRowVisible(row, false);
            if (row.NameText != null)
            {
                row.NameText.text = modelName ?? string.Empty;
                row.NameText.gameObject.SetActive(true);
            }
        }

        private static void SetAttributeRowVisible(RowElements row, bool visible)
        {
            if (row?.ModelAttributeImage == null)
            {
                return;
            }

            Transform attributeRow = row.ModelAttributeImage.transform.parent;
            if (attributeRow != null && attributeRow.name == "AttributeRow")
            {
                attributeRow.gameObject.SetActive(visible);
            }
            else
            {
                row.ModelAttributeImage.gameObject.SetActive(visible);
            }
        }

        private static void SetIndexRowVisible(RowElements row, bool visible)
        {
            if (row?.IndexText == null)
            {
                return;
            }

            Transform indexRow = row.IndexText.transform.parent;
            if (indexRow != null)
            {
                indexRow.gameObject.SetActive(visible);
            }
            else
            {
                row.IndexText.gameObject.SetActive(visible);
            }
        }

        private static void ApplyAttacks(RowElements row, IReadOnlyList<MotionType> attacks)
        {
            if (row.AttacksPanel == null)
            {
                return;
            }

            if (attacks == null || attacks.Count == 0)
            {
                row.AttacksPanel.Clear();
                return;
            }

            row.AttacksPanel.ShowForSaveSlot(attacks);
        }

        private static void SyncThumbnailFrameHeight(RowElements row, float totalHeight, int attackCount = -1)
        {
            if (row.ThumbnailFrame == null)
            {
                return;
            }

            if (row.UseConfirmLayout)
            {
                int resolvedAttackCount = attackCount >= 0 ? attackCount : row.ActiveAttackCount;
                ApplyThumbnailSlotHeight(row, resolvedAttackCount, totalHeight);
                return;
            }

            LayoutElement frameLayout = row.ThumbnailFrame.GetComponent<LayoutElement>();
            if (frameLayout == null)
            {
                return;
            }

            if (row.UseSquareThumbnail)
            {
                float thumbSize = row.ThumbnailColumnWidth;
                frameLayout.flexibleHeight = 0f;
                frameLayout.minHeight = thumbSize;
                frameLayout.preferredHeight = thumbSize;
                return;
            }

            float frameHeight = Mathf.Max(72f, totalHeight - (RowContentPadding * 2f));
            frameLayout.minHeight = frameHeight;
            frameLayout.preferredHeight = frameHeight;
        }

        private static void ApplyThumbnailFrameWidth(RowElements row, float thumbnailColumnWidth)
        {
            if (row.ThumbnailFrame == null)
            {
                return;
            }

            LayoutElement frameLayout = row.ThumbnailFrame.GetComponent<LayoutElement>();
            if (frameLayout == null)
            {
                return;
            }

            frameLayout.minWidth = thumbnailColumnWidth;
            frameLayout.preferredWidth = thumbnailColumnWidth;
        }

        private static void ResolveColumnWidths(
            float rowWidth,
            float thumbnailColumnWidth,
            out float leftWidth,
            out float attacksWidth)
        {
            float contentWidth = Mathf.Max(0f, ResolveContentWidth(rowWidth, thumbnailColumnWidth));
            if (contentWidth <= 0f)
            {
                leftWidth = 0f;
                attacksWidth = 0f;
                return;
            }

            leftWidth = Mathf.Clamp(
                contentWidth - AttacksColumnMinWidth,
                LeftInfoMinWidth,
                LeftInfoMaxWidth);
            attacksWidth = Mathf.Max(AttacksColumnMinWidth, contentWidth - leftWidth);
            if (leftWidth + attacksWidth > contentWidth)
            {
                leftWidth = Mathf.Max(0f, contentWidth - attacksWidth);
            }
        }

        private static float ResolveContentWidth(float rowWidth, float thumbnailColumnWidth)
        {
            return rowWidth
                - thumbnailColumnWidth
                - (RowContentPadding * 2f)
                - (ColumnSpacing * 2f);
        }

        private static float ResolveLeftInfoWidth(float rowWidth, float thumbnailColumnWidth)
        {
            ResolveColumnWidths(rowWidth, thumbnailColumnWidth, out float leftWidth, out _);
            return leftWidth;
        }

        private static float ResolveAttacksColumnWidth(float rowWidth, float thumbnailColumnWidth)
        {
            ResolveColumnWidths(rowWidth, thumbnailColumnWidth, out _, out float attacksWidth);
            return attacksWidth;
        }

        private static void UpdateRowColumnWidths(RowElements row, float rowWidth)
        {
            float leftWidth = ResolveLeftInfoWidth(rowWidth, row.ThumbnailColumnWidth);

            if (row.LeftInfoColumn != null)
            {
                LayoutElement leftLayout = row.LeftInfoColumn.GetComponent<LayoutElement>();
                if (leftLayout != null)
                {
                    leftLayout.minWidth = leftWidth;
                    leftLayout.preferredWidth = leftWidth;
                }

                if (!row.UseConfirmLayout)
                {
                    SyncHeaderRowWidths(row.LeftInfoColumn, leftWidth);
                }
            }

            if (row.UseConfirmLayout && row.ThumbnailColumn != null)
            {
                LayoutElement thumbColumnLayout = row.ThumbnailColumn.GetComponent<LayoutElement>();
                if (thumbColumnLayout != null)
                {
                    thumbColumnLayout.minWidth = row.ThumbnailColumnWidth;
                    thumbColumnLayout.preferredWidth = row.ThumbnailColumnWidth;
                }

                SyncRowWidth(row.ThumbnailColumn, "NameRow", row.ThumbnailColumnWidth);
            }
            else if (row.UseConfirmLayout && row.LeftInfoColumn != null)
            {
                LayoutElement thumbColumnLayout = row.ThumbnailColumn?.GetComponent<LayoutElement>();
                if (thumbColumnLayout != null)
                {
                    thumbColumnLayout.minWidth = row.ThumbnailColumnWidth;
                    thumbColumnLayout.preferredWidth = row.ThumbnailColumnWidth;
                }

                SyncRowWidth(row.LeftInfoColumn, "NameRow", leftWidth);
            }

            if (row.AttacksContainer != null)
            {
                float attacksWidth = ResolveAttacksColumnWidth(rowWidth, row.ThumbnailColumnWidth);
                LayoutElement attacksLayout = row.AttacksContainer.GetComponent<LayoutElement>();
                if (attacksLayout != null)
                {
                    attacksLayout.minWidth = attacksWidth;
                    attacksLayout.preferredWidth = attacksWidth;
                }
            }
        }

        private static void ApplyConfirmColumnHeights(RowElements row, int attackCount = 0)
        {
            float dataRegionHeight = ResolveConfirmDataRegionHeight(attackCount, row.ConfirmMetrics);

            ApplyDataRegionColumnLayout(row, dataRegionHeight);

            if (row.ThumbnailColumn != null)
            {
                LayoutElement thumbLayout = row.ThumbnailColumn.GetComponent<LayoutElement>();
                if (thumbLayout != null)
                {
                    thumbLayout.minHeight = dataRegionHeight;
                    thumbLayout.preferredHeight = dataRegionHeight;
                    thumbLayout.flexibleHeight = IsRestructuredLayout(row) ? 1f : 0f;
                }
            }

            Transform dataRow = IsRestructuredLayout(row) ? row.ThumbnailColumn.parent : null;
            if (dataRow != null)
            {
                LayoutElement dataRowLayout = dataRow.GetComponent<LayoutElement>();
                if (dataRowLayout != null)
                {
                    dataRowLayout.minHeight = dataRegionHeight;
                    dataRowLayout.preferredHeight = dataRegionHeight;
                }
            }

            ApplyNameHeaderLayout(row);
        }

        private static void ApplyConfirmColumnHeights(RowElements row)
        {
            ApplyConfirmColumnHeights(row, row.ActiveAttackCount);
        }

        private static void UpdateAttacksLayoutWidth(RowElements row, float rowWidth)
        {
            if (row.AttacksContainer == null)
            {
                return;
            }

            float slotWidth = ResolveAttacksColumnWidth(rowWidth, row.ThumbnailColumnWidth) - 8f;
            for (int i = 0; i < TrainingResumeAttacksContentView.AttackSlotCount; i++)
            {
                Transform slotTransform = row.AttacksContainer.Find("AttackSlot" + (i + 1));
                if (slotTransform == null)
                {
                    continue;
                }

                LayoutElement slotLayout = slotTransform.GetComponent<LayoutElement>();
                if (slotLayout == null)
                {
                    continue;
                }

                slotLayout.preferredWidth = slotWidth;
                slotLayout.minWidth = slotWidth;
                slotLayout.flexibleHeight = 0f;
            }
        }

        private static void ApplyAttacksContainerHeight(RowElements row, int attackCount)
        {
            if (row.AttacksContainer == null)
            {
                return;
            }

            LayoutElement attacksLayout = row.AttacksContainer.GetComponent<LayoutElement>();
            if (attacksLayout == null)
            {
                return;
            }

            if (row.UseConfirmLayout)
            {
                ApplyConfirmColumnHeights(row, attackCount);
                return;
            }

            if (attackCount <= 0)
            {
                attacksLayout.minHeight = 0f;
                attacksLayout.preferredHeight = 0f;
                return;
            }

            float attacksHeight = ResolveResumeAttacksStackHeight(attackCount);
            attacksLayout.minHeight = attacksHeight;
            attacksLayout.preferredHeight = attacksHeight;
        }

        private static RowElements CreateRowColumns(
            Transform parent,
            Image thumbnailImage,
            RectTransform thumbnailFrame,
            float rowWidth,
            float thumbnailColumnWidth,
            bool useSquareThumbnail)
        {
            RectTransform leftInfoColumn = CreateLeftInfoColumn(
                parent,
                rowWidth,
                thumbnailColumnWidth,
                out TMP_Text indexText,
                out TMP_Text nameText,
                out TMP_Text paramsText,
                out TMP_Text subText,
                out Image modelAttributeImage);

            RectTransform attacksContainer = CreateAttacksContainer(parent, rowWidth, thumbnailColumnWidth);

            return new RowElements(
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
                useSquareThumbnail);
        }

        private static RectTransform CreateLeftInfoColumn(
            Transform parent,
            float rowWidth,
            float thumbnailColumnWidth,
            out TMP_Text indexText,
            out TMP_Text nameText,
            out TMP_Text paramsText,
            out TMP_Text subText,
            out Image modelAttributeImage)
        {
            var columnObject = new GameObject("LeftInfoColumn", typeof(RectTransform), typeof(LayoutElement), typeof(VerticalLayoutGroup));
            columnObject.transform.SetParent(parent, false);

            float leftWidth = ResolveLeftInfoWidth(rowWidth, thumbnailColumnWidth);
            float leftHeight = ResolveLeftSectionHeight();
            LayoutElement columnLayout = columnObject.GetComponent<LayoutElement>();
            columnLayout.flexibleWidth = 0f;
            columnLayout.flexibleHeight = 0f;
            columnLayout.minWidth = leftWidth;
            columnLayout.preferredWidth = leftWidth;
            columnLayout.minHeight = leftHeight;
            columnLayout.preferredHeight = leftHeight;

            VerticalLayoutGroup columnLayoutGroup = columnObject.GetComponent<VerticalLayoutGroup>();
            columnLayoutGroup.spacing = 2f;
            columnLayoutGroup.padding = new RectOffset(0, 0, 0, 0);
            columnLayoutGroup.childAlignment = TextAnchor.UpperLeft;
            columnLayoutGroup.childControlWidth = true;
            columnLayoutGroup.childControlHeight = true;
            columnLayoutGroup.childForceExpandWidth = true;
            columnLayoutGroup.childForceExpandHeight = false;

            CreateIndexRow(
                columnObject.transform,
                leftWidth,
                out indexText);
            modelAttributeImage = CreateNameRow(
                columnObject.transform,
                leftWidth,
                out nameText);

            CreateVerticalGap(columnObject.transform, StatusTopSpacing);

            paramsText = CreateRowText(
                columnObject.transform,
                "ParamsText",
                ParamsFontSize,
                FontStyles.Normal,
                new Color(0.82f, 0.86f, 0.92f, 1f),
                StatusBlockHeight);
            paramsText.alignment = TextAlignmentOptions.TopLeft;
            paramsText.enableWordWrapping = true;
            paramsText.overflowMode = TextOverflowModes.Overflow;
            paramsText.lineSpacing = -4f;
            paramsText.characterSpacing = 1f;
            LayoutElement paramsLayout = paramsText.GetComponent<LayoutElement>();
            paramsLayout.flexibleWidth = 1f;
            paramsLayout.flexibleHeight = 0f;
            ApplyTextLayoutHeight(paramsLayout, ParamsFontSize, StatusBlockHeight, ParamsLineCount);

            subText = CreateRowText(
                columnObject.transform,
                "SubText",
                16f,
                FontStyles.Normal,
                new Color(0.70f, 0.74f, 0.80f, 1f),
                22f);
            subText.gameObject.SetActive(false);

            return columnObject.GetComponent<RectTransform>();
        }

        private static void EnsureSlotRowVerticalLayout(Transform slotRow)
        {
            if (slotRow == null)
            {
                return;
            }

            ReplaceHorizontalLayoutWithVertical(slotRow.gameObject);
        }

        private static void ReplaceHorizontalLayoutWithVertical(GameObject slotRowObject)
        {
            if (slotRowObject == null)
            {
                return;
            }

            HorizontalLayoutGroup slotHBox = slotRowObject.GetComponent<HorizontalLayoutGroup>();
            RectOffset slotPadding = slotHBox != null
                ? slotHBox.padding
                : new RectOffset(
                    (int)RowContentPadding,
                    (int)RowContentPadding,
                    (int)RowContentPadding,
                    (int)RowContentPadding);
            if (slotPadding == null)
            {
                slotPadding = new RectOffset(
                    (int)RowContentPadding,
                    (int)RowContentPadding,
                    (int)RowContentPadding,
                    (int)RowContentPadding);
            }

            if (slotHBox != null)
            {
                Object.DestroyImmediate(slotHBox);
            }

            VerticalLayoutGroup slotVBox = slotRowObject.GetComponent<VerticalLayoutGroup>();
            if (slotVBox == null)
            {
                slotVBox = slotRowObject.AddComponent<VerticalLayoutGroup>();
            }

            if (slotVBox == null)
            {
                return;
            }

            slotVBox.padding = slotPadding;
            slotVBox.spacing = 0f;
            slotVBox.childAlignment = TextAnchor.UpperLeft;
            slotVBox.childControlWidth = true;
            slotVBox.childControlHeight = true;
            slotVBox.childForceExpandWidth = true;
            slotVBox.childForceExpandHeight = false;
        }

        private static RectTransform CreateConfirmDataRow(Transform slotRow)
        {
            var dataRowObject = new GameObject(
                DataRowName,
                typeof(RectTransform),
                typeof(HorizontalLayoutGroup),
                typeof(LayoutElement));
            dataRowObject.transform.SetParent(slotRow, false);

            HorizontalLayoutGroup dataHBox = dataRowObject.GetComponent<HorizontalLayoutGroup>();
            dataHBox.padding = new RectOffset(0, 0, 0, 0);
            dataHBox.spacing = ColumnSpacing;
            dataHBox.childAlignment = TextAnchor.UpperLeft;
            dataHBox.childControlWidth = true;
            dataHBox.childControlHeight = true;
            dataHBox.childForceExpandWidth = false;
            dataHBox.childForceExpandHeight = true;

            LayoutElement dataRowLayout = dataRowObject.GetComponent<LayoutElement>();
            dataRowLayout.flexibleWidth = 1f;
            dataRowLayout.flexibleHeight = 1f;
            dataRowLayout.minHeight = 0f;

            return dataRowObject.GetComponent<RectTransform>();
        }

        private static RectTransform CreateConfirmNameHeaderRow(
            Transform slotRow,
            float thumbnailColumnWidth,
            ConfirmLayoutMetrics metrics,
            out TMP_Text nameText)
        {
            var nameHeaderObject = new GameObject(
                NameHeaderRowName,
                typeof(RectTransform),
                typeof(HorizontalLayoutGroup),
                typeof(LayoutElement));
            nameHeaderObject.transform.SetParent(slotRow, false);

            float nameRowHeight = ResolveConfirmNameRowHeight(metrics);
            LayoutElement headerLayout = nameHeaderObject.GetComponent<LayoutElement>();
            headerLayout.flexibleWidth = 1f;
            headerLayout.flexibleHeight = 0f;
            headerLayout.minHeight = nameRowHeight;
            headerLayout.preferredHeight = nameRowHeight;

            HorizontalLayoutGroup nameHeaderGroup = nameHeaderObject.GetComponent<HorizontalLayoutGroup>();
            nameHeaderGroup.spacing = 0f;
            nameHeaderGroup.padding = new RectOffset(0, 0, 0, 0);
            nameHeaderGroup.childAlignment = TextAnchor.UpperLeft;
            nameHeaderGroup.childControlWidth = true;
            nameHeaderGroup.childControlHeight = true;
            nameHeaderGroup.childForceExpandWidth = false;
            nameHeaderGroup.childForceExpandHeight = false;

            CreateNameOnlyRow(
                nameHeaderObject.transform,
                thumbnailColumnWidth,
                metrics,
                out nameText);

            var spacerObject = new GameObject(
                "NameHeaderSpacer",
                typeof(RectTransform),
                typeof(LayoutElement));
            spacerObject.transform.SetParent(nameHeaderObject.transform, false);
            LayoutElement spacerLayout = spacerObject.GetComponent<LayoutElement>();
            spacerLayout.flexibleWidth = 1f;
            spacerLayout.flexibleHeight = 0f;

            return nameHeaderObject.GetComponent<RectTransform>();
        }

        private static RectTransform CreateConfirmThumbnailOnlyColumn(
            Transform parent,
            float thumbnailColumnWidth,
            ConfirmLayoutMetrics metrics,
            out Image thumbnailImage,
            out RectTransform thumbnailFrame)
        {
            var columnObject = new GameObject(
                "ThumbnailColumn",
                typeof(RectTransform),
                typeof(LayoutElement),
                typeof(VerticalLayoutGroup));
            columnObject.transform.SetParent(parent, false);

            float dataRegionHeight = ResolveConfirmDataRegionHeight(ConfirmAttackSlotCount, metrics);
            LayoutElement columnLayout = columnObject.GetComponent<LayoutElement>();
            columnLayout.flexibleWidth = 0f;
            columnLayout.flexibleHeight = 1f;
            columnLayout.minWidth = thumbnailColumnWidth;
            columnLayout.preferredWidth = thumbnailColumnWidth;
            columnLayout.minHeight = dataRegionHeight;
            columnLayout.preferredHeight = dataRegionHeight;

            VerticalLayoutGroup columnLayoutGroup = columnObject.GetComponent<VerticalLayoutGroup>();
            columnLayoutGroup.spacing = 0f;
            columnLayoutGroup.padding = new RectOffset(0, 0, 0, 0);
            columnLayoutGroup.childAlignment = TextAnchor.UpperLeft;
            columnLayoutGroup.childControlWidth = true;
            columnLayoutGroup.childControlHeight = true;
            columnLayoutGroup.childForceExpandWidth = true;
            columnLayoutGroup.childForceExpandHeight = true;

            thumbnailFrame = CreateThumbnailImage(
                columnObject.transform,
                out thumbnailImage,
                thumbnailColumnWidth,
                useSquareThumbnail: false);

            LayoutElement thumbnailFrameLayout = thumbnailFrame.GetComponent<LayoutElement>();
            if (thumbnailFrameLayout != null)
            {
                thumbnailFrameLayout.flexibleWidth = 0f;
                thumbnailFrameLayout.flexibleHeight = 1f;
                thumbnailFrameLayout.minHeight = dataRegionHeight;
                thumbnailFrameLayout.preferredHeight = dataRegionHeight;
            }

            return columnObject.GetComponent<RectTransform>();
        }

        private static RectTransform CreateConfirmThumbnailColumn(
            Transform parent,
            float thumbnailColumnWidth,
            ConfirmLayoutMetrics metrics,
            out Image thumbnailImage,
            out RectTransform thumbnailFrame,
            out TMP_Text nameText)
        {
            RectTransform thumbnailColumn = CreateConfirmThumbnailOnlyColumn(
                parent,
                thumbnailColumnWidth,
                metrics,
                out thumbnailImage,
                out thumbnailFrame);
            nameText = null;
            return thumbnailColumn;
        }

        private static RectTransform CreateConfirmParamsColumn(
            Transform parent,
            float rowWidth,
            float thumbnailColumnWidth,
            ConfirmLayoutMetrics metrics,
            out TMP_Text paramsText,
            out TMP_Text subText,
            out TMP_Text indexText,
            out Image modelAttributeImage)
        {
            var columnObject = new GameObject(
                "LeftInfoColumn",
                typeof(RectTransform),
                typeof(LayoutElement),
                typeof(VerticalLayoutGroup));
            columnObject.transform.SetParent(parent, false);

            float leftWidth = ResolveLeftInfoWidth(rowWidth, thumbnailColumnWidth);
            float leftHeight = ResolveConfirmDataRegionHeight(ConfirmAttackSlotCount, metrics);
            LayoutElement columnLayout = columnObject.GetComponent<LayoutElement>();
            columnLayout.flexibleWidth = 0f;
            columnLayout.flexibleHeight = 1f;
            columnLayout.minWidth = leftWidth;
            columnLayout.preferredWidth = leftWidth;
            columnLayout.minHeight = leftHeight;
            columnLayout.preferredHeight = leftHeight;

            VerticalLayoutGroup columnLayoutGroup = columnObject.GetComponent<VerticalLayoutGroup>();
            columnLayoutGroup.spacing = 0f;
            columnLayoutGroup.padding = new RectOffset(0, 0, 0, 0);
            columnLayoutGroup.childAlignment = TextAnchor.UpperLeft;
            columnLayoutGroup.childControlWidth = true;
            columnLayoutGroup.childControlHeight = true;
            columnLayoutGroup.childForceExpandWidth = true;
            columnLayoutGroup.childForceExpandHeight = false;

            CreateIndexRow(
                columnObject.transform,
                leftWidth,
                out indexText);
            indexText.transform.parent.gameObject.SetActive(false);

            modelAttributeImage = CreateConfirmAttributeRow(
                columnObject.transform,
                leftWidth,
                metrics);
            modelAttributeImage.transform.parent.gameObject.SetActive(false);

            float statusBlockHeight = ConfirmStatusBlockHeight(metrics);
            paramsText = CreateRowText(
                columnObject.transform,
                "ParamsText",
                metrics.ParamsFontSize,
                FontStyles.Normal,
                new Color(0.82f, 0.86f, 0.92f, 1f),
                statusBlockHeight,
                ConfirmParamsLineCount);
            paramsText.alignment = TextAlignmentOptions.Left;
            paramsText.enableWordWrapping = false;
            paramsText.overflowMode = TextOverflowModes.Overflow;
            paramsText.lineSpacing = 12f;
            paramsText.characterSpacing = 1f;
            LayoutElement paramsLayout = paramsText.GetComponent<LayoutElement>();
            paramsLayout.flexibleWidth = 1f;
            paramsLayout.flexibleHeight = 0f;
            ApplyTextLayoutHeight(paramsLayout, metrics.ParamsFontSize, statusBlockHeight, ConfirmParamsLineCount);

            subText = CreateRowText(
                columnObject.transform,
                "SubText",
                16f,
                FontStyles.Normal,
                new Color(0.70f, 0.74f, 0.80f, 1f),
                22f);
            subText.gameObject.SetActive(false);

            return columnObject.GetComponent<RectTransform>();
        }

        private static void CreateIndexRow(
            Transform parent,
            float columnWidth,
            out TMP_Text indexText)
        {
            var indexRowObject = new GameObject(
                "IndexRow",
                typeof(RectTransform),
                typeof(LayoutElement));
            indexRowObject.transform.SetParent(parent, false);

            LayoutElement indexRowLayout = indexRowObject.GetComponent<LayoutElement>();
            indexRowLayout.minHeight = IndexRowHeight;
            indexRowLayout.preferredHeight = IndexRowHeight;
            indexRowLayout.flexibleHeight = 0f;
            indexRowLayout.minWidth = columnWidth;
            indexRowLayout.preferredWidth = columnWidth;
            indexRowLayout.flexibleWidth = 1f;

            indexText = CreateRowText(
                indexRowObject.transform,
                "IndexText",
                IndexFontSize,
                FontStyles.Normal,
                new Color(0.70f, 0.74f, 0.80f, 1f),
                IndexRowHeight);
            LayoutElement indexTextLayout = indexText.GetComponent<LayoutElement>();
            indexTextLayout.flexibleWidth = 1f;
            indexTextLayout.flexibleHeight = 0f;
            ApplyTextLayoutHeight(indexTextLayout, IndexFontSize, IndexRowHeight);
            indexText.alignment = TextAlignmentOptions.MidlineLeft;
            indexText.enableWordWrapping = false;
        }

        private static void CreateNameOnlyRow(
            Transform parent,
            float columnWidth,
            ConfirmLayoutMetrics metrics,
            out TMP_Text nameText)
        {
            var nameRowObject = new GameObject(
                "NameRow",
                typeof(RectTransform),
                typeof(LayoutElement));
            nameRowObject.transform.SetParent(parent, false);

            float nameRowHeight = ResolveConfirmNameRowHeight(metrics);
            LayoutElement nameRowLayout = nameRowObject.GetComponent<LayoutElement>();
            nameRowLayout.minHeight = nameRowHeight;
            nameRowLayout.preferredHeight = nameRowHeight;
            nameRowLayout.flexibleHeight = 0f;
            nameRowLayout.minWidth = columnWidth;
            nameRowLayout.preferredWidth = columnWidth;
            nameRowLayout.flexibleWidth = 1f;

            nameText = CreateRowText(
                nameRowObject.transform,
                "NameText",
                metrics.NameFontSize,
                FontStyles.Bold,
                new Color(0.97f, 0.96f, 0.93f, 1f),
                nameRowHeight);
            LayoutElement nameTextLayout = nameText.GetComponent<LayoutElement>();
            nameTextLayout.flexibleWidth = 1f;
            nameTextLayout.flexibleHeight = 0f;
            ApplyTextLayoutHeight(nameTextLayout, metrics.NameFontSize, nameRowHeight);
            nameText.alignment = TextAlignmentOptions.TopLeft;
            nameText.margin = new Vector4(0f, 2f, 8f, 0f);
            nameText.enableWordWrapping = false;
            nameText.overflowMode = TextOverflowModes.Overflow;
        }

        private static Image CreateConfirmAttributeRow(Transform parent, float columnWidth, ConfirmLayoutMetrics metrics)
        {
            float attributeRowHeight = ConfirmAttributeRowHeight(metrics);
            var attributeRowObject = new GameObject(
                "AttributeRow",
                typeof(RectTransform),
                typeof(LayoutElement),
                typeof(HorizontalLayoutGroup));
            attributeRowObject.transform.SetParent(parent, false);

            LayoutElement attributeRowLayout = attributeRowObject.GetComponent<LayoutElement>();
            attributeRowLayout.minHeight = attributeRowHeight;
            attributeRowLayout.preferredHeight = attributeRowHeight;
            attributeRowLayout.flexibleHeight = 0f;
            attributeRowLayout.minWidth = columnWidth;
            attributeRowLayout.preferredWidth = columnWidth;
            attributeRowLayout.flexibleWidth = 1f;

            HorizontalLayoutGroup attributeRowLayoutGroup = attributeRowObject.GetComponent<HorizontalLayoutGroup>();
            attributeRowLayoutGroup.spacing = 6f;
            attributeRowLayoutGroup.padding = new RectOffset(0, 0, 0, 0);
            attributeRowLayoutGroup.childAlignment = TextAnchor.MiddleLeft;
            attributeRowLayoutGroup.childControlWidth = true;
            attributeRowLayoutGroup.childControlHeight = true;
            attributeRowLayoutGroup.childForceExpandWidth = false;
            attributeRowLayoutGroup.childForceExpandHeight = false;

            TMP_Text attributeLabel = CreateRowText(
                attributeRowObject.transform,
                "AttributeLabel",
                metrics.ParamsFontSize,
                FontStyles.Normal,
                new Color(0.82f, 0.86f, 0.92f, 1f),
                attributeRowHeight);
            attributeLabel.text = "属性";
            LayoutElement attributeLabelLayout = attributeLabel.GetComponent<LayoutElement>();
            attributeLabelLayout.flexibleWidth = 0f;
            attributeLabelLayout.flexibleHeight = 0f;
            ApplyTextLayoutHeight(attributeLabelLayout, metrics.ParamsFontSize, attributeRowHeight);
            attributeLabel.alignment = TextAlignmentOptions.MidlineLeft;
            attributeLabel.enableWordWrapping = false;

            return CreateAttributeSquare(attributeRowObject.transform, metrics.AttributeSquareSize);
        }

        private static Image CreateNameRow(
            Transform parent,
            float columnWidth,
            out TMP_Text nameText,
            bool truncateWithEllipsis = true)
        {
            var nameRowObject = new GameObject(
                "NameRow",
                typeof(RectTransform),
                typeof(LayoutElement),
                typeof(HorizontalLayoutGroup));
            nameRowObject.transform.SetParent(parent, false);

            LayoutElement nameRowLayout = nameRowObject.GetComponent<LayoutElement>();
            nameRowLayout.minHeight = NameRowContainerHeight;
            nameRowLayout.preferredHeight = NameRowContainerHeight;
            nameRowLayout.flexibleHeight = 0f;
            nameRowLayout.minWidth = columnWidth;
            nameRowLayout.preferredWidth = columnWidth;
            nameRowLayout.flexibleWidth = 1f;

            HorizontalLayoutGroup nameRowLayoutGroup = nameRowObject.GetComponent<HorizontalLayoutGroup>();
            nameRowLayoutGroup.spacing = 6f;
            nameRowLayoutGroup.padding = new RectOffset(0, 0, 0, 0);
            nameRowLayoutGroup.childAlignment = TextAnchor.MiddleLeft;
            nameRowLayoutGroup.childControlWidth = true;
            nameRowLayoutGroup.childControlHeight = true;
            nameRowLayoutGroup.childForceExpandWidth = false;
            nameRowLayoutGroup.childForceExpandHeight = false;

            nameText = CreateRowText(
                nameRowObject.transform,
                "NameText",
                NameFontSize,
                FontStyles.Normal,
                new Color(0.97f, 0.96f, 0.93f, 1f),
                NameRowHeight);
            LayoutElement nameTextLayout = nameText.GetComponent<LayoutElement>();
            nameTextLayout.flexibleWidth = 1f;
            nameTextLayout.flexibleHeight = 0f;
            nameTextLayout.minWidth = 40f;
            ApplyTextLayoutHeight(nameTextLayout, NameFontSize, NameRowHeight);
            nameText.alignment = TextAlignmentOptions.MidlineLeft;
            nameText.enableWordWrapping = false;
            nameText.overflowMode = truncateWithEllipsis
                ? TextOverflowModes.Ellipsis
                : TextOverflowModes.Overflow;

            return CreateAttributeSquare(nameRowObject.transform);
        }

        private static void SyncHeaderRowWidths(RectTransform leftInfoColumn, float leftWidth)
        {
            SyncRowWidth(leftInfoColumn, "IndexRow", leftWidth);
            SyncRowWidth(leftInfoColumn, "NameRow", leftWidth);
        }

        private static void SyncRowWidth(RectTransform leftInfoColumn, string rowName, float leftWidth)
        {
            if (leftInfoColumn == null)
            {
                return;
            }

            Transform rowTransform = leftInfoColumn.Find(rowName);
            if (rowTransform == null)
            {
                return;
            }

            LayoutElement rowLayout = rowTransform.GetComponent<LayoutElement>();
            if (rowLayout == null)
            {
                return;
            }

            rowLayout.minWidth = leftWidth;
            rowLayout.preferredWidth = leftWidth;
        }

        private static void RemoveLayoutElement(GameObject target)
        {
            LayoutElement layoutElement = target.GetComponent<LayoutElement>();
            if (layoutElement != null)
            {
                Object.Destroy(layoutElement);
            }
        }

        private static void CreateVerticalGap(Transform parent, float height)
        {
            var gapObject = new GameObject("VerticalGap", typeof(RectTransform), typeof(LayoutElement));
            gapObject.transform.SetParent(parent, false);

            LayoutElement gapLayout = gapObject.GetComponent<LayoutElement>();
            gapLayout.flexibleWidth = 1f;
            gapLayout.flexibleHeight = 0f;
            gapLayout.minHeight = height;
            gapLayout.preferredHeight = height;
        }

        private static Image CreateAttributeSquare(Transform parent, float size = AttributeSquareSize)
        {
            var squareObject = new GameObject("ModelAttribute", typeof(RectTransform), typeof(LayoutElement), typeof(Image));
            squareObject.transform.SetParent(parent, false);

            LayoutElement squareLayout = squareObject.GetComponent<LayoutElement>();
            squareLayout.preferredWidth = size;
            squareLayout.preferredHeight = size;
            squareLayout.minWidth = size;
            squareLayout.minHeight = size;
            squareLayout.flexibleWidth = 0f;
            squareLayout.flexibleHeight = 0f;

            Image image = squareObject.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private static RectTransform CreateAttacksContainer(Transform parent, float rowWidth, float thumbnailColumnWidth)
        {
            var attacksObject = new GameObject(
                "AttacksColumn",
                typeof(RectTransform),
                typeof(LayoutElement),
                typeof(VerticalLayoutGroup));
            attacksObject.transform.SetParent(parent, false);

            float attacksWidth = ResolveAttacksColumnWidth(rowWidth, thumbnailColumnWidth);
            LayoutElement attacksLayout = attacksObject.GetComponent<LayoutElement>();
            attacksLayout.flexibleWidth = 0f;
            attacksLayout.flexibleHeight = 0f;
            attacksLayout.minWidth = attacksWidth;
            attacksLayout.preferredWidth = attacksWidth;
            attacksLayout.minHeight = 0f;

            VerticalLayoutGroup attacksLayoutGroup = attacksObject.GetComponent<VerticalLayoutGroup>();
            attacksLayoutGroup.spacing = ResumeAttackSlotSpacing;
            attacksLayoutGroup.padding = new RectOffset(0, 0, 2, 0);
            attacksLayoutGroup.childAlignment = TextAnchor.UpperLeft;
            attacksLayoutGroup.childControlWidth = true;
            attacksLayoutGroup.childControlHeight = true;
            attacksLayoutGroup.childForceExpandWidth = true;
            attacksLayoutGroup.childForceExpandHeight = false;

            return attacksObject.GetComponent<RectTransform>();
        }

        private static RectTransform CreateConfirmAttacksContainer(
            Transform parent,
            float rowWidth,
            float thumbnailColumnWidth,
            ConfirmLayoutMetrics metrics)
        {
            RectTransform attacksContainer = CreateAttacksContainer(parent, rowWidth, thumbnailColumnWidth);
            float dataRegionHeight = ResolveConfirmDataRegionHeight(ConfirmAttackSlotCount, metrics);

            LayoutElement attacksLayout = attacksContainer.GetComponent<LayoutElement>();
            attacksLayout.flexibleHeight = 1f;
            attacksLayout.minHeight = dataRegionHeight;
            attacksLayout.preferredHeight = dataRegionHeight;

            VerticalLayoutGroup attacksLayoutGroup = attacksContainer.GetComponent<VerticalLayoutGroup>();
            attacksLayoutGroup.padding = new RectOffset(0, 0, 0, 0);
            attacksLayoutGroup.spacing = ResumeAttackSlotSpacing;
            attacksContainer.gameObject.AddComponent<TrainingResumeAttacksContentView>();

            return attacksContainer;
        }

        private static RectTransform CreateThumbnailImage(
            Transform parent,
            out Image thumbnailImage,
            float thumbnailColumnWidth,
            bool useSquareThumbnail)
        {
            var frameObject = new GameObject("ThumbnailFrame", typeof(RectTransform), typeof(LayoutElement), typeof(Image));
            frameObject.transform.SetParent(parent, false);

            LayoutElement frameLayout = frameObject.GetComponent<LayoutElement>();
            frameLayout.preferredWidth = thumbnailColumnWidth;
            frameLayout.minWidth = thumbnailColumnWidth;
            frameLayout.flexibleWidth = 0f;
            if (useSquareThumbnail)
            {
                frameLayout.flexibleHeight = 0f;
                frameLayout.minHeight = thumbnailColumnWidth;
                frameLayout.preferredHeight = thumbnailColumnWidth;
            }
            else
            {
                frameLayout.flexibleHeight = 1f;
                frameLayout.minHeight = 72f;
                frameLayout.preferredHeight = 72f;
            }

            Image frameImage = frameObject.GetComponent<Image>();
            frameImage.raycastTarget = false;
            TitleClayUiVisualUtility.ApplySlotThumbnailFrame(frameImage);

            var thumbnailObject = new GameObject("Thumbnail", typeof(RectTransform), typeof(Image));
            thumbnailObject.transform.SetParent(frameObject.transform, false);
            RectTransform thumbnailRect = thumbnailObject.GetComponent<RectTransform>();
            thumbnailRect.anchorMin = Vector2.zero;
            thumbnailRect.anchorMax = Vector2.one;
            thumbnailRect.offsetMin = new Vector2(6f, 6f);
            thumbnailRect.offsetMax = new Vector2(-6f, -6f);

            thumbnailImage = thumbnailObject.GetComponent<Image>();
            thumbnailImage.raycastTarget = false;
            ApplySlotEmptyImage(thumbnailImage);
            return frameObject.GetComponent<RectTransform>();
        }

        private static void ApplyTextLayoutHeight(
            LayoutElement layoutElement,
            float fontSize,
            float preferredHeight,
            int lineCount = 1)
        {
            if (layoutElement == null)
            {
                return;
            }

            float layoutHeight = Mathf.Max(preferredHeight, ResolveTextHeight(fontSize, lineCount));
            layoutElement.minHeight = layoutHeight;
            layoutElement.preferredHeight = layoutHeight;
        }

        private static TMP_Text CreateRowText(
            Transform parent,
            string objectName,
            float fontSize,
            FontStyles fontStyle,
            Color color,
            float preferredHeight,
            int lineCount = 1)
        {
            var textObject = new GameObject(objectName, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);

            LayoutElement textLayout = textObject.AddComponent<LayoutElement>();
            textLayout.flexibleWidth = 0f;
            ApplyTextLayoutHeight(textLayout, fontSize, preferredHeight, lineCount);

            TMP_Text text = textObject.AddComponent<TextMeshProUGUI>();
            AppTmpFontUtility.ApplyDefaultFont(text);
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.color = color;
            text.raycastTarget = false;
            text.enableWordWrapping = false;
            text.margin = new Vector4(0f, 0f, 8f, 0f);
            return text;
        }

        private static List<MotionType> CollectAttackMotions(IReadOnlyList<MotionType> motions)
        {
            var attacks = new List<MotionType>(ConfirmAttackSlotCount);
            if (motions == null)
            {
                return attacks;
            }

            int count = Mathf.Min(motions.Count, ConfirmAttackSlotCount);
            for (int i = 0; i < count; i++)
            {
                attacks.Add(motions[i]);
            }

            return attacks;
        }

        private static void ApplySlotEmptyImage(Image image)
        {
            TitleClayUiVisualUtility.ApplySlotEmptyImage(image);
        }

        private static void ApplySlotThumbnailImage(Image image, Sprite sprite)
        {
            TitleClayUiVisualUtility.ApplySlotThumbnailImage(image, sprite);
        }
    }
}
