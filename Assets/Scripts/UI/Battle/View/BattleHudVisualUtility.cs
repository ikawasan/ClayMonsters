using Extensions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Battle.View
{
    /// <summary>
    /// 戦闘HUDの見た目調整を行う(レイアウトは変更しない)
    /// </summary>
    internal static class BattleHudVisualUtility
    {
        /// <summary>
        /// テキストに縁取りを付けて視認性を上げる
        /// </summary>
        public static void ApplyLabelOutline(TMP_Text text, float width = 0.22f)
        {
            AppTmpFontUtility.ApplyOutline(
                text,
                width,
                new Color(0.06f, 0.08f, 0.12f, 0.92f));
        }

        /// <summary>
        /// 数値表示用の縁取りを付ける
        /// </summary>
        public static void ApplyValueOutline(TMP_Text text, float width = 0.28f)
        {
            AppTmpFontUtility.ApplyOutline(
                text,
                width,
                new Color(0.04f, 0.05f, 0.08f, 0.95f));
        }

        /// <summary>
        /// 決着テキスト用の縁取りを付ける
        /// </summary>
        public static void ApplyResultText(TMP_Text text, float width = 0.32f)
        {
            AppTmpFontUtility.ApplyOutline(
                text,
                width,
                new Color(0.04f, 0.05f, 0.08f, 0.95f));
        }

        /// <summary>
        /// ガッツスライダーへ見た目を適用する
        /// </summary>
        public static void ApplySliders(params Slider[] sliders)
        {
            if (sliders == null)
            {
                return;
            }

            for (int i = 0; i < sliders.Length; i++)
            {
                Slider slider = sliders[i];
                if (slider == null)
                {
                    continue;
                }

                if (slider.fillRect != null)
                {
                    Image fill = slider.fillRect.GetComponent<Image>();
                    if (fill != null)
                    {
                        fill.color = new Color(0.88f, 0.58f, 0.28f, 1f);
                    }
                }

                if (slider.handleRect != null)
                {
                    Image handle = slider.handleRect.GetComponent<Image>();
                    if (handle != null)
                    {
                        handle.color = new Color(0.97f, 0.95f, 0.91f, 1f);
                    }
                }
            }
        }

        /// <summary>
        /// 比率に応じたHP色を返す
        /// </summary>
        public static Color ResolveHpFillColor(float ratio, Color healthy, Color warning, Color critical)
        {
            ratio = Mathf.Clamp01(ratio);
            if (ratio <= 0.25f)
            {
                return critical;
            }

            if (ratio <= 0.5f)
            {
                return Color.Lerp(critical, warning, (ratio - 0.25f) / 0.25f);
            }

            return Color.Lerp(warning, healthy, (ratio - 0.5f) / 0.5f);
        }

        /// <summary>
        /// 間合い帯名からゲージ色を返す
        /// </summary>
        public static Color ResolveDistanceFillColor(string bandName, Color close, Color mid, Color far, Color fallback)
        {
            if (string.IsNullOrEmpty(bandName))
            {
                return fallback;
            }

            if (bandName.Contains("近"))
            {
                return close;
            }

            if (bandName.Contains("中"))
            {
                return mid;
            }

            if (bandName.Contains("遠"))
            {
                return far;
            }

            return fallback;
        }

        /// <summary>
        /// Imageの色だけを変える
        /// </summary>
        public static void SetImageColor(Image image, Color color)
        {
            if (image == null)
            {
                return;
            }

            image.color = color;
        }

        /// <summary>
        /// fillAmountを滑らかに近づける
        /// </summary>
        public static float SmoothFill(float current, float target, float speed, float deltaTime)
        {
            if (speed <= 0f)
            {
                return target;
            }

            return Mathf.Lerp(current, target, 1f - Mathf.Exp(-speed * deltaTime));
        }
    }
}
