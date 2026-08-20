using Audio;
using Audio.Interface;
using R3;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace UI.ColorPicker
{
    public class ColorPicker : MonoBehaviour
    {
        [SerializeField] private Image colorPreview;
        [SerializeField] private Slider hueSlider;
        [SerializeField] private Slider saturationSlider;
        [SerializeField] private Slider valueSlider;

        [SerializeField] private Image hueBackground;
        [SerializeField] private Image saturationBackground;
        [SerializeField] private Image valueBackground;

        [Header("Color History")]
        [SerializeField] private Button[] historyButtons;

        [Header("Eyedropper")]
        [SerializeField] private Button eyedropperButton;

        private readonly Subject<Color> onColorSelected = new Subject<Color>();
        public Observable<Color> OnColorSelected => onColorSelected;

        // 過去に使用した色の履歴(新しい順)が更新されたときに通知する
        private readonly Subject<IReadOnlyList<Color>> onHistoryChanged = new Subject<IReadOnlyList<Color>>();
        private readonly ReactiveProperty<bool> isEyedropperActive = new(false);

        /// <summary>
        /// 使用色履歴が更新されたときに 新しい順の色リストを通知する
        /// </summary>
        public Observable<IReadOnlyList<Color>> OnHistoryChanged => onHistoryChanged;

        /// <summary>
        /// スポイト採取待ち状態の変化
        /// </summary>
        public Observable<bool> OnEyedropperActiveChanged => isEyedropperActive;

        /// <summary>
        /// スポイトでモデル色を採取する待ち状態か
        /// </summary>
        public bool IsEyedropperActive => isEyedropperActive.Value;

        // 過去に使用した色(先頭が最新)
        private readonly List<Color> colorHistory = new List<Color>();
        private ISeService seService;
        private SliderMoveSeBinder sliderSeBinder;

        /// <summary>
        /// 過去に使用した色の履歴(新しい順)
        /// </summary>
        public IReadOnlyList<Color> ColorHistory => colorHistory;

        // 動的生成した背景テクスチャ
        private Texture2D hueTex, satTex, valTex;

        [Inject]
        public void Construct(ISeService seService)
        {
            this.seService = seService;
        }

        private void Start()
        {
            // スライダーの範囲設定
            if (hueSlider)
            {
                hueSlider.minValue = 0f;
                hueSlider.maxValue = 1f;
                hueSlider.value = 0f;
            }
            if (saturationSlider)
            {
                saturationSlider.minValue = 0f;
                saturationSlider.maxValue = 1f;
                saturationSlider.value = 1f;
            }
            if (valueSlider)
            {
                valueSlider.minValue = 0f;
                valueSlider.maxValue = 1f;
                valueSlider.value = 1f;
            }

            // スライダーの背景テクスチャを初期化する
            InitializeBackgroundTextures();

            // 現在のスライダー値に基づいて初期表示を更新する
            SyncUIAndBackgrounds();

            // 3つのスライダーのどれかが動いたら処理を走らせる
            Observable.Merge(
                hueSlider.onValueChanged.AsObservable(),
                saturationSlider.onValueChanged.AsObservable(),
                valueSlider.onValueChanged.AsObservable()
            )
            .Subscribe(_ => OnSliderValueChanged())
            .AddTo(this);

            BindSliderMoveSe();

            OnSliderValueChanged();

            // 履歴ボタンが押されたときの処理を登録する
            for (int i = 0; i < historyButtons.Length; i++)
            {
                int index = i;
                if (historyButtons[index] == null)
                {
                    continue;
                }

                historyButtons[index].onClick.AsObservable()
                    .Subscribe(_ => OnHistoryButtonClicked(index))
                    .AddTo(this);
            }

            // 履歴更新を購読してボタン表示を更新する
            OnHistoryChanged
                .Subscribe(UpdateButtons)
                .AddTo(this);

            // 初期表示
            UpdateButtons(colorHistory);
            ApplyVisualStyles();
            BindEyedropperButton();
        }

        /// <summary>
        /// スポイト採取待ちを開始する
        /// </summary>
        public void BeginEyedropper()
        {
            isEyedropperActive.Value = true;
            RefreshEyedropperVisual();
        }

        /// <summary>
        /// スポイト採取待ちを解除する
        /// </summary>
        public void CancelEyedropper()
        {
            if (!isEyedropperActive.Value)
            {
                return;
            }

            isEyedropperActive.Value = false;
            RefreshEyedropperVisual();
        }

        /// <summary>
        /// スポイトで取得した色をピッカーへ反映する
        /// </summary>
        /// <param name="color">採取した表示色</param>
        public void ApplySampledColor(Color color)
        {
            SelectColor(color);
            CancelEyedropper();
        }

        private void BindEyedropperButton()
        {
            if (eyedropperButton == null)
            {
                Debug.LogError(
                    "[ColorPicker] eyedropperButtonが未配線ですHierarchyで接続してください",
                    this);
                return;
            }

            ApplyEyedropperIcon(eyedropperButton);
            eyedropperButton.onClick.AsObservable()
                .Subscribe(_ =>
                {
                    if (isEyedropperActive.Value)
                    {
                        CancelEyedropper();
                    }
                    else
                    {
                        BeginEyedropper();
                    }
                })
                .AddTo(this);
            RefreshEyedropperVisual();
        }

        private void ApplyEyedropperIcon(Button button)
        {
            if (button == null)
            {
                return;
            }

            Sprite spoito = Resources.Load<Sprite>("Image/InputIcons/Spoito");
            if (spoito == null)
            {
                Debug.LogWarning("[ColorPicker] Spoitoアイコンが見つかりません: Image/InputIcons/Spoito", this);
                return;
            }

            Image image = button.targetGraphic as Image;
            if (image == null)
            {
                image = button.GetComponent<Image>();
            }

            if (image == null)
            {
                return;
            }

            image.sprite = spoito;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = Color.white;
        }

        private void RefreshEyedropperVisual()
        {
            if (eyedropperButton == null)
            {
                return;
            }

            Graphic graphic = eyedropperButton.targetGraphic;
            if (graphic == null)
            {
                return;
            }

            graphic.color = isEyedropperActive.Value
                ? new Color(1f, 0.85f, 0.35f, 1f)
                : Color.white;
        }

        private void BindSliderMoveSe()
        {
            if (seService == null)
            {
                Debug.LogError("[ColorPicker] ISeServiceが注入されていません", this);
                return;
            }

            sliderSeBinder = new SliderMoveSeBinder(seService, SeTrackId.ClayEditSlider);
            sliderSeBinder.Attach(hueSlider);
            sliderSeBinder.Attach(saturationSlider);
            sliderSeBinder.Attach(valueSlider);
        }

        private void ApplyVisualStyles()
        {
            ApplySliderStyle(hueSlider, "GameUi_SliderFill", new Color(0.88f, 0.58f, 0.28f, 1f));
            ApplySliderStyle(saturationSlider, "GameUi_SliderFill", new Color(0.88f, 0.58f, 0.28f, 1f));
            ApplySliderStyle(valueSlider, "GameUi_SliderFill", new Color(0.88f, 0.58f, 0.28f, 1f));
            ApplyHistoryButtons(historyButtons);
            ApplyRoundedCorners();
        }

        private void ApplyRoundedCorners()
        {
            // 色プレビューとHSV背景の矩形を角丸マスクでクリップする
            EnsureRoundedMask(colorPreview, "GameUi_SlotInner");
            EnsureRoundedMask(hueBackground, "GameUi_SliderFill");
            EnsureRoundedMask(saturationBackground, "GameUi_SliderFill");
            EnsureRoundedMask(valueBackground, "GameUi_SliderFill");
            EnsureRoundedMaskOnSliderTrack(hueSlider);
            EnsureRoundedMaskOnSliderTrack(saturationSlider);
            EnsureRoundedMaskOnSliderTrack(valueSlider);
        }

        private static void EnsureRoundedMaskOnSliderTrack(Slider slider)
        {
            if (slider == null)
            {
                return;
            }

            // Background直下やfill親のImageを角丸にする
            Transform track = slider.transform.Find("Background");
            if (track != null && track.TryGetComponent(out Image backgroundImage))
            {
                EnsureRoundedMask(backgroundImage, "GameUi_SliderFill");
            }

            if (slider.fillRect != null)
            {
                Image fill = slider.fillRect.GetComponent<Image>();
                EnsureSlicedRoundedSprite(fill, "GameUi_SliderFill", fill != null ? fill.color : Color.white);
            }
        }

        private static void EnsureSlicedRoundedSprite(Image image, string spriteName, Color color)
        {
            if (image == null)
            {
                return;
            }

            Sprite sprite = Resources.Load<Sprite>("Image/GameUi/" + spriteName);
            if (sprite == null)
            {
                return;
            }

            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
            image.color = color;
            image.maskable = true;
        }

        private static void EnsureRoundedMask(Image contentImage, string maskSpriteName)
        {
            if (contentImage == null)
            {
                return;
            }

            Transform currentParent = contentImage.transform.parent;
            if (currentParent != null && currentParent.name == "RoundMask")
            {
                Mask existingMask = currentParent.GetComponent<Mask>();
                if (existingMask != null)
                {
                    existingMask.showMaskGraphic = false;
                    contentImage.maskable = true;
                    EnsureSlicedRoundedSprite(
                        currentParent.GetComponent<Image>(),
                        maskSpriteName,
                        Color.white);
                    return;
                }
            }

            Transform frameParent = currentParent;
            if (frameParent == null)
            {
                Debug.LogError("[ColorPicker] 角丸対象Imageの親がありません: " + contentImage.name);
                return;
            }

            RectTransform contentRect = contentImage.rectTransform;
            Vector2 anchorMin = contentRect.anchorMin;
            Vector2 anchorMax = contentRect.anchorMax;
            Vector2 anchoredPosition = contentRect.anchoredPosition;
            Vector2 sizeDelta = contentRect.sizeDelta;
            Vector2 pivot = contentRect.pivot;
            Vector3 localScale = contentRect.localScale;
            int siblingIndex = contentRect.GetSiblingIndex();

            var maskObject = new GameObject(
                "RoundMask",
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
            // 元Imageのスケールをマスクへ引き継ぎ見た目サイズを維持する
            maskRect.localScale = localScale;
            maskRect.localRotation = contentRect.localRotation;

            Image maskImage = maskObject.GetComponent<Image>();
            EnsureSlicedRoundedSprite(maskImage, maskSpriteName, Color.white);
            maskImage.raycastTarget = contentImage.raycastTarget;
            contentImage.raycastTarget = false;

            Mask mask = maskObject.GetComponent<Mask>();
            mask.showMaskGraphic = false;

            contentRect.SetParent(maskRect, false);
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;
            contentRect.pivot = new Vector2(0.5f, 0.5f);
            contentRect.localScale = Vector3.one;
            contentRect.localRotation = Quaternion.identity;
            contentImage.maskable = true;
        }

        private static void ApplySliderStyle(Slider slider, string spriteName, Color tint)
        {
            if (slider == null)
            {
                return;
            }

            Sprite sprite = Resources.Load<Sprite>("Image/GameUi/" + spriteName);
            Sprite handleSprite = Resources.Load<Sprite>("Image/GameUi/GameUi_SliderHandle");

            if (slider.fillRect != null)
            {
                Image fill = slider.fillRect.GetComponent<Image>();
                if (fill != null && sprite != null)
                {
                    fill.sprite = sprite;
                    fill.type = Image.Type.Sliced;
                    fill.color = tint;
                }
            }

            if (slider.handleRect != null)
            {
                Image handle = slider.handleRect.GetComponent<Image>();
                if (handle != null && handleSprite != null)
                {
                    handle.sprite = handleSprite;
                    handle.type = Image.Type.Sliced;
                    handle.color = Color.white;
                }
            }
        }

        private static void ApplyHistoryButtons(Button[] buttons)
        {
            if (buttons == null)
            {
                return;
            }

            Sprite sprite = Resources.Load<Sprite>("Image/GameUi/GameUi_Frame");
            Color tint = new Color(0.42f, 0.46f, 0.54f, 1f);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button == null)
                {
                    continue;
                }

                Image image = button.targetGraphic as Image;
                if (image != null && sprite != null)
                {
                    image.sprite = sprite;
                    image.type = Image.Type.Sliced;
                    image.color = tint;
                }
            }
        }

        private void SyncUIAndBackgrounds()
        {
            float h = hueSlider ? hueSlider.value : 0f;
            float s = saturationSlider ? saturationSlider.value : 0f;
            float v = valueSlider ? valueSlider.value : 0f;

            UpdateSliderBackgrounds(h, s, v);
            UpdatePreviewColor(Color.HSVToRGB(h, s, v));
        }

        private void OnSliderValueChanged()
        {
            float h = hueSlider != null ? hueSlider.value : 0f;
            float s = saturationSlider != null ? saturationSlider.value : 0f;
            float v = valueSlider != null ? valueSlider.value : 0f;

            Color newColor = Color.HSVToRGB(h, s, v);
            UpdatePreviewColor(newColor);
            UpdateSliderBackgrounds(h, s, v);
            onColorSelected.OnNext(newColor);
        }

        /// <summary>
        /// 確定した色を使用色履歴へ追加する
        /// </summary>
        /// <param name="color">履歴へ追加する色</param>
        public void AddColorToHistory(Color color)
        {
            // 既存の同色を取り除いてから先頭へ追加する(重複防止と最新化)
            for (int i = colorHistory.Count - 1; i >= 0; i--)
            {
                if (ApproximatelyEqual(colorHistory[i], color))
                {
                    colorHistory.RemoveAt(i);
                }
            }

            colorHistory.Insert(0, color);

            int max = Mathf.Max(historyButtons.Length, 1);
            while (colorHistory.Count > max)
            {
                colorHistory.RemoveAt(colorHistory.Count - 1);
            }

            onHistoryChanged.OnNext(colorHistory);
        }

        /// <summary>
        /// 使用色履歴をすべて消去する
        /// </summary>
        public void ClearHistory()
        {
            colorHistory.Clear();
            onHistoryChanged.OnNext(colorHistory);
        }

        // RGBがほぼ等しいかどうか(履歴の重複判定に使う)
        private static bool ApproximatelyEqual(Color a, Color b)
        {
            const float epsilon = 0.001f;
            return Mathf.Abs(a.r - b.r) < epsilon
                && Mathf.Abs(a.g - b.g) < epsilon
                && Mathf.Abs(a.b - b.b) < epsilon;
        }

        private void InitializeBackgroundTextures()
        {
            // 色相
            if (hueBackground != null)
            {
                hueTex = new Texture2D(256, 1, TextureFormat.RGB24, false, false);
                hueTex.wrapMode = TextureWrapMode.Clamp;
                for (int i = 0; i < 256; i++)
                {
                    hueTex.SetPixel(i, 0, Color.HSVToRGB(i / 255f, 1f, 1f));
                }
                hueTex.Apply();
                hueBackground.sprite = Sprite.Create(hueTex, new Rect(0, 0, 256, 1), Vector2.zero);
                hueBackground.color = Color.white;
            }

            // 彩度
            if (saturationBackground != null)
            {
                satTex = new Texture2D(2, 1, TextureFormat.RGB24, false, false);
                satTex.wrapMode = TextureWrapMode.Clamp;
                saturationBackground.sprite = Sprite.Create(satTex, new Rect(0, 0, 2, 1), Vector2.zero);
                saturationBackground.color = Color.white;
            }

            // 明度
            if (valueBackground != null)
            {
                valTex = new Texture2D(2, 1, TextureFormat.RGB24, false, false);
                valTex.wrapMode = TextureWrapMode.Clamp;
                valueBackground.sprite = Sprite.Create(valTex, new Rect(0, 0, 2, 1), Vector2.zero);
                valueBackground.color = Color.white;
            }
        }

        private void UpdateSliderBackgrounds(float h, float s, float v)
        {
            // 彩度スライダーの背景
            if (satTex != null)
            {
                satTex.SetPixel(0, 0, Color.HSVToRGB(h, 0f, v));
                satTex.SetPixel(1, 0, Color.HSVToRGB(h, 1f, v));
                satTex.Apply();
            }

            // 明度スライダーの背景
            if (valTex != null)
            {
                valTex.SetPixel(0, 0, Color.black);
                valTex.SetPixel(1, 0, Color.HSVToRGB(h, s, 1f));
                valTex.Apply();
            }
        }

        private void UpdatePreviewColor(Color color)
        {
            if (colorPreview != null)
            {
                colorPreview.color = color;
            }
        }

        private void OnHistoryButtonClicked(int index)
        {
            if (index < 0 || index >= ColorHistory.Count)
            {
                return;
            }

            SelectColor(ColorHistory[index]);
        }

        // 履歴の内容に合わせてボタンの色と表示状態を更新する
        private void UpdateButtons(IReadOnlyList<Color> history)
        {
            for (int i = 0; i < historyButtons.Length; i++)
            {
                if (historyButtons[i] == null)
                {
                    continue;
                }

                bool hasColor = i < history.Count;
                historyButtons[i].gameObject.SetActive(hasColor);

                if (hasColor)
                {
                    Graphic target = historyButtons[i].targetGraphic;
                    if (target != null)
                    {
                        target.color = history[i];
                    }
                }
            }
        }

        private void SetColorDirectly(Color presetColor)
        {
            Color.RGBToHSV(presetColor, out float h, out float s, out float v);
            if (hueSlider)
            {
                hueSlider.value = h;
            }
            if (saturationSlider)
            {
                saturationSlider.value = s;
            }
            if (valueSlider)
            {
                valueSlider.value = v;
            }

            SyncUIAndBackgrounds();
        }

        /// <summary>
        /// 指定した色をピッカーへ反映し選択色として通知する
        /// </summary>
        /// <param name="color">選択する色</param>
        private void SelectColor(Color color)
        {
            SetColorDirectly(color);

            // ペイント色へ確実に伝えるため、選択色として明示的に通知する
            UpdatePreviewColor(color);
            onColorSelected.OnNext(color);
        }

        private void OnDisable()
        {
            CancelEyedropper();
            sliderSeBinder?.Stop();
        }

        private void OnDestroy()
        {
            // メモリリークを防ぐため 動的に生成したテクスチャを破棄
            if (hueTex != null)
            {
                Destroy(hueTex);
            }
            if (satTex != null)
            {
                Destroy(satTex);
            }
            if (valTex != null)
            {
                Destroy(valTex);
            }

            onColorSelected.OnCompleted();
            onColorSelected.Dispose();

            onHistoryChanged.OnCompleted();
            onHistoryChanged.Dispose();

            isEyedropperActive.Dispose();

            sliderSeBinder?.Dispose();
            sliderSeBinder = null;
        }
    }
}