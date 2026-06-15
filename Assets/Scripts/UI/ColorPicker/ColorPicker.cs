using R3;
using UnityEngine;
using UnityEngine.UI;

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

        private readonly Subject<Color> onColorSelected = new Subject<Color>();
        public Observable<Color> OnColorSelected => onColorSelected;

        // 動的生成する背景テクスチャ
        private Texture2D hueTex, satTex, valTex;

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

            // スライダーの背景テクスチャを初期化
            InitializeBackgroundTextures();

            // 現在のスライダー値に基づいて初回表示を更新
            SyncUIAndBackgrounds();

            // 3つのスライダーのどれかが動いたら処理を走らせる
            Observable.Merge(
                hueSlider.onValueChanged.AsObservable(),
                saturationSlider.onValueChanged.AsObservable(),
                valueSlider.onValueChanged.AsObservable()
            )
            .Subscribe(_ => OnSliderValueChanged())
            .AddTo(this);

            OnSliderValueChanged();
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

        public void SetColorDirectly(Color presetColor)
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

        private void OnDestroy()
        {
            // メモリリークを防ぐため、動的に生成したテクスチャを破棄
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
        }
    }
}
