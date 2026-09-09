using Localization;
using UnityEngine;

namespace Scene.TrainingScene.Domain
{
    /// <summary>
    /// 天候の表示と訓練倍率を解決する
    /// </summary>
    public static class TrainingWeatherCatalog
    {
        private const string ResourcesRoot = "Image/Training/Weather/";
        private static readonly Sprite[][] frameCache = new Sprite[TypeCount][];

        private static readonly string[] ResourceNames =
        {
            "clear",
            "cloudy",
            "rainy"
        };

        /// <summary>
        /// 種類数
        /// </summary>
        public const int TypeCount = 3;

        /// <summary>
        /// 1種類あたりのアニメーション連番フレーム数
        /// </summary>
        public const int AnimationFrameCount = 4;

        /// <summary>
        /// 抽選の合計重み(快晴4曇り1雨1)
        /// </summary>
        public const int RollWeightTotal =
            TrainingSettings.WeatherRollWeightClear
            + TrainingSettings.WeatherRollWeightCloudy
            + TrainingSettings.WeatherRollWeightRainy;

        /// <summary>
        /// 表示名を返す
        /// </summary>
        /// <param name="weather">天候</param>
        public static string GetDisplayName(TrainingWeather weather)
        {
            return weather switch
            {
                TrainingWeather.Clear => LocalizedText.GetOrFallback(
                    GameTextKeys.TrainingWeatherClear, "快晴"),
                TrainingWeather.Cloudy => LocalizedText.GetOrFallback(
                    GameTextKeys.TrainingWeatherCloudy, "曇り"),
                TrainingWeather.Rainy => LocalizedText.GetOrFallback(
                    GameTextKeys.TrainingWeatherRainy, "雨"),
                _ => weather.ToString()
            };
        }

        /// <summary>
        /// ホバー用の説明文を返す
        /// </summary>
        /// <param name="weather">天候</param>
        public static string GetDescription(TrainingWeather weather)
        {
            return weather switch
            {
                TrainingWeather.Clear => LocalizedText.GetOrFallback(
                    GameTextKeys.TrainingWeatherDescClear,
                    "快晴\nトレーニングでのステータス上昇値 / 大成功率少しアップ。"),
                TrainingWeather.Cloudy => LocalizedText.GetOrFallback(
                    GameTextKeys.TrainingWeatherDescCloudy,
                    "曇り\nトレーニングでのステータス上昇値少しダウン / 強敵遭遇率少しアップ。"),
                TrainingWeather.Rainy => LocalizedText.GetOrFallback(
                    GameTextKeys.TrainingWeatherDescRainy,
                    "雨\nトレーニングでのステータス上昇値ダウン / 強敵遭遇率アップ。"),
                _ => string.Empty
            };
        }

        /// <summary>
        /// 範囲内へ丸める
        /// </summary>
        /// <param name="value">生値</param>
        public static TrainingWeather Clamp(int value)
        {
            return (TrainingWeather)Mathf.Clamp(value, 0, TypeCount - 1);
        }

        /// <summary>
        /// 比率抽選する
        /// </summary>
        /// <param name="random">乱数</param>
        public static TrainingWeather Roll(System.Random random = null)
        {
            random ??= new System.Random();
            int roll = random.Next(0, RollWeightTotal);
            if (roll < TrainingSettings.WeatherRollWeightClear)
            {
                return TrainingWeather.Clear;
            }

            roll -= TrainingSettings.WeatherRollWeightClear;
            if (roll < TrainingSettings.WeatherRollWeightCloudy)
            {
                return TrainingWeather.Cloudy;
            }

            return TrainingWeather.Rainy;
        }

        /// <summary>
        /// 訓練ステ上昇倍率を返す
        /// </summary>
        /// <param name="weather">天候</param>
        public static float GetTrainGainMultiplier(TrainingWeather weather)
        {
            return weather switch
            {
                TrainingWeather.Clear => TrainingSettings.WeatherTrainMultiplierClear,
                TrainingWeather.Cloudy => TrainingSettings.WeatherTrainMultiplierCloudy,
                TrainingWeather.Rainy => TrainingSettings.WeatherTrainMultiplierRainy,
                _ => 1f
            };
        }

        /// <summary>
        /// 訓練大成功率の加算(百分率)を返す
        /// </summary>
        /// <param name="weather">天候</param>
        public static float GetGreatSuccessBonusPercent(TrainingWeather weather)
        {
            return weather switch
            {
                TrainingWeather.Clear => TrainingSettings.WeatherGreatSuccessBonusClear,
                TrainingWeather.Cloudy => TrainingSettings.WeatherGreatSuccessBonusCloudy,
                TrainingWeather.Rainy => TrainingSettings.WeatherGreatSuccessBonusRainy,
                _ => 0f
            };
        }

        /// <summary>
        /// 強敵遭遇率倍率を返す
        /// </summary>
        /// <param name="weather">天候</param>
        public static float GetAmbushRateMultiplier(TrainingWeather weather)
        {
            return weather switch
            {
                TrainingWeather.Clear => TrainingSettings.WeatherAmbushMultiplierClear,
                TrainingWeather.Cloudy => TrainingSettings.WeatherAmbushMultiplierCloudy,
                TrainingWeather.Rainy => TrainingSettings.WeatherAmbushMultiplierRainy,
                _ => 1f
            };
        }

        /// <summary>
        /// 天候アイコンの先頭フレームを返す
        /// </summary>
        /// <param name="weather">天候</param>
        public static Sprite ResolveIcon(TrainingWeather weather)
        {
            Sprite[] frames = ResolveIconFrames(weather);
            return frames != null && frames.Length > 0 ? frames[0] : null;
        }

        /// <summary>
        /// 天候アイコンの連番アニメーションフレームを返す
        /// スプライトシートを横4分割して返す
        /// </summary>
        /// <param name="weather">天候</param>
        public static Sprite[] ResolveIconFrames(TrainingWeather weather)
        {
            int index = (int)Clamp((int)weather);
            if (frameCache[index] != null && frameCache[index].Length == AnimationFrameCount)
            {
                return frameCache[index];
            }

            string path = ResourcesRoot + ResourceNames[index];
            Texture2D sheet = Resources.Load<Texture2D>(path);
            if (sheet == null)
            {
                Debug.LogError(
                    $"[TrainingWeatherCatalog] 天候スプライトシートが見つかりません path={path}");
                frameCache[index] = System.Array.Empty<Sprite>();
                return frameCache[index];
            }

            if (sheet.width < AnimationFrameCount || sheet.height <= 0)
            {
                Debug.LogError(
                    $"[TrainingWeatherCatalog] 天候スプライトシートサイズが不正です path={path} size={sheet.width}x{sheet.height}");
                frameCache[index] = System.Array.Empty<Sprite>();
                return frameCache[index];
            }

            int frameWidth = sheet.width / AnimationFrameCount;
            if (frameWidth * AnimationFrameCount != sheet.width)
            {
                Debug.LogError(
                    $"[TrainingWeatherCatalog] 天候スプライトシート幅がフレーム数で割り切れません path={path} width={sheet.width}");
                frameCache[index] = System.Array.Empty<Sprite>();
                return frameCache[index];
            }

            var frames = new Sprite[AnimationFrameCount];
            for (int frame = 0; frame < AnimationFrameCount; frame++)
            {
                frames[frame] = Sprite.Create(
                    sheet,
                    new Rect(frame * frameWidth, 0f, frameWidth, sheet.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
            }

            frameCache[index] = frames;
            return frames;
        }
    }
}
