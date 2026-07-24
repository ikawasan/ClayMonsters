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
        private static Sprite slotInnerSprite;
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
        public const string InputBlockerObjectName = "Blocker";
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
            ApplySlicedSprite(image, LoadPanelSprite());
            if (image != null)
            {
                image.raycastTarget = false;
                image.pixelsPerUnitMultiplier = 1f;
            }
        }

        /// <summary>
        /// Editor Bake向けにモーダル背景パネルへ粘土風スタイルを適用する
        /// </summary>
        public static void ApplyPanelForEditorBake(Image image)
        {
            ApplySlicedSpriteForEditorBake(image, LoadPanelSprite(), Color.white);
            if (image != null)
            {
                image.raycastTarget = false;
                image.pixelsPerUnitMultiplier = 1f;
            }
        }

        /// <summary>
        /// 入力ブロッカーを設定する
        /// 見た目は常に透明(alpha=0)で背面UIへの入力のみ遮断する
        /// </summary>
        public static void ApplyBlocker(Image image)
        {
            ConfigureInputBlocker(image, blocksRaycasts: true);
        }

        /// <summary>
        /// 入力ブロッカーの入力遮断を切り替える
        /// alphaは常に0を維持する
        /// </summary>
        public static void ConfigureInputBlocker(Image image, bool blocksRaycasts)
        {
            if (image == null)
            {
                return;
            }

            Color color = image.color;
            color.a = 0f;
            image.color = color;
            image.raycastTarget = blocksRaycasts;
        }

        /// <summary>
        /// Editor Bake向けに入力ブロッカーを配置する
        /// 見た目は常に透明(alpha=0)
        /// </summary>
        public static void ApplyBlockerForEditorBake(Image image)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = null;
            image.type = Image.Type.Simple;
            ConfigureInputBlocker(image, blocksRaycasts: true);
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
        /// 色はシーン配置を維持する
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
        /// Editor Bake向けにLHButtonへTitleメニュー風スタイルを適用する
        /// </summary>
        public static void ApplyMenuButtonForEditorBake(
            LHButton button,
            TextAlignmentOptions labelAlignment = TextAlignmentOptions.MidlineLeft)
        {
            ApplyMenuButtonForEditorBake(button, LabelColor, labelAlignment);
        }

        /// <summary>
        /// Editor Bake向けにLHButtonへTitleメニュー風スタイルを適用する
        /// </summary>
        public static void ApplyMenuButtonForEditorBake(
            LHButton button,
            Color labelColor,
            TextAlignmentOptions labelAlignment = TextAlignmentOptions.MidlineLeft)
        {
            if (button == null)
            {
                return;
            }

            ApplyMenuButtonSpritesForEditorBake(button);
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
                image.pixelsPerUnitMultiplier = 1f;
            }

            button.transition = Selectable.Transition.SpriteSwap;
            SpriteState spriteState = button.spriteState;
            spriteState.highlightedSprite = LoadButtonHighlightedSprite() ?? normalSprite;
            spriteState.pressedSprite = LoadButtonPressedSprite() ?? normalSprite;
            spriteState.selectedSprite = spriteState.highlightedSprite;
            spriteState.disabledSprite = normalSprite;
            button.spriteState = spriteState;

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                ApplyMenuButtonLabelPresentation(label);
            }
        }

        /// <summary>
        /// Editor Bake向けに通常ButtonへTitleメニュー風スタイルを適用する
        /// </summary>
        public static void ApplyMenuButtonForEditorBake(Button button, TextAlignmentOptions labelAlignment = TextAlignmentOptions.Center)
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
            ApplySlicedSprite(image, LoadPanelSprite());
            if (image != null)
            {
                image.raycastTarget = false;
            }
        }

        /// <summary>
        /// Editor Bake向けにスロット内側へ粘土風スタイルを適用する
        /// </summary>
        public static void ApplySlotInnerForEditorBake(Image image)
        {
            ApplySlicedSpriteForEditorBake(image, LoadPanelSprite(), SlotInnerColor);
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
            ApplySlicedSprite(image, LoadThumbnailFrameSprite());
            if (image != null)
            {
                image.raycastTarget = false;
            }
        }

        /// <summary>
        /// Editor Bake向けにセーブスロットサムネイル枠へ粘土風スタイルを適用する
        /// </summary>
        public static void ApplySlotThumbnailFrameForEditorBake(Image image)
        {
            ApplySlicedSpriteForEditorBake(image, LoadThumbnailFrameSprite(), Color.white);
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
            image.preserveAspect = true;
            image.type = Image.Type.Simple;
            image.maskable = true;
        }

        /// <summary>
        /// サムネイルImageを角丸Mask配下へ移す
        /// </summary>
        /// <param name="thumbnailImage">サムネイルImage</param>
        public static void EnsureThumbnailUnderRoundedMask(Image thumbnailImage)
        {
            if (thumbnailImage == null)
            {
                return;
            }

            Transform currentParent = thumbnailImage.transform.parent;
            if (currentParent != null && currentParent.name == "ThumbnailMask")
            {
                Mask existingMask = currentParent.GetComponent<Mask>();
                if (existingMask != null)
                {
                    existingMask.showMaskGraphic = false;
                    thumbnailImage.maskable = true;
                    return;
                }
            }

            Transform frameParent = currentParent;
            if (frameParent == null)
            {
                Debug.LogError("[TitleClayUiVisualUtility] thumbnailの親がありません");
                return;
            }

            RectTransform thumbRect = thumbnailImage.rectTransform;
            Vector2 anchorMin = thumbRect.anchorMin;
            Vector2 anchorMax = thumbRect.anchorMax;
            Vector2 anchoredPosition = thumbRect.anchoredPosition;
            Vector2 sizeDelta = thumbRect.sizeDelta;
            Vector2 pivot = thumbRect.pivot;
            int siblingIndex = thumbRect.GetSiblingIndex();

            var maskObject = new GameObject(
                "ThumbnailMask",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Mask));
            maskObject.layer = frameParent.gameObject.layer;
            RectTransform maskRect = maskObject.GetComponent<RectTransform>();
            maskRect.SetParent(frameParent, false);
            maskRect.SetSiblingIndex(siblingIndex);
            maskRect.anchorMin = anchorMin;
            maskRect.anchorMax = anchorMax;
            maskRect.anchoredPosition = anchoredPosition;
            maskRect.sizeDelta = sizeDelta;
            maskRect.pivot = pivot;

            Image maskImage = maskObject.GetComponent<Image>();
            ApplySlicedSprite(maskImage, LoadSlotInnerSprite());
            maskImage.raycastTarget = false;
            maskImage.color = Color.white;

            Mask mask = maskObject.GetComponent<Mask>();
            mask.showMaskGraphic = false;

            thumbRect.SetParent(maskRect, false);
            thumbRect.anchorMin = Vector2.zero;
            thumbRect.anchorMax = Vector2.one;
            thumbRect.anchoredPosition = Vector2.zero;
            thumbRect.sizeDelta = Vector2.zero;
            thumbRect.pivot = new Vector2(0.5f, 0.5f);
            thumbnailImage.maskable = true;
        }

        /// <summary>
        /// 枠配下からサムネイルImageを探す
        /// </summary>
        /// <param name="thumbnailFrame">ThumbnailFrame</param>
        /// <returns>サムネイルImage</returns>
        public static Image FindThumbnailImage(Transform thumbnailFrame)
        {
            if (thumbnailFrame == null)
            {
                return null;
            }

            Transform direct = thumbnailFrame.Find("Thumbnail")
                ?? thumbnailFrame.Find("ThumbnailImage");
            if (direct != null)
            {
                return direct.GetComponent<Image>();
            }

            Transform masked = thumbnailFrame.Find("ThumbnailMask/Thumbnail")
                ?? thumbnailFrame.Find("ThumbnailMask/ThumbnailImage");
            return masked != null ? masked.GetComponent<Image>() : null;
        }

        /// <summary>
        /// スロットを空き表示に戻す
        /// 色はシーン配置を維持する
        /// </summary>
        public static void ApplySlotEmptyImage(Image image)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = null;
            image.preserveAspect = false;
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
            ApplySlicedSprite(background, LoadButtonNormalSprite());

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
                ApplySlicedSprite(target, LoadButtonNormalSprite());
                target.pixelsPerUnitMultiplier = 1f;
            }

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
            _ = arrowImage;
        }

        /// <summary>
        /// ドロップダウン項目トグルへ粘土風スタイルを適用する
        /// </summary>
        public static void ApplyDropdownItemToggle(Toggle toggle)
        {
            _ = toggle;
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
            _ = checkmarkImage;
        }

        /// <summary>
        /// ドロップダウン項目背景へ粘土風スタイルを適用する
        /// </summary>
        public static void ApplyDropdownItemBackground(Image image)
        {
            ApplySlicedSprite(image, LoadButtonNormalSprite());
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
            ApplySlicedSprite(image, LoadPanelSprite());
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
            }

            Image fill = slider.fillRect != null
                ? slider.fillRect.GetComponent<Image>()
                : slider.transform.Find("Fill Area/Fill")?.GetComponent<Image>();
            if (fill != null)
            {
                fill.sprite = fillSprite;
                fill.type = Image.Type.Sliced;
            }

            Image handle = slider.handleRect != null
                ? slider.handleRect.GetComponent<Image>()
                : slider.transform.Find("Handle Slide Area/Handle")?.GetComponent<Image>();
            if (handle != null)
            {
                handle.sprite = handleSprite;
                handle.type = Image.Type.Simple;
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
                checkmark.SetNativeSize();
                toggle.graphic = checkmark;
            }
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

            _ = isPlayer;
            ApplySlicedSprite(image, LoadPanelSprite());
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
            ApplySlicedSprite(image, LoadSliderTrackSprite());
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

            ApplySlicedSprite(image, LoadSliderFillSprite());
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
                        }

                        break;
                    case "AttackSeparator":
                        if (child.TryGetComponent(out Image separatorImage))
                        {
                            separatorImage.sprite = null;
                            separatorImage.type = Image.Type.Simple;
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
                image.pixelsPerUnitMultiplier = 1f;
            }

            button.transition = Selectable.Transition.SpriteSwap;
            SpriteState spriteState = button.spriteState;
            spriteState.highlightedSprite = LoadButtonHighlightedSprite() ?? normalSprite;
            spriteState.pressedSprite = LoadButtonPressedSprite() ?? normalSprite;
            spriteState.selectedSprite = spriteState.highlightedSprite;
            spriteState.disabledSprite = normalSprite;
            button.spriteState = spriteState;
        }

        private static void ApplyMenuButtonSpritesForEditorBake(LHButton button)
        {
            ApplyMenuButtonSprites(button);

            if (button?.targetGraphic is Image image)
            {
                image.color = Color.white;
            }

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

        private static void ApplySlicedSprite(Image image, Sprite sprite)
        {
            if (image == null)
            {
                return;
            }

            if (sprite == null)
            {
                image.type = Image.Type.Simple;
                return;
            }

            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
        }

        private static void ApplySlicedSpriteForEditorBake(Image image, Sprite sprite, Color color)
        {
            ApplySlicedSprite(image, sprite);
            if (image != null)
            {
                image.color = color;
            }
        }

        private static void ApplyTextOutline(TMP_Text text, float outlineWidth, Color outlineColor)
        {
            AppTmpFontUtility.ApplyOutline(text, outlineWidth, outlineColor);
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

        private static Sprite LoadSlotInnerSprite() =>
            LoadGameUiSprite("GameUi_SlotInner", ref slotInnerSprite);

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
