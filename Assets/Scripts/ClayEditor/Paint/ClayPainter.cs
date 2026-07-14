using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace ClayEditor.Paint
{
    /// <summary>
    /// 頂点カラーペイントの調整役 色やブラシ半径の保持と ペイント実行 Undo/Redoを担う
    /// ペイントはボクセル色バッファへ塗り メッシュ生成時に頂点へ反映される
    /// Undo/Redoはストローク記録で行い メモリ軽量に塗り直す
    /// </summary>
    public class ClayPainter : MonoBehaviour
    {
        [Inject] private readonly ClayVoxelEngine engine;
        [Inject] private readonly ClayPaintHistoryManager historyManager;

        [Header("Paint Settings")]
        [SerializeField] private float brushRadius = 2f;
        [SerializeField] private float minBrushRadius = 0.1f;
        [SerializeField] private float maxBrushRadius = 20f;
        [SerializeField] private Color initialColor = Color.red;

        private Color currentColor;

        // 塗り直し用に履歴のストロークを展開する使い回しバッファ
        private readonly List<Vector3> repaintCenters = new();
        private readonly List<float> repaintRadii = new();
        private readonly List<Color> repaintColors = new();

        /// <summary>
        /// 現在のペイントブラシ半径
        /// </summary>
        public float BrushRadius => brushRadius;

        /// <summary>
        /// 現在のペイント色
        /// </summary>
        public Color CurrentColor => currentColor;

        /// <summary>
        /// レイキャスト深度の基準Transform
        /// </summary>
        public Transform RaycastAnchor => engine.ClayModelTransform;

        private void Awake()
        {
            currentColor = initialColor;
        }

        /// <summary>
        /// ペイントに使用する色を設定する
        /// </summary>
        /// <param name="color">ペイントカラー</param>
        public void SetColor(Color color)
        {
            currentColor = color;
        }

        /// <summary>
        /// ブラシ半径を相対的に変更する
        /// </summary>
        /// <param name="delta">加算する半径量</param>
        public void ChangeBrushRadius(float delta)
        {
            brushRadius = Mathf.Clamp(brushRadius + delta, minBrushRadius, maxBrushRadius);
        }

        /// <summary>
        /// ペイントストロークを開始する（マウス押下時に呼ぶ）
        /// </summary>
        public void BeginStroke()
        {
            historyManager.BeginStroke();
        }

        /// <summary>
        /// ペイントストロークを終了して確定する（マウスを離した時に呼ぶ）
        /// </summary>
        public void EndStroke()
        {
            historyManager.EndStroke();
        }

        /// <summary>
        /// ワールド座標を中心に現在の色でボクセル色を塗る あわせて現在ストロークへ記録する
        /// </summary>
        /// <param name="worldPos">塗りの中心となるワールド座標</param>
        /// <param name="worldNormal">ヒット面の法線（ワールド座標）</param>
        public void PaintAtWorldPosition(Vector3 worldPos, Vector3 worldNormal)
        {
            engine.PaintVoxels(worldPos, brushRadius, currentColor, worldNormal);

            historyManager.AddDab(new PaintDab
            {
                worldCenter = worldPos,
                worldRadius = brushRadius,
                color = currentColor
            });
        }

        /// <summary>
        /// 直前のペイントストロークを取り消す 残ったストロークを塗り直して状態を再現する
        /// </summary>
        public void Undo()
        {
            if (historyManager.TryUndo())
            {
                Repaint();
            }
        }

        /// <summary>
        /// 取り消したペイントストロークをやり直す
        /// </summary>
        public void Redo()
        {
            if (historyManager.TryRedo())
            {
                Repaint();
            }
        }

        // 色を既定へ戻してから 履歴の全ストロークの塗り操作を順に塗り直す
        private void Repaint()
        {
            engine.ResetVoxelColors();

            repaintCenters.Clear();
            repaintRadii.Clear();
            repaintColors.Clear();

            var strokes = historyManager.Strokes;
            for (int s = 0; s < strokes.Count; s++)
            {
                var dabs = strokes[s];
                for (int d = 0; d < dabs.Count; d++)
                {
                    repaintCenters.Add(dabs[d].worldCenter);
                    repaintRadii.Add(dabs[d].worldRadius);
                    repaintColors.Add(dabs[d].color);
                }
            }

            engine.RepaintStrokes(repaintCenters, repaintRadii, repaintColors, repaintCenters.Count);
        }
    }
}