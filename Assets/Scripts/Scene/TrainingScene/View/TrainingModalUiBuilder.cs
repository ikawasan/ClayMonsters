using Extensions;
using LighthouseExtends.UIComponent.Button;
using TMPro;
using UI.ClayEditor.View;
using UnityEngine;
using UnityEngine.UI;

namespace Scene.TrainingScene.View
{
    /// <summary>
    /// 育成オーバーレイUIの共通部品生成
    /// </summary>
    public static class TrainingModalUiBuilder
    {
        /// <summary>
        /// オーバーレイ用Canvasを設定する
        /// </summary>
        public static Canvas EnsureOverlayCanvas(GameObject root, int sortingOrder)
        {
            Canvas canvas = root.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = root.AddComponent<Canvas>();
            }

            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;

            if (root.GetComponent<GraphicRaycaster>() == null)
            {
                root.AddComponent<GraphicRaycaster>();
            }

            Stretch(root.GetComponent<RectTransform>());
            return canvas;
        }

        /// <summary>
        /// 全画面ブロッカーを生成する
        /// </summary>
        public static Image CreateBlocker(Transform parent)
        {
            var blockerObject = new GameObject("Blocker", typeof(RectTransform), typeof(Image));
            blockerObject.transform.SetParent(parent, false);
            Stretch(blockerObject.GetComponent<RectTransform>());
            Image image = blockerObject.GetComponent<Image>();
            TitleClayUiVisualUtility.ApplyBlocker(image);
            return image;
        }

        /// <summary>
        /// 中央パネルを生成する
        /// </summary>
        public static RectTransform CreatePanel(Transform parent, Vector2 size)
        {
            var panelObject = new GameObject("WindowPanel", typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(parent, false);
            RectTransform rect = panelObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;

            Image image = panelObject.GetComponent<Image>();
            TitleClayUiVisualUtility.ApplyPanel(image);
            image.raycastTarget = true;
            return rect;
        }

        /// <summary>
        /// ラベルテキストを生成する
        /// </summary>
        public static TMP_Text CreateLabel(
            Transform parent,
            string name,
            Vector2 anchor,
            Vector2 anchoredPosition,
            float fontSize,
            TextAlignmentOptions alignment,
            Vector2 size)
        {
            var labelObject = new GameObject(name, typeof(RectTransform));
            labelObject.transform.SetParent(parent, false);
            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            TMP_Text text = labelObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.raycastTarget = false;
            AppTmpFontUtility.ApplyDefaultFont(text);
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }

        /// <summary>
        /// ボタンを生成する
        /// </summary>
        public static LHButton CreateButton(
            Transform parent,
            string name,
            string label,
            Vector2 anchor,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LHButton));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            LHButton button = buttonObject.GetComponent<LHButton>();
            button.targetGraphic = buttonObject.GetComponent<Image>();

            TMP_Text text = CreateLabel(
                buttonObject.transform,
                "Label",
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                28f,
                TextAlignmentOptions.Center,
                Vector2.zero);
            Stretch(text.rectTransform);
            text.text = label;
            TitleClayUiVisualUtility.ApplyMenuButton(button, TextAlignmentOptions.Center);
            button.EnsureUiSoundFeedback();
            return button;
        }

        /// <summary>
        /// RectTransformを全画面に伸ばす
        /// </summary>
        public static void Stretch(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// 再開確認ウィンドウのサムネイル画像を生成する
        /// </summary>
        public static Image CreateResumeThumbnailImage(Transform panelParent)
        {
            var frameObject = new GameObject("ThumbnailFrame", typeof(RectTransform), typeof(Image));
            frameObject.transform.SetParent(panelParent, false);
            ConfigureTopLeftAnchor(frameObject.GetComponent<RectTransform>(), new Vector2(32f, -140f), new Vector2(200f, 200f));

            Image frameImage = frameObject.GetComponent<Image>();
            frameImage.raycastTarget = false;
            TitleClayUiVisualUtility.ApplySlotThumbnailFrame(frameImage);

            var thumbnailObject = new GameObject("ThumbnailImage", typeof(RectTransform), typeof(Image));
            thumbnailObject.transform.SetParent(frameObject.transform, false);
            Stretch(thumbnailObject.GetComponent<RectTransform>());
            RectTransform thumbnailRect = thumbnailObject.GetComponent<RectTransform>();
            thumbnailRect.offsetMin = new Vector2(6f, 6f);
            thumbnailRect.offsetMax = new Vector2(-6f, -6f);

            Image thumbnailImage = thumbnailObject.GetComponent<Image>();
            thumbnailImage.raycastTarget = false;
            thumbnailImage.gameObject.SetActive(false);
            return thumbnailImage;
        }

        private static void ApplyTrainingLabelStyle(TMP_Text text, float fontSize)
        {
            if (text == null)
            {
                return;
            }

            AppTmpFontUtility.ApplyDefaultFont(text);
        }

        private static void ConfigureTopLeftAnchor(
            RectTransform rect,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }
    }
}
