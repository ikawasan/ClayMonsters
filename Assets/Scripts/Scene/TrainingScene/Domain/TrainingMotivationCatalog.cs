using UnityEngine;

namespace Scene.TrainingScene.Domain
{
    /// <summary>
    /// やる気の表示と訓練倍率を解決する
    /// </summary>
    public static class TrainingMotivationCatalog
    {
        private const string ResourcesRoot = "Image/Training/Motivation/";
        private static readonly Sprite[][] frameCache = new Sprite[LevelCount][];

        /// <summary>
        /// 段階数
        /// </summary>
        public const int LevelCount = 4;

        /// <summary>
        /// 1段階あたりのアニメーション連番フレーム数
        /// </summary>
        public const int AnimationFrameCount = 4;

        /// <summary>
        /// 表示名を返す
        /// </summary>
        /// <param name="motivation">やる気</param>
        public static string GetDisplayName(TrainingMotivation motivation)
        {
            return motivation switch
            {
                TrainingMotivation.VeryLow => "絶不調",
                TrainingMotivation.Low => "不調",
                TrainingMotivation.Normal => "普通",
                TrainingMotivation.High => "絶好調",
                _ => motivation.ToString()
            };
        }

        /// <summary>
        /// HUD用のアイコン代替文字を返す
        /// </summary>
        /// <param name="motivation">やる気</param>
        public static string GetIconGlyph(TrainingMotivation motivation)
        {
            return motivation switch
            {
                TrainingMotivation.VeryLow => "↓",
                TrainingMotivation.Low => "△",
                TrainingMotivation.Normal => "○",
                TrainingMotivation.High => "◎",
                _ => "?"
            };
        }

        /// <summary>
        /// HUD1行表示を返す
        /// </summary>
        /// <param name="motivation">やる気</param>
        public static string FormatHudLine(TrainingMotivation motivation)
        {
            return $"やる気　{GetIconGlyph(motivation)}";
        }

        /// <summary>
        /// 訓練ステ上昇倍率を返す
        /// </summary>
        /// <param name="motivation">やる気</param>
        public static float GetTrainGainMultiplier(TrainingMotivation motivation)
        {
            return motivation switch
            {
                TrainingMotivation.VeryLow => TrainingSettings.MotivationTrainMultiplierVeryLow,
                TrainingMotivation.Low => TrainingSettings.MotivationTrainMultiplierLow,
                TrainingMotivation.Normal => TrainingSettings.MotivationTrainMultiplierNormal,
                TrainingMotivation.High => TrainingSettings.MotivationTrainMultiplierHigh,
                _ => 1f
            };
        }

        /// <summary>
        /// 範囲内へ丸める
        /// </summary>
        /// <param name="value">生値</param>
        public static TrainingMotivation Clamp(int value)
        {
            return (TrainingMotivation)Mathf.Clamp(value, 0, LevelCount - 1);
        }

        /// <summary>
        /// やる気アイコンの先頭フレームを返す
        /// </summary>
        /// <param name="motivation">やる気</param>
        public static Sprite ResolveIcon(TrainingMotivation motivation)
        {
            Sprite[] frames = ResolveIconFrames(motivation);
            return frames != null && frames.Length > 0 ? frames[0] : null;
        }

        /// <summary>
        /// やる気アイコンの連番アニメーションフレームを返す
        /// levelNスプライトシートを横4分割して返す
        /// </summary>
        /// <param name="motivation">やる気</param>
        public static Sprite[] ResolveIconFrames(TrainingMotivation motivation)
        {
            int index = (int)Clamp((int)motivation);
            if (frameCache[index] != null && frameCache[index].Length == AnimationFrameCount)
            {
                return frameCache[index];
            }

            string path = ResourcesRoot + $"level{index}";
            Texture2D sheet = Resources.Load<Texture2D>(path);
            if (sheet == null)
            {
                Debug.LogError(
                    $"[TrainingMotivationCatalog] やる気スプライトシートが見つかりません path={path}");
                frameCache[index] = System.Array.Empty<Sprite>();
                return frameCache[index];
            }

            if (sheet.width < AnimationFrameCount || sheet.height <= 0)
            {
                Debug.LogError(
                    $"[TrainingMotivationCatalog] やる気スプライトシートサイズが不正です path={path} size={sheet.width}x{sheet.height}");
                frameCache[index] = System.Array.Empty<Sprite>();
                return frameCache[index];
            }

            int frameWidth = sheet.width / AnimationFrameCount;
            if (frameWidth * AnimationFrameCount != sheet.width)
            {
                Debug.LogError(
                    $"[TrainingMotivationCatalog] やる気スプライトシート幅がフレーム数で割り切れません path={path} width={sheet.width}");
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
