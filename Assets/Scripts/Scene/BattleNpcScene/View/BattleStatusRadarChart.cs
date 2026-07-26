using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Scene.BattleNpcScene.View
{
    /// <summary>
    /// 5ステータスを五角形レーダーで描画するUI
    /// 頂点順は上から時計回りにHP攻撃防御速さ命中
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattleStatusRadarChart : MaskableGraphic
    {
        private const int AxisCount = 5;

        private static readonly string[] AxisNames =
        {
            "HP",
            "攻撃",
            "防御",
            "速さ",
            "命中"
        };

        [SerializeField] private Color fillColor = new Color(0.35f, 0.75f, 1f, 0.45f);
        [SerializeField] private Color lineColor = new Color(0.85f, 0.95f, 1f, 0.95f);
        [SerializeField] private Color gridColor = new Color(1f, 1f, 1f, 0.22f);
        [SerializeField] private int gridRings = 4;
        [SerializeField] [Range(0.2f, 0.95f)] private float radiusRatio = 0.62f;

        // 頂点ラベルは上から時計回りにHP攻撃防御速さ命中の順で配線する
        [SerializeField] private TMP_Text[] axisTexts = new TMP_Text[AxisCount];

        private readonly float[] normalizedValues = { 0.2f, 0.2f, 0.2f, 0.2f, 0.2f };

        /// <summary>
        /// 塗りと線の色を設定する
        /// </summary>
        /// <param name="fill">塗り色</param>
        /// <param name="line">線色</param>
        public void SetColors(Color fill, Color line)
        {
            fillColor = fill;
            lineColor = line;
            SetVerticesDirty();
        }

        /// <summary>
        /// 実数値と表示上限から五角形と頂点ラベルを更新する
        /// </summary>
        /// <param name="values">HP攻撃防御速さ命中の実数値</param>
        /// <param name="maxValues">HP攻撃防御速さ命中の表示上限</param>
        public void SetStatusValues(int[] values, float[] maxValues)
        {
            if (values == null || values.Length < AxisCount)
            {
                Debug.LogError("[BattleStatusRadarChart] valuesが5要素ではありません", this);
                return;
            }

            if (maxValues == null || maxValues.Length < AxisCount)
            {
                Debug.LogError("[BattleStatusRadarChart] maxValuesが5要素ではありません", this);
                return;
            }

            for (int i = 0; i < AxisCount; i++)
            {
                float max = Mathf.Max(1f, maxValues[i]);
                normalizedValues[i] = Mathf.Clamp01(values[i] / max);
                ApplyAxisText(i, values[i]);
            }

            SetVerticesDirty();
        }

        /// <summary>
        /// 軸ラベル文言を返す
        /// </summary>
        /// <param name="index">頂点番号</param>
        public static string GetAxisName(int index)
        {
            if (index < 0 || index >= AxisNames.Length)
            {
                return string.Empty;
            }

            return AxisNames[index];
        }

        /// <inheritdoc/>
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = GetPixelAdjustedRect();
            Vector2 center = rect.center;
            float radius = Mathf.Min(rect.width, rect.height) * 0.5f * radiusRatio;
            if (radius <= 1f)
            {
                return;
            }

            int rings = Mathf.Max(1, gridRings);
            for (int ring = 1; ring <= rings; ring++)
            {
                float ringRadius = radius * (ring / (float)rings);
                AddPolygonOutline(vh, center, ringRadius, 1f, gridColor);
            }

            for (int i = 0; i < AxisCount; i++)
            {
                Vector2 tip = center + ResolveDirection(i) * radius;
                AddLine(vh, center, tip, 1.5f, gridColor);
            }

            Vector2[] valuePoints = new Vector2[AxisCount];
            for (int i = 0; i < AxisCount; i++)
            {
                float valueRadius = radius * Mathf.Max(0.04f, normalizedValues[i]);
                valuePoints[i] = center + ResolveDirection(i) * valueRadius;
            }

            AddFilledPolygon(vh, center, valuePoints, fillColor);
            AddPolygonOutline(vh, valuePoints, 2.2f, lineColor);
        }

        private void ApplyAxisText(int index, int value)
        {
            if (axisTexts == null || index >= axisTexts.Length)
            {
                return;
            }

            TMP_Text text = axisTexts[index];
            if (text == null)
            {
                Debug.LogError($"[BattleStatusRadarChart] axisTexts[{index}]が未配線です", this);
                return;
            }

            text.text = $"{AxisNames[index]}\n{value}";
        }

        private static Vector2 ResolveDirection(int index)
        {
            // 真上始まりの時計回り
            float angle = (90f - index * 72f) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        private static void AddFilledPolygon(VertexHelper vh, Vector2 center, Vector2[] points, Color color)
        {
            int centerIndex = vh.currentVertCount;
            vh.AddVert(center, color, Vector2.zero);
            int start = vh.currentVertCount;
            for (int i = 0; i < points.Length; i++)
            {
                vh.AddVert(points[i], color, Vector2.zero);
            }

            for (int i = 0; i < points.Length; i++)
            {
                int next = start + ((i + 1) % points.Length);
                vh.AddTriangle(centerIndex, start + i, next);
            }
        }

        private static void AddPolygonOutline(VertexHelper vh, Vector2 center, float radius, float thickness, Color color)
        {
            Vector2[] points = new Vector2[AxisCount];
            for (int i = 0; i < AxisCount; i++)
            {
                points[i] = center + ResolveDirection(i) * radius;
            }

            AddPolygonOutline(vh, points, thickness, color);
        }

        private static void AddPolygonOutline(VertexHelper vh, Vector2[] points, float thickness, Color color)
        {
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 a = points[i];
                Vector2 b = points[(i + 1) % points.Length];
                AddLine(vh, a, b, thickness, color);
            }
        }

        private static void AddLine(VertexHelper vh, Vector2 a, Vector2 b, float thickness, Color color)
        {
            Vector2 direction = b - a;
            if (direction.sqrMagnitude < 1e-6f)
            {
                return;
            }

            Vector2 normal = new Vector2(-direction.y, direction.x).normalized * (thickness * 0.5f);
            int start = vh.currentVertCount;
            vh.AddVert(a - normal, color, Vector2.zero);
            vh.AddVert(a + normal, color, Vector2.zero);
            vh.AddVert(b + normal, color, Vector2.zero);
            vh.AddVert(b - normal, color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
