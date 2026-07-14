using Extensions;
using LighthouseExtends.UIComponent.Button;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.ClayEditor.View
{
    /// <summary>
    /// Title/Optionsと同系統の粘土風UIスタイルを適用する
    /// RectTransformとTMPのalignment/color/fontSize等は変更しない
    /// </summary>
    public static class TitleClayUiVisualUtility
    {
        private const string GameUiResourceRoot = "Image/GameUi/";
        private const string ResourceRoot = "Image/Title/";

        private static Sprite panelSprite;
        private static Sprite thumbnailFrameSprite;
        private static Sprite buttonNormalSprite;
        private static Sprite buttonHighlightedSprite;
        private static Sprite buttonPressedSprite;
        private static Sprite sliderTrackSprite;
        private static Sprite sliderFillSprite;
        private static Sprite sliderHandleSprite;
        private static Sprite toggleOffSprite;
        private static Sprite toggleOnSprite;

        public static readonly Color LabelColor = new Color(0.36f, 0.24f, 0.18f, 1f);
        public static readonly Color SectionColor = new Color(0.55f, 0.32f, 0.20f, 1f);
        public static readonly Color SubLabelColor = new Color(0.48f, 0.34f, 0.26f, 1f);
        public static readonly Color DangerLabelColor = new Color(0.68f, 0.22f, 0.18f, 1f);
        public static readonly Color BlockerColor = new Color(0.12f, 0.08f, 0.06f, 0.72f);
        public static readonly Color ChipBackgroundColor = new Color(0.55f, 0.38f, 0.28f, 0.22f);
        public static readonly Color SeparatorColor = new Color(0.55f, 0.38f, 0.28f, 0.35f);
        public static readonly Color SlotInnerColor = new Color(0.94f, 0.88f, 0.80f, 0.55f);
        public static readonly Color DisabledColor = new Color(1f, 1f, 1f, 0.55f);
        public static readonly Color DropdownItemHighlightColor = new Color(1f, 0.96f, 0.92f, 1f);
        public static readonly Color DropdownItemSelectedColor = new Color(0.94f, 0.86f, 0.76f, 1f);
        public static readonly Color DropdownItemPressedColor = new Color(0.92f, 0.86f, 0.78f, 1f);
        public static readonly Color MatchupVsColor = new Color(1f, 0.88f, 0.28f, 1f);
        public static readonly Color MatchupReadyColor = new Color(0.96f, 0.92f, 0.84f, 1f);
        public static readonly Color MatchupFightColor = new Color(1f, 0.55f, 0.18f, 1f);
        public static readonly Color MatchupPlayerNameColor = new Color(0.72f, 0.9f, 1f, 1f);
        public static readonly Color MatchupEnemyNameColor = new Color(1f, 0.78f, 0.45f, 1f);
        public static readonly Color MatchupPlayerPlateTint = new Color(0.82f, 0.9f, 1f, 0.92f);
        public static readonly Color MatchupEnemyPlateTint = new Color(1f, 0.86f, 0.68f, 0.92f);

        /// <summary>
        /// モーダル背景パネルへ粘土風スタイルを適用する
        /// </summary>
        public static void ApplyPanel(Image image)
        {
            ApplySlicedSprite(image, LoadPanelSprite(), Color.white);
            if (image != null)
            {
                image.raycastTarget = false;
                image.pixelsPerUnitMultiplier = 1f;
            }
        }

        /// <summary>
        /// 入力ブロッカーへ粘土風スタイルを適用する
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
        /// LHButtonへTitleメニュー風スタイルを適用する
        /// </summary>
        public static void ApplyMenuButton(LHButton button, TextAlignmentOptions labelAlignment = TextAlignmentOptions.MidlineLeft)
        {
            ApplyMenuButton(button, LabelColor, labelAlignment);
        }

        /// <summary>
        /// LHButtonへTitleメニュー風スタイルを適用する
        /// </summary>
        public static void ApplyMenuButton(
            LHButton button,
            Color labelColor,
            TextAlignmentOptions labelAlignment = TextAlignmentOptions.MidlineLeft)
        {
            if (button == null)
            {
                return;
            }

            ApplyMenuButtonSprites(button);
            ApplyMenuButtonLabel(button, labelColor, labelAlignment);
        }

        /// <summary>
        /// 通常ButtonへTitleメニュー風スタイルを適用する
        /// </summary>
        public static void ApplyMenuButton(Button button, TextAlignmentOptions labelAlignment = TextAlignmentOptions.Center)
        {
            if (button == null)
            {
                return;
            }

            Sprite normalSprite = LoadButtonNormalSprite();
            if (normalSprite == null)
            {
                return;
            }

            if (button.targetGraphic is Image image)
            {
                image.sprite = normalSprite;
                image.type = Image.Type.Sliced;
                image.color = Color.white;
                image.pixelsPerUnitMultiplier = 1f;
            }

            button.transition = Selectable.Transition.SpriteSwap;
            SpriteState spriteState = button.spriteState;
            spriteState.highlightedSprite = LoadButtonHighlightedSprite() ?? normalSprite;
            spriteState.pressedSprite = LoadButtonPressedSprite() ?? normalSprite;
            spriteState.selectedSprite = spriteState.highlightedSprite;
            spriteState.disabledSprite = normalSprite;
            button.spriteState = spriteState;

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.disabledColor = DisabledColor;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                ApplyMenuButtonLabelPresentation(label);
            }
        }

        /// <summary>
        /// 削除等の危険操作ボタンへTitleメニュー風スタイルを適用する
        /// </summary>
        public static void ApplyDangerMenuButton(LHButton button)
        {
            ApplyMenuButton(button, DangerLabelColor, TextAlignmentOptions.Midline);
        }

        /// <summary>
        /// スロット選択ボタンへ粘土風スタイルを適用する
        /// </summary>
        public static void ApplySlotButton(LHButton button)
        {
            ApplyMenuButton(button, LabelColor, TextAlignmentOptions.MidlineLeft);
        }

        /// <summary>
        /// スロット内側へ粘土風スタイルを適用する
        /// </summary>
        public static void ApplySlotInner(Image image)
        {
            ApplySlicedSprite(image, LoadPanelSprite(), SlotInnerColor);
            if (image != null)
            {
                image.raycastTarget = false;
            }
        }

        /// <summary>
        /// セーブスロットサムネイル枠へ粘土風スタイルを適用する
        /// </summary>
        public static void ApplySlotThumbnailFrame(Image image)
        {
            ApplySlicedSprite(image, LoadThumbnailFrameSprite(), Color.white);
            if (image != null)
            {
                image.raycastTarget = false;
            }
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
            ApplySlotInner(image);
        }

        /// <summary>
        /// 欠落時のみフォントとマテリアルを補完する
        /// color/alignment/fontSize/margin等は変更しない
        /// </summary>
        public static void EnsureTextFontOnly(TMP_Text text)
        {
            if (text == null)
            {
                return;
            }

            if (text.font != null)
            {
                if (text.fontSharedMaterial == null)
                {
                    text.fontSharedMaterial = text.font.material;
                }

                return;
            }

            AppTmpFontUtility.ApplyDefaultFont(text);
        }

        /// <summary>
        /// タイトルテキストへフォントを補完する
        /// </summary>
        public static void ApplyTitleText(TMP_Text text)
        {
            EnsureTextFontOnly(text);
        }

        /// <summary>
        /// 本文テキストへフォントを補完する
        /// </summary>
        public static void ApplyBodyText(TMP_Text text)
        {
            EnsureTextFontOnly(text);
        }

        /// <summary>
        /// スロットラベルへフォントを補完する
        /// </summary>
        public static void ApplySlotLabel(TMP_Text text)
        {
            EnsureTextFontOnly(text);
        }

        /// <summary>
        /// 補助ラベルへフォントを補完する
        /// </summary>
        public static void ApplySubLabelText(TMP_Text text)
        {
            EnsureTextFontOnly(text);
        }

        /// <summary>
        /// 入力欄へ粘土風スタイルを適用する
        /// </summary>
        public static void ApplyInputField(TMP_InputField inputField)
        {
            if (inputField == null)
            {
                return;
            }

            Image background = inputField.GetComponent<Image>();
            ApplySlicedSprite(background, LoadButtonNormalSprite(), Color.white);

            if (inputField.textComponent != null)
            {
                EnsureTextFontOnly(inputField.textComponent);
            }

            if (inputField.placeholder is TMP_Text placeholder)
            {
                if (placeholder.text == "Enter text...")
                {
                    placeholder.text = "モデル名を入力";
                }

                EnsureTextFontOnly(placeholder);
            }
        }

        /// <summary>
        /// ドロップダウンへ粘土風スタイルを適用する
        /// </summary>
        public static void ApplyDropdown(TMP_Dropdown dropdown)
        {
            if (dropdown == null)
            {
                return;
            }

            if (dropdown.targetGraphic is Image target)
            {
                ApplySlicedSprite(target, LoadButtonNormalSprite(), Color.white);
                target.pixelsPerUnitMultiplier = 1f;
            }

            ColorBlock colors = dropdown.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = DropdownItemHighlightColor;
            colors.pressedColor = DropdownItemPressedColor;
            colors.selectedColor = DropdownItemHighlightColor;
            colors.disabledColor = DisabledColor;
            colors.fadeDuration = 0.08f;
            dropdown.colors = colors;

            if (dropdown.captionText != null)
            {
                ApplyDropdownCaptionText(dropdown.captionText);
            }

            if (dropdown.itemText != null)
            {
                ApplyDropdownItemLabel(dropdown.itemText);
            }
        }

        /// <summary>
        /// ドロップダウン展開リストへ粘土風スタイルを適用する
        /// </summary>
        public static void ApplyDropdownList(Transform dropdownListRoot)
        {
            if (dropdownListRoot == null)
            {
                return;
            }

            Image listImage = dropdownListRoot.GetComponent<Image>();
            if (listImage != null)
            {
                ApplyPanel(listImage);
                listImage.raycastTarget = true;
            }

            foreach (Toggle toggle in dropdownListRoot.GetComponentsInChildren<Toggle>(true))
            {
                ApplyDropdownItemToggle(toggle);
            }

            foreach (TMP_Text text in dropdownListRoot.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.name == "Item Label")
                {
                    ApplyDropdownItemLabel(text);
                }
            }

            foreach (Transform child in dropdownListRoot.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "Item Checkmark" && child.TryGetComponent(out Image checkmarkImage))
                {
                    ApplyDropdownItemCheckmark(checkmarkImage);
                }

                if (child.name == "Item Background" && child.TryGetComponent(out Image itemBackground))
                {
                    ApplyDropdownItemBackground(itemBackground);
                }
            }
        }

        /// <summary>
        /// ドロップダウンテンプレートへ粘土風スタイルを適用する
        /// </summary>
        public static void ApplyDropdownTemplate(RectTransform template)
        {
            if (template == null)
            {
                return;
            }

            ApplyPanel(template.GetComponent<Image>());

            Transform item = template.GetComponentInChildren<ScrollRect>(true)?.content?.Find("Item");
            if (item == null)
            {
                return;
            }

            if (item.TryGetComponent(out Toggle toggle))
            {
                ApplyDropdownItemToggle(toggle);
            }

            if (item.Find("Item Background")?.GetComponent<Image>() is Image itemBackground)
            {
                ApplyDropdownItemBackground(itemBackground);
            }

            if (item.Find("Item Checkmark")?.GetComponent<Image>() is Image checkmarkImage)
            {
                ApplyDropdownItemCheckmark(checkmarkImage);
            }

            if (item.Find("Item Label")?.GetComponent<TMP_Text>() is TMP_Text itemLabel)
            {
                ApplyDropdownItemLabel(itemLabel);
            }
        }

        /// <summary>
        /// ドロップダウン矢印へ粘土風スタイルを適用する
        /// </summary>
        public static void ApplyDropdownArrow(Image arrowImage)
        {
            if (arrowImage == null)
            {
                return;
            }

            arrowImage.color = LabelColor;
        }

        /// <summary>
        /// ドロップダウン項目トグルへ粘土風スタイルを適用する
        /// </summary>
        public static void ApplyDropdownItemToggle(Toggle toggle)
        {
            if (toggle == null)
            {
                return;
            }

            ColorBlock colors = toggle.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = DropdownItemHighlightColor;
            colors.pressedColor = DropdownItemPressedColor;
            colors.selectedColor = DropdownItemSelectedColor;
            colors.disabledColor = DisabledColor;
            colors.fadeDuration = 0.08f;
            toggle.colors = colors;
        }

        /// <summary>
        /// ドロップダウンキャプションへ粘土風スタイルを適用する
        /// </summary>
        public static void ApplyDropdownCaptionText(TMP_Text text)
        {
            EnsureTextFontOnly(text);
        }

        /// <summary>
        /// ドロップダウン項目ラベルへフォントを補完する
        /// </summary>
        public static void ApplyDropdownItemLabel(TMP_Text text)
        {
            EnsureTextFontOnly(text);
        }

        /// <summary>
        /// ドロップダウン項目チェックへ粘土風スタイルを適用する
        /// </summary>
        public static void ApplyDropdownItemCheckmark(Image checkmarkImage)
        {
            if (checkmarkImage == null)
            {
                return;
            }

            checkmarkImage.color = SectionColor;
        }

        /// <summary>
        /// ドロップダウン項目背景へ粘土風スタイルを適用する
        /// </summary>
        public static void ApplyDropdownItemBackground(Image image)
        {
            ApplySlicedSprite(image, LoadButtonNormalSprite(), Color.white);
            if (image != null)
            {
                image.pixelsPerUnitMultiplier = 1f;
            }
        }

        /// <summary>
        /// ドロップダウン項目背景へ粘土風スタイルを適用する
        /// </summary>
        public static void ApplyDropdownItemPanelBackground(Image image)
        {
            ApplySlicedSprite(image, LoadPanelSprite(), Color.white);
        }

        /// <summary>
        /// スライダーへ粘土風スタイルを適用する
        /// </summary>
        public static void ApplySlider(Slider slider)
        {
            if (slider == null)
            {
                return;
            }

            Sprite trackSprite = LoadSliderTrackSprite();
            Sprite fillSprite = LoadSliderFillSprite();
            Sprite handleSprite = LoadSliderHandleSprite();
            if (trackSprite == null || fillSprite == null || handleSprite == null)
            {
                return;
            }

            Image background = slider.transform.Find("Background")?.GetComponent<Image>();
            if (background != null)
            {
                background.sprite = trackSprite;
                background.type = Image.Type.Sliced;
                background.color = Color.white;
            }

            Image fill = slider.fillRect != null
                ? slider.fillRect.GetComponent<Image>()
                : slider.transform.Find("Fill Area/Fill")?.GetComponent<Image>();
            if (fill != null)
            {
                fill.sprite = fillSprite;
                fill.type = Image.Type.Sliced;
                fill.color = Color.white;
            }

            Image handle = slider.handleRect != null
                ? slider.handleRect.GetComponent<Image>()
                : slider.transform.Find("Handle Slide Area/Handle")?.GetComponent<Image>();
            if (handle != null)
            {
                handle.sprite = handleSprite;
                handle.type = Image.Type.Simple;
                handle.color = Color.white;
                handle.SetNativeSize();
                slider.targetGraphic = handle;
            }
        }

        /// <summary>
        /// トグルへ粘土風スタイルを適用する
        /// </summary>
        public static void ApplyToggle(Toggle toggle)
        {
            if (toggle == null)
            {
                return;
            }

            Sprite offSprite = LoadToggleOffSprite();
            Sprite onSprite = LoadToggleOnSprite();
            if (offSprite == null || onSprite == null)
            {
                return;
            }

            Image background = toggle.transform.Find("Background")?.GetComponent<Image>();
            if (background != null)
            {
                background.sprite = offSprite;
                background.type = Image.Type.Simple;
                background.color = Color.white;
                background.SetNativeSize();
                toggle.targetGraphic = background;
            }

            Image checkmark = toggle.graphic as Image;
            if (checkmark == null)
            {
                checkmark = toggle.transform.Find("Checkmark")?.GetComponent<Image>();
            }

            if (checkmark != null)
            {
                checkmark.sprite = onSprite;
                checkmark.type = Image.Type.Simple;
                checkmark.color = Color.white;
                checkmark.SetNativeSize();
                toggle.graphic = checkmark;
            }

            ColorBlock colors = toggle.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.96f, 0.92f, 1f);
            colors.pressedColor = new Color(0.92f, 0.86f, 0.78f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
            colors.fadeDuration = 0.08f;
            toggle.colors = colors;
        }

        /// <summary>
        /// ステータス表示用テキストへ粘土風スタイルを適用する
        /// </summary>
        public static void ApplyStatusText(TMP_Text text)
        {
            EnsureTextFontOnly(text);
        }

        /// <summary>
        /// 対戦紹介のVSテキストへフォントを補完する
        /// </summary>
        public static void ApplyMatchupVsText(TMP_Text text)
        {
            EnsureTextFontOnly(text);
        }

        /// <summary>
        /// 対戦紹介のReadyテキストへフォントを補完する
        /// </summary>
        public static void ApplyMatchupReadyText(TMP_Text text)
        {
            EnsureTextFontOnly(text);
        }

        /// <summary>
        /// 対戦紹介のFightテキストへフォントを補完する
        /// </summary>
        public static void ApplyMatchupFightText(TMP_Text text)
        {
            EnsureTextFontOnly(text);
        }

        /// <summary>
        /// 対戦紹介の名前ラベルへフォントを補完する
        /// </summary>
        public static void ApplyMatchupNameLabel(TMP_Text text, bool isPlayer)
        {
            EnsureTextFontOnly(text);
        }

        /// <summary>
        /// 対戦紹介の名前プレート背景へ粘土風スタイルを適用する
        /// </summary>
        public static void ApplyMatchupNamePlatePanel(Image image, bool isPlayer)
        {
            if (image == null)
            {
                return;
            }

            ApplySlicedSprite(image, LoadPanelSprite(), isPlayer ? MatchupPlayerPlateTint : MatchupEnemyPlateTint);
            image.raycastTarget = false;
            image.pixelsPerUnitMultiplier = 1f;
        }

        /// <summary>
        /// 戦闘オーバーレイ用テキストの共通設定を行う
        /// 色やグラデーションは各View側で設定する
        /// </summary>
        public static void PrepareBattleOverlayText(TMP_Text text)
        {
            EnsureTextFontOnly(text);
        }

        /// <summary>
        /// 体力バー背景へ粘土風スタイルを適用する
        /// </summary>
        public static void ApplyStaminaTrack(Image image)
        {
            ApplySlicedSprite(image, LoadSliderTrackSprite(), Color.white);
        }

        /// <summary>
        /// 体力バー充填へ粘土風スタイルを適用する
        /// </summary>
        public static void ApplyStaminaFill(Image image)
        {
            if (image == null)
            {
                return;
            }

            ApplySlicedSprite(image, LoadSliderFillSprite(), Color.white);
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
        }

        /// <summary>
        /// スロット行の装飾へ粘土風スタイルを適用する
        /// </summary>
        public static void ApplySlotRowDecorations(Transform root)
        {
            if (root == null)
            {
                return;
            }

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                switch (child.name)
                {
                    case "AttackChip":
                        if (child.TryGetComponent(out Image chipImage))
                        {
                            chipImage.sprite = null;
                            chipImage.type = Image.Type.Simple;
                            chipImage.color = ChipBackgroundColor;
                        }

                        break;
                    case "AttackSeparator":
                        if (child.TryGetComponent(out Image separatorImage))
                        {
                            separatorImage.sprite = null;
                            separatorImage.type = Image.Type.Simple;
                            separatorImage.color = SeparatorColor;
                        }

                        break;
                    case "ThumbnailFrame":
                        ApplySlotThumbnailFrame(child.GetComponent<Image>());
                        break;
                    case "Thumbnail":
                    case "ThumbnailImage":
                        ApplySlotEmptyImage(child.GetComponent<Image>());
                        break;
                }
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

        private static void ApplyMenuButtonSprites(LHButton button)
        {
            Sprite normalSprite = LoadButtonNormalSprite();
            if (normalSprite == null)
            {
                return;
            }

            if (button.targetGraphic is Image image)
            {
                image.sprite = normalSprite;
                image.type = Image.Type.Sliced;
                image.color = Color.white;
                image.pixelsPerUnitMultiplier = 1f;
            }

            button.transition = Selectable.Transition.SpriteSwap;
            SpriteState spriteState = button.spriteState;
            spriteState.highlightedSprite = LoadButtonHighlightedSprite() ?? normalSprite;
            spriteState.pressedSprite = LoadButtonPressedSprite() ?? normalSprite;
            spriteState.selectedSprite = spriteState.highlightedSprite;
            spriteState.disabledSprite = normalSprite;
            button.spriteState = spriteState;

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.disabledColor = DisabledColor;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
        }

        private static void ApplyMenuButtonLabel(
            LHButton button,
            Color labelColor,
            TextAlignmentOptions labelAlignment)
        {
            _ = labelColor;
            _ = labelAlignment;

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label == null)
            {
                return;
            }

            ApplyMenuButtonLabelPresentation(label);
        }

        private static void ApplyMenuButtonLabelPresentation(TMP_Text text)
        {
            if (text == null)
            {
                return;
            }

            text.raycastTarget = false;
            EnsureTextFontOnly(text);
        }

        private static void ApplyLabelText(
            TMP_Text text,
            TextAlignmentOptions alignment,
            Color labelColor)
        {
            _ = alignment;
            _ = labelColor;
            ApplyMenuButtonLabelPresentation(text);
        }

        private static void ApplySlicedSprite(Image image, Sprite sprite, Color color)
        {
            if (image == null)
            {
                return;
            }

            if (sprite == null)
            {
                image.type = Image.Type.Simple;
                image.color = color;
                return;
            }

            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = color;
            image.pixelsPerUnitMultiplier = 1f;
        }

        private static void ApplyTextOutline(TMP_Text text, float outlineWidth, Color outlineColor)
        {
            if (text == null)
            {
                return;
            }

            if (!TryEnsureTextFontAndMaterial(text))
            {
                return;
            }

            text.fontSharedMaterial = text.fontMaterial;
            text.outlineWidth = outlineWidth;
            text.outlineColor = outlineColor;
        }

        private static void ClearTextOutline(TMP_Text text)
        {
            if (text == null)
            {
                return;
            }

            if (!TryEnsureTextFontAndMaterial(text))
            {
                return;
            }

            text.outlineWidth = 0f;
        }

        private static bool TryEnsureTextFontAndMaterial(TMP_Text text)
        {
            if (text == null)
            {
                return false;
            }

            if (text.font == null)
            {
                AppTmpFontUtility.ApplyDefaultFont(text);
            }

            if (text.font == null)
            {
                return false;
            }

            if (text.fontSharedMaterial == null)
            {
                text.fontSharedMaterial = text.font.material;
            }

            return text.fontSharedMaterial != null;
        }

        private static Sprite LoadPanelSprite() => LoadSprite("TitleOptionPanel", ref panelSprite);

        private static Sprite LoadThumbnailFrameSprite() =>
            LoadGameUiSprite("GameUi_ThumbnailFrame", ref thumbnailFrameSprite);

        private static Sprite LoadButtonNormalSprite() => LoadSprite("TitleMenuButton_Normal", ref buttonNormalSprite);

        private static Sprite LoadButtonHighlightedSprite() =>
            LoadSprite("TitleMenuButton_Highlighted", ref buttonHighlightedSprite);

        private static Sprite LoadButtonPressedSprite() => LoadSprite("TitleMenuButton_Pressed", ref buttonPressedSprite);

        private static Sprite LoadSliderTrackSprite() => LoadSprite("TitleOptionSlider_Track", ref sliderTrackSprite);

        private static Sprite LoadSliderFillSprite() => LoadSprite("TitleOptionSlider_Fill", ref sliderFillSprite);

        private static Sprite LoadSliderHandleSprite() => LoadSprite("TitleOptionSlider_Handle", ref sliderHandleSprite);

        private static Sprite LoadToggleOffSprite() => LoadSprite("TitleOptionToggle_Off", ref toggleOffSprite);

        private static Sprite LoadToggleOnSprite() => LoadSprite("TitleOptionToggle_On", ref toggleOnSprite);

        private static Sprite LoadSprite(string assetName, ref Sprite cache)
        {
            if (cache != null)
            {
                return cache;
            }

            cache = Resources.Load<Sprite>(ResourceRoot + assetName);
            if (cache == null)
            {
                Debug.LogWarning($"[TitleClayUiVisualUtility] UIテクスチャが見つかりません: {ResourceRoot}{assetName}");
            }

            return cache;
        }

        private static Sprite LoadGameUiSprite(string assetName, ref Sprite cache)
        {
            if (cache != null)
            {
                return cache;
            }

            cache = Resources.Load<Sprite>(GameUiResourceRoot + assetName);
            if (cache == null)
            {
                Debug.LogWarning($"[TitleClayUiVisualUtility] UIテクスチャが見つかりません: {GameUiResourceRoot}{assetName}");
            }

            return cache;
        }
    }
}
