using Battle;
using ClayEditor.Rigging;
using TMPro;
using UI.Battle.View;
using UnityEngine;

namespace Scene.TrainingScene.View
{
    /// <summary>
    /// 育成UI向け攻撃数値行
    /// シーン配置済みの数値テキストと間合いゲージへ表示を反映する
    /// </summary>
    public sealed class TrainingAttackStatsRowView : MonoBehaviour
    {
        [SerializeField] private TMP_Text statsText;
        [SerializeField] private MoveRangeSegmentBarView rangeBar;

        /// <summary>
        /// 表示内容を更新する
        /// </summary>
        /// <param name="attack">攻撃</param>
        /// <param name="statsTextValue">数値テキスト</param>
        public void Apply(MotionType attack, string statsTextValue)
        {
            if (statsText != null)
            {
                statsText.text = statsTextValue;
            }

            ApplyRange(attack);
        }

        /// <summary>
        /// 動的生成行向けに参照を設定する
        /// </summary>
        /// <param name="statsTextReference">数値テキスト</param>
        /// <param name="rangeBarReference">間合いゲージ</param>
        internal void ConfigureRuntime(TMP_Text statsTextReference, MoveRangeSegmentBarView rangeBarReference)
        {
            statsText = statsTextReference;
            rangeBar = rangeBarReference;
        }

        /// <summary>
        /// 指定幅での推奨高さを返す
        /// </summary>
        /// <param name="width">内容幅</param>
        internal float GetPreferredHeight(float width)
        {
            float textWidth = Mathf.Max(
                32f,
                width - MoveRangeSegmentBarView.CompactBarWidth - 8f);
            float textHeight = MeasureText(statsText, statsText != null ? statsText.text : string.Empty, textWidth);
            return Mathf.Max(textHeight, MoveRangeSegmentBarView.CompactBarHeight);
        }

        private void ApplyRange(MotionType attack)
        {
            if (rangeBar == null)
            {
                return;
            }

            Vector2 range = MotionPartRequirement.GetRange(attack);
            rangeBar.Apply(
                range.x,
                range.y,
                MoveRangeSegmentBarView.DefaultMaxDistance,
                usable: true);
        }

        private static float MeasureText(TMP_Text text, string value, float width)
        {
            if (text == null || string.IsNullOrEmpty(value))
            {
                return 0f;
            }

            Vector2 preferred = text.GetPreferredValues(value, width, 0f);
            return preferred.y;
        }
    }
}
