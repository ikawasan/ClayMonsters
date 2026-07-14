using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.ClayEditor.View
{
    /// <summary>
    /// ClayEditモード選択ドロップダウンのレイアウトと見た目を整える
    /// EditorからClayEditModeDropdownの見た目を焼き込む際に使用する
    /// 実行時はCollapseのみ利用する
    /// </summary>
    public static class ClayEditModeDropdownLayout
    {
        private const float DropdownWidth = 280f;
        private const float DropdownHeight = 56f;
        private const float DropdownListHeight = 168f;
        private const float ArrowSize = 28f;
        private const float ScreenMarginX = 20f;
        private const float ScreenMarginY = 20f;

        /// <summary>
        /// モード選択ドロップダウンのレイアウトのみ整える
        /// </summary>
        public static void ApplyLayout(TMP_Dropdown dropdown)
        {
            if (dropdown == null)
            {
                return;
            }

            Collapse(dropdown);
            ConfigureRoot(dropdown);
            ConfigureCaptionLayout(dropdown);
            ConfigureArrowLayout(dropdown);
            ConfigureTemplateLayout(dropdown);
        }

        /// <summary>
        /// モード選択ドロップダウンのレイアウトと見た目を整える
        /// </summary>
        public static void ApplyVisualStyle(TMP_Dropdown dropdown)
        {
            if (dropdown == null)
            {
                return;
            }

            ApplyLayout(dropdown);
            TitleClayUiVisualUtility.ApplyDropdown(dropdown);

            if (dropdown.captionText != null)
            {
                TitleClayUiVisualUtility.ApplyDropdownCaptionText(dropdown.captionText);
            }

            Transform arrow = dropdown.transform.Find("Arrow");
            if (arrow != null)
            {
                TitleClayUiVisualUtility.ApplyDropdownArrow(arrow.GetComponent<Image>());
            }

            if (dropdown.template != null)
            {
                ScrollRect scrollRect = dropdown.template.GetComponent<ScrollRect>();
                if (scrollRect?.viewport != null)
                {
                    Image viewportImage = scrollRect.viewport.GetComponent<Image>();
                    if (viewportImage != null)
                    {
                        TitleClayUiVisualUtility.ApplyDropdownItemPanelBackground(viewportImage);
                    }
                }

                TitleClayUiVisualUtility.ApplyDropdownTemplate(dropdown.template);
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor向けエイリアス
        /// </summary>
        public static void ApplyVisualStyleForEditor(TMP_Dropdown dropdown)
        {
            ApplyVisualStyle(dropdown);
        }
#endif

        /// <summary>
        /// 展開中のドロップダウンリストへ粘土風スタイルを適用する
        /// </summary>
        public static void ApplyExpandedListVisualStyle(Transform dropdownListRoot)
        {
            if (dropdownListRoot == null)
            {
                return;
            }

            TitleClayUiVisualUtility.ApplyDropdownList(dropdownListRoot);
        }

        /// <summary>
        /// 展開中のドロップダウンリストを即時破棄する
        /// Hideはフェード後に破棄するためOnDisable時は残ることがある
        /// </summary>
        public static void Collapse(TMP_Dropdown dropdown)
        {
            if (dropdown == null)
            {
                return;
            }

            dropdown.Hide();
            DestroyOrphanPopupObjects(dropdown);

            if (dropdown.template != null)
            {
                dropdown.template.gameObject.SetActive(false);
            }
        }

        private static void ConfigureRoot(TMP_Dropdown dropdown)
        {
            RectTransform root = dropdown.transform as RectTransform;
            if (root == null)
            {
                return;
            }

            root.anchorMin = new Vector2(0f, 1f);
            root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = new Vector2(ScreenMarginX, -ScreenMarginY);
            root.sizeDelta = new Vector2(DropdownWidth, DropdownHeight);
        }

        private static void ConfigureCaptionLayout(TMP_Dropdown dropdown)
        {
            if (dropdown.captionText == null)
            {
                return;
            }

            RectTransform captionRect = dropdown.captionText.rectTransform;
            captionRect.anchorMin = Vector2.zero;
            captionRect.anchorMax = Vector2.one;
            captionRect.offsetMin = new Vector2(20f, 8f);
            captionRect.offsetMax = new Vector2(-40f, -8f);
            dropdown.captionText.fontSize = 28f;
        }

        private static void ConfigureArrowLayout(TMP_Dropdown dropdown)
        {
            Transform arrow = dropdown.transform.Find("Arrow");
            if (arrow == null)
            {
                return;
            }

            arrow.localRotation = Quaternion.identity;
            arrow.localEulerAngles = Vector3.zero;
            RectTransform arrowRect = arrow as RectTransform;
            if (arrowRect == null)
            {
                return;
            }

            arrowRect.anchorMin = new Vector2(1f, 0.5f);
            arrowRect.anchorMax = new Vector2(1f, 0.5f);
            arrowRect.pivot = new Vector2(1f, 0.5f);
            arrowRect.anchoredPosition = new Vector2(-16f, 0f);
            arrowRect.sizeDelta = new Vector2(ArrowSize, ArrowSize);
        }

        private static void ConfigureTemplateLayout(TMP_Dropdown dropdown)
        {
            RectTransform template = dropdown.template;
            if (template == null)
            {
                return;
            }

            template.anchorMin = new Vector2(0f, 0f);
            template.anchorMax = new Vector2(1f, 0f);
            template.pivot = new Vector2(0.5f, 1f);
            template.anchoredPosition = Vector2.zero;
            template.sizeDelta = new Vector2(0f, DropdownListHeight);

            ScrollRect scrollRect = template.GetComponent<ScrollRect>();
            if (scrollRect != null)
            {
                scrollRect.horizontal = false;
                scrollRect.vertical = dropdown.options.Count > 6;
                scrollRect.movementType = ScrollRect.MovementType.Clamped;

                if (scrollRect.viewport != null)
                {
                    Mask viewportMask = scrollRect.viewport.GetComponent<Mask>();
                    if (viewportMask != null)
                    {
                        viewportMask.showMaskGraphic = false;
                    }
                }

                ResetTemplateContentForTmp(scrollRect, dropdown.options.Count);
            }

            ConfigureItemTemplateLayout(template);
            template.gameObject.SetActive(false);
        }

        private static void ConfigureItemTemplateLayout(RectTransform template)
        {
            Transform item = template.GetComponentInChildren<ScrollRect>(true)?.content?.Find("Item");
            if (item == null)
            {
                return;
            }

            TMP_Text itemLabel = item.Find("Item Label")?.GetComponent<TMP_Text>();
            if (itemLabel != null)
            {
                itemLabel.fontSize = 26f;
                RectTransform labelRect = itemLabel.rectTransform;
                labelRect.offsetMin = new Vector2(36f, 2f);
                labelRect.offsetMax = new Vector2(-12f, -2f);
                TitleClayUiVisualUtility.ApplyDropdownItemLabel(itemLabel);
            }

            if (item.Find("Item Checkmark") is RectTransform checkmarkRect)
            {
                checkmarkRect.anchorMin = new Vector2(0f, 0.5f);
                checkmarkRect.anchorMax = new Vector2(0f, 0.5f);
                checkmarkRect.pivot = new Vector2(0f, 0.5f);
                checkmarkRect.anchoredPosition = new Vector2(12f, 0f);
                checkmarkRect.sizeDelta = new Vector2(20f, 20f);
            }
        }

        private static void ResetTemplateContentForTmp(ScrollRect scrollRect, int itemCount)
        {
            if (scrollRect.content == null)
            {
                return;
            }

            float itemHeight = DropdownListHeight / Mathf.Max(1, itemCount);
            RectTransform content = scrollRect.content;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, itemHeight);

            Transform item = content.Find("Item");
            if (item is RectTransform itemRect)
            {
                itemRect.anchorMin = new Vector2(0f, 0.5f);
                itemRect.anchorMax = new Vector2(1f, 0.5f);
                itemRect.pivot = new Vector2(0.5f, 0.5f);
                itemRect.anchoredPosition = Vector2.zero;
                itemRect.sizeDelta = new Vector2(0f, itemHeight);
            }
        }

        private static void DestroyOrphanPopupObjects(TMP_Dropdown dropdown)
        {
            DestroyNamedChildren(dropdown.transform, "Dropdown List");

            Transform current = dropdown.transform.parent;
            while (current != null)
            {
                DestroyNamedChildren(current, "Dropdown List");
                current = current.parent;
            }

            Canvas canvas = dropdown.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                return;
            }

            DestroyNamedChildren(canvas.transform, "Dropdown List");
            DestroyNamedChildren(canvas.transform, "Blocker");
        }

        private static void DestroyNamedChildren(Transform root, string objectName)
        {
            if (root == null)
            {
                return;
            }

            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                if (child.name == objectName)
                {
                    Object.DestroyImmediate(child.gameObject);
                }
            }
        }
    }
}
