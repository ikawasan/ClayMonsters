using UnityEngine;
using UnityEngine.UI;

namespace Scene.BattleNpcScene.Tournament
{
    /// <summary>
    /// トーナメント表の直角ブラケット線を一枚のメッシュで描く
    /// 通常は親高さで水平合流し優勝のみ茎を上へ伸ばす
    /// </summary>
    public sealed class NpcTournamentBracketLines : MaskableGraphic
    {
        /// <summary>
        /// 1試合目と同じ上方向の縦線長
        /// </summary>
        public const float VerticalSegmentLength = 145f;

        /// <summary>
        /// 親がこの倍数より遠いとき茎付き合流にする(優勝枠用)
        /// </summary>
        private const float StemGapFactor = 1.5f;

        [SerializeField] private float thickness = 14f;
        [SerializeField] private float outlineThickness = 6f;
        [SerializeField] private Color outlineColor = new Color(0.12f, 0.08f, 0.04f, 1f);

        private RectTransform[] leaves;
        private RectTransform[] quarters;
        private RectTransform[] semis;
        private RectTransform finalSlot;

        /// <summary>
        /// 合流Yを子点と親点から求める
        /// </summary>
        /// <param name="childY">子Y</param>
        /// <param name="parentY">親Y</param>
        /// <returns>合流Y</returns>
        public static float ResolveMergeY(float childY, float parentY)
        {
            float delta = parentY - childY;
            float direction = delta >= 0f ? 1f : -1f;
            float absGap = Mathf.Abs(delta);
            if (absGap < 0.01f)
            {
                return childY;
            }

            // 通常段は親高さで合流(縦線が2倍に見えない)
            // 優勝のように親が遠いときだけ1試合目と同じ長さで合流し茎を残す
            if (absGap > VerticalSegmentLength * StemGapFactor)
            {
                return childY + (direction * VerticalSegmentLength);
            }

            return parentY;
        }

        /// <summary>
        /// 茎API互換
        /// </summary>
        /// <param name="childY">子Y</param>
        /// <param name="parentY">親Y</param>
        /// <param name="minStemLength">未使用</param>
        /// <returns>合流Y</returns>
        public static float ResolveMergeYWithMinStem(
            float childY,
            float parentY,
            float minStemLength = 36f)
        {
            _ = minStemLength;
            return ResolveMergeY(childY, parentY);
        }

        /// <summary>
        /// スロット参照を受け取り線を更新する
        /// </summary>
        /// <param name="leafRects">葉</param>
        /// <param name="quarterRects">準々</param>
        /// <param name="semiRects">準</param>
        /// <param name="finalRect">決勝</param>
        public void Bind(
            RectTransform[] leafRects,
            RectTransform[] quarterRects,
            RectTransform[] semiRects,
            RectTransform finalRect)
        {
            leaves = leafRects;
            quarters = quarterRects;
            semis = semiRects;
            finalSlot = finalRect;
            SetVerticesDirty();
        }

        /// <summary>
        /// 線位置を更新する
        /// </summary>
        public void Apply()
        {
            SetVerticesDirty();
        }

        private void LateUpdate()
        {
            if (leaves == null || leaves.Length == 0)
            {
                return;
            }

            SetVerticesDirty();
        }

        /// <inheritdoc/>
        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
            if (material == null)
            {
                material = defaultGraphicMaterial;
            }

            if (color.a < 0.01f)
            {
                color = new Color(0.95f, 0.82f, 0.35f, 1f);
            }
        }

        /// <inheritdoc/>
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (thickness <= 0.01f)
            {
                return;
            }

            // 輪郭を先に描き本体で上書きする
            if (outlineThickness > 0.01f && outlineColor.a > 0.01f)
            {
                DrawAllPairs(vh, thickness + (outlineThickness * 2f), outlineColor);
            }

            DrawAllPairs(vh, thickness, color);
        }

        private void DrawAllPairs(VertexHelper vh, float lineThickness, Color32 tint)
        {
            if (leaves != null && leaves.Length >= 8 && quarters != null && quarters.Length >= 4)
            {
                DrawPair(vh, leaves[0], leaves[1], quarters[0], lineThickness, tint);
                DrawPair(vh, leaves[2], leaves[3], quarters[1], lineThickness, tint);
                DrawPair(vh, leaves[4], leaves[5], quarters[2], lineThickness, tint);
                DrawPair(vh, leaves[6], leaves[7], quarters[3], lineThickness, tint);
            }

            if (quarters != null && quarters.Length >= 4 && semis != null && semis.Length >= 2)
            {
                DrawPair(vh, quarters[0], quarters[1], semis[0], lineThickness, tint);
                DrawPair(vh, quarters[2], quarters[3], semis[1], lineThickness, tint);
            }

            // 準決勝→優勝は茎を上へ伸ばす
            if (semis != null && semis.Length >= 2 && finalSlot != null)
            {
                DrawPair(vh, semis[0], semis[1], finalSlot, lineThickness, tint);
            }
        }

        private void DrawPair(
            VertexHelper vh,
            RectTransform left,
            RectTransform right,
            RectTransform parent,
            float lineThickness,
            Color32 tint)
        {
            if (left == null || right == null || parent == null)
            {
                return;
            }

            Vector2 leftPoint = ToLocalPoint(left, new Vector2(0.5f, 0.5f));
            Vector2 rightPoint = ToLocalPoint(right, new Vector2(0.5f, 0.5f));
            Vector2 parentPoint = ToLocalPoint(parent, new Vector2(0.5f, 0.5f));
            float childY = Mathf.Max(leftPoint.y, rightPoint.y);
            float mergeY = ResolveMergeY(childY, parentPoint.y);

            Vector2 leftElbow = new Vector2(leftPoint.x, mergeY);
            Vector2 rightElbow = new Vector2(rightPoint.x, mergeY);
            Vector2 stemBase = new Vector2(parentPoint.x, mergeY);

            AddSegment(vh, leftPoint, leftElbow, lineThickness, tint);
            AddSegment(vh, rightPoint, rightElbow, lineThickness, tint);
            AddSegment(vh, leftElbow, rightElbow, lineThickness, tint);
            // 肘のみ継ぎ目を埋める(スロット中心へのジョイントは謎画像に見える)
            AddJoint(vh, leftElbow, lineThickness, tint);
            AddJoint(vh, rightElbow, lineThickness, tint);

            // 親が合流より上(優勝)なら茎を描く
            if (Mathf.Abs(parentPoint.y - mergeY) > 0.01f)
            {
                AddSegment(vh, stemBase, parentPoint, lineThickness, tint);
                AddJoint(vh, stemBase, lineThickness, tint);
            }
        }

        private Vector2 ToLocalPoint(RectTransform target, Vector2 normalizedPivot)
        {
            Rect rect = target.rect;
            Vector2 local = new Vector2(
                Mathf.Lerp(rect.xMin, rect.xMax, normalizedPivot.x),
                Mathf.Lerp(rect.yMin, rect.yMax, normalizedPivot.y));
            Vector3 world = target.TransformPoint(local);
            return rectTransform.InverseTransformPoint(world);
        }

        private static void AddJoint(
            VertexHelper vh,
            Vector2 center,
            float lineThickness,
            Color32 tint)
        {
            float extent = lineThickness * 0.65f;
            Vector2 bl = center + new Vector2(-extent, -extent);
            Vector2 tl = center + new Vector2(-extent, extent);
            Vector2 tr = center + new Vector2(extent, extent);
            Vector2 br = center + new Vector2(extent, -extent);
            int index = vh.currentVertCount;
            vh.AddVert(bl, tint, Vector2.zero);
            vh.AddVert(tl, tint, Vector2.zero);
            vh.AddVert(tr, tint, Vector2.zero);
            vh.AddVert(br, tint, Vector2.zero);
            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index, index + 2, index + 3);
        }

        private static void AddSegment(
            VertexHelper vh,
            Vector2 start,
            Vector2 end,
            float lineThickness,
            Color32 tint)
        {
            Vector2 delta = end - start;
            float length = delta.magnitude;
            if (length < 0.01f)
            {
                return;
            }

            Vector2 dir = delta / length;
            Vector2 normal = new Vector2(-dir.y, dir.x) * (lineThickness * 0.5f);
            float overlap = lineThickness * 0.65f;
            Vector2 a = start - (dir * overlap);
            Vector2 b = end + (dir * overlap);

            int index = vh.currentVertCount;
            vh.AddVert(a - normal, tint, Vector2.zero);
            vh.AddVert(a + normal, tint, Vector2.zero);
            vh.AddVert(b + normal, tint, Vector2.zero);
            vh.AddVert(b - normal, tint, Vector2.zero);
            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
