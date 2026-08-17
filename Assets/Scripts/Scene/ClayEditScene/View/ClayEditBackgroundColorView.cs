using Audio;
using Audio.Interface;
using ClayEditor.Input.Interface;
using Localization;
using R3;
using Scene.ClayEditScene.Interface;
using UI.ClayEditor.View;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;

namespace Scene.ClayEditScene.View
{
    /// <summary>
    /// ClayEditのカメラ背景色をスライダーで変更するView
    /// 0が黒1が灰色のグレースケールとして反映する
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ClayEditBackgroundColorView : MonoBehaviour, ILanguageAwareUi
    {
        private const float MaxGray = 0.5f;

        [SerializeField] private Canvas rootCanvas;
        [SerializeField] private Slider backgroundColorSlider;
        [SerializeField] private Image gaugeBackground;

        private IClayEditPostProcess clayEditPostProcess;
        private IClayInputProvider clayInputProvider;
        private ISeService seService;
        private LocalizedBakedTextApplier bakedLabelApplier;
        private SliderMoveSeBinder sliderSeBinder;
        private bool isCapturingPointer;
        private Texture2D gaugeTexture;
        private Sprite gaugeSprite;

        [Inject]
        public void Construct(
            IClayEditPostProcess clayEditPostProcess,
            IClayInputProvider clayInputProvider,
            ISeService seService)
        {
            this.clayEditPostProcess = clayEditPostProcess;
            this.clayInputProvider = clayInputProvider;
            this.seService = seService;
        }

        private void Start()
        {
            if (rootCanvas == null || backgroundColorSlider == null || gaugeBackground == null)
            {
                Debug.LogError(
                    "[ClayEditBackgroundColorView] 必須SerializeFieldが未配線ですHierarchyで接続してください",
                    this);
                return;
            }

            if (clayEditPostProcess == null)
            {
                Debug.LogError(
                    "[ClayEditBackgroundColorView] IClayEditPostProcessが注入されていません",
                    this);
                return;
            }

            if (clayInputProvider == null)
            {
                Debug.LogError(
                    "[ClayEditBackgroundColorView] IClayInputProviderが注入されていません",
                    this);
                return;
            }

            ApplyLocalizedLabels();

            backgroundColorSlider.minValue = 0f;
            backgroundColorSlider.maxValue = 1f;
            float initialValue = ColorToSliderValue(clayEditPostProcess.BackgroundColor);
            backgroundColorSlider.SetValueWithoutNotify(initialValue);
            InitializeGaugeBackground();
            ClayEditUiVisualUtility.ApplySlider(backgroundColorSlider);
            ClayEditUiVisualUtility.EnsureRoundedMask(gaugeBackground);
            BindSliderInputBlock();
            BindSliderMoveSe();

            backgroundColorSlider.onValueChanged.AsObservable()
                .Subscribe(ApplySliderValue)
                .AddTo(this);

            ApplySliderValue(initialValue);
        }


        /// <inheritdoc/>
        public void RefreshLocalizedUi()
        {
            ApplyLocalizedLabels();
        }

        private void ApplyLocalizedLabels()
        {
            if (bakedLabelApplier == null)
            {
                bakedLabelApplier = new LocalizedBakedTextApplier();
                bakedLabelApplier.Register(
                    GameTextKeys.ClayEditBackgroundColor,
                    "背景色");
                Transform root = rootCanvas != null ? rootCanvas.transform : transform;
                bakedLabelApplier.Capture(root);
            }

            bakedLabelApplier.Apply();
        }

        private void OnDisable()
        {
            EndPointerCapture();
            sliderSeBinder?.Stop();
        }

        private void OnDestroy()
        {
            EndPointerCapture();
            sliderSeBinder?.Dispose();
            sliderSeBinder = null;
            if (gaugeSprite != null)
            {
                Destroy(gaugeSprite);
                gaugeSprite = null;
            }

            if (gaugeTexture != null)
            {
                Destroy(gaugeTexture);
                gaugeTexture = null;
            }
        }

        private void InitializeGaugeBackground()
        {
            gaugeTexture = new Texture2D(2, 1, TextureFormat.RGB24, false, false);
            gaugeTexture.wrapMode = TextureWrapMode.Clamp;
            gaugeTexture.filterMode = FilterMode.Bilinear;
            gaugeTexture.SetPixel(0, 0, Color.black);
            gaugeTexture.SetPixel(1, 0, new Color(MaxGray, MaxGray, MaxGray, 1f));
            gaugeTexture.Apply();

            gaugeSprite = Sprite.Create(gaugeTexture, new Rect(0f, 0f, 2f, 1f), Vector2.zero);
            gaugeBackground.sprite = gaugeSprite;
            gaugeBackground.type = Image.Type.Simple;
            gaugeBackground.color = Color.white;
            gaugeBackground.raycastTarget = true;
        }

        private void BindSliderInputBlock()
        {
            EventTrigger trigger = backgroundColorSlider.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = backgroundColorSlider.gameObject.AddComponent<EventTrigger>();
            }

            AddTrigger(trigger, EventTriggerType.PointerDown, BeginPointerCapture);
            AddTrigger(trigger, EventTriggerType.BeginDrag, BeginPointerCapture);
            AddTrigger(trigger, EventTriggerType.PointerUp, EndPointerCapture);
            AddTrigger(trigger, EventTriggerType.EndDrag, EndPointerCapture);
            AddTrigger(trigger, EventTriggerType.Cancel, EndPointerCapture);
        }

        private void BindSliderMoveSe()
        {
            if (seService == null)
            {
                Debug.LogError("[ClayEditBackgroundColorView] ISeServiceが注入されていません", this);
                return;
            }

            sliderSeBinder = new SliderMoveSeBinder(seService, SeTrackId.ClayEditSlider);
            sliderSeBinder.Attach(backgroundColorSlider);
        }

        private static void AddTrigger(EventTrigger trigger, EventTriggerType type, UnityAction action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }

        private void BeginPointerCapture()
        {
            if (isCapturingPointer || clayInputProvider == null)
            {
                return;
            }

            isCapturingPointer = true;
            clayInputProvider.BeginUiPointerCapture();
        }

        private void EndPointerCapture()
        {
            if (!isCapturingPointer || clayInputProvider == null)
            {
                return;
            }

            isCapturingPointer = false;
            clayInputProvider.EndUiPointerCapture();
        }

        private void ApplySliderValue(float value)
        {
            if (clayEditPostProcess == null)
            {
                return;
            }

            clayEditPostProcess.SetBackgroundColor(SliderValueToColor(value));
        }

        private static Color SliderValueToColor(float value)
        {
            float gray = Mathf.Clamp01(value) * MaxGray;
            return new Color(gray, gray, gray, 1f);
        }

        private static float ColorToSliderValue(Color color)
        {
            float luminance = (color.r + color.g + color.b) / 3f;
            return Mathf.Clamp01(luminance / MaxGray);
        }
    }
}
