using Extensions;
using LighthouseExtends.UIComponent.Button;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.ClayEditor.View
{
    /// <summary>
    /// ClayEdit UIの見た目調整を行う(レイアウトは変更しない)
    /// </summary>
    public static class ClayEditUiVisualUtility
    {
        private const string ResourceRoot = "Image/GameUi/";
        private const string TrainingResourceRoot = "Image/Training/";

        private static Sprite frameSprite;
        private static Sprite inputFieldSprite;
        private static Sprite slotInnerSprite;
        private static Sprite thumbnailFrameSprite;
        private static Sprite sliderFillSprite;
        private static Sprite sliderHandleSprite;
        private static Sprite trainingHudHeaderSprite;
        private static Sprite trainingLogPanelSprite;
        private static Sprite trainingRestButtonSprite;
        private static Sprite trainingStaminaTrackSprite;
        private static Sprite trainingStaminaFillSprite;

        private static readonly Color PanelColor = new Color(0.18f, 0.20f, 0.26f, 0.94f);
        private static readonly Color BlockerColor = new Color(0.04f, 0.06f, 0.10f, 0.62f);
        private static readonly Color InputTextColor = new Color(0.16f, 0.18f, 0.22f, 1f);
        private static readonly Color PlaceholderColor = new Color(0.45f, 0.47f, 0.52f, 0.85f);
        private static readonly Color DropdownTextColor = new Color(0.20f, 0.22f, 0.28f, 1f);
        private static readonly Color SlotInnerColor = new Color(0.14f, 0.16f, 0.21f, 0.92f);

        private static readonly Color PrimaryNormal = new Color(0.88f, 0.58f, 0.28f, 1f);
        private static readonly Color PrimaryHighlight = new Color(0.96f, 0.68f, 0.36f, 1f);
        private static readonly Color PrimaryPressed = new Color(0.72f, 0.46f, 0.20f, 1f);

        private static readonly Color SecondaryNormal = new Color(0.42f, 0.46f, 0.54f, 1f);
        private static readonly Color SecondaryHighlight = new Color(0.52f, 0.56f, 0.64f, 1f);
        private static readonly Color SecondaryPressed = new Color(0.32f, 0.35f, 0.42f, 1f);

        private static readonly Color AccentNormal = new Color(0.36f, 0.58f, 0.78f, 1f);
        private static readonly Color AccentHighlight = new Color(0.46f, 0.68f, 0.88f, 1f);
        private static readonly Color AccentPressed = new Color(0.28f, 0.48f, 0.66f, 1f);

        private static readonly Color DisabledColor = new Color(0.55f, 0.57f, 0.62f, 0.55f);

        private static readonly Color DangerNormal = new Color(0.82f, 0.32f, 0.30f, 1f);
        private static readonly Color DangerHighlight = new Color(0.92f, 0.42f, 0.38f, 1f);
        private static readonly Color DangerPressed = new Color(0.66f, 0.24f, 0.22f, 1f);

        /// <summary>
        /// モーダル背景パネルへスタイルを適用する
        /// </summary>
        public static void ApplyPanel(Image image)
        {
            ApplySlicedImage(image, PanelColor);
        }

        /// <summary>
        /// TrainingHUDヘッダーへTitle系パネルスタイルを適用する
        /// </summary>
        public static void ApplyTrainingHudHeaderPanel(Image image)
        {
            TitleClayUiVisualUtility.ApplyPanel(image);
        }

        /// <summary>
        /// TrainingHUDステータス欄へTitle系パネルスタイルを適用する
        /// </summary>
        public static void ApplyTrainingStatusPanel(Image image)
        {
            TitleClayUiVisualUtility.ApplyPanel(image);
        }

        /// <summary>
        /// TrainingHUDログ欄へTitle系パネルスタイルを適用する
        /// </summary>
        public static void ApplyTrainingLogPanel(Image image)
        {
            TitleClayUiVisualUtility.ApplyPanel(image);
        }

        /// <summary>
        /// TrainingHUD休憩ボタンへTitleメニューボタンスタイルを適用する
        /// </summary>
        public static void ApplyTrainingRestButton(LHButton button)
        {
            TitleClayUiVisualUtility.ApplyMenuButton(button, TextAlignmentOptions.Center);
        }

        /// <summary>
        /// TrainingHUD体力バー背景へTraining用スプライトを適用する
        /// </summary>
        public static void ApplyTrainingStaminaTrack(Image image)
        {
            if (image == null)
            {
                return;
            }

            ApplySprite(image, LoadTrainingStaminaTrackSprite(), Color.white);
        }

        /// <summary>
        /// TrainingHUD体力バー充填へTraining用スプライトを適用する
        /// </summary>
        public static void ApplyTrainingStaminaFill(Image image)
        {
            if (image == null)
            {
                return;
            }

            ApplySprite(image, LoadTrainingStaminaFillSprite(), Color.white);
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
        }

        /// <summary>
        /// 入力ブロッカーへスタイルを適用する
        /// </summary>
        public static void ApplyBlocker(Image image)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = null;
            image.type = Image.Type.Simple;
            image.color = BlockerColor;
            image.raycastTarget = true;
        }

        /// <summary>
        /// 主要アクションボタンへスタイルを適用する
        /// </summary>
        public static void ApplyPrimaryButton(LHButton button)
        {
            ApplyButton(button, PrimaryNormal, PrimaryHighlight, PrimaryPressed);
            ApplyButtonLabel(button, true);
        }

        /// <summary>
        /// 副次アクションボタンへスタイルを適用する
        /// </summary>
        public static void ApplySecondaryButton(LHButton button)
        {
            ApplyButton(button, SecondaryNormal, SecondaryHighlight, SecondaryPressed);
            ApplyButtonLabel(button, true);
        }

        /// <summary>
        /// 強調アクションボタンへスタイルを適用する
        /// </summary>
        public static void ApplyAccentButton(LHButton button)
        {
            ApplyButton(button, AccentNormal, AccentHighlight, AccentPressed);
            ApplyButtonLabel(button, true);
        }

        /// <summary>
        /// 破壊的アクションボタンへスタイルを適用する
        /// </summary>
        public static void ApplyDangerButton(LHButton button)
        {
            ApplyButton(button, DangerNormal, DangerHighlight, DangerPressed);
            ApplyButtonLabel(button, true);
        }

        /// <summary>
        /// スロットにサムネイルを表示する
        /// </summary>
        public static void ApplySlotThumbnailImage(Image image, Sprite sprite)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
            image.type = Image.Type.Simple;
        }

        /// <summary>
        /// セーブスロットサムネイル枠へスタイルを適用する
        /// </summary>
        public static void ApplySlotThumbnailFrame(Image image)
        {
            ApplySprite(image, LoadThumbnailFrameSprite(), Color.white);
            if (image != null)
            {
                image.raycastTarget = false;
            }
        }

        /// <summary>
        /// スロットを空き表示に戻す
        /// </summary>
        public static void ApplySlotEmptyImage(Image image)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = null;
            image.preserveAspect = false;
            ApplySprite(image, LoadSlotInnerSprite(), SlotInnerColor);
        }

        /// <summary>
        /// スロット選択ボタンへスタイルを適用する
        /// </summary>
        public static void ApplySlotButton(LHButton button)
        {
            if (button == null)
            {
                return;
            }

            Image image = button.targetGraphic as Image;
            ApplySprite(image, LoadSlotInnerSprite(), SlotInnerColor);

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.92f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 0.95f);
            colors.disabledColor = DisabledColor;
            colors.colorMultiplier = 1f;
            button.colors = colors;

            ApplyButtonLabel(button, true);
        }

        /// <summary>
        /// タイトルテキストへスタイルを適用する
        /// </summary>
        public static void ApplyTitleText(TMP_Text text)
        {
            TitleClayUiVisualUtility.EnsureTextFontOnly(text);
        }

        /// <summary>
        /// 本文テキストへフォントを補完する
        /// </summary>
        public static void ApplyBodyText(TMP_Text text)
        {
            TitleClayUiVisualUtility.EnsureTextFontOnly(text);
        }

        /// <summary>
        /// スロットラベルへフォントを補完する
        /// </summary>
        public static void ApplySlotLabel(TMP_Text text)
        {
            TitleClayUiVisualUtility.EnsureTextFontOnly(text);
        }

        /// <summary>
        /// ドロップダウン項目背景へスタイルを適用する
        /// </summary>
        public static void ApplyDropdownItemBackground(Image image)
        {
            ApplySprite(image, LoadInputFieldSprite(), Color.white);
        }

        /// <summary>
        /// ドロップダウンへスタイルを適用する
        /// </summary>
        public static void ApplyDropdown(TMP_Dropdown dropdown)
        {
            if (dropdown == null)
            {
                return;
            }

            Image target = dropdown.targetGraphic as Image;
            ApplySprite(target, LoadInputFieldSprite(), Color.white);

            if (dropdown.captionText != null)
            {
                TitleClayUiVisualUtility.EnsureTextFontOnly(dropdown.captionText);
            }

            if (dropdown.itemText != null)
            {
                TitleClayUiVisualUtility.EnsureTextFontOnly(dropdown.itemText);
            }
        }

        /// <summary>
        /// 入力欄へスタイルを適用する
        /// </summary>
        public static void ApplyInputField(TMP_InputField inputField)
        {
            if (inputField == null)
            {
                return;
            }

            if (inputField.textComponent != null)
            {
                TitleClayUiVisualUtility.EnsureTextFontOnly(inputField.textComponent);
            }

            if (inputField.placeholder is TMP_Text placeholder)
            {
                if (placeholder.text == "Enter text...")
                {
                    placeholder.text = "モデル名を入力";
                }

                TitleClayUiVisualUtility.EnsureTextFontOnly(placeholder);
            }

            Image background = inputField.GetComponent<Image>();
            ApplySprite(background, LoadInputFieldSprite(), Color.white);
        }

        /// <summary>
        /// 通常ボタンへスタイルを適用する
        /// </summary>
        public static void ApplyStandardButton(Button button)
        {
            ApplyStandardButton(button, SecondaryNormal, SecondaryHighlight, SecondaryPressed);
        }

        /// <summary>
        /// 主要アクションの通常ボタンへスタイルを適用する
        /// </summary>
        public static void ApplyPrimaryStandardButton(Button button)
        {
            ApplyStandardButton(button, PrimaryNormal, PrimaryHighlight, PrimaryPressed);
            ApplyStandardButtonLabel(button);
        }

        /// <summary>
        /// ステータス表示用テキストへスタイルを適用する
        /// </summary>
        public static void ApplyStatusText(TMP_Text text)
        {
            TitleClayUiVisualUtility.EnsureTextFontOnly(text);
        }

        /// <summary>
        /// スライダーへスタイルを適用する
        /// </summary>
        public static void ApplySlider(Slider slider)
        {
            if (slider == null)
            {
                return;
            }

            if (slider.fillRect != null)
            {
                Image fill = slider.fillRect.GetComponent<Image>();
                ApplySprite(fill, LoadSliderFillSprite(), PrimaryNormal);
            }

            if (slider.handleRect != null)
            {
                Image handle = slider.handleRect.GetComponent<Image>();
                ApplySprite(handle, LoadSliderHandleSprite(), Color.white);
            }
        }

        /// <summary>
        /// キャンバススケーラーを統一する
        /// </summary>
        public static void NormalizeCanvasScaler(CanvasScaler scaler)
        {
            if (scaler == null)
            {
                return;
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        private static void ApplyStandardButton(
            Button button,
            Color normal,
            Color highlighted,
            Color pressed)
        {
            if (button == null)
            {
                return;
            }

            Image image = button.targetGraphic as Image;
            ApplySlicedImage(image, normal);

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = ColorForTint(highlighted, normal);
            colors.pressedColor = ColorForTint(pressed, normal);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = DisabledColor;
            colors.colorMultiplier = 1f;
            button.colors = colors;
        }

        private static void ApplyStandardButtonLabel(Button button)
        {
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label == null)
            {
                return;
            }

            label.raycastTarget = false;
            TitleClayUiVisualUtility.EnsureTextFontOnly(label);
        }

        private static void ApplyButton(
            LHButton button,
            Color normal,
            Color highlighted,
            Color pressed)
        {
            if (button == null)
            {
                return;
            }

            Image image = button.targetGraphic as Image;
            ApplySlicedImage(image, normal);

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = ColorForTint(highlighted, normal);
            colors.pressedColor = ColorForTint(pressed, normal);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = DisabledColor;
            colors.colorMultiplier = 1f;
            button.colors = colors;
        }

        private static Color ColorForTint(Color target, Color baseColor)
        {
            if (baseColor.a <= 0.001f)
            {
                return target;
            }

            return new Color(
                target.r / baseColor.r,
                target.g / baseColor.g,
                target.b / baseColor.b,
                1f);
        }

        private static void ApplyButtonLabel(LHButton button, bool outlined)
        {
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label == null)
            {
                return;
            }

            label.raycastTarget = false;
            TitleClayUiVisualUtility.EnsureTextFontOnly(label);
        }

        private static void ApplySlicedImage(Image image, Color color)
        {
            ApplySprite(image, LoadFrameSprite(), color);
        }

        private static void ApplySprite(Image image, Sprite sprite, Color color)
        {
            if (image == null)
            {
                return;
            }

            Sprite resolvedSprite = sprite ?? LoadFrameSprite();
            if (resolvedSprite == null)
            {
                image.type = Image.Type.Simple;
                image.color = color;
                return;
            }

            image.sprite = resolvedSprite;
            image.type = Image.Type.Sliced;
            image.color = color;
        }

        private static Sprite LoadFrameSprite() => LoadSprite("GameUi_Frame", ref frameSprite);

        private static Sprite LoadThumbnailFrameSprite() =>
            LoadSprite("GameUi_ThumbnailFrame", ref thumbnailFrameSprite);

        private static Sprite LoadInputFieldSprite() => LoadSprite("GameUi_InputField", ref inputFieldSprite);

        private static Sprite LoadSlotInnerSprite() => LoadSprite("GameUi_SlotInner", ref slotInnerSprite);

        private static Sprite LoadSliderFillSprite() => LoadSprite("GameUi_SliderFill", ref sliderFillSprite);

        private static Sprite LoadSliderHandleSprite() => LoadSprite("GameUi_SliderHandle", ref sliderHandleSprite);

        private static Sprite LoadTrainingHudHeaderSprite() =>
            LoadTrainingSprite("TrainingUi_HudHeader", ref trainingHudHeaderSprite);

        private static Sprite LoadTrainingLogPanelSprite() =>
            LoadTrainingSprite("TrainingUi_LogPanel", ref trainingLogPanelSprite);

        private static Sprite LoadTrainingRestButtonSprite() =>
            LoadTrainingSprite("TrainingUi_RestButton", ref trainingRestButtonSprite);

        private static Sprite LoadTrainingStaminaTrackSprite() =>
            LoadTrainingSprite("TrainingUi_StaminaTrack", ref trainingStaminaTrackSprite);

        private static Sprite LoadTrainingStaminaFillSprite() =>
            LoadTrainingSprite("TrainingUi_StaminaFill", ref trainingStaminaFillSprite);

        private static Sprite LoadSprite(string assetName, ref Sprite cache)
        {
            if (cache != null)
            {
                return cache;
            }

            cache = Resources.Load<Sprite>(ResourceRoot + assetName);
            if (cache == null)
            {
                Debug.LogWarning($"[ClayEditUiVisualUtility] UIテクスチャが見つかりません: {ResourceRoot}{assetName}");
            }

            return cache;
        }

        private static Sprite LoadTrainingSprite(string assetName, ref Sprite cache)
        {
            if (cache != null)
            {
                return cache;
            }

            cache = Resources.Load<Sprite>(TrainingResourceRoot + assetName);
            if (cache == null)
            {
                Debug.LogWarning($"[ClayEditUiVisualUtility] Training UIテクスチャが見つかりません: {TrainingResourceRoot}{assetName}");
            }

            return cache;
        }

        private static void ApplyOutline(TMP_Text text, float width, Color? color = null)
        {
            if (text.fontSharedMaterial == null && text.font != null)
            {
                text.fontSharedMaterial = text.font.material;
            }

            if (text.fontSharedMaterial == null)
            {
                return;
            }

            text.fontSharedMaterial = text.fontMaterial;
            text.outlineWidth = width;
            text.outlineColor = color ?? new Color(0.06f, 0.08f, 0.12f, 0.92f);
        }
    }
}
