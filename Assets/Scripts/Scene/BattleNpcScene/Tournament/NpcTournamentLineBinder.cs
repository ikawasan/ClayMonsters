using UnityEngine;
using UnityEngine.UI;

namespace Scene.BattleNpcScene.Tournament
{
    /// <summary>
    /// トーナメント表の直角ブラケット線
    /// 左右の子と親を短い縦線水平橋でつなぐ
    /// </summary>
    public sealed class NpcTournamentLineBinder : MaskableGraphic
    {
        [SerializeField] private RectTransform from;
        [SerializeField] private RectTransform sibling;
        [SerializeField] private RectTransform to;
        [SerializeField] private float thickness = 10f;

        /// <summary>
        /// 合流Yを子と親から求める
        /// </summary>
        /// <param name="childTopY">子Y</param>
        /// <param name="parentBottomY">親Y</param>
        /// <returns>合流Y</returns>
        public static float ResolveMergeY(float childTopY, float parentBottomY)
        {
            return NpcTournamentBracketLines.ResolveMergeY(childTopY, parentBottomY);
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
            if (from == null || to == null)
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
            if (color.a < 0.01f)
            {
                color = new Color(0.95f, 0.82f, 0.35f, 1f);
            }
        }

        /// <inheritdoc/>
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (from == null || to == null || thickness <= 0.01f)
            {
                return;
            }

            // 右側担当は描画しない(左側がペア全体を描く)
            if (sibling != null
                && from.anchoredPosition.x > sibling.anchoredPosition.x + 0.01f)
            {
                return;
            }

            Vector2 leftPoint = ToLocalPoint(from, new Vector2(0.5f, 0.5f));
            Vector2 parentPoint = ToLocalPoint(to, new Vector2(0.5f, 0.5f));
            Color32 tint = color;

            if (sibling == null)
            {
                DrawSingleElbow(vh, leftPoint, parentPoint, tint);
                return;
            }

            Vector2 rightPoint = ToLocalPoint(sibling, new Vector2(0.5f, 0.5f));
            float childY = Mathf.Max(leftPoint.y, rightPoint.y);
            float mergeY = NpcTournamentBracketLines.ResolveMergeY(childY, parentPoint.y);
            Vector2 leftElbow = new Vector2(leftPoint.x, mergeY);
            Vector2 rightElbow = new Vector2(rightPoint.x, mergeY);
            Vector2 stemBase = new Vector2(parentPoint.x, mergeY);

            AddSegment(vh, leftPoint, leftElbow, tint);
            AddSegment(vh, rightPoint, rightElbow, tint);
            AddSegment(vh, leftElbow, rightElbow, tint);
            AddJoint(vh, leftElbow, tint);
            AddJoint(vh, rightElbow, tint);
            if (Mathf.Abs(parentPoint.y - mergeY) > 0.01f)
            {
                AddSegment(vh, stemBase, parentPoint, tint);
                AddJoint(vh, stemBase, tint);
            }

            AddJoint(vh, parentPoint, tint);
        }

        private void DrawSingleElbow(
            VertexHelper vh,
            Vector2 childPoint,
            Vector2 parentPoint,
            Color32 tint)
        {
            float mergeY = NpcTournamentBracketLines.ResolveMergeY(childPoint.y, parentPoint.y);
            Vector2 childElbow = new Vector2(childPoint.x, mergeY);
            AddSegment(vh, childPoint, childElbow, tint);
            if (Mathf.Abs(parentPoint.y - mergeY) > 0.01f)
            {
                AddSegment(vh, childElbow, new Vector2(parentPoint.x, mergeY), tint);
                AddSegment(vh, new Vector2(parentPoint.x, mergeY), parentPoint, tint);
            }
            else
            {
                AddSegment(vh, childElbow, parentPoint, tint);
            }

            AddJoint(vh, childElbow, tint);
            AddJoint(vh, parentPoint, tint);
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

        private void AddJoint(VertexHelper vh, Vector2 center, Color32 tint)
        {
            float extent = thickness * 0.55f;
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

        private void AddSegment(VertexHelper vh, Vector2 start, Vector2 end, Color32 tint)
        {
            Vector2 delta = end - start;
            float length = delta.magnitude;
            if (length < 0.01f)
            {
                return;
            }

            Vector2 dir = delta / length;
            Vector2 normal = new Vector2(-dir.y, dir.x) * (thickness * 0.5f);
            float overlap = thickness * 0.5f;
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
